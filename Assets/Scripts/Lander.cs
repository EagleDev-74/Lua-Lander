using UnityEngine;
using UnityEngine.InputSystem;

public class Lander : MonoBehaviour
{
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
        if (Keyboard.current.wKey.isPressed)
        {
            _landerRigidBody2D.AddForce (transform.up * (force * Time.fixedDeltaTime));
        }

        if (Keyboard.current.aKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedLeft * Time.fixedDeltaTime);
        }

        if (Keyboard.current.dKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedRight * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionEnter2D (Collision2D other)
    {
        if (!other.gameObject.GetComponent <LandingPad> ())
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

        Debug.Log ("Landing Angle : " + landingAngleScore);
        Debug.Log ("Landing Speed : " + landingSpeedScore);
    }
}