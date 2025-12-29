using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float roundStartDelay = 3f;
    [SerializeField] private float roundEndDelay = 3f;

    [Header("Spawn Positions")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnHeight = 10f; // Height above spawn points to drop from

    [Header("Weapons")]
    [SerializeField] private WeaponData[] availableWeapons; // Array of all weapons
    // Index 0 = first weapon, Index 1 = second weapon, etc.
    // PlayerData.WeaponIndex of 0 = no weapon
    // PlayerData.WeaponIndex of 1 = availableWeapons[0]
    // PlayerData.WeaponIndex of 2 = availableWeapons[1], etc.

    [Header("Weapon Prefabs")]
    [SerializeField] private WeaponPickup[] weaponPrefabs; // Prefabs for each weapon (same order as availableWeapons)


    [Header("Game State")]
    [Networked] private NetworkBool GameStarted { get; set; }
    [Networked] private NetworkBool RoundInProgress { get; set; }
    [Networked] private int RoundNumber { get; set; }
    [Networked] private TickTimer RoundTimer { get; set; }

    private enum GameState
    {
        WaitingForPlayers,
        RoundStarting,
        RoundActive,
        RoundEnding
    }

    [Networked] private GameState CurrentState { get; set; }

    private List<PlayerData> allPlayers = new List<PlayerData>();

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentState = GameState.WaitingForPlayers;
            GameStarted = false;
            RoundInProgress = false;
            RoundNumber = 0;
        }

        // Find spawn points if not assigned
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            GameObject spawnParent = GameObject.Find("SpawnPoints");
            if (spawnParent != null)
            {
                spawnPoints = spawnParent.GetComponentsInChildren<Transform>()
                    .Where(t => t != spawnParent.transform).ToArray();
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        UpdatePlayerList();

        switch (CurrentState)
        {
            case GameState.WaitingForPlayers:
                // Waiting for game to be started manually
                break;

            case GameState.RoundStarting:
                if (RoundTimer.ExpiredOrNotRunning(Runner))
                {
                    StartRound();
                }
                break;

            case GameState.RoundActive:
                CheckForRoundEnd();
                break;

            case GameState.RoundEnding:
                if (RoundTimer.ExpiredOrNotRunning(Runner))
                {
                    PrepareNextRound();
                }
                break;
        }
    }

    public void SpawnDroppedWeapon(WeaponData weaponData, Vector3 position)
    {
        if (!Object.HasStateAuthority)
            return;

        int weaponIndex = GetWeaponIndex(weaponData);
        if (weaponIndex == -1 || weaponPrefabs == null || weaponIndex >= weaponPrefabs.Length)
        {
            Debug.LogError("Cannot spawn weapon: Invalid weapon or prefab not assigned!");
            return;
        }

        WeaponPickup prefab = weaponPrefabs[weaponIndex];
        if (prefab == null)
        {
            Debug.LogError($"Weapon prefab at index {weaponIndex} is null!");
            return;
        }

        // Spawn the weapon with a small offset and throw velocity
        Vector3 spawnPos = position + Vector3.up * 0.5f + Vector3.right * Random.Range(-0.5f, 0.5f);
        Vector2 throwVelocity = new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 6f));

        WeaponPickup droppedWeapon = Runner.Spawn(prefab, spawnPos, Quaternion.identity);
        droppedWeapon.Drop(spawnPos, throwVelocity);
    }

    private void UpdatePlayerList()
    {
        allPlayers.Clear();
        allPlayers.AddRange(FindObjectsOfType<PlayerData>());
    }

    // Called by UI button
    public void StartGame()
    {
        if (!Object.HasStateAuthority)
            return;

        if (GameStarted)
        {
            Debug.Log("Game already started!");
            return;
        }

        UpdatePlayerList();

        if (allPlayers.Count < minPlayers)
        {
            Debug.Log($"Not enough players! Need at least {minPlayers}, have {allPlayers.Count}");
            return;
        }

        if (allPlayers.Count > maxPlayers)
        {
            Debug.Log($"Too many players! Max is {maxPlayers}, have {allPlayers.Count}");
            return;
        }

        GameStarted = true;
        RoundNumber = 1;
        Debug.Log($"Game started with {allPlayers.Count} players!");

        PrepareNextRound();
    }

    private void PrepareNextRound()
    {
        CurrentState = GameState.RoundStarting;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundStartDelay);

        Debug.Log($"Starting Round {RoundNumber} in {roundStartDelay} seconds...");

        // Position all players at spawn points (elevated)
        PositionPlayersAtSpawns();

        // Reset all players
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                player.Respawn();

                // Freeze players during countdown (you can implement this in PlayerController)
                // For now, they'll just be positioned high up
            }
        }
    }

    private void PositionPlayersAtSpawns()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned! Players will not be repositioned.");
            return;
        }

        // Shuffle spawn points for randomness
        List<Transform> shuffledSpawns = spawnPoints.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < allPlayers.Count; i++)
        {
            if (allPlayers[i] != null)
            {
                // Use modulo to loop through spawn points if more players than spawns
                Transform spawnPoint = shuffledSpawns[i % shuffledSpawns.Count];

                // Position player above the spawn point
                Vector3 spawnPosition = spawnPoint.position + Vector3.up * spawnHeight;
                allPlayers[i].transform.position = spawnPosition;

                // Reset velocity in PlayerController if possible
                PlayerController controller = allPlayers[i].GetComponent<PlayerController>();
                if (controller != null)
                {
                    // The controller will reset velocity automatically
                }
            }
        }
    }

    private void StartRound()
    {
        CurrentState = GameState.RoundActive;
        RoundInProgress = true;

        Debug.Log($"Round {RoundNumber} started! Players dropped!");

        // Players are now free to move and fall
        // The PlayerController should already handle movement
    }

    private void CheckForRoundEnd()
    {
        UpdatePlayerList();

        // Count alive players
        int alivePlayers = allPlayers.Count(p => p != null && !p.Dead);

        if (alivePlayers <= 1)
        {
            EndRound();
        }
    }

    private void EndRound()
    {
        CurrentState = GameState.RoundEnding;
        RoundInProgress = false;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);

        // Find the winner
        PlayerData winner = allPlayers.FirstOrDefault(p => p != null && !p.Dead);

        if (winner != null)
        {
            Debug.Log($"Round {RoundNumber} ended! Winner: Player {winner.Object.InputAuthority}");
        }
        else
        {
            Debug.Log($"Round {RoundNumber} ended! No winner (all died).");
        }

        RoundNumber++;
    }

    // Public getters for UI
    public int GetAlivePlayers()
    {
        UpdatePlayerList();
        return allPlayers.Count(p => p != null && !p.Dead);
    }

    public int GetTotalPlayers()
    {
        return allPlayers.Count;
    }

    public int GetRoundNumber()
    {
        return RoundNumber;
    }

    public bool IsGameStarted()
    {
        return GameStarted;
    }

    public bool IsRoundActive()
    {
        return RoundInProgress;
    }

    public string GetGameStateString()
    {
        return CurrentState.ToString();
    }

    // Weapon helper methods
    public WeaponData GetWeaponData(int weaponIndex)
    {
        // WeaponIndex 0 = no weapon
        if (weaponIndex <= 0 || availableWeapons == null)
            return null;

        // Convert player weapon index to array index
        int arrayIndex = weaponIndex - 1;

        if (arrayIndex >= 0 && arrayIndex < availableWeapons.Length)
            return availableWeapons[arrayIndex];

        return null;
    }

    // Helper to get total number of weapons
    public int GetWeaponCount()
    {
        return availableWeapons != null ? availableWeapons.Length : 0;
    }

    public int GetWeaponIndex(WeaponData weaponData)
    {
        if (availableWeapons == null || weaponData == null)
            return -1;

        for (int i = 0; i < availableWeapons.Length; i++)
        {
            if (availableWeapons[i] == weaponData)
                return i; // Return array index (0-based)
        }

        return -1; // Not found
    }

    // Optional: Reset the entire game
    public void ResetGame()
    {
        if (!Object.HasStateAuthority)
            return;

        GameStarted = false;
        RoundInProgress = false;
        CurrentState = GameState.WaitingForPlayers;
        RoundNumber = 0;

        Debug.Log("Game reset!");
    }
}