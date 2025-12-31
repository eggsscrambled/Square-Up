using UnityEngine;

[ExecuteInEditMode] // Allows you to see the effect in the Scene view
public class WallShaderManager : MonoBehaviour
{
    private Vector4[] playerPositions = new Vector4[2];
    private PlayerData[] players;

    void Update()
    {
        // Find players if the array is empty
        if (players == null || players.Length == 0)
        {
            players = Object.FindObjectsByType<PlayerData>(FindObjectsSortMode.None);
        }

        if (players.Length > 0)
        {
            for (int i = 0; i < players.Length && i < 2; i++)
            {
                if (players[i] != null)
                {
                    Vector3 p = players[i].transform.position;
                    playerPositions[i] = new Vector4(p.x, p.y, p.z, 1);
                }
            }

            Shader.SetGlobalVectorArray("_PlayerPositions", playerPositions);
            Shader.SetGlobalInt("_PlayerCount", players.Length);
        }
    }
}