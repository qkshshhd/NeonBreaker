using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using NeonBreaker.Shared.StateMachine;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public sealed class ShooterEnemyBehavior : EnemyBehaviorBase
    {
        [SerializeField] private RangedAttackDefinition attackDefinition;
        [SerializeField] private Transform firePoint;
        [SerializeField] private bool logSetupProblems = true;

        private readonly StateMachine stateMachine = new StateMachine();
        private readonly RaycastHit2D[] lineOfSightHits = new RaycastHit2D[16];

        private float cooldownTimer;
        private bool setupLogged;

        private IdleState idleState;
        private PositionState positionState;
        private WindUpState windUpState;
        private FireState fireState;
        private RecoveryState recoveryState;
        private HitReactState hitReactState;

        private EnemyDefinition EnemyDefinition => Controller != null ? Controller.Definition : null;
        private bool CanFire => cooldownTimer <= 0f && attackDefinition != null && attackDefinition.ProjectilePoolKey != null;

        public override void Initialize(EnemyController controller)
        {
            base.Initialize(controller);

            idleState = new IdleState(this);
            positionState = new PositionState(this);
            windUpState = new WindUpState(this);
            fireState = new FireState(this);
            recoveryState = new RecoveryState(this);
            hitReactState = new HitReactState(this);
        }

        public override void OnSpawned()
        {
            cooldownTimer = 0f;
            setupLogged = false;
            ValidateSetup();
            stateMachine.ChangeState(idleState);
        }

        public override void OnDespawned()
        {
            cooldownTimer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= deltaTime;
            }

            stateMachine.Tick(deltaTime);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            stateMachine.FixedTick(fixedDeltaTime);
        }

        public override void OnDamaged(DamageInfo damage)
        {
            if (damage.Knockback > 0f)
            {
                stateMachine.ChangeState(hitReactState);
            }
        }

        public override void OnDeath()
        {
            if (Controller != null && Controller.Body != null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
            }
        }

        private void MoveForRange()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null || attackDefinition == null || !Controller.HasTarget)
            {
                Stop();
                return;
            }

            float distance = Controller.DistanceToTarget;
            if (distance < attackDefinition.RetreatRange)
            {
                Vector2 retreatDirection = -Controller.DirectionToTarget;
                Controller.Move(
                    retreatDirection,
                    enemyDefinition.MoveSpeed * attackDefinition.RetreatSpeedMultiplier,
                    enemyDefinition.Acceleration);
            }
            else if (distance > attackDefinition.PreferredRange)
            {
                Controller.MoveTowardTarget(enemyDefinition.MoveSpeed, enemyDefinition.Acceleration);
            }
            else
            {
                Stop();
            }

            Controller.RotateToward(Controller.DirectionToTarget, enemyDefinition.RotationSpeed);
        }

        private void Stop()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
                return;
            }

            Controller.Stop(enemyDefinition.Deceleration);
        }

        private bool IsInFireRange()
        {
            return attackDefinition != null
                && Controller.HasTarget
                && Controller.DistanceToTarget <= attackDefinition.FireRange;
        }

        private bool HasLineOfSight()
        {
            if (attackDefinition == null || !attackDefinition.RequireLineOfSight || !Controller.HasTarget)
            {
                return true;
            }

            Vector2 origin = GetFirePosition();
            Vector2 targetPosition = Controller.Target.position;
            Vector2 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.0001f)
            {
                return true;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(attackDefinition.LineOfSightBlockers);
            filter.useTriggers = false;

            int hitCount = Physics2D.Raycast(origin, toTarget.normalized, filter, lineOfSightHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = lineOfSightHits[i].collider;
                if (hit == null || ShouldIgnoreLineOfSightHit(hit))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void Fire()
        {
            if (attackDefinition == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Missing RangedAttackDefinition.");
                return;
            }

            if (attackDefinition.ProjectilePoolKey == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Missing Projectile PoolKey on RangedAttackDefinition.");
                return;
            }

            if (ObjectPoolManager.Instance == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] ObjectPoolManager.Instance is missing. Make sure ObjectPoolManager has Make Global Instance enabled.");
                return;
            }

            int count = attackDefinition.ProjectileCount;
            float spread = attackDefinition.SpreadAngle;
            float startAngle = count <= 1 ? 0f : -spread * 0.5f;
            float step = count <= 1 ? 0f : spread / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                Vector2 direction = Rotate(Controller.DirectionToTarget, angle);
                SpawnProjectile(direction);
            }

            cooldownTimer = attackDefinition.Cooldown;
        }

        private void SpawnProjectile(Vector2 direction)
        {
            Vector3 position = GetFirePosition();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            EnemyProjectile2D projectile = ObjectPoolManager.Instance.Spawn<EnemyProjectile2D>(
                attackDefinition.ProjectilePoolKey,
                position,
                Quaternion.Euler(0f, 0f, angle));

            if (projectile == null)
            {
                Debug.LogError($"[ShooterEnemyBehavior] Projectile pool '{attackDefinition.ProjectilePoolKey.name}' did not spawn an EnemyProjectile2D.", this);
                return;
            }

            projectile.Launch(attackDefinition.ProjectileDefinition, direction, gameObject);
        }

        private void ValidateSetup()
        {
            if (!logSetupProblems)
            {
                return;
            }

            if (Controller == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Controller was not initialized.");
                return;
            }

            if (EnemyDefinition == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] EnemyController.Definition is missing. Shooter needs DetectionRange/MoveSpeed from EnemyDefinition.");
            }

            if (attackDefinition == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Attack Definition is missing.");
                return;
            }

            if (attackDefinition.ProjectilePoolKey == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Projectile PoolKey is missing.");
            }

            if (attackDefinition.ProjectileDefinition == null)
            {
                LogSetupProblem("[ShooterEnemyBehavior] Projectile Definition is missing. The projectile prefab must have a Default Definition or the projectile will immediately despawn.");
            }

            if (attackDefinition.RequireLineOfSight && attackDefinition.LineOfSightBlockers.value == 0)
            {
                Debug.LogWarning("[ShooterEnemyBehavior] Require Line Of Sight is enabled, but Line Of Sight Blockers is empty. The shooter will ignore walls.", this);
            }
        }

        private void LogSetupProblem(string message)
        {
            if (!logSetupProblems || setupLogged)
            {
                return;
            }

            setupLogged = true;
            Debug.LogWarning(message, this);
        }

        private Vector3 GetFirePosition()
        {
            if (firePoint != null)
            {
                return firePoint.position;
            }

            Vector2 direction = Controller.DirectionToTarget.sqrMagnitude > 0.0001f
                ? Controller.DirectionToTarget
                : (Vector2)transform.right;
            return transform.position + (Vector3)(direction * (attackDefinition != null ? attackDefinition.MuzzleForwardOffset : 0.45f));
        }

        private bool ShouldIgnoreLineOfSightHit(Collider2D hit)
        {
            if (hit.transform.IsChildOf(transform))
            {
                return true;
            }

            if (Controller != null && Controller.Target != null && hit.transform.IsChildOf(Controller.Target))
            {
                return true;
            }

            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRoomEnemy)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            if (Mathf.Abs(degrees) <= 0.0001f)
            {
                return vector.normalized;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos).normalized;
        }

        private void ChangeToPositionOrIdle()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null || !Controller.HasTarget || Controller.DistanceToTarget > enemyDefinition.DetectionRange)
            {
                stateMachine.ChangeState(idleState);
            }
            else
            {
                stateMachine.ChangeState(positionState);
            }
        }

        private abstract class BehaviorState : IState
        {
            protected readonly ShooterEnemyBehavior Behavior;

            protected BehaviorState(ShooterEnemyBehavior behavior)
            {
                Behavior = behavior;
            }

            public virtual void Enter() { }
            public virtual void Tick(float deltaTime) { }
            public virtual void FixedTick(float fixedDeltaTime) { }
            public virtual void Exit() { }
        }

        private sealed class IdleState : BehaviorState
        {
            public IdleState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition enemyDefinition = Behavior.EnemyDefinition;
                if (enemyDefinition != null
                    && Behavior.Controller.HasTarget
                    && Behavior.Controller.DistanceToTarget <= enemyDefinition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.positionState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class PositionState : BehaviorState
        {
            public PositionState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition enemyDefinition = Behavior.EnemyDefinition;
                if (enemyDefinition == null
                    || Behavior.attackDefinition == null
                    || !Behavior.Controller.HasTarget
                    || Behavior.Controller.DistanceToTarget > enemyDefinition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.idleState);
                    return;
                }

                if (Behavior.CanFire && Behavior.IsInFireRange() && Behavior.HasLineOfSight())
                {
                    Behavior.stateMachine.ChangeState(Behavior.windUpState);
                    return;
                }

                if (Behavior.CanFire
                    && Behavior.attackDefinition != null
                    && Behavior.Controller.DistanceToTarget <= Behavior.attackDefinition.FireRange
                    && !Behavior.HasLineOfSight())
                {
                    Behavior.LogSetupProblem("[ShooterEnemyBehavior] Target is in range, but Line Of Sight is blocked. Check Line Of Sight Blockers.");
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.MoveForRange();
            }
        }

        private sealed class WindUpState : BehaviorState
        {
            private float timer;

            public WindUpState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.attackDefinition != null ? Behavior.attackDefinition.WindUpTime : 0.2f;
                Behavior.Stop();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.WindUp);
            }

            public override void Tick(float deltaTime)
            {
                if (!Behavior.Controller.HasTarget)
                {
                    Behavior.ChangeToPositionOrIdle();
                    return;
                }

                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.fireState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
                EnemyDefinition enemyDefinition = Behavior.EnemyDefinition;
                if (enemyDefinition != null)
                {
                    Behavior.Controller.RotateToward(Behavior.Controller.DirectionToTarget, enemyDefinition.RotationSpeed);
                }
            }
        }

        private sealed class FireState : BehaviorState
        {
            public FireState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Shoot);
                Behavior.Fire();
                Behavior.stateMachine.ChangeState(Behavior.recoveryState);
            }
        }

        private sealed class RecoveryState : BehaviorState
        {
            private float timer;

            public RecoveryState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.attackDefinition != null ? Behavior.attackDefinition.RecoveryTime : 0.2f;
                Behavior.Stop();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Recovery);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.ChangeToPositionOrIdle();
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class HitReactState : BehaviorState
        {
            private float timer;

            public HitReactState(ShooterEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.EnemyDefinition != null ? Behavior.EnemyDefinition.HitStunDuration : 0.12f;
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer > 0f)
                {
                    return;
                }

                if (Behavior.Controller.KnockbackReceiver != null && Behavior.Controller.KnockbackReceiver.IsReceivingKnockback)
                {
                    return;
                }

                Behavior.ChangeToPositionOrIdle();
            }
        }
    }
}
