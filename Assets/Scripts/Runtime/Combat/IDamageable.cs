namespace NeonBreaker.Combat
{
    public interface IDamageable
    {
        bool CanTakeDamage { get; }

        void TakeDamage(DamageInfo damage);
    }
}

