using UnityEngine;
using Ascent.Player.Sensors;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : NetworkBehaviour, IPlayerController
    {
        [Header("References")]
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private GameObject _cameraRoot;
        [SerializeField] private PlayerGripSensor _sensor;

        [Header("Debug Diagnostics")]
        [SerializeField] private bool _enableEventLogs = true;

        [Header("Camera Limits")]
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        [Header("IK Scripts")]
        public ClimbingHandIK LeftHandIK;
        public ClimbingHandIK RightHandIK;

        [Header("Animation")]
        public Animator BodyAnimator;
        private PlayerStateFactory _states;
        private PlayerBaseState _currentState;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;

        private StarterAssetsInputs _input;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Camera _mainCamera;

        private Vector3 _velocity;
        private float _horizontalSpeed;
        private float _cinemachineTargetPitch;
        private float _cinemachineTargetYaw;
        private float _targetYaw;
        public float TargetYaw { get => _targetYaw; set => _targetYaw = value; }
        private float _defaultYPos;
        private float _bobTimer;

        private Vector3 _wallNormal;
        private bool _isTouchingWall;

        private Vector3 _previousVelocity;
        private bool _wasKinematic;

        // --- FISHNET V4+ IK VE TUTUNMA SENKRONİZASYONU ---
        private readonly SyncVar<HandAnchorData> _netLeftAnchor = new SyncVar<HandAnchorData>();
        private readonly SyncVar<HandAnchorData> _netRightAnchor = new SyncVar<HandAnchorData>();

        private bool _pendingJump;
        private float _jumpForce;
        private float _jumpGraceTimer;

        // --- FISHNET V4+ TIRMANMA (IsClimbing) SENKRONİZASYONU ---
        private readonly SyncVar<bool> _netIsClimbing = new SyncVar<bool>();
        private bool _isClimbingLocal;

        public bool IsClimbing
        {
            get
            {
                return base.IsOwner ? _isClimbingLocal : _netIsClimbing.Value;
            }
            set
            {
                _isClimbingLocal = value;
                if (base.IsOwner) ServerSetIsClimbing(value);
            }
        }

        [ServerRpc]
        private void ServerSetIsClimbing(bool val)
        {
            _netIsClimbing.Value = val;
        }

        private Vector3 _climbForce;
        public float TensionCharge { get; private set; }
        public bool IsTensioning => TensionCharge > 0.01f;
        private float _tensionBuildRate = 1.5f;

        public PlayerStats Stats => _stats;
        public PlayerGripSensor Sensor => _sensor;

        public Vector3 Velocity => _rigidbody.linearVelocity;
        public Vector3 VaultTargetPos { get; set; }
        public float HorizontalSpeed => new Vector3(_rigidbody.linearVelocity.x, 0, _rigidbody.linearVelocity.z).magnitude;
        public Transform PlayerTransform => transform;
        public Transform CameraTransform => _mainCamera.transform;

        public Vector3? LeftAnchor { get; private set; }
        public Vector3? RightAnchor { get; private set; }
        public Vector3 LeftNormal { get; private set; }
        public Vector3 RightNormal { get; private set; }
        public bool IsFreeLook { get; set; }
        public float JumpBufferTimer { get; set; }
        private bool _wasJumpPressed;
        public void ConsumeJumpBuffer() { JumpBufferTimer = 0f; }

        public Vector2 MoveInput => _input.move;
        public bool JumpInput => _input.jump;
        public bool JumpHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (_playerInput != null) return _playerInput.actions["Jump"].IsPressed();
#endif
                return _input.jump;
            }
        }
        public bool SprintInput => _input.sprint;

        private bool _leftGripLocked;
        private bool _rightGripLocked;
        private StaminaController _stamina;
        public StaminaController Stamina => _stamina;

        public void LogEvent(string category, string message, bool isError = false)
        {
            if (!_enableEventLogs && !isError) return;
            string formattedMessage = $"<b>[{category}]</b> {message}";
            if (isError) Debug.LogError(formattedMessage, this);
            else Debug.Log(formattedMessage, this);
        }

        public void LockGrips()
        {
            _leftGripLocked = true;
            _rightGripLocked = true;
        }
        public void LockLeftGrip() { _leftGripLocked = true; }
        public void LockRightGrip() { _rightGripLocked = true; }

        public bool LeftGripInput
        {
            get
            {
                if (!_input.leftGrip) _leftGripLocked = false;
                return _input.leftGrip && !_leftGripLocked;
            }
        }

        public bool RightGripInput
        {
            get
            {
                if (!_input.rightGrip) _rightGripLocked = false;
                return _input.rightGrip && !_rightGripLocked;
            }
        }

        public void SetLeftAnchor(Vector3? point, Vector3 normal)
        {
            LeftAnchor = point;
            LeftNormal = normal;
            if (LeftHandIK != null) LeftHandIK.SetGrip(point, normal);

            if (base.IsOwner)
            {
                HandAnchorData data = new HandAnchorData
                {
                    IsGripping = point.HasValue,
                    Position = point.HasValue ? point.Value : Vector3.zero,
                    Normal = normal
                };
                ServerSetLeftAnchor(data);
            }
        }

        [ServerRpc]
        private void ServerSetLeftAnchor(HandAnchorData data)
        {
            _netLeftAnchor.Value = data;
        }

        public void SetRightAnchor(Vector3? point, Vector3 normal)
        {
            RightAnchor = point;
            RightNormal = normal;
            if (RightHandIK != null) RightHandIK.SetGrip(point, normal);

            if (base.IsOwner)
            {
                HandAnchorData data = new HandAnchorData
                {
                    IsGripping = point.HasValue,
                    Position = point.HasValue ? point.Value : Vector3.zero,
                    Normal = normal
                };
                ServerSetRightAnchor(data);
            }
        }

        [ServerRpc]
        private void ServerSetRightAnchor(HandAnchorData data)
        {
            _netRightAnchor.Value = data;
        }

        private void OnLeftAnchorChanged(HandAnchorData oldData, HandAnchorData newData, bool asServer)
        {
            if (base.IsOwner) return;

            Vector3? targetPoint = newData.IsGripping ? newData.Position : null;
            LeftAnchor = targetPoint;
            LeftNormal = newData.Normal;

            if (LeftHandIK != null)
            {
                LeftHandIK.SetGrip(targetPoint, newData.Normal);
            }
        }

        private void OnRightAnchorChanged(HandAnchorData oldData, HandAnchorData newData, bool asServer)
        {
            if (base.IsOwner) return;

            Vector3? targetPoint = newData.IsGripping ? newData.Position : null;
            RightAnchor = targetPoint;
            RightNormal = newData.Normal;

            if (RightHandIK != null)
            {
                RightHandIK.SetGrip(targetPoint, newData.Normal);
            }
        }

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

        [Header("Görsel Modeller")]
        public GameObject SadeceSagEl;
        public GameObject SadeceSolEl;
        public GameObject TamVucutObjesi;

        public override void OnStartClient()
        {
            base.OnStartClient();

            bool hasPlayerInput = TryGetComponent(out UnityEngine.InputSystem.PlayerInput playerInput);
            bool hasStarterInputs = TryGetComponent(out StarterAssetsInputs starterInputs);
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            AudioListener audioListener = GetComponentInChildren<AudioListener>(true);
            Rigidbody rb = GetComponent<Rigidbody>();

            if (base.IsOwner)
            {
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                if (hasPlayerInput) playerInput.enabled = true;
                if (hasStarterInputs) starterInputs.enabled = true;
                if (playerCamera != null) playerCamera.gameObject.SetActive(true);
                if (audioListener != null) audioListener.enabled = true;

                if (TamVucutObjesi != null)
                {
                    TamVucutObjesi.SetActive(true);

                    // ESNEK OBJELERİ GİZLE (Gövde, saç, ayakkabı)
                    SkinnedMeshRenderer[] skinnedRenderers = TamVucutObjesi.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var r in skinnedRenderers) r.enabled = false;

                    // KATI OBJELERİ GİZLE (Toka vb.)
                    MeshRenderer[] meshRenderers = TamVucutObjesi.GetComponentsInChildren<MeshRenderer>();
                    foreach (var r in meshRenderers) r.enabled = false;
                }
                if (SadeceSagEl != null) SadeceSagEl.SetActive(true);
                if (SadeceSolEl != null) SadeceSolEl.SetActive(true);

                GameObject lobbyCam = GameObject.Find("LobbyCamera");
                if (lobbyCam != null) lobbyCam.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None; // TİTREME DÜZELTİCİ SATIR
                }
                if (hasPlayerInput) playerInput.enabled = false;
                if (hasStarterInputs) starterInputs.enabled = false;
                if (playerCamera != null) playerCamera.gameObject.SetActive(false);
                if (audioListener != null) audioListener.enabled = false;

                if (TamVucutObjesi != null)
                {
                    TamVucutObjesi.SetActive(true);
                    // Ne olursa olsun, başkasının modelini zorla görünür yap (GÜVENLİK SATIRI)
                    SkinnedMeshRenderer[] renderers = TamVucutObjesi.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var r in renderers) r.enabled = true;
                }
                if (SadeceSagEl != null) SadeceSagEl.SetActive(false);
                if (SadeceSolEl != null) SadeceSolEl.SetActive(false);
            }
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _input = GetComponent<StarterAssetsInputs>();
            _mainCamera = Camera.main;

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            if (_sensor == null) _sensor = GetComponent<PlayerGripSensor>();
            if (_cameraRoot != null) _defaultYPos = _cameraRoot.transform.localPosition.y;

            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            _rigidbody.linearDamping = 0f;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _targetYaw = _rigidbody.rotation.eulerAngles.y;

            _capsuleCollider.material.dynamicFriction = 0f;
            _capsuleCollider.material.staticFriction = 0f;
            _capsuleCollider.material.frictionCombine = PhysicsMaterialCombine.Minimum;
            _capsuleCollider.material.bounciness = 0f;
            _capsuleCollider.material.bounceCombine = PhysicsMaterialCombine.Minimum;

            _states = new PlayerStateFactory(this);
            _currentState = _states.Grounded;
            _currentState.EnterState();
            _stamina = GetComponent<StaminaController>();
            _stamina.Initialize(_stats.MaxStamina);

            LogEvent("PHYSICS", "FirstPersonController initialized.");

            _netLeftAnchor.OnChange += OnLeftAnchorChanged;
            _netRightAnchor.OnChange += OnRightAnchorChanged;
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            if (_input.jump && !_wasJumpPressed) JumpBufferTimer = _stats.JumpBufferTime;
            _wasJumpPressed = _input.jump;
            if (JumpBufferTimer > 0) JumpBufferTimer -= Time.deltaTime;

            _currentState.UpdateState();
            MonitorPhysicsAnomalies();

            if (BodyAnimator != null)
            {
                BodyAnimator.SetBool("IsClimbing", IsClimbing);
            }
        }

        public void ExecuteJump(float jumpVelocity)
        {
            _pendingJump = true;
            _jumpForce = jumpVelocity;
            _jumpGraceTimer = 0.2f;
        }

        public void BuildTension()
        {
            TensionCharge = Mathf.Clamp01(TensionCharge + (Time.deltaTime * _tensionBuildRate));
        }

        public void ExecuteDynoJump()
        {
            if (TensionCharge < 0.1f)
            {
                ResetTension();
                ResetJump();
                return;
            }

            Vector3 jumpDirection = CameraTransform.forward;
            jumpDirection.y = Mathf.Max(jumpDirection.y, 0.3f);
            jumpDirection.Normalize();

            float appliedForce = _stats.ClimbJumpImpulse * TensionCharge;

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.AddForce(jumpDirection * appliedForce, ForceMode.VelocityChange);

            LogEvent("DYNO", $"Dyno Jump executed! Force: {appliedForce:F2}");

            SetLeftAnchor(null, Vector3.zero);
            SetRightAnchor(null, Vector3.zero);

            LockGrips();

            ResetTension();
            ResetJump();
            IsClimbing = false;
            SwitchState(_states.Air);
        }

        public void ResetTension()
        {
            TensionCharge = 0f;
        }

        public void ApplyClimbForce(Vector3 force)
        {
            _climbForce = force;
        }

        private void FixedUpdate()
        {
            if (!base.IsOwner) return;

            if (_rigidbody.isKinematic)
            {
                _rigidbody.MovePosition(transform.position + _velocity * Time.fixedDeltaTime);
                return;
            }

            if (_jumpGraceTimer > 0) _jumpGraceTimer -= Time.fixedDeltaTime;
            bool isEffectivelyGrounded = _sensor.IsGrounded && _jumpGraceTimer <= 0f;

            if (_pendingJump)
            {
                _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, _jumpForce, _rigidbody.linearVelocity.z);
                _pendingJump = false;
            }

            if (IsClimbing)
            {
                if (_sensor.IsGrounded && _input.move.sqrMagnitude < 0.01f)
                {
                    _capsuleCollider.material.dynamicFriction = 1f;
                    _capsuleCollider.material.staticFriction = 1f;
                    _capsuleCollider.material.frictionCombine = PhysicsMaterialCombine.Maximum;

                    _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
                    _climbForce = new Vector3(0f, _climbForce.y, 0f);
                }
                else
                {
                    _capsuleCollider.material.dynamicFriction = 0f;
                    _capsuleCollider.material.staticFriction = 0f;
                    _capsuleCollider.material.frictionCombine = PhysicsMaterialCombine.Minimum;
                }

                _rigidbody.AddForce(_climbForce, ForceMode.Acceleration);
            }
            else
            {
                Vector3 currentVel = _rigidbody.linearVelocity;
                float rate;

                if (isEffectivelyGrounded)
                {
                    _capsuleCollider.material.dynamicFriction = 0f;
                    _capsuleCollider.material.staticFriction = 0f;
                    _capsuleCollider.material.frictionCombine = PhysicsMaterialCombine.Minimum;

                    Vector3 velocityDiff = _velocity - currentVel;
                    rate = _velocity.sqrMagnitude > 0.01f ? _stats.AccelerationRate : _stats.DecelerationRate;

                    if (_velocity.sqrMagnitude < 0.01f)
                    {
                        _rigidbody.AddForce(-Physics.gravity * _stats.AntiSlipGravityMultiplier, ForceMode.Acceleration);
                    }
                    else
                    {
                        _rigidbody.AddForce(-_sensor.GroundNormal * _stats.GroundStickForce, ForceMode.Acceleration);
                    }

                    _rigidbody.AddForce(velocityDiff * (rate * Time.fixedDeltaTime), ForceMode.VelocityChange);

                    if (!isEffectivelyGrounded && _rigidbody.linearVelocity.y < 0)
                    {
                        _rigidbody.AddForce(Physics.gravity * (_stats.FallGravityMultiplier - 1f), ForceMode.Acceleration);
                    }
                }
                else
                {
                    Vector3 targetVel = new Vector3(_velocity.x, 0, _velocity.z);
                    Vector3 flatCurrent = new Vector3(currentVel.x, 0, currentVel.z);
                    Vector3 velocityDiff = targetVel - flatCurrent;

                    rate = targetVel.sqrMagnitude > 0.01f ? _stats.AirControlRate : _stats.AirDragRate;
                    _capsuleCollider.material.dynamicFriction = 0f;
                    _capsuleCollider.material.staticFriction = 0f;
                    _capsuleCollider.material.frictionCombine = PhysicsMaterialCombine.Minimum;

                    _rigidbody.AddForce(velocityDiff * (rate * Time.fixedDeltaTime), ForceMode.VelocityChange);
                }
            }

            Quaternion targetRotation = Quaternion.Euler(0f, _targetYaw, 0f);
            _rigidbody.MoveRotation(targetRotation);

            _previousVelocity = _rigidbody.linearVelocity;
        }

        private void MonitorPhysicsAnomalies()
        {
            if (_rigidbody.linearVelocity.sqrMagnitude > 500f)
            {
                LogEvent("PHYSICS", $"Anomalous high velocity detected: {_rigidbody.linearVelocity.magnitude:F2}", true);
            }
        }

        public void SmoothAlignBody(Vector3 targetNormal, float speed)
        {
            float targetAlignmentYaw = Quaternion.LookRotation(-targetNormal).eulerAngles.y;
            float prevYaw = _targetYaw;

            _targetYaw = Mathf.LerpAngle(_targetYaw, targetAlignmentYaw, speed * Time.deltaTime);

            if (IsFreeLook)
            {
                float delta = Mathf.DeltaAngle(prevYaw, _targetYaw);
                _cinemachineTargetYaw -= delta;
            }
        }

        private void LateUpdate()
        {
            if (!base.IsOwner) return;

            CameraRotation();
            if (_stats.EnableHeadBob && !IsFreeLook) HandleHeadBob();
            if (_stats.EnableCameraTilt && !IsFreeLook) HandleCameraTilt();
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= 0.01f)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                float lookX = _input.look.x * _stats.RotationSpeed * deltaTimeMultiplier;
                float lookY = _input.look.y * _stats.RotationSpeed * deltaTimeMultiplier;

                _cinemachineTargetPitch -= lookY;
                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                if (IsFreeLook)
                {
                    _cinemachineTargetYaw += lookX;
                    _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, -100f, 100f);
                }
                else
                {
                    _targetYaw += lookX;
                    _targetYaw = NormalizeYaw(_targetYaw);
                }
            }

            if (_cameraRoot != null)
            {
                float localYaw = _cinemachineTargetYaw;
                if (!IsFreeLook && _rigidbody != null)
                {
                    float bodyYaw = transform.eulerAngles.y;
                    localYaw = Mathf.DeltaAngle(bodyYaw, _targetYaw);
                }
                _cameraRoot.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, localYaw, 0.0f);
            }
        }

        public bool CheckLedgeVault(out Vector3 vaultTarget)
        {
            vaultTarget = Vector3.zero;
            Vector3 topRayStart = _mainCamera.transform.position + Vector3.up * 0.3f;
            Vector3 forwardDir = _cameraRoot.transform.forward;
            forwardDir.y = 0;
            forwardDir.Normalize();

            if (!Physics.Raycast(topRayStart, forwardDir, 2.0f, _stats.ClimbableLayers))
            {
                Vector3 downRayStart = topRayStart + forwardDir * 0.8f;

                if (Physics.SphereCast(downRayStart, 0.30f, Vector3.down, out RaycastHit hit, 3.0f, _stats.GroundLayers | _stats.ClimbableLayers))
                {
                    float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (slopeAngle < 45f)
                    {
                        vaultTarget = hit.point + (Vector3.up * 0.05f);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetGripPoint(Vector3? oppositeHandAnchor, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;
            if (CheckLedgeVault(out _)) return false;

            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

            if (!Physics.SphereCast(ray, 0.06f, out RaycastHit hit, _stats.GripReachDistance, _stats.ClimbableLayers))
            {
                return false;
            }

            float wallAngle = Vector3.Angle(Vector3.up, hit.normal);
            if (wallAngle < 25f || wallAngle > 160f) return false;
            if (Vector3.Dot(ray.direction, hit.normal) > 0.1f) return false;

            Vector3 shoulderPos = transform.position + (Vector3.up * _stats.ShoulderHeight);
            float distToShoulder = Vector3.Distance(shoulderPos, hit.point);
            float maxReach = Mathf.Min(_stats.GripReachDistance, 1.3f);
            if (distToShoulder > maxReach) return false;

            if (oppositeHandAnchor.HasValue)
            {
                float distBetweenHands = Vector3.Distance(hit.point, oppositeHandAnchor.Value);
                if (distBetweenHands > _stats.MaxArmSpan) return false;
            }

            hitPoint = hit.point;
            hitNormal = hit.normal;
            return true;
        }

        public void ResetFreeLook()
        {
            if (!IsFreeLook) return;
            IsFreeLook = false;

            _targetYaw = NormalizeYaw(_targetYaw + _cinemachineTargetYaw);
            _cinemachineTargetYaw = 0f;

            if (_cameraRoot != null)
            {
                _cameraRoot.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
            }
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            return yaw;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!base.IsOwner) return;
            LogEvent("COLLISION", $"Collision entered with {collision.gameObject.name}. Relative velocity: {collision.relativeVelocity.magnitude:F2}");
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!base.IsOwner) return;
            if (!_sensor.IsGrounded)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    if (Mathf.Abs(contact.normal.y) < 0.2f)
                    {
                        _wallNormal = contact.normal;
                        _isTouchingWall = true;
                        break;
                    }
                }
            }
        }

        private void HandleHeadBob()
        {
            if (_horizontalSpeed > 0.1f && _sensor.IsGrounded)
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

        public void SetVelocity(Vector3 newVelocity)
        {
            _velocity = newVelocity;

            if (IsClimbing)
            {
                _rigidbody.linearVelocity = newVelocity;
            }
        }

        public void ResetJump() => _input.jump = false;

        public void SwitchState(PlayerBaseState newState)
        {
            LogEvent("STATE", $"Transitioning from {_currentState?.GetType().Name} to {newState.GetType().Name}");
            _currentState?.ExitState();
            _currentState = newState;
            _currentState.EnterState();
        }

        public void SetKinematic(bool isKinematic)
        {
            _rigidbody.isKinematic = isKinematic;
        }
    }
}