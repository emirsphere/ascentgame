using UnityEngine;

public class PlayerHangState : PlayerBaseState
{
    private Vector3 _currentVelocity;
    private float _springDamping;

    public PlayerHangState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.IsFreeLook = true;
        _currentVelocity = _ctx.Velocity;
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
    }

    public override void UpdateState()
    {
        HandleGripLogic();
        CheckSwitchStates();

        // BUG FİX: Eğer CheckSwitchStates bizi Air state'ine attıysa, fiziği hesaplama. Çökmeyi engeller.
        if (_ctx.LeftAnchor != null || _ctx.RightAnchor != null)
        {
            HandlePendulumPhysics();
        }
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        if (_ctx.LeftAnchor != null && _ctx.RightAnchor != null)
        {
            _ctx.SwitchState(_factory.Climb);
        }
        else if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void HandleGripLogic()
    {
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null && _ctx.Sensor.CanGrip)
            _ctx.SetLeftAnchor(_ctx.Sensor.GripHit.point, _ctx.Sensor.GripHit.normal);

        if (_ctx.RightGripInput && _ctx.RightAnchor == null && _ctx.Sensor.CanGrip)
            _ctx.SetRightAnchor(_ctx.Sensor.GripHit.point, _ctx.Sensor.GripHit.normal);
    }

    private void HandlePendulumPhysics()
    {
        // BUG FİX: GÜVENLİ OKUMA (Defensive Programming). Null gelirse işlemi iptal et.
        if (!_ctx.LeftAnchor.HasValue && !_ctx.RightAnchor.HasValue) return;

        Vector3 anchor = _ctx.LeftAnchor.HasValue ? _ctx.LeftAnchor.Value : _ctx.RightAnchor.Value;
        Vector3 normal = _ctx.LeftAnchor.HasValue ? _ctx.LeftNormal : _ctx.RightNormal;

        Vector3 desiredPosition = anchor + (Vector3.down * _ctx.Stats.RestOffset) + (normal * _ctx.Stats.BaseWallDistance);

        Vector3 swingRight = Vector3.Cross(Vector3.up, normal).normalized;
        desiredPosition += swingRight * _ctx.MoveInput.x * _ctx.Stats.SwingAmplitude;

        Vector3 displacement = _ctx.PlayerTransform.position - desiredPosition;
        Vector3 springForce = -_ctx.Stats.SpringStiffness * displacement;
        Vector3 dampingForce = -_springDamping * _currentVelocity;

        _currentVelocity += (springForce + dampingForce) * Time.deltaTime;

        if (_currentVelocity.sqrMagnitude < 0.05f && displacement.sqrMagnitude < 0.05f)
            _currentVelocity = Vector3.zero;

        _ctx.SetVelocity(_currentVelocity);
    }
}