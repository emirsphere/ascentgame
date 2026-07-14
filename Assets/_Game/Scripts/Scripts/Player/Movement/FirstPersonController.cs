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
        private float _pitch;
        private float _freeLookYaw;
        private float _tilt;
        private float _defaultYPos;
        private float _bobTimer;
        private const float GripDebugLogInterval = 0.25f;
        private GripAcquisitionResult _lastLeftGripResult;
        private GripAcquisitionResult _lastRightGripResult;
        private float _nextLeftGripDebugLogTime;
        private float _nextRightGripDebugLogTime;

        private enum GripAcquisitionResult
        {
            NONE,
            INPUT_NOT_HELD,
            SPHERECAST_NO_HIT,
            SURFACE_NORMAL_REJECTED,
            CAMERA_RANGE_REJECTED,
            SHOULDER_REACH_REJECTED,
            OPPOSITE_HAND_SPAN_REJECTED,
            GRIP_ACCEPTED
        }

        public PlayerStats Stats => _stats;
        public PlayerGripSensor Sensor => _sensor;
        public Vector3 Velocity => _velocity;
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

            if (_stats.EnableHeadBob && !IsFreeLook)
                HandleHeadBob();

            UpdateCameraTilt();
            ApplyCameraRotation();
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude < 0.01f) return;

            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
            float lookX = _input.look.x * _stats.RotationSpeed * deltaTimeMultiplier;
            float lookY = _input.look.y * _stats.RotationSpeed * deltaTimeMultiplier;

            _pitch -= lookY;
            _pitch = ClampAngle(_pitch, BottomClamp, TopClamp);

            if (IsFreeLook)
            {
                _freeLookYaw += lookX;
                _freeLookYaw = ClampAngle(_freeLookYaw, -100f, 100f);
            }
            else
            {
                transform.Rotate(Vector3.up * lookX);
            }
        }

        private void ApplyCameraRotation()
        {
            if (_cameraRoot == null) return;
            _cameraRoot.transform.localRotation = Quaternion.Euler(_pitch, _freeLookYaw, _tilt);
        }

        public bool CheckLedgeVault(out Vector3 vaultTarget)
        {
            vaultTarget = Vector3.zero;

            // 1. Zirveyi daha rahat bulması için ışını kafanın biraz daha üstünden atıyoruz (0.5f)
            Vector3 topRayStart = _mainCamera.transform.position + Vector3.up * 0.5f;
            Vector3 forwardDir = _cameraRoot.transform.forward;
            forwardDir.y = 0; // Sadece yatayda ileri bak
            forwardDir.Normalize();

            // 2. İleri yönde duvarı aştık mı? (0.8f ileri)
            if (!Physics.Raycast(topRayStart, forwardDir, 0.8f, _stats.ClimbableLayers))
            {
                // 3. Duvarı aştıysak (boşluktaysak), o boşluktan aşağıya doğru ışın at
                // İleri gitme miktarını ince kayaları ıskalamaması için 0.6f yapıyoruz.
                Vector3 downRayStart = topRayStart + forwardDir * 0.6f;

                // 4. RAYCAST YERİNE SPHERECAST: İnce/sivri kayalarda ıskalamamak için aşağıya kalın bir küre atıyoruz.
                if (Physics.SphereCast(downRayStart, 0.15f, Vector3.down, out RaycastHit hit, 1.5f, _stats.GroundLayers | _stats.ClimbableLayers))
                {
                    // 5. Bulunan yüzey gerçekten basılabilecek kadar düz mü? (Aşırı dik yerlere çıkmayı reddet)
                    float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (slopeAngle < 45f)
                    {
                        float yOffset = _controller.height / 2f;
                        vaultTarget = hit.point + Vector3.up * (yOffset + 0.1f); // 0.1f güvenlik toleransı
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
            transform.Rotate(Vector3.up * _freeLookYaw);
            _freeLookYaw = 0f;
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

        private void UpdateCameraTilt()
        {
            float targetTilt = 0f;

            if (_stats.EnableCameraTilt && !IsFreeLook)
            {
                if (_input.move.x > 0.1f) targetTilt = -_stats.TiltAngle;
                else if (_input.move.x < -0.1f) targetTilt = _stats.TiltAngle;
            }

            _tilt = Mathf.Lerp(_tilt, targetTilt, Time.deltaTime * _stats.TiltSpeed);
        }

        public void SetVelocity(Vector3 newVelocity) => _velocity = newVelocity;
        public void ResetJump() => _input.jump = false;

        public void SimulateClimbingMovement()
        {
            Vector3 velocity = _velocity;
            if (velocity.y > -_stats.TerminalVelocity)
                velocity.y += _stats.ClimbingGravity * Time.deltaTime;

            Vector3 startPosition = transform.position;
            Vector3 predictedPosition = startPosition + velocity * Time.deltaTime;

            for (int iteration = 0; iteration < _stats.ClimbingConstraintIterations; iteration++)
            {
                if (LeftAnchor.HasValue)
                    ConstrainArm(ref predictedPosition, LeftAnchor.Value, -1f);

                if (RightAnchor.HasValue)
                    ConstrainArm(ref predictedPosition, RightAnchor.Value, 1f);
            }

            velocity = (predictedPosition - startPosition) / Time.deltaTime;
            RemoveOutwardArmVelocity(ref velocity, startPosition, predictedPosition, LeftAnchor, -1f);
            RemoveOutwardArmVelocity(ref velocity, startPosition, predictedPosition, RightAnchor, 1f);
            _velocity = velocity;
        }

        private void ConstrainArm(ref Vector3 predictedRootPosition, Vector3 anchor, float side)
        {
            Vector3 shoulder = GetVirtualShoulder(predictedRootPosition, side);
            Vector3 toAnchor = anchor - shoulder;
            float distance = toAnchor.magnitude;
            if (distance <= _stats.MaxArmReach || distance <= Mathf.Epsilon) return;

            predictedRootPosition += toAnchor * ((distance - _stats.MaxArmReach) / distance);
        }

        private void RemoveOutwardArmVelocity(ref Vector3 velocity, Vector3 startRootPosition, Vector3 predictedRootPosition, Vector3? anchor, float side)
        {
            if (!anchor.HasValue) return;

            float startDistance = Vector3.Distance(GetVirtualShoulder(startRootPosition, side), anchor.Value);
            if (startDistance < _stats.MaxArmReach - 0.001f) return;

            Vector3 shoulder = GetVirtualShoulder(predictedRootPosition, side);
            Vector3 fromAnchor = shoulder - anchor.Value;
            float distance = fromAnchor.magnitude;
            if (distance < _stats.MaxArmReach - 0.001f || distance <= Mathf.Epsilon) return;

            Vector3 radialDirection = fromAnchor / distance;
            float outwardSpeed = Vector3.Dot(velocity, radialDirection);
            if (outwardSpeed > 0f)
                velocity -= radialDirection * outwardSpeed;
        }

        private Vector3 GetVirtualShoulder(Vector3 rootPosition, float side)
        {
            return rootPosition
                + Vector3.up * _stats.VirtualShoulderHeight
                + transform.right * (side * _stats.VirtualShoulderHalfWidth);
        }

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

        public bool TryGetGripPoint(float handSide, Vector3? oppositeHandAnchor, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
            bool isLeftHand = handSide < 0f;
            bool isGripHeld = isLeftHand ? _input.leftGrip : _input.rightGrip;
            Debug.DrawRay(ray.origin, ray.direction * (_stats.GripReachDistance * 1.5f), Color.cyan, GripDebugLogInterval);

            if (!isGripHeld)
            {
                LogGripAcquisitionResult(handSide, GripAcquisitionResult.INPUT_NOT_HELD, ray, false, Vector3.zero, 0f, 0f);
                return false;
            }

            // RAFİNE TUTUNMA (SPHERECAST): İğne deliği kadar ince Raycast yerine 20cm kalınlığında SphereCast (Küre) atıyoruz. 
            // Bu sayede kayanın kenarlarına ve köşelerine çok daha yumuşak, manyetik bir hisle tıklayıp tutunabilirsin.
            if (Physics.SphereCast(ray, 0.2f, out RaycastHit hit, _stats.GripReachDistance * 1.5f, _stats.ClimbableLayers))
            {
                float cameraToHitDistance = Vector3.Distance(_mainCamera.transform.position, hit.point);
                float shoulderToHitDistance = Vector3.Distance(GetVirtualShoulder(transform.position, handSide), hit.point);

                if (Vector3.Dot(ray.direction, hit.normal) > -0.1f)
                {
                    LogGripAcquisitionResult(handSide, GripAcquisitionResult.SURFACE_NORMAL_REJECTED, ray, true, hit.point, cameraToHitDistance, shoulderToHitDistance);
                    return false; // İç yüzey koruması devam ediyor
                }

                if (cameraToHitDistance > _stats.GripReachDistance)
                {
                    LogGripAcquisitionResult(handSide, GripAcquisitionResult.CAMERA_RANGE_REJECTED, ray, true, hit.point, cameraToHitDistance, shoulderToHitDistance);
                    return false;
                }

                if (shoulderToHitDistance > _stats.MaxArmReach + _stats.GripReachTolerance)
                {
                    LogGripAcquisitionResult(handSide, GripAcquisitionResult.SHOULDER_REACH_REJECTED, ray, true, hit.point, cameraToHitDistance, shoulderToHitDistance);
                    return false;
                }

                if (oppositeHandAnchor.HasValue)
                {
                    float distBetweenHands = Vector3.Distance(hit.point, oppositeHandAnchor.Value);
                    if (distBetweenHands > _stats.MaxArmSpan)
                    {
                        LogGripAcquisitionResult(handSide, GripAcquisitionResult.OPPOSITE_HAND_SPAN_REJECTED, ray, true, hit.point, cameraToHitDistance, shoulderToHitDistance);
                        return false;
                    }

                }

                hitPoint = hit.point;
                hitNormal = hit.normal;
                LogGripAcquisitionResult(handSide, GripAcquisitionResult.GRIP_ACCEPTED, ray, true, hit.point, cameraToHitDistance, shoulderToHitDistance);
                return true;
            }

            LogGripAcquisitionResult(handSide, GripAcquisitionResult.SPHERECAST_NO_HIT, ray, false, Vector3.zero, 0f, 0f);
            return false;
        }

        private void LogGripAcquisitionResult(float handSide, GripAcquisitionResult result, Ray ray, bool hasHit, Vector3 hitPoint, float cameraToHitDistance, float shoulderToHitDistance)
        {
            bool isLeftHand = handSide < 0f;
            GripAcquisitionResult lastResult = isLeftHand ? _lastLeftGripResult : _lastRightGripResult;
            float nextLogTime = isLeftHand ? _nextLeftGripDebugLogTime : _nextRightGripDebugLogTime;
            if (lastResult == result && Time.unscaledTime < nextLogTime) return;

            if (isLeftHand)
            {
                _lastLeftGripResult = result;
                _nextLeftGripDebugLogTime = Time.unscaledTime + GripDebugLogInterval;
            }
            else
            {
                _lastRightGripResult = result;
                _nextRightGripDebugLogTime = Time.unscaledTime + GripDebugLogInterval;
            }

            Color color = result == GripAcquisitionResult.GRIP_ACCEPTED ? Color.green : Color.red;
            if (hasHit)
            {
                Debug.DrawLine(ray.origin, hitPoint, color, GripDebugLogInterval);
                Debug.DrawLine(GetVirtualShoulder(transform.position, handSide), hitPoint, Color.yellow, GripDebugLogInterval);
            }

            string hand = isLeftHand ? "LEFT" : "RIGHT";
            string hitDetails = hasHit
                ? $"hit={hitPoint}, cameraDistance={cameraToHitDistance:F3}, shoulderDistance={shoulderToHitDistance:F3}"
                : "hit=none, cameraDistance=n/a, shoulderDistance=n/a";
            Debug.Log($"[GripDebug] hand={hand} result={result} origin={ray.origin} direction={ray.direction} {hitDetails} MaxArmReach={_stats.MaxArmReach:F3} GripReachTolerance={_stats.GripReachTolerance:F3} state={_currentState?.GetType().Name}");
        }

        public void SetControllerEnabled(bool isEnabled) => _controller.enabled = isEnabled;
    }
}
