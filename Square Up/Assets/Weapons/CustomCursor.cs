using UnityEngine;
using UnityEngine.UI;

public class CustomCursor : MonoBehaviour
{
    public RectTransform cursorImage;

    void Start()
    {
        // Hide the default cursor
        Cursor.visible = false;
    }

    void Update()
    {
        // Move the UI cursor to the mouse position
        cursorImage.position = Input.mousePosition;
    }
}