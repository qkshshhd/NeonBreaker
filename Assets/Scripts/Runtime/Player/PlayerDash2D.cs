using System;
using System.Collections;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerDash2D : MonoBehaviour
    {
        [SerializeField] private float dashDistance = 4f;
        [SerializeField] private float dashDuration = 0.12f;
        [SerializeField] private float dashCooldown = 0.55f;
        [SerializeField] private bool invulnerableDuringDash = true;
        [SerializeField, Min(0f)] private float extraInvulnerabilityTime = 0.05f;

        private Rigidbody2D body;
        private Health health;
        private PlayerStats stats;
        private float cooldownTimer;
        private Coroutine dashRoutine;

        public event Action DashStarted;
        public event Action DashEnded;

        public bool IsDashing { get; private set; }
        public Vector2 LastDashDirection { get; private set; } = Vector2.right;
        public bool IsReady => !IsDashing && CooldownRemaining <= 0f;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public float CooldownNormalized
        {
            get
            {
                float effectiveCooldown = GetEffectiveCooldown();
                return effectiveCooldown <= 0f ? 0f : CooldownRemaining / effectiveCooldown;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

        }

        public void Configure(PlayerDefinition definition)
        {
            dashDistance = Mathf.Max(0.05f, definition.DashDistance);
            dashDuration = Mathf.Max(0.04f, definition.DashDuration);
            dashCooldown = Mathf.Max(0.02f, definition.DashCooldown);
            invulnerableDuringDash = definition.InvulnerableDuringDash;
        }

        public bool TryDash(Vector2 direction)
        {
            if (IsDashing || cooldownTimer > 0f || dashDuration <= 0f)
            {
                return false;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            dashRoutine = StartCoroutine(DashRoutine(direction.normalized));
            return true;
        }

        public void CancelDash()
        {
            bool wasDashing = IsDashing;

            if (dashRoutine != null)
            {
                StopCoroutine(dashRoutine);
                dashRoutine = null;
            }

            IsDashing = false;
            body.linearVelocity = Vector2.zero;

            if (wasDashing)
            {
                DashEnded?.Invoke();
            }
        }

        private IEnumerator DashRoutine(Vector2 direction)
        {
            LastDashDirection = direction;
            IsDashing = true;
            cooldownTimer = GetEffectiveCooldown();
            DashStarted?.Invoke();

            if (invulnerableDuringDash && health != null)
            {
                health.AddInvulnerability(dashDuration + extraInvulnerabilityTime);
            }

            float timer = 0f;
            float safeDuration = Mathf.Max(0.04f, dashDuration);
            float dashSpeed = GetEffectiveDistance() / safeDuration;

            while (timer < safeDuration)
            {
                body.linearVelocity = direction * dashSpeed;
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
            IsDashing = false;
            dashRoutine = null;
            DashEnded?.Invoke();
        }

        private float GetEffectiveCooldown()
        {
            return stats != null ? stats.GetDashCooldown(dashCooldown) : dashCooldown;
        }

        private float GetEffectiveDistance()
        {
            return stats != null ? stats.GetDashDistance(dashDistance) : dashDistance;
        }
    }
}
