using UnityEngine;
using Fusion;

public class WeaponPickup : NetworkBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private float pickupRadius = 1f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float floatAmount = 0.3f;

    [Header("Pickup Settings")]
    [SerializeField] private float orbitRadius = 0.5f; // Distance from player center
    [SerializeField] private bool hideWhenHeld = true;
    [SerializeField] private bool rotateWithAim = true;
    [SerializeField] private bool flipSpriteWhenAimingLeft = true;

    [Networked] private NetworkBool IsPickedUp { get; set; }
    [Networked] private PlayerRef Owner { get; set; }
    [Networked] private Vector2 AimDirection { get; set; }

    private PlayerRef nearbyPlayer = PlayerRef.None;
    private Rigidbody2D rb;
    private Collider2D col;
    private Vector3 startPosition;
    private float floatTimer;
    private GameManager gameManager;
    private Transform originalParent;
    private Transform ownerTransform;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        originalParent = transform.parent;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsPickedUp = false;
            Owner = PlayerRef.None;
            AimDirection = Vector2.right; // Default aim right
        }

        startPosition = transform.position;

        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found! WeaponPickup needs GameManager in scene.");
        }
    }



    public void TryPickup(PlayerRef player)
    {
        Debug.Log($"TryPickup called by player {player}");

        if (!Object.HasStateAuthority)
        {
            Debug.Log("No state authority");
            return;
        }

        if (IsPickedUp)
        {
            Debug.Log("Already picked up");
            return;
        }

        if (nearbyPlayer != player)
        {
            Debug.Log($"Player {player} not nearby. Nearby player is {nearbyPlayer}");
            return;
        }

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, pickupRadius, playerLayer);
        Debug.Log($"Found {nearbyColliders.Length} colliders in pickup range");

        foreach (var col in nearbyColliders)
        {
            PlayerData playerData = col.GetComponent<PlayerData>();
            if (playerData != null && playerData.Object.InputAuthority == player && !playerData.Dead)
            {
                Debug.Log($"Attempting pickup for player {player}");
                AttemptPickup(playerData);
                break;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!IsPickedUp)
        {
            // Check for nearby players only when not picked up
            CheckForNearbyPlayers();
        }
        else if (ownerTransform != null)
        {
            // Position weapon in orbit around player and rotate towards aim
            UpdateWeaponTransform();
        }
    }

    private void CheckForNearbyPlayers()
    {
        // Check for nearby players
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, pickupRadius, playerLayer);

        PlayerRef closestPlayer = PlayerRef.None;
        float closestDistance = float.MaxValue;

        Debug.Log($"Weapon at {transform.position}, checking {nearbyColliders.Length} colliders");

        foreach (var col in nearbyColliders)
        {
            PlayerData player = col.GetComponent<PlayerData>();
            if (player != null && !player.Dead)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                Debug.Log($"Found player at distance {distance}");
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = player.Object.InputAuthority;
                }
            }
        }

        nearbyPlayer = closestPlayer;
        if (closestPlayer != PlayerRef.None)
        {
            Debug.Log($"Nearby player detected: {closestPlayer}");
        }
    }

    private void Update()
    {
        if (!IsPickedUp)
        {
            // Floating animation when not picked up
            floatTimer += Time.deltaTime * floatSpeed;
            float yOffset = Mathf.Sin(floatTimer) * floatAmount;
            transform.position = startPosition + Vector3.up * yOffset;
        }

        // Visual sprite flipping (non-networked, cosmetic only)
        if (IsPickedUp && flipSpriteWhenAimingLeft && spriteRenderer != null)
        {
            spriteRenderer.flipY = AimDirection.x < 0;
        }
    }

    private void UpdateWeaponTransform()
    {
        if (AimDirection.magnitude < 0.1f)
            return;

        // Position weapon in a circle around the player based on aim direction
        Vector3 targetPosition = ownerTransform.position + (Vector3)(AimDirection.normalized * orbitRadius);
        transform.position = targetPosition;

        if (rotateWithAim)
        {
            // Rotate weapon to face aim direction
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    public void UpdateAimDirection(Vector2 aimDirection)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!IsPickedUp)
            return;

        AimDirection = aimDirection.normalized;
    }

    private void AttemptPickup(PlayerData player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (gameManager == null || weaponData == null)
        {
            Debug.LogError("Cannot pickup weapon: GameManager or WeaponData is null!");
            return;
        }

        int weaponIndex = gameManager.GetWeaponIndex(weaponData);

        if (weaponIndex == -1)
        {
            Debug.LogError($"Weapon {weaponData.weaponID} not found in GameManager's available weapons!");
            return;
        }

        if (player.HasWeapon())
        {
            DropWeapon(player);
        }

        player.PickupWeapon(weaponIndex + 1);
        IsPickedUp = true;
        Owner = player.Object.InputAuthority;
        ownerTransform = player.transform;

        // Notify the player's aim controller
        WeaponAimController aimController = player.GetComponent<WeaponAimController>();
        if (aimController != null)
        {
            aimController.SetCurrentWeapon(this);
        }

        // Don't parent to player - we'll position manually in FixedUpdateNetwork
        transform.SetParent(null);

        // Disable physics
        if (rb != null)
            rb.simulated = false;
        if (col != null)
            col.enabled = false;

        Debug.Log($"Player {player.Object.InputAuthority} picked up {weaponData.weaponID}");
    }

    private void DropWeapon(PlayerData player)
    {
        if (!Object.HasStateAuthority)
            return;

        WeaponData currentWeaponData = gameManager.GetWeaponData(player.WeaponIndex);

        if (currentWeaponData != null)
        {
            gameManager.SpawnDroppedWeapon(currentWeaponData, player.transform.position);
        }
    }

    public void Drop(Vector3 position, Vector2 throwVelocity)
    {
        if (!Object.HasStateAuthority)
            return;

        IsPickedUp = false;
        Owner = PlayerRef.None;
        ownerTransform = null;

        transform.SetParent(originalParent);
        transform.position = position;
        transform.rotation = Quaternion.identity;
        startPosition = position;

        // Re-enable physics and rendering
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = throwVelocity;
        }
        if (col != null)
            col.enabled = true;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.flipY = false; // Reset flip
        }
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }

    public bool IsPlayerNearby(PlayerRef player)
    {
        return nearbyPlayer == player && !IsPickedUp;
    }

    public bool GetIsPickedUp()
    {
        return IsPickedUp;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        // Show orbit radius when picked up
        if (IsPickedUp && ownerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ownerTransform.position, orbitRadius);
        }
    }
}