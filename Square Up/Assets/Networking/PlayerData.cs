using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class PlayerData : NetworkBehaviour
{
    [Networked]
    public float Health { get; set; }
    [Networked]
    public NetworkBool Dead { get; set; }
    [Networked]
    public int PlayerColorIndex { get; set; }
    [Networked]
    public int WeaponIndex { get; set; }

    public Image healthUI;

    private SpriteRenderer sprite;
    private bool tagAndLayerAssigned = false;
    private GameObject hitCollision;

    private static readonly Color[] availableColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        new Color(1f, 0.4f, 0.7f), // Pink
        new Color(0.5f, 0f, 0.5f), // Purple
        new Color(1f, 0.5f, 0f),   // Orange
        Color.cyan,
        Color.black,
        Color.white,
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
            Health = 100;
            Dead = false;
            WeaponIndex = 0;
            PlayerColorIndex = Random.Range(0, availableColors.Length);
        }

        TryAssignTagAndLayer();

        if (hitCollision != null)
        {
            hitCollision.SetActive(!Object.HasInputAuthority);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!tagAndLayerAssigned) TryAssignTagAndLayer();
    }

    public override void Render()
    {
        ApplyColor();
        if (healthUI != null)
        {
            healthUI.fillAmount = (Health / 100f);
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

    private void ApplyColor()
    {
        if (sprite != null && PlayerColorIndex >= 0 && PlayerColorIndex < availableColors.Length)
        {
            sprite.color = availableColors[PlayerColorIndex];
        }
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

    private void OnDeath()
    {
        WeaponIndex = 0;
    }

    public void Respawn()
    {
        if (Object.HasStateAuthority)
        {
            Health = 100;
            Dead = false;
            WeaponIndex = 0;
        }
    }

    public void PickupWeapon(int weaponIndex)
    {
        if (Object.HasStateAuthority) WeaponIndex = weaponIndex;
    }

    public void DropWeapon()
    {
        if (Object.HasStateAuthority) WeaponIndex = 0;
    }

    // --- Critical Method: Do Not Remove ---
    public bool HasWeapon()
    {
        return WeaponIndex > 0;
    }
}