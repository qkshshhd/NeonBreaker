using UnityEngine;

namespace NeonBreaker.Enemies
{
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Projectiles/Enemy Projectile Definition")]
    public sealed class EnemyProjectileDefinition : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float speed = 7f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private bool rotateToDirection = true;

        [Header("Hit")]
        [SerializeField] private float damage = 12f;
        [SerializeField] private float knockback = 4f;
        [SerializeField] private float knockbackDuration = 0.07f;
        [SerializeField] private float hitRadius = 0.18f;
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private LayerMask blockerLayers;
        [SerializeField] private bool despawnOnBlocker = true;
        [SerializeField] private bool despawnOnHit = true;

        public float Speed => Mathf.Max(0f, speed);
        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public bool RotateToDirection => rotateToDirection;
        public float Damage => Mathf.Max(0f, damage);
        public float Knockback => Mathf.Max(0f, knockback);
        public float KnockbackDuration => Mathf.Max(0f, knockbackDuration);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public LayerMask HitLayers => hitLayers;
        public LayerMask BlockerLayers => blockerLayers;
        public bool DespawnOnBlocker => despawnOnBlocker;
        public bool DespawnOnHit => despawnOnHit;
    }
}
