using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyPolishFeedback2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private Health health;

        [Header("SFX")]
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip criticalHitClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 0.25f)] private float pitchVariance = 0.05f;

        [Header("VFX")]
        [SerializeField] private PoolKey hitVfxPoolKey;
        [SerializeField] private GameObject hitVfx;
        [SerializeField] private bool playHitVfxOnDamage;
        [SerializeField] private PoolKey basicAttackSlashVfxPoolKey;
        [SerializeField] private GameObject basicAttackSlashVfx;
        [SerializeField] private PoolKey criticalBasicAttackSlashVfxPoolKey;
        [SerializeField] private GameObject criticalBasicAttackSlashVfx;
        [SerializeField] private PoolKey deathVfxPoolKey;
        [SerializeField] private GameObject deathVfx;
        [SerializeField, Min(0.01f)] private float fallbackVfxLifetime = 1.2f;

        [Header("Impact Direction")]
        [SerializeField] private bool offsetImpactVfxPosition;
        [SerializeField, Min(0f)] private float impactVfxDirectionOffset = 0.18f;
        [SerializeField] private bool rotateImpactVfxToDamageDirection = true;
        [SerializeField] private bool invertImpactVfxDirection;
        [SerializeField] private bool randomizeDamageVfxRotation = true;
        [SerializeField, Range(0f, 180f)] private float damageVfxRandomRotationRange = 180f;
        [SerializeField] private bool randomizeDeathVfxRotation;
        [SerializeField, Range(0f, 180f)] private float deathVfxRandomRotationRange = 180f;
        [SerializeField] private bool rotateParticleShapeToImpactDirection = true;
        [SerializeField] private Vector3 particleShapeRotationOffset = new Vector3(0f, 90f, 0f);
        [SerializeField] private bool restartParticlesAfterShapeRotation = true;
        [SerializeField] private bool drawImpactVfxDebug;
        [SerializeField, Min(0.01f)] private float debugLineDuration = 0.25f;

        private SpriteRenderer[] spriteRenderers;

        private void Awake()
        {
            if (enemy == null)
            {
                enemy = GetComponent<EnemyController>();
            }

            if (health == null && enemy != null)
            {
                health = enemy.Health;
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }

            if (enemy != null)
            {
                enemy.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            if (enemy != null)
            {
                enemy.Died -= HandleDied;
            }
        }

        private void HandleDamaged(DamageInfo damage)
        {
            AudioClip clip = damage.IsCritical && criticalHitClip != null ? criticalHitClip : hitClip;
            GameObject hitImpactVfx = playHitVfxOnDamage ? hitVfx : null;
            PoolKey hitImpactPoolKey = playHitVfxOnDamage ? hitVfxPoolKey : null;
            GameObject slashVfx = ResolveBasicAttackSlashVfx(damage);
            PoolKey slashPoolKey = ResolveBasicAttackSlashVfxPoolKey(damage);
            Vector3 center = GetFeedbackCenter();
            GetImpactVfxTransform(damage, center, out Vector3 position, out Quaternion rotation);
            Quaternion damageRotation = GetRandomizedRotation(rotation, randomizeDamageVfxRotation, damageVfxRandomRotationRange);

            Play(clip, center);
            Spawn(hitImpactVfx, hitImpactPoolKey, position, damageRotation, true);
            Spawn(slashVfx, slashPoolKey, center, damageRotation, true);
            DrawImpactDebug(center, position);
        }

        private GameObject ResolveBasicAttackSlashVfx(DamageInfo damage)
        {
            if (damage.SourceType != DamageSourceType.BasicAttack)
            {
                return null;
            }

            return damage.IsCritical && criticalBasicAttackSlashVfx != null
                ? criticalBasicAttackSlashVfx
                : basicAttackSlashVfx;
        }

        private PoolKey ResolveBasicAttackSlashVfxPoolKey(DamageInfo damage)
        {
            if (damage.SourceType != DamageSourceType.BasicAttack)
            {
                return null;
            }

            return damage.IsCritical && criticalBasicAttackSlashVfxPoolKey != null
                ? criticalBasicAttackSlashVfxPoolKey
                : basicAttackSlashVfxPoolKey;
        }

        private void HandleDied(EnemyController controller)
        {
            Vector3 center = GetFeedbackCenter();
            Play(deathClip, center);
            Spawn(deathVfx, deathVfxPoolKey, center, GetRandomizedRotation(Quaternion.identity, randomizeDeathVfxRotation, deathVfxRandomRotationRange), false);
        }

        private Vector3 GetFeedbackCenter()
        {
            if (TryGetColliderBounds(out Bounds colliderBounds))
            {
                return colliderBounds.center;
            }

            if (TryGetSpriteBounds(out Bounds spriteBounds))
            {
                return spriteBounds.center;
            }

            return transform.position;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            bounds = default;
            Collider2D[] colliders = enemy != null ? enemy.Colliders : null;
            bool hasBounds = false;

            if (colliders == null)
            {
                return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private bool TryGetSpriteBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (spriteRenderers == null)
            {
                return false;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null || !spriteRenderer.enabled || !spriteRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteRenderer.bounds);
                }
            }

            return hasBounds;
        }

        private void Play(AudioClip clip, Vector3 position)
        {
            GameSfxPlayer.Play(clip, position, sfxVolume, pitchVariance);
        }

        private void GetImpactVfxTransform(DamageInfo damage, Vector3 center, out Vector3 position, out Quaternion rotation)
        {
            Vector2 direction = damage.Direction;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.right;
            }

            direction.Normalize();
            if (invertImpactVfxDirection)
            {
                direction = -direction;
            }

            position = offsetImpactVfxPosition
                ? center + (Vector3)(direction * impactVfxDirectionOffset)
                : center;

            if (rotateImpactVfxToDamageDirection)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                rotation = Quaternion.identity;
            }
        }

        private void DrawImpactDebug(Vector3 center, Vector3 position)
        {
            if (!drawImpactVfxDebug)
            {
                return;
            }

            Debug.DrawLine(center, position, Color.magenta, debugLineDuration);
            Vector3 direction = position - center;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Debug.DrawRay(position, direction.normalized * 0.25f, Color.cyan, debugLineDuration);
            }
        }

        private void Spawn(GameObject prefab, PoolKey poolKey, Vector3 position, Quaternion rotation, bool alignParticleShape)
        {
            GameObject instance = SpawnFromPool(poolKey, position, rotation);
            if (instance == null && prefab != null)
            {
                instance = Instantiate(prefab, position, rotation);
            }

            if (instance == null)
            {
                return;
            }

            if (alignParticleShape)
            {
                ApplyParticleShapeDirection(instance, rotation.eulerAngles.z);
            }

            PooledVfx2D pooledVfx = instance.GetComponent<PooledVfx2D>();
            if (pooledVfx == null)
            {
                pooledVfx = instance.GetComponentInChildren<PooledVfx2D>(true);
            }

            if (pooledVfx != null)
            {
                pooledVfx.LockWorldTransform(position, rotation);
                pooledVfx.PlayDefault();
                return;
            }

            TransientVfx2D transientVfx = instance.GetComponent<TransientVfx2D>();
            if (transientVfx == null)
            {
                transientVfx = instance.AddComponent<TransientVfx2D>();
            }

            transientVfx.PlayFromAnimator(fallbackVfxLifetime);
        }

        private GameObject SpawnFromPool(PoolKey poolKey, Vector3 position, Quaternion rotation)
        {
            if (poolKey == null || ObjectPoolManager.Instance == null)
            {
                return null;
            }

            PoolableGameObject poolable = ObjectPoolManager.Instance.Spawn(poolKey, position, rotation);
            return poolable != null ? poolable.gameObject : null;
        }

        private Quaternion GetRandomizedRotation(Quaternion baseRotation, bool randomize, float randomRange)
        {
            if (!randomize || randomRange <= 0f)
            {
                return baseRotation;
            }

            float randomOffset = Random.Range(-randomRange, randomRange);
            return baseRotation * Quaternion.Euler(0f, 0f, randomOffset);
        }

        private void ApplyParticleShapeDirection(GameObject instance, float impactAngle)
        {
            if (!rotateParticleShapeToImpactDirection || instance == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                if (restartParticlesAfterShapeRotation)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                ParticleSystem.ShapeModule shape = particleSystem.shape;
                shape.rotation = particleShapeRotationOffset + new Vector3(0f, 0f, impactAngle);

                if (restartParticlesAfterShapeRotation)
                {
                    particleSystem.Play(true);
                }
            }
        }
    }
}
