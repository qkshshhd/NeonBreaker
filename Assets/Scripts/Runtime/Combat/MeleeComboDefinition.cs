using System;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [CreateAssetMenu(menuName = "Neon Breaker/Combat/Melee Combo Definition")]
    public sealed class MeleeComboDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class HitWindow
        {
            [SerializeField, Min(0f)] private float time = 0.06f;
            [SerializeField, Min(0f)] private float damageMultiplier = 1f;
            [SerializeField, Min(0.01f)] private float rangeMultiplier = 1f;
            [SerializeField, Range(1f, 360f)] private float angle = 85f;
            [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;
            [SerializeField] private bool clearHitTargetsBeforeThisHit;

            public float Time => time;
            public float DamageMultiplier => damageMultiplier;
            public float RangeMultiplier => rangeMultiplier;
            public float Angle => angle;
            public float KnockbackMultiplier => knockbackMultiplier;
            public bool ClearHitTargetsBeforeThisHit => clearHitTargetsBeforeThisHit;
        }

        [Serializable]
        public sealed class Step
        {
            [SerializeField] private string displayName = "Slash";
            [SerializeField, Min(0)] private int animationIndex;
            [SerializeField, Min(0.02f)] private float cooldown = 0.28f;
            [SerializeField, Min(0f)] private float recoveryTime = 0.1f;
            [SerializeField, Min(0f)] private float damageMultiplier = 1f;
            [SerializeField, Min(0.01f)] private float rangeMultiplier = 1f;
            [SerializeField, Range(1f, 360f)] private float angle = 85f;
            [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;
            [SerializeField] private HitWindow[] hitWindows;

            [Header("Movement During Attack")]
            [SerializeField, Range(0f, 1f)] private float startupMoveMultiplier = 0.18f;
            [SerializeField, Range(0f, 1f)] private float impactMoveMultiplier = 0.04f;
            [SerializeField, Range(0.01f, 0.95f)] private float impactNormalizedTime = 0.26f;
            [SerializeField, Range(0f, 1f)] private float recoveryMoveMultiplier = 0.58f;
            [SerializeField, Range(0f, 1f)] private float dashCancelNormalizedTime = 0.32f;
            [SerializeField] private AnimationCurve movementMultiplierCurve;

            public string DisplayName => displayName;
            public int AnimationIndex => animationIndex;
            public float Cooldown => cooldown;
            public float RecoveryTime => recoveryTime;
            public float DamageMultiplier => damageMultiplier;
            public float RangeMultiplier => rangeMultiplier;
            public float Angle => angle;
            public float KnockbackMultiplier => knockbackMultiplier;
            public HitWindow[] HitWindows => hitWindows;
            public float DashCancelNormalizedTime => dashCancelNormalizedTime;

            public float Duration
            {
                get
                {
                    float lastHitTime = 0f;
                    if (hitWindows != null)
                    {
                        for (int i = 0; i < hitWindows.Length; i++)
                        {
                            if (hitWindows[i] != null)
                            {
                                lastHitTime = Mathf.Max(lastHitTime, hitWindows[i].Time);
                            }
                        }
                    }

                    return Mathf.Max(cooldown, lastHitTime + recoveryTime);
                }
            }

            public float GetMovementMultiplier(float normalizedTime)
            {
                normalizedTime = Mathf.Clamp01(normalizedTime);

                if (movementMultiplierCurve != null && movementMultiplierCurve.length > 0)
                {
                    return Mathf.Clamp01(movementMultiplierCurve.Evaluate(normalizedTime));
                }

                float safeImpactTime = Mathf.Clamp(impactNormalizedTime, 0.01f, 0.95f);
                if (normalizedTime <= safeImpactTime)
                {
                    float t = Mathf.Clamp01(normalizedTime / safeImpactTime);
                    return Mathf.Lerp(startupMoveMultiplier, impactMoveMultiplier, t * t * (3f - 2f * t));
                }

                float recoveryT = Mathf.Clamp01((normalizedTime - safeImpactTime) / Mathf.Max(0.01f, 1f - safeImpactTime));
                return Mathf.Lerp(impactMoveMultiplier, recoveryMoveMultiplier, recoveryT * recoveryT * (3f - 2f * recoveryT));
            }
        }

        [Header("Combo")]
        [SerializeField, Min(0.05f)] private float resetWindow = 0.85f;
        [SerializeField] private bool loopCombo;
        [SerializeField] private Step[] steps;

        public float ResetWindow => resetWindow;
        public bool LoopCombo => loopCombo;
        public int StepCount => steps != null ? steps.Length : 0;

        public Step GetStep(int index)
        {
            if (steps == null || steps.Length == 0)
            {
                return null;
            }

            return steps[Mathf.Clamp(index, 0, steps.Length - 1)];
        }
    }
}
