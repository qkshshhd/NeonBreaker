using UnityEngine;

namespace NeonBreaker.Combat
{
    public enum DamageSourceType
    {
        Unknown,
        BasicAttack,
        Skill,
        Dash,
        Enemy
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector2 Point;
        public readonly Vector2 Direction;
        public readonly float Knockback;
        public readonly bool IsCritical;
        public readonly GameObject Source;
        public readonly DamageSourceType SourceType;

        public DamageInfo(
            float amount,
            Vector2 point,
            Vector2 direction,
            float knockback,
            bool isCritical,
            GameObject source,
            DamageSourceType sourceType = DamageSourceType.Unknown)
        {
            Amount = amount;
            Point = point;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
            Knockback = knockback;
            IsCritical = isCritical;
            Source = source;
            SourceType = sourceType;
        }
    }
}
