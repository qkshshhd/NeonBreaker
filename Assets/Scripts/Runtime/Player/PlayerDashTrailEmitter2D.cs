using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerDash2D))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerDashTrailEmitter2D : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private PlayerStats stats;

        [Header("Trail")]
        [SerializeField] private bool emitTrail = true;
        [SerializeField, Min(0.03f)] private float minSegmentLength = 0.25f;
        [SerializeField, Min(0.05f)] private float segmentLifetime = 0.85f;
        [SerializeField, Min(0.01f)] private float segmentWidth = 0.16f;
        [SerializeField, Min(0.01f)] private float glowWidthMultiplier = 2.8f;
        [SerializeField, Range(0f, 1f)] private float glowAlphaMultiplier = 0.35f;
        [SerializeField] private Color startColor = new Color(0.25f, 1f, 0.95f, 0.78f);
        [SerializeField] private Color endColor = new Color(0.65f, 0.08f, 1f, 0.08f);
        [SerializeField] private int sortingOrder = 2;
        [SerializeField] private Material trailMaterial;

        [Header("Damage")]
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private bool dealDamage = true;
        [SerializeField] private bool hitSameTargetOncePerDash = true;
        [SerializeField, Min(0f)] private float damage = 4f;
        [SerializeField, Min(0f)] private float knockbackForce = 1.25f;
        [SerializeField, Min(0f)] private float knockbackDuration = 0.04f;
        [SerializeField, Min(0.02f)] private float damageInterval = 0.8f;
        [SerializeField] private bool scaleDamageWithPlayerStats = true;

        private Vector2 lastEmitPosition;
        private bool isEmitting;
        private Material runtimeMaterial;
        private Transform inactiveRoot;
        private readonly Queue<DashTrailHazard2D> inactiveSegments = new Queue<DashTrailHazard2D>();
        private readonly Dictionary<IDamageable, float> nextDamageTimes = new Dictionary<IDamageable, float>();

        private void Awake()
        {
            if (dash == null)
            {
                dash = GetComponent<PlayerDash2D>();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            CreateInactiveRoot();
        }

        private void OnEnable()
        {
            if (dash == null)
            {
                return;
            }

            dash.DashStarted += HandleDashStarted;
            dash.DashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            if (dash == null)
            {
                return;
            }

            dash.DashStarted -= HandleDashStarted;
            dash.DashEnded -= HandleDashEnded;
        }

        private void FixedUpdate()
        {
            if (!emitTrail || !isEmitting || dash == null || !dash.IsDashing)
            {
                return;
            }

            Vector2 currentPosition = transform.position;
            if (Vector2.Distance(lastEmitPosition, currentPosition) < minSegmentLength)
            {
                return;
            }

            EmitSegment(lastEmitPosition, currentPosition);
            lastEmitPosition = currentPosition;
        }

        private void HandleDashStarted()
        {
            if (!emitTrail)
            {
                return;
            }

            isEmitting = true;
            lastEmitPosition = transform.position;
            nextDamageTimes.Clear();
        }

        private void HandleDashEnded()
        {
            if (!isEmitting)
            {
                return;
            }

            Vector2 currentPosition = transform.position;
            if (Vector2.Distance(lastEmitPosition, currentPosition) >= 0.05f)
            {
                EmitSegment(lastEmitPosition, currentPosition);
            }

            isEmitting = false;
        }

        private void EmitSegment(Vector2 start, Vector2 end)
        {
            if ((end - start).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            DashTrailHazard2D hazard = GetSegment();
            hazard.Initialize(
                start,
                end,
                segmentWidth,
                segmentLifetime,
                GetEffectiveDamage(),
                knockbackForce,
                knockbackDuration,
                damageInterval,
                hitLayers,
                gameObject,
                stats,
                this,
                GetTrailMaterial(),
                startColor,
                endColor,
                glowWidthMultiplier,
                glowAlphaMultiplier,
                sortingOrder,
                RecycleSegment);
        }

        public bool TryConsumeTrailDamage(IDamageable damageable)
        {
            if (!dealDamage || damageable == null)
            {
                return false;
            }

            if (hitSameTargetOncePerDash && nextDamageTimes.ContainsKey(damageable))
            {
                return false;
            }

            if (!hitSameTargetOncePerDash
                && nextDamageTimes.TryGetValue(damageable, out float nextTime)
                && Time.time < nextTime)
            {
                return false;
            }

            nextDamageTimes[damageable] = Time.time + damageInterval;
            return true;
        }

        private float GetEffectiveDamage()
        {
            return scaleDamageWithPlayerStats && stats != null
                ? stats.GetDamage(damage)
                : damage;
        }

        private Material GetTrailMaterial()
        {
            if (trailMaterial != null)
            {
                return trailMaterial;
            }

            if (runtimeMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                runtimeMaterial = shader != null ? new Material(shader) : null;
            }

            return runtimeMaterial;
        }

        private DashTrailHazard2D GetSegment()
        {
            DashTrailHazard2D segment = inactiveSegments.Count > 0
                ? inactiveSegments.Dequeue()
                : CreateSegment();

            segment.transform.SetParent(null, true);
            segment.gameObject.SetActive(true);
            return segment;
        }

        private DashTrailHazard2D CreateSegment()
        {
            GameObject segmentObject = new GameObject("Dash Trail Segment");
            return segmentObject.AddComponent<DashTrailHazard2D>();
        }

        private void RecycleSegment(DashTrailHazard2D segment)
        {
            if (segment == null)
            {
                return;
            }

            CreateInactiveRoot();
            segment.gameObject.SetActive(false);
            segment.transform.SetParent(inactiveRoot, false);
            inactiveSegments.Enqueue(segment);
        }

        private void CreateInactiveRoot()
        {
            if (inactiveRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("Dash Trail Segment Pool");
            root.transform.SetParent(transform, false);
            inactiveRoot = root.transform;
        }
    }
}
