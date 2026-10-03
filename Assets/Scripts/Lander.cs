using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Lander : MonoBehaviour
{
    public event EventHandler OnUpForce;
    public event EventHandler OnLeftForce;
    public event EventHandler OnRightForce;
    public event EventHandler OnBeforeForce;
    
    [SerializeField] private float force = 700f;
    [SerializeField] private float turnSpeedLeft = +100f;
    [SerializeField] private float turnSpeedRight = -100f;


    private Rigidbody2D _landerRigidBody2D;


    private void Awake ()
    {
        _landerRigidBody2D = GetComponent <Rigidbody2D> ();
    }

    private void FixedUpdate ()
    {
        OnBeforeForce?.Invoke(this, EventArgs.Empty);
        
        if (Keyboard.current.wKey.isPressed)
        {
            _landerRigidBody2D.AddForce (transform.up * (force * Time.fixedDeltaTime));
            OnUpForce?.Invoke(this, EventArgs.Empty);
        }

        if (Keyboard.current.aKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedLeft * Time.fixedDeltaTime);
            OnLeftForce?.Invoke(this, EventArgs.Empty);
        }

        if (Keyboard.current.dKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedRight * Time.fixedDeltaTime);
            OnRightForce?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCollisionEnter2D (Collision2D other)
    {
        if (!other.gameObject.TryGetComponent ( out LandingPad landingPad))
        {
            Debug.Log ("Crashed on the Terrain");
            return;
        }

        const float softLandingVelocityMagnitude = 4f;
        float relativeVelocityMagnitude = other.relativeVelocity.magnitude;
        if (other.relativeVelocity.magnitude > softLandingVelocityMagnitude)
        {
            Debug.Log (" Landing too hard");
            return;
        }

        float dotVector = Vector2.Dot (Vector2.up, transform.up);
        const float minDotVector = 0.90f;
        if (dotVector < minDotVector)
        {
            Debug.Log ("Landed on to steep angle");
            return;
        }

        Debug.Log (" Successful Landing");

        const float maxScoreAmountLandingAngle = 100;
        const float scoreDotVectorMultiplayer = 10f;
        float landingAngleScore = maxScoreAmountLandingAngle - Mathf.Abs (dotVector - 1f) * scoreDotVectorMultiplayer * maxScoreAmountLandingAngle;

        const float maxScoreAmountLandingSpeed = 100;
        float landingSpeedScore = (softLandingVelocityMagnitude - relativeVelocityMagnitude) * maxScoreAmountLandingSpeed;

        Debug.Log ("Landing Angle : ".Blue (bold:true) + landingAngleScore);
        Debug.Log ("Landing Speed : ".Blue (bold:true) + landingSpeedScore);

        int score = Mathf.RoundToInt(landingSpeedScore + landingAngleScore) * landingPad.GetScoreMultiplier ();
        Debug.Log ("Score : ".Red (bold:true) + score);
    }
}