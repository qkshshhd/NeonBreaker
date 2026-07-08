using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Shared.StateMachine;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public sealed class DashAttackEnemyBehavior : EnemyBehaviorBase
    {
        [SerializeField] private DashAttackDefinition definition;
        [SerializeField] private LineRenderer warningLine;
        [SerializeField] private bool createWarningLineIfMissing = true;

        private readonly StateMachine stateMachine = new StateMachine();
        private readonly Collider2D[] dashHitResults = new Collider2D[12];
        private readonly RaycastHit2D[] dashCastResults = new RaycastHit2D[12];
        private readonly Collider2D[] enemyIgnoreResults = new Collider2D[32];
        private readonly List<IgnoredCollisionPair> ignoredCollisionPairs = new List<IgnoredCollisionPair>();
        private static Material defaultWarningLineMaterial;

        private Vector2 lockedDashDirection;
        private Vector2 lastDashPosition;
        private bool damagedPlayerThisDash;
        private bool hitWallThisDash;

        private IdleState idleState;
        private ChaseState chaseState;
        private PrepareState prepareState;
        private DashState dashState;
        private RecoveryState recoveryState;
        private HitReactState hitReactState;

        private EnemyDefinition EnemyDefinition => Controller != null ? Controller.Definition : null;
        private float DistanceToTarget => Controller != null ? Controller.DistanceToTarget : float.PositiveInfinity;
        private bool HasTarget => Controller != null && Controller.HasTarget;
        private Vector2 DirectionToTarget => Controller != null ? Controller.DirectionToTarget : Vector2.zero;
        private bool CanPrepareDash => definition != null
            && HasTarget
            && DistanceToTarget <= definition.PrepareRange
            && DistanceToTarget >= definition.MinPrepareRange;
        private bool IsTooCloseToDash => definition != null
            && HasTarget
            && DistanceToTarget < definition.MinPrepareRange;

        private void Awake()
        {
            if (warningLine == null && createWarningLineIfMissing)
            {
                warningLine = CreateWarningLine();
            }

            ApplyWarningLineStyle(warningLine);
            SetWarningLineVisible(false);
        }

        public override void Initialize(EnemyController controller)
        {
            base.Initialize(controller);

            idleState = new IdleState(this);
            chaseState = new ChaseState(this);
            prepareState = new PrepareState(this);
            dashState = new DashState(this);
            recoveryState = new RecoveryState(this);
            hitReactState = new HitReactState(this);
        }

        public override void OnSpawned()
        {
            damagedPlayerThisDash = false;
            hitWallThisDash = false;
            ApplyWarningLineStyle(warningLine);
            SetWarningLineVisible(false);

            stateMachine.ChangeState(idleState);
        }

        public override void OnDespawned()
        {
            SetWarningLineVisible(false);
            RestoreIgnoredDashCollisions();
        }

        public override void Tick(float deltaTime)
        {
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
            SetWarningLineVisible(false);
            RestoreIgnoredDashCollisions();
            if (Controller != null && Controller.Body != null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
            }
        }

        public override void OnCollisionEnter2D(Collision2D collision)
        {
            if (stateMachine.CurrentState != dashState || definition == null)
            {
                return;
            }

            Collider2D other = collision.collider;
            if (((1 << other.gameObject.layer) & definition.WallLayers.value) != 0)
            {
                hitWallThisDash = true;
                return;
            }

            TryDamagePlayer(other);
        }

        public override void OnTriggerEnter2D(Collider2D other)
        {
            if (stateMachine.CurrentState == dashState)
            {
                TryDamagePlayer(other);
            }
        }

        private void MoveTowardTarget()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null)
            {
                Stop();
                return;
            }

            Controller.MoveTowardTarget(enemyDefinition.MoveSpeed, enemyDefinition.Acceleration);
            Controller.RotateToward(DirectionToTarget, enemyDefinition.RotationSpeed);
        }

        private void MoveAwayFromTarget()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null)
            {
                Stop();
                return;
            }

            Vector2 retreatDirection = -DirectionToTarget;
            float speed = enemyDefinition.MoveSpeed * (definition != null ? definition.RetreatSpeedMultiplier : 0.75f);
            Controller.Move(retreatDirection, speed, enemyDefinition.Acceleration);
            Controller.RotateToward(DirectionToTarget, enemyDefinition.RotationSpeed);
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

        private void LockDashDirection()
        {
            lockedDashDirection = DirectionToTarget.sqrMagnitude > 0.0001f ? DirectionToTarget : (Vector2)transform.right;
            Controller.RotateToward(lockedDashDirection, EnemyDefinition != null ? EnemyDefinition.RotationSpeed : 14f);
            UpdateWarningLine();
        }

        private void UpdateWarningLine()
        {
            if (warningLine == null)
            {
                return;
            }

            Vector3 start = transform.position;
            float lineLength = definition != null ? definition.WarningLineLength : 7f;
            Vector3 end = start + (Vector3)(lockedDashDirection.normalized * lineLength);
            warningLine.SetPosition(0, start);
            warningLine.SetPosition(1, end);
        }

        private void SetWarningLineVisible(bool visible)
        {
            if (warningLine != null)
            {
                warningLine.enabled = visible;
            }
        }

        private void TryDamagePlayer(Collider2D other)
        {
            if (damagedPlayerThisDash || definition == null || Controller == null || Controller.IsAttackSuppressed)
            {
                return;
            }

            if (((1 << other.gameObject.layer) & definition.HitLayers.value) == 0)
            {
                return;
            }

            IDamageable damageable = FindComponentInParents<IDamageable>(other);
            if (damageable == null || !damageable.CanTakeDamage)
            {
                return;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitDirection = lockedDashDirection.sqrMagnitude > 0.0001f ? lockedDashDirection.normalized : DirectionToTarget;
            DamageInfo damage = new DamageInfo(
                definition.ContactDamage,
                hitPoint,
                hitDirection,
                definition.ContactKnockback,
                false,
                gameObject);

            damageable.TakeDamage(damage);

            IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(other);
            knockbackReceiver?.ApplyKnockback(hitDirection, definition.ContactKnockback, definition.ContactKnockbackDuration);

            damagedPlayerThisDash = true;
        }

        private void ScanForDashHit()
        {
            if (definition == null || damagedPlayerThisDash)
            {
                return;
            }

            Vector2 direction = lockedDashDirection.sqrMagnitude > 0.0001f
                ? lockedDashDirection.normalized
                : DirectionToTarget;
            Vector2 center = (Vector2)transform.position + direction * definition.ContactCheckForwardOffset;

            Vector2 currentPosition = transform.position;
            Vector2 movement = currentPosition - lastDashPosition;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(definition.HitLayers);
            filter.useTriggers = true;

            if (movement.sqrMagnitude > 0.0001f)
            {
                int castCount = Physics2D.CircleCast(
                    lastDashPosition,
                    definition.ContactCheckRadius,
                    movement.normalized,
                    filter,
                    dashCastResults,
                    movement.magnitude + definition.ContactCheckForwardOffset);

                for (int i = 0; i < castCount; i++)
                {
                    Collider2D hit = dashCastResults[i].collider;
                    if (hit == null || IsOwnCollider(hit))
                    {
                        continue;
                    }

                    TryDamagePlayer(hit);
                    if (damagedPlayerThisDash)
                    {
                        lastDashPosition = currentPosition;
                        return;
                    }
                }
            }

            int hitCount = Physics2D.OverlapCircle(center, definition.ContactCheckRadius, filter, dashHitResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = dashHitResults[i];
                if (hit == null || IsOwnCollider(hit))
                {
                    continue;
                }

                TryDamagePlayer(hit);
                if (damagedPlayerThisDash)
                {
                    lastDashPosition = currentPosition;
                    return;
                }
            }

            lastDashPosition = currentPosition;
        }

        private void UpdateDashCollisionIgnore()
        {
            if (definition == null || !definition.IgnoreOtherEnemiesWhileDashing || Controller == null)
            {
                return;
            }

            float radius = definition.EnemyCollisionIgnoreRadius;
            if (radius <= 0f)
            {
                return;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(definition.EnemyCollisionIgnoreLayers);
            filter.useTriggers = false;

            int hitCount = Physics2D.OverlapCircle(transform.position, radius, filter, enemyIgnoreResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D other = enemyIgnoreResults[i];
                if (other == null || IsOwnCollider(other) || !IsOtherRoomEnemy(other))
                {
                    continue;
                }

                IgnoreCollisionWith(other);
            }
        }

        private void IgnoreCollisionWith(Collider2D other)
        {
            Collider2D[] selfColliders = Controller.Colliders;
            if (selfColliders == null)
            {
                return;
            }

            for (int i = 0; i < selfColliders.Length; i++)
            {
                Collider2D self = selfColliders[i];
                if (self == null || self.isTrigger)
                {
                    continue;
                }

                if (HasIgnoredPair(self, other))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(self, other, true);
                ignoredCollisionPairs.Add(new IgnoredCollisionPair(self, other));
            }
        }

        private void RestoreIgnoredDashCollisions()
        {
            for (int i = 0; i < ignoredCollisionPairs.Count; i++)
            {
                IgnoredCollisionPair pair = ignoredCollisionPairs[i];
                if (pair.Self != null && pair.Other != null)
                {
                    Physics2D.IgnoreCollision(pair.Self, pair.Other, false);
                }
            }

            ignoredCollisionPairs.Clear();
        }

        private bool HasIgnoredPair(Collider2D self, Collider2D other)
        {
            for (int i = 0; i < ignoredCollisionPairs.Count; i++)
            {
                IgnoredCollisionPair pair = ignoredCollisionPairs[i];
                if (pair.Self == self && pair.Other == other)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOwnCollider(Collider2D collider)
        {
            Collider2D[] selfColliders = Controller != null ? Controller.Colliders : null;
            if (selfColliders == null)
            {
                return false;
            }

            for (int i = 0; i < selfColliders.Length; i++)
            {
                if (selfColliders[i] == collider)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOtherRoomEnemy(Collider2D collider)
        {
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRoomEnemy && !ReferenceEquals(behaviours[i], Controller))
                {
                    return true;
                }
            }

            return false;
        }

        private LineRenderer CreateWarningLine()
        {
            GameObject lineObject = new GameObject("Dash Warning Line");
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            ApplyWarningLineStyle(line);
            return line;
        }

        private void ApplyWarningLineStyle(LineRenderer line)
        {
            if (line == null)
            {
                return;
            }

            float width = definition != null ? definition.WarningLineWidth : 0.08f;
            line.startWidth = width;
            line.endWidth = width;
            line.useWorldSpace = true;
            line.sharedMaterial = definition != null && definition.WarningLineMaterial != null
                ? definition.WarningLineMaterial
                : GetDefaultWarningLineMaterial();
            line.startColor = definition != null ? definition.WarningLineStartColor : new Color(1f, 0.2f, 0.2f, 0.9f);
            line.endColor = definition != null ? definition.WarningLineEndColor : new Color(1f, 0.2f, 0.2f, 0.15f);
            line.sortingOrder = definition != null ? definition.WarningLineSortingOrder : 30;
        }

        private static Material GetDefaultWarningLineMaterial()
        {
            if (defaultWarningLineMaterial == null)
            {
                defaultWarningLineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return defaultWarningLineMaterial;
        }

        private readonly struct IgnoredCollisionPair
        {
            public readonly Collider2D Self;
            public readonly Collider2D Other;

            public IgnoredCollisionPair(Collider2D self, Collider2D other)
            {
                Self = self;
                Other = other;
            }
        }

        private abstract class BehaviorState : IState
        {
            protected readonly DashAttackEnemyBehavior Behavior;

            protected BehaviorState(DashAttackEnemyBehavior behavior)
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
            public IdleState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition enemyDefinition = Behavior.EnemyDefinition;
                if (Behavior.definition != null
                    && enemyDefinition != null
                    && Behavior.HasTarget
                    && Behavior.DistanceToTarget <= enemyDefinition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.chaseState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class ChaseState : BehaviorState
        {
            public ChaseState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition enemyDefinition = Behavior.EnemyDefinition;
                if (Behavior.definition == null
                    || enemyDefinition == null
                    || !Behavior.HasTarget
                    || Behavior.DistanceToTarget > enemyDefinition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.idleState);
                    return;
                }

                if (Behavior.CanPrepareDash)
                {
                    Behavior.stateMachine.ChangeState(Behavior.prepareState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                if (Behavior.IsTooCloseToDash && Behavior.definition.RetreatWhenTooClose)
                {
                    Behavior.MoveAwayFromTarget();
                }
                else
                {
                    Behavior.MoveTowardTarget();
                }
            }
        }

        private sealed class PrepareState : BehaviorState
        {
            private float timer;

            public PrepareState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.PrepareDuration : 0.6f;
                Behavior.damagedPlayerThisDash = false;
                Behavior.hitWallThisDash = false;
                Behavior.LockDashDirection();
                Behavior.SetWarningLineVisible(true);
                Behavior.Stop();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.DashPrepare);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                Behavior.UpdateWarningLine();

                if (timer <= 0f)
                {
                    Behavior.SetWarningLineVisible(false);
                    Behavior.stateMachine.ChangeState(Behavior.dashState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
                if (Behavior.EnemyDefinition != null)
                {
                    Behavior.Controller.RotateToward(Behavior.lockedDashDirection, Behavior.EnemyDefinition.RotationSpeed);
                }
            }

            public override void Exit()
            {
                Behavior.SetWarningLineVisible(false);
            }
        }

        private sealed class DashState : BehaviorState
        {
            private float timer;

            public DashState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.DashDuration : 0.4f;
                Behavior.lastDashPosition = Behavior.transform.position;
                Behavior.UpdateDashCollisionIgnore();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Dash);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (Behavior.hitWallThisDash || timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.recoveryState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                float speed = Behavior.definition != null ? Behavior.definition.DashSpeed : 8f;
                Behavior.Controller.Body.linearVelocity = Behavior.lockedDashDirection.normalized * speed;
                Behavior.UpdateDashCollisionIgnore();
                Behavior.ScanForDashHit();
            }

            public override void Exit()
            {
                Behavior.RestoreIgnoredDashCollisions();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.DashEnd);
            }
        }

        private sealed class RecoveryState : BehaviorState
        {
            private float timer;

            public RecoveryState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.hitWallThisDash && Behavior.definition != null
                    ? Behavior.definition.WallStunDuration
                    : Behavior.definition != null
                        ? Behavior.definition.RecoveryDuration
                        : 0.5f;

                Behavior.Controller.Body.linearVelocity = Vector2.zero;
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Recovery);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.HasTarget ? Behavior.chaseState : Behavior.idleState);
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

            public HitReactState(DashAttackEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.EnemyDefinition != null ? Behavior.EnemyDefinition.HitStunDuration : 0.12f;
                Behavior.SetWarningLineVisible(false);
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

                Behavior.stateMachine.ChangeState(Behavior.HasTarget ? Behavior.chaseState : Behavior.idleState);
            }
        }
    }
}
