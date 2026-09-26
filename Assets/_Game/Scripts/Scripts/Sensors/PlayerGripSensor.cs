using UnityEngine;

namespace Ascent.Player.Sensors
{
    public enum GroundState
    {
        Airborne,
        Grounded,
        Sliding
    }

    public class PlayerGripSensor : MonoBehaviour
    {
        [Header("References")]
        public CapsuleCollider Capsule;
        public Transform CameraTransform;
        public PlayerStats Stats;
        public StarterAssets.FirstPersonController Controller; // LogEvent'e ulaşmak için

        [Header("Ground Detection")]
        public float MaxWalkableSlope = 45f;
        public float GroundCheckDistance = 0.25f;

        public GroundState CurrentGroundState { get; private set; }

        // Geriye dönük uyumluluk için property'ler
        public bool IsGrounded => CurrentGroundState == GroundState.Grounded;
        public bool IsSliding => CurrentGroundState == GroundState.Sliding;

        public Vector3 GroundNormal { get; private set; }
        public Vector3 GroundPoint { get; private set; }

        [Header("Grip Detection")]
        public bool CanGrip { get; private set; }
        public RaycastHit GripHit { get; private set; }

        private RaycastHit _groundHit;
        private float _gripBufferTimer;
        private GroundState _previousGroundState = GroundState.Airborne;

        private void Awake()
        {
            if (Controller == null) Controller = GetComponent<StarterAssets.FirstPersonController>();
        }

        // Fizik motoruyla %100 senkron olması için FixedUpdate'e taşındı
        private void FixedUpdate()
        {
            EvaluateGround();
            EvaluateGripTarget();
        }

        private void EvaluateGround()
        {
            if (Capsule == null || Stats == null) return;

            // Kapsülün tam ayak tabanını hesaplıyoruz
            Vector3 capsuleBottom = transform.position + Capsule.center - (Vector3.up * (Capsule.height / 2f));
            Vector3 sphereOrigin = capsuleBottom + (Vector3.up * Capsule.radius);

            // Yanal çarpmalarda (duvar vs.) yanlış ground vermemesi için yarıçap toleransı
            float safeRadius = Capsule.radius * 0.85f;

            bool hitGround = Physics.SphereCast(
                sphereOrigin,
                safeRadius,
                Vector3.down,
                out _groundHit,
                GroundCheckDistance,
                Stats.GroundLayers | Stats.ClimbableLayers
            );

            if (hitGround)
            {
                GroundNormal = _groundHit.normal;
                GroundPoint = _groundHit.point;
                float slopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                if (slopeAngle <= MaxWalkableSlope)
                {
                    CurrentGroundState = GroundState.Grounded;
                }
                else
                {
                    CurrentGroundState = GroundState.Sliding;
                }
            }
            else
            {
                CurrentGroundState = GroundState.Airborne;
                GroundNormal = Vector3.up;
            }

            // Olay Bazlı Loglama: Sadece state değiştiğinde Console'a yazar.
            if (_previousGroundState != CurrentGroundState)
            {
                if (Controller != null)
                {
                    Controller.LogEvent("GROUND", $"State changed: {_previousGroundState} -> {CurrentGroundState} | Slope: {Vector3.Angle(Vector3.up, GroundNormal):F1}°");
                }
                _previousGroundState = CurrentGroundState;
            }
        }

        private void EvaluateGripTarget()
        {
            if (CameraTransform == null || Stats == null) return;

            bool hitWall = Physics.SphereCast(CameraTransform.position, 0.15f, CameraTransform.forward, out RaycastHit hit, Stats.GripReachDistance, Stats.ClimbableLayers);

            if (hitWall)
            {
                // FİZİKSEL UZANMA SINIRI: Karakterin ayak tabanından max 4.2 metre yukarısını tutabilir.
                float maxPhysicalReach = transform.position.y + 4.2f;

                float wallAngle = Vector3.Angle(Vector3.up, hit.normal);

                // Şarta 'hit.point.y <= maxPhysicalReach' eklendi.
                if (wallAngle >= 25f && wallAngle <= 155f && hit.point.y <= maxPhysicalReach)
                {
                    CanGrip = true;
                    GripHit = hit;
                    _gripBufferTimer = Stats.GripBufferTime;
                    return;
                }
            }

            if (_gripBufferTimer > 0)
            {
                _gripBufferTimer -= Time.fixedDeltaTime;
            }
            else
            {
                CanGrip = false;
            }
        }

        // FAZ 18: Scene ekranında sensörleri görselleştirme
        private void OnDrawGizmosSelected()
        {
            if (Capsule == null) return;

            Vector3 capsuleBottom = transform.position + Capsule.center - (Vector3.up * (Capsule.height / 2f));
            Vector3 sphereOrigin = capsuleBottom + (Vector3.up * Capsule.radius);

            // Renk kodlaması: Yeşil (Grounded), Sarı (Sliding), Kırmızı (Airborne)
            Gizmos.color = IsGrounded ? Color.green : (IsSliding ? Color.yellow : Color.red);
            Gizmos.DrawWireSphere(sphereOrigin + Vector3.down * GroundCheckDistance, Capsule.radius * 0.85f);

            if (CurrentGroundState != GroundState.Airborne)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(GroundPoint, GroundNormal);
            }
        }
    }
}