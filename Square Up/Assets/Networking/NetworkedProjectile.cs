using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public TickTimer LifeTime { get; set; }
    [Networked] public int Damage { get; set; }
    [Networked] public float Knockback { get; set; }

    [Header("Detection Layers")]
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask combatLayer;

    private TickTimer _ignoreOwnerTimer;

    public void Initialize(WeaponData data, Vector2 velocity, PlayerRef owner)
    {
        Velocity = velocity;
        Owner = owner;
        Damage = data.damage;
        Knockback = data.knockbackForce;
        LifeTime = TickTimer.CreateFromSeconds(Runner, data.bulletLifetime);

        // Short timer to prevent the bullet from hitting the shooter immediately
        _ignoreOwnerTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
    }

    public override void FixedUpdateNetwork()
    {
        if (LifeTime.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        Vector2 movement = Velocity * Runner.DeltaTime;
        float distance = movement.magnitude;

        // 1. Environment Collision (Walls)
        RaycastHit2D envHit = Physics2D.Raycast(transform.position, Velocity.normalized, distance, environmentLayer);
        if (envHit.collider != null)
        {
            if (Object.HasStateAuthority) Runner.Despawn(Object);
            return;
        }

        // 2. Combat Collision (Players) - Lag Compensated
        if (Runner.LagCompensation.Raycast(transform.position, Velocity.normalized, distance,
            Owner, out LagCompensatedHit hit, combatLayer, HitOptions.IncludePhysX))
        {
            // Ignore the owner for the first few frames to prevent self-collision
            if (hit.Hitbox != null && hit.Hitbox.Root.Object.InputAuthority == Owner && !_ignoreOwnerTimer.Expired(Runner))
            {
                // Continue movement if it's the owner and the timer hasn't cleared
            }
            else if (Object.HasStateAuthority)
            {
                ApplyHitLogic(hit);
                Runner.Despawn(Object);
                return;
            }
        }

        // 3. Apply Movement
        transform.position += (Vector3)movement;

        // Update rotation to match velocity
        if (Velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void ApplyHitLogic(LagCompensatedHit hit)
    {
        if (hit.GameObject.TryGetComponent<PlayerData>(out var data))
        {
            data.TakeDamage(Damage);
            if (hit.GameObject.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(Velocity.normalized * Knockback, ForceMode2D.Impulse);
        }
    }
}