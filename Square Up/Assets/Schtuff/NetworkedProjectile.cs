using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked]
    public Vector2 Velocity { get; set; }

    [Networked]
    public int Damage { get; set; }

    [Networked]
    public float KnockbackForce { get; set; }

    [Networked]
    public PlayerRef Owner { get; set; }

    [Networked]
    public TickTimer LifeTime { get; set; }

    [SerializeField] private float maxLifeTime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private bool useGravity = false;
    [SerializeField] private float gravityScale = 1f;

    private bool hasHit = false;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            // Set lifetime for the projectile
            LifeTime = TickTimer.CreateFromSeconds(Runner, maxLifeTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            // Check if lifetime expired
            if (LifeTime.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            // Apply gravity if enabled
            if (useGravity)
            {
                Vector2 gravity = new Vector2(0, Physics2D.gravity.y * gravityScale * Runner.DeltaTime);
                Velocity += gravity;
            }

            // Calculate movement for this tick
            Vector2 movement = Velocity * Runner.DeltaTime;

            // Perform raycast to check for hits
            if (!hasHit)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, movement.normalized, movement.magnitude, hitLayers);

                if (hit.collider != null)
                {
                    OnHit(hit);
                    return;
                }
            }

            // Move the projectile
            transform.position += (Vector3)movement;

            // Rotate to face direction of travel
            if (Velocity != Vector2.zero)
            {
                float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    private void OnHit(RaycastHit2D hit)
    {
        if (hasHit) return;
        hasHit = true;

        // Check if we hit a player
        if (hit.collider.TryGetComponent<PlayerData>(out PlayerData playerData))
        {
            // Don't hit the owner
            if (playerData.Object.InputAuthority == Owner)
            {
                return;
            }

            // Apply damage
            playerData.TakeDamage(Damage);

            // Apply knockback if the hit object has a rigidbody2D
            if (hit.collider.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.AddForce(Velocity.normalized * KnockbackForce, ForceMode2D.Impulse);
            }

            Debug.Log($"Projectile hit player {playerData.Object.InputAuthority}! Dealt {Damage} damage");
        }

        // Optional: Spawn hit effect at impact point
        // SpawnHitEffect(hit.point, hit.normal);

        // Despawn the projectile
        Runner.Despawn(Object);
    }

    /// <summary>
    /// Initialize the projectile with weapon data
    /// </summary>
    public void Initialize(WeaponData weaponData, Vector2 direction, PlayerRef owner)
    {
        if (Object.HasStateAuthority)
        {
            Velocity = direction.normalized * weaponData.bulletSpeed;
            Damage = weaponData.damage;
            KnockbackForce = weaponData.knockbackForce;
            Owner = owner;
        }
    }

    /// <summary>
    /// Initialize the projectile with custom parameters
    /// </summary>
    public void Initialize(Vector2 velocity, int damage, float knockback, PlayerRef owner)
    {
        if (Object.HasStateAuthority)
        {
            Velocity = velocity;
            Damage = damage;
            KnockbackForce = knockback;
            Owner = owner;
        }
    }
}