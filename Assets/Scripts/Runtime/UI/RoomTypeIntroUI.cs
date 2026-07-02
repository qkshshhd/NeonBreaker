using System.Collections;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class RoomTypeIntroUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image accentImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
        [SerializeField, Min(0f)] private float holdDuration = 0.65f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.28f;

        [Header("Motion")]
        [SerializeField] private bool animateScale = true;
        [SerializeField] private Vector3 startScale = new Vector3(0.92f, 0.92f, 1f);
        [SerializeField] private Vector3 endScale = Vector3.one;

        [Header("Options")]
        [SerializeField] private bool showCombatRooms = true;
        [SerializeField] private bool showEliteRooms = true;
        [SerializeField] private bool showRewardRooms = true;
        [SerializeField] private bool showRestRooms = true;
        [SerializeField] private bool showBossRooms = false;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool findBindingsInChildren = true;

        private Coroutine routine;
        private RectTransform contentTransform;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            if (buildFallbackUiIfMissing && (root == null || titleText == null))
            {
                BuildFallbackUi();
            }

            ResolveContentTransform();
            SetVisible(0f);
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            if (room == null || !ShouldShow(room.RoomType))
            {
                return;
            }

            Show(GetTitle(room), GetSubtitle(room), GetColor(room));
        }

        public void Show(string title, string subtitle, Color accentColor)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = accentColor;
            }

            if (subtitleText != null)
            {
                subtitleText.text = subtitle;
                subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            }

            if (accentImage != null)
            {
                Color imageColor = accentColor;
                imageColor.a = 0.26f;
                accentImage.color = imageColor;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            if (contentTransform != null && animateScale)
            {
                contentTransform.localScale = startScale;
            }

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
            if (duration <= 0f)
            {
                SetVisible(to);
                UpdateScale(to);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float eased = 1f - (1f - t) * (1f - t);
                float alpha = Mathf.Lerp(from, to, eased);
                SetVisible(alpha);
                UpdateScale(alpha);
                yield return null;
            }

            SetVisible(to);
            UpdateScale(to);
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

        private void UpdateScale(float t)
        {
            if (!animateScale || contentTransform == null)
            {
                return;
            }

            contentTransform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Clamp01(t));
        }

        private bool ShouldShow(RoomType roomType)
        {
            return roomType switch
            {
                RoomType.Combat => showCombatRooms,
                RoomType.Elite => showEliteRooms,
                RoomType.Reward => showRewardRooms,
                RoomType.Rest => showRestRooms,
                RoomType.Boss => showBossRooms,
                _ => false
            };
        }

        private static string GetTitle(RoomDefinition room)
        {
            return room.RoomType switch
            {
                RoomType.Elite => "ELITE ROOM",
                RoomType.Reward => "REWARD ROOM",
                RoomType.Rest => "REST ROOM",
                RoomType.Boss => "BOSS ROOM",
                _ => "COMBAT ROOM"
            };
        }

        private static string GetSubtitle(RoomDefinition room)
        {
            if (!string.IsNullOrWhiteSpace(room.DisplayName))
            {
                return room.DisplayName;
            }

            return room.RoomType switch
            {
                RoomType.Elite => "Danger level increased",
                RoomType.Reward => "Choose your next power",
                RoomType.Rest => "Recover and breathe",
                RoomType.Boss => "Final encounter",
                _ => "Enemies incoming"
            };
        }

        private static Color GetColor(RoomDefinition room)
        {
            return room.RoomType switch
            {
                RoomType.Elite => new Color(1f, 0.24f, 0.28f, 1f),
                RoomType.Reward => new Color(1f, 0.82f, 0.25f, 1f),
                RoomType.Rest => new Color(0.28f, 1f, 0.72f, 1f),
                RoomType.Boss => new Color(1f, 0.12f, 0.22f, 1f),
                _ => new Color(0.36f, 0.9f, 1f, 1f)
            };
        }

        private void AutoBind()
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (titleText == null && lowerName.Contains("title"))
                {
                    titleText = texts[i];
                }
                else if (subtitleText == null && (lowerName.Contains("subtitle") || lowerName.Contains("sub")))
                {
                    subtitleText = texts[i];
                }
            }

            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (accentImage == null && images[i].name.ToLowerInvariant().Contains("accent"))
                {
                    accentImage = images[i];
                }
            }
        }

        private void ResolveContentTransform()
        {
            if (titleText != null)
            {
                contentTransform = titleText.transform.parent as RectTransform;
            }

            if (contentTransform == null && root != null)
            {
                contentTransform = root.transform as RectTransform;
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Room Type Intro Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 340;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Room Type Intro Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject contentObject = CreateUiObject("Room Type Intro Content", rootObject.transform);
            contentTransform = contentObject.GetComponent<RectTransform>();
            contentTransform.anchorMin = new Vector2(0.5f, 0.68f);
            contentTransform.anchorMax = new Vector2(0.5f, 0.68f);
            contentTransform.pivot = new Vector2(0.5f, 0.5f);
            contentTransform.anchoredPosition = Vector2.zero;
            contentTransform.sizeDelta = new Vector2(780f, 150f);

            accentImage = CreateUiObject("Accent Image", contentObject.transform).AddComponent<Image>();
            RectTransform accentRect = accentImage.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0.5f, 0.5f);
            accentRect.anchorMax = new Vector2(0.5f, 0.5f);
            accentRect.pivot = new Vector2(0.5f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(760f, 4f);

            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;

            titleText = CreateText(contentObject.transform, "Title Text", "COMBAT ROOM", 48f, FontStyles.Bold);
            subtitleText = CreateText(contentObject.transform, "Subtitle Text", "Enemies incoming", 22f, FontStyles.Normal);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, FontStyles style)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = Color.white;
            return text;
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
