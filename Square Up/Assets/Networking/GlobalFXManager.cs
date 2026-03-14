using UnityEngine;
using Fusion;

public class GlobalFXManager : NetworkBehaviour
{
    public static GlobalFXManager Instance { get; private set; }

    [Header("Hit Particle Prefabs")]
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private GameObject environmentHitPrefab;
    [SerializeField] private GameObject fizzlePrefab;

    [Header("Defaults")]
    [SerializeField] private GameObject defaultMuzzleFlashPrefab;
    [SerializeField] private float fallbackMuzzleLife = 0.5f;

    public override void Spawned()
    {
        if (Instance == null) Instance = this;
    }

    // --- HIT EFFECTS ---
    public void RequestHitEffect(Vector3 pos, float angle, string type, Color col)
    {
        if (Object.HasStateAuthority)
            RPC_PlayHitEffect(pos, angle, type, col);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHitEffect(Vector3 pos, float angle, string type, Color col)
    {
        GameObject prefab = type switch
        {
            "blood" => bloodPrefab,
            "environment" => environmentHitPrefab,
            _ => fizzlePrefab
        };

        if (prefab != null)
        {
            GameObject inst = Instantiate(prefab, pos, Quaternion.Euler(0, 0, -angle));
            if (type == "blood")
            {
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>())
                {
                    var m = ps.main; m.startColor = col;
                }
            }
            // Blood/Environment hits usually don't have long sounds, 1.5s is usually safe
            Destroy(inst, 5f);
        }
    }

    // --- DYNAMIC MUZZLE FLASHES (SFX FRIENDLY) ---
    public void RequestMuzzleFlash(Vector3 pos, Quaternion rot, int weaponIndex)
    {
        if (Object.HasStateAuthority)
            RPC_PlayMuzzleFlash(pos, rot, weaponIndex);
    }

    /// <summary>Play muzzle flash locally without RPC. Use for instant client feedback when the local player shoots.</summary>
    public void PlayMuzzleFlashLocal(Vector3 pos, Quaternion rot, int weaponIndex)
    {
        SpawnMuzzleFlashInstance(pos, rot, weaponIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayMuzzleFlash(Vector3 pos, Quaternion rot, int weaponIndex)
    {
        SpawnMuzzleFlashInstance(pos, rot, weaponIndex);
    }

    private void SpawnMuzzleFlashInstance(Vector3 pos, Quaternion rot, int weaponIndex)
    {
        GameObject flashPrefab = null;

        if (GameManager.Instance != null)
        {
            var data = GameManager.Instance.GetWeaponData(weaponIndex + 1);
            if (data != null) flashPrefab = data.muzzleFlashPrefab;
        }

        if (flashPrefab == null) flashPrefab = defaultMuzzleFlashPrefab;

        if (flashPrefab != null)
        {
            GameObject flash = Instantiate(flashPrefab, pos, rot);

            float lifetime = fallbackMuzzleLife;
            AudioSource audio = flash.GetComponentInChildren<AudioSource>();
            if (audio != null && audio.clip != null)
                lifetime = audio.clip.length;

            Destroy(flash, lifetime);
        }
    }
}