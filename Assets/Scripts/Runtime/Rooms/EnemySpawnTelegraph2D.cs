using System.Collections;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    public sealed class EnemySpawnTelegraph2D : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Color startColor = new Color(0.2f, 0.95f, 1f, 0.2f);
        [SerializeField] private Color endColor = new Color(1f, 0.25f, 0.35f, 0.95f);
        [SerializeField, Min(0.01f)] private float startScale = 1.6f;
        [SerializeField, Min(0.01f)] private float endScale = 0.3f;
        [SerializeField, Min(3)] private int fallbackRingSegments = 40;
        [SerializeField, Min(0.001f)] private float fallbackRingWidth = 0.055f;

        private Coroutine routine;

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            if (lineRenderer == null)
            {
                lineRenderer = GetComponentInChildren<LineRenderer>(true);
            }
        }

        public void Play(float duration)
        {
            if (lineRenderer == null && (renderers == null || renderers.Length == 0))
            {
                BuildFallbackRing();
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine(Mathf.Max(0.01f, duration)));
        }

        private IEnumerator PlayRoutine(float duration)
        {
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float pulse = 0.5f + Mathf.Sin(t * Mathf.PI * 8f) * 0.5f;
                Color color = Color.Lerp(startColor, endColor, t);
                color.a *= Mathf.Lerp(0.65f, 1f, pulse);

                if (visualRoot != null)
                {
                    float scale = Mathf.Lerp(startScale, endScale, EaseOutCubic(t));
                    visualRoot.localScale = Vector3.one * scale;
                }

                SetColor(color);
                yield return null;
            }

            SetColor(endColor);
            Destroy(gameObject);
        }

        private void SetColor(Color color)
        {
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].color = color;
                    }
                }
            }

            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

        private void BuildFallbackRing()
        {
            GameObject ringObject = new GameObject("Fallback Ring");
            ringObject.transform.SetParent(transform, false);
            visualRoot = ringObject.transform;

            lineRenderer = ringObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = Mathf.Max(3, fallbackRingSegments);
            lineRenderer.startWidth = fallbackRingWidth;
            lineRenderer.endWidth = fallbackRingWidth;
            lineRenderer.numCornerVertices = 3;
            lineRenderer.numCapVertices = 3;

            int count = lineRenderer.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }

        private static float EaseOutCubic(float t)
        {
            float inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse * inverse;
        }
    }
}
