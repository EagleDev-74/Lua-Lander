using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LandedUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleTextMesh;
    [SerializeField] private TextMeshProUGUI stateTextMesh;
    [SerializeField] private Button nextButton;

    private void Awake ()
    {
        nextButton.onClick.AddListener (() =>
        {
            SceneManager.LoadScene (0);
        });
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
            titleTextMesh.SetText ("CRASHED!".Red (prefix: false));
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