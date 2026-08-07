using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    private float _jumpTimeoutDelta;
    private bool _isExhausted;

    public PlayerGroundedState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(true);
        Vector3 vel = _ctx.Velocity;
        vel.y = _ctx.Stats.GroundStickVelocity;
        _ctx.SetVelocity(vel);
        _jumpTimeoutDelta = _ctx.Stats.JumpTimeout;
        _isExhausted = false;
    }

    public override void UpdateState()
    {
        if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;

        // 1. KİLİT AÇMA: Eğer oyuncu Shift tuşunu bırakırsa yorgunluk kilidini kaldır
        if (!_ctx.SprintInput) _isExhausted = false;

        // 2. KİLİTLEME: Eğer stamina tamamen bittiyse yorgunluk kilidini devreye sok
        if (_ctx.Stamina.CurrentStamina <= 0.1f) _isExhausted = true;

        HandleMovement();
        CheckSwitchStates();

        // 3. HARCAMA ŞARTI: Hareket var + Shift'e basılı + YORGUN DEĞİL
        if (_ctx.MoveInput != Vector2.zero && _ctx.SprintInput && !_isExhausted)
        {
            float sprintDrainRate = _ctx.Stats.ClimbDrainRate * 0.5f;
            _ctx.Stamina.ConsumeStamina(sprintDrainRate * Time.deltaTime);
        }
        else
        {
            _ctx.Stamina.RegenerateStamina(_ctx.Stats.StaminaRegenRate * Time.deltaTime);
        }
    }

    public override void ExitState() => _ctx.ResetJump();

    public override void CheckSwitchStates()
    {
        // YENİ AKTİF SENSÖR: Yerdeyken ilk tutunma
        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(null, out Vector3 point, out Vector3 normal))
                _ctx.SetLeftAnchor(point, normal);
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(null, out Vector3 point, out Vector3 normal))
                _ctx.SetRightAnchor(point, normal);
        }

        if (_ctx.LeftAnchor != null && _ctx.RightAnchor != null) { _ctx.SwitchState(_factory.Climb); return; }
        else if (_ctx.LeftAnchor != null || _ctx.RightAnchor != null) { _ctx.SwitchState(_factory.Hang); return; }

        if (_ctx.JumpInput && _jumpTimeoutDelta <= 0.0f && _ctx.Sensor.IsGrounded)
        {
            float jumpVelocity = Mathf.Sqrt(_ctx.Stats.JumpHeight * -2f * _ctx.Stats.Gravity);
            Vector3 vel = _ctx.Velocity; vel.y = jumpVelocity;
            _ctx.SetVelocity(vel);
            _ctx.SwitchState(_factory.Air);
            _ctx.ResetJump();
            return;
        }

        if (!_ctx.Sensor.IsGrounded) _ctx.SwitchState(_factory.Air);
    }

    private void HandleMovement()
    {
        PlayerStats stats = _ctx.Stats;

        // HIZ BELİRLEME: Koşabilmesi için yorgunluk kilidinin kapalı olması zorunlu
        bool canSprint = _ctx.SprintInput && !_isExhausted;
        float targetSpeed = canSprint ? stats.SprintSpeed : stats.MoveSpeed;

        if (_ctx.MoveInput == Vector2.zero) targetSpeed = 0.0f;

        float currentHorizontalSpeed = _ctx.HorizontalSpeed;
        float finalSpeed = targetSpeed;

        if (Mathf.Abs(currentHorizontalSpeed - targetSpeed) > stats.SpeedBlendThreshold)
        {
            float rate = (_ctx.MoveInput == Vector2.zero) ? stats.DecelerationRate : stats.AccelerationRate;
            finalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * rate);
        }

        Vector3 inputDirection = _ctx.MoveInput != Vector2.zero
            ? _ctx.PlayerTransform.right * _ctx.MoveInput.x + _ctx.PlayerTransform.forward * _ctx.MoveInput.y
            : (_ctx.HorizontalSpeed > 0.001f ? new Vector3(_ctx.Velocity.x, 0, _ctx.Velocity.z).normalized : Vector3.zero);

        Vector3 vel = _ctx.Velocity;
        vel.x = inputDirection.x * finalSpeed;
        vel.y = stats.GroundStickVelocity;
        vel.z = inputDirection.z * finalSpeed;
        _ctx.SetVelocity(vel);
    }
}