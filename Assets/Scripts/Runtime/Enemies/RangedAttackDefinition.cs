using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Behaviors/Ranged Attack Definition")]
    public sealed class RangedAttackDefinition : ScriptableObject
    {
        [Header("Projectile")]
        [SerializeField] private PoolKey projectilePoolKey;
        [SerializeField] private EnemyProjectileDefinition projectileDefinition;

        [Header("Range")]
        [SerializeField] private float fireRange = 8f;
        [SerializeField] private float preferredRange = 5.5f;
        [SerializeField] private float retreatRange = 3f;
        [SerializeField] private float retreatSpeedMultiplier = 0.85f;

        [Header("Timing")]
        [SerializeField] private float cooldown = 1.4f;
        [SerializeField] private float windUpTime = 0.35f;
        [SerializeField] private float recoveryTime = 0.25f;

        [Header("Pattern")]
        [SerializeField] private int projectileCount = 1;
        [SerializeField] private float spreadAngle = 0f;
        [SerializeField] private float muzzleForwardOffset = 0.45f;

        [Header("Line Of Sight")]
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask lineOfSightBlockers;

        public PoolKey ProjectilePoolKey => projectilePoolKey;
        public EnemyProjectileDefinition ProjectileDefinition => projectileDefinition;
        public float FireRange => Mathf.Max(0f, fireRange);
        public float PreferredRange => Mathf.Max(0f, preferredRange);
        public float RetreatRange => Mathf.Max(0f, retreatRange);
        public float RetreatSpeedMultiplier => Mathf.Max(0f, retreatSpeedMultiplier);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float WindUpTime => Mathf.Max(0f, windUpTime);
        public float RecoveryTime => Mathf.Max(0f, recoveryTime);
        public int ProjectileCount => Mathf.Max(1, projectileCount);
        public float SpreadAngle => Mathf.Max(0f, spreadAngle);
        public float MuzzleForwardOffset => Mathf.Max(0f, muzzleForwardOffset);
        public bool RequireLineOfSight => requireLineOfSight;
        public LayerMask LineOfSightBlockers => lineOfSightBlockers;
    }
}
