using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ColorIdentifier : MonoBehaviour
{

    public Color color;
    public GameObject outlineOBJ;
    public bool beingUsed;
    public int ID;
    public Image PlayerPrev;
    public TMP_Text ColorNameLabel;


    private void Awake()
    {

        if (PlayerPrefs.GetInt("PlayerColor") == ID)
        {
            beingUsed = true;
            outlineOBJ.SetActive(true);
            PlayerPrev.color = color;

            ColorNameLabel.text = gameObject.name;
            ColorNameLabel.color = color;
        }
    }


}
