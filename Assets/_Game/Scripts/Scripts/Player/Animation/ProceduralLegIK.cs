using UnityEngine;
using UnityEngine.Animations.Rigging;
using StarterAssets;

public class ProceduralLegIK : MonoBehaviour
{
    [Header("IK Referansları")]
    public TwoBoneIKConstraint IKConstraint;
    public Transform Target;
    public Transform Hint;
    public Transform Pelvis;
    public Transform ActualFootBone;
    public FirstPersonController PlayerController;

    [Header("El-Bacak Senkronizasyonu")]
    public float HandToFootDistance = 1.4f;
    public float StepDistance = 0.35f;
    public float LerpSpeed = 15f;

    [Header("Ayak Pozisyon Ayarları")]
    public bool IsRightLeg;
    public float FootYOffset = -0.6f;
    public float FootXOffset = 0.25f;
    public float WallOffset = 0.1f;

    [Header("Diz (Hint) Ayarları")]
    public Vector3 KneeOffset = new Vector3(0.5f, -0.2f, 0.4f);

    public LayerMask ClimbableLayers;

    private Vector3 _currentStepPos;
    private Quaternion _naturalFootRotation;

    private void Start()
    {
        if (Target != null) _currentStepPos = Target.position;

        if (ActualFootBone != null && PlayerController != null)
        {
            _naturalFootRotation = Quaternion.Inverse(PlayerController.transform.rotation) * ActualFootBone.rotation;
        }
    }

    private void Update()
    {
        if (IKConstraint == null || Target == null || Hint == null || Pelvis == null || PlayerController == null || ActualFootBone == null)
            return;

        if (PlayerController.IsClimbing)
        {
            Transform playerT = PlayerController.transform;

            Vector3? handAnchor = IsRightLeg ? PlayerController.RightAnchor : PlayerController.LeftAnchor;

            Vector3 rayOrigin = Pelvis.position;
            Vector3 sideDir = IsRightLeg ? playerT.right : -playerT.right;

            if (handAnchor.HasValue)
            {
                rayOrigin.y = handAnchor.Value.y - HandToFootDistance;
            }
            else
            {
                rayOrigin.y = Pelvis.position.y + FootYOffset;
            }

            rayOrigin += sideDir * FootXOffset;

            if (Physics.Raycast(rayOrigin, playerT.forward, out RaycastHit hit, 1.5f, ClimbableLayers))
            {
                IKConstraint.weight = Mathf.Lerp(IKConstraint.weight, 1f, Time.deltaTime * LerpSpeed);

                Vector3 desiredPos = hit.point + (hit.normal * WallOffset);

                if (Vector3.Distance(_currentStepPos, desiredPos) > StepDistance)
                {
                    _currentStepPos = desiredPos;
                }

                Target.position = Vector3.Lerp(Target.position, _currentStepPos, Time.deltaTime * LerpSpeed);

                Quaternion wallRot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                Target.rotation = wallRot * _naturalFootRotation;

                // --- KESİN DİZ YÖNÜ ÇÖZÜMÜ (TWIST / İÇE DÖNME ENGELLEYİCİ) ---
                Vector3 legMidPoint = Vector3.Lerp(Pelvis.position, Target.position, 0.5f);

                Vector3 forwardDir = playerT.forward;
                Vector3 localSideDir = IsRightLeg ? playerT.right : -playerT.right;

                // Diz hedefini orta noktadan kesinlikle pozitif eksenlerde sabitliyoruz (İçeri bükülme ihtimali kalmaz)
                Vector3 hintPos = legMidPoint
                                + (forwardDir * Mathf.Abs(KneeOffset.z))
                                + (localSideDir * Mathf.Abs(KneeOffset.x))
                                + (Vector3.up * KneeOffset.y);

                Hint.position = hintPos;
                return;
            }
        }

        IKConstraint.weight = Mathf.Lerp(IKConstraint.weight, 0f, Time.deltaTime * LerpSpeed);
    }
}