using UnityEngine;

public class PlayerHangState : PlayerBaseState
{
    private Vector3 _currentVelocity;
    private float _springDamping;

    public PlayerHangState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.SetControllerEnabled(false);
        _ctx.IsFreeLook = true;
        _currentVelocity = _ctx.Velocity;
        // Kritik sönümleme katsayısı (Kusursuz yay fiziği formülü)
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
    }

    public override void UpdateState()
    {
        if (_ctx.MoveInput.y > 0.1f && _ctx.CheckLedgeVault(out Vector3 targetPos))
        {
            _ctx.VaultTargetPos = targetPos; // Hedefi FirstPersonController hafızasına yaz
            _ctx.SwitchState(_factory.Vault);
            return;
        }
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

        // EKLENEN KISIM: Tek elle asılıyken de tepeye W ile çıkmaya çalışırsa Vault'a geç
        
        else if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void HandleGripLogic()
    {
        // 1. Tıkı bıraktıysa o eli serbest bırak ve düş.
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        // 2. Sol el boşta ve tıklandı. Sağı referans noktası vererek tutun.
        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetLeftAnchor(point, normal);
            }
        }

        // 3. Sağ el boşta ve tıklandı. Solu referans noktası vererek tutun.
        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
            {
                _ctx.SetRightAnchor(point, normal);
            }
        }
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