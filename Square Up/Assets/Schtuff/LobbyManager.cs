using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private int gameSceneBuildIndex = 1; // Set this to your game scene's build index

    private NetworkRunner runner;
    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkInputData inputData;

    async void Start()
    {
        // Initialize Fusion runner
        runner = Instantiate(runnerPrefab);
        runner.AddCallbacks(this);

        // Keep the runner alive across scenes
        DontDestroyOnLoad(runner.gameObject);
        DontDestroyOnLoad(gameObject); // Keep the LobbyManager alive too
    }

    void Update()
    {
        if (runner != null && runner.IsRunning)
        {
            inputData.movementInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            inputData.jump = Input.GetButton("Jump");

            // Mouse aim - calculate direction from player to mouse
            if (Camera.main != null)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0; // Make sure z is 0 for 2D

                // Get local player position using input authority
                PlayerData localPlayer = FindObjectsByType<PlayerData>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);

                if (localPlayer != null)
                {
                    Vector2 direction = (mousePos - localPlayer.transform.position);
                    inputData.aimDirection = direction.normalized;
                }
                else
                {
                    // Fallback if no player found yet
                    inputData.aimDirection = Vector2.right;
                }
            }

            inputData.fire = Input.GetButton("Fire1");
            inputData.pickup = Input.GetKeyDown(KeyCode.E);
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

        if (result.Ok)
        {
            Debug.Log("Host started successfully");
            // Load the game scene
            runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex));
        }
        else
        {
            Debug.LogError($"Failed to start: {result.ShutdownReason}");
        }
    }

    public async void StartClient()
    {
        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = "TestRoom",
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("Client connected successfully");
        }
        else
        {
            Debug.LogError($"Failed to connect: {result.ShutdownReason}");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined");

        // Host spawns players
        if (runner.IsServer)
        {
            Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-5f, 5f), 2f, 0f);
            NetworkObject networkPlayer = runner.Spawn(
                playerPrefab,
                spawnPos,
                Quaternion.identity,
                player
            );

            spawnedPlayers.Add(player, networkPlayer);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} left");

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
        {
            runner.Despawn(playerObj);
            spawnedPlayers.Remove(player);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Shutdown: {shutdownReason}");
    }

    // Required callbacks (empty implementations)
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        input.Set(inputData);
    }
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