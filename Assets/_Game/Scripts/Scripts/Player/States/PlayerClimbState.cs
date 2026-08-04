using UnityEngine;

public class PlayerClimbState : PlayerBaseState
{
    private float _currentOffset;
    private float _currentWallDist;
    private Vector3 _currentVelocity;
    private float _springDamping;

    public PlayerClimbState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(false);
        _ctx.IsFreeLook = true;
        _currentVelocity = _ctx.Velocity;
        _currentOffset = _ctx.Stats.RestOffset;
        _currentWallDist = _ctx.Stats.BaseWallDistance;
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
    }

    public override void UpdateState()
    {
        if (_ctx.MoveInput.y > 0.1f && _ctx.CheckLedgeVault(out Vector3 targetPos))
        {
            _ctx.VaultTargetPos = targetPos; // Hedefi FirstPersonController hafızasına yaz
            _ctx.SwitchState(_factory.Vault);
            return; // State değiştiği için fonksiyonu burada kes
        }
        HandleGripLogic();
        CheckSwitchStates();
        HandleTwoHandedPhysics();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
        }
        else if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null)
        {
            _ctx.SwitchState(_factory.Hang);
        }

        else if (_ctx.Sensor.IsGrounded)
        {
            _ctx.SwitchState(_factory.Grounded);
            return;
        }



        else if (_ctx.JumpInput)
        {
            _ctx.ResetFreeLook();
            _ctx.ResetJump();
            Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;
            Vector3 jumpDir = (averageNormal * _ctx.Stats.ClimbJumpNormalScale + Vector3.up * _ctx.Stats.ClimbJumpUpScale).normalized;

            _ctx.SetLeftAnchor(null, Vector3.zero);
            _ctx.SetRightAnchor(null, Vector3.zero);

            _ctx.SetVelocity(_currentVelocity * _ctx.Stats.ClimbJumpVelocityRetain + jumpDir * _ctx.Stats.ClimbJumpImpulse);
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void HandleGripLogic()
    {
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetLeftAnchor(point, normal);
            }
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetRightAnchor(point, normal);
            }
        }
    }

    private void HandleTwoHandedPhysics()
    {
        if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null) return;

        PlayerStats stats = _ctx.Stats;
        Vector3 averagePivot = (_ctx.LeftAnchor.Value + _ctx.RightAnchor.Value) * 0.5f;
        Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;

        float verticalInput = _ctx.MoveInput.y;
        float targetOffset = stats.RestOffset;
        float targetWallDist = stats.BaseWallDistance;

        if (verticalInput > stats.ClimbInputThreshold)
        {
            targetOffset = stats.PullOffset;
            targetWallDist = stats.BaseWallDistance * stats.PullWallDistanceMultiplier;
        }
        else if (verticalInput < -stats.ClimbInputThreshold)
        {
            targetWallDist = stats.LeanWallDistance;
        }

        _currentOffset = Mathf.Lerp(_currentOffset, targetOffset, Time.deltaTime * stats.MuscleSpeed);
        _currentWallDist = Mathf.Lerp(_currentWallDist, targetWallDist, Time.deltaTime * stats.MuscleSpeed);

        Vector3 desiredPosition = averagePivot + (Vector3.down * _currentOffset) + (averageNormal * _currentWallDist);

        Vector3 displacement = _ctx.PlayerTransform.position - desiredPosition;

        // KORUMA: Çift elde de çok uzaklaşırsak bırak.
        if (displacement.magnitude > 4.0f)
        {
            _ctx.SetLeftAnchor(null, Vector3.zero);
            _ctx.SetRightAnchor(null, Vector3.zero);
            return;
        }

        Vector3 springForce = -stats.SpringStiffness * displacement;
        Vector3 dampingForce = -_springDamping * _currentVelocity;

        _currentVelocity += (springForce + dampingForce) * Time.deltaTime;

        if (_currentVelocity.sqrMagnitude < stats.ClimbSnapThreshold && displacement.sqrMagnitude < stats.ClimbSnapThreshold)
        {
            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 10f);
        }

        _ctx.SetVelocity(_currentVelocity);
    }
}