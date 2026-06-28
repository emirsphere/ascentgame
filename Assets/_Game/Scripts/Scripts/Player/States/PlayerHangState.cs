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
        // Kritik sönümleme katsayısı (Kusursuz yay fiziği formülü)
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
    }

    public override void UpdateState()
    {
        HandleGripLogic();
        CheckSwitchStates();

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
        if (!_ctx.LeftAnchor.HasValue && !_ctx.RightAnchor.HasValue) return;

        Vector3 anchor = _ctx.LeftAnchor.HasValue ? _ctx.LeftAnchor.Value : _ctx.RightAnchor.Value;
        Vector3 normal = _ctx.LeftAnchor.HasValue ? _ctx.LeftNormal : _ctx.RightNormal;

        Vector3 desiredPosition = anchor + (Vector3.down * _ctx.Stats.RestOffset) + (normal * _ctx.Stats.BaseWallDistance);

        // A/D ile Momentum Kazanımı
        Vector3 swingRight = Vector3.Cross(Vector3.up, normal).normalized;
        desiredPosition += swingRight * _ctx.MoveInput.x * _ctx.Stats.SwingAmplitude;

        Vector3 displacement = _ctx.PlayerTransform.position - desiredPosition;
        Vector3 springForce = -_ctx.Stats.SpringStiffness * displacement;
        Vector3 dampingForce = -_springDamping * _currentVelocity;

        _currentVelocity += (springForce + dampingForce) * Time.deltaTime;

        // Fiziği daha yumuşak bitirmek için tolerans eşiği düşürüldü
        if (_currentVelocity.sqrMagnitude < _ctx.Stats.ClimbSnapThreshold && displacement.sqrMagnitude < _ctx.Stats.ClimbSnapThreshold)
        {
            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 10f);
        }

        _ctx.SetVelocity(_currentVelocity);
    }
}