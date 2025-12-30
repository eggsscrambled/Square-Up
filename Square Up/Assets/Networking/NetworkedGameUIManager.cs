using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class NetworkedGameUIManager : NetworkBehaviour
{
    public GameManager gm;
    public PlayerData[] players;
    public PlayerData localPlayer;
    public Image gunImage;
    public Image dashImage;

    private bool hasInitialized = false;

    public override void FixedUpdateNetwork()
    {
        // Only run this logic once when game starts and we have input authority
        if (!hasInitialized && gm != null && gm.GameStarted && HasInputAuthority)
        {
            InitializePlayers();
            hasInitialized = true;
        }
    }

    private void InitializePlayers()
    {
        // Find all PlayerData objects in the scene
        players = FindObjectsOfType<PlayerData>();

        if (players.Length == 0)
        {
            Debug.LogWarning("No PlayerData objects found in scene!");
            return;
        }

        // Find the local player (the one with input authority)
        foreach (PlayerData player in players)
        {
            if (player.HasInputAuthority)
            {
                localPlayer = player;
                Debug.Log($"Local player found: {player.name}");
                break;
            }
        }

        if (localPlayer == null)
        {
            Debug.LogWarning("Local player not found! No PlayerData has input authority.");
        }
        else
        {
            Debug.Log($"Initialized with {players.Length} total players");
            OnPlayersInitialized();
        }
    }

    // Override this method in derived classes or add your UI setup logic here
    protected virtual void OnPlayersInitialized()
    {
        // UI initialization code goes here
        // Example: Update UI elements with player data
    }

    // Helper method to get other players (excluding local player)
    public PlayerData[] GetOtherPlayers()
    {
        if (localPlayer == null || players == null) return new PlayerData[0];

        return System.Array.FindAll(players, p => p != localPlayer);
    }

    public void LateUpdate()
    {
        float dashProgress = localPlayer.gameObject.GetComponent<PlayerController>().GetDashCooldownProgress();
    }
}