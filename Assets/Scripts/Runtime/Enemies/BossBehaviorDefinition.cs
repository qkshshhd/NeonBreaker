using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Behaviors/Boss Behavior Definition")]
    public sealed class BossBehaviorDefinition : ScriptableObject
    {
        [Header("Range")]
        [SerializeField] private float engageRange = 14f;
        [SerializeField] private float preferredRange = 5.5f;
        [SerializeField] private float retreatRange = 3f;
        [SerializeField] private float retreatSpeedMultiplier = 0.75f;

        [Header("Timing")]
        [SerializeField] private float initialPatternDelay = 2.2f;
        [SerializeField] private float patternCooldown = 1.15f;
        [SerializeField] private float windUpTime = 0.45f;
        [SerializeField] private float recoveryTime = 0.45f;
        [SerializeField] private bool enforceReadableTiming = true;
        [SerializeField] private float minimumPatternCooldown = 1.65f;
        [SerializeField] private float minimumWindUpTime = 0.8f;

        [Header("Pattern Sequence")]
        [SerializeField] private BossPatternType[] patternSequence =
        {
            BossPatternType.CombinedShot,
            BossPatternType.AimedBurst,
            BossPatternType.RadialBurst,
            BossPatternType.Spiral,
            BossPatternType.Dash
        };

        [Header("Projectiles")]
        [SerializeField] private PoolKey projectilePoolKey;
        [SerializeField] private EnemyProjectileDefinition projectileDefinition;
        [SerializeField] private int aimedProjectileCount = 3;
        [SerializeField] private float aimedSpreadAngle = 22f;
        [SerializeField] private int radialProjectileCount = 8;
        [SerializeField] private float radialAngleOffsetPerPattern = 11f;
        [SerializeField] private float muzzleForwardOffset = 0.65f;

        [Header("Warning Telegraph")]
        [SerializeField] private bool showPatternTelegraph = true;
        [SerializeField] private float telegraphLineLength = 12f;
        [SerializeField] private float telegraphLineWidth = 0.07f;
        [SerializeField] private Color telegraphLineStartColor = new Color(1f, 0.08f, 0.24f, 0.98f);
        [SerializeField] private Color telegraphLineEndColor = new Color(1f, 0.08f, 0.24f, 0.08f);
        [SerializeField] private int telegraphLineSortingOrder = 36;
        [SerializeField] private Material telegraphLineMaterial;

        [Header("Aimed Burst")]
        [SerializeField] private int aimedBurstShotCount = 3;
        [SerializeField] private float aimedBurstShotInterval = 0.12f;
        [SerializeField] private float aimedBurstSpreadAngle = 10f;

        [Header("Spiral")]
        [SerializeField] private int spiralArmCount = 2;
        [SerializeField] private int spiralShotCount = 7;
        [SerializeField] private float spiralShotInterval = 0.08f;
        [SerializeField] private float spiralAngleStep = 18f;

        [Header("Dash Pattern")]
        [SerializeField] private int dashEveryNthPattern = 3;
        [SerializeField] private float dashPrepareTime = 0.25f;
        [SerializeField] private float dashSpeed = 8f;
        [SerializeField] private float dashDuration = 0.45f;
        [SerializeField] private float contactDamage = 20f;
        [SerializeField] private float contactKnockback = 10f;
        [SerializeField] private float contactKnockbackDuration = 0.08f;
        [SerializeField] private float contactCheckRadius = 0.7f;
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private LayerMask wallLayers = Physics2D.DefaultRaycastLayers;

        [Header("Phase 2")]
        [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoHealthRatio = 0.5f;
        [SerializeField] private float phaseTwoCooldownMultiplier = 0.72f;
        [SerializeField] private int phaseTwoExtraAimedProjectiles = 2;
        [SerializeField] private int phaseTwoExtraRadialProjectiles = 4;
        [SerializeField] private int phaseTwoExtraBurstShots = 1;
        [SerializeField] private int phaseTwoExtraSpiralShots = 3;
        [SerializeField] private float phaseTwoDashSpeedMultiplier = 1.2f;

        public float EngageRange => Mathf.Max(0f, engageRange);
        public float PreferredRange => Mathf.Max(0f, preferredRange);
        public float RetreatRange => Mathf.Max(0f, retreatRange);
        public float RetreatSpeedMultiplier => Mathf.Max(0f, retreatSpeedMultiplier);
        public float InitialPatternDelay => Mathf.Max(0f, initialPatternDelay);
        public float PatternCooldown => Mathf.Max(0f, enforceReadableTiming ? Mathf.Max(patternCooldown, minimumPatternCooldown) : patternCooldown);
        public float WindUpTime => Mathf.Max(0f, enforceReadableTiming ? Mathf.Max(windUpTime, minimumWindUpTime) : windUpTime);
        public float RecoveryTime => Mathf.Max(0f, recoveryTime);
        public BossPatternType[] PatternSequence => patternSequence;
        public PoolKey ProjectilePoolKey => projectilePoolKey;
        public EnemyProjectileDefinition ProjectileDefinition => projectileDefinition;
        public int AimedProjectileCount => Mathf.Max(0, aimedProjectileCount);
        public float AimedSpreadAngle => Mathf.Max(0f, aimedSpreadAngle);
        public int RadialProjectileCount => Mathf.Max(0, radialProjectileCount);
        public float RadialAngleOffsetPerPattern => radialAngleOffsetPerPattern;
        public float MuzzleForwardOffset => Mathf.Max(0f, muzzleForwardOffset);
        public bool ShowPatternTelegraph => showPatternTelegraph;
        public float TelegraphLineLength => Mathf.Max(0f, telegraphLineLength);
        public float TelegraphLineWidth => Mathf.Max(0f, telegraphLineWidth);
        public Color TelegraphLineStartColor => telegraphLineStartColor;
        public Color TelegraphLineEndColor => telegraphLineEndColor;
        public int TelegraphLineSortingOrder => telegraphLineSortingOrder;
        public Material TelegraphLineMaterial => telegraphLineMaterial;
        public int AimedBurstShotCount => Mathf.Max(0, aimedBurstShotCount);
        public float AimedBurstShotInterval => Mathf.Max(0f, aimedBurstShotInterval);
        public float AimedBurstSpreadAngle => Mathf.Max(0f, aimedBurstSpreadAngle);
        public int SpiralArmCount => Mathf.Max(1, spiralArmCount);
        public int SpiralShotCount => Mathf.Max(0, spiralShotCount);
        public float SpiralShotInterval => Mathf.Max(0f, spiralShotInterval);
        public float SpiralAngleStep => spiralAngleStep;
        public int DashEveryNthPattern => Mathf.Max(0, dashEveryNthPattern);
        public float DashPrepareTime => Mathf.Max(0f, dashPrepareTime);
        public float DashSpeed => Mathf.Max(0f, dashSpeed);
        public float DashDuration => Mathf.Max(0.05f, dashDuration);
        public float ContactDamage => Mathf.Max(0f, contactDamage);
        public float ContactKnockback => Mathf.Max(0f, contactKnockback);
        public float ContactKnockbackDuration => Mathf.Max(0f, contactKnockbackDuration);
        public float ContactCheckRadius => Mathf.Max(0.01f, contactCheckRadius);
        public LayerMask HitLayers => hitLayers;
        public LayerMask WallLayers => wallLayers;
        public float PhaseTwoHealthRatio => phaseTwoHealthRatio;
        public float PhaseTwoCooldownMultiplier => Mathf.Max(0.05f, phaseTwoCooldownMultiplier);
        public int PhaseTwoExtraAimedProjectiles => Mathf.Max(0, phaseTwoExtraAimedProjectiles);
        public int PhaseTwoExtraRadialProjectiles => Mathf.Max(0, phaseTwoExtraRadialProjectiles);
        public int PhaseTwoExtraBurstShots => Mathf.Max(0, phaseTwoExtraBurstShots);
        public int PhaseTwoExtraSpiralShots => Mathf.Max(0, phaseTwoExtraSpiralShots);
        public float PhaseTwoDashSpeedMultiplier => Mathf.Max(0.05f, phaseTwoDashSpeedMultiplier);
    }
}
