using System.Collections;
using NeonBreaker.Combat;
using NeonBreaker.Player;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class RunResultUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerInputReader playerInput;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [Header("Text")]
        [SerializeField] private string victoryTitle = "런 클리어";
        [SerializeField] private string victoryDescription = "네온 코어가 붕괴했습니다.";
        [SerializeField] private string defeatTitle = "작전 실패";
        [SerializeField] private string defeatDescription = "네온 신호가 끊겼습니다.";
        [SerializeField] private string hint = "R: 재시작 / Esc: 종료";
        [SerializeField] private string restartLabel = "재시작";
        [SerializeField] private string quitLabel = "종료";

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float victoryShowDelay = 0.65f;
        [SerializeField, Min(0f)] private float defeatShowDelay = 0.15f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.28f;
        [SerializeField] private bool revealTitleByCharacter = true;
        [SerializeField, Min(0.01f)] private float titleCharacterInterval = 0.055f;
        [SerializeField] private bool selectRestartButtonOnShow = true;

        [Header("Options")]
        [SerializeField] private bool lockPlayerInputOnResult = true;
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool pauseGameOnResult = true;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private KeyCode restartKey = KeyCode.R;
        [SerializeField] private KeyCode quitKey = KeyCode.Escape;

        private bool resultShown;
        private bool pausedByResult;
        private bool inputLocked;
        private float previousTimeScale = 1f;
        private Coroutine showRoutine;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (playerHealth == null)
            {
                playerHealth = FindPlayerHealth();
            }

            if (playerInput == null)
            {
                playerInput = FindPlayerInput();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            if (buildFallbackUiIfMissing && (root == null || titleText == null || descriptionText == null))
            {
                BuildFallbackUi();
            }

            if (hideOnAwake)
            {
                Hide();
            }
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunCleared += HandleRunCleared;
                runManager.RunRoomStarted += HandleRunRoomStarted;
            }

            if (playerHealth != null)
            {
                playerHealth.Died += HandlePlayerDied;
            }

            BindButtons();
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunCleared -= HandleRunCleared;
                runManager.RunRoomStarted -= HandleRunRoomStarted;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }

            ReleasePlayerInput();
            RestoreTimeScale();
        }

        private void Update()
        {
            if (!resultShown)
            {
                return;
            }

            if (Input.GetKeyDown(restartKey))
            {
                RestartRun();
            }
            else if (Input.GetKeyDown(quitKey))
            {
                QuitGame();
            }
        }

        public void ShowVictory()
        {
            ShowResult(victoryTitle, victoryDescription, victoryShowDelay);
        }

        public void ShowDefeat()
        {
            ShowResult(defeatTitle, defeatDescription, defeatShowDelay);
        }

        public void Hide()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            resultShown = false;
            ShowAllTitleCharacters();
            SetVisible(false);
            ReleasePlayerInput();
            RestoreTimeScale();
        }

        public void RestartRun()
        {
            RestoreTimeScale();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void QuitGame()
        {
            RestoreTimeScale();
            Application.Quit();
        }

        private void HandleRunCleared()
        {
            ShowVictory();
        }

        private void HandlePlayerDied()
        {
            ShowDefeat();
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            if (resultShown)
            {
                Hide();
            }
        }

        private void ShowResult(string title, string description, float delay)
        {
            if (resultShown)
            {
                return;
            }

            resultShown = true;
            LockPlayerInput();
            SetText(titleText, title);
            SetText(descriptionText, description);
            SetText(hintText, hint);
            ResetTitleReveal();
            SetVisible(false);

            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(ShowResultRoutine(Mathf.Max(0f, delay)));
        }

        private IEnumerator ShowResultRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            PauseTimeScale();
            yield return FadeInRoutine();
            yield return RevealTitleRoutine();
            ShowAllTitleCharacters();
            SelectDefaultButton();
            showRoutine = null;
        }

        private IEnumerator FadeInRoutine()
        {
            if (root == null)
            {
                yield break;
            }

            root.interactable = false;
            root.blocksRaycasts = true;

            if (fadeInDuration <= 0f)
            {
                root.alpha = 1f;
                root.interactable = true;
                yield break;
            }

            root.alpha = 0f;
            float timer = 0f;
            while (timer < fadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeInDuration);
                root.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            root.alpha = 1f;
            root.interactable = true;
        }

        private IEnumerator RevealTitleRoutine()
        {
            if (!revealTitleByCharacter || titleText == null)
            {
                ShowAllTitleCharacters();
                yield break;
            }

            titleText.ForceMeshUpdate();
            int characterCount = titleText.textInfo.characterCount;
            if (characterCount <= 0)
            {
                ShowAllTitleCharacters();
                yield break;
            }

            titleText.maxVisibleCharacters = 0;
            for (int i = 1; i <= characterCount; i++)
            {
                titleText.maxVisibleCharacters = i;
                if (i < characterCount)
                {
                    yield return new WaitForSecondsRealtime(titleCharacterInterval);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.interactable = visible;
            root.blocksRaycasts = visible;
        }

        private void ResetTitleReveal()
        {
            if (titleText != null)
            {
                titleText.maxVisibleCharacters = revealTitleByCharacter ? 0 : int.MaxValue;
            }
        }

        private void ShowAllTitleCharacters()
        {
            if (titleText != null)
            {
                titleText.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void SelectDefaultButton()
        {
            if (!selectRestartButtonOnShow || restartButton == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        private void PauseTimeScale()
        {
            if (!pauseGameOnResult || pausedByResult)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByResult = true;
        }

        private void RestoreTimeScale()
        {
            if (!pausedByResult)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            pausedByResult = false;
        }

        private void LockPlayerInput()
        {
            if (!lockPlayerInputOnResult || playerInput == null || inputLocked)
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
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (titleText == null && (lowerName.Contains("title") || lowerName.Contains("result")))
                {
                    titleText = texts[i];
                }
                else if (descriptionText == null && (lowerName.Contains("description") || lowerName.Contains("desc") || lowerName.Contains("body")))
                {
                    descriptionText = texts[i];
                }
                else if (hintText == null && lowerName.Contains("hint"))
                {
                    hintText = texts[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Run Result Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Run Result Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            StretchToParent(rootObject.GetComponent<RectTransform>());

            Image backdrop = rootObject.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.018f, 0.026f, 0.86f);

            GameObject panelObject = CreateUiObject("Run Result Panel", rootObject.transform);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(760f, 360f);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(42, 42, 38, 38);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleText = CreateText(panelObject.transform, "Result Title", victoryTitle, 58f, FontStyles.Bold, TextAlignmentOptions.Center);
            AddLayout(titleText.gameObject, 670f, 82f);

            descriptionText = CreateText(panelObject.transform, "Result Description", victoryDescription, 28f, FontStyles.Normal, TextAlignmentOptions.Center);
            AddLayout(descriptionText.gameObject, 670f, 90f);

            hintText = CreateText(panelObject.transform, "Result Hint", hint, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            hintText.color = new Color(0.7f, 0.82f, 0.9f, 0.9f);
            AddLayout(hintText.gameObject, 670f, 42f);

            GameObject buttonRow = CreateUiObject("Result Button Row", panelObject.transform);
            AddLayout(buttonRow, 670f, 58f);

            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 18f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = false;
            buttonLayout.childControlHeight = false;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            restartButton = CreateButton(buttonRow.transform, "Restart Button", restartLabel);
            quitButton = CreateButton(buttonRow.transform, "Quit Button", quitLabel);

            EnsureEventSystem();
            BindButtons();
        }

        private void BindButtons()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartRun);
                restartButton.onClick.AddListener(RestartRun);
                SetButtonLabel(restartButton, restartLabel);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
                quitButton.onClick.AddListener(QuitGame);
                SetButtonLabel(quitButton, quitLabel);
            }
        }

        private Health FindPlayerHealth()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null && playerObject.TryGetComponent(out Health health))
            {
                return health;
            }

            return FindAnyObjectByType<Health>();
        }

        private PlayerInputReader FindPlayerInput()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null && playerObject.TryGetComponent(out PlayerInputReader input))
            {
                return input;
            }

            return FindAnyObjectByType<PlayerInputReader>();
        }

        private Button CreateButton(Transform parent, string objectName, string label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 180f;
            layout.preferredHeight = 52f;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.2f, 0.75f, 0.95f, 1f);
            colors.pressedColor = new Color(0.1f, 0.48f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform);
            return button;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.94f, 0.98f, 1f, 1f);
            return text;
        }

        private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
        {
            LayoutElement layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
