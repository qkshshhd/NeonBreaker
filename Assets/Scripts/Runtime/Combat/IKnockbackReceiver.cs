using UnityEngine;

namespace NeonBreaker.Combat
{
    public interface IKnockbackReceiver
    {
        void ApplyKnockback(Vector2 direction, float force, float duration);
    }
}

