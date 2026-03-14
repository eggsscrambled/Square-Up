using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ObsidianWaveController : MonoBehaviour
{
    [HideInInspector] public float waveSpeed = 0f;
    private float _accumulatedOffset;
    private Material _instancedMaterial;
    private static readonly int WaveOffsetID = Shader.PropertyToID("_WaveOffset");

    void Awake()
    {
        _instancedMaterial = GetComponent<Renderer>().material;
        _accumulatedOffset = Random.Range(0f, 1000f);
    }

    void Update()
    {
        // Smoothly adds to the offset. If speed is 0, it stays at the current value.
        _accumulatedOffset += Time.deltaTime * waveSpeed;
        _instancedMaterial.SetFloat(WaveOffsetID, _accumulatedOffset);
    }

    private void OnDestroy()
    {
        if (_instancedMaterial != null) Destroy(_instancedMaterial);
    }
}