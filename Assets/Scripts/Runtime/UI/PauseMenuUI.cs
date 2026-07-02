using NeonBreaker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerInputReader playerInput;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [Header("Text")]
        [SerializeField] private string title = "일시정지";
        [SerializeField] private string hint = "Esc를 눌러 게임으로 돌아가기";
        [SerializeField] private string resumeLabel = "계속하기";
        [SerializeField] private string restartLabel = "다시 시작";
        [SerializeField] private string quitLabel = "종료";

        [Header("Options")]
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool lockPlayerInputWhilePaused = true;
        [SerializeField] private bool canOpenWhenTimeAlreadyPaused = false;
        [SerializeField] private bool selectResumeButtonOnOpen = true;

        private bool isPaused;
        private bool inputLocked;
        private float previousTimeScale = 1f;

        private void Awake()
        {
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

            if (buildFallbackUiIfMissing && (root == null || resumeButton == null))
            {
                BuildFallbackUi();
            }

            BindButtons();
            HideImmediate();
        }

        private void OnDisable()
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                ReleasePlayerInput();
            }
        }

        private void Update()
        {
            bool pausePressed = playerInput != null
                ? playerInput.PausePressed
                : Input.GetKeyDown(KeyCode.Escape);

            if (!pausePressed)
            {
                return;
            }

            if (isPaused)
            {
                Resume();
                return;
            }

            TryPause();
        }

        public void TryPause()
        {
            if (isPaused)
            {
                return;
            }

            if (!canOpenWhenTimeAlreadyPaused && Time.timeScale <= 0f)
            {
                return;
            }

            Pause();
        }

        public void Pause()
        {
            if (isPaused)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isPaused = true;

            LockPlayerInput();
            SetText(titleText, title);
            SetText(hintText, hint);
            BindButtons();
            SetVisible(true);
            SelectDefaultButton();
        }

        public void Resume()
        {
            if (!isPaused)
            {
                return;
            }

            Time.timeScale = Mathf.Max(0f, previousTimeScale);
            isPaused = false;
            SetVisible(false);
            ReleasePlayerInput();
        }

        public void RestartRun()
        {
            Time.timeScale = 1f;
            ReleasePlayerInput();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
            ReleasePlayerInput();
            Application.Quit();
        }

        private void AutoBind()
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (titleText == null && (lowerName.Contains("title") || lowerName.Contains("pause")))
                {
                    titleText = texts[i];
                }
                else if (hintText == null && lowerName.Contains("hint"))
                {
                    hintText = texts[i];
                }
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                string lowerName = buttons[i].name.ToLowerInvariant();
                if (resumeButton == null && (lowerName.Contains("resume") || lowerName.Contains("continue")))
                {
                    resumeButton = buttons[i];
                }
                else if (restartButton == null && lowerName.Contains("restart"))
                {
                    restartButton = buttons[i];
                }
                else if (quitButton == null && (lowerName.Contains("quit") || lowerName.Contains("exit")))
                {
                    quitButton = buttons[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Pause Menu Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Pause Menu Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            StretchToParent(rootObject.GetComponent<RectTransform>());

            Image backdrop = rootObject.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.018f, 0.026f, 0.78f);

            GameObject panelObject = CreateUiObject("Pause Menu Panel", rootObject.transform);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520f, 500f);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(38, 38, 34, 34);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleText = CreateText(panelObject.transform, "Pause Title", title, 46f, FontStyles.Bold);
            AddLayout(titleText.gameObject, 440f, 70f);

            resumeButton = CreateButton(panelObject.transform, "Resume Button", resumeLabel);
            restartButton = CreateButton(panelObject.transform, "Restart Button", restartLabel);
            quitButton = CreateButton(panelObject.transform, "Quit Button", quitLabel);

            hintText = CreateText(panelObject.transform, "Pause Hint", hint, 18f, FontStyles.Normal);
            hintText.color = new Color(0.74f, 0.85f, 0.92f, 0.88f);
            AddLayout(hintText.gameObject, 440f, 34f);

            EnsureEventSystem();
        }

        private void BindButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(Resume);
                resumeButton.onClick.AddListener(Resume);
                SetButtonLabel(resumeButton, resumeLabel);
            }

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

        private void LockPlayerInput()
        {
            if (!lockPlayerInputWhilePaused || playerInput == null || inputLocked)
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

        private void HideImmediate()
        {
            isPaused = false;
            SetVisible(false);
        }

        private void SelectDefaultButton()
        {
            if (!selectResumeButtonOnOpen || resumeButton == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
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

        private static Button CreateButton(Transform parent, string objectName, string label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            AddLayout(buttonObject, 340f, 56f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.2f, 0.75f, 0.95f, 1f);
            colors.pressedColor = new Color(0.1f, 0.48f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 22f, FontStyles.Bold);
            StretchToParent(text.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float size, FontStyles style)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.94f, 0.98f, 1f, 1f);
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
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
