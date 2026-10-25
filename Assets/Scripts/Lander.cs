using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = System.Object;

public class Lander : MonoBehaviour
{
    public static Lander Instance { get; private set; }
    
    public event EventHandler OnUpForce;
    public event EventHandler OnLeftForce;
    public event EventHandler OnRightForce;
    public event EventHandler OnBeforeForce;
    public event EventHandler <OnStateChangedEventArgs> OnStateChanged;

    public class OnStateChangedEventArgs : EventArgs
    {
        public State state;
    }
    
    public event EventHandler OnCoinPickUp;
    public event EventHandler <OnLandedEventArguments> OnLanded;

    public class OnLandedEventArguments : EventArgs
    {
        public LandingType landingType;
        public int score;
        public float landingSpeed;
        public float dotVector;
        public float scoreMultiplier;
    }

    public enum LandingType
    {
        Success,
        TooFastLanding,
        TooSteepAngle,
        WrongLandingArea,
    }

    public enum State
    {
        WaitingToStart,
        Normal,
        GameOver,
    }
    
    private const float GRAVITY_NORMAL = 0.7f;

    [SerializeField] private float force = 700f;
    [SerializeField] private float turnSpeedLeft = +100f;
    [SerializeField] private float turnSpeedRight = -100f;
    
    private float _fuelAmount;
    private float _fuelAmountMax = 10f;
    
    private State _state;
    
    private Rigidbody2D _landerRigidBody2D;

    private void Awake ()
    {
        Instance = this;
        _fuelAmount = _fuelAmountMax;
        _landerRigidBody2D = GetComponent <Rigidbody2D> ();
        _landerRigidBody2D.gravityScale = 0f;
        _state = State.WaitingToStart;
    }

    private void FixedUpdate ()
    {
        OnBeforeForce?.Invoke (this, EventArgs.Empty);

        switch (_state)
        {
            default:
            case State.WaitingToStart:
                if (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)
                {
                    // if any key is pressed
                    _landerRigidBody2D.gravityScale = GRAVITY_NORMAL;
                    _state = State.Normal;
                    SetActive (State.Normal);
                }
                break;
            
            case State.Normal:
                if (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)
                {
                    // if any key is pressed
                    FuelConsumption ();
                }

                if (_fuelAmount <= 0)
                {
                    // no fuel
                    return;
                }

                if (Keyboard.current.wKey.isPressed)
                {
                    _landerRigidBody2D.AddForce (transform.up * (force * Time.fixedDeltaTime));
                    OnUpForce?.Invoke (this, EventArgs.Empty);
                }

                if (Keyboard.current.aKey.isPressed)
                {
                    _landerRigidBody2D.AddTorque (turnSpeedLeft * Time.fixedDeltaTime);
                    OnLeftForce?.Invoke (this, EventArgs.Empty);
                }

                if (Keyboard.current.dKey.isPressed)
                {
                    _landerRigidBody2D.AddTorque (turnSpeedRight * Time.fixedDeltaTime);
                    OnRightForce?.Invoke (this, EventArgs.Empty);
                } 
                break;
            
            case State.GameOver:
                break;
        }
    }

    private void OnCollisionEnter2D (Collision2D other)
    {
        if (!other.gameObject.TryGetComponent (out LandingPad landingPad))
        {
            Debug.Log ("Crashed on the Terrain");
            OnLanded?.Invoke (this, new OnLandedEventArguments
            {
                landingType = LandingType.WrongLandingArea,
                landingSpeed = 0f,
                dotVector = 0f,
                scoreMultiplier = 0f,
                score = 0,
            });
            SetActive (State.GameOver);
            return;
        }

        const float softLandingVelocityMagnitude = 4f;
        float relativeVelocityMagnitude = other.relativeVelocity.magnitude;
        if (other.relativeVelocity.magnitude > softLandingVelocityMagnitude)
        {
            Debug.Log (" Landing too hard");
            OnLanded?.Invoke (this, new OnLandedEventArguments
            {
                landingType = LandingType.TooFastLanding,
                landingSpeed = relativeVelocityMagnitude,
                dotVector = 0f,
                scoreMultiplier = 0f,
                score = 0,
            });
            SetActive (State.GameOver);
            return;
        }

        float dotVector = Vector2.Dot (Vector2.up, transform.up);
        const float minDotVector = 0.90f;
        if (dotVector < minDotVector)
        {
            Debug.Log ("Landed on to steep angle");
            OnLanded?.Invoke (this, new OnLandedEventArguments
            {
                landingType = LandingType.TooSteepAngle,
                landingSpeed = relativeVelocityMagnitude,
                dotVector = dotVector,
                scoreMultiplier = 0f,
                score = 0,
            });
            SetActive (State.GameOver);
            return;
        }

        Debug.Log (" Successful Landing");

        const float maxScoreAmountLandingAngle = 100;
        const float scoreDotVectorMultiplayer = 10f;
        float landingAngleScore = maxScoreAmountLandingAngle - Mathf.Abs (dotVector - 1f) * scoreDotVectorMultiplayer * maxScoreAmountLandingAngle;

        const float maxScoreAmountLandingSpeed = 100;
        float landingSpeedScore = (softLandingVelocityMagnitude - relativeVelocityMagnitude) * maxScoreAmountLandingSpeed;

        Debug.Log ("Landing Angle : ".Blue (bold: true) + landingAngleScore);
        Debug.Log ("Landing Speed : ".Blue (bold: true) + landingSpeedScore);

        int score = Mathf.RoundToInt (landingSpeedScore + landingAngleScore) * landingPad.GetScoreMultiplier ();
        Debug.Log ("Score : ".Red (bold: true) + score);
        OnLanded?.Invoke (this, new OnLandedEventArguments
        {
            landingType = LandingType.Success,
            landingSpeed = relativeVelocityMagnitude,
            dotVector = dotVector,
            scoreMultiplier = landingPad.GetScoreMultiplier (),
            score = score,
        });
        SetActive (State.GameOver);
    }

    private void SetActive (State state)
    { 
        _state = state;
        OnStateChanged?.Invoke (this, new OnStateChangedEventArgs
        {
            state = _state,
        });
    }

    private void OnTriggerEnter2D (Collider2D other)
    {
        if (other.gameObject.TryGetComponent (out FuelPickUp fuelPickUp))
        {
            const float addFuelAmount = 10f;
            _fuelAmount += addFuelAmount;
            if (_fuelAmount > _fuelAmountMax)
            {
                _fuelAmount = _fuelAmountMax;
            }

            fuelPickUp.DestroySelf ();
        }

        if (other.gameObject.TryGetComponent (out CoinPickUp coinPickUp))
        {
            OnCoinPickUp?.Invoke (this, EventArgs.Empty);
            coinPickUp.DestroySelf ();
        }
    }

    private void FuelConsumption ()
    {
        float fuelConsumption = 1f;
        _fuelAmount -= fuelConsumption * Time.fixedDeltaTime;
    }

    public float GetFuelAmountNormalized ()
    {
        return _fuelAmount / _fuelAmountMax;
    }

    public float GetSpeedX ()
    {
        return _landerRigidBody2D.linearVelocityX;
    }

    public float GetSpeedY ()
    {
        return _landerRigidBody2D.linearVelocityY;
    }

    public float GetFuel ()
    {
        return _fuelAmount;
    }
}