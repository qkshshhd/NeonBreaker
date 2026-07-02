using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "Game";
        [SerializeField] private int fallbackGameplayBuildIndex = 1;

        [Header("Text")]
        [SerializeField] private string title = "NEON BREAKER";
        [SerializeField] private string subtitle = "방을 돌파하고 코어를 강화해 보스에게 도전하세요";
        [SerializeField] private string startLabel = "시작";
        [SerializeField] private string quitLabel = "종료";

        [Header("Options")]
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool selectStartButtonOnAwake = true;

        private void Awake()
        {
            Time.timeScale = 1f;

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            if (buildFallbackUiIfMissing && (root == null || startButton == null))
            {
                BuildFallbackUi();
            }

            BindButtons();
            SetText(titleText, title);
            SetText(subtitleText, subtitle);
            SetVisible(true);

            if (selectStartButtonOnAwake && startButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void StartGame()
        {
            Time.timeScale = 1f;

            if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
                return;
            }

            if (fallbackGameplayBuildIndex >= 0 && fallbackGameplayBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(fallbackGameplayBuildIndex);
            }
            else
            {
                Debug.LogError("[MainMenuUI] Gameplay scene is not configured. Set Gameplay Scene Name or Fallback Gameplay Build Index.", this);
            }
        }

        public void QuitGame()
        {
            Application.Quit();
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
                else if (subtitleText == null && (lowerName.Contains("subtitle") || lowerName.Contains("desc")))
                {
                    subtitleText = texts[i];
                }
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                string lowerName = buttons[i].name.ToLowerInvariant();
                if (startButton == null && (lowerName.Contains("start") || lowerName.Contains("play")))
                {
                    startButton = buttons[i];
                }
                else if (quitButton == null && (lowerName.Contains("quit") || lowerName.Contains("exit")))
                {
                    quitButton = buttons[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Main Menu Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Main Menu Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            StretchToParent(rootObject.GetComponent<RectTransform>());

            Image backdrop = rootObject.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.018f, 0.026f, 1f);

            GameObject panelObject = CreateUiObject("Main Menu Panel", rootObject.transform);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.12f);
            panelRect.anchorMax = new Vector2(0.45f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(42, 42, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            titleText = CreateText(panelObject.transform, "Title", title, 64f, FontStyles.Bold, TextAlignmentOptions.Left);
            AddLayout(titleText.gameObject, 640f, 88f);

            subtitleText = CreateText(panelObject.transform, "Subtitle", subtitle, 24f, FontStyles.Normal, TextAlignmentOptions.Left);
            subtitleText.color = new Color(0.74f, 0.85f, 0.92f, 0.92f);
            AddLayout(subtitleText.gameObject, 600f, 80f);

            GameObject spacer = CreateUiObject("Spacer", panelObject.transform);
            AddLayout(spacer, 480f, 40f);

            startButton = CreateButton(panelObject.transform, "Start Button", startLabel);
            quitButton = CreateButton(panelObject.transform, "Quit Button", quitLabel);

            EnsureEventSystem();
        }

        private void BindButtons()
        {
            UnbindButtons();

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
                SetButtonLabel(startButton, startLabel);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
                SetButtonLabel(quitButton, quitLabel);
            }
        }

        private void UnbindButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
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

        private static Button CreateButton(Transform parent, string objectName, string label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            AddLayout(buttonObject, 280f, 56f);

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

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float size, FontStyles style, TextAlignmentOptions alignment)
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
            TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
