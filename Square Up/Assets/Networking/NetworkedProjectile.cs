using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public TickTimer LifeTime { get; set; }

    // Missing properties added here
    [Networked] public int Damage { get; set; }
    [Networked] public float KnockbackForce { get; set; }
    [Networked] public NetworkBool UseGravity { get; set; }
    [Networked] public float GravityScale { get; set; }

    [Header("Fallbacks/Defaults")]
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private bool defaultUseGravity = false;
    [SerializeField] private float defaultGravityScale = 1f;

    // Getter methods for the WeaponAimController to check prefab defaults
    public bool GetUseGravity() => defaultUseGravity;
    public float GetGravityScale() => defaultGravityScale;
    public LayerMask GetHitLayers() => hitLayers;

    public override void FixedUpdateNetwork()
    {
        if (LifeTime.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // Apply Gravity if enabled
        if (UseGravity)
        {
            Vector2 gravity = new Vector2(0, Physics2D.gravity.y * GravityScale * Runner.DeltaTime);
            Velocity += gravity;
        }

        Vector2 movement = Velocity * Runner.DeltaTime;
        float distance = movement.magnitude;

        // Lag Compensated Raycast (Rewinds time to match client view)
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

        transform.position += (Vector3)movement;

        if (Velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void HandleHit(LagCompensatedHit hit)
    {
        if (Object.HasStateAuthority)
        {
            if (hit.GameObject.TryGetComponent<PlayerData>(out var data))
            {
                data.TakeDamage(Damage);

                if (hit.GameObject.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.AddForce(Velocity.normalized * KnockbackForce, ForceMode2D.Impulse);
                }
            }
            Runner.Despawn(Object);
        }
        else
        {
            // Predicted visual feedback
            gameObject.SetActive(false);
        }
    }
}