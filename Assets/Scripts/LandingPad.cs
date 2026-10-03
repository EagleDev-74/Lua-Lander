using UnityEngine;

public class LandingPad : MonoBehaviour
{
    [SerializeField] int scoreMultiplier ;

    public int GetScoreMultiplier ()
    {
        return scoreMultiplier ;
    }
}
