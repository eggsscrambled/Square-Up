using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

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
    [SerializeField] private float minWeaponFillPercent = 0.3f;
    [SerializeField] private float maxWeaponFillPercent = 0.7f;

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

    private Transform worldOrigin;

    public override void Spawned()
    {
        Instance = this;

        GameObject worldOriginObj = GameObject.Find("worldorigin");
        if (worldOriginObj != null)
        {
            worldOrigin = worldOriginObj.transform;
        }

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
                    // Continuously enforce player positions during the countdown.
                    // This solves the first-round race condition.
                    PositionPlayersAtSpawns();

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
        if (Runner.ActivePlayers.Count() < 2)
        {
            DestroyAllDontDestroyOnLoadObjects();
            SceneManager.LoadScene(0);
            return;
        }

        GameStarted = true;
        for (int i = 0; i < PlayerWins.Length; i++) PlayerWins.Set(i, 0);

        PrepareNextRound();
    }

    private void DestroyAllDontDestroyOnLoadObjects()
    {
        GameObject temp = null;
        try
        {
            temp = new GameObject("Temp");
            DontDestroyOnLoad(temp);
            Scene dontDestroyOnLoadScene = temp.scene;
            DestroyImmediate(temp);
            temp = null;

            GameObject[] rootObjects = dontDestroyOnLoadScene.GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                Destroy(obj);
            }
        }
        finally
        {
            if (temp != null)
                DestroyImmediate(temp);
        }
    }

    private void PrepareNextRound()
    {
        if (CurrentState == GameState.MatchOver) return;

        ShowRoundEndUI = false;
        LastRoundWinnerIndex = -1;
        CurrentState = GameState.RoundStarting;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundStartDelay);

        // Clear all player weapons
        ClearAllPlayerWeapons();

        // Clear all dropped weapons from previous round
        ClearAllDroppedWeapons();

        SpawnRandomMap();

        // Trigger an initial respawn to reset health/dead flags
        for (int i = 0; i < 2; i++)
        {
            PlayerData data = GetPlayerData(i);
            if (data != null) data.Respawn();
        }

        // Spawn weapons on the map
        SpawnMapWeapons();
    }

    private void ClearAllPlayerWeapons()
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < 2; i++)
        {
            PlayerData data = GetPlayerData(i);
            if (data != null)
            {
                data.ClearWeapons();
            }
        }
    }

    private void ClearAllDroppedWeapons()
    {
        if (!Object.HasStateAuthority) return;

        // Find all WeaponPickup objects in the scene and despawn them
        WeaponPickup[] droppedWeapons = FindObjectsOfType<WeaponPickup>();
        foreach (WeaponPickup weapon in droppedWeapons)
        {
            if (weapon.Object != null)
            {
                Runner.Despawn(weapon.Object);
            }
        }
    }

    private void SpawnRandomMap()
    {
        if (!Object.HasStateAuthority) return;

        if (CurrentMap != null)
        {
            Runner.Despawn(CurrentMap);
            CurrentMap = null;
        }

        if (availableMaps == null || availableMaps.Length == 0) return;

        int randomIndex = Random.Range(0, availableMaps.Length);
        NetworkPrefabRef selectedMap = availableMaps[randomIndex];
        Vector3 spawnPosition = worldOrigin != null ? worldOrigin.position : Vector3.zero;

        CurrentMap = Runner.Spawn(selectedMap, spawnPosition, Quaternion.identity);
    }

    private void SpawnMapWeapons()
    {
        if (!Object.HasStateAuthority || CurrentMap == null) return;

        // Find all weapon spawn points in the map
        Transform[] allChildren = CurrentMap.GetComponentsInChildren<Transform>(true);
        List<Transform> weaponSpawns = new List<Transform>();

        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("WeaponSpawn"))
            {
                weaponSpawns.Add(child);
            }
        }

        if (weaponSpawns.Count == 0)
        {
            Debug.LogWarning("No weapon spawn points found in the map!");
            return;
        }

        // Get the weapon pool from the map
        MapWeaponPoolDataHolder poolHolder = CurrentMap.GetComponent<MapWeaponPoolDataHolder>();
        if (poolHolder == null || poolHolder.mapWeaponPools == null || poolHolder.mapWeaponPools.Length == 0)
        {
            Debug.LogWarning("No weapon pools found on the map!");
            return;
        }

        // Pick a random weapon pool
        MapWeaponPool selectedPool = poolHolder.mapWeaponPools[Random.Range(0, poolHolder.mapWeaponPools.Length)];
        if (selectedPool == null || selectedPool.weapons == null || selectedPool.weapons.Length == 0)
        {
            Debug.LogWarning("Selected weapon pool is empty!");
            return;
        }

        // Calculate how many weapons to spawn (random fill percentage, minimum 2)
        float fillPercent = Random.Range(minWeaponFillPercent, maxWeaponFillPercent);
        int weaponsToSpawn = Mathf.Max(2, Mathf.RoundToInt(weaponSpawns.Count * fillPercent));

        // Shuffle the spawn points and select the first N
        List<Transform> shuffledSpawns = weaponSpawns.OrderBy(x => Random.value).ToList();

        // Spawn weapons at selected points
        for (int i = 0; i < weaponsToSpawn && i < shuffledSpawns.Count; i++)
        {
            // Pick a random weapon from the pool
            NetworkPrefabRef randomWeapon = selectedPool.weapons[Random.Range(0, selectedPool.weapons.Length)];

            // Spawn the weapon at this point
            Vector3 spawnPos = shuffledSpawns[i].position;
            Runner.Spawn(randomWeapon, spawnPos, Quaternion.identity);
        }

        Debug.Log($"Spawned {weaponsToSpawn} weapons from pool '{selectedPool.poolName}' at {weaponSpawns.Count} available spawn points");
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
        if (CurrentMap == null) return;

        // Search inside the spawned map for objects tagged "SpawnPoints"
        Transform[] allChildren = CurrentMap.GetComponentsInChildren<Transform>(true);
        List<Transform> spawnPoints = new List<Transform>();

        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("SpawnPoints"))
            {
                spawnPoints.Add(child);
            }
        }

        if (spawnPoints.Count < 2) return;

        for (int i = 0; i < 2; i++)
        {
            PlayerData p = GetPlayerData(i);
            if (p != null)
            {
                int spawnIndex = i % spawnPoints.Count;
                Vector3 targetPos = spawnPoints[spawnIndex].position + Vector3.up * spawnHeight;

                // Teleport the transform
                p.transform.position = targetPos;

                // If the player is dead, respawn them at this location
                if (p.Dead) p.Respawn();
            }
        }
    }

    private async void ReturnToMenu()
    {
        if (!Object.HasStateAuthority) return;

        RPC_ReturnToMenu();
        await System.Threading.Tasks.Task.Delay(100);
        await Runner.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReturnToMenu()
    {
        StartCoroutine(ReturnToMenuCoroutine());
    }

    private System.Collections.IEnumerator ReturnToMenuCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
        if (Runner != null) Runner.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

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
        if (arrayIdx >= 0 && arrayIdx < availableWeapons.Length) return availableWeapons[arrayIdx];
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