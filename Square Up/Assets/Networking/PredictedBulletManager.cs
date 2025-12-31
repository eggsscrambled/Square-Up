using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages predicted bullets and destroys them when their networked counterparts spawn
/// </summary>
public class PredictedBulletManager : MonoBehaviour
{
    public static PredictedBulletManager Instance { get; private set; }

    private Dictionary<int, PredictedBullet> predictedBullets = new Dictionary<int, PredictedBullet>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterPredictedBullet(PredictedBullet bullet)
    {
        if (bullet != null && !predictedBullets.ContainsKey(bullet.BulletId))
        {
            predictedBullets[bullet.BulletId] = bullet;
            Debug.Log($"[PredictedBulletManager] Registered predicted bullet ID: {bullet.BulletId}");
        }
    }

    public void UnregisterPredictedBullet(PredictedBullet bullet)
    {
        if (bullet != null && predictedBullets.ContainsKey(bullet.BulletId))
        {
            predictedBullets.Remove(bullet.BulletId);
        }
    }

    /// <summary>
    /// Called by NetworkedProjectile when it spawns on the client
    /// Destroys the matching predicted bullet
    /// </summary>
    public void OnNetworkedBulletSpawned(int bulletId)
    {
        if (predictedBullets.TryGetValue(bulletId, out PredictedBullet predicted))
        {
            Debug.Log($"[PredictedBulletManager] Destroying predicted bullet ID: {bulletId} (networked version spawned)");
            predictedBullets.Remove(bulletId);
            if (predicted != null)
            {
                Destroy(predicted.gameObject);
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}