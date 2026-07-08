using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.Tutorial
{
    public sealed class TutorialHintUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Image accentLine;

        [Header("Options")]
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private float fadeDuration = 0.12f;
        [SerializeField, Min(0f)] private float transitionGap = 0.08f;

        private Coroutine fadeRoutine;
        private Coroutine transitionRoutine;

        private void Awake()
        {
            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (titleText == null || bodyText == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length > 0 && titleText == null)
                {
                    titleText = texts[0];
                }

                if (texts.Length > 1 && bodyText == null)
                {
                    bodyText = texts[1];
                }
            }

            if (buildFallbackUiIfMissing && (root == null || titleText == null || bodyText == null))
            {
                BuildFallbackUi();
            }

            SetImmediate(false);
        }

        public void Show(string title, string body)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (titleText != null)
            {
                titleText.text = title;
            }

            if (bodyText != null)
            {
                bodyText.text = body;
            }

            FadeTo(1f);
        }

        public void TransitionTo(string title, string body)
        {
            if (root == null)
            {
                Show(title, body);
                return;
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(TransitionRoutine(title, body));
        }

        public void Hide()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            FadeTo(0f);
        }

        private void FadeTo(float targetAlpha)
        {
            if (root == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator TransitionRoutine(string title, string body)
        {
            yield return FadeRoutine(0f);

            if (transitionGap > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionGap);
            }

            if (titleText != null)
            {
                titleText.text = title;
            }

            if (bodyText != null)
            {
                bodyText.text = body;
            }

            yield return FadeRoutine(1f);
            transitionRoutine = null;
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            float startAlpha = root.alpha;
            float timer = 0f;
            float duration = Mathf.Max(0.01f, fadeDuration);

            root.gameObject.SetActive(true);
            root.interactable = false;
            root.blocksRaycasts = false;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                root.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            root.alpha = targetAlpha;
            root.gameObject.SetActive(targetAlpha > 0.001f);
            fadeRoutine = null;
        }

        private void SetImmediate(bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
            root.gameObject.SetActive(visible);
        }

        private void BuildFallbackUi()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Tutorial Hint Canvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 360;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            GameObject panelObject = new GameObject("Tutorial Hint Panel");
            panelObject.transform.SetParent(canvas.transform, false);
            RectTransform panel = panelObject.AddComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 86f);
            panel.sizeDelta = new Vector2(520f, 96f);

            Image background = panelObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.04f, 0.07f, 0.86f);
            root = panelObject.AddComponent<CanvasGroup>();

            accentLine = CreateImage(panel, "Accent", new Color(0.08f, 0.95f, 1f, 1f));
            RectTransform accentRect = accentLine.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 3f);

            titleText = CreateText(panel, "Title", 21f, FontStyles.Bold, new Color(0.84f, 1f, 1f, 1f));
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            titleRect.sizeDelta = new Vector2(-42f, 28f);

            bodyText = CreateText(panel, "Body", 17f, FontStyles.Normal, new Color(0.76f, 0.86f, 0.95f, 1f));
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, -16f);
            bodyRect.sizeDelta = new Vector2(-42f, -46f);
        }

        private static Image CreateImage(RectTransform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, float size, FontStyles style, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
