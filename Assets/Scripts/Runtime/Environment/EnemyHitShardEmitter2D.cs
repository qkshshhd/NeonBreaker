using System.Collections;
using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Environment
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyHitShardEmitter2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health health;
        [SerializeField] private Transform shardRoot;
        [SerializeField] private bool useSharedWorldShardRoot = true;
        [SerializeField] private bool keepShardsAfterEmitterDestroyed = true;

        [Header("Shard Visual")]
        [SerializeField] private Sprite[] shardSprites;
        [SerializeField] private Material shardMaterial;
        [SerializeField] private Color[] shardColors =
        {
            new Color(0.1f, 0.95f, 1f, 1f),
            new Color(1f, 0.15f, 0.55f, 1f),
            new Color(0.95f, 0.95f, 1f, 1f)
        };
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 45;
        [SerializeField] private Vector2 sizeRange = new Vector2(0.12f, 0.26f);

        [Header("Shard Trail")]
        [SerializeField] private bool useLightStreak = true;
        [SerializeField] private Material streakMaterial;
        [SerializeField, Min(0f)] private float streakLength = 0.9f;
        [SerializeField, Min(0f)] private float streakStartWidth = 0.13f;
        [SerializeField, Min(0f)] private float streakEndWidth = 0.025f;
        [SerializeField, Range(0f, 1f)] private float streakAlpha = 0.9f;

        [Header("Emit")]
        [SerializeField, Min(0)] private int minShardCount = 10;
        [SerializeField, Min(0)] private int maxShardCount = 18;
        [SerializeField, Min(0f)] private float criticalCountMultiplier = 1.65f;
        [SerializeField, Min(0f)] private float fatalHitCountMultiplier = 1.45f;
        [SerializeField] private bool emitOnlyForPlayerDamage = true;
        [SerializeField] private bool ignoreSkillDamage;
        [SerializeField] private bool emitAwayFromDamageSource = true;
        [SerializeField] private bool invertEmitDirection;
        [SerializeField, Range(0f, 180f)] private float spreadAngle = 135f;
        [SerializeField] private Vector2 speedRange = new Vector2(8f, 15f);
        [SerializeField] private Vector2 settleTimeRange = new Vector2(0.12f, 0.24f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(1.8f, 3.4f);
        [SerializeField, Min(0f)] private float minimumTravelDistance = 1.25f;
        [SerializeField] private Vector2 travelDistanceMultiplierRange = new Vector2(0.65f, 1.45f);
        [SerializeField] private Vector2 easePowerRange = new Vector2(2.4f, 5.2f);
        [SerializeField] private Vector2 spinSpeedRange = new Vector2(520f, 1320f);
        [SerializeField, Range(0f, 1f)] private float bounceBackAmount = 0.02f;
        [SerializeField, Min(0f)] private float spawnRadius = 0.08f;
        [SerializeField, Min(0f)] private float forwardSpawnOffset = 0.12f;

        [Header("Spawn Placement")]
        [SerializeField] private bool spawnFromEnemyBackSide = true;
        [SerializeField, Range(0.5f, 1.5f)] private float backSideBoundsOffset = 0.95f;
        [SerializeField, Min(0f)] private float minimumBackSideOffset = 0.35f;
        [SerializeField, Min(0f)] private float backSideTangentJitter = 0.18f;

        [Header("Environment Collision")]
        [SerializeField] private bool collideWithEnvironment = true;
        [SerializeField] private LayerMask environmentCollisionLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField, Min(0f)] private float collisionSkin = 0.035f;
        [SerializeField, Range(0f, 1f)] private float collisionBounceFactor = 0.22f;
        [SerializeField, Min(0f)] private float maxCollisionBounceDistance = 0.35f;

        private const int CollisionCastCapacity = 8;
        private static readonly Queue<SpriteRenderer> SharedInactiveShards = new Queue<SpriteRenderer>();
        private static readonly RaycastHit2D[] CollisionHits = new RaycastHit2D[CollisionCastCapacity];
        private readonly List<SpriteRenderer> activeShards = new List<SpriteRenderer>();
        private readonly Dictionary<SpriteRenderer, Coroutine> activeShardRoutines = new Dictionary<SpriteRenderer, Coroutine>();
        private Sprite fallbackSprite;
        private static Transform sharedShardRoot;
        private static Material fallbackStreakMaterial;
        private static ShardCoroutineRunner coroutineRunner;

        private sealed class ShardCoroutineRunner : MonoBehaviour { }

        private readonly struct ShardMotionSettings
        {
            public ShardMotionSettings(
                Vector2 settleTimeRange,
                Vector2 lifetimeRange,
                float minimumTravelDistance,
                Vector2 travelDistanceMultiplierRange,
                Vector2 easePowerRange,
                Vector2 spinSpeedRange,
                float bounceBackAmount,
                float streakLength,
                float streakAlpha,
                bool collideWithEnvironment,
                LayerMask environmentCollisionLayers,
                float collisionSkin,
                float collisionBounceFactor,
                float maxCollisionBounceDistance)
            {
                this.settleTimeRange = settleTimeRange;
                this.lifetimeRange = lifetimeRange;
                this.minimumTravelDistance = minimumTravelDistance;
                this.travelDistanceMultiplierRange = travelDistanceMultiplierRange;
                this.easePowerRange = easePowerRange;
                this.spinSpeedRange = spinSpeedRange;
                this.bounceBackAmount = bounceBackAmount;
                this.streakLength = streakLength;
                this.streakAlpha = streakAlpha;
                this.collideWithEnvironment = collideWithEnvironment;
                this.environmentCollisionLayers = environmentCollisionLayers;
                this.collisionSkin = collisionSkin;
                this.collisionBounceFactor = collisionBounceFactor;
                this.maxCollisionBounceDistance = maxCollisionBounceDistance;
            }

            public readonly Vector2 settleTimeRange;
            public readonly Vector2 lifetimeRange;
            public readonly float minimumTravelDistance;
            public readonly Vector2 travelDistanceMultiplierRange;
            public readonly Vector2 easePowerRange;
            public readonly Vector2 spinSpeedRange;
            public readonly float bounceBackAmount;
            public readonly float streakLength;
            public readonly float streakAlpha;
            public readonly bool collideWithEnvironment;
            public readonly LayerMask environmentCollisionLayers;
            public readonly float collisionSkin;
            public readonly float collisionBounceFactor;
            public readonly float maxCollisionBounceDistance;
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (shardRoot == null)
            {
                shardRoot = useSharedWorldShardRoot ? GetSharedShardRoot() : CreateLocalShardRoot();
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDestroy()
        {
            if (!keepShardsAfterEmitterDestroyed)
            {
                ClearOwnedActiveShards(destroyObjects: true);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (!ShouldEmit(damage))
            {
                return;
            }

            int count = Random.Range(minShardCount, Mathf.Max(minShardCount, maxShardCount) + 1);
            if (damage.IsCritical)
            {
                count = Mathf.CeilToInt(count * Mathf.Max(1f, criticalCountMultiplier));
            }

            if (health != null && health.CurrentHealth <= 0f)
            {
                count = Mathf.CeilToInt(count * Mathf.Max(1f, fatalHitCountMultiplier));
            }

            Vector2 damageDirection = damage.Direction.sqrMagnitude > 0.0001f ? damage.Direction.normalized : Vector2.right;
            Vector2 emitDirection = GetEmitDirection(damageDirection);
            Vector3 origin = GetSpawnOrigin(damage, emitDirection);
            for (int i = 0; i < count; i++)
            {
                EmitShard(origin, emitDirection);
            }
        }

        private bool ShouldEmit(DamageInfo damage)
        {
            if (damage.Amount <= 0f)
            {
                return false;
            }

            if (ignoreSkillDamage && damage.SourceType == DamageSourceType.Skill)
            {
                return false;
            }

            if (!emitOnlyForPlayerDamage)
            {
                return true;
            }

            return damage.SourceType == DamageSourceType.BasicAttack
                || damage.SourceType == DamageSourceType.Skill
                || damage.SourceType == DamageSourceType.Dash;
        }

        private Vector3 GetSpawnOrigin(DamageInfo damage, Vector2 emitDirection)
        {
            Vector2 direction = emitDirection.sqrMagnitude > 0.0001f ? emitDirection.normalized : Vector2.right;
            Vector3 origin;

            if (spawnFromEnemyBackSide)
            {
                origin = GetBackSideOrigin(direction);
            }
            else
            {
                origin = damage.Point;
                if (origin == Vector3.zero)
                {
                    origin = transform.position;
                }
            }

            origin.z = transform.position.z;
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 jitter = tangent * Random.Range(-backSideTangentJitter, backSideTangentJitter);
            jitter += direction * Random.Range(-spawnRadius * 0.35f, spawnRadius);
            return origin + (Vector3)jitter;
        }

        private void EmitShard(Vector3 origin, Vector2 baseDirection)
        {
            SpriteRenderer shard = GetShard();
            if (shard == null)
            {
                return;
            }

            Vector3 spawnPosition = origin + (Vector3)(baseDirection * forwardSpawnOffset);
            shard.transform.position = spawnPosition;
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            shard.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + Random.Range(-28f, 28f));
            shard.transform.localScale = Vector3.one * Random.Range(sizeRange.x, Mathf.Max(sizeRange.x, sizeRange.y));
            shard.sprite = GetShardSprite();
            shard.color = GetShardColor();
            shard.sortingLayerName = sortingLayer;
            shard.sortingOrder = sortingOrder;
            shard.sharedMaterial = shardMaterial;
            shard.gameObject.SetActive(true);
            ConfigureStreak(shard, baseDirection);

            float angle = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            Vector2 velocity = Quaternion.Euler(0f, 0f, angle) * baseDirection.normalized;
            velocity *= Random.Range(speedRange.x, Mathf.Max(speedRange.x, speedRange.y));

            activeShards.Add(shard);
            activeShardRoutines[shard] = GetCoroutineRunner().StartCoroutine(ShardRoutine(shard, velocity, CreateMotionSettings()));
        }

        private ShardMotionSettings CreateMotionSettings()
        {
            return new ShardMotionSettings(
                settleTimeRange,
                lifetimeRange,
                minimumTravelDistance,
                travelDistanceMultiplierRange,
                easePowerRange,
                spinSpeedRange,
                bounceBackAmount,
                streakLength,
                streakAlpha,
                collideWithEnvironment,
                environmentCollisionLayers,
                collisionSkin,
                collisionBounceFactor,
                maxCollisionBounceDistance);
        }

        private Vector3 GetBackSideOrigin(Vector2 direction)
        {
            Bounds bounds = GetEffectBounds();
            Vector3 center = bounds.size.sqrMagnitude > 0.0001f ? bounds.center : transform.position;
            Vector3 extents = bounds.extents;
            Vector2 absoluteDirection = new Vector2(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            float directionalExtent = extents.x * absoluteDirection.x + extents.y * absoluteDirection.y;
            float offset = Mathf.Max(minimumBackSideOffset, directionalExtent * backSideBoundsOffset);
            return center + (Vector3)(direction.normalized * offset);
        }

        private Bounds GetEffectBounds()
        {
            bool hasBounds = false;
            Bounds result = default;

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    result = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
            {
                return result;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    result = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? result : new Bounds(transform.position, Vector3.zero);
        }

        private static IEnumerator ShardRoutine(SpriteRenderer shard, Vector2 velocity, ShardMotionSettings settings)
        {
            float settleTime = Random.Range(settings.settleTimeRange.x, Mathf.Max(settings.settleTimeRange.x, settings.settleTimeRange.y));
            Vector3 start = shard.transform.position;
            Vector2 moveDirection = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;
            float distanceMultiplier = Random.Range(
                Mathf.Min(settings.travelDistanceMultiplierRange.x, settings.travelDistanceMultiplierRange.y),
                Mathf.Max(settings.travelDistanceMultiplierRange.x, settings.travelDistanceMultiplierRange.y));
            float minimumDistance = settings.minimumTravelDistance * distanceMultiplier;
            float travelDistance = Mathf.Max(minimumDistance, velocity.magnitude * settleTime * distanceMultiplier);
            Vector3 target = start + (Vector3)(moveDirection * travelDistance);
            target += (Vector3)(-moveDirection * travelDistance * settings.bounceBackAmount);
            LineRenderer streak = shard.GetComponent<LineRenderer>();
            float easePower = Random.Range(
                Mathf.Min(settings.easePowerRange.x, settings.easePowerRange.y),
                Mathf.Max(settings.easePowerRange.x, settings.easePowerRange.y));
            float spinSpeed = Random.Range(
                Mathf.Min(settings.spinSpeedRange.x, settings.spinSpeedRange.y),
                Mathf.Max(settings.spinSpeedRange.x, settings.spinSpeedRange.y));
            spinSpeed *= Random.value < 0.5f ? -1f : 1f;

            float timer = 0f;
            Vector3 previousPosition = start;
            while (timer < settleTime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, settleTime));
                float eased = 1f - Mathf.Pow(1f - t, easePower);
                Vector3 desiredPosition = Vector3.LerpUnclamped(start, target, eased);
                if (TryResolveEnvironmentCollision(previousPosition, desiredPosition, settings, out Vector3 collisionPosition, out Vector3 bounceTarget))
                {
                    shard.transform.position = collisionPosition;
                    UpdateStreak(streak, collisionPosition, moveDirection, 1f - t, settings.streakAlpha, settings.streakLength);

                    if ((bounceTarget - collisionPosition).sqrMagnitude > 0.0001f)
                    {
                        yield return BounceShard(shard, streak, collisionPosition, bounceTarget, spinSpeed, moveDirection, settings);
                    }

                    break;
                }

                shard.transform.position = desiredPosition;
                shard.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime * (1f - t));
                UpdateStreak(streak, desiredPosition, moveDirection, 1f - t, settings.streakAlpha, settings.streakLength);
                previousPosition = desiredPosition;
                yield return null;
            }

            if ((shard.transform.position - target).sqrMagnitude > 0.0001f && !HasEnvironmentCollisionBetween(shard.transform.position, target, settings))
            {
                shard.transform.position = target;
            }
            SetStreakVisible(streak, false);

            float lifetime = Random.Range(settings.lifetimeRange.x, Mathf.Max(settings.lifetimeRange.x, settings.lifetimeRange.y));
            float fadeTime = Mathf.Min(0.75f, lifetime * 0.35f);
            float waitTime = Mathf.Max(0f, lifetime - fadeTime);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            Color startColor = shard.color;
            timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, fadeTime));
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                shard.color = color;
                yield return null;
            }

            ReturnSharedShard(shard);
        }

        private static IEnumerator BounceShard(SpriteRenderer shard, LineRenderer streak, Vector3 start, Vector3 target, float spinSpeed, Vector2 fallbackDirection, ShardMotionSettings settings)
        {
            float bounceTime = Mathf.Clamp(Vector3.Distance(start, target) * 0.08f, 0.035f, 0.08f);
            Vector2 streakDirection = ((Vector2)(target - start)).sqrMagnitude > 0.0001f
                ? ((Vector2)(target - start)).normalized
                : fallbackDirection;

            float timer = 0f;
            while (timer < bounceTime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, bounceTime));
                Vector3 desiredPosition = Vector3.Lerp(start, target, 1f - Mathf.Pow(1f - t, 2f));
                if (TryResolveEnvironmentCollision(shard.transform.position, desiredPosition, settings, out Vector3 collisionPosition, out _))
                {
                    shard.transform.position = collisionPosition;
                    UpdateStreak(streak, collisionPosition, streakDirection, 1f - t, settings.streakAlpha, settings.streakLength);
                    yield break;
                }

                shard.transform.position = desiredPosition;
                shard.transform.Rotate(0f, 0f, spinSpeed * 0.45f * Time.deltaTime * (1f - t));
                UpdateStreak(streak, desiredPosition, streakDirection, 1f - t, settings.streakAlpha, settings.streakLength);
                yield return null;
            }

            shard.transform.position = target;
        }

        private static bool TryResolveEnvironmentCollision(Vector3 from, Vector3 to, ShardMotionSettings settings, out Vector3 collisionPosition, out Vector3 bounceTarget)
        {
            collisionPosition = to;
            bounceTarget = to;

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (!settings.collideWithEnvironment || settings.environmentCollisionLayers.value == 0 || distance <= 0.0001f)
            {
                return false;
            }

            Vector2 direction = delta / distance;
            if (!TryGetNearestEnvironmentHit(from, direction, distance, settings.environmentCollisionLayers, out RaycastHit2D hit))
            {
                return false;
            }

            Vector2 safePosition = hit.point - direction * settings.collisionSkin;
            collisionPosition = new Vector3(safePosition.x, safePosition.y, from.z);

            Vector2 reflectedDirection = Vector2.Reflect(direction, hit.normal);
            if (reflectedDirection.sqrMagnitude <= 0.0001f || settings.collisionBounceFactor <= 0f)
            {
                bounceTarget = collisionPosition;
                return true;
            }

            float remainingDistance = distance * Mathf.Clamp01(1f - hit.fraction);
            float bounceDistance = Mathf.Min(settings.maxCollisionBounceDistance, remainingDistance * settings.collisionBounceFactor);
            bounceTarget = collisionPosition + (Vector3)(reflectedDirection.normalized * bounceDistance);
            bounceTarget.z = from.z;
            return true;
        }

        private static bool HasEnvironmentCollisionBetween(Vector3 from, Vector3 to, ShardMotionSettings settings)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (!settings.collideWithEnvironment || settings.environmentCollisionLayers.value == 0 || distance <= 0.0001f)
            {
                return false;
            }

            return TryGetNearestEnvironmentHit(from, delta / distance, distance, settings.environmentCollisionLayers, out _);
        }

        private static bool TryGetNearestEnvironmentHit(Vector2 origin, Vector2 direction, float distance, LayerMask layerMask, out RaycastHit2D nearestHit)
        {
            nearestHit = default;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
            filter.useTriggers = false;

            int hitCount = Physics2D.Raycast(origin, direction, filter, CollisionHits, distance);
            float nearestFraction = float.PositiveInfinity;
            bool hasHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = CollisionHits[i];
                if (!IsValidEnvironmentHit(hit))
                {
                    continue;
                }

                if (hit.fraction < nearestFraction)
                {
                    nearestFraction = hit.fraction;
                    nearestHit = hit;
                    hasHit = true;
                }
            }

            return hasHit;
        }

        private static bool IsValidEnvironmentHit(RaycastHit2D hit)
        {
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.isTrigger)
            {
                return false;
            }

            return hitCollider.GetComponentInParent<Health>() == null;
        }

        private Vector2 GetEmitDirection(Vector2 damageDirection)
        {
            Vector2 direction = emitAwayFromDamageSource ? damageDirection : -damageDirection;
            if (invertEmitDirection)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Random.insideUnitCircle;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            return direction.normalized;
        }

        private SpriteRenderer GetShard()
        {
            while (SharedInactiveShards.Count > 0)
            {
                SpriteRenderer pooledShard = SharedInactiveShards.Dequeue();
                if (pooledShard != null)
                {
                    return pooledShard;
                }
            }

            GameObject shardObject = new GameObject("Enemy Hit Shard");
            if (shardRoot != null)
            {
                shardObject.transform.SetParent(shardRoot, true);
            }

            SpriteRenderer shard = shardObject.AddComponent<SpriteRenderer>();
            LineRenderer streak = shardObject.AddComponent<LineRenderer>();
            streak.useWorldSpace = true;
            streak.positionCount = 2;
            streak.alignment = LineAlignment.View;
            streak.textureMode = LineTextureMode.Stretch;
            streak.numCapVertices = 2;
            streak.numCornerVertices = 2;
            streak.enabled = false;
            return shard;
        }

        private Sprite GetShardSprite()
        {
            if (shardSprites != null && shardSprites.Length > 0)
            {
                Sprite sprite = shardSprites[Random.Range(0, shardSprites.Length)];
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return GetFallbackSprite();
        }

        private Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            Texture2D texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    bool filled = x >= y && x < texture.width - y;
                    texture.SetPixel(x, y, filled ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.25f, 0.5f), 16f);
            fallbackSprite.name = "Runtime Hit Shard Sliver";
            return fallbackSprite;
        }

        private Color GetShardColor()
        {
            if (shardColors == null || shardColors.Length == 0)
            {
                return Color.cyan;
            }

            Color color = shardColors[Random.Range(0, shardColors.Length)];
            color.a = 1f;
            return color;
        }

        private void ConfigureStreak(SpriteRenderer shard, Vector2 direction)
        {
            LineRenderer streak = shard.GetComponent<LineRenderer>();
            if (streak == null)
            {
                return;
            }

            streak.enabled = useLightStreak && streakLength > 0f;
            if (!streak.enabled)
            {
                return;
            }

            streak.sharedMaterial = streakMaterial != null ? streakMaterial : GetFallbackStreakMaterial();
            streak.sortingLayerName = sortingLayer;
            streak.sortingOrder = sortingOrder + 1;
            streak.startWidth = streakStartWidth;
            streak.endWidth = streakEndWidth;
            Color color = shard.color;
            color.a *= streakAlpha;
            Color endColor = color;
            endColor.a *= 0.08f;
            streak.startColor = color;
            streak.endColor = endColor;
            UpdateStreak(streak, shard.transform.position, direction, 1f, streakAlpha, streakLength);
        }

        private static void UpdateStreak(LineRenderer streak, Vector3 position, Vector2 direction, float alphaMultiplier, float streakAlpha, float streakLength)
        {
            if (streak == null || !streak.enabled)
            {
                return;
            }

            Vector3 tail = position - (Vector3)(direction.normalized * streakLength * Mathf.Clamp01(alphaMultiplier));
            streak.SetPosition(0, tail);
            streak.SetPosition(1, position);
            Color start = streak.startColor;
            Color end = streak.endColor;
            start.a = Mathf.Clamp01(streakAlpha * alphaMultiplier);
            end.a = Mathf.Clamp01(streakAlpha * 0.08f * alphaMultiplier);
            streak.startColor = start;
            streak.endColor = end;
        }

        private static void SetStreakVisible(LineRenderer streak, bool visible)
        {
            if (streak != null)
            {
                streak.enabled = visible;
            }
        }

        private static Material GetFallbackStreakMaterial()
        {
            if (fallbackStreakMaterial != null)
            {
                return fallbackStreakMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            fallbackStreakMaterial = shader != null ? new Material(shader) : null;
            return fallbackStreakMaterial;
        }

        private void ReturnShard(SpriteRenderer shard)
        {
            if (shard == null)
            {
                return;
            }

            activeShards.Remove(shard);
            activeShardRoutines.Remove(shard);
            SetStreakVisible(shard.GetComponent<LineRenderer>(), false);
            shard.gameObject.SetActive(false);
            Transform root = useSharedWorldShardRoot || shardRoot == null ? GetSharedShardRoot() : shardRoot;
            shard.transform.SetParent(root, true);
            SharedInactiveShards.Enqueue(shard);
        }

        private static void ReturnSharedShard(SpriteRenderer shard)
        {
            if (shard == null)
            {
                return;
            }

            SetStreakVisible(shard.GetComponent<LineRenderer>(), false);
            shard.gameObject.SetActive(false);
            shard.transform.SetParent(GetSharedShardRoot(), true);
            SharedInactiveShards.Enqueue(shard);
        }

        private void ClearOwnedActiveShards(bool destroyObjects)
        {
            foreach (KeyValuePair<SpriteRenderer, Coroutine> pair in activeShardRoutines)
            {
                if (pair.Value != null && coroutineRunner != null)
                {
                    coroutineRunner.StopCoroutine(pair.Value);
                }
            }

            activeShardRoutines.Clear();

            for (int i = activeShards.Count - 1; i >= 0; i--)
            {
                SpriteRenderer shard = activeShards[i];
                if (shard == null)
                {
                    continue;
                }

                SetStreakVisible(shard.GetComponent<LineRenderer>(), false);
                if (destroyObjects)
                {
                    Destroy(shard.gameObject);
                }
                else
                {
                    shard.gameObject.SetActive(false);
                    SharedInactiveShards.Enqueue(shard);
                }
            }

            activeShards.Clear();
        }

        private static Transform GetSharedShardRoot()
        {
            if (sharedShardRoot != null)
            {
                return sharedShardRoot;
            }

            GameObject existing = GameObject.Find("Enemy Hit Shards (World)");
            if (existing != null)
            {
                sharedShardRoot = existing.transform;
                return sharedShardRoot;
            }

            GameObject rootObject = new GameObject("Enemy Hit Shards (World)");
            rootObject.transform.SetParent(null, false);
            sharedShardRoot = rootObject.transform;
            return sharedShardRoot;
        }

        private static Transform CreateLocalShardRoot()
        {
            GameObject rootObject = new GameObject("Hit Shards");
            rootObject.transform.SetParent(null, false);
            return rootObject.transform;
        }

        private static void ClearSharedInactiveShards()
        {
            while (SharedInactiveShards.Count > 0)
            {
                SpriteRenderer shard = SharedInactiveShards.Dequeue();
                if (shard != null)
                {
                    Destroy(shard.gameObject);
                }
            }
        }

        private static ShardCoroutineRunner GetCoroutineRunner()
        {
            if (coroutineRunner != null)
            {
                return coroutineRunner;
            }

            GameObject runnerObject = new GameObject("Enemy Hit Shard Coroutine Runner");
            DontDestroyOnLoad(runnerObject);
            coroutineRunner = runnerObject.AddComponent<ShardCoroutineRunner>();
            return coroutineRunner;
        }
    }
}
