using UnityEngine;

public class PlayerVaultState : PlayerBaseState
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _vaultProgress;
    private float _vaultDuration = 0.35f; // Çıkışın ne kadar hızlı/dinamik olacağı

    public PlayerVaultState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(false); // Fizik motorunu kapalı tut!
        _ctx.ResetFreeLook();

        _startPos = _ctx.PlayerTransform.position;
        _vaultProgress = 0f;

        // Hedefi bir kez daha doğrula
        if (_ctx.CheckLedgeVault(out Vector3 target))
        {
            _targetPos = target;
        }
        else
        {
            // Beklenmedik bir hata olursa güvenli düşüş
            _ctx.SwitchState(_factory.Air);
        }

        // Tırmanma tutamaklarını temizle
        _ctx.SetLeftAnchor(null, Vector3.zero);
        _ctx.SetRightAnchor(null, Vector3.zero);
    }

    public override void UpdateState()
    {
        _vaultProgress += Time.deltaTime / _vaultDuration;

        // Yumuşak geçiş için SmoothStep matematiği
        float t = Mathf.SmoothStep(0f, 1f, _vaultProgress);

        // Gövdeyi yeni pozisyona kaydır
        _ctx.PlayerTransform.position = Vector3.Lerp(_startPos, _targetPos, t);

        CheckSwitchStates();
    }

    public override void ExitState()
    {
        // Vault bittiğinde ivmeyi sıfırla ki ileri fırlamasın
        _ctx.SetVelocity(Vector3.zero);
    }

    public override void CheckSwitchStates()
    {
        // Animasyon (Lerp) bittiğinde yere basma durumuna geç
        if (_vaultProgress >= 1f)
        {
            _ctx.SwitchState(_factory.Grounded);
        }
    }
}