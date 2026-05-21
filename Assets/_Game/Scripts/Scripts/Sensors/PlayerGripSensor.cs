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

        private void Update()
        {
            EvaluateGround();
            EvaluateGripTarget();
        }

        private void EvaluateGround()
        {
            if (Controller == null || Stats == null) return;

            Vector3 sphereOrigin = transform.position + (Vector3.up * Controller.radius);

            // Eğer ışın Stats.GroundLayers maskesine (Level Designer'ın Ground yaptığı yere) çarpıyorsa...
            bool hitGround = Physics.SphereCast(sphereOrigin, Controller.radius, Vector3.down, out _groundHit, 0.3f, Stats.GroundLayers);

            if (hitGround)
            {
                GroundNormal = _groundHit.normal;
                float slopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                // Koyduğun o görünmez Ground collider'ı düzse (açısı limitin altındaysa) direkt zemin sayar.
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

            // Farenin merkezinden ufak bir SphereCast atıyoruz.
            // SADECE Stats.ClimbableLayers maskesindeki objeleri tarar.
            bool hitWall = Physics.SphereCast(CameraTransform.position, 0.15f, CameraTransform.forward, out RaycastHit hit, Stats.GripReachDistance, Stats.ClimbableLayers);

            if (hitWall)
            {
                float wallAngle = Vector3.Angle(Vector3.up, hit.normal);

                // MÜHENDİSLİK KARARI (Tasarımcıya Güven): 
                // Madem Level Designer (Sen) buraya açıkça "Climbable" layer'ı atadı,
                // kodun çok katı bir açı kontrolü yapıp işi bozmasını engelliyoruz.
                // Sadece tam zemin (<25 derece) veya tam tavan (>155 derece) değilse tırmanmaya izin ver.
                if (wallAngle >= 25f && wallAngle <= 155f)
                {
                    CanGrip = true;
                    GripHit = hit;
                    return;
                }
            }

            CanGrip = false;
        }
    }
}