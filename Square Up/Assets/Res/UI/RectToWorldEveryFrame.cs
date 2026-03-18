using UnityEngine;

public class RectToWorldEveryFrame : MonoBehaviour
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Camera uiCamera; // assign if Screen Space - Camera, leave null for Overlay

    void Update()
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, targetRect.position);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        particles.transform.position = worldPos;
    }
}
