using System.Collections;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class KnockbackReceiver2D : MonoBehaviour, IKnockbackReceiver
    {
        [SerializeField] private float resistance = 0f;

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
    }
}
