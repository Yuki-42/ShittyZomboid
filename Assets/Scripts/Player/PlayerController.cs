// ------------------------------------------ 
// BasicFPCC.cs
// A basic first person character controller
// with jump, crouch, run, sprint, climb
//
// Modified from BasicFPCC.cs 2020-10-04 Alucard Jay Kay https://discussions.unity.com/t/855344
// ------------------------------------------ 

// ------------------------------------------
// Other Sources : 
// https://discussions.unity.com/t/855344
// Brackeys FPS controller base : 
// https://www.youtube.com/watch?v=_QajrabyTJc
// ground check : (added isGrounded)
// https://gist.github.com/jawinn/f466b237c0cdc5f92d96
// run, crouch, slide : (added check for headroom before un-crouching)
// https://answers.unity.com/questions/374157/character-controller-slide-action-script.html
// interact with rigidbodies : 
// https://docs.unity3d.com/2018.4/Documentation/ScriptReference/CharacterController.OnControllerColliderHit.html
// 
// ** SETUP **
// Assign the BasicFPCC object to its own Layer
// Assign the Layer Mask to ignore the BasicFPCC object Layer
// CharacterController (component) : Center => X 0, Y 1, Z 0
// Main Camera (as child) : Transform : Position => X 0, Y 1.7, Z 0
// (optional GFX) Capsule primitive without collider (as child) : Transform : Position => X 0, Y 1, Z 0
// alternatively : 
// at the end of this script is a Menu Item function to create and auto-configure a BasicFPCC object
// GameObject -> 3D Object -> BasicFPCC
//
// TODOs
// 1. Implement a sprint-vector check to ensure the player is only sprinting forwards to disallow sprinting backwards
// 2. Clamp both vertical and horizontal max speed.
// 3. Fix the jump strength and insane gravity
//
//
// Fixed:
// - Fix insane speed issue when falling off solid surface
// ------------------------------------------

using System;

namespace Player
{
    using Config;
    using UnityEngine;
    using UnityEngine.InputSystem;
    
#if UNITY_EDITOR // only required if using the Menu Item function at the end of this script
#endif

    //[RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerConfig))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Layer Mask")]
        [Tooltip("Layer Mask for sphere/raycasts. Assign the Player object to a Layer, then Ignore that layer here.")]
        public LayerMask castingMask; // Layer mask for casts. You'll want to ignore the player.

        // - Components -
        private CharacterController _controller; // CharacterController component
        private PlayerConfig _playerConfig; // PlayerConfig component
        public Canvas canvas; // Canvas component

        [Header("Main Camera")]
        public Transform playerCameraTransform; // Main Camera, as child of BasicFPCC object
        public Camera playerCamera;

        [Header("Optional Player Graphic")] [Tooltip("optional capsule to visualize player in scene view")]
        public Transform playerGfx; // optional capsule graphic object

        // Grab input actions class
        private PlayerControls _playerControls;
        [Space(5)]
        // Input Variables that can be assigned externally the cursor can also be manually locked or freed by calling the public void SetLockCursor( bool doLock )
        private bool _lookEnabled = true;
        private Vector2 _mouseSensitivity = new Vector2(1f, 1f);

        private bool _invertLookY = false; // toggle invert look Y
        public float clampLookY = 90f; // maximum look up/down angle

        private float _cameraXRotation = 0f;

        [Header("Move Settings")]
        public float crouchWalkSpeed = 3f;
        public float walkSpeed = 7f;
        public float runSpeed = 12f;
        public float sprintSpeed = 16f;
        public float gravity = -9.81f;
        public float jumpHeight = 1f;
        public float horizontalSpeedCap = 240f;  // Realistic terminal velocity of a human
        public float verticalSpeedCap = 240f;
        public float acceleration = 2f;

        private Vector3 _velocity = Vector3.zero;
        private float _targetSpeedFactor = 0f;
        private float _previousSpeed = 0f;
        
        // Reference variables
        private float _defaultHeight; // Normal player height, used for scaling down on crouch
        private float _cameraDefaultY; // Normal camera Y position within player
        
        
        // ------------------------------------------------------------------------------------------------------------------------
        // ----------------------------------------       Input Manager Boilerplate        ----------------------------------------
        
        // Create actions
        private InputAction _toggleCursor;
        private InputAction _toggleUI;
        
        // Create input variables
        [HideInInspector] public Vector2 inputLook;
        [HideInInspector] public Vector2 inputMove;
        [HideInInspector] public bool inputKeyRun = false; // is key Held
        [HideInInspector] public bool inputKeySprint = false;
        [HideInInspector] public bool inputKeyCrouch = false; // is key Held
        [HideInInspector] public bool inputKeyJump = false; // is key Pressed
        
        private void Awake()
        {
            _playerControls = new PlayerControls();
        }

        private void OnEnable()
        {
            _playerControls.Enable();
            _toggleCursor = _playerControls.Player.ShowCursor;
            _toggleCursor.Enable();

            _toggleUI = _playerControls.Player.ShowUI;
            _toggleUI.Enable();
            
            // Assign actions
            // ReSharper disable All
            _toggleCursor.performed += ActionCursorLock;  // Not exactly sure why Intellij is detecting this as an error
            _toggleUI.performed += ActionShowUI;
            // ReSharper restore All
        }

        private void OnDisable()
        {
            _playerControls.Disable();
        }

        // ----------------------------------------     END Input Manager Boilerplate      ----------------------------------------
        // ------------------------------------------------------------------------------------------------------------------------
        
        
        // ------------------------------------------------------------------------------------------------------------------------
        // ----------------------------------------             Handle Inputs              ----------------------------------------

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
        
        // ----------------------------------------           END Handle Inputs            ----------------------------------------
        // ------------------------------------------------------------------------------------------------------------------------


        private void Start()
        {
            // Get required components
            _controller = GetComponent<CharacterController>();
            _playerConfig = GetComponent<PlayerConfig>();

            _defaultHeight = _controller.height;
            _cameraDefaultY = playerCameraTransform.localPosition.y;

            // Lock cursor by default
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            ReadInputs();
            HandleCamera();
            ProcessMovement();
        }

        private void FixedUpdate()
        {
            // Handle movement in fixed update
            

            // Check for settings updates for expensive calculations
            if (!_playerConfig.updated) return;
            _playerConfig.updated = false;
            _mouseSensitivity.x = _playerConfig.MouseSensitivity;
            _mouseSensitivity.y = _playerConfig.MouseSensitivity;
            _invertLookY = _playerConfig.InvertYAxis;
        }

        private void ReadInputs()
        {
            // Read in input values
            inputLook = _playerControls.Player.Look.ReadValue<Vector2>();
            inputMove = _playerControls.Player.Move.ReadValue<Vector2>();
            inputKeyRun = _playerControls.Player.Run.ReadValue<float>() >= 0.5f;
            inputKeySprint = _playerControls.Player.Sprint.ReadValue<float>() >= 0.5f;
            inputKeyCrouch = _playerControls.Player.Crouch.ReadValue<float>() >= 0.5f;
            inputKeyJump = _playerControls.Player.Jump.ReadValue<float>() >= 0.5f;
        }
        
        /// <summary>
        /// Handle camera rotation.
        /// </summary>
        private void HandleCamera()
        {
            if (!_lookEnabled) return;
            
            // Get mouse input
            float mouseX = inputLook.x * _mouseSensitivity.x * 100f * Time.deltaTime;
            float mouseY = inputLook.y * _mouseSensitivity.y * 100f * Time.deltaTime;

            // Rotate camera X
            float xRotationFactor = _invertLookY ? mouseY : -mouseY;

            // Get current x rotation
            _cameraXRotation = playerCamera.transform.localRotation.eulerAngles.x + xRotationFactor;

            // Clamp rotation
            if (_cameraXRotation > 180) _cameraXRotation -= 360;  // Normalize the rotation (0 - 360)
            _cameraXRotation = Mathf.Clamp(_cameraXRotation, -clampLookY, clampLookY);

            // Apply rotation
            playerCamera.transform.localRotation = Quaternion.Euler(_cameraXRotation, 0f, 0f);  // This is jumping back to

            // Rotate player Y
            transform.Rotate(Vector3.up * mouseX);
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

        private void ProcessMovement()
        {
            // Apply speed increases
            _targetSpeedFactor = walkSpeed;
            if (inputKeyRun) _targetSpeedFactor = runSpeed;
            if (inputKeySprint) _targetSpeedFactor = sprintSpeed;
            if (inputKeyCrouch) _targetSpeedFactor = crouchWalkSpeed;

            //_targetSpeedFactor /= 5;

            // Add acceleration
            float localSpeedFactor = _previousSpeed;
            if (_previousSpeed < _targetSpeedFactor)
            {
                localSpeedFactor += acceleration * Time.deltaTime;
                if (localSpeedFactor > _targetSpeedFactor) localSpeedFactor = _targetSpeedFactor;
            } if (_previousSpeed > _targetSpeedFactor)
            {
                localSpeedFactor -= acceleration * Time.deltaTime;
                if (localSpeedFactor < _targetSpeedFactor) localSpeedFactor = _targetSpeedFactor;
            }

            localSpeedFactor *= Time.deltaTime;

            // Do jump movement
            if (inputKeyJump && _controller.isGrounded)
            {
                Debug.Log("Jumping");
                _velocity.y += jumpHeight / 100;
            }

            // Apply gravity
            if (!_controller.isGrounded) _velocity.y += (gravity / 100) * Time.deltaTime * 0.5f;

            // Calculate movement
            Vector3 move = transform.right * (inputMove.x * localSpeedFactor) + transform.forward * (inputMove.y * localSpeedFactor) + (_velocity);
            _controller.Move(move);

            // Note previous speed
            _previousSpeed = _targetSpeedFactor;
        }

        /*
        private void ProcessMovement()
        {








            // ALL OF THIS CODE IS TERRIBLE AND SHOULD BE REWRITTEN USING THIS GUIDE AND A BRAIN https://discussions.unity.com/t/proper-velocity-based-movement-101/659301

            // // - variables -
            // float vScale = 1f; // for calculating GFX scale (optional)
            // float h = _defaultHeight;
            // float nextSpeed = walkSpeed;
            // Vector3 calc; // used for calculations
            //
            // // Player current speed
            // float currSpeed = (_playerTransform.position - _lastPos).magnitude / Time.deltaTime;
            // currSpeed = currSpeed < 0 ? 0 - currSpeed : currSpeed; // Get the absolute value, regardless of vector direction
            //
            // // Grounded Checks
            // GroundCheck();
            // isSlipping = groundSlopeAngle > _controller.slopeLimit;
            //
            // // Headroom Check
            // CeilingCheck();
            //
            // // - Run and Crouch -
            //
            // // If the player is grounded and not stuck on ceiling, apply speed increases
            // if (isGrounded && !isCeiling)
            // {
            //     if (inputKeyRun) nextSpeed = runSpeed;
            //     if (inputKeySprint) nextSpeed = sprintSpeed;
            // }
            //
            // if (inputKeyCrouch) // Crouch
            // {
            //     vScale = 0.5f;
            //     h = 0.5f * _defaultHeight;
            //     nextSpeed = crouchWalkSpeed; // slow down when crouching
            // }
            //
            // // - Slide -
            //
            // // // if not sliding, and not stuck on ceiling, and is running
            // // if (!isCeiling && inputKeyRun && inputKeyDownSlide) // slide
            // // {
            // //     // check velocity is faster than walkSpeed
            // //     if (currSpeed > walkSpeed)
            // //     {
            // //         slideTimer = 0; // start slide timer
            // //         isSliding = true;
            // //         slideForward = (_playerTransform.position - _lastPos).normalized;
            // //     }
            // // }
            // // _lastPos = _playerTransform.position; // update reference
            //
            //
            // // - Player Move Input -
            // Vector3 move = (_playerTransform.right * inputMove.x) + (_playerTransform.forward * inputMove.y); // direction calculation
            //
            // if (move.magnitude > 1f)
            // {
            //     move = move.normalized;
            // }
            //
            //
            // // - Height -
            //
            // // crouch/stand up smoothly
            // float lastHeight = _controller.height;
            // float nextHeight = Mathf.Lerp(_controller.height, h, 5f * Time.deltaTime);
            //
            // // if crouching, or only stand if there is no ceiling
            // if (nextHeight < lastHeight || !isCeiling)
            // {
            //     _controller.height = Mathf.Lerp(_controller.height, h, 5f * Time.deltaTime);
            //
            //     // fix vertical position
            //     calc = _playerTransform.position;
            //     calc.y += (_controller.height - lastHeight) / 2f;
            //     _playerTransform.position = calc;
            //
            //     // offset camera
            //     calc = playerCameraTransform.localPosition;
            //     calc.y = (_controller.height / _defaultHeight) + _cameraDefaultY - (_defaultHeight * 0.5f);
            //     playerCameraTransform.localPosition = calc;
            //
            //     // calculate offset
            //     float heightFactor = (_defaultHeight - _controller.height) * 0.5f;
            //
            //     // offset ground check
            //     _groundOffsetY = heightFactor + groundCheckY;
            //
            //     // offset ceiling check
            //     _ceilingOffsetY = heightFactor + _controller.height - (_defaultHeight - ceilingCheckY);
            //
            //     // scale gfx (optional)
            //     if (playerGfx)
            //     {
            //         calc = playerGfx.localScale;
            //         calc.y = Mathf.Lerp(calc.y, vScale, 5f * Time.deltaTime);
            //         playerGfx.localScale = calc;
            //     }
            // }
            //
            // // - Slipping Jumping Gravity -
            //
            // // Smooth speed  // ?????????? What????
            // float speed;
            //
            // if (isGrounded)
            // {
            //     if (isSlipping) // slip down slope
            //     {
            //         // Movement left/right while slipping down
            //         // Player rotation to slope
            //         Vector3 slopeRight = Quaternion.LookRotation(Vector3.right) * groundSlopeDir;
            //         float dot = Vector3.Dot(slopeRight, _playerTransform.right);
            //         // Move on X axis, with Y rotation relative to slopeDir
            //         move = slopeRight * (dot > 0 ? inputMove.x : -inputMove.x);
            //
            //         // Speed
            //         nextSpeed = Mathf.Lerp(currSpeed, runSpeed, 5f * Time.deltaTime);
            //
            //         // Increase angular gravity
            //         float mag = _fauxGravity.magnitude;
            //         calc = Vector3.Slerp(_fauxGravity, groundSlopeDir * runSpeed, 4f * Time.deltaTime);
            //         _fauxGravity = calc.normalized * mag;
            //     }
            //     else
            //     {
            //         // reset angular fauxGravity movement
            //         _fauxGravity.x = 0;
            //         _fauxGravity.z = 0;
            //
            //         if (_fauxGravity.y < 0) // constant grounded gravity
            //         {
            //             //fauxGravity.y = -1f;
            //             _fauxGravity.y = Mathf.Lerp(_fauxGravity.y, -1f, 4f * Time.deltaTime);
            //         }
            //     }
            //
            //     // - Jump -
            //     if (!isCeiling && inputKeyJump) // jump
            //     {
            //         _fauxGravity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            //     }
            //
            //     // --
            //
            //     // - smooth speed -
            //     // take less time to slow down, more time speed up
            //     float lerpFactor = (_lastSpeed > nextSpeed ? 4f : 2f);
            //     speed = Mathf.Lerp(_lastSpeed, nextSpeed, lerpFactor * Time.deltaTime);
            // }
            // else // no friction, speed changes slower
            // {
            //     speed = Mathf.Lerp(_lastSpeed, nextSpeed, 0.125f * Time.deltaTime);
            // }
            //
            // // prevent floating if jumping into a ceiling
            // if (isCeiling)
            // {
            //     speed = crouchWalkSpeed; // clamp speed to crouched
            //
            //     if (_fauxGravity.y > 0)
            //     {
            //         _fauxGravity.y = -1f; // 0;
            //     }
            // }
            //
            // _lastSpeed = speed; // update reference
            //
            // // - Add Gravity -
            // _fauxGravity.y += gravity * Time.deltaTime;
            //
            // // Do movement calculations
            // calc = move * (speed * Time.deltaTime);
            // calc += _fauxGravity * Time.deltaTime;
            //
            // // Apply speed cap
            // calc = new Vector3(
            //     Mathf.Clamp(calc.x, -horizontalSpeedCap, horizontalSpeedCap),
            //     Mathf.Clamp(calc.y, -horizontalSpeedCap, horizontalSpeedCap),
            //     Mathf.Clamp(calc.z, -verticalSpeedCap, verticalSpeedCap)
            //     );
            //
            // _controller.Move(calc);
            //
            // // - DEBUG -

#if UNITY_EDITOR
            // slope angle and fauxGravity debug info
            // if (showGizmos)
            // {
            //     calc = _playerTransform.position;
            //     calc.y += _groundOffsetY;
            //     Debug.DrawRay(calc, groundSlopeDir.normalized * 5f, Color.blue);
            //     Debug.DrawRay(calc, _fauxGravity, Color.green);
            // }
#endif
        }



        // check the area above, for standing from crouch
        private void CeilingCheck()
        {
            Vector3 origin = new(_playerTransform.position.x, _playerTransform.position.y + _ceilingOffsetY, _playerTransform.position.z);

            isCeiling = Physics.CheckSphere(origin, sphereCastRadius, castingMask);
        }

        // find if isGrounded, slope angle and directional vector
        private void GroundCheck()
        {
            //Vector3 origin = new Vector3( transform.position.x, transform.position.y - (controller.height / 2) + startDistanceFromBottom, transform.position.z );
            Vector3 origin = new(_playerTransform.position.x, _playerTransform.position.y + _groundOffsetY, _playerTransform.position.z);

            // Out hit point from our cast(s)
            RaycastHit hit;

            // SPHERECAST
            // "Casts a sphere along a ray and returns detailed information on what was hit."
            if (Physics.SphereCast(origin, sphereCastRadius, Vector3.down, out hit, sphereCastDistance, castingMask))
            {
                // Angle of our slope (between these two vectors).
                // A hit normal is at a 90 degree angle from the surface that is collided with (at the point of collision).
                // e.g. On a flat surface, both vectors are facing straight up, so the angle is 0.
                groundSlopeAngle = Vector3.Angle(hit.normal, Vector3.up);

                // Find the vector that represents our slope as well.
                //  temp: basically, finds vector moving across hit surface
                Vector3 temp = Vector3.Cross(hit.normal, Vector3.down);
                //  Now use this vector and the hit normal, to find the other vector moving up and down the hit surface
                groundSlopeDir = Vector3.Cross(temp, hit.normal);

                // --
                isGrounded = true;
            }
            else
            {
                isGrounded = false;
            } // --

            // Now that's all fine and dandy, but on edges, corners, etc, we get angle values that we don't want.
            // To correct for this, let's do some ray-casts. You could do more ray-casts, and check for more
            // edge cases here. There are lots of situations that could pop up, so test and see what gives you trouble.
            RaycastHit slopeHit1;
            RaycastHit slopeHit2;

            // FIRST RAYCAST
            if (!Physics.Raycast(origin + rayOriginOffset1, Vector3.down, out slopeHit1, raycastLength)) return;
            // Debug line to first hit point
#if UNITY_EDITOR
            if (showGizmos)
            {
                Debug.DrawLine(origin + rayOriginOffset1, slopeHit1.point, Color.red);
            }
#endif
            // Get angle of slope on hit normal
            float angleOne = Vector3.Angle(slopeHit1.normal, Vector3.up);

            // 2ND RAYCAST
            if (Physics.Raycast(origin + rayOriginOffset2, Vector3.down, out slopeHit2, raycastLength))
            {
                // Debug line to second hit point
#if UNITY_EDITOR
                if (showGizmos)
                {
                    Debug.DrawLine(origin + rayOriginOffset2, slopeHit2.point, Color.red);
                }
#endif
                // Get angle of slope of these two hit points.
                float angleTwo = Vector3.Angle(slopeHit2.normal, Vector3.up);
                // 3 collision points: Take the MEDIAN by sorting array and grabbing middle.
                float[] tempArray = { groundSlopeAngle, angleOne, angleTwo };
                groundSlopeAngle = tempArray[1];
            }
            else
            {
                // 2 collision points (sphere and first raycast): AVERAGE the two
                float average = (groundSlopeAngle + angleOne) / 2;
                groundSlopeAngle = average;
            }
        }

        /// <summary>
        /// Push rigid-bodies the player is colliding with.
        /// </summary>
        /// <param name="hit">The object the player is hitting</param>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            // No rigidbody found
            if (body == null || body.isKinematic)
            {
                return;
            }

            // We don't want to push objects below us
            if (hit.moveDirection.y < -0.3f)
            {
                return;
            }

            // If you know how fast your character is trying to move, then you can also multiply the push velocity by that.
            body.velocity = hit.moveDirection * _lastSpeed;
        }

        // Debug Gizmos
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            if (!Application.isPlaying)
            {
                _groundOffsetY = groundCheckY;
                _ceilingOffsetY = ceilingCheckY;
            }

            Vector3 position = transform.position;
            Vector3 startPoint = new(position.x, position.y + _groundOffsetY, position.z);
            Vector3 endPoint = startPoint + new Vector3(0, -sphereCastDistance, 0);
            Vector3 ceilingPoint = new(position.x, position.y + _ceilingOffsetY,
                position.z);

            Gizmos.color = (isGrounded ? Color.green : Color.white);
            Gizmos.DrawWireSphere(startPoint, sphereCastRadius);

            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(endPoint, sphereCastRadius);

            Gizmos.DrawLine(startPoint, endPoint);

            Gizmos.color = (isCeiling ? Color.red : Color.white);
            Gizmos.DrawWireSphere(ceilingPoint, sphereCastRadius);
        }
#endif  */
    }

    /*
// =======================================================================================================================================

// ** DELETE from here down, if menu item and auto configuration is NOT Required **

// this section adds create BasicFPCC object to the menu : New -> GameObject -> 3D Object
// then configures the gameobject
// demo layer used : Ignore Raycast
// also finds the main camera, attaches and sets position
// and creates capsule gfx object (for visual while editing)

// A using clause must precede all other elements defined in the namespace except extern alias declarations
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

    public class BasicFPCC_Setup : MonoBehaviour
    {
#if UNITY_EDITOR

        private static int playerLayer = 2; // default to the Ignore Raycast Layer (to demonstrate configuration)

        [MenuItem("GameObject/3D Object/BasicFPCC", false, 0)]
        public static void CreateBasicFPCC()
        {
            GameObject go = new GameObject("Player");

            CharacterController controller = go.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 1, 0);

            BasicFPCC basicFPCC = go.AddComponent<BasicFPCC>();

            // Layer Mask
            go.layer = playerLayer;
            basicFPCC.castingMask = ~(1 << playerLayer);
            Debug.LogError("** SET the LAYER of the PLAYER Object, and the LAYERMASK of the BasicFPCC castingMask **");
            Debug.LogWarning(
                "Assign the BasicFPCC Player object to its own Layer, then assign the Layer Mask to ignore the BasicFPCC Player object Layer. Currently using layer "
                + playerLayer.ToString() + ": " + LayerMask.LayerToName(playerLayer)
            );

            // Main Camera
            GameObject mainCamObject = GameObject.Find("Main Camera");
            if (mainCamObject)
            {
                mainCamObject.transform.parent = go.transform;
                mainCamObject.transform.localPosition = new Vector3(0, 1.7f, 0);
                mainCamObject.transform.localRotation = Quaternion.identity;

                basicFPCC._playerCameraTransform = mainCamObject.transform;
            }
            else // create example camera
            {
                Debug.LogError(
                    "** Main Camera NOT FOUND ** \nA new Camera has been created and assigned. Please replace this with the Main Camera (and associated AudioListener).");

                GameObject camGo = new GameObject("BasicFPCC Camera");
                camGo.AddComponent<Camera>();

                camGo.transform.parent = go.transform;
                camGo.transform.localPosition = new Vector3(0, 1.7f, 0);
                camGo.transform.localRotation = Quaternion.identity;

                basicFPCC._playerCameraTransform = camGo.transform;
            }

            // GFX
            GameObject gfx = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Collider cc = gfx.GetComponent<Collider>();
            DestroyImmediate(cc);
            gfx.transform.parent = go.transform;
            gfx.transform.localPosition = new Vector3(0, 1, 0);
            gfx.name = "GFX";
            gfx.layer = playerLayer;
            basicFPCC.playerGfx = gfx.transform;
        }
#endif
    } 
*/
}