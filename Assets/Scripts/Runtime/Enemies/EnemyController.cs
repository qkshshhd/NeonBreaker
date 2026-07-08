using System;
using System.Collections;
using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using NeonBreaker.Rooms;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(KnockbackReceiver2D))]
    [RequireComponent(typeof(PoolableGameObject))]
    public class EnemyController : MonoBehaviour, IPoolLifecycle, IRoomEnemy
    {
        private static readonly HashSet<EnemyController> ActiveEnemySet = new HashSet<EnemyController>();

        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private EnemyBehaviorBase behaviorOverride;
        [SerializeField] private string fallbackTargetTag = "Player";
        [SerializeField] private float deathDespawnDelay = 0.2f;
        [SerializeField] private bool disableCollidersOnDeath = true;
        [SerializeField] private bool addDefaultDamageFeedback = true;
        [SerializeField] private bool rotateRootTowardDirection;
        [SerializeField] private DamageNumberStyleDefinition damageNumberStyle;

        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver2D knockbackReceiver;
        private PoolableGameObject poolableObject;
        private Collider2D[] colliders;
        private IEnemyBehavior behavior;
        private Transform target;
        private bool isDead;
        private bool behaviorSuppressed;
        private bool attackSuppressed;
        private Coroutine despawnRoutine;

        public EnemyDefinition Definition => definition;
        public Rigidbody2D Body => body;
        public Health Health => health;
        public KnockbackReceiver2D KnockbackReceiver => knockbackReceiver;
        public Collider2D[] Colliders => colliders;
        public Transform Target => target;
        public bool HasTarget => target != null;
        public bool IsDead => isDead;
        public bool IsBehaviorSuppressed => behaviorSuppressed;
        public bool IsAttackSuppressed => attackSuppressed;
        public static IReadOnlyCollection<EnemyController> ActiveEnemies => ActiveEnemySet;
        public float DistanceToTarget => target == null ? float.PositiveInfinity : ((Vector2)target.position - (Vector2)transform.position).magnitude;
        public Vector2 DirectionToTarget => target == null ? Vector2.zero : ((Vector2)target.position - (Vector2)transform.position).normalized;

        public event Action<EnemyController> Died;
        public event Action<EnemyAnimationSignal> AnimationSignalRaised;
        private event Action<IRoomEnemy> RoomEnemyDied;

        event Action<IRoomEnemy> IRoomEnemy.Died
        {
            add => RoomEnemyDied += value;
            remove => RoomEnemyDied -= value;
        }

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockbackReceiver = GetComponent<KnockbackReceiver2D>();
            poolableObject = GetComponent<PoolableGameObject>();
            colliders = GetComponentsInChildren<Collider2D>(true);

            behavior = FindBehavior();
            behavior?.Initialize(this);

            EnsureDefaultDamageFeedback();

            ApplyDefinitionHealth();
        }

        protected virtual void OnEnable()
        {
            ActiveEnemySet.Add(this);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        protected virtual void OnDisable()
        {
            ActiveEnemySet.Remove(this);
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void Start()
        {
            AcquireTarget();
            RaiseAnimationSignal(EnemyAnimationSignal.Spawn);
            behavior?.OnSpawned();
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            if (!HasTarget)
            {
                AcquireTarget();
            }

            if (!behaviorSuppressed)
            {
                behavior?.Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (isDead)
            {
                return;
            }

            if (!behaviorSuppressed)
            {
                behavior?.FixedTick(Time.fixedDeltaTime);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isDead && !behaviorSuppressed)
            {
                behavior?.OnCollisionEnter2D(collision);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isDead && !behaviorSuppressed)
            {
                behavior?.OnTriggerEnter2D(other);
            }
        }

        public void OnSpawned()
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }

            isDead = false;
            behaviorSuppressed = false;
            attackSuppressed = false;
            body.linearVelocity = Vector2.zero;

            SetCollidersEnabled(true);
            ApplyDefinitionHealth();
            AcquireTarget();
            RaiseAnimationSignal(EnemyAnimationSignal.Spawn);
            behavior?.OnSpawned();
        }

        public void OnDespawned()
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }

            body.linearVelocity = Vector2.zero;
            target = null;
            isDead = false;
            Died = null;
            AnimationSignalRaised = null;
            RoomEnemyDied = null;
            behavior?.OnDespawned();
        }

        public void SetBehaviorSuppressed(bool suppressed, bool stopImmediately = true, bool suppressAttacks = true)
        {
            behaviorSuppressed = suppressed;
            attackSuppressed = suppressed && suppressAttacks;
            if (suppressed && stopImmediately && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void SetAttackSuppressed(bool suppressed)
        {
            attackSuppressed = suppressed;
        }

        public void AcquireTarget()
        {
            string targetTag = definition != null && !string.IsNullOrWhiteSpace(definition.TargetTag)
                ? definition.TargetTag
                : fallbackTargetTag;

            if (string.IsNullOrWhiteSpace(targetTag))
            {
                return;
            }

            GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
            target = targetObject != null ? targetObject.transform : null;
        }

        public void MoveTowardTarget(float speed, float acceleration)
        {
            if (!HasTarget)
            {
                Stop(acceleration);
                return;
            }

            Vector2 targetVelocity = DirectionToTarget * speed;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }

        public void Move(Vector2 direction, float speed, float acceleration)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Stop(acceleration);
                return;
            }

            Vector2 targetVelocity = direction.normalized * speed;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }

        public void Stop(float deceleration)
        {
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
        }

        public void RotateToward(Vector2 direction, float rotationSpeed)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (!rotateRootTowardDirection)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        public void RaiseAnimationSignal(EnemyAnimationSignal signal)
        {
            AnimationSignalRaised?.Invoke(signal);
        }

        public void InitializeHealth(float maxHealth, float invulnerabilityDuration)
        {
            health.Initialize(maxHealth, invulnerabilityDuration);
        }

        public void ApplyDifficulty(DifficultyContext context)
        {
            if (definition == null)
            {
                return;
            }

            float healthMultiplier = Mathf.Max(1f, context.EnemyHealthMultiplier);
            InitializeHealth(definition.MaxHealth * healthMultiplier, definition.HitInvulnerabilityDuration);
        }

        public void DespawnToPool()
        {
            poolableObject.ReturnToPool();
        }

        private void ApplyDefinitionHealth()
        {
            if (definition != null)
            {
                InitializeHealth(definition.MaxHealth, definition.HitInvulnerabilityDuration);
            }
        }

        private void EnsureDefaultDamageFeedback()
        {
            if (!addDefaultDamageFeedback)
            {
                return;
            }

            if (GetComponent<DamageFlash2D>() == null)
            {
                gameObject.AddComponent<DamageFlash2D>();
            }

            DamageNumberSpawner2D numberSpawner = GetComponent<DamageNumberSpawner2D>();
            if (numberSpawner == null)
            {
                numberSpawner = gameObject.AddComponent<DamageNumberSpawner2D>();
            }

            if (damageNumberStyle != null)
            {
                numberSpawner.ConfigureStyle(damageNumberStyle);
            }
        }

        private IEnemyBehavior FindBehavior()
        {
            if (behaviorOverride != null)
            {
                return behaviorOverride;
            }

            IEnemyBehavior foundBehavior = null;
            int behaviorCount = 0;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (ReferenceEquals(behaviours[i], this))
                {
                    continue;
                }

                if (behaviours[i] is IEnemyBehavior enemyBehavior)
                {
                    foundBehavior ??= enemyBehavior;
                    behaviorCount++;
                }
            }

            if (behaviorCount > 1)
            {
                Debug.LogWarning("[EnemyController] Multiple enemy behaviors found. Assign Behavior Override on EnemyController to avoid using the wrong behavior.", this);
            }

            if (foundBehavior != null)
            {
                return foundBehavior;
            }

            IEnemyBehavior fallbackBehavior = CreateFallbackBehavior();
            if (fallbackBehavior != null)
            {
                return fallbackBehavior;
            }

            Debug.LogError("[EnemyController] Enemy has no behavior component. Add ChaserMeleeEnemyBehavior, DashAttackEnemyBehavior, or another IEnemyBehavior.", this);
            return null;
        }

        protected virtual IEnemyBehavior CreateFallbackBehavior()
        {
            return null;
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (!isDead)
            {
                behavior?.OnDamaged(damage);
            }
        }

        private void HandleDied()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            body.linearVelocity = Vector2.zero;
            RaiseAnimationSignal(EnemyAnimationSignal.Death);
            behavior?.OnDeath();
            Died?.Invoke(this);
            RoomEnemyDied?.Invoke(this);
            BeginDeathDespawn();
        }

        private void BeginDeathDespawn()
        {
            if (despawnRoutine != null)
            {
                return;
            }

            despawnRoutine = StartCoroutine(DeathDespawnRoutine());
        }

        private IEnumerator DeathDespawnRoutine()
        {
            if (disableCollidersOnDeath)
            {
                SetCollidersEnabled(false);
            }

            if (deathDespawnDelay > 0f)
            {
                yield return new WaitForSeconds(deathDespawnDelay);
            }

            despawnRoutine = null;
            DespawnToPool();
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = isEnabled;
                }
            }
        }
    }
}
