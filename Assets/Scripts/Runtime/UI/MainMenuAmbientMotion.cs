using UnityEngine;

namespace NeonBreaker.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuAmbientMotion : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform[] worldTargets;
        [SerializeField] private RectTransform[] uiTargets;
        [SerializeField] private bool useMainCameraWhenEmpty = true;

        [Header("Motion")]
        [SerializeField] private Vector2 worldAmplitude = new Vector2(0.12f, 0.06f);
        [SerializeField] private Vector2 uiAmplitude = new Vector2(8f, 4f);
        [SerializeField, Min(0.01f)] private float frequency = 0.18f;
        [SerializeField] private float secondaryFrequencyMultiplier = 1.73f;

        private Vector3[] worldBasePositions;
        private Vector2[] uiBasePositions;

        private void Awake()
        {
            EnsureFallbackTarget();
            CacheBasePositions();
        }

        private void OnEnable()
        {
            EnsureFallbackTarget();
            CacheBasePositions();
        }

        private void Update()
        {
            float time = Time.unscaledTime * frequency;
            Vector2 drift = new Vector2(
                Mathf.Sin(time * Mathf.PI * 2f),
                Mathf.Sin(time * Mathf.PI * 2f * secondaryFrequencyMultiplier + 1.2f));

            ApplyWorldMotion(drift);
            ApplyUiMotion(drift);
        }

        private void EnsureFallbackTarget()
        {
            if (!useMainCameraWhenEmpty || worldTargets is { Length: > 0 })
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                worldTargets = new[] { mainCamera.transform };
            }
        }

        private void CacheBasePositions()
        {
            worldBasePositions = new Vector3[worldTargets != null ? worldTargets.Length : 0];
            for (int i = 0; i < worldBasePositions.Length; i++)
            {
                worldBasePositions[i] = worldTargets[i] != null ? worldTargets[i].localPosition : Vector3.zero;
            }

            uiBasePositions = new Vector2[uiTargets != null ? uiTargets.Length : 0];
            for (int i = 0; i < uiBasePositions.Length; i++)
            {
                uiBasePositions[i] = uiTargets[i] != null ? uiTargets[i].anchoredPosition : Vector2.zero;
            }
        }

        private void ApplyWorldMotion(Vector2 drift)
        {
            for (int i = 0; i < worldBasePositions.Length; i++)
            {
                if (worldTargets[i] == null)
                {
                    continue;
                }

                Vector3 offset = new Vector3(drift.x * worldAmplitude.x, drift.y * worldAmplitude.y, 0f);
                worldTargets[i].localPosition = worldBasePositions[i] + offset;
            }
        }

        private void ApplyUiMotion(Vector2 drift)
        {
            for (int i = 0; i < uiBasePositions.Length; i++)
            {
                if (uiTargets[i] == null)
                {
                    continue;
                }

                uiTargets[i].anchoredPosition = uiBasePositions[i] + new Vector2(drift.x * uiAmplitude.x, drift.y * uiAmplitude.y);
            }
        }
    }
}
