using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class PlayerData : NetworkBehaviour
{
    [Networked] public float Health { get; set; }
    [Networked] public NetworkBool Dead { get; set; }
    [Networked] public int PlayerColorIndex { get; set; }
    [Networked] public int WeaponIndex { get; set; }

    // --- NEW HEALING STATE ---
    [Networked] public NetworkBool IsHealing { get; set; }
    [Networked] public TickTimer HealWindupTimer { get; set; } // The delay timer

    public GameObject fovMaskObject;
    public GameObject healthUIParent;

    [Header("Healing Settings")]
    [SerializeField] private float healthPerSecond = 15f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float healWindupTime = 0.5f; // Time to hold Q before healing starts

    public Image healthUI;
    private SpriteRenderer sprite;
    private bool tagAndLayerAssigned = false;
    private GameObject hitCollision;

    private static readonly Color[] availableColors = new Color[]
    {
        new Color(1f, 0.459f, 0.459f), //red
        new Color(1f, 0.671f, 0.478f), //orange
        new Color(1f, 0.984f, 0.478f), //yellow
        new Color(0.867f, 1f, 0.478f), //tennis ball
        new Color(0.643f, 1f, 0.478f), //lime
        new Color(0.439f, 1f, 0.478f), //green
        new Color(0.439f, 1f, 0.804f), //cyan
        new Color(0.439f, 0.988f, 1f), //light blue
        new Color(0.439f, 0.733f, 1f), //blue
        new Color(0.439f, 0.525f, 1f), //dark blue
        new Color(0.486f, 0.439f, 1f), //purple
        new Color(0.655f, 0.439f, 1f), //indigo
        new Color(0.808f, 0.439f, 1f), //pink
        new Color(0.969f, 0.439f, 1f), //sexy pink
        new Color(1f, 0.439f, 0.745f), //barbie pink
    };

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        hitCollision = transform.Find("HitCollision")?.gameObject;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = maxHealth;
            Dead = false;
            WeaponIndex = 0;
            PlayerColorIndex = Random.Range(0, availableColors.Length);
        }
        TryAssignTagAndLayer();
        if (hitCollision != null) hitCollision.SetActive(!Object.HasInputAuthority);

        if (!Object.HasInputAuthority)
        {
            fovMaskObject.SetActive(false);
            healthUIParent.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!tagAndLayerAssigned) TryAssignTagAndLayer();

        // Force stop if full health
        if (Health >= maxHealth) IsHealing = false;

        if (IsHealing && !Dead && Health < maxHealth)
        {
            // Only increase health if the wind-up timer has finished
            if (HealWindupTimer.Expired(Runner))
            {
                Health = Mathf.MoveTowards(Health, maxHealth, healthPerSecond * Runner.DeltaTime);
            }
        }
    }

    public override void Render()
    {
        ApplyColor();
        if (healthUI != null) healthUI.fillAmount = (Health / 100f);

        if (IsHealing && sprite != null)
        {
            // If still in wind-up, do a very slow pulse (Gray)
            if (!HealWindupTimer.Expired(Runner))
            {
                // Changed from * 2 to * 0.66f (approx 0.33x speed)
                sprite.color = Color.Lerp(GetActualColor(), Color.gray, Mathf.PingPong(Time.time, 0.5f));
            }
            else // Active healing: medium pulse (White)
            {
                // Changed from * 8 to * 4f (0.5x speed)
                sprite.color = Color.Lerp(GetActualColor(), Color.white, Mathf.PingPong(Time.time * 4f, 0.7f));
            }
        }
    }

    private void TryAssignTagAndLayer()
    {
        if (!tagAndLayerAssigned && Object.HasInputAuthority)
        {
            gameObject.layer = LayerMask.NameToLayer("Player");
            gameObject.tag = "Player";
            tagAndLayerAssigned = true;
        }
    }

    public void ClearWeapons()
    {
        if (!Object.HasStateAuthority) return;

        // Drop current weapon if any
        if (WeaponIndex > 0)
        {
            WeaponData weaponData = GameManager.Instance.GetWeaponData(WeaponIndex);
            if (weaponData != null)
            {
                GameManager.Instance.SpawnDroppedWeapon(weaponData, transform.position);
            }
        }

        // Clear the weapon
        WeaponIndex = 0;
    }
    private void ApplyColor()
    {
        if (sprite != null && PlayerColorIndex >= 0 && PlayerColorIndex < availableColors.Length)
            sprite.color = availableColors[PlayerColorIndex];
    }

    public Color GetActualColor()
    {
        if (PlayerColorIndex >= 0 && PlayerColorIndex < availableColors.Length)
            return availableColors[PlayerColorIndex];
        return Color.white;
    }

    public void TakeDamage(int damage)
    {
        if (Object.HasStateAuthority)
        {
            Health -= damage;
            if (Health <= 0 && !Dead)
            {
                Health = 0;
                Dead = true;
                OnDeath();
            }
        }
    }

    private void OnDeath() { WeaponIndex = 0; IsHealing = false; }

    public void Respawn()
    {
        if (Object.HasStateAuthority)
        {
            Health = maxHealth;
            Dead = false;
            WeaponIndex = 0;
        }
    }

    public void PickupWeapon(int weaponIndex) { if (Object.HasStateAuthority) WeaponIndex = weaponIndex; }
    public void DropWeapon() { if (Object.HasStateAuthority) WeaponIndex = 0; }
    public bool HasWeapon() => WeaponIndex > 0;
}