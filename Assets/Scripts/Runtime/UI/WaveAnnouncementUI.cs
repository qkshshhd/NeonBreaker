using System.Collections;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class WaveAnnouncementUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomManager roomManager;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float holdDuration = 0.85f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Header("Options")]
        [SerializeField] private bool findTextInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool showWaveNameAsSubtitle = true;

        private Coroutine routine;

        private void Awake()
        {
            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findTextInChildren)
            {
                AutoBindTexts();
            }

            if (buildFallbackUiIfMissing && (root == null || titleText == null))
            {
                BuildFallbackUi();
            }

            SetVisible(0f);
        }

        private void OnEnable()
        {
            if (roomManager != null)
            {
                roomManager.RoomWaveStarted += HandleRoomWaveStarted;
            }
        }

        private void OnDisable()
        {
            if (roomManager != null)
            {
                roomManager.RoomWaveStarted -= HandleRoomWaveStarted;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void HandleRoomWaveStarted(RoomDefinition room, int waveIndex, RoomDefinition.EncounterWave wave)
        {
            int totalWaves = room != null && room.Waves != null ? room.Waves.Length : 0;
            string title = totalWaves > 0 ? $"WAVE {waveIndex + 1}" : "WAVE";
            string subtitle = string.Empty;

            if (showWaveNameAsSubtitle && wave != null && !string.IsNullOrWhiteSpace(wave.DisplayName))
            {
                subtitle = wave.DisplayName;
            }
            else if (totalWaves > 0)
            {
                subtitle = $"{waveIndex + 1} / {totalWaves}";
            }

            Show(title, subtitle);
        }

        public void Show(string title, string subtitle)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (subtitleText != null)
            {
                subtitleText.text = subtitle;
                subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            yield return FadeRoutine(0f, 1f, fadeInDuration);

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return FadeRoutine(1f, 0f, fadeOutDuration);
            routine = null;
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (root == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetVisible(to);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                SetVisible(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetVisible(to);
        }

        private void SetVisible(float alpha)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = alpha;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        private void AutoBindTexts()
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (titleText == null && (lowerName.Contains("title") || lowerName.Contains("wave")))
                {
                    titleText = texts[i];
                }
                else if (subtitleText == null && (lowerName.Contains("subtitle") || lowerName.Contains("sub")))
                {
                    subtitleText = texts[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Wave Announcement Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Wave Announcement Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject contentObject = CreateUiObject("Wave Announcement Content", rootObject.transform);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(900f, 180f);

            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;

            titleText = CreateText(contentObject.transform, "Wave Title Text", "WAVE 1", 76f, FontStyles.Bold);
            titleText.color = new Color(0.48f, 0.95f, 1f, 1f);
            AddLayout(titleText.gameObject, 900f, 92f);

            subtitleText = CreateText(contentObject.transform, "Wave Subtitle Text", "1 / 3", 28f, FontStyles.Normal);
            subtitleText.color = new Color(0.9f, 0.96f, 1f, 0.88f);
            AddLayout(subtitleText.gameObject, 900f, 44f);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, FontStyles fontStyle)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
        {
            LayoutElement layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }
    }
}
