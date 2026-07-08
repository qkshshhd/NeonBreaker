using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuStartTransition : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private Image overlayImage;
        [SerializeField] private Image coreFlashImage;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float preDelay = 0.04f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.38f;

        [Header("Look")]
        [SerializeField] private Color overlayColor = new Color(0.01f, 0.012f, 0.02f, 1f);
        [SerializeField] private Color flashColor = new Color(0.05f, 0.95f, 1f, 0.34f);
        [SerializeField] private Vector2 flashStartSize = new Vector2(72f, 72f);
        [SerializeField] private Vector2 flashEndSize = new Vector2(820f, 820f);
        [SerializeField, Range(0f, 1f)] private float flashCoverAlpha = 0.18f;
        [SerializeField, HideInInspector] private int tuningVersion;

        private Coroutine routine;
        private RectTransform coreFlashRect;
        private const int CurrentTuningVersion = 1;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            UpgradeDefaults();
            EnsureVisuals();
            SetCovered(0f);
        }

        private void OnValidate()
        {
            UpgradeDefaults();

            if (overlayImage != null)
            {
                overlayImage.color = overlayColor;
            }

            if (coreFlashImage != null)
            {
                coreFlashImage.color = flashColor;
            }
        }

        public void Play(Action onCovered)
        {
            EnsureVisuals();

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine(onCovered));
        }

        private IEnumerator PlayRoutine(Action onCovered)
        {
            IsPlaying = true;
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;

            if (preDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(preDelay);
            }

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / flashDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetCovered(Mathf.Lerp(0f, flashCoverAlpha, eased));
                SetFlash(eased, Mathf.Sin(t * Mathf.PI) * 0.85f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = t * t * (3f - 2f * t);
                SetCovered(Mathf.Lerp(flashCoverAlpha, 1f, eased));
                SetFlash(1f, 1f - eased);
                yield return null;
            }

            SetCovered(1f);
            SetFlash(1f, 0f);
            onCovered?.Invoke();
        }

        private void EnsureVisuals()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Main Menu Transition Canvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 950;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (overlayGroup == null)
            {
                GameObject overlayObject = new GameObject("Start Transition Overlay");
                overlayObject.transform.SetParent(canvas.transform, false);
                overlayGroup = overlayObject.AddComponent<CanvasGroup>();
                overlayImage = overlayObject.AddComponent<Image>();
                overlayImage.raycastTarget = true;
                Stretch(overlayObject.GetComponent<RectTransform>());
            }

            if (overlayImage == null)
            {
                overlayImage = overlayGroup.GetComponent<Image>();
            }

            if (coreFlashImage == null)
            {
                GameObject flashObject = new GameObject("Start Core Flash");
                flashObject.transform.SetParent(overlayGroup.transform, false);
                coreFlashImage = flashObject.AddComponent<Image>();
                coreFlashImage.raycastTarget = false;
                coreFlashImage.sprite = GetSoftCircleSprite();
                coreFlashRect = coreFlashImage.rectTransform;
                coreFlashRect.anchorMin = new Vector2(0.5f, 0.5f);
                coreFlashRect.anchorMax = new Vector2(0.5f, 0.5f);
                coreFlashRect.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                coreFlashRect = coreFlashImage.rectTransform;
            }

            overlayImage.color = overlayColor;
            coreFlashImage.color = flashColor;
            overlayGroup.transform.SetAsLastSibling();
        }

        private void SetCovered(float alpha)
        {
            if (overlayGroup != null)
            {
                overlayGroup.alpha = alpha;
            }
        }

        private void SetFlash(float sizeT, float alphaT)
        {
            if (coreFlashImage == null || coreFlashRect == null)
            {
                return;
            }

            coreFlashRect.sizeDelta = Vector2.Lerp(flashStartSize, flashEndSize, sizeT);
            Color color = flashColor;
            color.a *= Mathf.Clamp01(alphaT);
            coreFlashImage.color = color;
        }

        private void UpgradeDefaults()
        {
            if (tuningVersion >= CurrentTuningVersion)
            {
                return;
            }

            if (Mathf.Approximately(preDelay, 0.08f))
            {
                preDelay = 0.04f;
            }

            if (Mathf.Approximately(flashDuration, 0.28f))
            {
                flashDuration = 0.2f;
            }

            if (Mathf.Approximately(fadeDuration, 0.42f))
            {
                fadeDuration = 0.38f;
            }

            if (flashColor == new Color(0.05f, 0.95f, 1f, 0.78f))
            {
                flashColor = new Color(0.05f, 0.95f, 1f, 0.34f);
            }

            if (flashStartSize == new Vector2(96f, 96f))
            {
                flashStartSize = new Vector2(72f, 72f);
            }

            if (flashEndSize == new Vector2(1900f, 1900f))
            {
                flashEndSize = new Vector2(820f, 820f);
            }

            if (Mathf.Approximately(flashCoverAlpha, 0f))
            {
                flashCoverAlpha = 0.18f;
            }

            tuningVersion = CurrentTuningVersion;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static Sprite softCircleSprite;

        private static Sprite GetSoftCircleSprite()
        {
            if (softCircleSprite != null)
            {
                return softCircleSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            softCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            softCircleSprite.name = "Runtime Soft Circle";
            return softCircleSprite;
        }
    }
}
