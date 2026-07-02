using System;
using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    public sealed class MeleeAttack2D : MonoBehaviour
    {
        [SerializeField] private Transform attackOrigin = null;
        [SerializeField] private MeleeAttackDefinition definition = null;
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float attackRange = 1.35f;
        [SerializeField, Range(1f, 360f)] private float attackAngle = 85f;
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField] private float knockbackDuration = 0.08f;
        [SerializeField] private int maxHits = 24;
        [SerializeField] private bool drawDebugAttack = true;
        [SerializeField] private bool logAttackResult = true;
        [SerializeField] private bool playCombatFeedback = true;
        [SerializeField] private float hitStopDuration = 0.04f;
        [SerializeField] private float criticalHitStopDuration = 0.06f;
        [SerializeField] private float cameraShakeDuration = 0.06f;
        [SerializeField] private float cameraShakeStrength = 0.08f;
        [SerializeField] private float criticalCameraShakeStrength = 0.12f;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] hitBuffer;
        private PlayerStats stats;
        private float cooldownTimer;

        public event Action AttackStarted;
        public event Action<int> AttackHit;

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public Vector3 AttackOriginPosition => attackOrigin != null ? attackOrigin.position : transform.position;
        public float BaseAttackRange => attackRange;
        public float EffectiveAttackRange => stats != null ? stats.GetAttackRange(attackRange) : attackRange;
        public float EffectiveAttackAngle => stats != null ? stats.GetAttackAngle(attackAngle) : attackAngle;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            hitBuffer = new Collider2D[Mathf.Max(1, maxHits)];
            stats = GetComponentInParent<PlayerStats>();
            ApplyDefinition();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

        }

        public void Configure(MeleeAttackDefinition newDefinition)
        {
            definition = newDefinition;
            ApplyDefinition();
        }

        public bool TryAttack(Vector2 direction)
        {
            if (cooldownTimer > 0f)
            {
                return false;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            cooldownTimer = stats != null ? stats.GetAttackCooldown(attackCooldown) : attackCooldown;
            AttackStarted?.Invoke();

            int hitCount = PerformAttack(direction.normalized);
            AttackHit?.Invoke(hitCount);

            if (logAttackResult)
            {
                Debug.Log($"[MeleeAttack2D] Attack fired. Hit Count: {hitCount}", this);
            }

            return true;
        }

        private int PerformAttack(Vector2 direction)
        {
            damagedTargets.Clear();

            Vector2 origin = attackOrigin.position;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(hitLayers);
            filter.useTriggers = Physics2D.queriesHitTriggers;

            float effectiveRange = stats != null ? stats.GetAttackRange(attackRange) : attackRange;
            float effectiveAngle = stats != null ? stats.GetAttackAngle(attackAngle) : attackAngle;
            float effectiveKnockback = stats != null ? stats.GetKnockback(knockbackForce) : knockbackForce;
            int count = Physics2D.OverlapCircle(origin, effectiveRange, filter, hitBuffer);
            int successfulHits = 0;
            bool anyCritical = false;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector2 targetPoint = hit.ClosestPoint(origin);
                Vector2 toTarget = targetPoint - origin;
                if (!IsInsideAttackCone(direction, toTarget, effectiveAngle))
                {
                    continue;
                }

                IDamageable damageable = FindComponentInParents<IDamageable>(hit);
                if (damageable == null || !damageable.CanTakeDamage || damagedTargets.Contains(damageable))
                {
                    continue;
                }

                damagedTargets.Add(damageable);

                Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction;
                bool isCritical = false;
                float finalDamage = stats != null ? stats.RollAttackDamage(damage, out isCritical) : damage;
                DamageInfo damageInfo = new DamageInfo(
                    finalDamage,
                    targetPoint,
                    hitDirection,
                    effectiveKnockback,
                    isCritical,
                    gameObject,
                    DamageSourceType.BasicAttack);
                anyCritical |= isCritical;

                damageable.TakeDamage(damageInfo);
                stats?.NotifyDamageDealt(finalDamage);

                IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                knockbackReceiver?.ApplyKnockback(hitDirection, effectiveKnockback, knockbackDuration);

                successfulHits++;
            }

            if (drawDebugAttack)
            {
                DrawAttackDebug(origin, direction, effectiveRange, effectiveAngle, successfulHits > 0);
            }

            if (successfulHits > 0 && playCombatFeedback)
            {
                PlayFeedback(anyCritical);
            }

            return successfulHits;
        }

        private void PlayFeedback(bool isCritical)
        {
            HitStop2D.Play(isCritical ? criticalHitStopDuration : hitStopDuration);
            CameraShake2D.Shake(cameraShakeDuration, isCritical ? criticalCameraShakeStrength : cameraShakeStrength);
        }

        private bool IsInsideAttackCone(Vector2 direction, Vector2 toTarget, float effectiveAngle)
        {
            if (toTarget.sqrMagnitude <= 0.01f || effectiveAngle >= 359f)
            {
                return true;
            }

            float angle = Vector2.Angle(direction, toTarget.normalized);
            return angle <= effectiveAngle * 0.5f;
        }

        private static T FindComponentInParents<T>(Collider2D source) where T : class
        {
            MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T component)
                {
                    return component;
                }
            }

            return null;
        }

        private void ApplyDefinition()
        {
            if (definition == null)
            {
                return;
            }

            damage = definition.Damage;
            attackCooldown = definition.Cooldown;
            attackRange = definition.Range;
            attackAngle = definition.Angle;
            knockbackForce = definition.KnockbackForce;
            knockbackDuration = definition.KnockbackDuration;
            hitLayers = definition.TargetLayers;
        }

        private void DrawAttackDebug(Vector2 origin, Vector2 direction, float range, float effectiveAngle, bool didHit)
        {
            Color color = didHit ? Color.green : Color.red;
            Vector2 left = Quaternion.Euler(0f, 0f, effectiveAngle * 0.5f) * direction;
            Vector2 right = Quaternion.Euler(0f, 0f, -effectiveAngle * 0.5f) * direction;

            Debug.DrawLine(origin, origin + direction.normalized * range, color, 0.18f);
            Debug.DrawLine(origin, origin + left.normalized * range, color, 0.18f);
            Debug.DrawLine(origin, origin + right.normalized * range, color, 0.18f);
        }

        private void OnDrawGizmosSelected()
        {
            Transform originTransform = attackOrigin != null ? attackOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector2 forward = originTransform.right;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, attackRange);

            Vector2 left = Quaternion.Euler(0f, 0f, attackAngle * 0.5f) * forward;
            Vector2 right = Quaternion.Euler(0f, 0f, -attackAngle * 0.5f) * forward;
            Gizmos.DrawLine(origin, origin + (Vector3)(left.normalized * attackRange));
            Gizmos.DrawLine(origin, origin + (Vector3)(right.normalized * attackRange));
        }
    }
}
