using System.Collections;
using UnityEngine;

public class ButtonAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip pressSound;

    [Header("Scale Settings")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float scaleSpeed = 8f;

    private Vector3 targetScale;
    private bool isHovering;

    private void Start()
    {
        transform.localScale = Vector3.one * normalScale;
        targetScale = transform.localScale;
    }

    private void Update()
    {
        // Smoothly interpolate to target scale
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            scaleSpeed * Time.deltaTime
        );
    }

    public void OnHover()
    {
        isHovering = true;
        targetScale = Vector3.one * hoverScale;

        if (audioSource && hoverSound)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnHoverExit()
    {
        isHovering = false;
        targetScale = Vector3.one * normalScale;
    }

    public void OnPressed()
    {
        StartCoroutine(PressAnimation());

        if (audioSource && pressSound)
        {
            audioSource.PlayOneShot(pressSound);
        }
    }

    private IEnumerator PressAnimation()
    {
        // Quick press down
        targetScale = Vector3.one * pressScale;
        yield return new WaitForSeconds(0.1f);

        // Return to normal or hover state
        targetScale = Vector3.one * (isHovering ? hoverScale : normalScale);
    }
}