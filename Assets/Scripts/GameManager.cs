using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance {  get; private set; }
    
    private int _score;
    private float _time;
    
    private void Awake ()
    {
        Instance = this;
    }

    private void Update ()
    {
        _time += Time.deltaTime;
    }

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

    public int GetScore ()
    {
        return _score;
    }
    
    public float GetTime ()
    {
        return _time;
    }
}
