using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public TickTimer LifeTime { get; set; }

    [Header("Settings")]
    [SerializeField] private float maxLifeTime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackForce = 5f;

    public override void Spawned()
    {
        // On the Host, we initialize the timer. 
        // Prediction will handle the visual life on the client.
        if (Object.HasStateAuthority)
        {
            LifeTime = TickTimer.CreateFromSeconds(Runner, maxLifeTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 1. Check Lifetime
        if (LifeTime.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // 2. Calculate Movement for this tick
        Vector2 movement = Velocity * Runner.DeltaTime;
        float distance = movement.magnitude;

        // 3. LAG COMPENSATED HIT DETECTION
        // This is the "Secret Sauce." It rewinds the server to the exact time 
        // the client saw the enemy to check if the hit was valid.
        if (Runner.LagCompensation.Raycast(
            transform.position,
            Velocity.normalized,
            distance,
            Owner,
            out LagCompensatedHit hitInfo,
            hitLayers,
            HitOptions.IncludePhysX))
        {
            HandleHit(hitInfo);
            return;
        }

        // 4. Move the Projectile
        transform.position += (Vector3)movement;

        // Rotate to face travel
        if (Velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void HandleHit(LagCompensatedHit hit)
    {
        // Only the Server applies damage and despawns
        if (Object.HasStateAuthority)
        {
            if (hit.GameObject.TryGetComponent<PlayerData>(out var data))
            {
                data.TakeDamage((int)damage);

                if (hit.GameObject.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.AddForce(Velocity.normalized * knockbackForce, ForceMode2D.Impulse);
                }
            }
            Runner.Despawn(Object);
        }
        else
        {
            // Clients can disable their local predicted visual immediately on hit
            gameObject.SetActive(false);
        }
    }
}