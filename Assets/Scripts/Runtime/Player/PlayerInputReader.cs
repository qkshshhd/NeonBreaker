#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

#if ENABLE_INPUT_SYSTEM
        [Header("Input System Actions")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference skillAction;
        [SerializeField] private InputActionReference dashAction;
        [SerializeField] private InputActionReference pauseAction;
#endif

        public Vector2 MoveInput { get; private set; }
        public Vector2 AimWorldPosition { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public bool AttackPressed { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool SkillPressed { get; private set; }
        public bool DashPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool GameplayInputBlocked => gameplayInputLocks.Count > 0;

        private readonly HashSet<object> gameplayInputLocks = new HashSet<object>();

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(lookAction);
            EnableAction(attackAction);
            EnableAction(skillAction);
            EnableAction(dashAction);
            EnableAction(pauseAction);
        }

        private void OnDisable()
        {
            DisableAction(moveAction);
            DisableAction(lookAction);
            DisableAction(attackAction);
            DisableAction(skillAction);
            DisableAction(dashAction);
            DisableAction(pauseAction);
        }
#endif

        private void Update()
        {
            ReadMove();

            if (MoveInput.sqrMagnitude > 1f)
            {
                MoveInput = MoveInput.normalized;
            }

            ReadButtons();

            if (GameplayInputBlocked)
            {
                ClearGameplayInputs();
            }

            UpdateAim();
        }

        public void PushGameplayInputLock(object owner)
        {
            if (owner != null)
            {
                gameplayInputLocks.Add(owner);
            }

            ClearGameplayInputs();
        }

        public void ReleaseGameplayInputLock(object owner)
        {
            if (owner != null)
            {
                gameplayInputLocks.Remove(owner);
            }

            if (GameplayInputBlocked)
            {
                ClearGameplayInputs();
            }
        }

        public void ClearAllGameplayInputLocks()
        {
            gameplayInputLocks.Clear();
        }

        private void ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            if (moveAction != null && moveAction.action != null)
            {
                MoveInput = moveAction.action.ReadValue<Vector2>();
                return;
            }
#endif

            MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        private void ReadButtons()
        {
#if ENABLE_INPUT_SYSTEM
            AttackPressed = WasPressedThisFrame(attackAction);
            AttackHeld = IsPressed(attackAction);
            SkillPressed = WasPressedThisFrame(skillAction);
            DashPressed = WasPressedThisFrame(dashAction);
            PausePressed = WasPressedThisFrame(pauseAction);

            if (attackAction != null || skillAction != null || dashAction != null || pauseAction != null)
            {
                return;
            }
#endif

            AttackPressed = Input.GetMouseButtonDown(0);
            AttackHeld = Input.GetMouseButton(0);
            SkillPressed = Input.GetMouseButtonDown(1);
            DashPressed = Input.GetKeyDown(KeyCode.Space);
            PausePressed = Input.GetKeyDown(KeyCode.Escape);
        }

        private void ClearGameplayInputs()
        {
            MoveInput = Vector2.zero;
            AttackPressed = false;
            AttackHeld = false;
            SkillPressed = false;
            DashPressed = false;
        }

        private void UpdateAim()
        {
            if (targetCamera == null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            Vector2 screenPosition = lookAction != null && lookAction.action != null
                ? lookAction.action.ReadValue<Vector2>()
                : Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : (Vector2)Input.mousePosition;
#else
            Vector2 screenPosition = Input.mousePosition;
#endif

            Vector3 mousePosition = screenPosition;
            Vector3 worldPosition = targetCamera.ScreenToWorldPoint(mousePosition);
            AimWorldPosition = worldPosition;

            Vector2 direction = AimWorldPosition - (Vector2)transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                AimDirection = direction.normalized;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnableAction(InputActionReference actionReference)
        {
            if (actionReference != null && actionReference.action != null)
            {
                actionReference.action.Enable();
            }
        }

        private static void DisableAction(InputActionReference actionReference)
        {
            if (actionReference != null && actionReference.action != null)
            {
                actionReference.action.Disable();
            }
        }

        private static bool WasPressedThisFrame(InputActionReference actionReference)
        {
            return actionReference != null
                && actionReference.action != null
                && actionReference.action.WasPressedThisFrame();
        }

        private static bool IsPressed(InputActionReference actionReference)
        {
            return actionReference != null
                && actionReference.action != null
                && actionReference.action.IsPressed();
        }
#endif
    }
}
