using UnityEngine;

public class PlayerVaultState : PlayerBaseState
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _vaultProgress;

    // Süreyi uzatarak roket gibi fırlamayı engelledik (Ağırlık hissi eklendi)
    private float _vaultDuration = 0.75f;
    private CapsuleCollider _collider;

    public PlayerVaultState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.IsClimbing = false;
        _ctx.ResetFreeLook();
        _ctx.SetKinematic(true);

        _collider = _ctx.PlayerTransform.GetComponent<CapsuleCollider>();
        if (_collider != null) _collider.enabled = false;

        _startPos = _ctx.PlayerTransform.position;
        _targetPos = _ctx.VaultTargetPos;
        _vaultProgress = 0f;

        _ctx.SetLeftAnchor(null, Vector3.zero);
        _ctx.SetRightAnchor(null, Vector3.zero);

        _ctx.LogEvent("VAULT", "Started Ledge Vault. Physics suspended.");
    }

    public override void UpdateState()
    {
        _vaultProgress += Time.deltaTime / _vaultDuration;
        float t = Mathf.Clamp01(_vaultProgress);

        // Y EKSENİ (Ease-Out): Patlayıcı güçle yukarı çekil, tepeye yaklaşırken yavaşla ve bekle.
        float yT = Mathf.Sin(t * Mathf.PI * 0.5f);

        Vector3 currentPos = _ctx.PlayerTransform.position;
        currentPos.y = Mathf.Lerp(_startPos.y, _targetPos.y, yT);

        Vector3 startXZ = new Vector3(_startPos.x, 0, _startPos.z);
        Vector3 targetXZ = new Vector3(_targetPos.x, 0, _targetPos.z);

        // XZ EKSENİ (İleri Atılım): Hareketi animasyonun %40'ı bitene kadar BEKLET.
        // Karakter omuz hizasına gelmeden ileri fırlarsa o "roket" hissi oluşur.
        if (t > 0.4f)
        {
            float delayedXZ_T = Mathf.Clamp01((t - 0.4f) / 0.6f);
            delayedXZ_T = delayedXZ_T * delayedXZ_T * delayedXZ_T; // Kübik ivmelenme (Güçlü ileri atılım)

            Vector3 currentXZ = Vector3.Lerp(startXZ, targetXZ, delayedXZ_T);
            currentPos.x = currentXZ.x;
            currentPos.z = currentXZ.z;
        }

        _ctx.PlayerTransform.position = currentPos;

        CheckSwitchStates();
    }

    public override void ExitState()
    {
        if (_collider != null) _collider.enabled = true;

        _ctx.SetKinematic(false);

        // MÜHENDİSLİK ÇÖZÜMÜ: Ekstra ileri fırlatmayı sildik. Karakter tam kenarda duracak.
        _ctx.SetVelocity(Vector3.zero);

        _ctx.LogEvent("VAULT", "Finished Ledge Vault. Physics restored.");
    }

    public override void CheckSwitchStates()
    {
        if (_vaultProgress >= 1f)
        {
            _ctx.SwitchState(_factory.Grounded);
        }
    }
}