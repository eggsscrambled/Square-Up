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

    // Liquid and solid material values
    private const float LIQUID_WAVE_SPEED = 2f;
    private const float LIQUID_SHEEN = 3.53f;
    private const float LIQUID_DISTORTION = 0.2f;

    private const float SOLID_WAVE_SPEED = 0f;
    private const float SOLID_SHEEN = 1f;
    private const float SOLID_DISTORTION = 0f;

    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] private NetworkBool IsSolid { get; set; }

    private HashSet<PlayerData> playersInLava = new HashSet<PlayerData>();

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
            foreach (var player in playersInLava)
            {
                if (player != null && !player.Dead)
                {
                    player.TakeDamage((int)(damagePerSecond * Runner.DeltaTime));
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
        }
        else
        {
            // Liquid state
            lavaMaterial.SetFloat(WaveSpeedID, LIQUID_WAVE_SPEED);
            lavaMaterial.SetFloat(SheenIntensityID, LIQUID_SHEEN);
            lavaMaterial.SetFloat(DistortionID, LIQUID_DISTORTION);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerData player = other.GetComponent<PlayerData>();
        if (player != null)
        {
            playersInLava.Add(player);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
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