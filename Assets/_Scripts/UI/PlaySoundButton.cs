using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PlaySoundButton : MonoBehaviour
{
    public AudioClip sound;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlaySound);
    }


    public void PlaySound()
    {
        UISound.Instance.PlayUISound(sound);
    }
}

