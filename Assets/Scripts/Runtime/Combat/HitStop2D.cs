using System.Collections;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class HitStop2D : MonoBehaviour
    {
        private static HitStop2D instance;

        private Coroutine routine;
        private float previousTimeScale = 1f;
        private float previousFixedDeltaTime = 0.02f;

        public static void Play(float duration, float timeScale = 0.05f)
        {
            if (duration <= 0f || Time.timeScale <= 0f)
            {
                return;
            }

            EnsureInstance().PlayInternal(duration, Mathf.Clamp(timeScale, 0.01f, 1f));
        }

        private static HitStop2D EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject gameObject = new GameObject("HitStop2D");
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<HitStop2D>();
            return instance;
        }

        private void PlayInternal(float duration, float timeScale)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                RestoreTimeScale();
            }

            routine = StartCoroutine(HitStopRoutine(duration, timeScale));
        }

        private IEnumerator HitStopRoutine(float duration, float timeScale)
        {
            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = Mathf.Min(previousTimeScale, timeScale);
            Time.fixedDeltaTime = previousFixedDeltaTime * Time.timeScale;

            yield return new WaitForSecondsRealtime(duration);

            RestoreTimeScale();
            routine = null;
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;
        }
    }
}
