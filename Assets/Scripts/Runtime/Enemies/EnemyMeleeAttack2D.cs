using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public sealed class EnemyMeleeAttack2D : MonoBehaviour
    {
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private int maxHits = 16;
        [SerializeField] private bool drawDebugAttack = true;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] hitBuffer;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            hitBuffer = new Collider2D[Mathf.Max(1, maxHits)];
        }

        public int Execute(MeleeAttackDefinition attackDefinition, Vector2 direction)
        {
            if (attackDefinition == null)
            {
                return 0;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.right;
            }

            damagedTargets.Clear();

            Vector2 origin = attackOrigin.position;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(attackDefinition.TargetLayers);
            filter.useTriggers = Physics2D.queriesHitTriggers;

            int count = Physics2D.OverlapCircle(origin, attackDefinition.Range, filter, hitBuffer);
            int successfulHits = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector2 targetPoint = hit.ClosestPoint(origin);
                Vector2 toTarget = targetPoint - origin;
                if (!IsInsideAttackCone(direction, toTarget, attackDefinition.Angle))
                {
                    continue;
                }

                IDamageable damageable = FindComponentInParents<IDamageable>(hit);
                if (damageable == null || !damageable.CanTakeDamage || damagedTargets.Contains(damageable))
                {
                    continue;
                }

                damagedTargets.Add(damageable);

                Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction.normalized;
                DamageInfo damageInfo = new DamageInfo(
                    attackDefinition.Damage,
                    targetPoint,
                    hitDirection,
                    attackDefinition.KnockbackForce,
                    false,
                    gameObject);

                damageable.TakeDamage(damageInfo);

                IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                knockbackReceiver?.ApplyKnockback(
                    hitDirection,
                    attackDefinition.KnockbackForce,
                    attackDefinition.KnockbackDuration);

                successfulHits++;
            }

            if (drawDebugAttack)
            {
                DrawAttackDebug(origin, direction.normalized, attackDefinition, successfulHits > 0);
            }

            return successfulHits;
        }

        private static bool IsInsideAttackCone(Vector2 direction, Vector2 toTarget, float angle)
        {
            if (toTarget.sqrMagnitude <= 0.01f || angle >= 359f)
            {
                return true;
            }

            return Vector2.Angle(direction.normalized, toTarget.normalized) <= angle * 0.5f;
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

        private static void DrawAttackDebug(Vector2 origin, Vector2 direction, MeleeAttackDefinition definition, bool didHit)
        {
            Color color = didHit ? Color.green : Color.red;
            Vector2 left = Quaternion.Euler(0f, 0f, definition.Angle * 0.5f) * direction;
            Vector2 right = Quaternion.Euler(0f, 0f, -definition.Angle * 0.5f) * direction;

            Debug.DrawLine(origin, origin + direction * definition.Range, color, 0.18f);
            Debug.DrawLine(origin, origin + left.normalized * definition.Range, color, 0.18f);
            Debug.DrawLine(origin, origin + right.normalized * definition.Range, color, 0.18f);
        }
    }
}

