using UnityEngine;

public class PlayerHangState : PlayerBaseState
{
    private float _springDamping;
    private Vector3 _smoothedDesiredPosition;

    // --- YENİ: ADIM ADIM EL KAYDIRMA DEĞİŞKENLERİ ---
    private bool _isShiftingGrip;
    private Vector3 _shiftStartPos;
    private Vector3 _shiftTargetPos;
    private Vector3 _shiftStartNormal;
    private Vector3 _shiftTargetNormal;
    private float _shiftProgress;
    private float _shiftDuration = 0.25f; // Elin yeni hedefe ulaşma hızı

    public PlayerHangState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.IsFreeLook = true;
        _ctx.IsClimbing = true;
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
        _smoothedDesiredPosition = Vector3.zero;
        _ctx.SetVelocity(Vector3.zero);

        _isShiftingGrip = false; // Geçişi sıfırla
        _ctx.LogEvent("HANG", "Entered Hang State.");
    }

    public override void UpdateState()
    {
        if (_ctx.Stamina.CurrentStamina <= 0f)
        {
            _ctx.SetLeftAnchor(null, Vector3.zero);
            _ctx.SetRightAnchor(null, Vector3.zero);
            _ctx.LockGrips();
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
            return;
        }

        if (_ctx.MoveInput.y > 0.1f && _ctx.CheckLedgeVault(out Vector3 targetPos))
        {
            _ctx.VaultTargetPos = targetPos;
            _ctx.SwitchState(_factory.Vault);
            return;
        }

        if (_ctx.MoveInput.sqrMagnitude < 0.01f)
        {
            Vector3 dampedVelocity = Vector3.Lerp(_ctx.Velocity, Vector3.zero, Time.deltaTime * 15f);
            _ctx.SetVelocity(dampedVelocity);
        }

        if (_ctx.JumpHeld) _ctx.BuildTension();
        else if (_ctx.IsTensioning && !_ctx.JumpHeld)
        {
            _ctx.ExecuteDynoJump();
            return;
        }

        if (_ctx.IsTensioning)
        {
            HandlePendulumPhysics();
            CheckSwitchStates();
            return;
        }

        HandleGripLogic();
        CheckSwitchStates();
        if (!_ctx.IsClimbing) return;

        Vector3 currentNormal = _ctx.LeftAnchor.HasValue ? _ctx.LeftNormal : _ctx.RightNormal;
        _ctx.SmoothAlignBody(currentNormal, 10f);

        if (_ctx.LeftAnchor != null || _ctx.RightAnchor != null)
        {
            HandlePendulumPhysics();
        }

        _ctx.Stamina.ConsumeStamina(_ctx.Stats.ClimbDrainRate * Time.deltaTime);
    }

    public override void ExitState()
    {
        _ctx.IsClimbing = false;
        _ctx.ApplyClimbForce(Vector3.zero);
    }

    public override void CheckSwitchStates()
    {
        if (_ctx.LeftAnchor != null && _ctx.RightAnchor != null) _ctx.SwitchState(_factory.Climb);
        else if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetTension();
            _ctx.ApplyClimbForce(Vector3.zero);
            _ctx.SetVelocity(new Vector3(_ctx.Velocity.x, Mathf.Min(_ctx.Velocity.y, 0f), _ctx.Velocity.z));
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void HandleGripLogic()
    {
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        float maxReach = Mathf.Min(_ctx.Stats.GripReachDistance, 1.4f);
        Vector3 shoulderPos = _ctx.PlayerTransform.position + Vector3.up * _ctx.Stats.ShoulderHeight;

        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
            {
                Vector3 finalTarget = point + (-_ctx.PlayerTransform.right * 0.12f);
                if (Vector3.Distance(shoulderPos, finalTarget) <= maxReach) _ctx.SetLeftAnchor(finalTarget, normal);
                else _ctx.LockLeftGrip();
            }
            else _ctx.LockLeftGrip();
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
            {
                Vector3 finalTarget = point + (_ctx.PlayerTransform.right * 0.12f);
                if (Vector3.Distance(shoulderPos, finalTarget) <= maxReach) _ctx.SetRightAnchor(finalTarget, normal);
                else _ctx.LockRightGrip();
            }
            else _ctx.LockRightGrip();
        }
    }

    private void HandlePendulumPhysics()
    {
        Vector3 anchor = _ctx.LeftAnchor.HasValue ? _ctx.LeftAnchor.Value : _ctx.RightAnchor.Value;
        Vector3 handNormal = _ctx.LeftAnchor.HasValue ? _ctx.LeftNormal : _ctx.RightNormal;
        bool isLeftHand = _ctx.LeftAnchor.HasValue;

        Vector3 currentPos = _ctx.PlayerTransform.position;
        Vector3 swingRight = Vector3.Cross(Vector3.up, handNormal).normalized;
        float inputX = _ctx.MoveInput.x;
        float maxStretch = 0.55f;

        // --- MÜHENDİSLİK ÇÖZÜMÜ: ADIM ADIM EL KAYDIRMA (DISCRETE SHIFT) ---
        if (_isShiftingGrip)
        {
            // 1. El yeni hedefine doğru yumuşakça (Lerp) ilerler
            _shiftProgress += Time.deltaTime / _shiftDuration;
            float t = Mathf.Clamp01(_shiftProgress);
            float smoothT = t * t * (3f - 2f * t);

            anchor = Vector3.Lerp(_shiftStartPos, _shiftTargetPos, smoothT);
            handNormal = Vector3.Slerp(_shiftStartNormal, _shiftTargetNormal, smoothT).normalized;

            // Fiziği ve IK'yı güncelle
            if (isLeftHand) _ctx.SetLeftAnchor(anchor, handNormal);
            else _ctx.SetRightAnchor(anchor, handNormal);

            if (_shiftProgress >= 1f) _isShiftingGrip = false; // Geçiş bitti
        }
        else
        {
            // 2. El sabitse esneme miktarını ölç ve gerekirse yeni adım tetikle
            Vector3 absoluteOffset = Vector3.ProjectOnPlane(currentPos - anchor, handNormal);
            float driftAmount = Vector3.Dot(absoluteOffset, swingRight);

            // Eğer esneme %80'i geçtiyse ve oyuncu hala o yöne basıyorsa
            if (Mathf.Abs(inputX) > 0.1f && Mathf.Abs(driftAmount) > maxStretch * 0.8f)
            {
                if (Mathf.Sign(inputX) == Mathf.Sign(driftAmount))
                {
                    // 0.4 metre ileride yeni bir tutunma noktası tara
                    float stepDistance = 0.4f;
                    Vector3 checkOrigin = anchor + (swingRight * Mathf.Sign(inputX) * stepDistance) + (handNormal * 0.7f);
                    if (Physics.SphereCast(checkOrigin, 0.15f, -handNormal, out RaycastHit hit, 1.5f, _ctx.Stats.ClimbableLayers))
                    {
                        // Yeni adım animasyonunu başlat
                        _isShiftingGrip = true;
                        _shiftProgress = 0f;
                        _shiftStartPos = anchor;
                        _shiftStartNormal = handNormal;
                        _shiftTargetPos = hit.point;
                        _shiftTargetNormal = hit.normal;
                    }
                }
            }
        }

        // --- 3. FİZİKSEL SARKAÇ (EL YER DEĞİŞTİRİRKEN GÖVDEYİ OTOMATİK ÇEKER) ---
        float desiredShoulderY = anchor.y - 0.3f;
        float targetY = desiredShoulderY - _ctx.Stats.ShoulderHeight;
        float currentWallDistance = Mathf.Lerp(_ctx.Stats.BaseWallDistance, _ctx.Stats.LeanWallDistance, _ctx.TensionCharge);
        Vector3 depthOrigin = new Vector3(anchor.x, targetY, anchor.z);
        Vector3 rawDesiredPosition = depthOrigin;

        if (Physics.Raycast(depthOrigin + (handNormal * 1.5f), -handNormal, out RaycastHit hitDepth, currentWallDistance * 3f, _ctx.Stats.ClimbableLayers))
            rawDesiredPosition = hitDepth.point + (hitDepth.normal * currentWallDistance);
        else
            rawDesiredPosition = depthOrigin + (handNormal * currentWallDistance);

        if (_smoothedDesiredPosition == Vector3.zero) _smoothedDesiredPosition = rawDesiredPosition;
        _smoothedDesiredPosition = Vector3.Lerp(_smoothedDesiredPosition, rawDesiredPosition, Time.deltaTime * 12f);

        float deltaY = Mathf.Clamp(currentPos.y - targetY, -0.6f, 0.6f);
        Vector3 wallDisplacement = Vector3.Project(currentPos - _smoothedDesiredPosition, handNormal);

        // --- TASMA (LEASH) ---
        Vector3 lateralOffset = Vector3.ProjectOnPlane(currentPos - _smoothedDesiredPosition, handNormal);
        lateralOffset.y = 0;
        float smoothedDrift = Vector3.Dot(lateralOffset, swingRight);


        if (smoothedDrift > maxStretch && inputX > 0) inputX = 0f;
        if (smoothedDrift < -maxStretch && inputX < 0) inputX = 0f;

        Vector3 lateralSpring = Vector3.zero;
        if (Mathf.Abs(smoothedDrift) > maxStretch)
        {
            float overStretch = Mathf.Abs(smoothedDrift) - maxStretch;
            lateralSpring = -Mathf.Sign(smoothedDrift) * swingRight * (overStretch * _ctx.Stats.SpringStiffness * 2f);
        }
        else if (Mathf.Abs(inputX) < 0.01f)
        {
            lateralSpring = -lateralOffset * (_ctx.Stats.SpringStiffness * 0.15f);
        }

        Vector3 swingForce = swingRight * (inputX * _ctx.Stats.SwingAmplitude * 25f);
        float verticalSpring = -_ctx.Stats.SpringStiffness * deltaY;
        float verticalDamping = -_springDamping * _ctx.Velocity.y;
        Vector3 springForce = -_ctx.Stats.SpringStiffness * wallDisplacement;

        Vector3 horizontalVel = Vector3.Project(_ctx.Velocity, swingRight);
        Vector3 horizontalFriction = -horizontalVel * (_springDamping * 1.5f);

        Vector3 totalClimbForce = springForce + swingForce + lateralSpring + horizontalFriction + new Vector3(0f, verticalSpring + verticalDamping, 0f);
        totalClimbForce -= Physics.gravity;

        if (_ctx.MoveInput.sqrMagnitude < 0.01f && totalClimbForce.magnitude < 5f && Mathf.Abs(smoothedDrift) < 0.1f)
        {
            totalClimbForce = Vector3.zero;
            _smoothedDesiredPosition = currentPos;
        }

        if (_ctx.Sensor.IsGrounded && _ctx.MoveInput.sqrMagnitude < 0.01f)
        {
            _ctx.SetVelocity(new Vector3(0f, _ctx.Velocity.y, 0f));
            totalClimbForce = new Vector3(0f, totalClimbForce.y, 0f);
        }

        totalClimbForce = Vector3.ClampMagnitude(totalClimbForce, 20f);
        _ctx.ApplyClimbForce(totalClimbForce);
    }
}