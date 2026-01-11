using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedLava : NetworkBehaviour
{
    [Header("Timing")]
    [SerializeField] private float minLiquidTime = 3f;
    [SerializeField] private float maxLiquidTime = 8f;
    [SerializeField] private float solidDuration = 1f;

    [Header("Damage")]
    [SerializeField] private float damagePerSecond = 20f;

    private Material lavaMaterial;
    private SpriteRenderer spriteRenderer;

    // Material property IDs (cached for performance)
    private static readonly int WaveSpeedID = Shader.PropertyToID("_WaveSpeed");
    private static readonly int SheenIntensityID = Shader.PropertyToID("_SheenIntensity");
    private static readonly int DistortionID = Shader.PropertyToID("_Distortion");
    private static readonly int BaseDarknessID = Shader.PropertyToID("_BaseDarkness");
    private static readonly int SheenColorID = Shader.PropertyToID("_SheenColor");

    // Liquid and solid material values
    private const float LIQUID_WAVE_SPEED = 2f;
    private const float LIQUID_SHEEN = 3.53f;
    private const float LIQUID_DISTORTION = 0.2f;

    private const float SOLID_WAVE_SPEED = 0f;
    private const float SOLID_SHEEN = 1f;
    private const float SOLID_DISTORTION = 0f;

    // HSV Color values (H, S, V, A all 0-1 range)
    private static readonly Vector4 LIQUID_BASE_HSV = new Vector4(34f / 360f, 1f, 1f, 1f);
    private static readonly Vector4 LIQUID_SHEEN_HSV = new Vector4(60f / 360f, 1f, 0.03f, 1f);

    private static readonly Vector4 SOLID_BASE_HSV = new Vector4(34f / 360f, 1f, 0.14f, 1f);
    private static readonly Vector4 SOLID_SHEEN_HSV = new Vector4(8f / 360f, 1f, 0.03f, 1f);

    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] private NetworkBool IsSolid { get; set; }

    private List<PlayerData> playersInLava = new List<PlayerData>();

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            lavaMaterial = spriteRenderer.material;
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsSolid = false;
            SetNextLiquidTimer();
        }

        UpdateMaterialState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // Check if timer expired
        if (StateTimer.Expired(Runner))
        {
            if (IsSolid)
            {
                // Transition back to liquid
                IsSolid = false;
                SetNextLiquidTimer();
            }
            else
            {
                // Transition to solid
                IsSolid = true;
                StateTimer = TickTimer.CreateFromSeconds(Runner, solidDuration);
            }
        }

        // Deal damage if liquid
        if (!IsSolid)
        {
            // Clean up null references
            playersInLava.RemoveAll(p => p == null);

            foreach (var player in playersInLava)
            {
                if (!player.Dead)
                {
                    float damage = damagePerSecond * Runner.DeltaTime;
                    player.TakeDamage((int)damage);
                }
            }
        }
    }

    public override void Render()
    {
        UpdateMaterialState();
    }

    private void SetNextLiquidTimer()
    {
        float duration = Random.Range(minLiquidTime, maxLiquidTime);
        StateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    private void UpdateMaterialState()
    {
        if (lavaMaterial == null) return;

        if (IsSolid)
        {
            // Solid state
            lavaMaterial.SetFloat(WaveSpeedID, SOLID_WAVE_SPEED);
            lavaMaterial.SetFloat(SheenIntensityID, SOLID_SHEEN);
            lavaMaterial.SetFloat(DistortionID, SOLID_DISTORTION);
            lavaMaterial.SetColor(BaseDarknessID, HSVToRGB(SOLID_BASE_HSV));
            lavaMaterial.SetColor(SheenColorID, HSVToRGB(SOLID_SHEEN_HSV));
        }
        else
        {
            // Liquid state
            lavaMaterial.SetFloat(WaveSpeedID, LIQUID_WAVE_SPEED);
            lavaMaterial.SetFloat(SheenIntensityID, LIQUID_SHEEN);
            lavaMaterial.SetFloat(DistortionID, LIQUID_DISTORTION);
            lavaMaterial.SetColor(BaseDarknessID, HSVToRGB(LIQUID_BASE_HSV));
            lavaMaterial.SetColor(SheenColorID, HSVToRGB(LIQUID_SHEEN_HSV));
        }
    }

    private Color HSVToRGB(Vector4 hsva)
    {
        Color rgb = Color.HSVToRGB(hsva.x, hsva.y, hsva.z);
        rgb.a = hsva.w;
        return rgb;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!Object.HasStateAuthority) return;

        PlayerData player = other.GetComponent<PlayerData>();
        if (player != null && !playersInLava.Contains(player))
        {
            playersInLava.Add(player);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!Object.HasStateAuthority) return;

        PlayerData player = other.GetComponent<PlayerData>();
        if (player != null)
        {
            playersInLava.Remove(player);
        }
    }

    private void OnDestroy()
    {
        playersInLava.Clear();
    }
}