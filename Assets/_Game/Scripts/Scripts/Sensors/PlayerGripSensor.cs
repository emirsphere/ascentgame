using UnityEngine;

namespace Ascent.Player.Sensors
{
    public class PlayerGripSensor : MonoBehaviour
    {
        public CharacterController Controller;
        public Transform CameraTransform;
        public PlayerStats Stats;

        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public Vector3 GroundNormal { get; private set; }

        public bool CanGrip { get; private set; }
        public RaycastHit GripHit { get; private set; }

        private RaycastHit _groundHit;
        private float _gripBufferTimer; // YENİ: Coyote Time sayacı

        private void Update()
        {
            EvaluateGround();
            EvaluateGripTarget();
        }

        private void EvaluateGround()
        {
            if (Controller == null || Stats == null) return;

            Vector3 sphereOrigin = transform.position + (Vector3.up * Controller.radius);

            // KRİTİK DÜZELTME BURASI: Sadece GroundLayers'a değil, ClimbableLayers'a da (Kayalara) çarpıp çarpmadığını kontrol et.
            bool hitGround = Physics.SphereCast(sphereOrigin, Controller.radius, Vector3.down, out _groundHit, 0.3f, Stats.GroundLayers | Stats.ClimbableLayers);

            if (hitGround)
            {
                GroundNormal = _groundHit.normal;
                float slopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
                // Unity'nin kendi CharacterController içindeki slopeLimit'ini baz alıyoruz
                IsGrounded = slopeAngle <= Controller.slopeLimit;
                IsSliding = !IsGrounded;
            }
            else
            {
                IsGrounded = false;
                IsSliding = false;
                GroundNormal = Vector3.up;
            }
        }

        private void EvaluateGripTarget()
        {
            if (CameraTransform == null || Stats == null) return;

            bool hitWall = Physics.SphereCast(CameraTransform.position, 0.15f, CameraTransform.forward, out RaycastHit hit, Stats.GripReachDistance, Stats.ClimbableLayers);

            if (hitWall)
            {
                float wallAngle = Vector3.Angle(Vector3.up, hit.normal);
                if (wallAngle >= 25f && wallAngle <= 155f)
                {
                    CanGrip = true;
                    GripHit = hit;
                    _gripBufferTimer = Stats.GripBufferTime; // Zemin bulunduğunda sayacı fulle
                    return;
                }
            }

            // Hata Toleransı (Coyote Time): Yüzeyden anlık kopmalarda anında düşürme.
            if (_gripBufferTimer > 0)
            {
                _gripBufferTimer -= Time.deltaTime;
                // CanGrip = true olarak kalmaya devam eder, GripHit son geçerli pozisyonu tutar.
            }
            else
            {
                CanGrip = false;
            }
        }
    }
}