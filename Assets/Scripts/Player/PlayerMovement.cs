using System;
using Config;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerMovement : MonoBehaviour
{
    // Assignable
    public Transform playerCam;
    public Transform orientation;
    
    // Other
    private Rigidbody _rb;
    private PlayerConfig _config;
    private PlayerControls _controls;
    public Canvas canvas;
    
    // Rotation and look
    private float _xRotation;
    private float _sensitivity = 50f;
    private float _sensMultiplier;
    private bool _lookEnabled = true;
    
    // Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;
    
    public float counterMovement = 0.175f;
    private float _threshold = 0.01f;
    public float maxSlopeAngle = 35f;
    
    // Crouch & Slide
    private Vector3 _crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 _playerScale;
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;
    
    // Jumping
    private bool _readyToJump = true;
    private float _jumpCooldown = 0.25f;
    public float jumpForce = 550f;
    
    // Input
    private float _x, _y;
    private bool _jumping, _sprinting, _crouching;

    // Sliding (Currently unused)
    private Vector3 _normalVector = Vector3.up;
    private Vector3 _wallNormalVector;

    private void Awake()
    {
        // Get required components
        _config = GetComponent<PlayerConfig>();
        _rb = GetComponent<Rigidbody>();
        _controls = new PlayerControls();
    }
    
    private void OnEnable()
    {
        // Enable controls
        _controls.Enable();
        
        _toggleCursor = _controls.Player.ShowCursor;
        _toggleCursor.Enable();

        _toggleUI = _controls.Player.ShowUI;
        _toggleUI.Enable();
        
        // Assign actions
        // ReSharper disable All
        _toggleCursor.performed += ActionCursorLock;  // Not exactly sure why Intellij is detecting this as an error
        _toggleUI.performed += ActionShowUI;
        // ReSharper restore All
    }
    
    private void OnDisable()
    {
        _controls.Disable();
    }

    private void Start()
    {
        // Set initial sensitivity multiplier
        _sensMultiplier = _config.mouseSensitivity;
        
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // ----------------------------------------       Input Manager Boilerplate        ----------------------------------------
    // Create actions
    private InputAction _toggleCursor;
    private InputAction _toggleUI;
        
    // Create input variables
    private Vector2 _inputLook;
    private Vector2 _inputMove;
    private bool _inputKeyRun = false; // is key Held
    private bool _inputKeySprint = false;
    private bool _inputKeyCrouch = false; // is key Held
    private bool _inputKeyJump = false; // is key Pressed
    
    private void ReadInputs()
    {
        
    }
    // ----------------------------------------     END Input Manager Boilerplate      ----------------------------------------
    // ------------------------------------------------------------------------------------------------------------------------
    
    private void ActionCursorLock(InputAction.CallbackContext context)
    {
        Debug.Log("Toggle cursor lock");
        DoCursorLock();
        _lookEnabled = !_lookEnabled;
    }

    private void ActionShowUI(InputAction.CallbackContext context)
    {
        Debug.Log("Show UI");
        canvas.enabled = !canvas.enabled;
    }
    
    private static void DoCursorLock(bool? doLock = null)
    {
        switch (doLock)
        {
            case null when Cursor.lockState == CursorLockMode.Locked:  // When doLock is null and the cursor is locked, unlock it
                Cursor.lockState = CursorLockMode.None;
                return;
            case null:  // When doLock is null, the previous check has failed we know that the cursor is unlocked
                Cursor.lockState = CursorLockMode.Locked;
                return;
            case true:
                Cursor.lockState = CursorLockMode.Locked;
                return;
            default:
                Cursor.lockState = CursorLockMode.None;
                break;
        }
    }
    
    private void StartCrouch() {
        // Cache player transform position
        Vector3 playerPosition = transform.position;
        
        transform.localScale = _crouchScale;
        transform.position = new Vector3(playerPosition.x, playerPosition.y - 0.5f, playerPosition.z);
        if (!(_rb.velocity.magnitude > 0.5f)) return;
        if (grounded) {
            _rb.AddForce(orientation.transform.forward * slideForce);
        }
    }

    private void StopCrouch() {
        // Cache player transform position
        Vector3 playerPosition = transform.position;
        transform.localScale = _playerScale;
        transform.position = new Vector3(playerPosition.x, playerPosition.y + 0.5f, playerPosition.z);
    }
    
    private void Movement() {
        //Extra gravity
        _rb.AddForce(Vector3.down * Time.deltaTime * 10);
        
        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement((_x), _y, mag);
        
        //If holding jump && ready to jump, then jump
        if (_readyToJump && _jumping) Jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;
        
        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (_crouching && grounded && _readyToJump) {
            _rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }
        
        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (_x > 0 && xMag > maxSpeed) _x = 0;
        if (_x < 0 && xMag < -maxSpeed) _x = 0;
        if (_y > 0 && yMag > maxSpeed) _y = 0;
        if (_y < 0 && yMag < -maxSpeed) _y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        switch (grounded)
        {
            // Movement in air
            case false:
                multiplier = 0.5f;
                multiplierV = 0.5f;
                break;
            // Movement while sliding
            case true when _crouching:
                multiplierV = 0f;
                break;
        }

        //Apply forces to move player
        _rb.AddForce(orientation.transform.forward * _y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        _rb.AddForce(orientation.transform.right * _x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump()
    {
        if (!grounded || !_readyToJump) return;
        _readyToJump = false;

        //Add jump forces
        _rb.AddForce(Vector2.up * jumpForce * 1.5f);
        _rb.AddForce(_normalVector * jumpForce * 0.5f);
            
        //If jumping while falling, reset y velocity.
        Vector3 vel = _rb.velocity;
        _rb.velocity = _rb.velocity.y switch
        {
            < 0.5f => new Vector3(vel.x, 0, vel.z),
            > 0 => new Vector3(vel.x, vel.y / 2, vel.z),
            _ => _rb.velocity
        };

        Invoke(nameof(ResetJump), _jumpCooldown);
    }
    
    private void ResetJump() {
        _readyToJump = true;
    }
    
    private float _desiredX;
    private void Look() {
        float mouseX = Input.GetAxis("Mouse X") * _sensitivity * Time.fixedDeltaTime * _sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * _sensitivity * Time.fixedDeltaTime * _sensMultiplier;

        //Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        _desiredX = rot.y + mouseX;
        
        //Rotate, and also make sure we dont over- or under-rotate.
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        //Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(_xRotation, _desiredX, 0);
        orientation.transform.localRotation = Quaternion.Euler(0, _desiredX, 0);
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || _jumping) return;

        // Slow down sliding
        if (_crouching) {
            _rb.AddForce(moveSpeed * Time.deltaTime * -_rb.velocity.normalized * slideCounterMovement);
            return;
        }

        // Counter movement
        if (Math.Abs(mag.x) > _threshold && Math.Abs(x) < 0.05f || (mag.x < -_threshold && x > 0) || (mag.x > _threshold && x < 0)) {
            _rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > _threshold && Math.Abs(y) < 0.05f || (mag.y < -_threshold && y > 0) || (mag.y > _threshold && y < 0)) {
            _rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }
        
        // Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (!(Mathf.Sqrt((Mathf.Pow(_rb.velocity.x, 2) + Mathf.Pow(_rb.velocity.z, 2))) > maxSpeed)) return;
        float fallspeed = _rb.velocity.y;
        Vector3 n = _rb.velocity.normalized * maxSpeed;
        _rb.velocity = new Vector3(n.x, fallspeed, n.z);
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    private Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(_rb.velocity.x, _rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = _rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        
        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool cancellingGrounded;
    
    /// <summary>
    /// Handle ground detection
    /// </summary>
    private void OnCollisionStay(Collision other) {
        //Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++) {
            Vector3 normal = other.contacts[i].normal;
            //FLOOR
            if (!IsFloor(normal)) continue;
            grounded = true;
            cancellingGrounded = false;
            _normalVector = normal;
            CancelInvoke(nameof(StopGrounded));
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (cancellingGrounded) return;
        cancellingGrounded = true;
        Invoke(nameof(StopGrounded), Time.deltaTime * delay);
    }

    private void StopGrounded() {
        grounded = false;
    }
}