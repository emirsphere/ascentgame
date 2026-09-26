using UnityEngine;

[RequireComponent(typeof(IPlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;

    private IPlayerController _player;

    // Animasyon Parametre Hash'leri (Performans için)
    private int _blendHash;
    private int _isClimbingHash;
    private int _isGroundedHash;
    private int _verticalVelocityHash;

    private void Awake()
    {
        _player = GetComponent<IPlayerController>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _blendHash = Animator.StringToHash("Blend");
        _isClimbingHash = Animator.StringToHash("IsClimbing");
        _isGroundedHash = Animator.StringToHash("IsGrounded");
        _verticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    }

    private void Update()
    {
        if (_player is StarterAssets.FirstPersonController fpc && !fpc.IsOwner) return;
        if (_animator == null || _player == null) return;

        bool isClimbing = _player.IsClimbing;
        _animator.SetBool(_isClimbingHash, isClimbing);

        if (isClimbing)
        {
            _animator.SetFloat(_blendHash, 0f, 0.1f, Time.deltaTime);
            _animator.speed = 1f;
            return;
        }

        // --- YATAY HIZ VE YÜRÜME ---
        float currentSpeed = _player.HorizontalSpeed;
        float normalizedSpeed = Mathf.Clamp01(currentSpeed / _player.Stats.SprintSpeed);
        _animator.SetFloat(_blendHash, normalizedSpeed, 0.1f, Time.deltaTime);

        // --- ZEMİN VE HAVA HIZI ---
        bool isGrounded = _player.Sensor.IsGrounded;
        float verticalVelocity = _player.Velocity.y;

        _animator.SetBool(_isGroundedHash, isGrounded);
        _animator.SetFloat(_verticalVelocityHash, verticalVelocity);

        // Ayak kaymasını önlemek için animasyon hızı (Sadece yerde yürürken çalışır)
        if (currentSpeed > 0.1f && isGrounded)
        {
            _animator.speed = currentSpeed / 4f;
        }
        else
        {
            _animator.speed = 1f;
        }
    }
}