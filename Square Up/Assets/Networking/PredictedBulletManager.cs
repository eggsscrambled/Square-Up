using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class PredictedBulletManager : MonoBehaviour
{
    public static PredictedBulletManager Instance { get; private set; }

    private Dictionary<int, PredictedBullet> predictedBullets = new Dictionary<int, PredictedBullet>();

    // Store reference to local runner for host check
    private NetworkRunner localRunner;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Call this method from your network initialization code
    public void SetLocalRunner(NetworkRunner runner)
    {
        localRunner = runner;
    }

    // Check if local player is the host
    public bool IsLocalPlayerHost()
    {
        if (localRunner == null) return false;

        // Check if local runner is the server/host
        return localRunner.IsServer;
    }

    public void RegisterPredictedBullet(PredictedBullet bullet)
    {
        if (!predictedBullets.ContainsKey(bullet.BulletId))
        {
            predictedBullets.Add(bullet.BulletId, bullet);
        }
    }

    public void UnregisterPredictedBullet(PredictedBullet bullet)
    {
        predictedBullets.Remove(bullet.BulletId);
    }

    public void OnNetworkedBulletSpawned(int bulletId)
    {
        if (predictedBullets.TryGetValue(bulletId, out PredictedBullet bullet))
        {
            if (bullet != null)
            {
                Destroy(bullet.gameObject);
            }
            predictedBullets.Remove(bulletId);
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