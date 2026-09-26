using UnityEngine;

public class PlayerAirState : PlayerBaseState
{
    public PlayerAirState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetKinematic(false);
        _ctx.ResetJump();
    }

    public override void UpdateState()
    {
        if (_ctx.JumpInput) _ctx.ResetJump();

        HandleAirMovement();
        CheckSwitchStates();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        bool hasStamina = _ctx.Stamina.CurrentStamina > 1f;

        // 1. ÖNCE GRIP VE KİLİT MANTIĞI (Sadece stamina varsa tutunabilir)
        if (hasStamina)
        {
            if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
            {
                // Havada tek el kuralı için ilk parametre _ctx.RightAnchor olmalı
                if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
                {
                    _ctx.SetLeftAnchor(point, normal);
                }
                else
                {
                    // MÜHENDİSLİK HAMLESİ: Havada boşluğa basıldıysa anında kilitle
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
        }

        // 2. SONRA STATE DEĞİŞİMİ
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

        // 3. YERE ÇARPMA KONTROLÜ
        if (_ctx.Velocity.y <= 0.1f)
        {
            if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Grounded)
            {
                _ctx.SwitchState(_factory.Grounded);
                return;
            }
            else if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Sliding)
            {
                _ctx.SwitchState(_factory.Slide);
                return;
            }
        }
    }

    private void HandleAirMovement()
    {
        PlayerStats stats = _ctx.Stats;
        float targetSpeed = _ctx.MoveInput == Vector2.zero ? 0.0f : stats.MoveSpeed;

        Vector3 inputDir = (_ctx.PlayerTransform.right * _ctx.MoveInput.x + _ctx.PlayerTransform.forward * _ctx.MoveInput.y).normalized;

        // Havadaki nihai hedef vektörü veriyoruz[cite: 3]
        Vector3 targetVelocity = inputDir * targetSpeed;

        _ctx.SetVelocity(targetVelocity);


    }
}