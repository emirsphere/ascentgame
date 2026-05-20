using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerSurfaceSensor))]
    [RequireComponent(typeof(GripManager))]
    [RequireComponent(typeof(ClimbInputBridge))]
    [RequireComponent(typeof(ClimbBodySolver))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour, IPlayerController
    {
        [Header("References")]
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private GameObject _cameraRoot;

        [Header("Optional Body Sensor")]
        [SerializeField] private Transform _wallSensor;

        [Header("Camera Limits")]
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        private PlayerStateFactory _states;
        private PlayerBaseState _currentState;

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private PlayerSurfaceSensor _surfaceSensor;
        private GripManager _gripManager;
        private ClimbInputBridge _climbInputBridge;
        private ClimbBodySolver _climbBodySolver;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Camera _mainCamera;

        private Vector3 _velocity;
        private float _horizontalSpeed;
        private float _cinemachineTargetPitch;
        private float _defaultYPos;
        private float _bobTimer;
        private bool _useClimbClassificationContext;
        private bool _movementBlockedLoggedForClimb;
        private bool _rotationSpeedWarningLogged;

        public PlayerStats Stats => _stats;
        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => _horizontalSpeed;
        public bool IsClimbing => _currentState is PlayerClimbState;
        public bool IsGrounded => IsStableGround;
        public Transform PlayerTransform => transform;
        public Transform CameraTransform => _mainCamera != null ? _mainCamera.transform : transform;

        public SurfaceResult GroundContact => _surfaceSensor != null ? _surfaceSensor.GroundContact : SurfaceResult.None;
        public SurfaceResult GripContact => _surfaceSensor != null ? _surfaceSensor.GripContact : SurfaceResult.None;
        public SurfaceResult BodyContact => _surfaceSensor != null ? _surfaceSensor.BodyContact : SurfaceResult.None;

        public bool HasGroundContact => _surfaceSensor != null && _surfaceSensor.HasGroundContact;
        public bool IsStableGround => _surfaceSensor != null && _surfaceSensor.IsStableGround;
        public bool CanGrip => HasActiveHandAnchor;
        public bool HasClimbVolume => _surfaceSensor != null && _surfaceSensor.HasClimbVolume;
        public bool HasValidGripAnchor => HasActiveHandAnchor;
        public bool HasActiveHandAnchor => _gripManager != null && _gripManager.HasActiveAnchor;
        public float ClimbGripStrengthModifier => _surfaceSensor != null ? _surfaceSensor.ClimbGripStrengthModifier : 1f;

        public float CurrentSlopeAngle => _surfaceSensor != null ? _surfaceSensor.CurrentSlopeAngle : 0f;
        public Vector3 ContactNormal => _surfaceSensor != null ? _surfaceSensor.PrimaryContactNormal : Vector3.up;

        public Vector2 MoveInput => _input.move;
        public bool JumpInput => _input.jump;
        public bool SprintInput => _input.sprint;
        public bool LeftGripInput => _input.leftGrip;
        public bool RightGripInput => _input.rightGrip;
        public ClimbInputData ClimbInput => _climbInputBridge != null ? _climbInputBridge.CurrentInput : default;
        public HandAnchor LeftHandAnchor => _gripManager != null ? _gripManager.LeftHandAnchor : null;
        public HandAnchor RightHandAnchor => _gripManager != null ? _gripManager.RightHandAnchor : null;

        public bool TryGetGripAnchor(out Vector3 point, out Vector3 normal)
        {
            HandAnchor anchor = LeftHandAnchor != null && LeftHandAnchor.isActive
                ? LeftHandAnchor
                : RightHandAnchor;

            if (anchor == null || !anchor.isActive)
            {
                point = default;
                normal = Vector3.up;
                return false;
            }

            point = anchor.position;
            normal = anchor.normal;
            return true;
        }

        public bool ValidateGripAnchor(Vector3 anchor, Vector3 surfaceNormal, ref int physicsValidationStamp) =>
            _surfaceSensor.ValidateGripAnchor(anchor, surfaceNormal, ref physicsValidationStamp);

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _surfaceSensor = GetComponent<PlayerSurfaceSensor>();
            _gripManager = GetComponent<GripManager>();
            _climbInputBridge = GetComponent<ClimbInputBridge>();
            _climbBodySolver = GetComponent<ClimbBodySolver>();
            _mainCamera = Camera.main;
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            if (_cameraRoot != null) _defaultYPos = _cameraRoot.transform.localPosition.y;

            _surfaceSensor.Initialize(_stats, _mainCamera != null ? _mainCamera.transform : null, _wallSensor);

            _states = new PlayerStateFactory(this);
            _currentState = _states.Grounded;
            _currentState.EnterState();
        }

        private void Update()
        {
            bool climbContext = IsClimbing || _useClimbClassificationContext;
            _useClimbClassificationContext = IsClimbing;

            if (_surfaceSensor == null)
            {
                Debug.LogError("[FirstPersonController] PlayerSurfaceSensor is null. Surface Tick() cannot run.");
            }
            else
            {
                _surfaceSensor.Tick(climbContext);
            }

            float vx = _velocity.x;
            float vz = _velocity.z;
            _horizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz);

            _currentState.UpdateState();
            if (!IsClimbing)
                _controller.Move(_velocity * Time.deltaTime);
        }

        private void LateUpdate()
        {
            CameraRotation();

            if (IsClimbing)
                return;

            if (_stats.EnableHeadBob) HandleHeadBob();
            if (_stats.EnableCameraTilt) HandleCameraTilt();
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= 0.01f)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                if (_stats.RotationSpeed <= 0.1f && !_rotationSpeedWarningLogged)
                {
                    Debug.LogError("HATA: PlayerStats içinde 'Rotation Speed' çok düşük!");
                    _rotationSpeedWarningLogged = true;
                }

                float rotationVelocity = _input.look.x * _stats.RotationSpeed * deltaTimeMultiplier;
                transform.Rotate(Vector3.up * rotationVelocity);

                _cinemachineTargetPitch -= _input.look.y * _stats.RotationSpeed * deltaTimeMultiplier;
                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                if (_cameraRoot != null)
                {
                    _cameraRoot.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
                }
            }
        }

        private void HandleHeadBob()
        {
            if (_horizontalSpeed > 0.1f && IsGrounded)
            {
                float freq = _input.sprint ? _stats.BobFrequency * 1.5f : _stats.BobFrequency;
                _bobTimer += Time.deltaTime * freq;
                float newY = _defaultYPos + Mathf.Sin(_bobTimer) * _stats.BobAmplitude;
                Vector3 pos = _cameraRoot.transform.localPosition;
                pos.y = Mathf.Lerp(pos.y, newY, Time.deltaTime * 10f);
                _cameraRoot.transform.localPosition = pos;
            }
            else
            {
                _bobTimer = 0;
                Vector3 pos = _cameraRoot.transform.localPosition;
                pos.y = Mathf.Lerp(pos.y, _defaultYPos, Time.deltaTime * 10f);
                _cameraRoot.transform.localPosition = pos;
            }
        }

        private void HandleCameraTilt()
        {
            float targetTilt = 0f;
            if (_input.move.x > 0.1f) targetTilt = -_stats.TiltAngle;
            else if (_input.move.x < -0.1f) targetTilt = _stats.TiltAngle;

            Quaternion currentRot = _cameraRoot.transform.localRotation;
            Quaternion targetRot = Quaternion.Euler(currentRot.eulerAngles.x, currentRot.eulerAngles.y, targetTilt);
            _cameraRoot.transform.localRotation = Quaternion.Slerp(currentRot, targetRot, Time.deltaTime * _stats.TiltSpeed);
        }

        public void SetVelocity(Vector3 newVelocity) => _velocity = newVelocity;
        public void MoveCharacter(Vector3 motion) => _controller.Move(motion);
        public void ResetJump() => _input.jump = false;

        public void ClearGripAnchors()
        {
            if (_gripManager != null)
                _gripManager.ClearAllAnchors();
        }

        public void SetClimbSolverInput(ClimbInputData inputData)
        {
            if (_climbBodySolver != null)
                _climbBodySolver.SetInput(inputData);
        }

        public void SwitchState(PlayerBaseState newState)
        {
            string previousStateName = _currentState != null ? _currentState.GetType().Name : "None";
            _currentState.ExitState();
            _currentState = newState;
            _useClimbClassificationContext = newState is PlayerClimbState;
            Debug.Log($"[FirstPersonController] State transition: {previousStateName} -> {newState.GetType().Name}");

            if (newState is PlayerClimbState)
            {
                _movementBlockedLoggedForClimb = false;
                LogClimbMovementBlockedOnce();
            }

            _currentState.EnterState();
        }

        private void LogClimbMovementBlockedOnce()
        {
            if (_movementBlockedLoggedForClimb)
                return;

            Debug.Log("[FirstPersonController] Movement blocked due to climb state.");
            _movementBlockedLoggedForClimb = true;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}
