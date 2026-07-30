using UnityEngine;
using Ascent.Player.Sensors;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour, IPlayerController
    {
        [Header("References")]
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private GameObject _cameraRoot;
        [SerializeField] private PlayerGripSensor _sensor;

        [Header("Camera Limits")]
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;
        private PlayerStateFactory _states;
        private PlayerBaseState _currentState;

        private CharacterController _controller;
        private StarterAssetsInputs _input;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Camera _mainCamera;

        private Vector3 _velocity;
        private float _horizontalSpeed;
        private float _cinemachineTargetPitch;
        private float _cinemachineTargetYaw;
        private float _defaultYPos;
        private float _bobTimer;

        public PlayerStats Stats => _stats;
        public PlayerGripSensor Sensor => _sensor;
        public Vector3 Velocity => _velocity;
        public Vector3 VaultTargetPos { get; set; }
        public float HorizontalSpeed => _horizontalSpeed;
        public Transform PlayerTransform => transform;
        public Transform CameraTransform => _mainCamera.transform;

        public Vector3? LeftAnchor { get; private set; }
        public Vector3? RightAnchor { get; private set; }
        public Vector3 LeftNormal { get; private set; }
        public Vector3 RightNormal { get; private set; }
        public bool IsFreeLook { get; set; }

        public Vector2 MoveInput => _input.move;
        public bool JumpInput => _input.jump;
        public bool SprintInput => _input.sprint;
        public bool LeftGripInput => _input.leftGrip;
        public bool RightGripInput => _input.rightGrip;

        public void SetLeftAnchor(Vector3? point, Vector3 normal) { LeftAnchor = point; LeftNormal = normal; }
        public void SetRightAnchor(Vector3? point, Vector3 normal) { RightAnchor = point; RightNormal = normal; }

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
            _mainCamera = Camera.main;
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            if (_sensor == null) _sensor = GetComponent<PlayerGripSensor>();
            if (_cameraRoot != null) _defaultYPos = _cameraRoot.transform.localPosition.y;

            _states = new PlayerStateFactory(this);
            _currentState = _states.Grounded;
            _currentState.EnterState();
        }

        private void Update()
        {
            float vx = _velocity.x;
            float vz = _velocity.z;
            _horizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz);

            _currentState.UpdateState();

            if (_controller.enabled)
            {
                _controller.Move(_velocity * Time.deltaTime);
            }
            else
            {
                transform.position += _velocity * Time.deltaTime;
            }
        }

        private void LateUpdate()
        {
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
                    transform.Rotate(Vector3.up * lookX);
                }

                if (_cameraRoot != null)
                {
                    _cameraRoot.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
                }
            }
        }

        public bool CheckLedgeVault(out Vector3 vaultTarget)
        {
            vaultTarget = Vector3.zero;

            // 1. Işın kafanın 1 metre üstünden çıksın (Sorun yok)
            Vector3 topRayStart = _mainCamera.transform.position + Vector3.up * 1.0f;
            Vector3 forwardDir = _cameraRoot.transform.forward;
            forwardDir.y = 0;
            forwardDir.Normalize();

            // 2. İLERİ BAKIŞ: Karakter duvardan 1.2m uzakta. Çatıyı anlamak için 2.0 metre ileri bak.
            if (!Physics.Raycast(topRayStart, forwardDir, 2.0f, _stats.ClimbableLayers))
            {
                // 3. AŞAĞI BAKIŞ (KRİTİK DÜZELTME): Çatının ucuna değil, güvenli bir şekilde 
                // çatının iyice iç kısmına (2.2 metre ileriye) gidip aşağı ışın atıyoruz.
                Vector3 downRayStart = topRayStart + forwardDir * 2.2f;

                // 4. Aşağı doğru kalın küre (SphereCast)
                if (Physics.SphereCast(downRayStart, 0.30f, Vector3.down, out RaycastHit hit, 3.0f, _stats.GroundLayers | _stats.ClimbableLayers))
                {
                    float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (slopeAngle < 45f)
                    {
                        float yOffset = _controller.height / 2f;
                        // Güvenlik toleransını da hafif artırdık ki yerin içine girmesin
                        vaultTarget = hit.point + Vector3.up * (yOffset + 0.2f);
                        return true;
                    }
                }
            }
            return false;
        }

        public void ResetFreeLook()
        {
            if (!IsFreeLook) return;
            IsFreeLook = false;

            // KİLİTLENME FIX: Serbest bakıştan çıkarken aniden snaplemek yerine
            // gövdeyi yavaşça çevirmiyoruz, ancak yaw değerini sıfırlarken yumuşatıyoruz
            transform.Rotate(Vector3.up * _cinemachineTargetYaw);
            _cinemachineTargetYaw = 0f;
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

        public void SetVelocity(Vector3 newVelocity) => _velocity = newVelocity;
        public void ResetJump() => _input.jump = false;

        public void SwitchState(PlayerBaseState newState)
        {
            _currentState.ExitState();
            _currentState = newState;
            _currentState.EnterState();
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        public bool TryGetGripPoint(Vector3? oppositeHandAnchor, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

            // RAFİNE TUTUNMA (SPHERECAST): İğne deliği kadar ince Raycast yerine 20cm kalınlığında SphereCast (Küre) atıyoruz. 
            // Bu sayede kayanın kenarlarına ve köşelerine çok daha yumuşak, manyetik bir hisle tıklayıp tutunabilirsin.
            if (Physics.SphereCast(ray, 0.2f, out RaycastHit hit, _stats.GripReachDistance * 1.5f, _stats.ClimbableLayers))
            {
                float wallAngle = Vector3.Angle(Vector3.up, hit.normal);
                if (wallAngle < 45f || wallAngle > 135f)
                {
                    return false; // Burası yürünecek yer veya tavan, tutunma!
                }
                if (Vector3.Dot(ray.direction, hit.normal) > -0.1f)
                {
                    return false; // İç yüzey koruması devam ediyor
                }

                float distToShoulder = Vector3.Distance(_mainCamera.transform.position, hit.point);
                if (distToShoulder > _stats.GripReachDistance)
                {
                    return false;
                }

                if (oppositeHandAnchor.HasValue)
                {
                    float distBetweenHands = Vector3.Distance(hit.point, oppositeHandAnchor.Value);
                    if (distBetweenHands > _stats.MaxArmSpan)
                    {
                        return false;
                    }
                }

                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }

            return false;
        }

        public void SetControllerEnabled(bool isEnabled) => _controller.enabled = isEnabled;
    }
}