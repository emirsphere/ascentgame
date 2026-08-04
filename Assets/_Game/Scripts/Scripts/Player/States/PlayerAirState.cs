using UnityEngine;

public class PlayerAirState : PlayerBaseState
{
    public PlayerAirState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(true); // EKSİKTİ, EKLENDİ! (Düşerken zeminden geçmemek için)
        _ctx.ResetJump();
    }


    public override void UpdateState()
    {
        if (_ctx.JumpInput) _ctx.ResetJump();
        HandleGravity();
        HandleAirMovement();
        CheckSwitchStates();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        // YENİ AKTİF SENSÖR: Havada duvara uçarken (Diğer el boşta olduğu için null yolluyoruz)
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

        if (_ctx.Sensor.IsGrounded && _ctx.Velocity.y < 0.0f) _ctx.SwitchState(_factory.Grounded);
    }

    private void HandleGravity()
    {
        PlayerStats stats = _ctx.Stats;
        Vector3 vel = _ctx.Velocity;

        // EĞER DUVARDAN KAYIYORSA (SLIDING):
        // Duvara takılıp süzülmesini engellemek için dikey düşüş hızını ekstra artırıyoruz
        if (_ctx.Sensor.IsSliding)
        {
            // Duvar sürtünmesini kırmak için ekstra dikey ivme
            vel.y += stats.Gravity * 2.0f * Time.deltaTime;
        }
        else if (vel.y > -stats.TerminalVelocity)
        {
            vel.y += stats.Gravity * Time.deltaTime;
        }

        _ctx.SetVelocity(vel);
    }

    private void HandleAirMovement()
    {
        PlayerStats stats = _ctx.Stats;
        float targetSpeed = _ctx.MoveInput == Vector2.zero ? 0.0f : stats.MoveSpeed;
        float finalSpeed = Mathf.Lerp(_ctx.HorizontalSpeed, targetSpeed, Time.deltaTime * stats.AirControlRate);

        Vector3 vel = _ctx.Velocity;
        if (_ctx.MoveInput == Vector2.zero)
        {
            float drag = Time.deltaTime * stats.AirDragRate;
            vel.x = Mathf.Lerp(vel.x, 0f, drag);
            vel.z = Mathf.Lerp(vel.z, 0f, drag);
            _ctx.SetVelocity(vel);
            return;
        }

        Vector3 inputDir = (_ctx.PlayerTransform.right * _ctx.MoveInput.x + _ctx.PlayerTransform.forward * _ctx.MoveInput.y).normalized;
        vel.x = inputDir.x * finalSpeed;
        vel.z = inputDir.z * finalSpeed;
        _ctx.SetVelocity(vel);
    }
}