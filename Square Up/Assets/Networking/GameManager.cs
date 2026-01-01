using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int winsToWinMatch = 7;
    [SerializeField] private float roundStartDelay = 3f;
    [SerializeField] private float roundEndDelay = 4f;
    [SerializeField] private float matchEndDelay = 6.7f;

    [Header("UI Track Setup")]
    [SerializeField] private GameObject roundEndUIPanel;
    [SerializeField] private TextMeshProUGUI winStatusText;
    [SerializeField] private Image[] p1WinSprites;
    [SerializeField] private Image[] p2WinSprites;
    [SerializeField] private Image winnerCenterSprite;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnHeight = 10f;

    [Header("Map Settings")]
    [SerializeField] private NetworkPrefabRef[] availableMaps;

    [Header("Weapons")]
    [SerializeField] private WeaponData[] availableWeapons;
    [SerializeField] private WeaponPickup[] weaponPrefabs;

    [Header("Game State")]
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool ShowRoundEndUI { get; set; }
    [Networked] private GameState CurrentState { get; set; }
    [Networked] private TickTimer RoundTimer { get; set; }
    [Networked] private int LastRoundWinnerIndex { get; set; }
    [Networked] private NetworkObject CurrentMap { get; set; }

    [Networked, Capacity(2)]
    private NetworkArray<PlayerRef> PlayerRefs => default;

    [Networked, Capacity(2)]
    private NetworkArray<int> PlayerWins => default;

    private enum GameState { WaitingForPlayers, RoundStarting, RoundActive, RoundEnding, MatchOver }

    private GameObject[] spawnPoints;
    private Transform worldOrigin;

    public override void Spawned()
    {
        // Find the world origin map spawn point
        GameObject worldOriginObj = GameObject.Find("worldorigin");
        if (worldOriginObj != null)
        {
            worldOrigin = worldOriginObj.transform;
        }
        else
        {
            Debug.LogWarning("No 'worldorigin' GameObject found! Maps will spawn at (0,0,0)");
        }

        // Cache all spawn points by tag
        spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoints");

        if (Object.HasStateAuthority)
        {
            CurrentState = GameState.WaitingForPlayers;
            ShowRoundEndUI = false;
            LastRoundWinnerIndex = -1;
            for (int i = 0; i < PlayerWins.Length; i++) PlayerWins.Set(i, 0);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            UpdatePlayerList();

            if (PlayerRefs[0] != PlayerRef.None && PlayerRefs[1] != PlayerRef.None)
                EnsureUniqueColors();

            switch (CurrentState)
            {
                case GameState.RoundStarting:
                    if (RoundTimer.ExpiredOrNotRunning(Runner)) StartRound();
                    break;
                case GameState.RoundActive:
                    CheckForRoundEnd();
                    break;
                case GameState.RoundEnding:
                    if (RoundTimer.ExpiredOrNotRunning(Runner)) PrepareNextRound();
                    break;
                case GameState.MatchOver:
                    if (RoundTimer.ExpiredOrNotRunning(Runner)) ReturnToMenu();
                    break;
            }
        }
    }

    public override void Render()
    {
        if (roundEndUIPanel != null) roundEndUIPanel.SetActive(ShowRoundEndUI);

        if (ShowRoundEndUI && PlayerRefs[0] != PlayerRef.None && PlayerRefs[1] != PlayerRef.None)
        {
            UpdateWinTrackUI();
            UpdateWinStatusText();
        }
    }

    private void UpdatePlayerList()
    {
        var activePlayers = Runner.ActivePlayers.OrderBy(p => p.RawEncoded).ToList();
        for (int i = 0; i < 2; i++)
        {
            if (i < activePlayers.Count) PlayerRefs.Set(i, activePlayers[i]);
            else PlayerRefs.Set(i, PlayerRef.None);
        }
    }

    private PlayerData GetPlayerData(int index)
    {
        if (index < 0 || index >= PlayerRefs.Length) return null;
        PlayerRef pRef = PlayerRefs[index];
        if (pRef == PlayerRef.None) return null;

        NetworkObject pObj = Runner.GetPlayerObject(pRef);
        return pObj != null ? pObj.GetComponent<PlayerData>() : null;
    }

    private void UpdateWinTrackUI()
    {
        PlayerData p1 = GetPlayerData(0);
        PlayerData p2 = GetPlayerData(1);
        if (p1 == null || p2 == null) return;

        Color p1Col = p1.GetActualColor();
        Color p2Col = p2.GetActualColor();
        Color faded = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        for (int i = 0; i < p1WinSprites.Length; i++) p1WinSprites[i].color = (PlayerWins[0] > i) ? p1Col : faded;
        for (int i = 0; i < p2WinSprites.Length; i++) p2WinSprites[i].color = (PlayerWins[1] > i) ? p2Col : faded;

        if (PlayerWins[0] >= winsToWinMatch) winnerCenterSprite.color = p1Col;
        else if (PlayerWins[1] >= winsToWinMatch) winnerCenterSprite.color = p2Col;
        else winnerCenterSprite.color = Color.white;
    }

    private void UpdateWinStatusText()
    {
        if (winStatusText == null) return;

        if (LastRoundWinnerIndex == -1)
        {
            winStatusText.text = "DRAW!";
            winStatusText.color = Color.white;
        }
        else
        {
            PlayerData winner = GetPlayerData(LastRoundWinnerIndex);
            winStatusText.text = $"PLAYER {LastRoundWinnerIndex + 1} WINS ROUND!";
            if (winner != null) winStatusText.color = winner.GetActualColor();
        }

        if (CurrentState == GameState.MatchOver && LastRoundWinnerIndex != -1)
        {
            winStatusText.text = $"PLAYER {LastRoundWinnerIndex + 1} WINS THE MATCH!";
        }
    }

    private void EnsureUniqueColors()
    {
        PlayerData p1 = GetPlayerData(0);
        PlayerData p2 = GetPlayerData(1);
        if (p1 != null && p2 != null && p1.PlayerColorIndex == p2.PlayerColorIndex)
        {
            p2.PlayerColorIndex = (p2.PlayerColorIndex + 1) % 10;
        }
    }

    public void StartGame()
    {
        if (!Object.HasStateAuthority || GameStarted) return;
        if (Runner.ActivePlayers.Count() < 2) return;

        GameStarted = true;
        for (int i = 0; i < PlayerWins.Length; i++) PlayerWins.Set(i, 0);

        // Teleport players to spawn points when game starts
        PositionPlayersAtSpawns();

        PrepareNextRound();
    }

    private void PrepareNextRound()
    {
        if (CurrentState == GameState.MatchOver) return;

        ShowRoundEndUI = false;
        LastRoundWinnerIndex = -1;
        CurrentState = GameState.RoundStarting;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundStartDelay);

        // Despawn old map and spawn new random map
        SpawnRandomMap();

        PositionPlayersAtSpawns();
        for (int i = 0; i < 2; i++)
        {
            PlayerData data = GetPlayerData(i);
            if (data != null) data.Respawn();
        }
    }

    private void SpawnRandomMap()
    {
        if (!Object.HasStateAuthority) return;

        // Despawn current map if it exists
        if (CurrentMap != null)
        {
            Runner.Despawn(CurrentMap);
            CurrentMap = null;
        }

        // Check if we have maps to spawn
        if (availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogWarning("No maps available to spawn!");
            return;
        }

        // Select random map
        int randomIndex = Random.Range(0, availableMaps.Length);
        NetworkPrefabRef selectedMap = availableMaps[randomIndex];

        // Determine spawn position (world origin or 0,0,0)
        Vector3 spawnPosition = worldOrigin != null ? worldOrigin.position : Vector3.zero;

        // Spawn the map
        CurrentMap = Runner.Spawn(selectedMap, spawnPosition, Quaternion.identity);

        Debug.Log($"Spawned map {randomIndex} at {spawnPosition}");
    }

    private void StartRound() => CurrentState = GameState.RoundActive;

    private void CheckForRoundEnd()
    {
        int aliveCount = 0;
        int lastAliveIndex = -1;

        for (int i = 0; i < 2; i++)
        {
            PlayerData p = GetPlayerData(i);
            if (p != null && !p.Dead)
            {
                aliveCount++;
                lastAliveIndex = i;
            }
        }

        if (aliveCount <= 1) EndRound(lastAliveIndex);
    }

    private void EndRound(int winnerIndex)
    {
        CurrentState = GameState.RoundEnding;
        ShowRoundEndUI = true;
        LastRoundWinnerIndex = winnerIndex;

        if (winnerIndex != -1)
        {
            PlayerWins.Set(winnerIndex, PlayerWins[winnerIndex] + 1);
            if (PlayerWins[winnerIndex] >= winsToWinMatch)
            {
                CurrentState = GameState.MatchOver;
                RoundTimer = TickTimer.CreateFromSeconds(Runner, matchEndDelay);
            }
            else
            {
                RoundTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);
            }
        }
        else
        {
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);
        }
    }

    private void PositionPlayersAtSpawns()
    {
        if (spawnPoints == null || spawnPoints.Length < 2)
        {
            Debug.LogWarning("Not enough spawn points tagged 'SpawnPoints'!");
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            PlayerData p = GetPlayerData(i);
            if (p != null)
            {
                int spawnIndex = i % spawnPoints.Length;
                p.transform.position = spawnPoints[spawnIndex].transform.position + Vector3.up * spawnHeight;
            }
        }
    }

    private async void ReturnToMenu()
    {
        if (!Object.HasStateAuthority) return;

        // Tell all clients to return to menu
        RPC_ReturnToMenu();

        // Small delay to ensure RPC is sent
        await System.Threading.Tasks.Task.Delay(100);

        // Shutdown the runner
        await Runner.Shutdown();

        // Load scene index 0 (menu scene)
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReturnToMenu()
    {
        // This runs on all clients including host
        StartCoroutine(ReturnToMenuCoroutine());
    }

    private System.Collections.IEnumerator ReturnToMenuCoroutine()
    {
        // Small delay to let RPC finish
        yield return new WaitForSeconds(0.1f);

        // Shutdown runner (safe to call on both host and client)
        if (Runner != null)
        {
            Runner.Shutdown();
        }

        // Load menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // --- Weapon Helpers (FIXED) ---
    public void SpawnDroppedWeapon(WeaponData weaponData, Vector3 position)
    {
        if (!Object.HasStateAuthority) return;
        int idx = GetWeaponIndex(weaponData);
        if (idx == -1 || weaponPrefabs == null || idx >= weaponPrefabs.Length) return;

        WeaponPickup prefab = weaponPrefabs[idx];
        Vector3 spawnPos = position + Vector3.up * 0.5f;
        Vector2 throwVel = new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 6f));
        WeaponPickup dropped = Runner.Spawn(prefab, spawnPos, Quaternion.identity);
        dropped.Drop(spawnPos, throwVel);
    }

    public WeaponData GetWeaponData(int index)
    {
        if (index <= 0 || availableWeapons == null) return null;
        int arrayIdx = index - 1;

        if (arrayIdx >= 0 && arrayIdx < availableWeapons.Length)
            return availableWeapons[arrayIdx];

        return null;
    }

    public int GetWeaponIndex(WeaponData data)
    {
        if (availableWeapons == null || data == null) return -1;
        for (int i = 0; i < availableWeapons.Length; i++)
        {
            if (availableWeapons[i] == data) return i;
        }
        return -1;
    }
}