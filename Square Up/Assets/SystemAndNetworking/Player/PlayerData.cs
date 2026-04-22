using UnityEngine;
using Fusion;
using UnityEngine.UI;
using Unity.VisualScripting.Antlr3.Runtime;

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
        new Color(0.3647059f, 0.6901961f, 0.9803922f), //Classic Blue
        new Color(1f, 0.4f, 0.4f), //Pastel Red
        new Color(0.3019608f, 1f, 0.427367f), //Spring Green
        new Color(1f, 0.9822813f, 0.3915094f), //Sun Beam
        new Color(0.6973126f, 0.3443396f, 1f), //Melific Purple
        new Color(0.3820755f, 1f, 0.8688952f), //Ocean Teal
        new Color(0.3867925f, 0f, 0.02702537f), //Blood Moon
        new Color(0.7090764f, 1f, 0.4575472f), //Tennis Ball
        new Color(1f, 0.7550803f, 0.1462264f), //Heat Stroke
        new Color(0.1006461f, 0.06065325f, 0.2735849f), //Midnight
        new Color(1f, 0.1273585f, 0.5824191f), //Sexy
        new Color(0.1960784f, 0.1960784f, 0.1960784f), //Assasin
        new Color(0.1372549f, 0.4039216f, 0.06666667f), //Forest Dweller
        new Color(1f, 0.7019608f, 0.8784314f), //Baddie
        new Color(1f, 0.572549f, 0.345098f), //Coral Reef
        new Color(0.3098039f, 0.2078432f, 0.1960784f), //Clay
        new Color(0.6235294f, 0.2705882f, 0f), //Topaz
        new Color(0.1333333f, 0f, 0.4627451f), //Determination
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
        }

        if (Object.HasInputAuthority)
        {
            int colorIndex = PlayerPrefs.GetInt("PlayerColor");

            if (Object.HasStateAuthority)
            {
                // Host: set directly, no RPC needed
                PlayerColorIndex = colorIndex;
            }
            else
            {
                // Client: send to host to set authoritatively, Fusion syncs to everyone
                RPC_SetPlayerColor(colorIndex);
            }
        }

        TryAssignTagAndLayer();
        if (hitCollision != null) hitCollision.SetActive(!Object.HasInputAuthority);

        if (!Object.HasInputAuthority)
        {
            fovMaskObject.SetActive(false);
            healthUIParent.SetActive(false);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerColor(int colorIndex)
    {
        PlayerColorIndex = colorIndex;
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