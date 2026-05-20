using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSurfaceSensor))]
[RequireComponent(typeof(StarterAssetsInputs))]
public class GripManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private StarterAssetsInputs _input;
    [SerializeField] private float _anchorGizmoRadius = 0.08f;

    private PlayerSurfaceSensor _surfaceSensor;
    private bool _wasLeftGripHeld;
    private bool _wasRightGripHeld;

    public HandAnchor LeftHandAnchor { get; } = new HandAnchor();
    public HandAnchor RightHandAnchor { get; } = new HandAnchor();
    public bool HasActiveAnchor => LeftHandAnchor.isActive || RightHandAnchor.isActive;

    private void Awake()
    {
        _surfaceSensor = GetComponent<PlayerSurfaceSensor>();
        if (_input == null)
            _input = GetComponent<StarterAssetsInputs>();
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (_input == null || _camera == null || _surfaceSensor == null)
            return;

        HandleHandInput(
            _input.leftGrip,
            ref _wasLeftGripHeld,
            LeftHandAnchor);

        HandleHandInput(
            _input.rightGrip,
            ref _wasRightGripHeld,
            RightHandAnchor);
    }

    private void HandleHandInput(bool isHeld, ref bool wasHeld, HandAnchor anchor)
    {
        if (isHeld && !wasHeld)
            TryAcquire(anchor);
        else if (!isHeld && wasHeld)
            anchor.Clear();

        wasHeld = isHeld;
    }

    public void ClearAllAnchors()
    {
        LeftHandAnchor.Clear();
        RightHandAnchor.Clear();
    }

    private void TryAcquire(HandAnchor anchor)
    {
        if (anchor.isLocked)
            return;

        Transform cameraTransform = _camera.transform;
        if (!_surfaceSensor.TryProbeGripAnchor(
                cameraTransform.position,
                cameraTransform.forward,
                out Vector3 point,
                out Vector3 normal))
        {
            return;
        }

        anchor.Lock(point, normal, Time.frameCount);
    }

    private void OnDrawGizmos()
    {
        DrawAnchorGizmo(LeftHandAnchor, -transform.right * 0.25f);
        DrawAnchorGizmo(RightHandAnchor, transform.right * 0.25f);
    }

    private void DrawAnchorGizmo(HandAnchor anchor, Vector3 inactiveOffset)
    {
        Gizmos.color = anchor.isLocked ? Color.green : Color.red;

        if (!anchor.isLocked)
        {
            Gizmos.DrawWireSphere(transform.position + inactiveOffset, _anchorGizmoRadius);
            return;
        }

        Gizmos.DrawSphere(anchor.position, _anchorGizmoRadius);
        Gizmos.DrawLine(transform.position, anchor.position);
        Gizmos.DrawRay(anchor.position, anchor.normal * 0.35f);
    }
}
