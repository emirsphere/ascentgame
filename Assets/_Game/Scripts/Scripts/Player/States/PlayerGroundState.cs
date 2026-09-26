using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    private float _jumpTimeoutDelta;
    private bool _isExhausted;

    public PlayerGroundedState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetKinematic(false);
        _jumpTimeoutDelta = _ctx.Stats.JumpTimeout;
        _isExhausted = false;
    }

    public override void UpdateState()
    {
        if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;

        if (!_ctx.SprintInput) _isExhausted = false;
        if (_ctx.Stamina.CurrentStamina <= 0.1f) _isExhausted = true;

        HandleMovement();
        CheckSwitchStates();

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
        // 1. ÖNCE GRIP VE KİLİT MANTIĞI
        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetLeftAnchor(point, normal);
            }
            else
            {
                // MÜHENDİSLİK HAMLESİ: Yerden boşluğa basıldıysa anında kilitle
                _ctx.LockLeftGrip();
            }
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetRightAnchor(point, normal);
            }
            else
            {
                _ctx.LockRightGrip();
            }
        }

        // 2. SONRA STATE DEĞİŞİMİ (Tutunma Başarılıysa)
        if (_ctx.LeftAnchor != null && _ctx.RightAnchor != null)
        {
            _ctx.SwitchState(_factory.Climb);
            return;
        }
        else if (_ctx.LeftAnchor != null || _ctx.RightAnchor != null)
        {
            _ctx.SwitchState(_factory.Hang);
            return;
        }

        // 3. YER VE ZIPLAMA KONTROLLERİ (Tutunma Yoksa)
        // YENİ: Anlık tuş okuması yerine Jump Buffer hafızasını kontrol ediyoruz
        if (_ctx.JumpBufferTimer > 0 && _jumpTimeoutDelta <= 0.0f && _ctx.Sensor.IsGrounded)
        {
            float jumpVelocity = Mathf.Sqrt(_ctx.Stats.JumpHeight * -2f * Physics.gravity.y);
            _ctx.ExecuteJump(jumpVelocity);

            _ctx.ConsumeJumpBuffer(); // Zıpladıktan sonra hafızayı temizle ki art arda sekmesin

            _ctx.SwitchState(_factory.Air);
            return;
        }

        if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Sliding)
        {
            _ctx.SwitchState(_factory.Slide);
            return;
        }
        else if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Airborne)
        {
            _ctx.SwitchState(_factory.Air);
            return;
        }
    }

    private void HandleMovement()
    {
        PlayerStats stats = _ctx.Stats;
        bool canSprint = _ctx.SprintInput && !_isExhausted;
        float targetSpeed = canSprint ? stats.SprintSpeed : stats.MoveSpeed;

        if (_ctx.MoveInput == Vector2.zero) targetSpeed = 0.0f;

        Vector3 inputDirection = _ctx.PlayerTransform.right * _ctx.MoveInput.x + _ctx.PlayerTransform.forward * _ctx.MoveInput.y;

        Vector3 slopeDirection = Vector3.ProjectOnPlane(inputDirection, _ctx.Sensor.GroundNormal).normalized;

        Vector3 targetVelocity = slopeDirection * targetSpeed;

        _ctx.SetVelocity(targetVelocity);
    }
}