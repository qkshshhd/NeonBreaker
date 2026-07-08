using NeonBreaker.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace NeonBreaker.Enemies
{
    public sealed class StutterEnemyBehavior : EnemyBehaviorBase
    {
        private enum MovementPhase
        {
            Pause,
            Move
        }

        [Header("Contact Attack")]
        [SerializeField] private DashAttackDefinition contactDefinition;
        [SerializeField, Min(0f)] private float contactDamageCooldown = 0.65f;

        [Header("Stutter Movement")]
        [SerializeField] private bool startWithPause = true;
        [SerializeField, Min(0.05f)] private float pauseDurationMin = 0.35f;
        [SerializeField, Min(0.05f)] private float pauseDurationMax = 0.65f;
        [FormerlySerializedAs("directionPickIntervalMin")]
        [FormerlySerializedAs("moveDurationMin")]
        [SerializeField, Min(0.05f)] private float moveDistanceMin = 1.2f;
        [FormerlySerializedAs("directionPickIntervalMax")]
        [FormerlySerializedAs("moveDurationMax")]
        [SerializeField, Min(0.05f)] private float moveDistanceMax = 2.2f;
        [SerializeField, Min(0.05f)] private float moveDuration = 0.42f;
        [SerializeField] private AnimationCurve moveEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 180f)] private float maxAngleOffsetFromPlayer = 70f;
        [SerializeField, Range(0f, 1f)] private float directToPlayerChance = 0.25f;
        [SerializeField] private bool repickDirectionWhenHittingWall = true;

        private readonly Collider2D[] contactHitResults = new Collider2D[12];
        private readonly RaycastHit2D[] contactCastResults = new RaycastHit2D[12];

        private Vector2 moveDirection = Vector2.right;
        private Vector2 lastScanPosition;
        private Vector2 moveStartPosition;
        private Vector2 moveTargetPosition;
        private MovementPhase phase;
        private float phaseTimer;
        private float moveElapsedTime;
        private float targetMoveDistance;
        private float damageCooldownTimer;

        private EnemyDefinition EnemyDefinition => Controller != null ? Controller.Definition : null;
        private bool HasTarget => Controller != null && Controller.HasTarget;
        private float DistanceToTarget => Controller != null ? Controller.DistanceToTarget : float.PositiveInfinity;
        private Vector2 DirectionToTarget => Controller != null ? Controller.DirectionToTarget : Vector2.zero;

        public override void OnSpawned()
        {
            phaseTimer = 0f;
            damageCooldownTimer = 0f;
            lastScanPosition = transform.position;

            if (startWithPause)
            {
                BeginPause();
            }
            else
            {
                BeginMove();
            }
        }

        public override void OnDespawned()
        {
            phaseTimer = 0f;
            damageCooldownTimer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (damageCooldownTimer > 0f)
            {
                damageCooldownTimer -= deltaTime;
            }

            if (!CanMove())
            {
                ResetPathScan();
                Stop();
                BeginPause(false);
                return;
            }

            phaseTimer -= deltaTime;
            if (phaseTimer > 0f)
            {
                if (phase == MovementPhase.Move && moveElapsedTime >= moveDuration)
                {
                    BeginPause();
                }

                return;
            }

            if (phase == MovementPhase.Pause)
            {
                BeginMove();
            }
            else
            {
                BeginPause();
            }
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            if (!CanMove())
            {
                ResetPathScan();
                Stop();
                return;
            }

            if (phase != MovementPhase.Move)
            {
                ResetPathScan();
                Stop();
                return;
            }

            MoveInCurrentDirection();
            ScanMovementPathForPlayer();
        }

        public override void OnDamaged(DamageInfo damage)
        {
            if (damage.Knockback > 0f)
            {
                ResetPathScan();
            }
        }

        public override void OnDeath()
        {
            Stop();
        }

        public override void OnCollisionEnter2D(Collision2D collision)
        {
            if (!repickDirectionWhenHittingWall || contactDefinition == null)
            {
                return;
            }

            Collider2D other = collision.collider;
            if (other != null && ((1 << other.gameObject.layer) & contactDefinition.WallLayers.value) != 0)
            {
                BeginPause();
            }
        }

        public override void OnTriggerEnter2D(Collider2D other)
        {
            if (phase == MovementPhase.Move)
            {
                TryDamagePlayer(other);
            }
        }

        private bool CanMove()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            return contactDefinition != null
                && enemyDefinition != null
                && HasTarget
                && DistanceToTarget <= enemyDefinition.DetectionRange
                && (Controller.KnockbackReceiver == null || !Controller.KnockbackReceiver.IsReceivingKnockback);
        }

        private void BeginPause(bool resetTimer = true)
        {
            bool wasMoving = phase == MovementPhase.Move;
            phase = MovementPhase.Pause;
            if (resetTimer)
            {
                phaseTimer = Random.Range(pauseDurationMin, Mathf.Max(pauseDurationMin, pauseDurationMax));
            }

            ResetPathScan();
            Stop();

            if (wasMoving)
            {
                RaiseAnimationSignal(EnemyAnimationSignal.DashEnd);
            }
        }

        private void BeginMove(bool forceTowardPlayer = false)
        {
            phase = MovementPhase.Move;
            PickNewDirection(forceTowardPlayer);
            moveStartPosition = transform.position;
            targetMoveDistance = Random.Range(moveDistanceMin, Mathf.Max(moveDistanceMin, moveDistanceMax));
            moveTargetPosition = moveStartPosition + moveDirection * targetMoveDistance;
            moveElapsedTime = 0f;
            phaseTimer = moveDuration;
            ResetPathScan();
            RaiseAnimationSignal(EnemyAnimationSignal.Dash);
        }

        private void PickNewDirection(bool forceTowardPlayer = false)
        {
            Vector2 toPlayer = DirectionToTarget;
            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                toPlayer = moveDirection.sqrMagnitude > 0.0001f ? moveDirection : Vector2.right;
            }

            bool goDirectlyToPlayer = forceTowardPlayer || Random.value <= directToPlayerChance;
            float angleOffset = goDirectlyToPlayer ? 0f : Random.Range(-maxAngleOffsetFromPlayer, maxAngleOffsetFromPlayer);
            moveDirection = Rotate(toPlayer.normalized, angleOffset);
        }

        private void MoveInCurrentDirection()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (enemyDefinition == null)
            {
                Stop();
                return;
            }

            moveElapsedTime += Time.fixedDeltaTime;
            float duration = Mathf.Max(0.05f, moveDuration);
            float t = Mathf.Clamp01(moveElapsedTime / duration);
            float easedT = moveEaseCurve != null ? Mathf.Clamp01(moveEaseCurve.Evaluate(t)) : EaseOutQuad(t);
            Vector2 nextPosition = Vector2.LerpUnclamped(moveStartPosition, moveTargetPosition, easedT);

            Controller.Body.MovePosition(nextPosition);
            Controller.RotateToward(moveDirection, enemyDefinition.RotationSpeed);
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private void Stop()
        {
            EnemyDefinition enemyDefinition = EnemyDefinition;
            if (Controller == null || Controller.Body == null)
            {
                return;
            }

            if (enemyDefinition == null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
                return;
            }

            Controller.Stop(enemyDefinition.Deceleration);
        }

        private void ScanMovementPathForPlayer()
        {
            if (contactDefinition == null || damageCooldownTimer > 0f)
            {
                lastScanPosition = transform.position;
                return;
            }

            Vector2 currentPosition = transform.position;
            Vector2 movement = currentPosition - lastScanPosition;
            Vector2 direction = movement.sqrMagnitude > 0.0001f ? movement.normalized : moveDirection.normalized;
            Vector2 center = currentPosition + direction * contactDefinition.ContactCheckForwardOffset;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(contactDefinition.HitLayers);
            filter.useTriggers = true;

            if (movement.sqrMagnitude > 0.0001f)
            {
                int castCount = Physics2D.CircleCast(
                    lastScanPosition,
                    contactDefinition.ContactCheckRadius,
                    direction,
                    filter,
                    contactCastResults,
                    movement.magnitude + contactDefinition.ContactCheckForwardOffset);

                for (int i = 0; i < castCount; i++)
                {
                    Collider2D hit = contactCastResults[i].collider;
                    if (hit == null || IsOwnCollider(hit))
                    {
                        continue;
                    }

                    if (TryDamagePlayer(hit))
                    {
                        lastScanPosition = currentPosition;
                        return;
                    }
                }
            }

            int hitCount = Physics2D.OverlapCircle(center, contactDefinition.ContactCheckRadius, filter, contactHitResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = contactHitResults[i];
                if (hit == null || IsOwnCollider(hit))
                {
                    continue;
                }

                if (TryDamagePlayer(hit))
                {
                    lastScanPosition = currentPosition;
                    return;
                }
            }

            lastScanPosition = currentPosition;
        }

        private bool TryDamagePlayer(Collider2D other)
        {
            if (other == null
                || contactDefinition == null
                || damageCooldownTimer > 0f
                || Controller == null
                || Controller.IsAttackSuppressed)
            {
                return false;
            }

            if (((1 << other.gameObject.layer) & contactDefinition.HitLayers.value) == 0)
            {
                return false;
            }

            IDamageable damageable = FindComponentInParents<IDamageable>(other);
            if (damageable == null || !damageable.CanTakeDamage)
            {
                return false;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitDirection = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : DirectionToTarget;
            DamageInfo damage = new DamageInfo(
                contactDefinition.ContactDamage,
                hitPoint,
                hitDirection,
                contactDefinition.ContactKnockback,
                false,
                gameObject);

            damageable.TakeDamage(damage);

            IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(other);
            knockbackReceiver?.ApplyKnockback(hitDirection, contactDefinition.ContactKnockback, contactDefinition.ContactKnockbackDuration);

            damageCooldownTimer = contactDamageCooldown;
            RaiseAnimationSignal(EnemyAnimationSignal.Attack);
            return true;
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

        private void ResetPathScan()
        {
            lastScanPosition = transform.position;
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
    }
}
