using NeonBreaker.Combat;
using NeonBreaker.Pooling;
using NeonBreaker.Skills;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerPolishFeedback2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private MeleeAttack2D meleeAttack;
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Health health;
        [SerializeField] private PlayerInputReader input;

        [Header("SFX")]
        [SerializeField] private AudioClip attackSwingClip;
        [SerializeField] private AudioClip attackHitClip;
        [SerializeField] private AudioClip dashStartClip;
        [SerializeField] private AudioClip dashEndClip;
        [SerializeField] private AudioClip skillStartClip;
        [SerializeField] private AudioClip playerHitClip;
        [SerializeField] private AudioClip playerDeathClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 0.25f)] private float pitchVariance = 0.04f;

        [Header("VFX")]
        [SerializeField] private GameObject attackSwingVfx;
        [SerializeField] private PoolKey attackSwingVfxPoolKey;
        [Tooltip("Optional attack swing VFX by MeleeAttack2D.CurrentAttackAnimationIndex. Empty entries use the fallback attackSwingVfx.")]
        [SerializeField] private AttackSwingVfxEntry[] attackSwingVfxByAnimationIndex = new AttackSwingVfxEntry[5];
        [SerializeField] private GameObject attackHitVfx;
        [SerializeField] private GameObject dashStartVfx;
        [SerializeField] private PoolKey dashStartVfxPoolKey;
        [SerializeField] private GameObject dashEndVfx;
        [SerializeField] private PoolKey dashEndVfxPoolKey;
        [SerializeField] private GameObject skillStartVfx;
        [SerializeField] private PoolKey skillStartVfxPoolKey;
        [SerializeField] private GameObject playerHitVfx;
        [SerializeField] private GameObject playerDeathVfx;
        [SerializeField, Min(0.01f)] private float fallbackVfxLifetime = 1.2f;

        [Header("Skill VFX")]
        [SerializeField] private Vector3 skillStartWorldOffset;
        [SerializeField] private float skillStartRotationOffset;
        [SerializeField] private Vector3 skillStartRotationEulerOffset;
        [SerializeField] private Vector3 skillStartVfxScale = Vector3.one;
        [SerializeField] private bool scaleSkillStartVfxBySkillRadius = true;
        [SerializeField] private bool rotateSkillStartVfxToAimDirection;

        [Header("Dash VFX Direction")]
        [SerializeField] private bool rotateDashVfxToDashDirection = true;
        [SerializeField] private float dashStartVfxRotationOffset;
        [SerializeField] private float dashEndVfxRotationOffset;
        [SerializeField] private Vector3 dashStartWorldOffset;
        [SerializeField] private Vector3 dashEndWorldOffset;
        [SerializeField] private float dashStartDirectionOffset = -0.25f;
        [SerializeField] private float dashEndDirectionOffset;
        [SerializeField] private Vector3 dashStartVfxScale = Vector3.one;
        [SerializeField] private Vector3 dashEndVfxScale = Vector3.one;

        [Header("Attack Swing Direction")]
        [SerializeField] private bool rotateAttackSwingVfxToAimDirection = true;
        [SerializeField] private bool placeAttackSwingVfxByAttackRange = true;
        [SerializeField] private bool centerFullCircleAttackSwingVfx = true;
        [SerializeField, Range(1f, 360f)] private float fullCircleAttackAngleThreshold = 359f;
        [SerializeField, Range(0f, 1f)] private float attackSwingRangePositionFactor = 0.5f;
        [SerializeField, Min(0f)] private float attackSwingForwardOffset = 0.65f;
        [SerializeField] private Vector3 attackSwingWorldOffset;
        [SerializeField] private float attackSwingRotationOffset;
        [SerializeField] private bool scaleAttackSwingVfxByAttackRange = true;
        [SerializeField] private bool useMeleeAttackBaseRangeForSwingScale;
        [SerializeField, Min(0.01f)] private float attackSwingVfxBaseRange = 1.35f;
        [SerializeField] private Vector3 attackSwingVfxBaseScale = Vector3.one;
        [SerializeField, Min(0f)] private float attackSwingRangeScaleStrength = 1f;
        [SerializeField] private Vector3 attackSwingRangeScaleAxis = Vector3.one;
        [SerializeField] private bool scaleFullCircleAttackSwingVfxByDiameter = true;
        [SerializeField] private Vector3 fullCircleAttackSwingRangeScaleAxis = new Vector3(1f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float fullCircleAttackSwingScaleMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float attackSwingMinRangeScale = 0.25f;
        [SerializeField, Min(0.01f)] private float attackSwingMaxRangeScale = 4f;
        [SerializeField] private bool logAttackSwingVfxScale;
        [SerializeField, HideInInspector] private int attackSwingScaleSettingsVersion;

        [Header("Impact Direction")]
        [SerializeField] private bool offsetImpactVfxPosition;
        [SerializeField, Min(0f)] private float impactVfxDirectionOffset = 0.18f;
        [SerializeField] private bool rotateImpactVfxToDamageDirection = true;
        [SerializeField] private bool invertImpactVfxDirection;
        [SerializeField] private bool rotateParticleShapeToImpactDirection = true;
        [SerializeField] private Vector3 particleShapeRotationOffset = new Vector3(0f, 90f, 0f);
        [SerializeField] private bool restartParticlesAfterShapeRotation = true;
        [SerializeField] private bool drawImpactVfxDebug;
        [SerializeField, Min(0.01f)] private float debugLineDuration = 0.25f;

        private Collider2D[] colliders;
        private SpriteRenderer[] spriteRenderers;

        private const int CurrentAttackSwingScaleSettingsVersion = 2;

        [System.Serializable]
        private struct AttackSwingVfxEntry
        {
            [SerializeField] private GameObject prefab;
            [SerializeField] private PoolKey poolKey;

            public AttackSwingVfxEntry(GameObject prefab, PoolKey poolKey)
            {
                this.prefab = prefab;
                this.poolKey = poolKey;
            }

            public GameObject Prefab => prefab;
            public PoolKey PoolKey => poolKey;
            public bool IsConfigured => prefab != null || poolKey != null;
        }

        private void Awake()
        {
            UpgradeAttackSwingScaleSettings();

            if (meleeAttack == null)
            {
                meleeAttack = GetComponent<MeleeAttack2D>();
            }

            if (dash == null)
            {
                dash = GetComponent<PlayerDash2D>();
            }

            if (skillController == null)
            {
                skillController = GetComponent<PlayerSkillController>();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            colliders = GetComponentsInChildren<Collider2D>(true);
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnValidate()
        {
            UpgradeAttackSwingScaleSettings();
        }

        private void OnEnable()
        {
            if (meleeAttack != null)
            {
                meleeAttack.AttackSwingVfxRequested += HandleAttackSwingVfxRequested;
                meleeAttack.AttackHit += HandleAttackHit;
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
                health.Damaged += HandlePlayerDamaged;
                health.Died += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (meleeAttack != null)
            {
                meleeAttack.AttackSwingVfxRequested -= HandleAttackSwingVfxRequested;
                meleeAttack.AttackHit -= HandleAttackHit;
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
                health.Damaged -= HandlePlayerDamaged;
                health.Died -= HandlePlayerDied;
            }
        }

        private void HandleAttackSwingVfxRequested()
        {
            Play(attackSwingClip);
            GetAttackSwingVfxTransform(out Vector3 position, out Quaternion rotation);
            AttackSwingVfxEntry attackSwing = ResolveAttackSwingVfx();
            GameObject swing = Spawn(attackSwing.Prefab, position, rotation, false, attackSwing.PoolKey);
            Vector3 scale = GetAttackSwingVfxScale();
            LogAttackSwingVfxScale(scale);
            ApplyAttackSwingTransform(swing, position, rotation, scale);
        }

        private void HandleAttackHit(int hitCount)
        {
            if (hitCount <= 0)
            {
                return;
            }

            Play(attackHitClip);
            Spawn(attackHitVfx, transform.position, Quaternion.identity, false);
        }

        private void HandleDashStarted()
        {
            Play(dashStartClip);
            GetDashVfxTransform(
                dashStartDirectionOffset,
                dashStartWorldOffset,
                dashStartVfxRotationOffset,
                out Vector3 position,
                out Quaternion rotation);
            GameObject instance = Spawn(dashStartVfx, position, rotation, false, dashStartVfxPoolKey);
            ApplyLockedVfxTransform(instance, position, rotation, dashStartVfxScale);
        }

        private void HandleDashEnded()
        {
            Play(dashEndClip);
            GetDashVfxTransform(
                dashEndDirectionOffset,
                dashEndWorldOffset,
                dashEndVfxRotationOffset,
                out Vector3 position,
                out Quaternion rotation);
            GameObject instance = Spawn(dashEndVfx, position, rotation, false, dashEndVfxPoolKey);
            ApplyLockedVfxTransform(instance, position, rotation, dashEndVfxScale);
        }

        private void HandleSkillStarted(SkillDefinition skill)
        {
            Play(skillStartClip);
            GetSkillStartVfxTransform(out Vector3 position, out Quaternion rotation);
            GameObject instance = Spawn(skillStartVfx, position, rotation, false, skillStartVfxPoolKey);
            ApplyLockedVfxTransform(instance, position, rotation, GetSkillStartVfxScale(skill));
        }

        private void HandlePlayerDamaged(DamageInfo damage)
        {
            Vector3 center = GetFeedbackCenter();
            GetImpactVfxTransform(damage, center, out Vector3 position, out Quaternion rotation);

            Play(playerHitClip);
            Spawn(playerHitVfx, position, rotation, true);
            DrawImpactDebug(center, position);
        }

        private void HandlePlayerDied()
        {
            Vector3 center = GetFeedbackCenter();
            Play(playerDeathClip);
            Spawn(playerDeathVfx, center, Quaternion.identity, false);
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

        private void Play(AudioClip clip)
        {
            GameSfxPlayer.Play(clip, transform.position, sfxVolume, pitchVariance);
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

        private void GetAttackSwingVfxTransform(out Vector3 position, out Quaternion rotation)
        {
            Vector2 direction = GetAimDirection();
            if (ShouldCenterAttackSwingVfx())
            {
                position = transform.position + attackSwingWorldOffset;
            }
            else
            {
                Vector3 origin = meleeAttack != null ? meleeAttack.AttackOriginPosition : transform.position;
                float forwardDistance = placeAttackSwingVfxByAttackRange
                    ? GetAttackSwingRange() * attackSwingRangePositionFactor
                    : attackSwingForwardOffset;

                position = origin + (Vector3)(direction * forwardDistance) + attackSwingWorldOffset;
            }

            if (rotateAttackSwingVfxToAimDirection)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + attackSwingRotationOffset;
                rotation = Quaternion.Euler(0f, 0f, angle);
                return;
            }

            rotation = transform.rotation;
        }

        private bool ShouldCenterAttackSwingVfx()
        {
            return centerFullCircleAttackSwingVfx
                && IsFullCircleAttackSwing();
        }

        private Vector2 GetAimDirection()
        {
            if (input != null && input.AimDirection.sqrMagnitude > 0.0001f)
            {
                return input.AimDirection.normalized;
            }

            Vector2 right = transform.right;
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector2.right;
        }

        private void GetDashVfxTransform(
            float directionOffset,
            Vector3 worldOffset,
            float rotationOffset,
            out Vector3 position,
            out Quaternion rotation)
        {
            Vector2 direction = GetDashDirection();
            position = transform.position + (Vector3)(direction * directionOffset) + worldOffset;

            if (rotateDashVfxToDashDirection)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
                rotation = Quaternion.Euler(0f, 0f, angle);
                return;
            }

            rotation = transform.rotation;
        }

        private Vector2 GetDashDirection()
        {
            if (dash != null && dash.LastDashDirection.sqrMagnitude > 0.0001f)
            {
                return dash.LastDashDirection.normalized;
            }

            if (input != null && input.MoveInput.sqrMagnitude > 0.0001f)
            {
                return input.MoveInput.normalized;
            }

            return GetAimDirection();
        }

        private void GetSkillStartVfxTransform(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position + skillStartWorldOffset;
            Vector3 eulerOffset = skillStartRotationEulerOffset;
            eulerOffset.z += skillStartRotationOffset;
            Quaternion offsetRotation = Quaternion.Euler(eulerOffset);

            if (rotateSkillStartVfxToAimDirection)
            {
                Vector2 direction = GetAimDirection();
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(0f, 0f, angle) * offsetRotation;
                return;
            }

            rotation = offsetRotation;
        }


        private float GetAttackSwingRange()
        {
            if (meleeAttack != null)
            {
                return meleeAttack.CurrentAttackEffectiveRange > 0f
                    ? meleeAttack.CurrentAttackEffectiveRange
                    : meleeAttack.EffectiveAttackRange;
            }

            return attackSwingVfxBaseRange;
        }

        private Vector3 GetAttackSwingVfxScale()
        {
            Vector3 scale = GetSafeAttackSwingBaseScale();
            if (!scaleAttackSwingVfxByAttackRange)
            {
                return scale;
            }

            float baseRange = GetAttackSwingBaseRange();
            float rangeRatio = GetAttackSwingRangeRatio(baseRange);
            float scaleMultiplier = Mathf.Lerp(1f, rangeRatio, attackSwingRangeScaleStrength);
            scaleMultiplier = Mathf.Clamp(
                scaleMultiplier,
                Mathf.Min(attackSwingMinRangeScale, attackSwingMaxRangeScale),
                Mathf.Max(attackSwingMinRangeScale, attackSwingMaxRangeScale));

            Vector3 axis = GetAttackSwingScaleAxis();
            return new Vector3(
                scale.x * Mathf.Lerp(1f, scaleMultiplier, Mathf.Max(0f, axis.x)),
                scale.y * Mathf.Lerp(1f, scaleMultiplier, Mathf.Max(0f, axis.y)),
                scale.z * Mathf.Lerp(1f, scaleMultiplier, Mathf.Max(0f, axis.z)));
        }

        private float GetAttackSwingBaseRange()
        {
            if (useMeleeAttackBaseRangeForSwingScale && meleeAttack != null)
            {
                return meleeAttack.BaseAttackRange;
            }

            return attackSwingVfxBaseRange;
        }

        private void LogAttackSwingVfxScale(Vector3 finalScale)
        {
            if (!logAttackSwingVfxScale)
            {
                return;
            }

            float baseRange = GetAttackSwingBaseRange();
            float effectiveRange = GetAttackSwingRange();
            float rangeRatio = GetAttackSwingRangeRatio(baseRange);
            Debug.Log(
                $"[PlayerPolishFeedback2D] Attack Swing VFX Scale. Base Range: {baseRange:0.###}, Effective Range: {effectiveRange:0.###}, Ratio: {rangeRatio:0.###}, Strength: {attackSwingRangeScaleStrength:0.###}, Axis: {GetAttackSwingScaleAxis()}, Final Scale: {finalScale}",
                this);
        }

        private Vector3 GetSafeAttackSwingBaseScale()
        {
            if (attackSwingVfxBaseScale.sqrMagnitude <= 0.0001f)
            {
                return Vector3.one;
            }

            return attackSwingVfxBaseScale;
        }

        private Vector3 GetSafeAttackSwingRangeScaleAxis()
        {
            if (attackSwingRangeScaleAxis.sqrMagnitude <= 0.0001f)
            {
                return Vector3.one;
            }

            return attackSwingRangeScaleAxis;
        }

        private Vector3 GetAttackSwingScaleAxis()
        {
            if (IsFullCircleAttackSwing())
            {
                return fullCircleAttackSwingRangeScaleAxis.sqrMagnitude > 0.0001f
                    ? fullCircleAttackSwingRangeScaleAxis
                    : Vector3.one;
            }

            return GetSafeAttackSwingRangeScaleAxis();
        }

        private float GetAttackSwingRangeRatio(float baseRange)
        {
            float effectiveRange = GetAttackSwingRange();
            if (IsFullCircleAttackSwing() && scaleFullCircleAttackSwingVfxByDiameter)
            {
                effectiveRange *= 2f;
            }

            float multiplier = IsFullCircleAttackSwing()
                ? Mathf.Max(0.01f, fullCircleAttackSwingScaleMultiplier)
                : 1f;

            return effectiveRange * multiplier / Mathf.Max(0.01f, baseRange);
        }

        private bool IsFullCircleAttackSwing()
        {
            return meleeAttack != null
                && meleeAttack.CurrentAttackEffectiveAngle >= fullCircleAttackAngleThreshold;
        }

        private void UpgradeAttackSwingScaleSettings()
        {
            if (attackSwingScaleSettingsVersion < CurrentAttackSwingScaleSettingsVersion)
            {
                scaleAttackSwingVfxByAttackRange = true;
                useMeleeAttackBaseRangeForSwingScale = false;

                if (attackSwingVfxBaseScale.sqrMagnitude <= 0.0001f)
                {
                    attackSwingVfxBaseScale = Vector3.one;
                }

                if (attackSwingRangeScaleStrength <= 0.0001f)
                {
                    attackSwingRangeScaleStrength = 1f;
                }

                if (attackSwingRangeScaleAxis.sqrMagnitude <= 0.0001f)
                {
                    attackSwingRangeScaleAxis = Vector3.one;
                }

                if (attackSwingMinRangeScale <= 0.0001f)
                {
                    attackSwingMinRangeScale = 0.25f;
                }

                if (attackSwingMaxRangeScale <= 0.0001f)
                {
                    attackSwingMaxRangeScale = 4f;
                }

                attackSwingScaleSettingsVersion = CurrentAttackSwingScaleSettingsVersion;
            }

            attackSwingVfxBaseRange = Mathf.Max(0.01f, attackSwingVfxBaseRange);
            attackSwingRangeScaleStrength = Mathf.Max(0f, attackSwingRangeScaleStrength);
            attackSwingMinRangeScale = Mathf.Max(0.01f, attackSwingMinRangeScale);
            attackSwingMaxRangeScale = Mathf.Max(0.01f, attackSwingMaxRangeScale);
        }

        private Vector3 GetSkillStartVfxScale(SkillDefinition skill)
        {
            Vector3 scale = skillStartVfxScale;
            if (!scaleSkillStartVfxBySkillRadius || skill == null)
            {
                return scale;
            }

            float baseRadius = skill.Radius;
            float effectiveRadius = stats != null ? stats.GetSkillRadius(baseRadius) : baseRadius;
            float scaleMultiplier = effectiveRadius / Mathf.Max(0.01f, baseRadius);
            return scale * scaleMultiplier;
        }

        private AttackSwingVfxEntry ResolveAttackSwingVfx()
        {
            if (meleeAttack != null && attackSwingVfxByAnimationIndex != null)
            {
                int animationIndex = meleeAttack.CurrentAttackAnimationIndex;
                if (animationIndex >= 0 && animationIndex < attackSwingVfxByAnimationIndex.Length)
                {
                    AttackSwingVfxEntry entry = attackSwingVfxByAnimationIndex[animationIndex];
                    if (entry.IsConfigured)
                    {
                        return entry;
                    }
                }
            }

            return new AttackSwingVfxEntry(attackSwingVfx, attackSwingVfxPoolKey);
        }

        private void ApplyAttackSwingTransform(GameObject swing, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (swing == null)
            {
                return;
            }

            PooledVfx2D pooledVfx = swing.GetComponent<PooledVfx2D>();
            if (pooledVfx == null)
            {
                pooledVfx = swing.GetComponentInChildren<PooledVfx2D>(true);
            }

            if (pooledVfx != null)
            {
                bool appliedVisualScale = pooledVfx.SetVisualScaleMultiplier(scale);
                pooledVfx.LockWorldTransform(position, rotation, appliedVisualScale ? Vector3.one : scale);
                return;
            }

            swing.transform.SetPositionAndRotation(position, rotation);
            swing.transform.localScale = scale;
        }

        private static void ApplyLockedVfxTransform(
            GameObject instance,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool flipVisualX = false,
            bool flipVisualY = false)
        {
            if (instance == null)
            {
                return;
            }

            PooledVfx2D pooledVfx = instance.GetComponent<PooledVfx2D>();
            if (pooledVfx == null)
            {
                pooledVfx = instance.GetComponentInChildren<PooledVfx2D>(true);
            }

            if (pooledVfx != null)
            {
                pooledVfx.LockWorldTransform(position, rotation, scale);
                pooledVfx.SetVisualFlip(flipVisualX, flipVisualY);
                return;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            ApplyFallbackChildVisualFlip(instance, flipVisualX, flipVisualY);
        }

        private static void ApplyFallbackChildVisualFlip(GameObject swing, bool flipX, bool flipY)
        {
            if (swing.transform.childCount <= 0)
            {
                return;
            }

            Transform visual = swing.transform.GetChild(0);
            Vector3 scale = visual.localScale;
            if (flipX)
            {
                scale.x *= -1f;
            }

            if (flipY)
            {
                scale.y *= -1f;
            }

            visual.localScale = scale;
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

        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool alignParticleShape)
        {
            return Spawn(prefab, position, rotation, alignParticleShape, null);
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool alignParticleShape, PoolKey poolKey)
        {
            if (prefab == null && poolKey == null)
            {
                return null;
            }

            GameObject instance = SpawnFromPool(poolKey, position, rotation);
            bool spawnedFromPool = instance != null;

            if (instance == null)
            {
                if (prefab == null)
                {
                    return null;
                }

                instance = Instantiate(prefab, position, rotation);
            }

            if (alignParticleShape)
            {
                ApplyParticleShapeDirection(instance, rotation.eulerAngles.z);
            }

            if (spawnedFromPool)
            {
                return instance;
            }

            PooledVfx2D pooledVfx = instance.GetComponent<PooledVfx2D>();
            if (pooledVfx == null)
            {
                pooledVfx = instance.GetComponentInChildren<PooledVfx2D>(true);
            }

            if (pooledVfx != null)
            {
                pooledVfx.PlayDefault();
                return instance;
            }

            if (instance.GetComponent<TransientVfx2D>() == null && instance.GetComponentInChildren<ParticleSystem>() == null)
            {
                instance.AddComponent<TransientVfx2D>().Play(fallbackVfxLifetime);
            }

            return instance;
        }

        private static GameObject SpawnFromPool(PoolKey poolKey, Vector3 position, Quaternion rotation)
        {
            if (poolKey == null || ObjectPoolManager.Instance == null)
            {
                return null;
            }

            PoolableGameObject poolable = ObjectPoolManager.Instance.Spawn(poolKey, position, rotation);
            return poolable != null ? poolable.gameObject : null;
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
