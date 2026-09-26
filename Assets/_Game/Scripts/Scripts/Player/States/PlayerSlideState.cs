using UnityEngine;

public class PlayerSlideState : PlayerBaseState
{
    public PlayerSlideState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetKinematic(false);
        _ctx.LogEvent("STATE", "Entered SLIDING state. Physics taking over.");
    }

    public override void UpdateState()
    {
        CheckSwitchStates();
        HandleSlideMovement();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        // 1. Zemin açısı düzelirse ve yürünebilir hale gelirse yürüme state'ine geç
        if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Grounded)
        {
            _ctx.SwitchState(_factory.Grounded);
            return;
        }

        // 2. Zeminden tamamen koparsak (uçurumdan düşme) hava state'ine geç
        if (_ctx.Sensor.CurrentGroundState == Ascent.Player.Sensors.GroundState.Airborne)
        {
            _ctx.SwitchState(_factory.Air);
            return;
        }

        // 3. Kayarken zıplama izni (isteğe bağlı, şimdilik kapalı tutmak daha fizikseldir ama ekleyebiliriz)
        if (_ctx.JumpInput)
        {
            _ctx.ResetJump();
            // Kayarken zıplamak istiyorsan buraya zıplama emri eklenebilir.
        }
    }

    private void HandleSlideMovement()
    {
        // KAYMA FİZİĞİ: Oyuncunun WASD girdilerini yok sayıyoruz veya çok aza indiriyoruz.
        // Yüzeyin normal vektörüne (GroundNormal) göre aşağı doğru yerçekiminin bizi çekmesine izin veriyoruz.

        Vector3 slideDirection = new Vector3(_ctx.Sensor.GroundNormal.x, -_ctx.Sensor.GroundNormal.y, _ctx.Sensor.GroundNormal.z).normalized;

        // Karakteri yokuş aşağı hafifçe iterek motorun sürtünme takılmalarını engelliyoruz
        // Hedef hızı sıfırlıyoruz ki FixedUpdate'teki "durma/frenleme" matematiği devreye girmesin, fizik serbest kalsın.
        _ctx.SetVelocity(slideDirection * (_ctx.Stats.MoveSpeed * 0.5f));
    }
}