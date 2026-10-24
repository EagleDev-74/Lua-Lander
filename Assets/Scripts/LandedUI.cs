using System;
using TMPro;
using UnityEngine;

public class LandedUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleTextMesh;
    [SerializeField] private TextMeshProUGUI stateTextMesh;

    private void Awake ()
    {
        Log.UsePrefix = false;
    }

    private void Start ()
    {
        Lander.Instance.OnLanded += Lander_OnLanded;
        Hide ();
    }

    private void Lander_OnLanded (object sender, Lander.OnLandedEventArguments e)
    {
        if (e.landingType == Lander.LandingType.Success)
        {
            titleTextMesh.SetText ("SUCCESSFUL LANDING!");
        }
        else
        {
            titleTextMesh.SetText ("CRASHED!".Red ());
        }

        stateTextMesh.SetText ($"{Mathf.Round (e.landingSpeed)}\n" +
                               $" {Mathf.Round (e.dotVector)}\n" +
                               $"x{e.scoreMultiplier}\n" +
                               $"{e.score}");
        Show ();
    }

    private void Show ()
    {
        gameObject.SetActive (true);
    }

    private void Hide ()
    {
        gameObject.SetActive (false);
    }
    
}