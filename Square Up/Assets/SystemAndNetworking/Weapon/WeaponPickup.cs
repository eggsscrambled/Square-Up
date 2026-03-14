using UnityEngine;
using Fusion;

public class WeaponPickup : NetworkBehaviour
{
    // =========================================================
    //  INSPECTOR
    // =========================================================

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

    // =========================================================
    //  NETWORKED STATE
    // =========================================================

    [Networked] private NetworkBool IsPickedUp { get; set; }
    [Networked] private PlayerRef Owner { get; set; }
    [Networked] private Vector2 AimDirection { get; set; }
    [Networked] private NetworkId OwnerId { get; set; }
    [Networked] public int CurrentAmmo { get; set; }
    [Networked] private TickTimer PickupCooldown { get; set; }
    [Networked] private int ReloadSoundTrigger { get; set; }

    // =========================================================
    //  PRIVATE FIELDS
    // =========================================================

    private ChangeDetector _changeDetector;

    private Rigidbody2D rb;
    private Collider2D col;
    private NetworkTransform networkTransform;
    private GameManager gameManager;

    private Transform originalParent;
    private Transform ownerTransform;
    private Transform fireOrigin;

    private Vector3 startPosition;
    private Vector3 originalFireOriginLocalPos;
    private float floatTimer;

    // =========================================================
    //  UNITY / FUSION LIFECYCLE
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        networkTransform = GetComponent<NetworkTransform>();
        originalParent = transform.parent;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        fireOrigin = transform.Find("FireOrigin");
        if (fireOrigin != null)
            originalFireOriginLocalPos = fireOrigin.localPosition;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        startPosition = transform.position;
        gameManager = FindFirstObjectByType<GameManager>();

        if (Object.HasStateAuthority)
        {
            IsPickedUp = false;
            Owner = PlayerRef.None;
            CurrentAmmo = weaponData != null ? weaponData.maxAmmo : 0;
        }

        if (IsPickedUp && networkTransform != null)
        {
            networkTransform.enabled = false;
            if (rb != null) rb.simulated = false;
            if (col != null) col.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsPickedUp && ownerTransform != null)
            UpdateWeaponTransform();
    }

    public override void Render()
    {
        DetectAndPlayReloadSounds();
        ResolveOwnerReference();
        RenderHeldWeapon();
        RenderGroundWeapon();
        RenderSpriteFlip();
    }

    // =========================================================
    //  PUBLIC API — PICKUP / DROP
    // =========================================================

    public void ServerExecutePickup(PlayerRef player)
    {
        if (!Object.HasStateAuthority || IsPickedUp) return;
        if (!PickupCooldown.ExpiredOrNotRunning(Runner)) return;

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

        PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
        transform.position = pos;
        startPosition = pos;
        transform.SetParent(originalParent);

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = velocity;
        }
        if (col != null) col.enabled = true;

        if (networkTransform != null)
        {
            networkTransform.enabled = true;
            networkTransform.Teleport(pos, transform.rotation);
        }

        RPC_SyncDrop(pos, false);
    }

    // =========================================================
    //  PUBLIC ACCESSORS
    // =========================================================

    public void UpdateAimDirection(Vector2 aim) => AimDirection = aim;
    public bool GetIsPickedUp() => IsPickedUp;
    public WeaponData GetWeaponData() => weaponData;
    public int GetCurrentAmmo() => CurrentAmmo;
    public void SetCurrentAmmo(int ammo) => CurrentAmmo = ammo;

    public Vector3 GetWeaponHoldPosition() =>
        ownerTransform != null
            ? ownerTransform.position + Vector3.up * verticalOffset
            : transform.position;

    // =========================================================
    //  RELOAD SOUND TRIGGERS
    // =========================================================

    public void PlayReloadStartSound() { if (Object.HasStateAuthority) ReloadSoundTrigger = 1; }
    public void PlayReloadMidSound() { if (Object.HasStateAuthority) ReloadSoundTrigger = 2; }
    public void PlayReloadEndSound() { if (Object.HasStateAuthority) ReloadSoundTrigger = 3; }

    // =========================================================
    //  RPCS
    // =========================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncState(bool pickedUp)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        if (!pickedUp)
        {
            if (spriteRenderer != null) spriteRenderer.flipY = false;
            if (fireOrigin != null) fireOrigin.localPosition = originalFireOriginLocalPos;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncDrop(Vector3 dropPosition, bool pickedUp)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (spriteRenderer != null) spriteRenderer.flipY = false;
        if (fireOrigin != null) fireOrigin.localPosition = originalFireOriginLocalPos;

        if (!Object.HasStateAuthority)
        {
            transform.position = dropPosition;
            startPosition = dropPosition;
        }
    }

    // =========================================================
    //  PRIVATE — RENDER HELPERS
    // =========================================================

    private void DetectAndPlayReloadSounds()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ReloadSoundTrigger))
                PlayLocalSound(ReloadSoundTrigger);
        }
    }

    private void RenderHeldWeapon()
    {
        if (!IsPickedUp || ownerTransform == null) return;

        Vector2 displayAim = AimDirection;

        if (Owner == Runner.LocalPlayer && Camera.main != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2 localAim = ((Vector2)mouseWorld - (Vector2)GetWeaponHoldPosition()).normalized;
            if (localAim.magnitude > 0.1f) displayAim = localAim;
        }

        if (!Object.HasStateAuthority)
            UpdateWeaponTransform(displayAim);
    }

    private void RenderGroundWeapon()
    {
        if (IsPickedUp) return;

        floatTimer += Time.deltaTime * floatSpeed;
        transform.position = startPosition + Vector3.up * (Mathf.Sin(floatTimer) * floatAmount);
    }

    private void RenderSpriteFlip()
    {
        if (!IsPickedUp || !flipSpriteWhenAimingLeft || spriteRenderer == null) return;

        Vector2 flipAim = AimDirection;

        if (Owner == Runner.LocalPlayer && Camera.main != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector2 localAim = ((Vector2)mouseWorld - (Vector2)GetWeaponHoldPosition()).normalized;
            if (localAim.magnitude > 0.1f) flipAim = localAim;
        }

        bool shouldFlip = (flipAim.x < 0) ^ flipShouldFlip;
        spriteRenderer.flipY = shouldFlip;

        if (fireOrigin != null)
        {
            Vector3 fPos = fireOrigin.localPosition;
            fPos.y = shouldFlip ? -originalFireOriginLocalPos.y : originalFireOriginLocalPos.y;
            fireOrigin.localPosition = fPos;
        }
    }

    // =========================================================
    //  PRIVATE — TRANSFORM / REFERENCE HELPERS
    // =========================================================

    private void ResolveOwnerReference()
    {
        if (OwnerId != default && ownerTransform == null)
        {
            if (Runner.TryFindObject(OwnerId, out NetworkObject ownerObj))
                ownerTransform = ownerObj.transform;
        }
        else if (OwnerId == default)
        {
            ownerTransform = null;
        }
    }

    private void UpdateWeaponTransform() => UpdateWeaponTransform(AimDirection);
    private void UpdateWeaponTransform(Vector2 aimDirection)
    {
        if (aimDirection.magnitude < 0.1f) return;

        Vector3 target = ownerTransform.position + Vector3.up * verticalOffset;
        transform.position = target + (Vector3)(aimDirection.normalized * orbitRadius);

        if (rotateWithAim)
            transform.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg);
    }

    private void PlayLocalSound(int type)
    {
        if (audioSource == null) return;

        AudioClip clip = type switch
        {
            1 => reloadStartSound,
            2 => reloadMidSound,
            3 => reloadEndSound,
            _ => null
        };

        if (clip != null) audioSource.PlayOneShot(clip);
    }
}