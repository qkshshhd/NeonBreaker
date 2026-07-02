using System.Collections;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class CameraShake2D : MonoBehaviour
    {
        private static CameraShake2D instance;

        [SerializeField] private Transform target;

        private Coroutine routine;
        private Vector3 baseLocalPosition;

        public static void Shake(float duration, float strength)
        {
            if (duration <= 0f || strength <= 0f)
            {
                return;
            }

            CameraShake2D shaker = EnsureInstance();
            if (shaker != null)
            {
                shaker.Play(duration, strength);
            }
        }

        private static CameraShake2D EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            instance = camera.GetComponent<CameraShake2D>();
            if (instance == null)
            {
                instance = camera.gameObject.AddComponent<CameraShake2D>();
            }

            return instance;
        }

        private void Awake()
        {
            if (target == null)
            {
                target = transform;
            }

            if (instance == null)
            {
                instance = this;
            }
        }

        public void Play(float duration, float strength)
        {
            if (target == null)
            {
                target = transform;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                target.localPosition = baseLocalPosition;
            }

            routine = StartCoroutine(ShakeRoutine(duration, strength));
        }

        private IEnumerator ShakeRoutine(float duration, float strength)
        {
            baseLocalPosition = target.localPosition;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float amplitude = strength * (1f - t);
                Vector2 offset = Random.insideUnitCircle * amplitude;
                target.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            target.localPosition = baseLocalPosition;
            routine = null;
        }
    }
}
