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
        // Keep trying to initialize until we succeed
        if (!hasInitialized && gm != null && gm.GameStarted && HasInputAuthority)
        {
            if (TryInitializePlayers())
            {
                hasInitialized = true;
            }
        }
    }

    private bool TryInitializePlayers()
    {
        // Find all PlayerData objects in the scene
        players = FindObjectsOfType<PlayerData>();

        if (players.Length == 0)
        {
            Debug.LogWarning("No PlayerData objects found yet, waiting...");
            return false; // Try again next frame
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
            Debug.LogWarning("Local player not found yet, waiting...");
            return false; // Try again next frame
        }

        Debug.Log($"Initialized with {players.Length} total players");
        OnPlayersInitialized();
        return true; // Successfully initialized
    }

    protected virtual void OnPlayersInitialized()
    {
        // UI initialization code goes here
    }

    public PlayerData[] GetOtherPlayers()
    {
        if (localPlayer == null || players == null) return new PlayerData[0];
        return System.Array.FindAll(players, p => p != localPlayer);
    }

    public void LateUpdate()
    {
        // Add null check to prevent errors before initialization
        if (localPlayer == null) return;

        PlayerController controller = localPlayer.gameObject.GetComponent<PlayerController>();
        if (controller != null)
        {
            float dashProgress = controller.GetDashCooldownProgress();
            dashImage.fillAmount = 1 - dashProgress;
        }
    }
}