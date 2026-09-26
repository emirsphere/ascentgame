using UnityEngine;
using UnityEngine.Animations.Rigging;

public class ClimbingHandIK : MonoBehaviour
{
    [Header("IK References")]
    public TwoBoneIKConstraint IKConstraint;
    public Transform Target;
    public Transform Hint;
    public Transform ActualHandBone;
    public Transform ActualElbowBone;
    public Transform ShoulderBone;

    [Header("Grip Adjustments")]
    public float WeightLerpSpeed = 15f;
    public float SurfaceDepthOffset = 0.0f;
    public Vector3 RotationOffset;
    public float MaxReachDistance = 1.5f;
    public bool IsRightHand;

    [Header("Elbow Tweak (Play Modunda Ayarla)")]
    [Tooltip("X: Dışa Açıklık, Y: Yukarı/Aşağı, Z: Duvardan Geriye Çekme")]
    public Vector3 ElbowOffset = new Vector3(0.5f, -0.2f, 0.4f);

    private Vector3? _currentAnchor = null;
    private Vector3 _wallNormal = Vector3.forward;

    public void SetGrip(Vector3? point, Vector3 normal)
    {
        _currentAnchor = point;
        _wallNormal = normal;
    }

    private void LateUpdate()
    {
        if (IKConstraint == null || Target == null || Hint == null || ActualHandBone == null || ShoulderBone == null)
            return;

        bool isTargetTooFar = false;
        if (_currentAnchor.HasValue)
        {
            float distanceToTarget = Vector3.Distance(ShoulderBone.position, _currentAnchor.Value);
            if (distanceToTarget > MaxReachDistance) isTargetTooFar = true;
        }

        if (_currentAnchor.HasValue && !isTargetTooFar)
        {
            IKConstraint.weight = Mathf.Lerp(IKConstraint.weight, 1f, Time.deltaTime * WeightLerpSpeed);
            Target.position = _currentAnchor.Value - (_wallNormal * SurfaceDepthOffset);

            Quaternion wallAlignment = Quaternion.LookRotation(-_wallNormal, Vector3.up);
            Vector3 finalRotationOffset = RotationOffset;
            if (!IsRightHand) finalRotationOffset = new Vector3(RotationOffset.x, -RotationOffset.y, -RotationOffset.z);
            Target.rotation = wallAlignment * Quaternion.Euler(finalRotationOffset);

            // --- YENİ DİRSEK SİSTEMİ: TAM KONTROL ---
            // Omuz ve elin tam orta noktasını bul (Dirsek anatomik olarak buralardadır)
            Vector3 midPoint = Vector3.Lerp(ShoulderBone.position, Target.position, 0.5f);

            // Duvarın sağı/solu
            Vector3 wallRight = Vector3.Cross(_wallNormal, Vector3.up).normalized;
            Vector3 sideDir = IsRightHand ? wallRight : -wallRight;

            // Dirseği senin Inspector'dan gireceğin X, Y, Z ofsetlerine göre konumlandır
            Vector3 hintPos = midPoint
                            + (sideDir * ElbowOffset.x)    // X: Dirseği yanlara aç/kapat
                            + (Vector3.up * ElbowOffset.y) // Y: Dirseği havaya kaldır / aşağı indir
                            + (_wallNormal * ElbowOffset.z); // Z: Dirseği duvardan geriye (sırta doğru) çek

            Hint.position = hintPos;
        }
        else
        {
            IKConstraint.weight = Mathf.Lerp(IKConstraint.weight, 0f, Time.deltaTime * WeightLerpSpeed);
            if (IKConstraint.weight < 0.95f)
            {
                Target.position = ActualHandBone.position;
                Target.rotation = ActualHandBone.rotation;
                if (ActualElbowBone != null) Hint.position = ActualElbowBone.position;
            }
        }
    }
}