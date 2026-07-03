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

            public string DisplayName => displayName;
            public int AnimationIndex => animationIndex;
            public float Cooldown => cooldown;
            public float RecoveryTime => recoveryTime;
            public float DamageMultiplier => damageMultiplier;
            public float RangeMultiplier => rangeMultiplier;
            public float Angle => angle;
            public float KnockbackMultiplier => knockbackMultiplier;
            public HitWindow[] HitWindows => hitWindows;

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
