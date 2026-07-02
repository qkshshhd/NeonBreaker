using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerDash2D))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerDashShockwave2D : MonoBehaviour
    {
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float baseDamage = 12f;
        [SerializeField] private float radius = 1.25f;
        [SerializeField] private float knockbackForce = 5f;
        [SerializeField] private float knockbackDuration = 0.06f;
        [SerializeField] private int maxHits = 24;
        [SerializeField] private bool drawDebugShockwave = true;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] hitBuffer;
        private PlayerDash2D dash;
        private PlayerStats stats;

        private void Awake()
        {
            dash = GetComponent<PlayerDash2D>();
            stats = GetComponent<PlayerStats>();
            hitBuffer = new Collider2D[Mathf.Max(1, maxHits)];
        }

        private void OnEnable()
        {
            dash.DashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            dash.DashEnded -= HandleDashEnded;
        }

        private void HandleDashEnded()
        {
            if (stats == null || stats.DashShockwaveLevel <= 0)
            {
                return;
            }

            PerformShockwave();
        }

        private void PerformShockwave()
        {
            damagedTargets.Clear();

            Vector2 origin = transform.position;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(hitLayers);
            filter.useTriggers = Physics2D.queriesHitTriggers;

            float effectiveRadius = stats.GetDashShockwaveRadius(radius);
            float effectiveKnockback = stats.GetKnockback(knockbackForce);
            int count = Physics2D.OverlapCircle(origin, effectiveRadius, filter, hitBuffer);
            float shockwaveDamage = stats.GetDashShockwaveDamage(baseDamage);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = FindComponentInParents<IDamageable>(hit);
                if (damageable == null || !damageable.CanTakeDamage || damagedTargets.Contains(damageable))
                {
                    continue;
                }

                damagedTargets.Add(damageable);

                Vector2 targetPoint = hit.ClosestPoint(origin);
                Vector2 direction = targetPoint - origin;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = transform.right;
                }

                direction.Normalize();
                DamageInfo damageInfo = new DamageInfo(
                    shockwaveDamage,
                    targetPoint,
                    direction,
                    effectiveKnockback,
                    false,
                    gameObject,
                    DamageSourceType.Skill);

                damageable.TakeDamage(damageInfo);
                stats.NotifyDamageDealt(shockwaveDamage);

                IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                knockbackReceiver?.ApplyKnockback(direction, effectiveKnockback, knockbackDuration);
            }

            if (drawDebugShockwave)
            {
                Debug.DrawLine(origin + Vector2.left * effectiveRadius, origin + Vector2.right * effectiveRadius, Color.cyan, 0.2f);
                Debug.DrawLine(origin + Vector2.down * effectiveRadius, origin + Vector2.up * effectiveRadius, Color.cyan, 0.2f);
            }
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
    }
}
