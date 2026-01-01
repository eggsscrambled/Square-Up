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
    private GameObject[] spawnPoints;

    // Accumulators ensure we don't miss a fast keypress between ticks
    private bool _pickupAccumulator;
    private bool _dashAccumulator;
    private bool _fireAccumulator;
    private bool _reloadAccumulator; // Add this

    async void Start()
    {
        runner = Instantiate(runnerPrefab);
        runner.AddCallbacks(this);
        DontDestroyOnLoad(runner.gameObject);
        DontDestroyOnLoad(gameObject);

        // Cache spawn points
        RefreshSpawnPoints();
    }

    void Update()
    {
        if (runner == null || !runner.IsRunning) return;

        // Collect raw Unity input
        if (Input.GetButton("Fire1")) _fireAccumulator = true;
        if (Input.GetKeyDown(KeyCode.E)) _pickupAccumulator = true;
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space)) _dashAccumulator = true;
        if (Input.GetKeyDown(KeyCode.R)) _reloadAccumulator = true; // Add this
    }

    private void RefreshSpawnPoints()
    {
        spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoints");
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("No GameObjects with tag 'SpawnPoints' found!");
        }
    }

    private Vector3 GetSpawnPosition(int playerIndex)
    {
        // Refresh spawn points in case scene changed
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            RefreshSpawnPoints();
        }

        // If we have spawn points, use them
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = playerIndex % spawnPoints.Length;
            return spawnPoints[spawnIndex].transform.position;
        }

        // Fallback to random position if no spawn points
        Debug.LogWarning("Using fallback spawn position - no spawn points available!");
        return new Vector3(UnityEngine.Random.Range(-5f, 5f), 2f, 0f);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var inputData = new NetworkInputData();

        // 1. Movement Input
        inputData.movementInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // 2. Mouse and Aiming Logic
        if (Camera.main != null)
        {
            // Get mouse position in world space
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            inputData.mouseWorldPosition = mousePos;

            // Find the local player to calculate aim direction from the player's center to the mouse
            // We look for the PlayerData script that has Input Authority (the local client's player)
            PlayerData localPlayer = FindObjectsByType<PlayerData>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);

            if (localPlayer != null)
            {
                inputData.aimDirection = ((Vector2)mousePos - (Vector2)localPlayer.transform.position).normalized;
            }
        }

        // 3. Map Accumulators to NetworkButtons
        // Using Set() ensures the button bit is flipped to 'true' if the accumulator was tripped in Update()
        inputData.buttons.Set(MyButtons.Fire, _fireAccumulator);
        inputData.buttons.Set(MyButtons.Pickup, _pickupAccumulator);
        inputData.buttons.Set(MyButtons.Dash, _dashAccumulator);
        inputData.buttons.Set(MyButtons.Reload, _reloadAccumulator);

        // 4. Tick Synchronization
        // Storing the current tick helps with deterministic logic like seed-based spread
        inputData.inputTick = runner.Tick;

        // 5. Send to Fusion Simulation
        input.Set(inputData);

        // 6. Reset Accumulators
        // IMPORTANT: We clear these so the same button press doesn't "ghost" into the next input call
        _fireAccumulator = false;
        _pickupAccumulator = false;
        _dashAccumulator = false;
        _reloadAccumulator = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // Get spawn position based on player count
            int playerIndex = spawnedPlayers.Count;
            Vector3 spawnPos = GetSpawnPosition(playerIndex);

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
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // Refresh spawn points when scene loads
        RefreshSpawnPoints();
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}