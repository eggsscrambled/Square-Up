using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 2; // Restricted to 2
    [SerializeField] private int winsToWinMatch = 4; // First to 4 wins
    [SerializeField] private float roundStartDelay = 3f;
    [SerializeField] private float roundEndDelay = 3f;

    [Header("Spawn Positions")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnHeight = 10f;

    [Header("Weapons")]
    [SerializeField] private WeaponData[] availableWeapons;
    [SerializeField] private WeaponPickup[] weaponPrefabs;

    [Header("Game State")]
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] private int RoundNumber { get; set; }
    [Networked] private TickTimer RoundTimer { get; set; }
    [Networked] private GameState CurrentState { get; set; }

    // Track wins for Player 0 and Player 1
    [Networked, Capacity(2)]
    private NetworkArray<int> PlayerWins => default;

    private enum GameState
    {
        WaitingForPlayers,
        RoundStarting,
        RoundActive,
        RoundEnding,
        MatchOver
    }

    private List<PlayerData> allPlayers = new List<PlayerData>();

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentState = GameState.WaitingForPlayers;
            ResetMatchScores();
        }

        // Auto-find spawn points if missing
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
        if (!Object.HasStateAuthority) return;

        UpdatePlayerList();

        switch (CurrentState)
        {
            case GameState.WaitingForPlayers:
                break;
            case GameState.RoundStarting:
                if (RoundTimer.ExpiredOrNotRunning(Runner)) StartRound();
                break;
            case GameState.RoundActive:
                CheckForRoundEnd();
                break;
            case GameState.RoundEnding:
                if (RoundTimer.ExpiredOrNotRunning(Runner)) PrepareNextRound();
                break;
        }
    }

    // --- Core Game Flow ---

    public void StartGame()
    {
        if (!Object.HasStateAuthority || GameStarted) return;

        UpdatePlayerList();

        if (allPlayers.Count != 2)
        {
            Debug.Log($"Need exactly 2 players! Have: {allPlayers.Count}");
            return;
        }

        GameStarted = true;
        RoundNumber = 1;
        ResetMatchScores();
        PrepareNextRound();
    }

    private void PrepareNextRound()
    {
        if (CurrentState == GameState.MatchOver) return;

        CurrentState = GameState.RoundStarting;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundStartDelay);

        PositionPlayersAtSpawns();

        foreach (var player in allPlayers)
        {
            if (player != null) player.Respawn();
        }
    }

    private void StartRound()
    {
        CurrentState = GameState.RoundActive;
        Debug.Log($"Round {RoundNumber} Started!");
    }

    private void CheckForRoundEnd()
    {
        int alivePlayers = allPlayers.Count(p => p != null && !p.Dead);
        if (alivePlayers <= 1)
        {
            EndRound();
        }
    }

    private void EndRound()
    {
        CurrentState = GameState.RoundEnding;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);

        PlayerData roundWinner = allPlayers.FirstOrDefault(p => p != null && !p.Dead);

        if (roundWinner != null)
        {
            int winnerIndex = allPlayers.IndexOf(roundWinner);
            if (winnerIndex != -1)
            {
                PlayerWins.Set(winnerIndex, PlayerWins[winnerIndex] + 1);
                Debug.Log($"Player {winnerIndex} wins round! Total: {PlayerWins[winnerIndex]}");
            }
        }

        if (PlayerWins[0] >= winsToWinMatch || PlayerWins[1] >= winsToWinMatch)
        {
            CurrentState = GameState.MatchOver;
            Debug.Log("Match Finished!");
        }
        else
        {
            RoundNumber++;
        }
    }

    private void ResetMatchScores()
    {
        for (int i = 0; i < PlayerWins.Length; i++) PlayerWins.Set(i, 0);
    }

    private void UpdatePlayerList()
    {
        allPlayers = FindObjectsOfType<PlayerData>()
            .OrderBy(p => p.Object.InputAuthority.PlayerId)
            .ToList();
    }

    private void PositionPlayersAtSpawns()
    {
        if (spawnPoints.Length < 2) return;
        for (int i = 0; i < allPlayers.Count; i++)
        {
            if (allPlayers[i] != null)
            {
                allPlayers[i].transform.position = spawnPoints[i % spawnPoints.Length].position + Vector3.up * spawnHeight;
            }
        }
    }

    // --- Weapon Helper Methods (Fixed Errors) ---

    public void SpawnDroppedWeapon(WeaponData weaponData, Vector3 position)
    {
        if (!Object.HasStateAuthority) return;

        int weaponIndex = GetWeaponIndex(weaponData);
        if (weaponIndex == -1 || weaponPrefabs == null || weaponIndex >= weaponPrefabs.Length) return;

        WeaponPickup prefab = weaponPrefabs[weaponIndex];
        Vector3 spawnPos = position + Vector3.up * 0.5f;
        Vector2 throwVelocity = new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 6f));

        WeaponPickup droppedWeapon = Runner.Spawn(prefab, spawnPos, Quaternion.identity);
        droppedWeapon.Drop(spawnPos, throwVelocity);
    }

    public WeaponData GetWeaponData(int weaponIndex)
    {
        if (weaponIndex <= 0 || availableWeapons == null) return null;
        int arrayIndex = weaponIndex - 1;
        return (arrayIndex >= 0 && arrayIndex < availableWeapons.Length) ? availableWeapons[arrayIndex] : null;
    }

    public int GetWeaponIndex(WeaponData weaponData)
    {
        if (availableWeapons == null || weaponData == null) return -1;
        for (int i = 0; i < availableWeapons.Length; i++)
        {
            if (availableWeapons[i] == weaponData) return i;
        }
        return -1;
    }

    public int GetWeaponCount() => availableWeapons?.Length ?? 0;

    // --- UI Getters ---
    public int GetPlayerWins(int index) => (index >= 0 && index < 2) ? PlayerWins[index] : 0;
    public int GetRoundNumber() => RoundNumber;
}