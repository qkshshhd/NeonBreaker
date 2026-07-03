using System;
using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DashTrailHazard2D : MonoBehaviour
    {
        private LineRenderer glowRenderer;
        private LineRenderer coreRenderer;
        private readonly Dictionary<IDamageable, float> localNextDamageTimes = new Dictionary<IDamageable, float>();
        private BoxCollider2D hitbox;
        private Rigidbody2D body;
        private LayerMask hitLayers;
        private GameObject owner;
        private PlayerStats ownerStats;
        private PlayerDashTrailEmitter2D ownerEmitter;
        private Vector2 start;
        private Vector2 end;
        private Vector2 direction;
        private float damage;
        private float knockbackForce;
        private float knockbackDuration;
        private float damageInterval;
        private float lifetime;
        private float timer;
        private Color startColor;
        private Color endColor;
        private float glowAlphaMultiplier = 0.35f;
        private Action<DashTrailHazard2D> release;

        public void Initialize(
            Vector2 start,
            Vector2 end,
            float width,
            float lifetime,
            float damage,
            float knockbackForce,
            float knockbackDuration,
            float damageInterval,
            LayerMask hitLayers,
            GameObject owner,
            PlayerStats ownerStats,
            PlayerDashTrailEmitter2D ownerEmitter,
            Material material,
            Color startColor,
            Color endColor,
            float glowWidthMultiplier,
            float glowAlphaMultiplier,
            int sortingOrder,
            Action<DashTrailHazard2D> release)
        {
            this.start = start;
            this.end = end;
            this.direction = (end - start).sqrMagnitude > 0.0001f ? (end - start).normalized : Vector2.right;
            this.lifetime = Mathf.Max(0.05f, lifetime);
            this.damage = Mathf.Max(0f, damage);
            this.knockbackForce = Mathf.Max(0f, knockbackForce);
            this.knockbackDuration = Mathf.Max(0f, knockbackDuration);
            this.damageInterval = Mathf.Max(0.02f, damageInterval);
            this.hitLayers = hitLayers;
            this.owner = owner;
            this.ownerStats = ownerStats;
            this.ownerEmitter = ownerEmitter;
            this.startColor = startColor;
            this.endColor = endColor;
            this.glowAlphaMultiplier = Mathf.Clamp01(glowAlphaMultiplier);
            this.release = release;
            this.timer = 0f;
            localNextDamageTimes.Clear();

            EnsureComponents();
            ConfigureTransformAndCollider(width);
            ConfigureLine(width, Mathf.Max(1f, glowWidthMultiplier), Mathf.Clamp01(glowAlphaMultiplier), material, sortingOrder);
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / Mathf.Max(0.01f, lifetime));
            UpdateFade(normalized);

            if (timer >= lifetime)
            {
                Release();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void EnsureComponents()
        {
            if (glowRenderer == null)
            {
                glowRenderer = GetComponent<LineRenderer>();
            }

            if (coreRenderer == null)
            {
                Transform coreTransform = transform.Find("Core Line");
                GameObject coreObject = coreTransform != null
                    ? coreTransform.gameObject
                    : new GameObject("Core Line");

                coreObject.transform.SetParent(transform, false);
                coreRenderer = coreObject.GetComponent<LineRenderer>();
                if (coreRenderer == null)
                {
                    coreRenderer = coreObject.AddComponent<LineRenderer>();
                }
            }

            if (hitbox == null)
            {
                hitbox = GetComponent<BoxCollider2D>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.simulated = true;
                body.gravityScale = 0f;
            }
        }

        private void ConfigureTransformAndCollider(float width)
        {
            Vector2 segment = end - start;
            float length = Mathf.Max(0.01f, segment.magnitude);
            Vector2 center = (start + end) * 0.5f;
            float angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;

            transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));

            hitbox.isTrigger = true;
            hitbox.offset = Vector2.zero;
            hitbox.size = new Vector2(length, Mathf.Max(0.01f, width));
        }

        private void ConfigureLine(
            float width,
            float glowWidthMultiplier,
            float glowAlphaMultiplier,
            Material material,
            int sortingOrder)
        {
            ConfigureRenderer(glowRenderer, width * glowWidthMultiplier, material, sortingOrder, GetGlowColor(startColor, glowAlphaMultiplier), GetGlowColor(endColor, glowAlphaMultiplier));

            Color coreStart = Color.Lerp(startColor, Color.white, 0.55f);
            Color coreEnd = Color.Lerp(endColor, startColor, 0.35f);
            coreStart.a = Mathf.Clamp01(startColor.a);
            coreEnd.a = Mathf.Clamp01(endColor.a * 1.4f);
            ConfigureRenderer(coreRenderer, width, material, sortingOrder + 1, coreStart, coreEnd);
        }

        private void ConfigureRenderer(LineRenderer target, float width, Material material, int sortingOrder, Color rendererStartColor, Color rendererEndColor)
        {
            target.useWorldSpace = true;
            target.positionCount = 2;
            target.SetPosition(0, start);
            target.SetPosition(1, end);
            target.startWidth = width;
            target.endWidth = width * 0.75f;
            target.numCapVertices = 5;
            target.numCornerVertices = 3;
            target.sortingOrder = sortingOrder;
            target.material = material;
            target.startColor = rendererStartColor;
            target.endColor = rendererEndColor;
        }

        private void UpdateFade(float normalized)
        {
            float alpha = 1f - normalized;
            Color fadedStart = startColor;
            Color fadedEnd = endColor;
            fadedStart.a *= alpha;
            fadedEnd.a *= alpha;

            Color glowStart = GetGlowColor(fadedStart, glowAlphaMultiplier);
            Color glowEnd = GetGlowColor(fadedEnd, glowAlphaMultiplier);
            glowRenderer.startColor = glowStart;
            glowRenderer.endColor = glowEnd;

            Color coreStart = Color.Lerp(fadedStart, Color.white, 0.55f);
            Color coreEnd = Color.Lerp(fadedEnd, fadedStart, 0.35f);
            coreStart.a = fadedStart.a;
            coreEnd.a = Mathf.Clamp01(fadedEnd.a * 1.4f);
            coreRenderer.startColor = coreStart;
            coreRenderer.endColor = coreEnd;
        }

        private static Color GetGlowColor(Color source, float alphaMultiplier)
        {
            Color glow = source;
            glow.a *= alphaMultiplier;
            return glow;
        }

        private void Release()
        {
            release?.Invoke(this);
            if (release == null)
            {
                gameObject.SetActive(false);
            }
        }

        private void TryDamage(Collider2D other)
        {
            if (other == null || owner != null && other.transform.IsChildOf(owner.transform))
            {
                return;
            }

            if (((1 << other.gameObject.layer) & hitLayers.value) == 0)
            {
                return;
            }

            IDamageable damageable = FindComponentInParents<IDamageable>(other);
            if (damageable == null || !damageable.CanTakeDamage)
            {
                return;
            }

            if (ownerEmitter != null)
            {
                if (!ownerEmitter.TryConsumeTrailDamage(damageable))
                {
                    return;
                }
            }
            else
            {
                if (localNextDamageTimes.TryGetValue(damageable, out float nextTime) && Time.time < nextTime)
                {
                    return;
                }

                localNextDamageTimes[damageable] = Time.time + damageInterval;
            }

            Vector2 point = other.ClosestPoint(transform.position);
            Vector2 knockbackDirection = direction;
            if (knockbackDirection.sqrMagnitude <= 0.0001f)
            {
                knockbackDirection = Vector2.right;
            }

            DamageInfo damageInfo = new DamageInfo(
                damage,
                point,
                knockbackDirection,
                knockbackForce,
                false,
                owner,
                DamageSourceType.Dash);

            damageable.TakeDamage(damageInfo);
            ownerStats?.NotifyDamageDealt(damage);

            IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(other);
            knockbackReceiver?.ApplyKnockback(knockbackDirection, knockbackForce, knockbackDuration);
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
