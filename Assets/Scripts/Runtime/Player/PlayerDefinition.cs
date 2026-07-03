using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [CreateAssetMenu(menuName = "Neon Breaker/Player/Player Definition")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float hitInvulnerabilityDuration = 0.5f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float acceleration = 80f;
        [SerializeField] private float deceleration = 100f;
        [SerializeField] private float rotationSpeed = 30f;

        [Header("Dash")]
        [SerializeField] private float dashDistance = 4f;
        [SerializeField] private float dashDuration = 0.12f;
        [SerializeField] private float dashCooldown = 0.55f;
        [SerializeField] private bool invulnerableDuringDash = true;

        [Header("Combat")]
        [SerializeField] private MeleeAttackDefinition basicAttack;
        [SerializeField] private MeleeComboDefinition basicAttackCombo;
        [SerializeField] private bool attackWhileHeld = true;

        public float MaxHealth => maxHealth;
        public float HitInvulnerabilityDuration => hitInvulnerabilityDuration;
        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float RotationSpeed => rotationSpeed;
        public float DashDistance => dashDistance;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public bool InvulnerableDuringDash => invulnerableDuringDash;
        public MeleeAttackDefinition BasicAttack => basicAttack;
        public MeleeComboDefinition BasicAttackCombo => basicAttackCombo;
        public bool AttackWhileHeld => attackWhileHeld;
    }
}
