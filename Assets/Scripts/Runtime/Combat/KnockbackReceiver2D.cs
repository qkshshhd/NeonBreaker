using System.Collections;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class KnockbackReceiver2D : MonoBehaviour, IKnockbackReceiver
    {
        private enum KnockbackMotionStyle
        {
            Smooth,
            Mechanical
        }

        [SerializeField] private float resistance = 0f;
        [SerializeField] private KnockbackMotionStyle motionStyle = KnockbackMotionStyle.Mechanical;
        [SerializeField, Range(0.05f, 1f)] private float mechanicalImpactTimeRatio = 0.35f;
        [SerializeField, Range(0f, 1f)] private float mechanicalBrakeStrength = 0.85f;

        private Rigidbody2D body;
        private Coroutine knockbackRoutine;

        public bool IsReceivingKnockback => knockbackRoutine != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public void ApplyKnockback(Vector2 direction, float force, float duration)
        {
            if (force <= 0f || duration <= 0f)
            {
                return;
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }

            knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
        }

        private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
        {
            Vector2 knockbackDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
            float finalForce = Mathf.Max(0f, force - resistance);

            if (motionStyle == KnockbackMotionStyle.Mechanical)
            {
                yield return MechanicalKnockbackRoutine(knockbackDirection, finalForce, duration);
                knockbackRoutine = null;
                yield break;
            }

            float timer = 0f;

            while (timer < duration)
            {
                body.linearVelocity = knockbackDirection * finalForce;
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
            knockbackRoutine = null;
        }

        private IEnumerator MechanicalKnockbackRoutine(Vector2 direction, float force, float duration)
        {
            float impactDuration = Mathf.Max(Time.fixedDeltaTime, duration * mechanicalImpactTimeRatio);
            float brakeDuration = Mathf.Max(0f, duration - impactDuration);
            float timer = 0f;

            while (timer < impactDuration)
            {
                body.linearVelocity = direction * force;
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            Vector2 brakeVelocity = direction * force * Mathf.Clamp01(1f - mechanicalBrakeStrength);
            timer = 0f;

            while (timer < brakeDuration)
            {
                body.linearVelocity = brakeVelocity;
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
        }
    }
}
