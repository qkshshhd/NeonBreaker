using System;
using System.Collections;
using System.Collections.Generic;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    public sealed class MeleeAttack2D : MonoBehaviour
    {
        [SerializeField] private Transform attackOrigin = null;
        [SerializeField] private MeleeAttackDefinition definition = null;
        [SerializeField] private MeleeComboDefinition comboDefinition = null;
        [SerializeField] private LayerMask hitLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float attackRange = 1.35f;
        [SerializeField, Range(1f, 360f)] private float attackAngle = 85f;
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField] private float knockbackDuration = 0.08f;
        [SerializeField] private int maxHits = 24;
        [SerializeField] private bool drawDebugAttack = true;
        [SerializeField] private bool logAttackResult = true;
        [SerializeField] private bool playCombatFeedback = true;
        [SerializeField] private float hitStopDuration = 0.04f;
        [SerializeField] private float criticalHitStopDuration = 0.06f;
        [SerializeField] private float cameraShakeDuration = 0.06f;
        [SerializeField] private float cameraShakeStrength = 0.08f;
        [SerializeField] private float criticalCameraShakeStrength = 0.12f;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] hitBuffer;
        private PlayerStats stats;
        private float cooldownTimer;
        private int comboIndex;
        private float comboResetTimer;
        private Coroutine attackRoutine;

        public event Action AttackStarted;
        public event Action<int> AttackHit;

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public Vector3 AttackOriginPosition => attackOrigin != null ? attackOrigin.position : transform.position;
        public float BaseAttackRange => attackRange;
        public float EffectiveAttackRange => stats != null ? stats.GetAttackRange(attackRange) : attackRange;
        public float EffectiveAttackAngle => stats != null ? stats.GetAttackAngle(attackAngle) : attackAngle;
        public int CurrentComboIndex { get; private set; }
        public int CurrentAttackAnimationIndex { get; private set; }
        public float CurrentAttackStateDuration { get; private set; } = 0.2f;
        public float CurrentAttackBaseRange { get; private set; }
        public float CurrentAttackEffectiveRange { get; private set; }

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            hitBuffer = new Collider2D[Mathf.Max(1, maxHits)];
            stats = GetComponentInParent<PlayerStats>();
            ApplyDefinition();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (comboResetTimer > 0f)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0f)
                {
                    ResetCombo();
                }
            }
        }

        public void Configure(MeleeAttackDefinition newDefinition)
        {
            definition = newDefinition;
            ApplyDefinition();
        }

        public void ConfigureCombo(MeleeComboDefinition newComboDefinition)
        {
            comboDefinition = newComboDefinition;
            ResetCombo();
        }

        public bool TryAttack(Vector2 direction)
        {
            if (cooldownTimer > 0f)
            {
                return false;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            MeleeComboDefinition.Step comboStep = GetCurrentComboStep();
            CurrentComboIndex = comboStep != null ? comboIndex : 0;
            CurrentAttackAnimationIndex = comboStep != null ? comboStep.AnimationIndex : 0;
            CurrentAttackStateDuration = GetAttackStateDuration(comboStep);
            CurrentAttackBaseRange = GetBaseRange(comboStep, null);
            CurrentAttackEffectiveRange = GetEffectiveRange(comboStep, null);
            cooldownTimer = stats != null ? stats.GetAttackCooldown(GetAttackCooldown(comboStep)) : GetAttackCooldown(comboStep);
            AttackStarted?.Invoke();

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
            }

            attackRoutine = StartCoroutine(AttackRoutine(direction, comboStep));
            AdvanceCombo(comboStep);

            if (logAttackResult)
            {
                string stepName = comboStep != null ? comboStep.DisplayName : "Basic Attack";
                Debug.Log($"[MeleeAttack2D] Attack fired. Step: {CurrentComboIndex + 1}, Name: {stepName}", this);
            }

            return true;
        }

        private IEnumerator AttackRoutine(Vector2 direction, MeleeComboDefinition.Step comboStep)
        {
            damagedTargets.Clear();

            MeleeComboDefinition.HitWindow[] hitWindows = comboStep?.HitWindows;
            if (hitWindows == null || hitWindows.Length == 0)
            {
                int hitCount = PerformAttack(direction, comboStep, null);
                AttackHit?.Invoke(hitCount);
                attackRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            for (int i = 0; i < hitWindows.Length; i++)
            {
                MeleeComboDefinition.HitWindow hitWindow = hitWindows[i];
                if (hitWindow == null)
                {
                    continue;
                }

                float waitTime = Mathf.Max(0f, hitWindow.Time - elapsed);
                if (waitTime > 0f)
                {
                    yield return new WaitForSeconds(waitTime);
                    elapsed += waitTime;
                }

                if (hitWindow.ClearHitTargetsBeforeThisHit)
                {
                    damagedTargets.Clear();
                }

                int hitCount = PerformAttack(direction, comboStep, hitWindow);
                AttackHit?.Invoke(hitCount);
            }

            attackRoutine = null;
        }

        private int PerformAttack(Vector2 direction, MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            Vector2 origin = attackOrigin.position;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(hitLayers);
            filter.useTriggers = Physics2D.queriesHitTriggers;

            float effectiveRange = GetEffectiveRange(comboStep, hitWindow);
            float effectiveAngle = GetEffectiveAngle(comboStep, hitWindow);
            float effectiveKnockback = GetEffectiveKnockback(comboStep, hitWindow);
            int count = Physics2D.OverlapCircle(origin, effectiveRange, filter, hitBuffer);
            int successfulHits = 0;
            bool anyCritical = false;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector2 targetPoint = hit.ClosestPoint(origin);
                Vector2 toTarget = targetPoint - origin;
                if (!IsInsideAttackCone(direction, toTarget, effectiveAngle))
                {
                    continue;
                }

                IDamageable damageable = FindComponentInParents<IDamageable>(hit);
                if (damageable == null || !damageable.CanTakeDamage || damagedTargets.Contains(damageable))
                {
                    continue;
                }

                damagedTargets.Add(damageable);

                Vector2 hitDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction;
                bool isCritical = false;
                float finalDamage = RollDamage(comboStep, hitWindow, out isCritical);
                DamageInfo damageInfo = new DamageInfo(
                    finalDamage,
                    targetPoint,
                    hitDirection,
                    effectiveKnockback,
                    isCritical,
                    gameObject,
                    DamageSourceType.BasicAttack);
                anyCritical |= isCritical;

                damageable.TakeDamage(damageInfo);
                stats?.NotifyDamageDealt(finalDamage);

                IKnockbackReceiver knockbackReceiver = FindComponentInParents<IKnockbackReceiver>(hit);
                knockbackReceiver?.ApplyKnockback(hitDirection, effectiveKnockback, knockbackDuration);

                successfulHits++;
            }

            if (drawDebugAttack)
            {
                DrawAttackDebug(origin, direction, effectiveRange, effectiveAngle, successfulHits > 0);
            }

            if (successfulHits > 0 && playCombatFeedback)
            {
                PlayFeedback(anyCritical);
            }

            return successfulHits;
        }

        private float RollDamage(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow, out bool isCritical)
        {
            float damageMultiplier = GetDamageMultiplier(comboStep, hitWindow);
            float baseDamage = damage * damageMultiplier;
            return stats != null ? stats.RollAttackDamage(baseDamage, out isCritical) : RollFallbackDamage(baseDamage, out isCritical);
        }

        private float RollFallbackDamage(float baseDamage, out bool isCritical)
        {
            isCritical = false;
            return baseDamage;
        }

        private void PlayFeedback(bool isCritical)
        {
            HitStop2D.Play(isCritical ? criticalHitStopDuration : hitStopDuration);
            CameraShake2D.Shake(cameraShakeDuration, isCritical ? criticalCameraShakeStrength : cameraShakeStrength);
        }

        private bool IsInsideAttackCone(Vector2 direction, Vector2 toTarget, float effectiveAngle)
        {
            if (toTarget.sqrMagnitude <= 0.01f || effectiveAngle >= 359f)
            {
                return true;
            }

            float angle = Vector2.Angle(direction, toTarget.normalized);
            return angle <= effectiveAngle * 0.5f;
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

        private void ApplyDefinition()
        {
            if (definition == null)
            {
                return;
            }

            damage = definition.Damage;
            attackCooldown = definition.Cooldown;
            attackRange = definition.Range;
            attackAngle = definition.Angle;
            knockbackForce = definition.KnockbackForce;
            knockbackDuration = definition.KnockbackDuration;
            hitLayers = definition.TargetLayers;
            CurrentAttackBaseRange = attackRange;
            CurrentAttackEffectiveRange = EffectiveAttackRange;
        }

        private MeleeComboDefinition.Step GetCurrentComboStep()
        {
            if (comboDefinition == null || comboDefinition.StepCount <= 0)
            {
                return null;
            }

            return comboDefinition.GetStep(comboIndex);
        }

        private void AdvanceCombo(MeleeComboDefinition.Step comboStep)
        {
            if (comboStep == null || comboDefinition == null || comboDefinition.StepCount <= 0)
            {
                comboIndex = 0;
                comboResetTimer = 0f;
                return;
            }

            comboIndex++;
            if (comboIndex >= comboDefinition.StepCount)
            {
                comboIndex = comboDefinition.LoopCombo ? 0 : 0;
            }

            comboResetTimer = comboDefinition.ResetWindow;
        }

        private void ResetCombo()
        {
            comboIndex = 0;
            comboResetTimer = 0f;
        }

        private float GetAttackCooldown(MeleeComboDefinition.Step comboStep)
        {
            return comboStep != null ? comboStep.Cooldown : attackCooldown;
        }

        private float GetAttackStateDuration(MeleeComboDefinition.Step comboStep)
        {
            return comboStep != null ? comboStep.Duration : attackCooldown;
        }

        private float GetDamageMultiplier(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            float multiplier = comboStep != null ? comboStep.DamageMultiplier : 1f;
            if (hitWindow != null)
            {
                multiplier *= hitWindow.DamageMultiplier;
            }

            return multiplier;
        }

        private float GetEffectiveRange(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            float baseRange = GetBaseRange(comboStep, hitWindow);
            return stats != null ? stats.GetAttackRange(baseRange) : baseRange;
        }

        private float GetBaseRange(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            float multiplier = comboStep != null ? comboStep.RangeMultiplier : 1f;
            if (hitWindow != null)
            {
                multiplier *= hitWindow.RangeMultiplier;
            }

            return attackRange * multiplier;
        }

        private float GetEffectiveAngle(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            float baseAngle = attackAngle;
            if (comboStep != null)
            {
                baseAngle = comboStep.Angle;
            }

            if (hitWindow != null)
            {
                baseAngle = hitWindow.Angle;
            }

            return stats != null ? stats.GetAttackAngle(baseAngle) : baseAngle;
        }

        private float GetEffectiveKnockback(MeleeComboDefinition.Step comboStep, MeleeComboDefinition.HitWindow hitWindow)
        {
            float multiplier = comboStep != null ? comboStep.KnockbackMultiplier : 1f;
            if (hitWindow != null)
            {
                multiplier *= hitWindow.KnockbackMultiplier;
            }

            float baseKnockback = knockbackForce * multiplier;
            return stats != null ? stats.GetKnockback(baseKnockback) : baseKnockback;
        }

        private void DrawAttackDebug(Vector2 origin, Vector2 direction, float range, float effectiveAngle, bool didHit)
        {
            Color color = didHit ? Color.green : Color.red;
            Vector2 left = Quaternion.Euler(0f, 0f, effectiveAngle * 0.5f) * direction;
            Vector2 right = Quaternion.Euler(0f, 0f, -effectiveAngle * 0.5f) * direction;

            Debug.DrawLine(origin, origin + direction.normalized * range, color, 0.18f);
            Debug.DrawLine(origin, origin + left.normalized * range, color, 0.18f);
            Debug.DrawLine(origin, origin + right.normalized * range, color, 0.18f);
        }

        private void OnDrawGizmosSelected()
        {
            Transform originTransform = attackOrigin != null ? attackOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector2 forward = originTransform.right;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, attackRange);

            Vector2 left = Quaternion.Euler(0f, 0f, attackAngle * 0.5f) * forward;
            Vector2 right = Quaternion.Euler(0f, 0f, -attackAngle * 0.5f) * forward;
            Gizmos.DrawLine(origin, origin + (Vector3)(left.normalized * attackRange));
            Gizmos.DrawLine(origin, origin + (Vector3)(right.normalized * attackRange));
        }
    }
}
