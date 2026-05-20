using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(GripManager))]
public class ClimbBodySolver : MonoBehaviour
{
    [Header("Constraint")]
    [SerializeField] private float _wallDistance = 0.75f;
    [SerializeField] private float _verticalOffset = 1.25f;

    [Header("Spring")]
    [SerializeField] private float _stiffness = 45f;
    [SerializeField] private float _damping = 14f;
    [SerializeField] private float _maxClimbSpeed = 8f;

    [Header("Stability")]
    [SerializeField] private float _snapDistance = 0.025f;
    [SerializeField] private float _velocityDeadzone = 0.02f;

    [Header("Input Influence")]
    [SerializeField] private float _inputResponsiveness = 8f;
    [SerializeField] private float _verticalInfluence = 0.45f;
    [SerializeField] private float _horizontalInfluence = 0.35f;

    private CharacterController _characterController;
    private GripManager _gripManager;
    private Vector3 _solverVelocity;
    private ClimbInputData _inputData;
    private Vector2 _smoothedInput;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _gripManager = GetComponent<GripManager>();
    }

    public void SetInput(ClimbInputData inputData)
    {
        _inputData = inputData;
    }

    private void LateUpdate()
    {
        if (!TryBuildTarget(out Vector3 targetPosition, out Vector3 wallNormal))
        {
            _solverVelocity = Vector3.zero;
            _smoothedInput = Vector2.zero;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= Mathf.Epsilon)
            return;

        Vector2 targetInput = new Vector2(_inputData.horizontalIntent, _inputData.verticalIntent);
        _smoothedInput = Vector2.Lerp(_smoothedInput, targetInput, dt * _inputResponsiveness);
        targetPosition += BuildInputOffset(wallNormal);

        Vector3 displacement = targetPosition - transform.position;
        float snapDistanceSq = _snapDistance * _snapDistance;
        float velocityDeadzoneSq = _velocityDeadzone * _velocityDeadzone;

        if (displacement.sqrMagnitude <= snapDistanceSq && _solverVelocity.sqrMagnitude <= velocityDeadzoneSq)
        {
            _solverVelocity = Vector3.zero;
            return;
        }

        Vector3 springForce = displacement * _stiffness;
        Vector3 dampingForce = -_solverVelocity * _damping;

        _solverVelocity += (springForce + dampingForce) * dt;
        _solverVelocity = Vector3.ClampMagnitude(_solverVelocity, _maxClimbSpeed);

        if (_solverVelocity.sqrMagnitude <= velocityDeadzoneSq && displacement.sqrMagnitude <= snapDistanceSq)
            _solverVelocity = Vector3.zero;

        _characterController.Move(_solverVelocity * dt);
    }

    private bool TryBuildTarget(out Vector3 targetPosition, out Vector3 wallNormal)
    {
        targetPosition = default;
        wallNormal = Vector3.forward;

        int activeCount = 0;
        Vector3 center = Vector3.zero;
        Vector3 normalSum = Vector3.zero;

        AccumulateAnchor(_gripManager.LeftHandAnchor, ref activeCount, ref center, ref normalSum);
        AccumulateAnchor(_gripManager.RightHandAnchor, ref activeCount, ref center, ref normalSum);

        if (activeCount == 0)
            return false;

        center /= activeCount;
        wallNormal = normalSum.sqrMagnitude > 0.001f
            ? normalSum.normalized
            : Vector3.forward;

        targetPosition = center
            - wallNormal * _wallDistance
            - Vector3.up * _verticalOffset;

        return true;
    }

    private Vector3 BuildInputOffset(Vector3 wallNormal)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, wallNormal);
        if (lateral.sqrMagnitude < 0.001f)
            lateral = transform.right;
        else
            lateral.Normalize();

        Vector3 verticalOffset = Vector3.up * (_smoothedInput.y * _verticalInfluence);
        Vector3 horizontalOffset = lateral * (_smoothedInput.x * _horizontalInfluence);
        return verticalOffset + horizontalOffset;
    }

    private static void AccumulateAnchor(
        HandAnchor anchor,
        ref int activeCount,
        ref Vector3 center,
        ref Vector3 normalSum)
    {
        if (!anchor.isActive)
            return;

        activeCount++;
        center += anchor.position;
        normalSum += anchor.normal;
    }
}
