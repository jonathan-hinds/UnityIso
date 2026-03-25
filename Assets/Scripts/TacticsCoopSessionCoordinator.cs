using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Burst;
#endif

[DisallowMultipleComponent]
public sealed class TacticsCoopSessionCoordinator : MonoBehaviour
{
    private const int ExpectedPlayerCount = 2;
    private const int HostPartyIndex = 0;
    private const int ClientPartyIndex = 1;
    private const NetworkDelivery NamedMessageDelivery = NetworkDelivery.ReliableFragmentedSequenced;
    private const string RelayConnectionType = "dtls";

    private const string PartySelectionMessageName = "tactics.party.selection";
    private const string BattleStartMessageName = "tactics.battle.start";
    private const string MoveRequestMessageName = "tactics.command.move.request";
    private const string MoveCommandMessageName = "tactics.command.move.execute";
    private const string AbilityRequestMessageName = "tactics.command.ability.request";
    private const string AbilityCommandMessageName = "tactics.command.ability.execute";
    private const string EndTurnRequestMessageName = "tactics.command.endturn.request";
    private const string EndTurnCommandMessageName = "tactics.command.endturn.execute";
    private const string CommitProgressionRequestMessageName = "tactics.command.progression.request";
    private const string CommitProgressionCommandMessageName = "tactics.command.progression.execute";
    private const string OpenChestRequestMessageName = "tactics.command.chest.request";
    private const string OpenChestCommandMessageName = "tactics.command.chest.execute";
    private const string ExitSessionRequestMessageName = "tactics.session.exit.request";
    private const string ExitSessionCommandMessageName = "tactics.session.exit.command";

    private readonly Dictionary<ulong, List<TacticsCoopCharacterLoadout>> partySelectionsByClientId = new();
    private readonly Queue<ReplicatedCommandEnvelope> pendingReplicatedCommands = new();

    private NetworkManager networkManager;
    private UnityTransport transport;
    private TacticsPartySelectionService partySelectionService;
    private TacticsCharacterProgressionService progressionService;
    private TacticsPlayerCurrencyService currencyService;
    private bool handlersRegistered;
    private bool isMatchStarting;
    private bool pendingClientPartySubmission;
    private string activeRelayJoinCode = string.Empty;
    private TacticsMatchGenerationSettings pendingHostMatchSettings;

    public event Action<string> StatusChanged;
    public event Action<TacticsCoopBattleSetup> BattleSetupReady;
    public event Action SessionEnded;

    public bool IsOnlineSession => networkManager != null && networkManager.IsListening;
    public bool IsHostAuthority => networkManager != null && (networkManager.IsHost || networkManager.IsServer);
    public bool CanRunAutomatedTurns => !IsOnlineSession || IsHostAuthority;
    public string ActiveRelayJoinCode => activeRelayJoinCode;
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

    public void AssignCharacterProgressionService(TacticsCharacterProgressionService service)
    {
        progressionService = service;
    }

    public void AssignCurrencyService(TacticsPlayerCurrencyService service)
    {
        currencyService = service;
    }

    public async Task<bool> StartHostAsync(TacticsMatchGenerationSettings matchSettings)
    {
        EnsureNetworkStack();
        StopActiveSession();
        ResetSessionState();
        pendingHostMatchSettings = matchSettings?.Clone() ?? new TacticsMatchGenerationSettings();
        pendingHostMatchSettings.Sanitize();
        if (!await TryConfigureHostTransportAsync())
        {
            return false;
        }

        ApplyEditorBurstWorkaround();

        if (!networkManager.StartHost())
        {
            EmitStatus("Failed to start Relay host.");
            return false;
        }

        RegisterNetworkCallbacks();
        partySelectionsByClientId[networkManager.LocalClientId] = GetLocalPartyLoadout();
        EmitStatus($"Relay host ready. Share join code {activeRelayJoinCode}. Waiting for a second player...");
        TryStartBattleIfReady();
        return true;
    }

    public async Task<bool> StartClientAsync(string joinCode)
    {
        EnsureNetworkStack();
        StopActiveSession();
        ResetSessionState();
        if (!await TryConfigureClientTransportAsync(joinCode))
        {
            return false;
        }

        ApplyEditorBurstWorkaround();

        if (!networkManager.StartClient())
        {
            EmitStatus("Failed to start Relay client.");
            return false;
        }

        RegisterNetworkCallbacks();
        pendingClientPartySubmission = true;
        EmitStatus($"Joining Relay session with code {activeRelayJoinCode}...");
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

    public bool RequestCommitProgression(TacticsCharacterController character, TacticsCharacterProgressionSnapshot snapshot)
    {
        if (character == null || !CanInitiateCommandForCharacter(character))
        {
            return false;
        }

        CommitProgressionCommandMessage message = new CommitProgressionCommandMessage
        {
            runtimeCharacterId = character.RuntimeCharacterId,
            snapshot = snapshot.Sanitize()
        };

        if (!IsOnlineSession)
        {
            return ExecuteCommitProgressionCommand(message);
        }

        if (IsHostAuthority)
        {
            if (!ExecuteCommitProgressionCommand(message))
            {
                return false;
            }

            BroadcastMessage(CommitProgressionCommandMessageName, JsonUtility.ToJson(message), includeHost: false);
            return true;
        }

        SendMessageToServer(CommitProgressionRequestMessageName, JsonUtility.ToJson(message));
        return true;
    }

    public bool RequestOpenChest(TacticsCharacterController character, TacticsChestController chest)
    {
        if (character == null || chest == null || !CanInitiateCommandForCharacter(character))
        {
            return false;
        }

        OpenChestCommandMessage message = BuildOpenChestCommandMessage(character, chest);

        if (!IsOnlineSession)
        {
            return ExecuteOpenChestCommand(message);
        }

        if (IsHostAuthority)
        {
            if (!ExecuteOpenChestCommand(message))
            {
                return false;
            }

            BroadcastMessage(OpenChestCommandMessageName, JsonUtility.ToJson(message), includeHost: false);
            return true;
        }

        SendMessageToServer(OpenChestRequestMessageName, JsonUtility.ToJson(message));
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
        activeRelayJoinCode = string.Empty;
        pendingHostMatchSettings = null;
    }

    private void StopActiveSession()
    {
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        UnregisterNetworkCallbacks();
        networkManager.Shutdown();
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
        messaging.RegisterNamedMessageHandler(CommitProgressionRequestMessageName, HandleCommitProgressionRequestMessage);
        messaging.RegisterNamedMessageHandler(CommitProgressionCommandMessageName, HandleCommitProgressionCommandMessage);
        messaging.RegisterNamedMessageHandler(OpenChestRequestMessageName, HandleOpenChestRequestMessage);
        messaging.RegisterNamedMessageHandler(OpenChestCommandMessageName, HandleOpenChestCommandMessage);
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
            messaging.UnregisterNamedMessageHandler(CommitProgressionRequestMessageName);
            messaging.UnregisterNamedMessageHandler(CommitProgressionCommandMessageName);
            messaging.UnregisterNamedMessageHandler(OpenChestRequestMessageName);
            messaging.UnregisterNamedMessageHandler(OpenChestCommandMessageName);
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
        partySelectionsByClientId[senderClientId] = message?.characters ?? new List<TacticsCoopCharacterLoadout>();
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

    private void HandleCommitProgressionRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        CommitProgressionCommandMessage message = JsonUtility.FromJson<CommitProgressionCommandMessage>(payload);
        if (!CanClientControlCharacter(senderClientId, message.runtimeCharacterId))
        {
            return;
        }

        if (ExecuteCommitProgressionCommand(message))
        {
            BroadcastMessage(CommitProgressionCommandMessageName, payload, includeHost: false);
        }
    }

    private void HandleCommitProgressionCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EnqueueReplicatedCommand(ReplicatedCommandType.CommitProgression, ReadPayload(ref reader));
    }

    private void HandleOpenChestRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        OpenChestCommandMessage message = JsonUtility.FromJson<OpenChestCommandMessage>(payload);
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        TacticsChestController chest = TacticsChestController.FindByRuntimeId(message.runtimeChestId);
        if (!CanClientControlCharacter(senderClientId, message.runtimeCharacterId) ||
            character == null ||
            chest == null)
        {
            return;
        }

        message = BuildOpenChestCommandMessage(character, chest);
        string resolvedPayload = JsonUtility.ToJson(message);
        if (ExecuteOpenChestCommand(message))
        {
            BroadcastMessage(OpenChestCommandMessageName, resolvedPayload, includeHost: false);
        }
    }

    private void HandleOpenChestCommandMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        EnqueueReplicatedCommand(ReplicatedCommandType.OpenChest, ReadPayload(ref reader));
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

    private bool ExecuteCommitProgressionCommand(CommitProgressionCommandMessage message)
    {
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        return character != null && character.TryCommitProgression(message.snapshot);
    }

    private bool ExecuteOpenChestCommand(OpenChestCommandMessage message, bool requireLocalAuthorityState = true)
    {
        TacticsCharacterController character = FindCharacterByRuntimeId(message.runtimeCharacterId);
        TacticsChestController chest = TacticsChestController.FindByRuntimeId(message.runtimeChestId);
        TacticsChestEncounterService chestEncounterService = FindFirstObjectByType<TacticsChestEncounterService>();
        if (character == null ||
            chest == null ||
            chestEncounterService == null ||
            !character.CanInteractThisTurn ||
            !chest.IsAdjacentAndInteractable(character))
        {
            return false;
        }

        if (!chestEncounterService.TryResolveChestOpen(
                character,
                chest,
                Mathf.Max(0, message.goldReward),
                message.mimicRuntimeCharacterId,
                out TacticsChestResolutionResult result))
        {
            return false;
        }

        bool consumed = result.RevealedMimic
            ? (requireLocalAuthorityState ? character.TryEndTurn() : character.ApplyReplicatedEndTurn())
            : (requireLocalAuthorityState ? character.TryConsumeInteraction() : character.ApplyReplicatedInteraction());
        if (!consumed)
        {
            return false;
        }

        if (currencyService != null &&
            result.GoldReward > 0 &&
            character.IsPlayerControlled &&
            CanLocalPlayerControlCharacter(character))
        {
            currencyService.AddGold(result.GoldReward);
        }

        return true;
    }

    private OpenChestCommandMessage BuildOpenChestCommandMessage(
        TacticsCharacterController character,
        TacticsChestController chest)
    {
        bool containsMimic = chest != null && chest.ContainsMimic;
        return new OpenChestCommandMessage
        {
            runtimeCharacterId = character != null ? character.RuntimeCharacterId : string.Empty,
            runtimeChestId = chest != null ? chest.RuntimeChestId : string.Empty,
            goldReward = containsMimic ? 0 : RollChestReward(),
            containsMimic = containsMimic,
            mimicRuntimeCharacterId = containsMimic ? BuildMimicRuntimeCharacterId(chest.RuntimeChestId) : string.Empty
        };
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
            if (!partySelectionsByClientId.TryGetValue(clientId, out List<TacticsCoopCharacterLoadout> partyIds) || partyIds == null || partyIds.Count == 0)
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
            hostPartyMembers = new List<TacticsCoopCharacterLoadout>(partySelectionsByClientId[hostClientId]),
            clientPartyMembers = remoteClientId != 0 && partySelectionsByClientId.TryGetValue(remoteClientId, out List<TacticsCoopCharacterLoadout> remoteParty)
                ? new List<TacticsCoopCharacterLoadout>(remoteParty)
                : new List<TacticsCoopCharacterLoadout>(),
            matchSettings = pendingHostMatchSettings?.Clone()
        };

        isMatchStarting = true;
        EmitStatus("Both players are ready. Starting co-op battle...");
        BroadcastMessage(BattleStartMessageName, JsonUtility.ToJson(battleSetup), includeHost: false);
        BattleSetupReady?.Invoke(battleSetup);
    }

    private List<TacticsCoopCharacterLoadout> GetLocalPartyLoadout()
    {
        List<TacticsCoopCharacterLoadout> result = new();
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
                result.Add(new TacticsCoopCharacterLoadout
                {
                    characterId = characterId,
                    progression = progressionService != null
                        ? progressionService.GetProgression(characterId)
                        : TacticsCharacterProgressionSnapshot.CreateDefault(characterId)
                });
            }
        }

        return result;
    }

    private async Task<bool> TryConfigureHostTransportAsync()
    {
        if (transport == null)
        {
            EmitStatus("Network transport is unavailable.");
            return false;
        }

        try
        {
            await TacticsUnityServicesBootstrap.EnsureInitializedAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                EmitStatus("Sign in with a player account before hosting online co-op.");
                return false;
            }

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Max(1, ExpectedPlayerCount - 1));
            activeRelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EmitStatus($"Relay host setup failed: {exception.Message}");
            return false;
        }
    }

    private async Task<bool> TryConfigureClientTransportAsync(string joinCode)
    {
        if (transport == null)
        {
            EmitStatus("Network transport is unavailable.");
            return false;
        }

        string normalizedJoinCode = ResolveJoinCode(joinCode);
        if (string.IsNullOrWhiteSpace(normalizedJoinCode))
        {
            EmitStatus("Enter a Relay join code.");
            return false;
        }

        try
        {
            await TacticsUnityServicesBootstrap.EnsureInitializedAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                EmitStatus("Sign in with a player account before joining online co-op.");
                return false;
            }

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(normalizedJoinCode);
            activeRelayJoinCode = normalizedJoinCode;
            transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EmitStatus($"Relay join failed: {exception.Message}");
            return false;
        }
    }

    private static string ResolveJoinCode(string joinCode)
    {
        return string.IsNullOrWhiteSpace(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();
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
        networkManager.CustomMessagingManager.SendNamedMessage(messageName, recipients, writer, NamedMessageDelivery);
    }

    private void SendMessageToServer(string messageName, string payload)
    {
        if (networkManager?.CustomMessagingManager == null)
        {
            return;
        }

        using FastBufferWriter writer = new(FastBufferWriter.GetWriteSize(payload), Allocator.Temp);
        writer.WriteValueSafe(payload);
        networkManager.CustomMessagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer, NamedMessageDelivery);
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

    private int RollChestReward()
    {
        ProceduralIsometricMapGenerator generator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        return generator != null ? generator.RollChestGoldReward() : 0;
    }

    private static string BuildMimicRuntimeCharacterId(string runtimeChestId)
    {
        string normalizedChestId = string.IsNullOrWhiteSpace(runtimeChestId) ? "chest" : runtimeChestId.Trim();
        return $"enemy_mimic_{normalizedChestId}";
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
            ReplicatedCommandType.CommitProgression => ExecuteCommitProgressionCommand(
                JsonUtility.FromJson<CommitProgressionCommandMessage>(command.Payload)),
            ReplicatedCommandType.OpenChest => ExecuteOpenChestCommand(
                JsonUtility.FromJson<OpenChestCommandMessage>(command.Payload),
                requireLocalAuthorityState: false),
            _ => false
        };
    }

    private void SendLocalPartySelectionToServer()
    {
        EmitStatus("Connected. Sending team selection to host...");
        SendMessageToServer(PartySelectionMessageName, JsonUtility.ToJson(new PartySelectionMessage
        {
            characters = GetLocalPartyLoadout()
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
        public List<TacticsCoopCharacterLoadout> characters = new();
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
    private struct CommitProgressionCommandMessage
    {
        public string runtimeCharacterId;
        public TacticsCharacterProgressionSnapshot snapshot;
    }

    [Serializable]
    private struct OpenChestCommandMessage
    {
        public string runtimeCharacterId;
        public string runtimeChestId;
        public int goldReward;
        public bool containsMimic;
        public string mimicRuntimeCharacterId;
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
        EndTurn = 2,
        CommitProgression = 3,
        OpenChest = 4
    }
}
