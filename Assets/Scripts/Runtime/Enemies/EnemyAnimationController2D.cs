using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationController2D : MonoBehaviour
    {
        private enum FacingSource
        {
            Target,
            Movement,
            None
        }

        [Header("Bindings")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private Health health;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool flipAllChildSpriteRenderers = true;

        [Header("Facing")]
        [SerializeField] private FacingSource facingSource = FacingSource.Target;
        [SerializeField] private bool useHorizontalFlip = true;
        [SerializeField] private bool spriteFacesRightByDefault = true;
        [SerializeField] private bool keepEnemyRootUpright = true;
        [SerializeField, Min(0f)] private float directionDeadZone = 0.05f;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float fallbackMoveSpeedForNormalizedSpeed = 2f;

        private readonly HashSet<int> parameters = new HashSet<int>();

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int FacingXHash = Animator.StringToHash("FacingX");
        private static readonly int FacingYHash = Animator.StringToHash("FacingY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int SpawnHash = Animator.StringToHash("Spawn");
        private static readonly int WindUpHash = Animator.StringToHash("WindUp");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int ShootHash = Animator.StringToHash("Shoot");
        private static readonly int DashPrepareHash = Animator.StringToHash("DashPrepare");
        private static readonly int DashHash = Animator.StringToHash("Dash");
        private static readonly int DashEndHash = Animator.StringToHash("DashEnd");
        private static readonly int RecoveryHash = Animator.StringToHash("Recovery");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private Vector2 lastFacingDirection = Vector2.down;
        private SpriteRenderer[] spriteRenderers;
        private bool[] baseSpriteFlipX;
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
            if (enemy == null)
            {
                return;
            }

            Vector2 velocity = enemy.Body != null ? enemy.Body.linearVelocity : Vector2.zero;
            float speed = velocity.magnitude;
            float normalizedSpeed = Mathf.Clamp01(speed / GetReferenceMoveSpeed());
            bool isMoving = speed > directionDeadZone;

            SetFloat(SpeedHash, normalizedSpeed);
            SetFloat(MoveXHash, velocity.x);
            SetFloat(MoveYHash, velocity.y);
            SetBool(IsMovingHash, isMoving);

            Vector2 facingDirection = ResolveFacingDirection(velocity);
            if (facingDirection.sqrMagnitude > directionDeadZone * directionDeadZone)
            {
                lastFacingDirection = facingDirection.normalized;
            }

            SetFloat(FacingXHash, lastFacingDirection.x);
            SetFloat(FacingYHash, lastFacingDirection.y);
            UpdateHorizontalFlip(lastFacingDirection);
            KeepRootUpright();
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
            enemy ??= GetComponentInParent<EnemyController>();
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

            RefreshSpriteRenderers();
        }

        private void BindEvents()
        {
            if (isBound)
            {
                return;
            }

            if (enemy != null)
            {
                enemy.AnimationSignalRaised += HandleAnimationSignal;
            }

            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }

            isBound = true;
        }

        private void UnbindEvents()
        {
            if (!isBound)
            {
                return;
            }

            if (enemy != null)
            {
                enemy.AnimationSignalRaised -= HandleAnimationSignal;
            }

            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            isBound = false;
        }

        private void HandleAnimationSignal(EnemyAnimationSignal signal)
        {
            switch (signal)
            {
                case EnemyAnimationSignal.Spawn:
                    SetBool(IsDashingHash, false);
                    SetBool(IsDeadHash, false);
                    SetTrigger(SpawnHash);
                    break;
                case EnemyAnimationSignal.WindUp:
                    SetTrigger(WindUpHash);
                    break;
                case EnemyAnimationSignal.Attack:
                    SetTrigger(AttackHash);
                    break;
                case EnemyAnimationSignal.Shoot:
                    SetTrigger(ShootHash);
                    break;
                case EnemyAnimationSignal.DashPrepare:
                    SetBool(IsDashingHash, false);
                    SetTrigger(DashPrepareHash);
                    break;
                case EnemyAnimationSignal.Dash:
                    SetBool(IsDashingHash, true);
                    SetTrigger(DashHash);
                    break;
                case EnemyAnimationSignal.DashEnd:
                    SetBool(IsDashingHash, false);
                    SetTrigger(DashEndHash);
                    break;
                case EnemyAnimationSignal.Recovery:
                    SetBool(IsDashingHash, false);
                    SetTrigger(RecoveryHash);
                    break;
                case EnemyAnimationSignal.Hit:
                    SetBool(IsDashingHash, false);
                    SetTrigger(HitHash);
                    break;
                case EnemyAnimationSignal.Death:
                    SetBool(IsDashingHash, false);
                    SetBool(IsDeadHash, true);
                    SetTrigger(DeathHash);
                    break;
            }
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (damage.Direction.sqrMagnitude > directionDeadZone * directionDeadZone)
            {
                lastFacingDirection = -damage.Direction.normalized;
            }

            SetTrigger(HitHash);
        }

        private Vector2 ResolveFacingDirection(Vector2 velocity)
        {
            return facingSource switch
            {
                FacingSource.Target => enemy != null && enemy.DirectionToTarget.sqrMagnitude > directionDeadZone * directionDeadZone
                    ? enemy.DirectionToTarget
                    : lastFacingDirection,
                FacingSource.Movement => velocity.sqrMagnitude > directionDeadZone * directionDeadZone
                    ? velocity
                    : lastFacingDirection,
                _ => lastFacingDirection
            };
        }

        private float GetReferenceMoveSpeed()
        {
            if (enemy != null && enemy.Definition != null)
            {
                return Mathf.Max(0.01f, enemy.Definition.MoveSpeed);
            }

            return Mathf.Max(0.01f, fallbackMoveSpeedForNormalizedSpeed);
        }

        private void UpdateHorizontalFlip(Vector2 direction)
        {
            if (!useHorizontalFlip || Mathf.Abs(direction.x) <= directionDeadZone)
            {
                return;
            }

            bool facesLeft = direction.x < 0f;
            bool shouldFlip = spriteFacesRightByDefault ? facesLeft : !facesLeft;

            if (flipAllChildSpriteRenderers && spriteRenderers != null && spriteRenderers.Length > 0)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    SpriteRenderer targetRenderer = spriteRenderers[i];
                    if (targetRenderer == null)
                    {
                        continue;
                    }

                    bool baseFlip = baseSpriteFlipX != null && i < baseSpriteFlipX.Length && baseSpriteFlipX[i];
                    targetRenderer.flipX = baseFlip != shouldFlip;
                }

                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = shouldFlip;
            }
        }

        private void RefreshSpriteRenderers()
        {
            if (!flipAllChildSpriteRenderers)
            {
                spriteRenderers = spriteRenderer != null ? new[] { spriteRenderer } : null;
                baseSpriteFlipX = spriteRenderer != null ? new[] { spriteRenderer.flipX } : null;
                return;
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers == null)
            {
                baseSpriteFlipX = null;
                return;
            }

            baseSpriteFlipX = new bool[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer targetRenderer = spriteRenderers[i];
                baseSpriteFlipX[i] = targetRenderer != null && targetRenderer.flipX;
            }
        }

        private void KeepRootUpright()
        {
            if (!keepEnemyRootUpright || enemy == null)
            {
                return;
            }

            enemy.transform.rotation = Quaternion.identity;
        }

        private void ApplyImmediateState()
        {
            if (animator == null)
            {
                return;
            }

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
