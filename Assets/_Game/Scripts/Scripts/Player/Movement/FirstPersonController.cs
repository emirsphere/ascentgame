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
        private float _cinemachineTargetYaw; // FreeLook için Kafa Dönüşü
        private float _defaultYPos;
        private float _bobTimer;

        // --- IPLAYERCONTROLLER IMPLEMENTATION ---
        public PlayerStats Stats => _stats;
        public PlayerGripSensor Sensor => _sensor;
        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => _horizontalSpeed;
        public Transform PlayerTransform => transform;
        public Transform CameraTransform => _mainCamera.transform;

        // Bağımsız Eller
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

            // KRİTİK ÇÖZÜM: Tırmanırken CharacterController'ı devre dışı bırakıp hareketi manuel devralıyoruz.
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
                    // Asılıyken gövde dönmez, sadece kafa (yaw) döner.
                    _cinemachineTargetYaw += lookX;
                    _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, -120f, 120f); // Boyun kırma sınırı
                }
                else
                {
                    // Normal yürüme: Fare gövdeyi çevirir.
                    transform.Rotate(Vector3.up * lookX);
                }

                if (_cameraRoot != null)
                {
                    _cameraRoot.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
                }
            }
        }

        public void ResetFreeLook()
        {
            if (!IsFreeLook) return;
            IsFreeLook = false;
            // Serbest bakıştan düşerken, gövdeyi kafanın baktığı yere hizala
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

            // 1. Işın tam kameranın baktığı yere (Crosshair) atılıyor
            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

            // 2. Çarpışma kontrolü (Menzili biraz uzun tutuyoruz ki tolerans olsun)
            if (Physics.Raycast(ray, out RaycastHit hit, _stats.GripReachDistance * 1.5f, _stats.ClimbableLayers))
            {
                // 3. DÜZELTİLMİŞ MESAFE KONTROLÜ: 
                // Mesafeyi karakterin merkezinden (göbek/ayak) değil, kameradan (omuz hizası) ölçüyoruz.
                float distToShoulder = Vector3.Distance(_mainCamera.transform.position, hit.point);
                if (distToShoulder > _stats.GripReachDistance)
                {
                    // Gövdeden çok uzakta, yetişemez
                    return false;
                }

                // 4. MEKANİK KİLİT: Tek elle tırmanmayı engelleyen "Kol Açıklığı" kuralı
                if (oppositeHandAnchor.HasValue)
                {
                    float distBetweenHands = Vector3.Distance(hit.point, oppositeHandAnchor.Value);
                    if (distBetweenHands > _stats.MaxArmSpan)
                    {
                        // Diğer el çok uzakta kaldı, kollar kopamaz!
                        return false;
                    }
                }

                // Her şey geçerli, noktayı onayla.
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }

            // Hiçbir şeye çarpmadı
            return false;
        }
        public void SetControllerEnabled(bool isEnabled) => _controller.enabled = isEnabled;
    }
}