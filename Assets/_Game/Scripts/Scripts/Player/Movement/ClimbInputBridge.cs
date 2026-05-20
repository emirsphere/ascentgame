using StarterAssets;
using UnityEngine;

public readonly struct ClimbInputData
{
    public Vector2 moveInput { get; }
    public float verticalIntent { get; }
    public float horizontalIntent { get; }
    public bool jumpOff { get; }

    public ClimbInputData(Vector2 moveInput, bool jumpOff)
    {
        this.moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        verticalIntent = Mathf.Clamp(this.moveInput.y, -1f, 1f);
        horizontalIntent = Mathf.Clamp(this.moveInput.x, -1f, 1f);
        this.jumpOff = jumpOff;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(StarterAssetsInputs))]
public class ClimbInputBridge : MonoBehaviour
{
    [SerializeField] private StarterAssetsInputs _input;

    public ClimbInputData CurrentInput { get; private set; }

    private void Awake()
    {
        if (_input == null)
            _input = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (_input == null)
        {
            CurrentInput = default;
            return;
        }

        CurrentInput = new ClimbInputData(_input.move, _input.jump);
    }
}
