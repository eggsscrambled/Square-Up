using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ButtonAnimator : MonoBehaviour
{

    public GameObject button;
    public bool InButton;
    private AudioSource audioSource;
    public AudioClip Hover;
    public AudioClip Press;


    [Header("Size Change")]
    public float SizeChangeSpeed;
    public float targetSize;
    public float baseSize;
    public float ClcikSize;



    private void Start()
    {
        audioSource = GameObject.Find("Main Camera").GetComponent<AudioSource>();
    }
    public void OnHover()
    {

            InButton = true;
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(Hover);


    }

    public void OnHoverExit()
    {
            InButton = false;
    }


    private void Update()
    {


        
            if (InButton)
            {
                if (button.transform.localScale.x < targetSize && button.transform.localScale.y < targetSize)
                {
                     button.transform.localScale += new Vector3(SizeChangeSpeed, SizeChangeSpeed, 0) * Time.deltaTime;
                }




            }

            if (!InButton)
            {
                if (button.transform.localScale.x > baseSize && button.transform.localScale.y > baseSize)
                {
                     button.transform.localScale -= new Vector3(SizeChangeSpeed, SizeChangeSpeed, 0) * Time.deltaTime;
                }




            }

        


    }

    public void pressed()
    {
        InButton = false ;
        button.transform.localScale = new Vector3(baseSize, baseSize, baseSize);
        audioSource.PlayOneShot(Press);
    }
}
