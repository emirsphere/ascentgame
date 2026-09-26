using UnityEngine;

public class PlayerClimbState : PlayerBaseState
{
    private float _springDamping;
    private Vector3 _smoothedDesiredPosition;

    private int _shiftPhase = 0;
    private bool _isLeftHandMoving;
    private float _shiftProgress;
    private float _shiftDuration = 0.2f;

    private Vector3 _startPos, _targetPos;
    private Vector3 _startNorm, _targetNorm;
    private Vector3 _lastShiftVector;

    public PlayerClimbState(IPlayerController currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.IsFreeLook = true;
        _ctx.IsClimbing = true;
        _springDamping = 2f * Mathf.Sqrt(_ctx.Stats.SpringStiffness);
        _ctx.SetVelocity(Vector3.zero);
        _smoothedDesiredPosition = Vector3.zero;

        _shiftPhase = 0;
        _ctx.LogEvent("CLIMB", "Entered Climb State.");
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

        if (_ctx.MoveInput.y > 0.1f && _shiftPhase == 0 && _ctx.CheckLedgeVault(out Vector3 targetPos))
        {
            _ctx.VaultTargetPos = targetPos;
            _ctx.SwitchState(_factory.Vault);
            return;
        }

        if (_ctx.MoveInput.sqrMagnitude < 0.01f && _shiftPhase == 0)
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
            HandleTwoHandedPhysics();
            CheckSwitchStates();
            return;
        }

        HandleGripLogic();
        CheckSwitchStates();
        if (!_ctx.IsClimbing) return;

        HandleSequentialShimmy();

        Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;
        _ctx.SmoothAlignBody(averageNormal, 10f);

        HandleTwoHandedPhysics();
        _ctx.Stamina.ConsumeStamina(_ctx.Stats.ClimbDrainRate * Time.deltaTime);
    }

    public override void ExitState()
    {
        _ctx.IsClimbing = false;
        _ctx.ApplyClimbForce(Vector3.zero);
    }

    public override void CheckSwitchStates()
    {
        if (_ctx.LeftAnchor == null && _ctx.RightAnchor == null)
        {
            _ctx.ResetTension();
            _ctx.ApplyClimbForce(Vector3.zero);
            _ctx.SetVelocity(new Vector3(_ctx.Velocity.x, Mathf.Min(_ctx.Velocity.y, 0f), _ctx.Velocity.z));
            _ctx.ResetFreeLook();
            _ctx.SwitchState(_factory.Air);
        }
        else if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null)
        {
            _ctx.ApplyClimbForce(Vector3.zero);
            _ctx.SwitchState(_factory.Hang);
        }
    }

    private void HandleGripLogic()
    {
        if (!_ctx.LeftGripInput && _ctx.LeftAnchor != null) _ctx.SetLeftAnchor(null, Vector3.zero);
        if (!_ctx.RightGripInput && _ctx.RightAnchor != null) _ctx.SetRightAnchor(null, Vector3.zero);

        float maxClimbReach = Mathf.Min(_ctx.Stats.GripReachDistance, 1.4f);
        Vector3 shoulderPos = _ctx.PlayerTransform.position + (Vector3.up * _ctx.Stats.ShoulderHeight);

        if (_ctx.LeftGripInput && _ctx.LeftAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.RightAnchor, out Vector3 point, out Vector3 normal))
            {
                Vector3 finalTargetPoint = point + (-_ctx.PlayerTransform.right * 0.12f);
                if (Vector3.Distance(shoulderPos, finalTargetPoint) <= maxClimbReach) _ctx.SetLeftAnchor(finalTargetPoint, normal);
                else _ctx.LockLeftGrip();
            }
            else _ctx.LockLeftGrip();
        }

        if (_ctx.RightGripInput && _ctx.RightAnchor == null)
        {
            if (_ctx.TryGetGripPoint(_ctx.LeftAnchor, out Vector3 point, out Vector3 normal))
            {
                Vector3 finalTargetPoint = point + (_ctx.PlayerTransform.right * 0.12f);
                if (Vector3.Distance(shoulderPos, finalTargetPoint) <= maxClimbReach) _ctx.SetRightAnchor(finalTargetPoint, normal);
                else _ctx.LockRightGrip();
            }
            else _ctx.LockRightGrip();
        }
    }

    private void HandleSequentialShimmy()
    {
        if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null) return;

        Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;
        if (averageNormal == Vector3.zero) averageNormal = Vector3.up;

        Vector3 lateralRight = _ctx.PlayerTransform.right;

        float inputX = _ctx.MoveInput.x;

        if (_shiftPhase > 0)
        {
            _shiftProgress += Time.deltaTime / _shiftDuration;
            float t = Mathf.Clamp01(_shiftProgress);
            float smoothT = t * t * (3f - 2f * t);

            Vector3 currentPos = Vector3.Lerp(_startPos, _targetPos, smoothT);
            Vector3 currentNorm = Vector3.Slerp(_startNorm, _targetNorm, smoothT).normalized;

            if (_isLeftHandMoving) _ctx.SetLeftAnchor(currentPos, currentNorm);
            else _ctx.SetRightAnchor(currentPos, currentNorm);

            if (_shiftProgress >= 1f)
            {
                if (_shiftPhase == 1)
                {
                    _shiftPhase = 2;
                    _shiftProgress = 0f;
                    _isLeftHandMoving = !_isLeftHandMoving;

                    Vector3 anchorToMove = _isLeftHandMoving ? _ctx.LeftAnchor.Value : _ctx.RightAnchor.Value;

                    // Takipçi El için Hacimli ve Derin Sensör
                    Vector3 checkOrigin = anchorToMove + _lastShiftVector + (averageNormal * 1.0f);

                    if (Physics.SphereCast(checkOrigin, 0.25f, -averageNormal, out RaycastHit hit, 2.5f, _ctx.Stats.ClimbableLayers))
                    {
                        _startPos = anchorToMove;
                        _startNorm = _isLeftHandMoving ? _ctx.LeftNormal : _ctx.RightNormal;
                        _targetPos = hit.point;
                        _targetNorm = hit.normal;
                    }
                    else
                    {
                        _shiftPhase = 0;
                    }
                }
                else
                {
                    _shiftPhase = 0;
                }
            }
        }
        else
        {
            Vector3 currentPos = _ctx.PlayerTransform.position;
            Vector3 averagePivot = (_ctx.LeftAnchor.Value + _ctx.RightAnchor.Value) * 0.5f;
            Vector3 lateralOffset = Vector3.ProjectOnPlane(currentPos - averagePivot, averageNormal);
            float driftAmount = Vector3.Dot(lateralOffset, lateralRight);

            // Dinamik Esneme Sınırı (Kapsül dik kalsa bile kol boyuna göre esner)
            float maxStretch = _ctx.Stats.GripReachDistance * 0.6f;

            if (Mathf.Abs(inputX) > 0.1f && Mathf.Abs(driftAmount) > maxStretch * 0.3f)
            {
                if (Mathf.Sign(inputX) == Mathf.Sign(driftAmount))
                {
                    float stepDistance = 0.4f;
                    _lastShiftVector = lateralRight * Mathf.Sign(inputX) * stepDistance;

                    _isLeftHandMoving = inputX < 0;
                    Vector3 leadingAnchor = _isLeftHandMoving ? _ctx.LeftAnchor.Value : _ctx.RightAnchor.Value;

                    // Öncü El için Hacimli ve Derin Sensör
                    Vector3 checkOrigin = leadingAnchor + _lastShiftVector + (averageNormal * 1.0f);

                    if (Physics.SphereCast(checkOrigin, 0.25f, -averageNormal, out RaycastHit hit, 2.5f, _ctx.Stats.ClimbableLayers))
                    {
                        _shiftPhase = 1;
                        _shiftProgress = 0f;
                        _startPos = leadingAnchor;
                        _startNorm = _isLeftHandMoving ? _ctx.LeftNormal : _ctx.RightNormal;
                        _targetPos = hit.point;
                        _targetNorm = hit.normal;
                    }
                }
            }
        }
    }

    private void HandleTwoHandedPhysics()
    {
        if (_ctx.LeftAnchor == null || _ctx.RightAnchor == null) return;

        PlayerStats stats = _ctx.Stats;
        Vector3 currentPos = _ctx.PlayerTransform.position;
        Vector3 averagePivot = (_ctx.LeftAnchor.Value + _ctx.RightAnchor.Value) * 0.5f;
        Vector3 averageNormal = (_ctx.LeftNormal + _ctx.RightNormal).normalized;
        if (averageNormal == Vector3.zero) averageNormal = Vector3.up;

        float desiredShoulderY = averagePivot.y - 0.3f;
        float targetY = desiredShoulderY - stats.ShoulderHeight;

        float currentWallDistance = Mathf.Lerp(stats.BaseWallDistance, stats.LeanWallDistance, _ctx.TensionCharge);
        Vector3 depthOrigin = new Vector3(averagePivot.x, targetY, averagePivot.z);
        Vector3 rawDesiredPosition = depthOrigin;

        if (Physics.Raycast(depthOrigin + (averageNormal * 1.5f), -averageNormal, out RaycastHit hit, currentWallDistance * 3f, _ctx.Stats.ClimbableLayers))
            rawDesiredPosition = hit.point + (hit.normal * currentWallDistance);
        else
            rawDesiredPosition = depthOrigin + (averageNormal * currentWallDistance);

        if (_smoothedDesiredPosition == Vector3.zero) _smoothedDesiredPosition = rawDesiredPosition;
        _smoothedDesiredPosition = Vector3.Lerp(_smoothedDesiredPosition, rawDesiredPosition, Time.deltaTime * 12f);

        float deltaY = Mathf.Clamp(currentPos.y - targetY, -0.6f, 0.6f);
        Vector3 wallDisplacement = Vector3.Project(currentPos - _smoothedDesiredPosition, averageNormal);
        Vector3 springForce = -stats.SpringStiffness * wallDisplacement;

        Vector3 lateralRight = _ctx.PlayerTransform.right;
        Vector3 lateralDisplacement = Vector3.Project(currentPos - _smoothedDesiredPosition, lateralRight);

        float inputX = _ctx.MoveInput.x;
        // Dinamik Yanal Fizik Limiti
        float maxStretch = stats.GripReachDistance * 0.7f;
        float smoothedDrift = Vector3.Dot(lateralDisplacement, lateralRight);

        if (smoothedDrift > maxStretch && inputX > 0) inputX = 0f;
        if (smoothedDrift < -maxStretch && inputX < 0) inputX = 0f;

        Vector3 lateralSpring = Vector3.zero;
        if (Mathf.Abs(smoothedDrift) > maxStretch)
        {
            float overStretch = Mathf.Abs(smoothedDrift) - maxStretch;
            lateralSpring = -Mathf.Sign(smoothedDrift) * lateralRight * (overStretch * stats.SpringStiffness * 2f);
        }
        else if (Mathf.Abs(inputX) < 0.01f && _shiftPhase == 0)
        {
            lateralSpring = -lateralDisplacement * (stats.SpringStiffness * 0.15f);
        }

        Vector3 swingForce = lateralRight * (inputX * stats.SwingAmplitude * 25f);
        Vector3 horizontalVel = Vector3.Project(_ctx.Velocity, lateralRight);
        Vector3 horizontalFriction = -horizontalVel * (_springDamping * 1.5f);
        float verticalSpring = -stats.SpringStiffness * deltaY;
        float verticalDamping = -_springDamping * _ctx.Velocity.y;

        Vector3 totalClimbForce = springForce + swingForce + lateralSpring + horizontalFriction + new Vector3(0f, verticalSpring + verticalDamping, 0f);
        totalClimbForce -= Physics.gravity;

        if (_ctx.MoveInput.sqrMagnitude < 0.01f && _shiftPhase == 0 && Mathf.Abs(smoothedDrift) < 0.1f)
        {
            if (totalClimbForce.magnitude < 5f)
            {
                totalClimbForce = Vector3.zero;
                _smoothedDesiredPosition = currentPos;
            }
        }

        if (_ctx.Sensor.IsGrounded && _ctx.MoveInput.sqrMagnitude < 0.01f)
        {
            _ctx.SetVelocity(new Vector3(0f, _ctx.Velocity.y, 0f));
            totalClimbForce = new Vector3(0f, totalClimbForce.y, 0f);
        }

        totalClimbForce = Vector3.ClampMagnitude(totalClimbForce, 25f);
        _ctx.ApplyClimbForce(totalClimbForce);
    }
}