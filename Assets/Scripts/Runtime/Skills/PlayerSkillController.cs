using System;
using System.Collections;
using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Player;
using UnityEngine;

namespace NeonBreaker.Skills
{
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerSkillController : MonoBehaviour
    {
        [SerializeField] private SkillDefinition equippedSkill;
        [SerializeField] private Transform skillOrigin;
        [SerializeField] private bool drawDebugSkill = true;
        [SerializeField] private bool logSkillUse = true;
        [SerializeField] private bool playCombatFeedback = true;
        [SerializeField] private float hitStopDuration = 0.06f;
        [SerializeField] private float cameraShakeDuration = 0.09f;
        [SerializeField] private float cameraShakeStrength = 0.14f;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private PlayerInputReader input;
        private PlayerStats stats;
        private Collider2D[] hitBuffer;
        private float cooldownTimer;
        private Coroutine castRoutine;

        public event Action<SkillDefinition> SkillStarted;
        public event Action<SkillDefinition> SkillPerformed;
        public event Action<SkillDefinition, int> SkillHit;

        public SkillDefinition EquippedSkill => equippedSkill;
        public bool HasSkill => equippedSkill != null;
        public bool IsCasting => castRoutine != null;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public bool IsReady => HasSkill && !IsCasting && CooldownRemaining <= 0f;
        public float CooldownNormalized
        {
            get
            {
                float cooldown = GetEffectiveCooldown();
                return cooldown <= 0f ? 0f : CooldownRemaining / cooldown;
            }
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            stats = GetComponent<PlayerStats>();

            if (skillOrigin == null)
            {
                skillOrigin = transform;
            }

            EnsureHitBuffer();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (input != null && input.SkillPressed)
            {
                TryUseSkill(input.AimDirection);
            }
        }

        private void OnDisable()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }
        }

        public void Equip(SkillDefinition skill)
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }

            equippedSkill = skill;
            EnsureHitBuffer();
            cooldownTimer = 0f;
        }

        public bool TryUseSkill(Vector2 direction)
        {
            if (equippedSkill == null || cooldownTimer > 0f || IsCasting)
            {
                return false;
            }

            EnsureHitBuffer();
            SkillDefinition skill = equippedSkill;
            cooldownTimer = GetEffectiveCooldown();
            SkillStarted?.Invoke(skill);
            castRoutine = StartCoroutine(CastRoutine(skill));
            return true;
        }

        private IEnumerator CastRoutine(SkillDefinition skill)
        {
            if (skill.CastDelay > 0f)
            {
                yield return new WaitForSeconds(skill.CastDelay);
            }

            SkillPerformed?.Invoke(skill);

            int hitCount = skill.SkillType switch
            {
                SkillType.Shockwave => ExecuteShockwave(skill),
                _ => 0
            };

            SkillHit?.Invoke(skill, hitCount);

            if (logSkillUse)
            {
                Debug.Log($"[PlayerSkillController] Skill used: {skill.DisplayName}. Hit Count: {hitCount}", this);
            }

            if (hitCount > 0 && playCombatFeedback)
            {
                HitStop2D.Play(hitStopDuration);
                CameraShake2D.Shake(cameraShakeDuration, cameraShakeStrength);
            }

            castRoutine = null;
        }

        private int ExecuteShockwave(SkillDefinition skill)
        {
            damagedTargets.Clear();

            Vector2 origin = skillOrigin.position;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(skill.TargetLayers);
            filter.useTriggers = Physics2D.queriesHitTriggers;

            float effectiveRadius = stats != null ? stats.GetSkillRadius(skill.Radius) : skill.Radius;
            float effectiveKnockback = stats != null ? stats.GetKnockback(skill.KnockbackForce) : skill.KnockbackForce;
            int count = Physics2D.OverlapCircle(origin, effectiveRadius, filter, hitBuffer);
            int successfulHits = 0;

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
                Vector2 hitDirection = targetPoint - origin;
                if (hitDirection.sqrMagnitude <= 0.0001f)
                {
                    hitDirection = transform.right;
                }

                hitDirection.Normalize();
                float finalDamage = stats != null ? stats.GetSkillDamage(skill.Damage) : skill.Damage;
                DamageInfo damageInfo = new DamageInfo(
                    finalDamage,
                    targetPoint,
                    hitDirection,
                    effectiveKnockback,
                    false,
                    gameObject,
                    DamageSourceType.Skill);

                damageable.TakeDamage(damageInfo);
                stats?.NotifyDamageDealt(finalDamage);

                IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                knockbackReceiver?.ApplyKnockback(hitDirection, effectiveKnockback, skill.KnockbackDuration);

                successfulHits++;
            }

            if (drawDebugSkill)
            {
                DrawShockwaveDebug(origin, effectiveRadius, successfulHits > 0);
            }

            return successfulHits;
        }

        private float GetEffectiveCooldown()
        {
            if (equippedSkill == null)
            {
                return 0f;
            }

            return stats != null ? stats.GetSkillCooldown(equippedSkill.Cooldown) : equippedSkill.Cooldown;
        }

        private void EnsureHitBuffer()
        {
            int maxHits = equippedSkill != null ? equippedSkill.MaxHits : 32;
            if (hitBuffer == null || hitBuffer.Length != maxHits)
            {
                hitBuffer = new Collider2D[maxHits];
            }
        }

        private void DrawShockwaveDebug(Vector2 origin, float radius, bool didHit)
        {
            Color color = didHit ? Color.cyan : Color.gray;
            Debug.DrawLine(origin + Vector2.left * radius, origin + Vector2.right * radius, color, 0.25f);
            Debug.DrawLine(origin + Vector2.down * radius, origin + Vector2.up * radius, color, 0.25f);
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

        private void OnDrawGizmosSelected()
        {
            if (equippedSkill == null || equippedSkill.SkillType != SkillType.Shockwave)
            {
                return;
            }

            Transform origin = skillOrigin != null ? skillOrigin : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin.position, equippedSkill.Radius);
        }
    }
}
