using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private int gameSceneBuildIndex = 1;

    private NetworkRunner runner;
    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private GameObject[] spawnPoints;

    public InputField lobbyIDField;

    // Accumulators ensure we don't miss a fast keypress between ticks
    private bool _pickupAccumulator;
    private bool _dashAccumulator;
    private bool _fireAccumulator;
    private bool _reloadAccumulator;

    void Start()
    {
        // We no longer Instantiate the runner here. 
        // We do it on demand in StartHost/StartClient.
        DontDestroyOnLoad(gameObject);
        RefreshSpawnPoints();
    }

    void Update()
    {
        if (runner == null || !runner.IsRunning) return;

        // Collect raw Unity input
        if (Input.GetButton("Fire1")) _fireAccumulator = true;
        if (Input.GetKeyDown(KeyCode.E)) _pickupAccumulator = true;
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space)) _dashAccumulator = true;
        if (Input.GetKeyDown(KeyCode.R)) _reloadAccumulator = true;
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
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            RefreshSpawnPoints();
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = playerIndex % spawnPoints.Length;
            return spawnPoints[spawnIndex].transform.position;
        }

        Debug.LogWarning("Using fallback spawn position - no spawn points available!");
        return new Vector3(UnityEngine.Random.Range(-5f, 5f), 2f, 0f);
    }

    public async void StartHost()
    {
        await StartGame(GameMode.Host);
    }

    public async void StartClient()
    {
        await StartGame(GameMode.Client);
    }

    private async Task StartGame(GameMode mode)
    {
        // 1. If there is an old runner sitting around, destroy it
        if (runner != null)
        {
            Destroy(runner.gameObject);
        }

        // 2. Create a fresh runner for this specific attempt
        runner = Instantiate(runnerPrefab);
        runner.AddCallbacks(this);
        DontDestroyOnLoad(runner.gameObject);

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = lobbyIDField.text,
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            if (mode == GameMode.Host)
            {
                runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex));
            }
            Debug.Log($"Successfully started {mode}");
        }
        else
        {
            // 3. If it failed (e.g., room already exists), cleanup so the button works again
            Debug.LogError($"Failed to {mode}: {result.ShutdownReason}");

            // Cleanup the failed runner
            if (runner != null)
            {
                Destroy(runner.gameObject);
                runner = null;
            }
        }
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
            {
                inputData.aimDirection = ((Vector2)mousePos - (Vector2)localPlayer.transform.position).normalized;
            }
        }

        inputData.buttons.Set(MyButtons.Fire, _fireAccumulator);
        inputData.buttons.Set(MyButtons.Pickup, _pickupAccumulator);
        inputData.buttons.Set(MyButtons.Dash, _dashAccumulator);
        inputData.buttons.Set(MyButtons.Reload, _reloadAccumulator);
        inputData.buttons.Set(MyButtons.Heal, Input.GetKey(KeyCode.Q));

        inputData.inputTick = runner.Tick;

        input.Set(inputData);

        _fireAccumulator = false;
        _pickupAccumulator = false;
        _dashAccumulator = false;
        _reloadAccumulator = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            int playerIndex = spawnedPlayers.Count;
            Vector3 spawnPos = GetSpawnPosition(playerIndex);

            NetworkObject networkPlayer = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            runner.SetPlayerObject(player, networkPlayer);
            spawnedPlayers.Add(player, networkPlayer);
        }

        if (player == runner.LocalPlayer && PredictedBulletManager.Instance != null)
        {
            PredictedBulletManager.Instance.SetLocalRunner(runner);
            Debug.Log($"<color=cyan>PredictedBulletManager initialized for local player (IsHost: {runner.IsServer})</color>");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out var obj))
        {
            runner.Despawn(obj);
            spawnedPlayers.Remove(player);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.LogWarning($"Runner Shutdown: {shutdownReason}");
        // Ensure our reference is cleared if the runner dies
        if (this.runner == runner)
        {
            this.runner = null;
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        RefreshSpawnPoints();
    }

    // Boilerplate implementations
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
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}