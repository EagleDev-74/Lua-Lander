using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    
    private int _score;

    private void Start ()
    {
        Lander.Instance.OnCoinPickUp += Lander_OnCoinPickUp;
        Lander.Instance.OnLanded += Lander_OnLanded;
    }

    private void Lander_OnLanded (object sender,Lander.OnLandedEventArguments e)
    {
        AddScore (e.score);
    }
    private void Lander_OnCoinPickUp (object sender, System.EventArgs e)
    {
        AddScore (500);
    }
    

    public void AddScore (int addScoreAmount)
    {
        _score += addScoreAmount;
        Debug.Log (_score);
    }
}
