using UnityEngine;

public class PlayerHangState : PlayerBaseState
{
    public PlayerHangState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(true);
        _ctx.IsFreeLook = true;

        // Grounded state's stick velocity is not useful hanging momentum.
        if (_ctx.Sensor.IsGrounded)
        {
            Vector3 velocity = _ctx.Velocity;
            velocity.y = Mathf.Max(0f, velocity.y);
            _ctx.SetVelocity(velocity);
        }
    }

    public override void UpdateState()
    {
        HandleGripLogic();
        if (TrySwitchStates()) return;

        // Hang has exactly one anchor. The controller solves that arm's reach constraint.
        _ctx.SimulateClimbingMovement();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        TrySwitchStates();
    }

    private bool TrySwitchStates()
    {
        if (_ctx.MoveInput.y > _ctx.Stats.ClimbInputThreshold && _ctx.CheckLedgeVault(out _))
        {
            _ctx.SwitchState(_factory.Vault);
            return true;
        }

        if (_ctx.LeftAnchor != null && _ctx.RightAnchor != null)
        {
            _ctx.SwitchState(_factory.Climb);
            return true;
        }

        if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
            return true;
        }

        return false;
    }

    private void HandleGripLogic()
    {
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(-1f, _ctx.RightAnchor, out Vector3 point, out Vector3 normal))
                _ctx.SetLeftAnchor(point, normal);
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(1f, _ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
                _ctx.SetRightAnchor(point, normal);
        }
    }
}
