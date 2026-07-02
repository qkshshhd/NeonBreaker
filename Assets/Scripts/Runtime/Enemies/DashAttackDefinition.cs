using UnityEngine;

namespace NeonBreaker.Enemies
{
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Behaviors/Dash Attack Definition")]
    public class DashAttackDefinition : ScriptableObject
    {
        [Header("Range")]
        [SerializeField] private float prepareRange = 7f;
        [SerializeField] private float minPrepareRange = 2.2f;
        [SerializeField] private bool retreatWhenTooClose = true;
        [SerializeField] private float retreatSpeedMultiplier = 0.75f;

        [Header("Timing")]
        [SerializeField] private float prepareDuration = 0.65f;
        [SerializeField] private float dashDuration = 0.45f;
        [SerializeField] private float recoveryDuration = 0.55f;
        [SerializeField] private float wallStunDuration = 0.8f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 9.5f;
        [SerializeField] private LayerMask wallLayers = Physics2D.DefaultRaycastLayers;

        [Header("Dash Collision")]
        [SerializeField] private bool ignoreOtherEnemiesWhileDashing = true;
        [SerializeField] private float enemyCollisionIgnoreRadius = 2.5f;
        [SerializeField] private LayerMask enemyCollisionIgnoreLayers = Physics2D.DefaultRaycastLayers;

        [Header("Contact Attack")]
        [SerializeField] private float contactDamage = 18f;
        [SerializeField] private float contactKnockback = 10f;
        [SerializeField] private float contactKnockbackDuration = 0.08f;
        [SerializeField] private float contactCheckRadius = 0.55f;
        [SerializeField] private float contactCheckForwardOffset = 0.35f;
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;

        [Header("Warning Line")]
        [SerializeField] private float warningLineLength = 7f;
        [SerializeField] private float warningLineWidth = 0.08f;
        [SerializeField] private Color warningLineStartColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color warningLineEndColor = new Color(1f, 0.2f, 0.2f, 0.15f);
        [SerializeField] private int warningLineSortingOrder = 30;
        [SerializeField] private Material warningLineMaterial;

        public float PrepareRange => Mathf.Max(0f, prepareRange);
        public float MinPrepareRange => Mathf.Max(0f, minPrepareRange);
        public bool RetreatWhenTooClose => retreatWhenTooClose;
        public float RetreatSpeedMultiplier => Mathf.Max(0f, retreatSpeedMultiplier);
        public float PrepareDuration => Mathf.Max(0f, prepareDuration);
        public float DashDuration => Mathf.Max(0.05f, dashDuration);
        public float RecoveryDuration => Mathf.Max(0f, recoveryDuration);
        public float WallStunDuration => Mathf.Max(0f, wallStunDuration);
        public float DashSpeed => Mathf.Max(0f, dashSpeed);
        public LayerMask WallLayers => wallLayers;
        public bool IgnoreOtherEnemiesWhileDashing => ignoreOtherEnemiesWhileDashing;
        public float EnemyCollisionIgnoreRadius => Mathf.Max(0f, enemyCollisionIgnoreRadius);
        public LayerMask EnemyCollisionIgnoreLayers => enemyCollisionIgnoreLayers;
        public float ContactDamage => Mathf.Max(0f, contactDamage);
        public float ContactKnockback => Mathf.Max(0f, contactKnockback);
        public float ContactKnockbackDuration => Mathf.Max(0f, contactKnockbackDuration);
        public float ContactCheckRadius => Mathf.Max(0.01f, contactCheckRadius);
        public float ContactCheckForwardOffset => Mathf.Max(0f, contactCheckForwardOffset);
        public LayerMask HitLayers => hitLayers;
        public float WarningLineLength => Mathf.Max(0f, warningLineLength);
        public float WarningLineWidth => Mathf.Max(0f, warningLineWidth);
        public Color WarningLineStartColor => warningLineStartColor;
        public Color WarningLineEndColor => warningLineEndColor;
        public int WarningLineSortingOrder => warningLineSortingOrder;
        public Material WarningLineMaterial => warningLineMaterial;
    }
}
