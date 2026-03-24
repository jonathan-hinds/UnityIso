using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Burst;
#endif

[DisallowMultipleComponent]
public sealed class TacticsCoopSessionCoordinator : MonoBehaviour
{
    private const ushort DefaultPort = 7777;
    private const int ExpectedPlayerCount = 2;
    private const int HostPartyIndex = 0;
    private const int ClientPartyIndex = 1;

    private const string PartySelectionMessageName = "tactics.party.selection";
    private const string BattleStartMessageName = "tactics.battle.start";
    private const string MoveRequestMessageName = "tactics.command.move.request";
    private const string MoveCommandMessageName = "tactics.command.move.execute";
    private const string AbilityRequestMessageName = "tactics.command.ability.request";
    private const string AbilityCommandMessageName = "tactics.command.ability.execute";
    private const string EndTurnRequestMessageName = "tactics.command.endturn.request";
    private const string EndTurnCommandMessageName = "tactics.command.endturn.execute";
    private const string ExitSessionRequestMessageName = "tactics.session.exit.request";
    private const string ExitSessionCommandMessageName = "tactics.session.exit.command";

    private readonly Dictionary<ulong, List<string>> partySelectionsByClientId = new();
    private readonly Queue<ReplicatedCommandEnvelope> pendingReplicatedCommands = new();

    private NetworkManager networkManager;
    private UnityTransport transport;
    private TacticsPartySelectionService partySelectionService;
    private bool handlersRegistered;
    private bool isMatchStarting;
    private bool pendingClientPartySubmission;

    public event Action<string> StatusChanged;
    public event Action<TacticsCoopBattleSetup> BattleSetupReady;
    public event Action SessionEnded;

    public bool IsOnlineSession => networkManager != null && networkManager.IsListening;
    public bool IsHostAuthority => networkManager != null && (networkManager.IsHost || networkManager.IsServer);
    public bool CanRunAutomatedTurns => !IsOnlineSession || IsHostAuthority;
    public int LocalPartyIndex
    {
        get
        {
            if (!IsOnlineSession)
            {
                return HostPartyIndex;
            }

            return IsHostAuthority ? HostPartyIndex : ClientPartyIndex;
        }
    }

    private void OnDestroy()
    {
        UnregisterNetworkCallbacks();

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }

    public void AssignPartySelectionService(TacticsPartySelectionService selectionService)
    {
        partySelectionService = selectionService;
    }

    public bool StartHost()
    {
        EnsureNetworkStack();
        ResetSessionState();
        ConfigureHostTransport();
        ApplyEditorBurstWorkaround();

        if (!networkManager.StartHost())
        {
            EmitStatus("Failed to start host.");
            return false;
        }

        RegisterNetworkCallbacks();
        partySelectionsByClientId[networkManager.LocalClientId] = GetLocalPartyCharacterIds();
        EmitStatus($"Hosting co-op on 127.0.0.1:{DefaultPort}. Waiting for a second player...");
        TryStartBattleIfReady();
        return true;
    }

    public bool StartClient(string address)
    {
        EnsureNetworkStack();
        ResetSessionState();
        ConfigureClientTransport(address);
        ApplyEditorBurstWorkaround();

        if (!networkManager.StartClient())
        {
            EmitStatus("Failed to start client.");
            return false;
        }

        RegisterNetworkCallbacks();
        pendingClientPartySubmission = true;
        EmitStatus($"Joining co-op at {ResolveAddress(address)}:{DefaultPort}...");
        return true;
    }

    private void Update()
    {
        if (pendingClientPartySubmission &&
            networkManager != null &&
            networkManager.IsConnectedClient &&
            !IsHostAuthority)
        {
            pendingClientPartySubmission = false;
            SendLocalPartySelectionToServer();
        }

        ProcessPendingReplicatedCommands();
    }

    public bool RequestMove(TacticsCharacterController character, Vector2Int targetTile)
    {
        if (character == null || !CanInitiateCommandForCharacter(character))
        {
            return false;
        }

        MoveCommandMessage message = new MoveCommandMessage
        {
            runtimeCharacterId = character.RuntimeCharacterId,
            targetX = targetTile.x,
            targetY = targetTile.y
        };

        if (!IsOnlineSession)
        {
            return ExecuteMoveCommand(message);
        }

        if (IsHostAuthority)
        {
            if (!ExecuteMoveCommand(message))
            {
                return false;
            }

            BroadcastMessage(MoveCommandMessageName, JsonUtility.ToJson(message), includeHost: false);
            return true;
        }

        SendMessageToServer(MoveRequestMessageName, JsonUtility.ToJson(message));
        return true;
    }

    public bool RequestUseAbility(TacticsCharacterController character, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        if (character == null || ability == null || !CanInitiateCommandForCharacter(character))
        {
            return false;
        }

        AbilityCommandMessage message = new AbilityCommandMessage
        {
            runtimeCharacterId = character.RuntimeCharacterId,
            abilityId = ability.AbilityId,
            targetX = targetTile.x,
            targetY = targetTile.y,
            randomStateJson = SerializeRandomState(UnityEngine.Random.state)
        };

        if (!IsOnlineSession)
        {
            return ExecuteAbilityCommand(message, applyProvidedRandomState: false);
        }

        if (IsHostAuthority)
        {
            if (!ExecuteAbilityCommand(message, applyProvidedRandomState: false))
            {
                return false;
            }

            BroadcastMessage(AbilityCommandMessageName, JsonUtility.ToJson(message), includeHost: false);
            return true;
        }

        SendMessageToServer(AbilityRequestMessageName, JsonUtility.ToJson(message));
        return true;
    }

    public bool RequestEndTurn(TacticsCharacterController character)
    {
        if (character == null || !CanInitiateCommandForCharacter(character))
        {
            return false;
        }

        EndTurnCommandMessage message = new EndTurnCommandMessage
        {
            runtimeCharacterId = character.RuntimeCharacterId
        };

        if (!IsOnlineSession)
        {
            return ExecuteEndTurnCommand(message);
        }

        if (IsHostAuthority)
        {
            if (!ExecuteEndTurnCommand(message))
            {
                return false;
            }

            BroadcastMessage(EndTurnCommandMessageName, JsonUtility.ToJson(message), includeHost: false);
            return true;
        }

        SendMessageToServer(EndTurnRequestMessageName, JsonUtility.ToJson(message));
        return true;
    }

    public bool RequestReturnToHome()
    {
        if (!IsOnlineSession)
        {
            EndSessionLocally();
            return true;
        }

        if (IsHostAuthority)
        {
            BroadcastMessage(ExitSessionCommandMessageName, "{}", includeHost: false);
            EndSessionLocally();
            return true;
        }

        SendMessageToServer(ExitSessionRequestMessageName, "{}");
        return true;
    }

    private void EnsureNetworkStack()
    {
        if (networkManager != null && transport != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("Tactics Coop Network Runtime");
        DontDestroyOnLoad(runtimeObject);

        transport = runtimeObject.AddComponent<UnityTransport>();
        networkManager = runtimeObject.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new NetworkConfig
        {
            EnableSceneManagement = false
        };
        networkManager.NetworkConfig.NetworkTransport = transport;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ApplyEditorBurstWorkaround()
    {
#if UNITY_EDITOR
        if (BurstCompiler.Options.EnableBurstCompilation)
        {
            BurstCompiler.Options.EnableBurstCompilation = false;
            EmitStatus("Editor multiplayer test mode: Burst disabled for transport startup stability.");
        }
#endif
    }

    private void ResetSessionState()
    {
        partySelectionsByClientId.Clear();
        pendingReplicatedCommands.Clear();
        isMatchStarting = false;
        pendingClientPartySubmission = false;
    }

    private void RegisterNetworkCallbacks()
    {
        if (networkManager == null || handlersRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

        CustomMessagingManager messaging = networkManager.CustomMessagingManager;
        messaging.RegisterNamedMessageHandler(PartySelectionMessageName, HandlePartySelectionMessage);
        messaging.RegisterNamedMessageHandler(BattleStartMessageName, HandleBattleStartMessage);
        messaging.RegisterNamedMessageHandler(MoveRequestMessageName, HandleMoveRequestMessage);
        messaging.RegisterNamedMessageHandler(MoveCommandMessageName, HandleMoveCommandMessage);
        messaging.RegisterNamedMessageHandler(AbilityRequestMessageName, HandleAbilityRequestMessage);
        messaging.RegisterNamedMessageHandler(AbilityCommandMessageName, HandleAbilityCommandMessage);
        messaging.RegisterNamedMessageHandler(EndTurnRequestMessageName, HandleEndTurnRequestMessage);
        messaging.RegisterNamedMessageHandler(EndTurnCommandMessageName, HandleEndTurnCommandMessage);
        messaging.RegisterNamedMessageHandler(ExitSessionRequestMessageName, HandleExitSessionRequestMessage);
        messaging.RegisterNamedMessageHandler(ExitSessionCommandMessageName, HandleExitSessionCommandMessage);
        handlersRegistered = true;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (networkManager == null || !handlersRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;

        CustomMessagingManager messaging = networkManager.CustomMessagingManager;
        if (messaging != null)
        {
            messaging.UnregisterNamedMessageHandler(PartySelectionMessageName);
            messaging.UnregisterNamedMessageHandler(BattleStartMessageName);
            messaging.UnregisterNamedMessageHandler(MoveRequestMessageName);
            messaging.UnregisterNamedMessageHandler(MoveCommandMessageName);
            messaging.UnregisterNamedMessageHandler(AbilityRequestMessageName);
            messaging.UnregisterNamedMessageHandler(AbilityCommandMessageName);
            messaging.UnregisterNamedMessageHandler(EndTurnRequestMessageName);
            messaging.UnregisterNamedMessageHandler(EndTurnCommandMessageName);
            messaging.UnregisterNamedMessageHandler(ExitSessionRequestMessageName);
            messaging.UnregisterNamedMessageHandler(ExitSessionCommandMessageName);
        }

        handlersRegistered = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager == null)
        {
            return;
        }

        if (clientId == networkManager.LocalClientId && !IsHostAuthority)
        {
            SendLocalPartySelectionToServer();
            return;
        }

        if (IsHostAuthority && clientId != networkManager.LocalClientId)
        {
            EmitStatus("Second player connected. Waiting for their team selection...");
            TryStartBattleIfReady();
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        partySelectionsByClientId.Remove(clientId);
        if (!IsOnlineSession)
        {
            return;
        }

        EmitStatus(clientId == networkManager.LocalClientId
            ? "Disconnected from co-op session."
            : "A player disconnected from the co-op session.");
    }

    private void HandlePartySelectionMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        PartySelectionMessage message = JsonUtility.FromJson<PartySelectionMessage>(payload);
        partySelectionsByClientId[senderClientId] = message?.characterIds ?? new List<string>();
        EmitStatus("Both clients are connected. Finalizing battle setup...");
        TryStartBattleIfReady();
    }

    private void HandleBattleStartMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        TacticsCoopBattleSetup battleSetup = JsonUtility.FromJson<TacticsCoopBattleSetup>(payload);
        EmitStatus("Co-op session ready. Launching battle...");
        BattleSetupReady?.Invoke(battleSetup);
    }

    private void HandleMoveRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        MoveCommandMessage message = JsonUtility.FromJson<MoveCommandMessage>(payload);
        if (!CanClientControlCharacter(senderClientId, message.runtimeCharacterId))
        {
            return;
        }

        if (ExecuteMoveCommand(message))
        {
            BroadcastMessage(MoveCommandMessageName, payload, includeHost: false);
        }
    }

    private void HandleMoveCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EnqueueReplicatedCommand(ReplicatedCommandType.Move, ReadPayload(ref reader));
    }

    private void HandleAbilityRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        AbilityCommandMessage message = JsonUtility.FromJson<AbilityCommandMessage>(payload);
        if (!CanClientControlCharacter(senderClientId, message.runtimeCharacterId))
        {
            return;
        }

        message.randomStateJson = SerializeRandomState(UnityEngine.Random.state);
        string resolvedPayload = JsonUtility.ToJson(message);

        if (ExecuteAbilityCommand(message, applyProvidedRandomState: false))
        {
            BroadcastMessage(AbilityCommandMessageName, resolvedPayload, includeHost: false);
        }
    }

    private void HandleAbilityCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EnqueueReplicatedCommand(ReplicatedCommandType.Ability, ReadPayload(ref reader));
    }

    private void HandleEndTurnRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        EndTurnCommandMessage message = JsonUtility.FromJson<EndTurnCommandMessage>(payload);
        if (!CanClientControlCharacter(senderClientId, message.runtimeCharacterId))
        {
            return;
        }

        if (ExecuteEndTurnCommand(message))
        {
            BroadcastMessage(EndTurnCommandMessageName, payload, includeHost: false);
        }
    }

    private void HandleEndTurnCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EnqueueReplicatedCommand(ReplicatedCommandType.EndTurn, ReadPayload(ref reader));
    }

    private void HandleExitSessionRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        BroadcastMessage(ExitSessionCommandMessageName, "{}", includeHost: false);
        EndSessionLocally();
    }

    private void HandleExitSessionCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EndSessionLocally();
    }

    private bool ExecuteMoveCommand(MoveCommandMessage message, bool requireLocalAuthorityState = true)
    {
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        if (character == null)
        {
            return false;
        }

        Vector2Int destination = new Vector2Int(message.targetX, message.targetY);
        return requireLocalAuthorityState
            ? character.TryMoveTo(destination)
            : character.ApplyReplicatedMove(destination);
    }

    private bool ExecuteAbilityCommand(
        AbilityCommandMessage message,
        bool applyProvidedRandomState,
        bool requireLocalAuthorityState = true)
    {
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        TacticsAbilityDefinition ability = FindAbility(character, message.abilityId);
        TacticsCombatSystem combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        if (character == null || ability == null || combatSystem == null)
        {
            return false;
        }

        if (applyProvidedRandomState && TryDeserializeRandomState(message.randomStateJson, out UnityEngine.Random.State state))
        {
            UnityEngine.Random.state = state;
        }

        Vector2Int targetTile = new Vector2Int(message.targetX, message.targetY);
        return requireLocalAuthorityState
            ? combatSystem.TryUseAbility(character, ability, targetTile)
            : combatSystem.ApplyReplicatedAbility(character, ability, targetTile);
    }

    private bool ExecuteEndTurnCommand(EndTurnCommandMessage message, bool requireLocalAuthorityState = true)
    {
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        if (character == null)
        {
            return false;
        }

        return requireLocalAuthorityState
            ? character.TryEndTurn()
            : character.ApplyReplicatedEndTurn();
    }

    private void TryStartBattleIfReady()
    {
        if (!IsHostAuthority || isMatchStarting || networkManager == null)
        {
            return;
        }

        if (networkManager.ConnectedClientsIds.Count < ExpectedPlayerCount)
        {
            return;
        }

        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            if (!partySelectionsByClientId.TryGetValue(clientId, out List<string> partyIds) || partyIds == null || partyIds.Count == 0)
            {
                return;
            }
        }

        ulong hostClientId = networkManager.LocalClientId;
        ulong remoteClientId = 0;
        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            if (clientId != hostClientId)
            {
                remoteClientId = clientId;
                break;
            }
        }

        TacticsCoopBattleSetup battleSetup = new TacticsCoopBattleSetup
        {
            hostPartyCharacterIds = new List<string>(partySelectionsByClientId[hostClientId]),
            clientPartyCharacterIds = remoteClientId != 0 && partySelectionsByClientId.TryGetValue(remoteClientId, out List<string> remoteParty)
                ? new List<string>(remoteParty)
                : new List<string>()
        };

        isMatchStarting = true;
        EmitStatus("Both players are ready. Starting co-op battle...");
        BroadcastMessage(BattleStartMessageName, JsonUtility.ToJson(battleSetup), includeHost: false);
        BattleSetupReady?.Invoke(battleSetup);
    }

    private List<string> GetLocalPartyCharacterIds()
    {
        List<string> result = new();
        TacticsPartySelection selection = partySelectionService?.LoadSelection();
        if (selection == null)
        {
            return result;
        }

        for (int i = 0; i < selection.Capacity; i++)
        {
            string characterId = selection.GetCharacterId(i);
            if (!string.IsNullOrEmpty(characterId))
            {
                result.Add(characterId);
            }
        }

        return result;
    }

    private void ConfigureHostTransport()
    {
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
        }
    }

    private void ConfigureClientTransport(string address)
    {
        if (transport != null)
        {
            transport.SetConnectionData(ResolveAddress(address), DefaultPort);
        }
    }

    private static string ResolveAddress(string address)
    {
        return string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
    }

    public bool CanLocalPlayerControlCharacter(TacticsCharacterController character)
    {
        if (character == null || !character.IsPlayerControlled)
        {
            return false;
        }

        if (!IsOnlineSession)
        {
            return true;
        }

        return TryGetPartyIndex(character.RuntimeCharacterId, out int partyIndex) &&
               partyIndex == LocalPartyIndex;
    }

    private bool CanInitiateCommandForCharacter(TacticsCharacterController character)
    {
        if (character == null)
        {
            return false;
        }

        if (!character.IsPlayerControlled)
        {
            return !IsOnlineSession || IsHostAuthority;
        }

        return CanLocalPlayerControlCharacter(character);
    }

    private bool CanClientControlCharacter(ulong clientId, string runtimeCharacterId)
    {
        if (!TryGetPartyIndex(runtimeCharacterId, out int partyIndex))
        {
            return false;
        }

        int expectedPartyIndex = clientId == networkManager?.LocalClientId
            ? HostPartyIndex
            : ClientPartyIndex;
        return partyIndex == expectedPartyIndex;
    }

    private static bool TryGetPartyIndex(string runtimeCharacterId, out int partyIndex)
    {
        partyIndex = -1;
        if (string.IsNullOrWhiteSpace(runtimeCharacterId))
        {
            return false;
        }

        string[] tokens = runtimeCharacterId.Split('_');
        if (tokens.Length < 2 || !string.Equals(tokens[0], "party", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(tokens[1], out partyIndex);
    }

    private void BroadcastMessage(string messageName, string payload, bool includeHost)
    {
        if (networkManager?.CustomMessagingManager == null)
        {
            return;
        }

        List<ulong> recipients = new();
        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            if (!includeHost && clientId == networkManager.LocalClientId)
            {
                continue;
            }

            recipients.Add(clientId);
        }

        if (recipients.Count == 0)
        {
            return;
        }

        using FastBufferWriter writer = new(FastBufferWriter.GetWriteSize(payload), Allocator.Temp);
        writer.WriteValueSafe(payload);
        networkManager.CustomMessagingManager.SendNamedMessage(messageName, recipients, writer, NetworkDelivery.ReliableSequenced);
    }

    private void SendMessageToServer(string messageName, string payload)
    {
        if (networkManager?.CustomMessagingManager == null)
        {
            return;
        }

        using FastBufferWriter writer = new(FastBufferWriter.GetWriteSize(payload), Allocator.Temp);
        writer.WriteValueSafe(payload);
        networkManager.CustomMessagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
    }

    private static string ReadPayload(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out string payload);
        return payload;
    }

    private static TacticsCharacterController FindCharacterByRuntimeId(string runtimeCharacterId)
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character != null &&
                string.Equals(character.RuntimeCharacterId, runtimeCharacterId, StringComparison.OrdinalIgnoreCase))
            {
                return character;
            }
        }

        return null;
    }

    private static TacticsAbilityDefinition FindAbility(TacticsCharacterController character, string abilityId)
    {
        if (character == null || string.IsNullOrWhiteSpace(abilityId))
        {
            return null;
        }

        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability != null &&
                string.Equals(ability.AbilityId, abilityId, StringComparison.OrdinalIgnoreCase))
            {
                return ability;
            }
        }

        return null;
    }

    private static string SerializeRandomState(UnityEngine.Random.State state)
    {
        return JsonUtility.ToJson(new RandomStatePayload
        {
            state = state
        });
    }

    private static bool TryDeserializeRandomState(string json, out UnityEngine.Random.State state)
    {
        state = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        RandomStatePayload payload = JsonUtility.FromJson<RandomStatePayload>(json);
        state = payload.state;
        return true;
    }

    private void EmitStatus(string message)
    {
        StatusChanged?.Invoke(message);
    }

    private void EnqueueReplicatedCommand(ReplicatedCommandType commandType, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        pendingReplicatedCommands.Enqueue(new ReplicatedCommandEnvelope(commandType, payload));
        ProcessPendingReplicatedCommands();
    }

    private void ProcessPendingReplicatedCommands()
    {
        if (IsHostAuthority || pendingReplicatedCommands.Count == 0)
        {
            return;
        }

        ReplicatedCommandEnvelope nextCommand = pendingReplicatedCommands.Peek();
        if (!TryExecuteReplicatedCommand(nextCommand))
        {
            return;
        }

        pendingReplicatedCommands.Dequeue();
    }

    private bool TryExecuteReplicatedCommand(ReplicatedCommandEnvelope command)
    {
        return command.CommandType switch
        {
            ReplicatedCommandType.Move => ExecuteMoveCommand(
                JsonUtility.FromJson<MoveCommandMessage>(command.Payload),
                requireLocalAuthorityState: false),
            ReplicatedCommandType.Ability => ExecuteAbilityCommand(
                JsonUtility.FromJson<AbilityCommandMessage>(command.Payload),
                applyProvidedRandomState: true,
                requireLocalAuthorityState: false),
            ReplicatedCommandType.EndTurn => ExecuteEndTurnCommand(
                JsonUtility.FromJson<EndTurnCommandMessage>(command.Payload),
                requireLocalAuthorityState: false),
            _ => false
        };
    }

    private void SendLocalPartySelectionToServer()
    {
        EmitStatus("Connected. Sending team selection to host...");
        SendMessageToServer(PartySelectionMessageName, JsonUtility.ToJson(new PartySelectionMessage
        {
            characterIds = GetLocalPartyCharacterIds()
        }));
    }

    private void EndSessionLocally()
    {
        pendingReplicatedCommands.Clear();
        partySelectionsByClientId.Clear();
        isMatchStarting = false;
        pendingClientPartySubmission = false;
        UnregisterNetworkCallbacks();

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        EmitStatus("Returning to home screen...");
        SessionEnded?.Invoke();
    }

    [Serializable]
    private sealed class PartySelectionMessage
    {
        public List<string> characterIds = new();
    }

    [Serializable]
    private struct MoveCommandMessage
    {
        public string runtimeCharacterId;
        public int targetX;
        public int targetY;
    }

    [Serializable]
    private struct AbilityCommandMessage
    {
        public string runtimeCharacterId;
        public string abilityId;
        public int targetX;
        public int targetY;
        public string randomStateJson;
    }

    [Serializable]
    private struct EndTurnCommandMessage
    {
        public string runtimeCharacterId;
    }

    [Serializable]
    private struct RandomStatePayload
    {
        public UnityEngine.Random.State state;
    }

    private readonly struct ReplicatedCommandEnvelope
    {
        public ReplicatedCommandEnvelope(ReplicatedCommandType commandType, string payload)
        {
            CommandType = commandType;
            Payload = payload;
        }

        public ReplicatedCommandType CommandType { get; }
        public string Payload { get; }
    }

    private enum ReplicatedCommandType
    {
        Move = 0,
        Ability = 1,
        EndTurn = 2
    }
}
