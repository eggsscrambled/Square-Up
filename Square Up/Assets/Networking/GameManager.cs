using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro; // Added for TextMeshPro support

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private int winsToWinMatch = 7; // First to 7 wins
    [SerializeField] private float roundStartDelay = 3f;
    [SerializeField] private float roundEndDelay = 4f;

    [Header("UI Track Setup")]
    [SerializeField] private GameObject roundEndUIPanel;
    [SerializeField] private TextMeshProUGUI winStatusText; // Modernized to TextMeshPro
    [SerializeField] private Image[] p1WinSprites; // 6 sprites for P1
    [SerializeField] private Image[] p2WinSprites; // 6 sprites for P2
    [SerializeField] private Image winnerCenterSprite; // The middle "7th win" sprite

    [Header("Spawn Positions")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnHeight = 10f;

    [Header("Weapons")]
    [SerializeField] private WeaponData[] availableWeapons;
    [SerializeField] private WeaponPickup[] weaponPrefabs;

    [Header("Game State")]
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool ShowRoundEndUI { get; set; }
    [Networked] private int RoundNumber { get; set; }
    [Networked] private TickTimer RoundTimer { get; set; }
    [Networked] private GameState CurrentState { get; set; }

    [Networked] private int LastRoundWinnerIndex { get; set; }

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
            ShowRoundEndUI = false;
            LastRoundWinnerIndex = -1;
            ResetMatchScores();
        }

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
        UpdatePlayerList();

        if (Object.HasStateAuthority && allPlayers.Count == 2)
        {
            EnsureUniqueColors();
        }

        if (!Object.HasStateAuthority) return;

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

    public override void Render()
    {
        if (roundEndUIPanel != null)
        {
            roundEndUIPanel.SetActive(ShowRoundEndUI);
        }

        if (ShowRoundEndUI && allPlayers.Count == 2)
        {
            UpdateWinTrackUI();
            UpdateWinStatusText();
        }
    }

    private void UpdateWinTrackUI()
    {
        Color p1Color = allPlayers[0].GetActualColor();
        Color p2Color = allPlayers[1].GetActualColor();
        Color fadedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        // Update P1 Icons 
        for (int i = 0; i < p1WinSprites.Length; i++)
        {
            p1WinSprites[i].color = (PlayerWins[0] > i) ? p1Color : fadedColor;
        }

        // Update P2 Icons
        for (int i = 0; i < p2WinSprites.Length; i++)
        {
            p2WinSprites[i].color = (PlayerWins[1] > i) ? p2Color : fadedColor;
        }

        // Center Sprite logic
        if (PlayerWins[0] >= winsToWinMatch) winnerCenterSprite.color = p1Color;
        else if (PlayerWins[1] >= winsToWinMatch) winnerCenterSprite.color = p2Color;
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
            winStatusText.text = $"PLAYER {LastRoundWinnerIndex + 1} WINS ROUND!";
            winStatusText.color = allPlayers[LastRoundWinnerIndex].GetActualColor();
        }

        if (CurrentState == GameState.MatchOver && LastRoundWinnerIndex != -1)
        {
            winStatusText.text = $"PLAYER {LastRoundWinnerIndex + 1} WINS THE MATCH!";
        }
    }

    private void EnsureUniqueColors()
    {
        if (allPlayers[0].PlayerColorIndex == allPlayers[1].PlayerColorIndex)
        {
            allPlayers[1].PlayerColorIndex = (allPlayers[1].PlayerColorIndex + 1) % 10;
        }
    }

    public void StartGame()
    {
        if (!Object.HasStateAuthority || GameStarted) return;
        if (allPlayers.Count != 2) return;

        GameStarted = true;
        RoundNumber = 1;
        ResetMatchScores();
        PrepareNextRound();
    }

    private void PrepareNextRound()
    {
        if (CurrentState == GameState.MatchOver) return;

        ShowRoundEndUI = false;
        LastRoundWinnerIndex = -1;
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
        ShowRoundEndUI = true;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);

        PlayerData roundWinner = allPlayers.FirstOrDefault(p => p != null && !p.Dead);

        if (roundWinner != null)
        {
            LastRoundWinnerIndex = allPlayers.IndexOf(roundWinner);
            if (LastRoundWinnerIndex != -1)
            {
                PlayerWins.Set(LastRoundWinnerIndex, PlayerWins[LastRoundWinnerIndex] + 1);
            }
        }
        else
        {
            LastRoundWinnerIndex = -1;
        }

        if (PlayerWins[0] >= winsToWinMatch || PlayerWins[1] >= winsToWinMatch)
        {
            CurrentState = GameState.MatchOver;
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

    // --- Weapon Helpers (DO NOT REMOVE) ---
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
}