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
    private const int DefaultOfflinePartyIndex = 0;
    private const int MinLobbyPlayersToStart = 2;
    private const int MaxLobbyPlayers = 4;
    private const NetworkDelivery NamedMessageDelivery = NetworkDelivery.ReliableFragmentedSequenced;
    private const string RelayConnectionType = "dtls";

    private const string PartySelectionMessageName = "tactics.party.selection";
    private const string LobbyStateMessageName = "tactics.lobby.state";
    private const string LobbyReadyStateRequestMessageName = "tactics.lobby.ready.request";
    private const string LobbyStartRequestMessageName = "tactics.lobby.start.request";
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
    private readonly Dictionary<ulong, bool> readyStatesByClientId = new();
    private readonly Dictionary<ulong, string> usernamesByClientId = new();
    private readonly Queue<ReplicatedCommandEnvelope> pendingReplicatedCommands = new();

    private NetworkManager networkManager;
    private UnityTransport transport;
    private TacticsCharacterRegistry characterRegistry;
    private TacticsCombatSystem combatSystem;
    private TacticsPartySelectionService partySelectionService;
    private TacticsCharacterProgressionService progressionService;
    private TacticsPlayerCurrencyService currencyService;
    private ITacticsAccountSessionService accountSessionService;
    private bool handlersRegistered;
    private bool isMatchStarting;
    private bool pendingClientPartySubmission;
    private string activeRelayJoinCode = string.Empty;
    private TacticsMatchGenerationSettings pendingHostMatchSettings;
    private TacticsCoopLobbyState currentLobbyState;

    public event Action<string> StatusChanged;
    public event Action<TacticsCoopLobbyState> LobbyStateChanged;
    public event Action<TacticsCoopBattleSetup> BattleSetupReady;
    public event Action SessionEnded;

    public bool IsOnlineSession => networkManager != null && networkManager.IsListening;
    public bool IsHostAuthority => networkManager != null && (networkManager.IsHost || networkManager.IsServer);
    public bool CanRunAutomatedTurns => !IsOnlineSession || IsHostAuthority;
    public string ActiveRelayJoinCode => activeRelayJoinCode;
    public ulong LocalClientId => networkManager != null ? networkManager.LocalClientId : ulong.MaxValue;
    public bool IsLobbyActive => currentLobbyState != null;
    public TacticsCoopLobbyState CurrentLobbyState => currentLobbyState?.Clone();
    public int LocalPartyIndex
    {
        get
        {
            if (!IsOnlineSession)
            {
                return DefaultOfflinePartyIndex;
            }

            return GetPartyIndexForClientId(networkManager.LocalClientId);
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

    public void AssignAccountSessionService(ITacticsAccountSessionService service)
    {
        accountSessionService = service;
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
        CacheLocalLobbyPlayerState();
        RebuildLobbyStateAndNotify();
        EmitStatus($"Relay host ready. Share join code {activeRelayJoinCode}. Waiting for allies to join the lobby...");
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
        ResolveRuntimeReferences();

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

    public void UpdateLobbyMatchSettings(TacticsMatchGenerationSettings matchSettings)
    {
        if (!IsHostAuthority || matchSettings == null)
        {
            return;
        }

        pendingHostMatchSettings = matchSettings.Clone();
        pendingHostMatchSettings.Sanitize();
        RebuildLobbyStateAndNotify(broadcastToClients: true);
    }

    public bool SetLocalReadyState(bool isReady)
    {
        if (!IsOnlineSession)
        {
            return false;
        }

        if (IsHostAuthority)
        {
            readyStatesByClientId[networkManager.LocalClientId] = isReady;
            RebuildLobbyStateAndNotify(broadcastToClients: true);
            return true;
        }

        SendMessageToServer(LobbyReadyStateRequestMessageName, JsonUtility.ToJson(new LobbyReadyStateRequestMessage
        {
            isReady = isReady
        }));
        return true;
    }

    public bool RequestStartMatch()
    {
        if (!IsOnlineSession)
        {
            return false;
        }

        if (IsHostAuthority)
        {
            return TryStartBattleIfReady();
        }

        SendMessageToServer(LobbyStartRequestMessageName, "{}");
        return true;
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
        return RequestUseAbility(character, ability, targetTile, null);
    }

    public bool RequestUseAbility(
        TacticsCharacterController character,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        Vector2Int? throwDestination)
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
            hasThrowDestination = throwDestination.HasValue,
            throwTargetX = throwDestination.HasValue ? throwDestination.Value.x : 0,
            throwTargetY = throwDestination.HasValue ? throwDestination.Value.y : 0,
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
        readyStatesByClientId.Clear();
        usernamesByClientId.Clear();
        pendingReplicatedCommands.Clear();
        isMatchStarting = false;
        pendingClientPartySubmission = false;
        activeRelayJoinCode = string.Empty;
        pendingHostMatchSettings = null;
        currentLobbyState = null;
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
        messaging.RegisterNamedMessageHandler(LobbyStateMessageName, HandleLobbyStateMessage);
        messaging.RegisterNamedMessageHandler(LobbyReadyStateRequestMessageName, HandleLobbyReadyStateRequestMessage);
        messaging.RegisterNamedMessageHandler(LobbyStartRequestMessageName, HandleLobbyStartRequestMessage);
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
            messaging.UnregisterNamedMessageHandler(LobbyStateMessageName);
            messaging.UnregisterNamedMessageHandler(LobbyReadyStateRequestMessageName);
            messaging.UnregisterNamedMessageHandler(LobbyStartRequestMessageName);
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
            readyStatesByClientId[clientId] = false;
            usernamesByClientId[clientId] = BuildFallbackUsername(clientId);
            partySelectionsByClientId[clientId] = new List<TacticsCoopCharacterLoadout>();
            RebuildLobbyStateAndNotify(broadcastToClients: true);
            EmitStatus("A player joined the lobby. Waiting for their party selection.");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        partySelectionsByClientId.Remove(clientId);
        readyStatesByClientId.Remove(clientId);
        usernamesByClientId.Remove(clientId);
        if (!IsOnlineSession)
        {
            return;
        }

        if (IsHostAuthority)
        {
            RebuildLobbyStateAndNotify(broadcastToClients: true);
        }

        EmitStatus(clientId == networkManager.LocalClientId
            ? "Disconnected from co-op session."
            : "A player disconnected from the co-op lobby.");
    }

    private void HandlePartySelectionMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        PartySelectionMessage message = JsonUtility.FromJson<PartySelectionMessage>(payload);
        partySelectionsByClientId[senderClientId] = SanitizePartyLoadout(message?.characters);
        readyStatesByClientId[senderClientId] = false;
        usernamesByClientId[senderClientId] = SanitizeUsername(message?.username, senderClientId);
        RebuildLobbyStateAndNotify(broadcastToClients: true);
        EmitStatus($"{usernamesByClientId[senderClientId]} joined the lobby and synced their party.");
    }

    private void HandleLobbyStateMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        TacticsCoopLobbyState lobbyState = JsonUtility.FromJson<TacticsCoopLobbyState>(payload);
        currentLobbyState = lobbyState?.Clone();
        LobbyStateChanged?.Invoke(CurrentLobbyState);
    }

    private void HandleLobbyReadyStateRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority)
        {
            return;
        }

        string payload = ReadPayload(ref reader);
        LobbyReadyStateRequestMessage message = JsonUtility.FromJson<LobbyReadyStateRequestMessage>(payload);
        bool requestedReady = message != null && message.isReady;
        if (requestedReady &&
            (!partySelectionsByClientId.TryGetValue(senderClientId, out List<TacticsCoopCharacterLoadout> partyMembers) ||
             !HasRequiredPartyMembers(partyMembers)))
        {
            readyStatesByClientId[senderClientId] = false;
            EmitStatus($"{ResolveUsername(senderClientId)} must select a full party before readying up.");
            RebuildLobbyStateAndNotify(broadcastToClients: true);
            return;
        }

        readyStatesByClientId[senderClientId] = requestedReady;
        RebuildLobbyStateAndNotify(broadcastToClients: true);
    }

    private void HandleLobbyStartRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHostAuthority || senderClientId != networkManager?.LocalClientId)
        {
            return;
        }

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
        if (character == null || ability == null || combatSystem == null)
        {
            return false;
        }

        if (applyProvidedRandomState && TryDeserializeRandomState(message.randomStateJson, out UnityEngine.Random.State state))
        {
            UnityEngine.Random.state = state;
        }

        Vector2Int targetTile = new Vector2Int(message.targetX, message.targetY);
        Vector2Int? throwDestination = message.hasThrowDestination
            ? new Vector2Int(message.throwTargetX, message.throwTargetY)
            : null;
        return requireLocalAuthorityState
            ? combatSystem.TryUseAbility(character, ability, targetTile, throwDestination)
            : combatSystem.ApplyReplicatedAbility(character, ability, targetTile, throwDestination);
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

    private bool TryStartBattleIfReady()
    {
        if (!IsHostAuthority || isMatchStarting || networkManager == null)
        {
            return false;
        }

        if (networkManager.ConnectedClientsIds.Count < MinLobbyPlayersToStart)
        {
            EmitStatus("At least two players must be in the lobby before the match can start.");
            return false;
        }

        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            if (!partySelectionsByClientId.TryGetValue(clientId, out List<TacticsCoopCharacterLoadout> partyMembers) ||
                !HasRequiredPartyMembers(partyMembers))
            {
                EmitStatus($"{ResolveUsername(clientId)} must sync a full party before the match can start.");
                return false;
            }

            if (!readyStatesByClientId.TryGetValue(clientId, out bool isReady) || !isReady)
            {
                EmitStatus($"{ResolveUsername(clientId)} is not ready yet.");
                return false;
            }
        }

        TacticsCoopBattleSetup battleSetup = new TacticsCoopBattleSetup
        {
            matchSettings = pendingHostMatchSettings?.Clone()
        };

        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            battleSetup.players.Add(new TacticsCoopBattlePlayer
            {
                clientId = clientId,
                username = ResolveUsername(clientId),
                isHost = clientId == networkManager.LocalClientId,
                partyMembers = CloneLoadoutList(partySelectionsByClientId[clientId])
            });
        }

        isMatchStarting = true;
        RebuildLobbyStateAndNotify(broadcastToClients: true);
        EmitStatus("All players are ready. Starting co-op battle...");
        string payload = JsonUtility.ToJson(battleSetup);
        BroadcastMessage(BattleStartMessageName, payload, includeHost: false);
        BattleSetupReady?.Invoke(battleSetup.Clone());
        return true;
    }

    private List<TacticsCoopCharacterLoadout> GetLocalPartyLoadout()
    {
        TacticsPartySelection selection = partySelectionService?.LoadSelection();
        if (selection == null)
        {
            return new List<TacticsCoopCharacterLoadout>();
        }

        List<TacticsCoopCharacterLoadout> result = new(selection.Capacity);
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

        return SanitizePartyLoadout(result, selection.Capacity);
    }

    private void CacheLocalLobbyPlayerState()
    {
        if (networkManager == null)
        {
            return;
        }

        ulong clientId = networkManager.LocalClientId;
        partySelectionsByClientId[clientId] = GetLocalPartyLoadout();
        readyStatesByClientId[clientId] = false;
        usernamesByClientId[clientId] = ResolveLocalUsername();
    }

    private List<TacticsCoopCharacterLoadout> SanitizePartyLoadout(
        IReadOnlyList<TacticsCoopCharacterLoadout> loadout,
        int capacity = TacticsPartySelection.DefaultCapacity)
    {
        return TacticsPartyCompositionRules.SanitizeLoadout(loadout, partySelectionService?.LoadRoster(), capacity);
    }

    private bool HasRequiredPartyMembers(IReadOnlyList<TacticsCoopCharacterLoadout> loadout)
    {
        int capacity = partySelectionService?.LoadSelection()?.Capacity ?? TacticsPartySelection.DefaultCapacity;
        return TacticsPartyCompositionRules.HasRequiredMembers(loadout, partySelectionService?.LoadRoster(), capacity);
    }

    private void RebuildLobbyStateAndNotify(bool broadcastToClients = false)
    {
        if (networkManager == null)
        {
            currentLobbyState = null;
            LobbyStateChanged?.Invoke(null);
            return;
        }

        TacticsCoopLobbyState state = BuildLobbyState();
        currentLobbyState = state;
        LobbyStateChanged?.Invoke(CurrentLobbyState);

        if (broadcastToClients && IsHostAuthority)
        {
            BroadcastMessage(LobbyStateMessageName, JsonUtility.ToJson(state), includeHost: false);
        }
    }

    private TacticsCoopLobbyState BuildLobbyState()
    {
        TacticsCoopLobbyState state = new TacticsCoopLobbyState
        {
            hostClientId = networkManager != null ? networkManager.LocalClientId : 0,
            maxPlayers = MaxLobbyPlayers,
            minPlayersToStart = MinLobbyPlayersToStart,
            isMatchStarting = isMatchStarting,
            relayJoinCode = activeRelayJoinCode,
            matchSettings = pendingHostMatchSettings?.Clone()
        };

        if (networkManager == null)
        {
            return state;
        }

        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = networkManager.ConnectedClientsIds[i];
            state.players.Add(new TacticsCoopLobbyPlayerState
            {
                clientId = clientId,
                username = ResolveUsername(clientId),
                isHost = clientId == networkManager.LocalClientId,
                isReady = readyStatesByClientId.TryGetValue(clientId, out bool isReady) && isReady,
                partyMembers = partySelectionsByClientId.TryGetValue(clientId, out List<TacticsCoopCharacterLoadout> loadout)
                    ? CloneLoadoutList(loadout)
                    : new List<TacticsCoopCharacterLoadout>()
            });
        }

        return state;
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

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Max(1, MaxLobbyPlayers - 1));
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
               partyIndex == GetPartyIndexForClientId(networkManager.LocalClientId);
    }

    public bool ShouldShowLocalOwnershipIndicator(TacticsCharacterController character)
    {
        return IsOnlineSession && CanLocalPlayerControlCharacter(character);
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

        return partyIndex == GetPartyIndexForClientId(clientId);
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

    private int GetPartyIndexForClientId(ulong clientId)
    {
        if (networkManager == null)
        {
            return DefaultOfflinePartyIndex;
        }

        for (int i = 0; i < networkManager.ConnectedClientsIds.Count; i++)
        {
            if (networkManager.ConnectedClientsIds[i] == clientId)
            {
                return i;
            }
        }

        return DefaultOfflinePartyIndex;
    }

    private string ResolveLocalUsername()
    {
        return SanitizeUsername(accountSessionService?.Username, networkManager != null ? networkManager.LocalClientId : 0);
    }

    private string ResolveUsername(ulong clientId)
    {
        return usernamesByClientId.TryGetValue(clientId, out string username)
            ? SanitizeUsername(username, clientId)
            : BuildFallbackUsername(clientId);
    }

    private static string SanitizeUsername(string username, ulong clientId)
    {
        return string.IsNullOrWhiteSpace(username) ? BuildFallbackUsername(clientId) : username.Trim();
    }

    private static string BuildFallbackUsername(ulong clientId)
    {
        return clientId == 0 ? "Host" : $"Player {clientId + 1}";
    }

    private static List<TacticsCoopCharacterLoadout> CloneLoadoutList(List<TacticsCoopCharacterLoadout> loadout)
    {
        List<TacticsCoopCharacterLoadout> clone = new();
        if (loadout == null)
        {
            return clone;
        }

        for (int i = 0; i < loadout.Count; i++)
        {
            TacticsCoopCharacterLoadout entry = loadout[i];
            if (entry != null)
            {
                clone.Add(entry.Clone());
            }
        }

        return clone;
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

    private void ResolveRuntimeReferences()
    {
        if (characterRegistry == null)
        {
            characterRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        }

        if (combatSystem == null)
        {
            combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        }
    }

    private TacticsCharacterController FindCharacterByRuntimeId(string runtimeCharacterId)
    {
        ResolveRuntimeReferences();
        return characterRegistry != null &&
               characterRegistry.TryGetCharacterByRuntimeId(runtimeCharacterId, out TacticsCharacterController character)
            ? character
            : null;
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
        CacheLocalLobbyPlayerState();
        EmitStatus("Connected. Syncing party and lobby state with the host...");
        SendMessageToServer(PartySelectionMessageName, JsonUtility.ToJson(new PartySelectionMessage
        {
            username = ResolveLocalUsername(),
            characters = GetLocalPartyLoadout()
        }));
    }

    private void EndSessionLocally()
    {
        pendingReplicatedCommands.Clear();
        partySelectionsByClientId.Clear();
        readyStatesByClientId.Clear();
        usernamesByClientId.Clear();
        isMatchStarting = false;
        pendingClientPartySubmission = false;
        currentLobbyState = null;
        UnregisterNetworkCallbacks();

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        EmitStatus("Returning to home screen...");
        LobbyStateChanged?.Invoke(null);
        SessionEnded?.Invoke();
    }

    [Serializable]
    private sealed class PartySelectionMessage
    {
        public string username;
        public List<TacticsCoopCharacterLoadout> characters = new();
    }

    [Serializable]
    private sealed class LobbyReadyStateRequestMessage
    {
        public bool isReady;
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
        public bool hasThrowDestination;
        public int throwTargetX;
        public int throwTargetY;
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
