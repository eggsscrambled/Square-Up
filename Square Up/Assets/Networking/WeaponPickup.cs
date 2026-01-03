using UnityEngine;
using Fusion;

public class WeaponPickup : NetworkBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float floatAmount = 0.3f;

    [Header("Held Settings")]
    [SerializeField] private float orbitRadius = 0.5f;
    [SerializeField] private float verticalOffset = -0.3f;
    [SerializeField] private bool hideWhenHeld = true;
    [SerializeField] private bool rotateWithAim = true;
    [SerializeField] private bool flipSpriteWhenAimingLeft = true;
    [SerializeField] private bool flipShouldFlip = false;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip reloadStartSound;
    [SerializeField] private AudioClip reloadMidSound;
    [SerializeField] private AudioClip reloadEndSound;

    [Networked] private NetworkBool IsPickedUp { get; set; }
    [Networked] private PlayerRef Owner { get; set; }
    [Networked] private Vector2 AimDirection { get; set; }
    [Networked] private NetworkId OwnerId { get; set; }
    [Networked] public int CurrentAmmo { get; set; }

    private Rigidbody2D rb;
    private Collider2D col;
    private NetworkTransform networkTransform;
    private Vector3 startPosition;
    private float floatTimer;
    private GameManager gameManager;
    private Transform originalParent;
    private Transform ownerTransform;
    private Transform fireOrigin;
    private Vector3 originalFireOriginLocalPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        networkTransform = GetComponent<NetworkTransform>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        originalParent = transform.parent;
        fireOrigin = transform.Find("FireOrigin");
        if (fireOrigin != null) originalFireOriginLocalPos = fireOrigin.localPosition;
    }

    public override void Spawned()
    {
        startPosition = transform.position;
        gameManager = FindFirstObjectByType<GameManager>();

        if (Object.HasStateAuthority)
        {
            IsPickedUp = false;
            Owner = PlayerRef.None;
            CurrentAmmo = weaponData != null ? weaponData.maxAmmo : 0;
        }

        // NEW: Handle late-join case
        if (IsPickedUp && networkTransform != null)
        {
            networkTransform.enabled = false;
            if (rb != null) rb.simulated = false;
            if (col != null) col.enabled = false;
        }
    }

    public void ServerExecutePickup(PlayerRef player)
    {
        if (!Object.HasStateAuthority || IsPickedUp) return;

        NetworkObject pObj = Runner.GetPlayerObject(player);
        if (pObj == null) return;

        PlayerData pd = pObj.GetComponent<PlayerData>();
        if (pd == null || pd.Dead) return;

        IsPickedUp = true;
        Owner = player;
        OwnerId = pObj.Id;
        ownerTransform = pd.transform;

        pd.PickupWeapon(gameManager.GetWeaponIndex(weaponData) + 1);

        WeaponAimController aim = pd.GetComponent<WeaponAimController>();
        if (aim != null) aim.SetCurrentWeapon(this);

        transform.SetParent(null);
        if (rb != null) rb.simulated = false;
        if (col != null) col.enabled = false;
        if (networkTransform != null) networkTransform.enabled = false;

        RPC_SyncState(true);
    }

    public void Drop(Vector3 pos, Vector2 velocity)
    {
        if (!Object.HasStateAuthority || !IsPickedUp) return;

        IsPickedUp = false;
        Owner = PlayerRef.None;
        OwnerId = default;
        ownerTransform = null;
        transform.position = pos;
        startPosition = pos;
        transform.SetParent(originalParent);

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = velocity;
        }
        if (col != null) col.enabled = true;

        // Re-enable NetworkTransform and immediately teleport to sync the new position
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
            networkTransform.Teleport(pos, transform.rotation);
        }

        // Notify all clients of the drop with the new position
        RPC_SyncDrop(pos, false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncState(bool pickedUp)
    {
        if (spriteRenderer != null && hideWhenHeld) spriteRenderer.enabled = !pickedUp;
        if (!pickedUp)
        {
            if (spriteRenderer != null) spriteRenderer.flipY = false;
            if (fireOrigin != null) fireOrigin.localPosition = originalFireOriginLocalPos;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncDrop(Vector3 dropPosition, bool pickedUp)
    {
        // Update visual state
        if (spriteRenderer != null && hideWhenHeld) spriteRenderer.enabled = !pickedUp;
        if (spriteRenderer != null) spriteRenderer.flipY = false;
        if (fireOrigin != null) fireOrigin.localPosition = originalFireOriginLocalPos;

        // Update position for clients (host already set it in Drop())
        if (!Object.HasStateAuthority)
        {
            transform.position = dropPosition;
            startPosition = dropPosition;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Update weapon position on server when held
        if (Object.HasStateAuthority && IsPickedUp && ownerTransform != null)
        {
            UpdateWeaponTransform();
        }
    }

    public override void Render()
    {
        // Resolve owner reference on all clients
        ResolveOwnerReference();

        // Update visuals locally on NON-AUTHORITY clients for smooth rendering
        if (!Object.HasStateAuthority && IsPickedUp && ownerTransform != null)
        {
            UpdateWeaponTransform();
        }

        // Handle floating animation when not picked up
        if (!IsPickedUp)
        {
            floatTimer += Time.deltaTime * floatSpeed;
            transform.position = startPosition + Vector3.up * (Mathf.Sin(floatTimer) * floatAmount);
        }

        // Handle sprite flipping
        if (IsPickedUp && flipSpriteWhenAimingLeft && spriteRenderer != null)
        {
            bool shouldFlip = (AimDirection.x < 0) ^ flipShouldFlip;
            spriteRenderer.flipY = shouldFlip;
            if (fireOrigin != null)
            {
                Vector3 fPos = fireOrigin.localPosition;
                fPos.y = shouldFlip ? -originalFireOriginLocalPos.y : originalFireOriginLocalPos.y;
                fireOrigin.localPosition = fPos;
            }
        }
    }

    private void ResolveOwnerReference()
    {
        if (OwnerId != default && ownerTransform == null)
        {
            if (Runner.TryFindObject(OwnerId, out NetworkObject ownerObj))
            {
                ownerTransform = ownerObj.transform;
            }
        }
        else if (OwnerId == default)
        {
            ownerTransform = null;
        }
    }

    private void UpdateWeaponTransform()
    {
        if (AimDirection.magnitude < 0.1f) return;
        Vector3 target = ownerTransform.position + Vector3.up * verticalOffset;
        transform.position = target + (Vector3)(AimDirection.normalized * orbitRadius);
        if (rotateWithAim) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
    }

    public void UpdateAimDirection(Vector2 aim) => AimDirection = aim;
    public bool GetIsPickedUp() => IsPickedUp;
    public WeaponData GetWeaponData() => weaponData;
    public Vector3 GetWeaponHoldPosition() => ownerTransform != null ? ownerTransform.position + Vector3.up * verticalOffset : transform.position;
    public int GetCurrentAmmo() => CurrentAmmo;
    public void SetCurrentAmmo(int ammo) => CurrentAmmo = ammo;

    // Modified Sound Methods using RPCs
    public void PlayReloadStartSound()
    {
        // Only the StateAuthority (Host/Server) or the Owner should trigger the RPC
        if (Object.HasStateAuthority) RPC_PlayReloadSound(0);
    }

    public void PlayReloadMidSound()
    {
        if (Object.HasStateAuthority) RPC_PlayReloadSound(1);
    }

    public void PlayReloadEndSound()
    {
        if (Object.HasStateAuthority) RPC_PlayReloadSound(2);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadSound(int soundType)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = null;
        switch (soundType)
        {
            case 0: clipToPlay = reloadStartSound; break;
            case 1: clipToPlay = reloadMidSound; break;
            case 2: clipToPlay = reloadEndSound; break;
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }
}