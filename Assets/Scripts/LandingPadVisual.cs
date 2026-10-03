using System;
using TMPro;
using UnityEngine;

public class LandingPadVisual : MonoBehaviour
{
    [SerializeField] private TextMeshPro scoreMultiplierTextMeshPro;

    private void Awake ()
    {
        LandingPad landingPad = GetComponent <LandingPad> ();
        scoreMultiplierTextMeshPro.SetText ("x" + landingPad.GetScoreMultiplier ());
    }
}
