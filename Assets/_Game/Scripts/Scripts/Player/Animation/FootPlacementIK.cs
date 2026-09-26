using UnityEngine;
using UnityEngine.Animations.Rigging;
using StarterAssets;
public class FootPlacementIK : MonoBehaviour
{
    [Header("Oyuncu ve IK Bileşenleri")]

    private IPlayerController player;
    public TwoBoneIKConstraint leftFootIK;
    public TwoBoneIKConstraint rightFootIK;

    [Header("IK Targets")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Animated Bones")]
    public Transform leftFootBone;
    public Transform rightFootBone;

    [Header("Ayarlar")]
    public LayerMask Ground;
    public float rayOriginOffset = 1.0f;
    public float rayDistance = 1.5f;
    public float footOffset = 0.02f; // Topuk havada kalmasın diye iyice düşürdük      
    public float adjustmentSpeed = 20f;

    // YENİ: Editörde ayarladığın kusursuz düz açıları hafızada tutacağız
    private Quaternion leftFlatRotation;
    private Quaternion rightFlatRotation;
    private void Awake()
    {
        player = GetComponentInParent<IPlayerController>();
        // Oyun başlarken senin o Target'lara verdiğin düz rotasyonları kaydet

        if (leftFootTarget != null) leftFlatRotation = leftFootTarget.localRotation;
        if (rightFootTarget != null) rightFlatRotation = rightFootTarget.localRotation;

    }



    private void LateUpdate()

    {

        if (leftFootBone == null || leftFootTarget == null || player == null) return;



        bool shouldDisableIK = player.IsClimbing || player.HorizontalSpeed > 0.1f;

        float targetWeight = shouldDisableIK ? 0f : 1f;



        leftFootIK.weight = Mathf.Lerp(leftFootIK.weight, targetWeight, Time.deltaTime * 15f);

        rightFootIK.weight = Mathf.Lerp(rightFootIK.weight, targetWeight, Time.deltaTime * 15f);



        if (leftFootIK.weight < 0.01f && rightFootIK.weight < 0.01f) return;



        AdjustFoot(leftFootBone, leftFootTarget);

        AdjustFoot(rightFootBone, rightFootTarget);

    }



    private void AdjustFoot(Transform bone, Transform target)

    {

        Vector3 targetPos = bone.position;



        // KRİTİK DEĞİŞİKLİK: Animasyonun (bone) yamuk rotasyonunu sildik.

        // Hangi ayaksa onun bizim kaydettiğimiz düz yerel açısını al, karakterin yönüyle çarp

        Quaternion savedLocalRot = (target == leftFootTarget) ? leftFlatRotation : rightFlatRotation;

        Quaternion flatWorldRot = target.parent.rotation * savedLocalRot;



        Quaternion targetRot = flatWorldRot;



        Vector3 rayOrigin = bone.position + (Vector3.up * rayOriginOffset);



        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, Ground))

        {

            if (hit.point.y + footOffset > bone.position.y)

            {

                targetPos.y = hit.point.y + footOffset;

                // Düz taban açımızı alıp zemin eğimine (yokuş/merdiven) göre büküyoruz

                Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                targetRot = slopeRotation * flatWorldRot;

            }

        }



        target.position = Vector3.Lerp(target.position, targetPos, Time.deltaTime * adjustmentSpeed);

        target.rotation = Quaternion.Slerp(target.rotation, targetRot, Time.deltaTime * adjustmentSpeed);

    }

}