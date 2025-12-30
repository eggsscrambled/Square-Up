using UnityEngine;
using Fusion;

public class NetworkedProjectile : NetworkBehaviour
{
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public int Damage { get; set; }
    [Networked] public float KnockbackForce { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public TickTimer LifeTime { get; set; }

    [SerializeField] private float maxLifeTime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private bool useGravity = false;
    [SerializeField] private float gravityScale = 1f;

    public LayerMask GetHitLayers() => hitLayers;
    public bool GetUseGravity() => useGravity;
    public float GetGravityScale() => gravityScale;

    private bool _hasHit = false;
    private SpriteRenderer _spriteRenderer;

    public override void Spawned()
    {
        // Only the server initializes the timer
        if (Object.HasStateAuthority)
        {
            LifeTime = TickTimer.CreateFromSeconds(Runner, maxLifeTime);
        }

        // Functional projectiles are ALWAYS invisible (they're server-authoritative hitboxes only)
        DisableVisuals();
    }

    private void DisableVisuals()
    {
        // Disable sprite renderer on this object
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }

        // Disable all child objects (particles, trails, etc.)
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 1. Check Lifetime (Server only handles despawning)
        if (Object.HasStateAuthority && LifeTime.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // 2. Movement Logic (Runs on BOTH Client and Server for Prediction)
        // Note: No 'HasStateAuthority' check here so the client can "predict" the bullet path
        MoveProjectile();
    }

    private void MoveProjectile()
    {
        if (_hasHit) return;

        // Apply gravity
        if (useGravity)
        {
            Vector2 gravity = new Vector2(0, Physics2D.gravity.y * gravityScale * Runner.DeltaTime);
            Velocity += gravity;
        }

        Vector2 movement = Velocity * Runner.DeltaTime;

        // 3. Collision Detection (Only State Authority applies damage/despawns)
        // But we check on both so the client stops the bullet visually immediately
        RaycastHit2D hit = Physics2D.Raycast(transform.position, movement.normalized, movement.magnitude, hitLayers);

        if (hit.collider != null)
        {
            // Ignore owner
            if (hit.collider.TryGetComponent<PlayerData>(out var data) && data.Object.InputAuthority == Owner)
            {
                transform.position += (Vector3)movement; // Keep moving past the owner
            }
            else
            {
                OnHit(hit);
                return;
            }
        }

        // Apply Position
        transform.position += (Vector3)movement;

        // Rotate to face travel
        if (Velocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void OnHit(RaycastHit2D hit)
    {
        _hasHit = true;

        // Only the Server applies damage and effects
        if (Object.HasStateAuthority)
        {
            if (hit.collider.TryGetComponent<PlayerData>(out PlayerData playerData))
            {
                playerData.TakeDamage(Damage);

                if (hit.collider.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                {
                    rb.AddForce(Velocity.normalized * KnockbackForce, ForceMode2D.Impulse);
                }
            }

            // Despawn on server
            Runner.Despawn(Object);
        }
    }

    public void Initialize(WeaponData weaponData, Vector2 direction, PlayerRef owner)
    {
        // This is called by the Server/Host in Runner.Spawn
        Velocity = direction.normalized * weaponData.bulletSpeed;
        Damage = weaponData.damage;
        KnockbackForce = weaponData.knockbackForce;
        Owner = owner;

        // Use the weapon-specific lifetime instead of the default
        if (Object.HasStateAuthority)
        {
            LifeTime = TickTimer.CreateFromSeconds(Runner, weaponData.bulletLifetime);
        }
    }
}