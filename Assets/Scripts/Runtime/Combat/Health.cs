using System;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        private const float MaximumShieldRatio = 0.6f;

        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityDuration = 0.2f;
        [SerializeField, Range(0f, MaximumShieldRatio)] private float maxShieldPercentOfMaxHealth = MaximumShieldRatio;
        [SerializeField] private bool destroyOnDeath = false;

        private float currentHealth;
        private float currentShield;
        private float invulnerabilityTimer;
        private bool isDead;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> ShieldChanged;
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float CurrentShield => currentShield;
        public float MaxShield => Mathf.Max(
            0f,
            maxHealth * Mathf.Clamp(maxShieldPercentOfMaxHealth, 0f, MaximumShieldRatio));
        public bool IsDead => isDead;
        public bool IsInvulnerable => invulnerabilityTimer > 0f;
        public bool CanTakeDamage => isActiveAndEnabled && !isDead && !IsInvulnerable;

        private void Awake()
        {
            maxShieldPercentOfMaxHealth = Mathf.Clamp(
                maxShieldPercentOfMaxHealth,
                0f,
                MaximumShieldRatio);
            currentHealth = maxHealth;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            maxShieldPercentOfMaxHealth = Mathf.Clamp(
                maxShieldPercentOfMaxHealth,
                0f,
                MaximumShieldRatio);
            currentShield = Mathf.Min(currentShield, MaxShield);
        }

        private void Update()
        {
            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= Time.deltaTime;
            }
        }

        public void ResetHealth()
        {
            isDead = false;
            invulnerabilityTimer = 0f;
            currentHealth = maxHealth;
            currentShield = 0f;
            HealthChanged?.Invoke(currentHealth, maxHealth);
            ShieldChanged?.Invoke(currentShield, MaxShield);
        }

        public void Initialize(float newMaxHealth, float newInvulnerabilityDuration)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            invulnerabilityDuration = Mathf.Max(0f, newInvulnerabilityDuration);
            ResetHealth();
        }

        public void SetMaxHealth(float newMaxHealth, bool keepCurrentRatio)
        {
            float oldMaxHealth = maxHealth;
            float ratio = oldMaxHealth <= 0f ? 1f : currentHealth / oldMaxHealth;

            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = keepCurrentRatio
                ? Mathf.Clamp(maxHealth * ratio, 0f, maxHealth)
                : Mathf.Min(currentHealth, maxHealth);
            currentShield = Mathf.Min(currentShield, MaxShield);

            HealthChanged?.Invoke(currentHealth, maxHealth);
            ShieldChanged?.Invoke(currentShield, MaxShield);
        }

        public void IncreaseMaxHealth(float amount, bool healByIncrease)
        {
            if (amount <= 0f)
            {
                return;
            }

            maxHealth += amount;
            if (healByIncrease && !isDead)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            }

            HealthChanged?.Invoke(currentHealth, maxHealth);
            ShieldChanged?.Invoke(currentShield, MaxShield);
        }

        public void IncreaseInvulnerabilityDuration(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            invulnerabilityDuration += amount;
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void AddShield(float amount)
        {
            if (isDead || amount <= 0f || MaxShield <= 0f)
            {
                return;
            }

            currentShield = Mathf.Clamp(currentShield + amount, 0f, MaxShield);
            ShieldChanged?.Invoke(currentShield, MaxShield);
        }

        public void ClearShield()
        {
            if (currentShield <= 0f)
            {
                return;
            }

            currentShield = 0f;
            ShieldChanged?.Invoke(currentShield, MaxShield);
        }

        public void AddInvulnerability(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, duration);
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!CanTakeDamage || damage.Amount <= 0f)
            {
                return;
            }

            float remainingDamage = damage.Amount;
            if (currentShield > 0f)
            {
                float absorbedDamage = Mathf.Min(currentShield, remainingDamage);
                currentShield -= absorbedDamage;
                remainingDamage -= absorbedDamage;
                ShieldChanged?.Invoke(currentShield, MaxShield);
            }

            currentHealth = Mathf.Max(0f, currentHealth - remainingDamage);
            invulnerabilityTimer = invulnerabilityDuration;

            HealthChanged?.Invoke(currentHealth, maxHealth);
            Damaged?.Invoke(damage);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            Died?.Invoke();

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
