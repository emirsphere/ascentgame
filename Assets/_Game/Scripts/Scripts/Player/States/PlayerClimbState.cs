using UnityEngine;

public class PlayerClimbState : PlayerBaseState
{
    private Vector3 _activeWallNormal = Vector3.zero;

    public PlayerClimbState(IPlayerController currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetVelocity(Vector3.zero);
        Debug.Log("[PlayerClimbState] Climb state entered.");
    }

    public override void UpdateState()
    {
        CheckSwitchStates();

        if (!IsGrabbing())
            return;

        UpdateActiveWallNormal();
        _ctx.SetVelocity(Vector3.zero);
        _ctx.SetClimbSolverInput(_ctx.ClimbInput);
    }

    public override void ExitState()
    {
        Debug.Log("[PlayerClimbState] Climb state exited.");
    }

    public override void CheckSwitchStates()
    {
        if (!IsGrabbing())
        {
            _ctx.SwitchState(_factory.Air);
            return;
        }

        if (_ctx.ClimbInput.jumpOff)
        {
            PlayerStats stats = _ctx.Stats;
            _ctx.ResetJump();
            _ctx.ClearGripAnchors();

            Vector3 jumpDir = (_activeWallNormal * stats.ClimbJumpNormalScale + Vector3.up * stats.ClimbJumpUpScale).normalized;
            _ctx.SetVelocity(jumpDir * stats.ClimbJumpImpulse);
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void UpdateActiveWallNormal()
    {
        Vector3 normalSum = Vector3.zero;
        int activeCount = 0;

        if (_ctx.LeftHandAnchor.isActive)
        {
            normalSum += _ctx.LeftHandAnchor.normal;
            activeCount++;
        }

        if (_ctx.RightHandAnchor.isActive)
        {
            normalSum += _ctx.RightHandAnchor.normal;
            activeCount++;
        }

        _activeWallNormal = activeCount > 0 && normalSum.sqrMagnitude > 0.001f
            ? (normalSum / activeCount).normalized
            : Vector3.up;
    }

    private bool IsGrabbing() => _ctx.HasActiveHandAnchor;
}
