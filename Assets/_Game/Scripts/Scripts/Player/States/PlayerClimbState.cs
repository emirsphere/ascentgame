using UnityEngine;

public class PlayerClimbState : PlayerBaseState
{
    public PlayerClimbState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(true);
        _ctx.IsFreeLook = true;
    }

    public override void UpdateState()
    {
        HandleGripLogic();
        if (TrySwitchStates()) return;

        // The controller applies left and right arm reach constraints independently.
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

        if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
            return true;
        }

        if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null)
        {
            _ctx.SwitchState(_factory.Hang);
            return true;
        }

        if (_ctx.JumpInput)
        {
            _ctx.ResetFreeLook();
            _ctx.ResetJump();
            Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;
            Vector3 jumpDirection = (averageNormal * _ctx.Stats.ClimbJumpNormalScale + Vector3.up * _ctx.Stats.ClimbJumpUpScale).normalized;

            _ctx.SetLeftAnchor(null, Vector3.zero);
            _ctx.SetRightAnchor(null, Vector3.zero);
            _ctx.SetVelocity(_ctx.Velocity * _ctx.Stats.ClimbJumpVelocityRetain + jumpDirection * _ctx.Stats.ClimbJumpImpulse);
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
