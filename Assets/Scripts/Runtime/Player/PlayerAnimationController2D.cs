using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Skills;
using UnityEngine;

namespace NeonBreaker.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController2D : MonoBehaviour
    {
        private enum FacingMode
        {
            Aim,
            Movement,
            AimWhileActing
        }

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private MeleeAttack2D meleeAttack;
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private Health health;

        [Header("Direction")]
        [SerializeField] private FacingMode facingMode = FacingMode.AimWhileActing;
        [SerializeField] private bool useHorizontalFlip = true;
        [SerializeField] private bool spriteFacesRightByDefault = true;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.05f;
        [SerializeField, Min(0f)] private float actionFacingHoldTime = 0.2f;

        [Header("Attack")]
        [SerializeField, Min(1)] private int attackAnimationCount = 2;
        [SerializeField] private bool alternateAttackAnimations = true;
        [SerializeField] private bool useMeleeComboAnimationIndex = true;

        private readonly HashSet<int> parameters = new HashSet<int>();

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int FacingXHash = Animator.StringToHash("FacingX");
        private static readonly int FacingYHash = Animator.StringToHash("FacingY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
        private static readonly int SkillIndexHash = Animator.StringToHash("SkillIndex");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int SkillHash = Animator.StringToHash("Skill");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private Vector2 lastFacingDirection = Vector2.down;
        private Vector2 actionFacingDirection = Vector2.down;
        private float actionFacingTimer;
        private int nextAttackIndex;
        private bool isBound;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            CacheAnimatorParameters();
            ApplyImmediateState();
        }

        private void OnEnable()
        {
            BindEvents();
            ApplyImmediateState();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (animator == null || input == null)
            {
                return;
            }

            if (actionFacingTimer > 0f)
            {
                actionFacingTimer -= Time.deltaTime;
            }

            Vector2 moveDirection = input.MoveInput;
            Vector2 facingDirection = ResolveFacingDirection(moveDirection);
            bool isMoving = moveDirection.sqrMagnitude > directionDeadZone * directionDeadZone;

            SetFloat(SpeedHash, Mathf.Clamp01(moveDirection.magnitude));
            SetFloat(MoveXHash, moveDirection.x);
            SetFloat(MoveYHash, moveDirection.y);
            SetBool(IsMovingHash, isMoving);

            if (facingDirection.sqrMagnitude > directionDeadZone * directionDeadZone)
            {
                lastFacingDirection = facingDirection.normalized;
            }

            SetFloat(FacingXHash, lastFacingDirection.x);
            SetFloat(FacingYHash, lastFacingDirection.y);
            UpdateHorizontalFlip(lastFacingDirection);
        }

        public void RefreshBindings()
        {
            UnbindEvents();
            ResolveBindings();
            CacheAnimatorParameters();
            BindEvents();
            ApplyImmediateState();
        }

        private void ResolveBindings()
        {
            input ??= GetComponentInParent<PlayerInputReader>();
            meleeAttack ??= GetComponentInParent<MeleeAttack2D>();
            dash ??= GetComponentInParent<PlayerDash2D>();
            skillController ??= GetComponentInParent<PlayerSkillController>();
            health ??= GetComponentInParent<Health>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (spriteRenderer == null && animator != null)
            {
                spriteRenderer = animator.GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void BindEvents()
        {
            if (isBound)
            {
                return;
            }

            if (meleeAttack != null)
            {
                meleeAttack.AttackStarted += HandleAttackStarted;
            }

            if (dash != null)
            {
                dash.DashStarted += HandleDashStarted;
                dash.DashEnded += HandleDashEnded;
            }

            if (skillController != null)
            {
                skillController.SkillStarted += HandleSkillStarted;
            }

            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }

            isBound = true;
        }

        private void UnbindEvents()
        {
            if (!isBound)
            {
                return;
            }

            if (meleeAttack != null)
            {
                meleeAttack.AttackStarted -= HandleAttackStarted;
            }

            if (dash != null)
            {
                dash.DashStarted -= HandleDashStarted;
                dash.DashEnded -= HandleDashEnded;
            }

            if (skillController != null)
            {
                skillController.SkillStarted -= HandleSkillStarted;
            }

            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            isBound = false;
        }

        private void HandleAttackStarted()
        {
            CaptureActionFacing();

            int animationCount = Mathf.Max(1, attackAnimationCount);
            int attackIndex = useMeleeComboAnimationIndex && meleeAttack != null
                ? Mathf.Clamp(meleeAttack.CurrentAttackAnimationIndex, 0, animationCount - 1)
                : alternateAttackAnimations ? nextAttackIndex : 0;
            SetInteger(AttackIndexHash, attackIndex);
            SetTrigger(AttackHash);

            if (!useMeleeComboAnimationIndex && alternateAttackAnimations)
            {
                nextAttackIndex = (nextAttackIndex + 1) % animationCount;
            }
        }

        private void HandleDashStarted()
        {
            Vector2 direction = dash != null ? dash.LastDashDirection : Vector2.zero;
            CaptureActionFacing(direction);
            SetBool(IsDashingHash, true);
        }

        private void HandleDashEnded()
        {
            SetBool(IsDashingHash, false);
        }

        private void HandleSkillStarted(SkillDefinition skill)
        {
            CaptureActionFacing();
            SetInteger(SkillIndexHash, skill != null ? (int)skill.SkillType : 0);
            SetTrigger(SkillHash);
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (damage.Direction.sqrMagnitude > directionDeadZone * directionDeadZone)
            {
                CaptureActionFacing(-damage.Direction);
            }

            SetTrigger(HitHash);
        }

        private void HandleDied()
        {
            SetBool(IsDeadHash, true);
            SetBool(IsDashingHash, false);
            SetTrigger(DeathHash);
        }

        private void CaptureActionFacing()
        {
            Vector2 direction = input != null ? input.AimDirection : lastFacingDirection;
            CaptureActionFacing(direction);
        }

        private void CaptureActionFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude <= directionDeadZone * directionDeadZone)
            {
                direction = lastFacingDirection;
            }

            actionFacingDirection = direction.normalized;
            actionFacingTimer = actionFacingHoldTime;
        }

        private Vector2 ResolveFacingDirection(Vector2 moveDirection)
        {
            Vector2 aimDirection = input != null ? input.AimDirection : lastFacingDirection;

            return facingMode switch
            {
                FacingMode.Aim => aimDirection,
                FacingMode.Movement => moveDirection.sqrMagnitude > directionDeadZone * directionDeadZone
                    ? moveDirection
                    : lastFacingDirection,
                FacingMode.AimWhileActing => actionFacingTimer > 0f
                    ? actionFacingDirection
                    : moveDirection.sqrMagnitude > directionDeadZone * directionDeadZone
                        ? moveDirection
                        : aimDirection,
                _ => aimDirection
            };
        }

        private void UpdateHorizontalFlip(Vector2 direction)
        {
            if (!useHorizontalFlip || spriteRenderer == null || Mathf.Abs(direction.x) <= directionDeadZone)
            {
                return;
            }

            bool facesLeft = direction.x < 0f;
            spriteRenderer.flipX = spriteFacesRightByDefault ? facesLeft : !facesLeft;
        }

        private void ApplyImmediateState()
        {
            if (animator == null)
            {
                return;
            }

            SetBool(IsDashingHash, dash != null && dash.IsDashing);
            SetBool(IsDeadHash, health != null && health.IsDead);
            SetFloat(FacingXHash, lastFacingDirection.x);
            SetFloat(FacingYHash, lastFacingDirection.y);
        }

        private void CacheAnimatorParameters()
        {
            parameters.Clear();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] animatorParameters = animator.parameters;
            for (int i = 0; i < animatorParameters.Length; i++)
            {
                parameters.Add(animatorParameters[i].nameHash);
            }
        }

        private void SetFloat(int parameterHash, float value)
        {
            if (animator != null && parameters.Contains(parameterHash))
            {
                animator.SetFloat(parameterHash, value);
            }
        }

        private void SetInteger(int parameterHash, int value)
        {
            if (animator != null && parameters.Contains(parameterHash))
            {
                animator.SetInteger(parameterHash, value);
            }
        }

        private void SetBool(int parameterHash, bool value)
        {
            if (animator != null && parameters.Contains(parameterHash))
            {
                animator.SetBool(parameterHash, value);
            }
        }

        private void SetTrigger(int parameterHash)
        {
            if (animator != null && parameters.Contains(parameterHash))
            {
                animator.SetTrigger(parameterHash);
            }
        }
    }
}
