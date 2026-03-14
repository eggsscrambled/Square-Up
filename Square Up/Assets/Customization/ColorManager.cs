using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorManager : MonoBehaviour
{
    public TMP_Text ColorNameLabel;
    public Image PlayerPrev;
    public ColorIdentifier idenity;
    public GameObject[] Outlines;

    public void ColorClicked()
    {
        GameObject clicked = EventSystem.current.currentSelectedGameObject;
        string colorName = clicked.name;
        ColorNameLabel.text = colorName;

        idenity = clicked.GetComponent<ColorIdentifier>();
        Color color = idenity.color;

        PlayerPrev.color = color;
        ColorNameLabel.color = color;

        for (int i = 0; i < Outlines.Length; i++)
        {
            Outlines[i].SetActive(false);
        }

        idenity.outlineOBJ.SetActive(true);

        PlayerPrefs.SetInt("PlayerColor", idenity.ID);

        

    }
}
