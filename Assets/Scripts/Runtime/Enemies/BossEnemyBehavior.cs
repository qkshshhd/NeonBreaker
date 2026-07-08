using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using NeonBreaker.Shared.StateMachine;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public sealed class BossEnemyBehavior : EnemyBehaviorBase
    {
        [SerializeField] private BossBehaviorDefinition definition;
        [SerializeField] private Transform firePoint;
        [SerializeField] private bool logSetupProblems = true;

        private readonly StateMachine stateMachine = new StateMachine();
        private readonly Collider2D[] dashHitResults = new Collider2D[12];
        private readonly RaycastHit2D[] dashCastResults = new RaycastHit2D[12];
        private readonly List<Vector2> telegraphDirections = new List<Vector2>(24);

        private static Material defaultTelegraphLineMaterial;

        private float patternCooldownTimer;
        private int patternIndex;
        private BossPatternType activeProjectilePattern;
        private int projectilePatternShotIndex;
        private int projectilePatternShotCount;
        private float projectilePatternTimer;
        private float projectilePatternAngleOffset;
        private bool projectilePatternComplete;
        private Vector2 lockedDashDirection;
        private Vector2 lastDashPosition;
        private bool damagedPlayerThisDash;
        private bool hitWallThisDash;
        private bool setupLogged;
        private LineRenderer[] telegraphLines;

        private IdleState idleState;
        private RepositionState repositionState;
        private WindUpState windUpState;
        private FirePatternState firePatternState;
        private DashPrepareState dashPrepareState;
        private DashState dashState;
        private RecoveryState recoveryState;

        private EnemyDefinition EnemyDefinition => Controller != null ? Controller.Definition : null;
        private bool IsPhaseTwo => Controller != null
            && definition != null
            && Controller.Health != null
            && Controller.Health.MaxHealth > 0f
            && Controller.Health.CurrentHealth / Controller.Health.MaxHealth <= definition.PhaseTwoHealthRatio;

        public override void Initialize(EnemyController controller)
        {
            base.Initialize(controller);

            idleState = new IdleState(this);
            repositionState = new RepositionState(this);
            windUpState = new WindUpState(this);
            firePatternState = new FirePatternState(this);
            dashPrepareState = new DashPrepareState(this);
            dashState = new DashState(this);
            recoveryState = new RecoveryState(this);
        }

        public override void OnSpawned()
        {
            patternCooldownTimer = definition != null ? definition.InitialPatternDelay : 1.5f;
            patternIndex = 0;
            setupLogged = false;
            damagedPlayerThisDash = false;
            hitWallThisDash = false;
            ValidateSetup();
            SetTelegraphLinesVisible(false);
            stateMachine.ChangeState(idleState);
        }

        public override void OnDespawned()
        {
            patternCooldownTimer = 0f;
            SetTelegraphLinesVisible(false);
        }

        public override void Tick(float deltaTime)
        {
            if (patternCooldownTimer > 0f)
            {
                patternCooldownTimer -= deltaTime;
            }

            stateMachine.Tick(deltaTime);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            stateMachine.FixedTick(fixedDeltaTime);
        }

        public override void OnDeath()
        {
            if (Controller != null && Controller.Body != null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
            }

            SetTelegraphLinesVisible(false);
        }

        public override void OnCollisionEnter2D(Collision2D collision)
        {
            if (stateMachine.CurrentState != dashState || definition == null)
            {
                return;
            }

            Collider2D other = collision.collider;
            int layer = 1 << other.gameObject.layer;
            if ((layer & definition.WallLayers.value) != 0)
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

        private void MoveForRange()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null || definition == null || !Controller.HasTarget)
            {
                Stop();
                return;
            }

            float distance = Controller.DistanceToTarget;
            if (distance < definition.RetreatRange)
            {
                Controller.Move(-Controller.DirectionToTarget, enemyDefinition.MoveSpeed * definition.RetreatSpeedMultiplier, enemyDefinition.Acceleration);
            }
            else if (distance > definition.PreferredRange)
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

        private bool ShouldAttack()
        {
            return definition != null
                && Controller.HasTarget
                && Controller.DistanceToTarget <= definition.EngageRange
                && patternCooldownTimer <= 0f;
        }

        private bool ShouldDashPattern()
        {
            return GetCurrentPatternType() == BossPatternType.Dash;
        }

        private BossPatternType GetCurrentPatternType()
        {
            if (definition == null)
            {
                return BossPatternType.CombinedShot;
            }

            BossPatternType[] sequence = definition.PatternSequence;
            if (sequence != null && sequence.Length > 0)
            {
                return sequence[patternIndex % sequence.Length];
            }

            if (definition.DashEveryNthPattern > 0
                && patternIndex > 0
                && patternIndex % definition.DashEveryNthPattern == 0)
            {
                return BossPatternType.Dash;
            }

            return BossPatternType.CombinedShot;
        }

        private void StartPatternCooldown()
        {
            float cooldown = definition != null ? definition.PatternCooldown : 1f;
            if (definition != null && IsPhaseTwo)
            {
                cooldown *= definition.PhaseTwoCooldownMultiplier;
            }

            patternCooldownTimer = cooldown;
        }

        private void BeginProjectilePattern()
        {
            if (definition == null || ObjectPoolManager.Instance == null || definition.ProjectilePoolKey == null)
            {
                LogSetupProblem("[BossEnemyBehavior] Cannot fire. Check ObjectPoolManager, Projectile PoolKey, and BossBehaviorDefinition.");
                projectilePatternComplete = true;
                return;
            }

            activeProjectilePattern = GetCurrentPatternType();
            projectilePatternShotIndex = 0;
            projectilePatternShotCount = GetProjectilePatternShotCount(activeProjectilePattern);
            projectilePatternTimer = 0f;
            projectilePatternAngleOffset = patternIndex * definition.RadialAngleOffsetPerPattern;
            projectilePatternComplete = projectilePatternShotCount <= 0;
        }

        private void TickProjectilePattern(float deltaTime)
        {
            if (projectilePatternComplete)
            {
                return;
            }

            projectilePatternTimer -= deltaTime;
            while (!projectilePatternComplete && projectilePatternTimer <= 0f)
            {
                FireProjectilePatternShot(activeProjectilePattern, projectilePatternShotIndex);
                projectilePatternShotIndex++;

                if (projectilePatternShotIndex >= projectilePatternShotCount)
                {
                    projectilePatternComplete = true;
                    return;
                }

                float interval = GetProjectilePatternShotInterval(activeProjectilePattern);
                if (interval <= 0f)
                {
                    continue;
                }

                projectilePatternTimer += interval;
            }
        }

        private int GetProjectilePatternShotCount(BossPatternType patternType)
        {
            if (definition == null)
            {
                return 0;
            }

            return patternType switch
            {
                BossPatternType.AimedBurst => definition.AimedBurstShotCount + (IsPhaseTwo ? definition.PhaseTwoExtraBurstShots : 0),
                BossPatternType.Spiral => definition.SpiralShotCount + (IsPhaseTwo ? definition.PhaseTwoExtraSpiralShots : 0),
                BossPatternType.Dash => 0,
                _ => 1
            };
        }

        private float GetProjectilePatternShotInterval(BossPatternType patternType)
        {
            if (definition == null)
            {
                return 0f;
            }

            return patternType switch
            {
                BossPatternType.AimedBurst => definition.AimedBurstShotInterval,
                BossPatternType.Spiral => definition.SpiralShotInterval,
                _ => 0f
            };
        }

        private void FireProjectilePatternShot(BossPatternType patternType, int shotIndex)
        {
            if (definition == null)
            {
                return;
            }

            int aimedCount = definition.AimedProjectileCount + (IsPhaseTwo ? definition.PhaseTwoExtraAimedProjectiles : 0);
            int radialCount = definition.RadialProjectileCount + (IsPhaseTwo ? definition.PhaseTwoExtraRadialProjectiles : 0);

            switch (patternType)
            {
                case BossPatternType.AimedSpread:
                    FireAimedSpread(aimedCount, definition.AimedSpreadAngle);
                    break;
                case BossPatternType.RadialBurst:
                    FireRadial(radialCount, projectilePatternAngleOffset);
                    break;
                case BossPatternType.Spiral:
                    FireSpiralShot(shotIndex);
                    break;
                case BossPatternType.AimedBurst:
                    FireAimedSpread(aimedCount, definition.AimedBurstSpreadAngle);
                    break;
                case BossPatternType.CombinedShot:
                default:
                    FireAimedSpread(aimedCount, definition.AimedSpreadAngle);
                    FireRadial(radialCount, projectilePatternAngleOffset);
                    break;
            }
        }

        private void FireAimedSpread(int count, float spreadAngle)
        {
            if (count <= 0 || !Controller.HasTarget)
            {
                return;
            }

            float startAngle = count <= 1 ? 0f : -spreadAngle * 0.5f;
            float step = count <= 1 ? 0f : spreadAngle / (count - 1);
            for (int i = 0; i < count; i++)
            {
                SpawnProjectile(Rotate(Controller.DirectionToTarget, startAngle + step * i));
            }
        }

        private void FireRadial(int count, float angleOffset)
        {
            if (count <= 0)
            {
                return;
            }

            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + step * i;
                SpawnProjectile(new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)));
            }
        }

        private void FireSpiralShot(int shotIndex)
        {
            if (definition == null)
            {
                return;
            }

            int armCount = definition.SpiralArmCount;
            float armStep = 360f / armCount;
            float baseAngle = projectilePatternAngleOffset + definition.SpiralAngleStep * shotIndex;
            for (int i = 0; i < armCount; i++)
            {
                float angle = baseAngle + armStep * i;
                SpawnProjectile(new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)));
            }
        }

        private void SpawnProjectile(Vector2 direction)
        {
            Vector3 position = GetFirePosition(direction);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            EnemyProjectile2D projectile = ObjectPoolManager.Instance.Spawn<EnemyProjectile2D>(
                definition.ProjectilePoolKey,
                position,
                Quaternion.Euler(0f, 0f, angle));

            if (projectile == null)
            {
                LogSetupProblem($"[BossEnemyBehavior] Projectile pool '{definition.ProjectilePoolKey.name}' did not spawn an EnemyProjectile2D.");
                return;
            }

            projectile.Launch(definition.ProjectileDefinition, direction, gameObject);
        }

        private void UpdatePatternTelegraph()
        {
            if (definition == null || !definition.ShowPatternTelegraph || !Controller.HasTarget)
            {
                SetTelegraphLinesVisible(false);
                return;
            }

            telegraphDirections.Clear();
            CollectPatternTelegraphDirections(GetCurrentPatternType(), telegraphDirections);
            int count = telegraphDirections.Count;
            if (count <= 0)
            {
                SetTelegraphLinesVisible(false);
                return;
            }

            EnsureTelegraphLineCount(count);
            for (int i = 0; i < telegraphLines.Length; i++)
            {
                LineRenderer line = telegraphLines[i];
                if (line == null)
                {
                    continue;
                }

                bool active = i < count;
                line.enabled = active;
                if (!active)
                {
                    continue;
                }

                ApplyTelegraphLineStyle(line);
                Vector2 direction = telegraphDirections[i].sqrMagnitude > 0.0001f
                    ? telegraphDirections[i].normalized
                    : Vector2.right;
                Vector3 start = GetFirePosition(direction);
                line.SetPosition(0, start);
                line.SetPosition(1, start + (Vector3)(direction * definition.TelegraphLineLength));
            }
        }

        private void CollectPatternTelegraphDirections(BossPatternType patternType, List<Vector2> results)
        {
            if (definition == null)
            {
                return;
            }

            int aimedCount = definition.AimedProjectileCount + (IsPhaseTwo ? definition.PhaseTwoExtraAimedProjectiles : 0);
            int radialCount = definition.RadialProjectileCount + (IsPhaseTwo ? definition.PhaseTwoExtraRadialProjectiles : 0);
            float angleOffset = patternIndex * definition.RadialAngleOffsetPerPattern;

            switch (patternType)
            {
                case BossPatternType.AimedSpread:
                    AddAimedTelegraphDirections(results, aimedCount, definition.AimedSpreadAngle);
                    break;
                case BossPatternType.AimedBurst:
                    AddAimedTelegraphDirections(results, aimedCount, definition.AimedBurstSpreadAngle);
                    break;
                case BossPatternType.RadialBurst:
                    AddRadialTelegraphDirections(results, radialCount, angleOffset);
                    break;
                case BossPatternType.Spiral:
                    AddRadialTelegraphDirections(results, definition.SpiralArmCount, angleOffset);
                    break;
                case BossPatternType.Dash:
                    results.Add(GetDashTelegraphDirection());
                    break;
                case BossPatternType.CombinedShot:
                default:
                    AddAimedTelegraphDirections(results, aimedCount, definition.AimedSpreadAngle);
                    AddRadialTelegraphDirections(results, radialCount, angleOffset);
                    break;
            }
        }

        private void AddAimedTelegraphDirections(List<Vector2> results, int count, float spreadAngle)
        {
            if (count <= 0 || !Controller.HasTarget)
            {
                return;
            }

            float startAngle = count <= 1 ? 0f : -spreadAngle * 0.5f;
            float step = count <= 1 ? 0f : spreadAngle / (count - 1);
            for (int i = 0; i < count; i++)
            {
                results.Add(Rotate(Controller.DirectionToTarget, startAngle + step * i));
            }
        }

        private static void AddRadialTelegraphDirections(List<Vector2> results, int count, float angleOffset)
        {
            if (count <= 0)
            {
                return;
            }

            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + step * i;
                results.Add(new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)));
            }
        }

        private Vector2 GetDashTelegraphDirection()
        {
            if (lockedDashDirection.sqrMagnitude > 0.0001f)
            {
                return lockedDashDirection.normalized;
            }

            if (Controller != null && Controller.DirectionToTarget.sqrMagnitude > 0.0001f)
            {
                return Controller.DirectionToTarget.normalized;
            }

            return transform.right;
        }

        private void SetTelegraphLinesVisible(bool visible)
        {
            if (telegraphLines == null)
            {
                return;
            }

            for (int i = 0; i < telegraphLines.Length; i++)
            {
                if (telegraphLines[i] != null)
                {
                    telegraphLines[i].enabled = visible;
                }
            }
        }

        private void EnsureTelegraphLineCount(int count)
        {
            count = Mathf.Max(1, count);
            if (telegraphLines != null && telegraphLines.Length >= count)
            {
                return;
            }

            int oldCount = telegraphLines != null ? telegraphLines.Length : 0;
            LineRenderer[] newLines = new LineRenderer[count];
            for (int i = 0; i < oldCount; i++)
            {
                newLines[i] = telegraphLines[i];
            }

            for (int i = oldCount; i < count; i++)
            {
                newLines[i] = CreateTelegraphLine(i);
            }

            telegraphLines = newLines;
        }

        private LineRenderer CreateTelegraphLine(int index)
        {
            GameObject lineObject = new GameObject($"Boss Pattern Telegraph Line {index + 1}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 0;
            ApplyTelegraphLineStyle(line);
            line.enabled = false;
            return line;
        }

        private void ApplyTelegraphLineStyle(LineRenderer line)
        {
            if (line == null)
            {
                return;
            }

            line.sharedMaterial = definition != null && definition.TelegraphLineMaterial != null
                ? definition.TelegraphLineMaterial
                : GetDefaultTelegraphLineMaterial();
            line.startWidth = definition != null ? definition.TelegraphLineWidth : 0.07f;
            line.endWidth = 0f;
            line.startColor = definition != null ? definition.TelegraphLineStartColor : new Color(1f, 0.08f, 0.24f, 0.98f);
            line.endColor = definition != null ? definition.TelegraphLineEndColor : new Color(1f, 0.08f, 0.24f, 0.08f);
            line.sortingOrder = definition != null ? definition.TelegraphLineSortingOrder : 36;
        }

        private static Material GetDefaultTelegraphLineMaterial()
        {
            if (defaultTelegraphLineMaterial == null)
            {
                defaultTelegraphLineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return defaultTelegraphLineMaterial;
        }

        private Vector3 GetFirePosition(Vector2 direction)
        {
            if (firePoint != null)
            {
                return firePoint.position;
            }

            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector2)transform.right;
            return transform.position + (Vector3)(safeDirection * (definition != null ? definition.MuzzleForwardOffset : 0.65f));
        }

        private void LockDashDirection()
        {
            lockedDashDirection = Controller.DirectionToTarget.sqrMagnitude > 0.0001f
                ? Controller.DirectionToTarget
                : (Vector2)transform.right;
            lastDashPosition = transform.position;
            damagedPlayerThisDash = false;
            hitWallThisDash = false;
        }

        private void ScanForDashHit()
        {
            if (definition == null || damagedPlayerThisDash)
            {
                return;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(definition.HitLayers);
            filter.useTriggers = true;

            Vector2 currentPosition = transform.position;
            Vector2 movement = currentPosition - lastDashPosition;
            if (movement.sqrMagnitude > 0.0001f)
            {
                int castCount = Physics2D.CircleCast(
                    lastDashPosition,
                    definition.ContactCheckRadius,
                    movement.normalized,
                    filter,
                    dashCastResults,
                    movement.magnitude);

                for (int i = 0; i < castCount; i++)
                {
                    Collider2D hit = dashCastResults[i].collider;
                    if (hit != null && !IsOwnCollider(hit))
                    {
                        TryDamagePlayer(hit);
                        if (damagedPlayerThisDash)
                        {
                            lastDashPosition = currentPosition;
                            return;
                        }
                    }
                }
            }

            int hitCount = Physics2D.OverlapCircle(currentPosition, definition.ContactCheckRadius, filter, dashHitResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = dashHitResults[i];
                if (hit != null && !IsOwnCollider(hit))
                {
                    TryDamagePlayer(hit);
                    if (damagedPlayerThisDash)
                    {
                        break;
                    }
                }
            }

            lastDashPosition = currentPosition;
        }

        private void TryDamagePlayer(Collider2D other)
        {
            if (definition == null || damagedPlayerThisDash)
            {
                return;
            }

            int layer = 1 << other.gameObject.layer;
            if ((layer & definition.HitLayers.value) == 0)
            {
                return;
            }

            IDamageable damageable = FindComponentInParents<IDamageable>(other);
            if (damageable == null || !damageable.CanTakeDamage)
            {
                return;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            DamageInfo damage = new DamageInfo(
                definition.ContactDamage,
                hitPoint,
                lockedDashDirection,
                definition.ContactKnockback,
                false,
                gameObject);

            damageable.TakeDamage(damage);

            IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(other);
            knockbackReceiver?.ApplyKnockback(lockedDashDirection, definition.ContactKnockback, definition.ContactKnockbackDuration);

            damagedPlayerThisDash = true;
        }

        private bool IsOwnCollider(Collider2D collider)
        {
            Collider2D[] ownColliders = Controller != null ? Controller.Colliders : null;
            if (ownColliders == null)
            {
                return false;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == collider)
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidateSetup()
        {
            if (!logSetupProblems)
            {
                return;
            }

            if (EnemyDefinition == null)
            {
                LogSetupProblem("[BossEnemyBehavior] EnemyController.Definition is missing.");
            }

            if (definition == null)
            {
                LogSetupProblem("[BossEnemyBehavior] BossBehaviorDefinition is missing.");
                return;
            }

            if (definition.ProjectilePoolKey == null)
            {
                LogSetupProblem("[BossEnemyBehavior] Projectile PoolKey is missing.");
            }

            if (definition.ProjectileDefinition == null)
            {
                LogSetupProblem("[BossEnemyBehavior] ProjectileDefinition is missing.");
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

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            Vector2 normalized = vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector2.right;
            if (Mathf.Abs(degrees) <= 0.0001f)
            {
                return normalized;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos).normalized;
        }

        private void ChangeToRepositionOrIdle()
        {
            if (definition == null || !Controller.HasTarget || Controller.DistanceToTarget > definition.EngageRange)
            {
                stateMachine.ChangeState(idleState);
            }
            else
            {
                stateMachine.ChangeState(repositionState);
            }
        }

        private abstract class BehaviorState : IState
        {
            protected readonly BossEnemyBehavior Behavior;

            protected BehaviorState(BossEnemyBehavior behavior)
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
            public IdleState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                if (Behavior.definition != null
                    && Behavior.Controller.HasTarget
                    && Behavior.Controller.DistanceToTarget <= Behavior.definition.EngageRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.repositionState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class RepositionState : BehaviorState
        {
            public RepositionState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                if (Behavior.definition == null || !Behavior.Controller.HasTarget)
                {
                    Behavior.stateMachine.ChangeState(Behavior.idleState);
                    return;
                }

                if (Behavior.ShouldAttack())
                {
                    Behavior.stateMachine.ChangeState(Behavior.windUpState);
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

            public WindUpState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.WindUpTime : 0.35f;
                Behavior.Stop();
                Behavior.UpdatePatternTelegraph();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.WindUp);
            }

            public override void Tick(float deltaTime)
            {
                Behavior.UpdatePatternTelegraph();
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.ShouldDashPattern() ? Behavior.dashPrepareState : Behavior.firePatternState);
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

            public override void Exit()
            {
                Behavior.SetTelegraphLinesVisible(false);
            }
        }

        private sealed class FirePatternState : BehaviorState
        {
            public FirePatternState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Shoot);
                Behavior.SetTelegraphLinesVisible(false);
                Behavior.BeginProjectilePattern();
            }

            public override void Tick(float deltaTime)
            {
                Behavior.TickProjectilePattern(deltaTime);
                if (!Behavior.projectilePatternComplete)
                {
                    return;
                }

                Behavior.patternIndex++;
                Behavior.StartPatternCooldown();
                Behavior.stateMachine.ChangeState(Behavior.recoveryState);
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

        private sealed class DashPrepareState : BehaviorState
        {
            private float timer;

            public DashPrepareState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.DashPrepareTime : 0.2f;
                Behavior.LockDashDirection();
                Behavior.Stop();
                Behavior.UpdatePatternTelegraph();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.DashPrepare);
            }

            public override void Tick(float deltaTime)
            {
                Behavior.UpdatePatternTelegraph();
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.dashState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }

            public override void Exit()
            {
                Behavior.SetTelegraphLinesVisible(false);
            }
        }

        private sealed class DashState : BehaviorState
        {
            private float timer;

            public DashState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.DashDuration : 0.45f;
                Behavior.lastDashPosition = Behavior.transform.position;
                Behavior.SetTelegraphLinesVisible(false);
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Dash);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer <= 0f || Behavior.hitWallThisDash)
                {
                    Behavior.patternIndex++;
                    Behavior.StartPatternCooldown();
                    Behavior.stateMachine.ChangeState(Behavior.recoveryState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                float speed = Behavior.definition != null ? Behavior.definition.DashSpeed : 8f;
                if (Behavior.definition != null && Behavior.IsPhaseTwo)
                {
                    speed *= Behavior.definition.PhaseTwoDashSpeedMultiplier;
                }

                Behavior.Controller.Body.linearVelocity = Behavior.lockedDashDirection.normalized * speed;
                Behavior.ScanForDashHit();
            }
        }

        private sealed class RecoveryState : BehaviorState
        {
            private float timer;

            public RecoveryState(BossEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.definition != null ? Behavior.definition.RecoveryTime : 0.35f;
                Behavior.Stop();
                Behavior.SetTelegraphLinesVisible(false);
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Recovery);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.ChangeToRepositionOrIdle();
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }
    }
}
