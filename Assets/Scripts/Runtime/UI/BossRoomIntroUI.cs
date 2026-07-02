using System.Collections;
using NeonBreaker.Player;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class BossRoomIntroUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private PlayerInputReader playerInput;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField] private Image dimImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.18f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

        [Header("Stepped Reveal")]
        [SerializeField] private bool revealTitleByCharacter = true;
        [SerializeField, Min(0f)] private float titleRevealStartDelay = 0.08f;
        [SerializeField, Min(0.01f)] private float titleCharacterInterval = 0.09f;
        [SerializeField] private bool revealSubtitleByCharacter = true;
        [SerializeField, Min(0f)] private float subtitleRevealDelay = 0.12f;
        [SerializeField, Min(0.01f)] private float subtitleCharacterInterval = 0.035f;

        [Header("Screen Shake")]
        [SerializeField] private bool shakeOnTitleCharacter = true;
        [SerializeField] private bool shakeOnSubtitleCharacter;
        [SerializeField, Min(0f)] private float characterShakeDuration = 0.055f;
        [SerializeField, Min(0f)] private float characterShakeMagnitude = 12f;
        [SerializeField, Min(1f)] private float characterShakeFrequency = 42f;

        [Header("Options")]
        [SerializeField] private bool lockPlayerInputDuringIntro = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private Color titleColor = new Color(1f, 0.25f, 0.35f, 1f);
        [SerializeField] private Color subtitleColor = new Color(0.95f, 0.98f, 1f, 0.9f);
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.58f);

        private Coroutine routine;
        private Vector2 shakeTargetBasePosition;
        private bool inputLocked;

        private void Awake()
        {
            ResolveSources();

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

            ResolveShakeTarget();
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            if (roomManager != null)
            {
                roomManager.RoomIntroStarted += HandleRoomIntroStarted;
            }
        }

        private void OnDisable()
        {
            if (roomManager != null)
            {
                roomManager.RoomIntroStarted -= HandleRoomIntroStarted;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            ReleasePlayerInput();
        }

        private void HandleRoomIntroStarted(RoomDefinition room, string title, string subtitle, float duration)
        {
            if (room == null || room.RoomType != RoomType.Boss)
            {
                return;
            }

            Show(title, subtitle, duration);
        }

        public void Show(string title, string subtitle, float duration)
        {
            SetText(titleText, string.IsNullOrWhiteSpace(title) ? "BOSS" : title);
            SetText(subtitleText, subtitle);
            ResetRevealState(titleText);
            ResetRevealState(subtitleText);
            CaptureShakeBasePosition();
            ResetShakePosition();

            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            }

            if (dimImage != null)
            {
                dimImage.color = dimColor;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                ReleasePlayerInput();
            }

            LockPlayerInput();
            routine = StartCoroutine(ShowRoutine(Mathf.Max(0.01f, duration)));
        }

        private IEnumerator ShowRoutine(float duration)
        {
            float fadeIn = Mathf.Min(fadeInDuration, duration * 0.45f);
            float fadeOut = Mathf.Min(fadeOutDuration, duration * 0.45f);
            float remaining = Mathf.Max(0f, duration - fadeIn - fadeOut);

            yield return FadeRoutine(0f, 1f, fadeIn);

            if (revealTitleByCharacter)
            {
                yield return RevealTextRoutine(titleText, titleRevealStartDelay, titleCharacterInterval, shakeOnTitleCharacter);
            }

            if (revealSubtitleByCharacter && subtitleText != null && subtitleText.gameObject.activeSelf)
            {
                yield return RevealTextRoutine(subtitleText, subtitleRevealDelay, subtitleCharacterInterval, shakeOnSubtitleCharacter);
            }

            remaining -= EstimateRevealDuration(titleText, revealTitleByCharacter, titleRevealStartDelay, titleCharacterInterval);
            remaining -= EstimateRevealDuration(subtitleText, revealSubtitleByCharacter && subtitleText != null && subtitleText.gameObject.activeSelf, subtitleRevealDelay, subtitleCharacterInterval);

            if (remaining > 0f)
            {
                yield return new WaitForSecondsRealtime(remaining);
            }

            yield return FadeRoutine(1f, 0f, fadeOut);
            ShowAllText(titleText);
            ShowAllText(subtitleText);
            ResetShakePosition();
            ReleasePlayerInput();
            routine = null;
        }

        private IEnumerator RevealTextRoutine(TextMeshProUGUI target, float startDelay, float interval, bool shake)
        {
            if (target == null)
            {
                yield break;
            }

            target.ForceMeshUpdate();
            int characterCount = target.textInfo.characterCount;
            if (characterCount <= 0)
            {
                ShowAllText(target);
                yield break;
            }

            target.maxVisibleCharacters = 0;

            if (startDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelay);
            }

            for (int i = 1; i <= characterCount; i++)
            {
                target.maxVisibleCharacters = i;

                if (shake)
                {
                    yield return ShakeRoutine();
                }

                if (interval > 0f && i < characterCount)
                {
                    yield return new WaitForSecondsRealtime(interval);
                }
            }

            ShowAllText(target);
            ResetShakePosition();
        }

        private IEnumerator ShakeRoutine()
        {
            if (shakeTarget == null || characterShakeDuration <= 0f || characterShakeMagnitude <= 0f)
            {
                yield break;
            }

            Vector2 basePosition = shakeTargetBasePosition;
            float timer = 0f;
            while (timer < characterShakeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / characterShakeDuration);
                float power = 1f - t;
                float x = Mathf.Sin(timer * characterShakeFrequency) * characterShakeMagnitude * power;
                float y = Mathf.Cos(timer * characterShakeFrequency * 1.37f) * characterShakeMagnitude * 0.35f * power;

                shakeTarget.anchoredPosition = basePosition + new Vector2(x, y);
                yield return null;
            }

            ResetShakePosition();
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (root == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = alpha;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        private void ResolveSources()
        {
            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
            }

            if (playerInput == null)
            {
                playerInput = FindPlayerInput();
            }
        }

        private void LockPlayerInput()
        {
            if (!lockPlayerInputDuringIntro || playerInput == null || inputLocked)
            {
                return;
            }

            playerInput.PushGameplayInputLock(this);
            inputLocked = true;
        }

        private void ReleasePlayerInput()
        {
            if (!inputLocked || playerInput == null)
            {
                inputLocked = false;
                return;
            }

            playerInput.ReleaseGameplayInputLock(this);
            inputLocked = false;
        }

        private void AutoBind()
        {
            if (shakeTarget == null)
            {
                RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i].name.ToLowerInvariant().Contains("content"))
                    {
                        shakeTarget = rects[i];
                        break;
                    }
                }
            }

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

            if (dimImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].name.ToLowerInvariant().Contains("dim"))
                    {
                        dimImage = images[i];
                        break;
                    }
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Boss Room Intro Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 370;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Boss Room Intro Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject dimObject = CreateUiObject("Dim Image", rootObject.transform);
            dimImage = dimObject.AddComponent<Image>();
            dimImage.color = dimColor;
            RectTransform dimRect = dimObject.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            GameObject contentObject = CreateUiObject("Boss Room Intro Content", rootObject.transform);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(980f, 220f);

            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;

            titleText = CreateText(contentObject.transform, "Title Text", "BOSS", 92f, FontStyles.Bold, titleColor);
            subtitleText = CreateText(contentObject.transform, "Subtitle Text", "Final Encounter", 30f, FontStyles.Normal, subtitleColor);
            shakeTarget = contentRect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, FontStyles style, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = color;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static float EstimateRevealDuration(TextMeshProUGUI target, bool reveal, float startDelay, float interval)
        {
            if (!reveal || target == null)
            {
                return 0f;
            }

            target.ForceMeshUpdate();
            int characterCount = target.textInfo.characterCount;
            if (characterCount <= 0)
            {
                return 0f;
            }

            return startDelay + Mathf.Max(0, characterCount - 1) * interval;
        }

        private static void ResetRevealState(TextMeshProUGUI target)
        {
            if (target != null)
            {
                target.maxVisibleCharacters = 0;
            }
        }

        private static void ShowAllText(TextMeshProUGUI target)
        {
            if (target != null)
            {
                target.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void ResolveShakeTarget()
        {
            if (shakeTarget == null && root != null)
            {
                shakeTarget = root.GetComponent<RectTransform>();
            }

            CaptureShakeBasePosition();
        }

        private void CaptureShakeBasePosition()
        {
            if (shakeTarget != null)
            {
                shakeTargetBasePosition = shakeTarget.anchoredPosition;
            }
        }

        private void ResetShakePosition()
        {
            if (shakeTarget != null)
            {
                shakeTarget.anchoredPosition = shakeTargetBasePosition;
            }
        }

        private static PlayerInputReader FindPlayerInput()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null && playerObject.TryGetComponent(out PlayerInputReader input))
            {
                return input;
            }

            return FindAnyObjectByType<PlayerInputReader>();
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
