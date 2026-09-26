using UnityEngine;
using UnityEngine.InputSystem; // New Input System kütüphanesi

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class BearController : MonoBehaviour
{
    private Animator _animator;
    private Rigidbody _rb;
    public float moveSpeed = 8f;

    private float _currentInput = 0f;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // W/S tuşlarına göre 1 veya -1 değeri alıyoruz
        _currentInput = 0f;
        if (Keyboard.current.wKey.isPressed) _currentInput = 1f;
        else if (Keyboard.current.sKey.isPressed) _currentInput = -1f;

        _animator.SetFloat("Speed", Mathf.Abs(_currentInput));

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            _animator.SetTrigger("Attack");
        }
    }

    void FixedUpdate()
    {
        Vector3 moveVelocity = transform.forward * _currentInput * moveSpeed;
        _rb.linearVelocity = new Vector3(moveVelocity.x, _rb.linearVelocity.y, moveVelocity.z);
    }
}