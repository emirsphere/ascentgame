using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        public bool leftGrip;
        public bool rightGrip;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
        private InputAction _leftGripAction;
        private InputAction _rightGripAction;
        private bool _loggedMissingLeftGripAction;
        private bool _loggedMissingRightGripAction;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput?.actions == null) return;

            _leftGripAction = _playerInput.actions.FindAction("LeftGrip", throwIfNotFound: false);
            _rightGripAction = _playerInput.actions.FindAction("RightGrip", throwIfNotFound: false);
        }

        private void Update()
        {
            if (_leftGripAction != null)
                leftGrip = _leftGripAction.IsPressed();
            else if (!_loggedMissingLeftGripAction)
            {
                Debug.LogWarning("[StarterAssetsInputs] Missing Input System action binding: LeftGrip");
                _loggedMissingLeftGripAction = true;
            }

            if (_rightGripAction != null)
                rightGrip = _rightGripAction.IsPressed();
            else if (!_loggedMissingRightGripAction)
            {
                Debug.LogWarning("[StarterAssetsInputs] Missing Input System action binding: RightGrip");
                _loggedMissingRightGripAction = true;
            }
        }

        public void OnMove(InputValue value) => MoveInput(value.Get<Vector2>());
        public void OnLook(InputValue value)
        {
            if (cursorInputForLook) LookInput(value.Get<Vector2>());
        }
        public void OnJump(InputValue value) => JumpInput(value.isPressed);
        public void OnSprint(InputValue value) => SprintInput(value.isPressed);
        public void OnLeftGrip(InputValue value) => LeftGripInput(value.isPressed);
        public void OnRightGrip(InputValue value) => RightGripInput(value.isPressed);
#endif

        public void MoveInput(Vector2 newMoveDirection) => move = newMoveDirection;
        public void LookInput(Vector2 newLookDirection) => look = newLookDirection;
        public void JumpInput(bool newJumpState) => jump = newJumpState;
        public void SprintInput(bool newSprintState) => sprint = newSprintState;

        public void LeftGripInput(bool newGripState) => leftGrip = newGripState;
        public void RightGripInput(bool newGripState) => rightGrip = newGripState;

        private void OnApplicationFocus(bool hasFocus) => SetCursorState(cursorLocked);
        private void SetCursorState(bool newState) => Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
