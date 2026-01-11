using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ObsidianWaveController))]
public class NetworkedLava : NetworkBehaviour
{
    [Header("Timing")]
    [SerializeField] private float minLiquidTime = 3f;
    [SerializeField] private float maxLiquidTime = 8f;
    [SerializeField] private float solidDuration = 1f;
    [SerializeField] private float transitionDuration = 1f;

    [Header("Damage")]
    [SerializeField] private float damagePerSecond = 20f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private Material _lavaMaterial;
    private Renderer _renderer;
    private ObsidianWaveController _waveController;
    private Collider2D _lavaCollider;

    // Shader property IDs
    private static readonly int SheenIntensityID = Shader.PropertyToID("_SheenIntensity");
    private static readonly int DistortionID = Shader.PropertyToID("_Distortion");
    private static readonly int BaseDarknessID = Shader.PropertyToID("_BaseDarkness");
    private static readonly int SheenColorID = Shader.PropertyToID("_SheenColor");

    // Constants
    private const float LIQUID_WAVE_SPEED = 2f;
    private const float LIQUID_SHEEN = 3.53f;
    private const float LIQUID_DISTORTION = 0.2f;
    private const float SOLID_WAVE_SPEED = 0f;
    private const float SOLID_SHEEN = 1f;
    private const float SOLID_DISTORTION = 0f;

    private static readonly Vector4 LIQUID_BASE_HSV = new Vector4(34f / 360f, 1f, 1f, 1f);
    private static readonly Vector4 LIQUID_SHEEN_HSV = new Vector4(60f / 360f, 1f, 0.03f, 1f);
    private static readonly Vector4 SOLID_BASE_HSV = new Vector4(34f / 360f, 1f, 0.14f, 1f);
    private static readonly Vector4 SOLID_SHEEN_HSV = new Vector4(8f / 360f, 1f, 0.03f, 1f);

    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] private NetworkBool IsSolid { get; set; }
    [Networked] private float TransitionStartTime { get; set; }

    // Track players currently in lava
    private HashSet<PlayerData> _playersInLava = new HashSet<PlayerData>();

    // Accumulate fractional damage per player
    private Dictionary<PlayerData, float> _accumulatedDamage = new Dictionary<PlayerData, float>();

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _waveController = GetComponent<ObsidianWaveController>();
        _lavaCollider = GetComponent<Collider2D>();

        if (_renderer != null)
        {
            _lavaMaterial = _renderer.material;
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            IsSolid = false;
            TransitionStartTime = (float)Runner.SimulationTime;
            SetNextLiquidTimer();

            if (enableDebugLogs)
            {
                Debug.Log($"[Lava] Spawned with StateAuthority on {gameObject.name}");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[Lava] Spawned WITHOUT StateAuthority on {gameObject.name}");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (StateTimer.Expired(Runner))
        {
            IsSolid = !IsSolid;
            TransitionStartTime = (float)Runner.SimulationTime;

            if (IsSolid)
            {
                StateTimer = TickTimer.CreateFromSeconds(Runner, solidDuration);
                // Clear players from damage tracking when solidifying
                _playersInLava.Clear();
                _accumulatedDamage.Clear();
            }
            else
            {
                SetNextLiquidTimer();
                // Re-check for players already in the trigger when becoming liquid
                RecheckPlayersInTrigger();
            }
        }

        // Apply damage to players in lava (only when liquid and after transition)
        if (!IsSolid)
        {
            float timeSinceLiquid = (float)Runner.SimulationTime - TransitionStartTime;
            if (timeSinceLiquid >= transitionDuration)
            {
                ApplyDamageToPlayersInLava();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Lava] OnTriggerEnter2D - Object: {collision.name}, Tag: {collision.tag}, HasStateAuthority: {HasStateAuthority}");
        }

        if (!HasStateAuthority) return;

        if (collision.CompareTag("Player"))
        {
            PlayerData player = collision.GetComponent<PlayerData>();
            if (player != null)
            {
                _playersInLava.Add(player);
                // Initialize accumulated damage for this player
                if (!_accumulatedDamage.ContainsKey(player))
                {
                    _accumulatedDamage[player] = 0f;
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"[Lava] Added player to lava: {player.name}, Total in lava: {_playersInLava.Count}");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"[Lava] Player tagged object has no PlayerData component: {collision.name}");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Lava] OnTriggerExit2D - Object: {collision.name}");
        }

        if (!HasStateAuthority) return;

        if (collision.CompareTag("Player"))
        {
            PlayerData player = collision.GetComponent<PlayerData>();
            if (player != null)
            {
                _playersInLava.Remove(player);
                _accumulatedDamage.Remove(player);

                if (enableDebugLogs)
                {
                    Debug.Log($"[Lava] Removed player from lava: {player.name}, Total in lava: {_playersInLava.Count}");
                }
            }
        }
    }

    private void ApplyDamageToPlayersInLava()
    {
        if (enableDebugLogs && _playersInLava.Count > 0)
        {
            Debug.Log($"[Lava] ApplyDamageToPlayersInLava - Players in lava: {_playersInLava.Count}, IsSolid: {IsSolid}, Damage per tick: {damagePerSecond * Runner.DeltaTime}");
        }

        List<PlayerData> playersToRemove = new List<PlayerData>();

        foreach (var player in _playersInLava)
        {
            if (player == null || player.Dead)
            {
                playersToRemove.Add(player);
                if (enableDebugLogs)
                {
                    Debug.Log($"[Lava] Player is null or dead, removing from tracking");
                }
                continue;
            }

            // Accumulate fractional damage
            float damageThisTick = damagePerSecond * Runner.DeltaTime;
            _accumulatedDamage[player] += damageThisTick;

            // Only apply damage when we've accumulated at least 1 point
            if (_accumulatedDamage[player] >= 1f)
            {
                int damageToApply = Mathf.FloorToInt(_accumulatedDamage[player]);
                _accumulatedDamage[player] -= damageToApply;

                player.TakeDamage(damageToApply);

                if (enableDebugLogs)
                {
                    Debug.Log($"[Lava] Dealt {damageToApply} damage to {player.name} (accumulated: {_accumulatedDamage[player]})");
                }
            }
        }

        // Clean up null or dead players
        foreach (var player in playersToRemove)
        {
            _playersInLava.Remove(player);
            _accumulatedDamage.Remove(player);
        }
    }

    private void RecheckPlayersInTrigger()
    {
        if (_lavaCollider == null) return;

        // Get all colliders currently overlapping with this trigger
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(Physics2D.AllLayers);

        Collider2D[] results = new Collider2D[10];
        int count = _lavaCollider.Overlap(filter, results);

        if (enableDebugLogs)
        {
            Debug.Log($"[Lava] RecheckPlayersInTrigger - Found {count} overlapping colliders");
        }

        for (int i = 0; i < count; i++)
        {
            if (results[i].CompareTag("Player"))
            {
                PlayerData player = results[i].GetComponent<PlayerData>();
                if (player != null && !_playersInLava.Contains(player))
                {
                    _playersInLava.Add(player);
                    if (!_accumulatedDamage.ContainsKey(player))
                    {
                        _accumulatedDamage[player] = 0f;
                    }

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[Lava] Re-added player already in trigger: {player.name}");
                    }
                }
            }
        }
    }

    public override void Render()
    {
        if (_lavaMaterial == null || Runner == null || _waveController == null) return;

        float timeSinceTransition = (float)Runner.SimulationTime - TransitionStartTime;
        float t = Mathf.Clamp01(timeSinceTransition / transitionDuration);

        float startSpeed = IsSolid ? LIQUID_WAVE_SPEED : SOLID_WAVE_SPEED;
        float endSpeed = IsSolid ? SOLID_WAVE_SPEED : LIQUID_WAVE_SPEED;
        _waveController.waveSpeed = Mathf.Lerp(startSpeed, endSpeed, t);

        _lavaMaterial.SetFloat(SheenIntensityID, Mathf.Lerp(IsSolid ? LIQUID_SHEEN : SOLID_SHEEN, IsSolid ? SOLID_SHEEN : LIQUID_SHEEN, t));
        _lavaMaterial.SetFloat(DistortionID, Mathf.Lerp(IsSolid ? LIQUID_DISTORTION : SOLID_DISTORTION, IsSolid ? SOLID_DISTORTION : LIQUID_DISTORTION, t));

        Color startBase = HSVToRGB(IsSolid ? LIQUID_BASE_HSV : SOLID_BASE_HSV);
        Color endBase = HSVToRGB(IsSolid ? SOLID_BASE_HSV : LIQUID_BASE_HSV);
        _lavaMaterial.SetColor(BaseDarknessID, Color.Lerp(startBase, endBase, t));

        Color startSheen = HSVToRGB(IsSolid ? LIQUID_SHEEN_HSV : SOLID_SHEEN_HSV);
        Color endSheen = HSVToRGB(IsSolid ? SOLID_SHEEN_HSV : LIQUID_SHEEN_HSV);
        _lavaMaterial.SetColor(SheenColorID, Color.Lerp(startSheen, endSheen, t));
    }

    private void SetNextLiquidTimer()
    {
        StateTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(minLiquidTime, maxLiquidTime));
    }

    private Color HSVToRGB(Vector4 hsva)
    {
        Color rgb = Color.HSVToRGB(hsva.x, hsva.y, hsva.z);
        rgb.a = hsva.w;
        return rgb;
    }
}