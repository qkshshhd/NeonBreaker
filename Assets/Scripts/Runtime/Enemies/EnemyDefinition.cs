using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 60f;
        [SerializeField] private float hitInvulnerabilityDuration = 0.05f;

        [Header("Perception")]
        [SerializeField] private float detectionRange = 12f;
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private string targetTag = "Player";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float deceleration = 60f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Reactions")]
        [SerializeField] private float hitStunDuration = 0.12f;

        [Header("Combat")]
        [SerializeField] private MeleeAttackDefinition attack;

        public float MaxHealth => maxHealth;
        public float HitInvulnerabilityDuration => hitInvulnerabilityDuration;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public string TargetTag => targetTag;
        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float RotationSpeed => rotationSpeed;
        public float HitStunDuration => hitStunDuration;
        public MeleeAttackDefinition Attack => attack;
    }
}
