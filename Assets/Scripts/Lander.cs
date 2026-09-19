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
        _landerRigidBody2D = GetComponent<Rigidbody2D> ();
    }

    private void FixedUpdate ()
    {
        
        
        if (Keyboard.current.upArrowKey.isPressed)
        {
            _landerRigidBody2D.AddForce (transform.up * (force * Time.fixedDeltaTime));
            Debug.Log ("Up");
        }
        
        if (Keyboard.current.leftArrowKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedLeft * Time.fixedDeltaTime);
            Debug.Log ("Left");
        }
        
        if (Keyboard.current.rightArrowKey.isPressed)
        {
            _landerRigidBody2D.AddTorque (turnSpeedRight * Time.fixedDeltaTime);
            Debug.Log ("Right");
        }
    }
}