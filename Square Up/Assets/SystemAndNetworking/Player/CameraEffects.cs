using UnityEngine;

public class CameraEffects : MonoBehaviour
{
    public static CameraEffects Instance { get; private set; }

    [Header("Movement Bounds")]
    [SerializeField] private float maxHorizontalMove = 5f;
    [SerializeField] private float maxVerticalMove = 3f;

    [Header("Player Movement Influence")]
    [SerializeField] private float velocitySwayStrength = 0.05f;
    [SerializeField] private float swaySmoothing = 5f;
    [SerializeField] private float returnToBaseSpeed = 3f;

    private Camera cam;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 currentSway;
    private Vector3 playerVelocity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        cam = GetComponent<Camera>();
        basePosition = transform.position;
        baseRotation = transform.rotation;
    }

    public void UpdatePlayerData(Vector3 position, Vector3 velocity)
    {
        playerVelocity = velocity;
    }

    private void LateUpdate()
    {
        ApplyCameraEffects();
    }

    private void ApplyCameraEffects()
    {
        // Calculate subtle sway based on velocity only
        Vector3 velocitySway = new Vector3(
            Mathf.Sin(Time.time * 10f) * playerVelocity.magnitude * velocitySwayStrength,
            Mathf.Cos(Time.time * 12f) * playerVelocity.magnitude * velocitySwayStrength,
            0f
        );

        // Smoothly interpolate towards velocity sway
        currentSway = Vector3.Lerp(currentSway, velocitySway, Time.deltaTime * swaySmoothing);

        // Clamp sway within bounds
        currentSway.x = Mathf.Clamp(currentSway.x, -maxHorizontalMove, maxHorizontalMove);
        currentSway.y = Mathf.Clamp(currentSway.y, -maxVerticalMove, maxVerticalMove);

        // Always return towards base position (0,0)
        Vector3 targetPosition = basePosition + currentSway;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * returnToBaseSpeed);
        transform.rotation = baseRotation;
    }

    public void ResetCamera()
    {
        transform.position = basePosition;
        transform.rotation = baseRotation;
        currentSway = Vector3.zero;
    }

    public void SetBasePosition(Vector3 newBase)
    {
        basePosition = newBase;
    }

    public void AddScreenShake(float intensity, float duration)
    {
        StartCoroutine(ScreenShakeCoroutine(intensity, duration));
    }

    private System.Collections.IEnumerator ScreenShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float currentIntensity = intensity * (1f - progress); // Decay over time

            Vector3 shake = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0f
            ) * currentIntensity;

            transform.position = basePosition + currentSway + shake;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}