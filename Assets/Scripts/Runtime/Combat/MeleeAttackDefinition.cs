using UnityEngine;

namespace NeonBreaker.Combat
{
    [CreateAssetMenu(menuName = "Neon Breaker/Combat/Melee Attack Definition")]
    public sealed class MeleeAttackDefinition : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField] private float cooldown = 0.35f;
        [SerializeField] private float windUpTime = 0.08f;
        [SerializeField] private float recoveryTime = 0.14f;

        [Header("Hit")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 1.35f;
        [SerializeField, Range(1f, 360f)] private float angle = 85f;
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField] private float knockbackDuration = 0.08f;
        [SerializeField] private LayerMask targetLayers;

        public float Cooldown => cooldown;
        public float WindUpTime => windUpTime;
        public float RecoveryTime => recoveryTime;
        public float Damage => damage;
        public float Range => range;
        public float Angle => angle;
        public float KnockbackForce => knockbackForce;
        public float KnockbackDuration => knockbackDuration;
        public LayerMask TargetLayers => targetLayers;
    }
}

