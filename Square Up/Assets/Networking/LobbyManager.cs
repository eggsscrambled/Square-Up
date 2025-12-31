using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private int gameSceneBuildIndex = 1;

    private NetworkRunner runner;
    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // Accumulators ensure we don't miss a fast keypress between ticks
    private bool _pickupAccumulator;
    private bool _dashAccumulator;
    private bool _fireAccumulator;

    async void Start()
    {
        runner = Instantiate(runnerPrefab);
        runner.AddCallbacks(this);
        DontDestroyOnLoad(runner.gameObject);
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (runner == null || !runner.IsRunning) return;

        // Collect raw Unity input
        if (Input.GetButton("Fire1")) _fireAccumulator = true;
        if (Input.GetKeyDown(KeyCode.E)) _pickupAccumulator = true;
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space)) _dashAccumulator = true;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var inputData = new NetworkInputData();

        inputData.movementInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (Camera.main != null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            inputData.mouseWorldPosition = mousePos;

            PlayerData localPlayer = FindObjectsByType<PlayerData>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);

            if (localPlayer != null)
                inputData.aimDirection = ((Vector2)mousePos - (Vector2)localPlayer.transform.position).normalized;
        }

        // Map accumulators to NetworkButtons
        inputData.buttons.Set(MyButtons.Fire, _fireAccumulator);
        inputData.buttons.Set(MyButtons.Pickup, _pickupAccumulator);
        inputData.buttons.Set(MyButtons.Dash, _dashAccumulator);

        input.Set(inputData);

        // Reset accumulators after passing them to the simulation
        _fireAccumulator = false;
        _pickupAccumulator = false;
        _dashAccumulator = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-5f, 5f), 2f, 0f);
            NetworkObject networkPlayer = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            runner.SetPlayerObject(player, networkPlayer);
            spawnedPlayers.Add(player, networkPlayer);
        }
    }

    public async void StartHost()
    {
        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = "TestRoom",
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
        if (result.Ok) runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex));
    }

    public async void StartClient() => await runner.StartGame(new StartGameArgs()
    {
        GameMode = GameMode.Client,
        SessionName = "TestRoom",
        SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
    });

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { if (spawnedPlayers.TryGetValue(player, out var obj)) { runner.Despawn(obj); spawnedPlayers.Remove(player); } }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}