using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [RequireComponent(typeof(PoolableGameObject))]
    public sealed class EnemyProjectile2D : MonoBehaviour, IPoolLifecycle
    {
        [SerializeField] private EnemyProjectileDefinition defaultDefinition;
        [SerializeField] private bool logSetupProblems = true;

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];

        private PoolableGameObject poolableObject;
        private EnemyProjectileDefinition activeDefinition;
        private Vector2 direction;
        private Vector2 previousPosition;
        private GameObject source;
        private float lifetimeTimer;
        private bool isActive;
        private bool setupLogged;

        private void Awake()
        {
            poolableObject = GetComponent<PoolableGameObject>();
        }

        private void Update()
        {
            if (!isActive || activeDefinition == null)
            {
                return;
            }

            lifetimeTimer -= Time.deltaTime;
            if (lifetimeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }

            Vector2 currentPosition = transform.position;
            Vector2 nextPosition = currentPosition + direction * activeDefinition.Speed * Time.deltaTime;

            ScanMovement(currentPosition, nextPosition);
            if (!isActive)
            {
                return;
            }

            transform.position = nextPosition;
            previousPosition = nextPosition;
        }

        public void Launch(EnemyProjectileDefinition definition, Vector2 launchDirection, GameObject owner)
        {
            activeDefinition = definition != null ? definition : defaultDefinition;
            direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.right;
            source = owner;
            lifetimeTimer = activeDefinition != null ? activeDefinition.Lifetime : 1f;
            previousPosition = transform.position;
            isActive = activeDefinition != null;

            if (activeDefinition != null && activeDefinition.RotateToDirection)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (activeDefinition == null)
            {
                LogSetupProblem("[EnemyProjectile2D] Missing projectile definition. Assign RangedAttackDefinition.ProjectileDefinition or EnemyProjectile2D.DefaultDefinition.");
                ReturnToPool();
            }
        }

        public void OnSpawned()
        {
            activeDefinition = defaultDefinition;
            direction = transform.right;
            previousPosition = transform.position;
            source = null;
            lifetimeTimer = activeDefinition != null ? activeDefinition.Lifetime : 1f;
            isActive = false;
            setupLogged = false;
        }

        public void OnDespawned()
        {
            isActive = false;
            source = null;
            activeDefinition = null;
        }

        private void ScanMovement(Vector2 from, Vector2 to)
        {
            Vector2 movement = to - from;
            if (movement.sqrMagnitude > 0.0001f)
            {
                ContactFilter2D castFilter = new ContactFilter2D();
                castFilter.SetLayerMask(activeDefinition.HitLayers | activeDefinition.BlockerLayers);
                castFilter.useTriggers = true;

                int castCount = Physics2D.CircleCast(
                    from,
                    activeDefinition.HitRadius,
                    movement.normalized,
                    castFilter,
                    castResults,
                    movement.magnitude);

                for (int i = 0; i < castCount; i++)
                {
                    Collider2D hit = castResults[i].collider;
                    if (hit != null && HandleHit(hit, castResults[i].point))
                    {
                        return;
                    }
                }
            }

            ContactFilter2D overlapFilter = new ContactFilter2D();
            overlapFilter.SetLayerMask(activeDefinition.HitLayers | activeDefinition.BlockerLayers);
            overlapFilter.useTriggers = true;

            int overlapCount = Physics2D.OverlapCircle(to, activeDefinition.HitRadius, overlapFilter, overlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit != null && HandleHit(hit, hit.ClosestPoint(to)))
                {
                    return;
                }
            }
        }

        private bool HandleHit(Collider2D hit, Vector2 point)
        {
            if (source != null && hit.transform.IsChildOf(source.transform))
            {
                return false;
            }

            int hitLayer = 1 << hit.gameObject.layer;
            if ((hitLayer & activeDefinition.HitLayers.value) != 0)
            {
                IDamageable damageable = FindComponentInParents<IDamageable>(hit);
                if (damageable != null && damageable.CanTakeDamage)
                {
                    DamageInfo damage = new DamageInfo(
                        activeDefinition.Damage,
                        point,
                        direction,
                        activeDefinition.Knockback,
                        false,
                        source != null ? source : gameObject);

                    damageable.TakeDamage(damage);

                    IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                    knockbackReceiver?.ApplyKnockback(direction, activeDefinition.Knockback, activeDefinition.KnockbackDuration);

                    if (activeDefinition.DespawnOnHit)
                    {
                        ReturnToPool();
                        return true;
                    }
                }

                return false;
            }

            if (activeDefinition.DespawnOnBlocker && (hitLayer & activeDefinition.BlockerLayers.value) != 0)
            {
                ReturnToPool();
                return true;
            }

            return false;
        }

        private void ReturnToPool()
        {
            isActive = false;
            poolableObject.ReturnToPool();
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

        private static T FindComponentInParents<T>(Collider2D sourceCollider) where T : class
        {
            MonoBehaviour[] behaviours = sourceCollider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T component)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
