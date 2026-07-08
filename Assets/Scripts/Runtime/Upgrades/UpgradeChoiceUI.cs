using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonBreaker.Upgrades
{
    public sealed class UpgradeChoiceUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private CanvasGroup root;
        [SerializeField] private UpgradeChoiceCardUI[] cards;

        [Header("Optional")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private bool findCardsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool hideRootGameObject = false;

        private IReadOnlyList<UpgradeDefinition> choices;

        private void Awake()
        {
            if (upgradeManager == null)
            {
                upgradeManager = GetComponent<UpgradeManager>();
            }

            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findCardsInChildren && (cards == null || cards.Length == 0))
            {
                cards = GetComponentsInChildren<UpgradeChoiceCardUI>(true);
            }

            if (buildFallbackUiIfMissing && (root == null || cards == null || cards.Length == 0))
            {
                BuildFallbackUi();
            }

            BindCards();
            SetStaticLabels();
            Hide();
        }

        private void OnEnable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.ChoicesOffered += Show;
                upgradeManager.UpgradeSelected += HandleUpgradeSelected;
            }
        }

        private void OnDisable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.ChoicesOffered -= Show;
                upgradeManager.UpgradeSelected -= HandleUpgradeSelected;
            }
        }

        private void Update()
        {
            if (!IsVisible() || upgradeManager == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectChoice(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectChoice(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectChoice(2);
            }
        }

        public void Show(IReadOnlyList<UpgradeDefinition> offeredChoices)
        {
            choices = offeredChoices != null ? new List<UpgradeDefinition>(offeredChoices) : null;
            if (choices == null || choices.Count == 0)
            {
                Hide();
                return;
            }

            EnsureEventSystem();
            BindCards();

            if (cards == null)
            {
                Hide();
                return;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                UpgradeDefinition choice = i < choices.Count ? choices[i] : null;
                string levelLabel = BuildLevelText(choice);
                cards[i].Bind(choice, levelLabel, upgradeManager != null && upgradeManager.CurrentRewardIsElite);
            }

            SetVisible(true);
        }

        public void Hide()
        {
            choices = null;
            SetVisible(false);
        }

        private void HandleUpgradeSelected(UpgradeDefinition selected)
        {
            Hide();
        }

        private void BindCards()
        {
            if (cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].Initialize(i, SelectChoice);
                }
            }
        }

        private void SelectChoice(int index)
        {
            upgradeManager?.SelectChoice(index);
        }

        private string BuildLevelText(UpgradeDefinition upgrade)
        {
            if (upgrade == null || upgradeManager == null)
            {
                return string.Empty;
            }

            int currentLevel = upgradeManager.GetUpgradeLevel(upgrade);
            int nextLevel = currentLevel + 1;

            if (!upgrade.HasMaxLevel)
            {
                return currentLevel <= 0 ? "NEW" : $"Lv.{currentLevel} -> Lv.{nextLevel}";
            }

            return $"Lv.{currentLevel} -> Lv.{nextLevel} / {upgrade.MaxLevel}";
        }

        private void SetVisible(bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.blocksRaycasts = visible;
            root.interactable = visible;

            if (hideRootGameObject)
            {
                root.gameObject.SetActive(visible);
            }
        }

        private bool IsVisible()
        {
            return root != null && root.alpha > 0.001f && root.interactable;
        }

        private void SetStaticLabels()
        {
            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            {
                titleText.text = "Upgrade Select";
            }

            if (hintText != null && string.IsNullOrWhiteSpace(hintText.text))
            {
                hintText.text = "Click a card or press 1 / 2 / 3";
            }
        }

        private void BuildFallbackUi()
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("Upgrade Choice Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Upgrade Choice Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();
            StretchToParent(rootObject.GetComponent<RectTransform>());

            Image backdrop = rootObject.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);

            titleText = CreateText(rootObject.transform, "Title", "Upgrade Select", 42, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 270f);
            titleRect.sizeDelta = new Vector2(900f, 70f);

            GameObject rowObject = CreateUiObject("Card Row", rootObject.transform);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, -10f);
            rowRect.sizeDelta = new Vector2(1180f, 410f);

            HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 26f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            cards = new UpgradeChoiceCardUI[3];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = BuildFallbackCard(rowObject.transform, i);
            }

            hintText = CreateText(rootObject.transform, "Hint", "Click a card or press 1 / 2 / 3", 22, FontStyles.Normal, TextAlignmentOptions.Center);
            RectTransform hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = new Vector2(0f, -270f);
            hintRect.sizeDelta = new Vector2(900f, 40f);
            hintText.color = new Color(0.78f, 0.88f, 0.95f, 0.9f);
        }

        private UpgradeChoiceCardUI BuildFallbackCard(Transform parent, int index)
        {
            GameObject cardObject = CreateUiObject($"Upgrade Card {index + 1}", parent);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(360f, 390f);

            Image cardImage = cardObject.AddComponent<Image>();
            cardImage.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            Outline cardOutline = cardObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.24f, 0.95f, 1f, 0.82f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            GameObject borderObject = CreateUiObject("Border", cardObject.transform);
            RectTransform borderRect = borderObject.GetComponent<RectTransform>();
            StretchToParent(borderRect);
            Image borderImage = borderObject.AddComponent<Image>();
            borderImage.color = Color.clear;
            borderImage.enabled = false;
            borderImage.raycastTarget = false;
            LayoutElement borderLayout = borderObject.AddComponent<LayoutElement>();
            borderLayout.ignoreLayout = true;

            GameObject accentObject = CreateUiObject("Accent", cardObject.transform);
            RectTransform accentRect = accentObject.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 6f);
            Image accentImage = accentObject.AddComponent<Image>();
            accentImage.color = new Color(0.24f, 0.95f, 1f, 1f);
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accentObject.AddComponent<LayoutElement>();
            accentLayout.ignoreLayout = true;

            Button button = cardObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.72f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.45f, 0.78f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            VerticalLayoutGroup layout = cardObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject topRow = CreateUiObject("Top Row", cardObject.transform);
            AddLayout(topRow, 312f, 58f);
            HorizontalLayoutGroup topRowLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topRowLayout.spacing = 12f;
            topRowLayout.childAlignment = TextAnchor.MiddleLeft;
            topRowLayout.childControlWidth = false;
            topRowLayout.childControlHeight = false;
            topRowLayout.childForceExpandWidth = false;
            topRowLayout.childForceExpandHeight = false;

            GameObject iconRoot = CreateUiObject("Icon Root", topRow.transform);
            AddLayout(iconRoot, 52f, 52f);
            Image iconBackground = iconRoot.AddComponent<Image>();
            iconBackground.color = new Color(0.24f, 0.95f, 1f, 0.2f);
            iconBackground.raycastTarget = false;

            GameObject iconObject = CreateUiObject("Icon", iconRoot.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(36f, 36f);
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            GameObject labelColumn = CreateUiObject("Label Column", topRow.transform);
            AddLayout(labelColumn, 248f, 52f);
            VerticalLayoutGroup labelLayout = labelColumn.AddComponent<VerticalLayoutGroup>();
            labelLayout.spacing = 2f;
            labelLayout.childAlignment = TextAnchor.MiddleLeft;
            labelLayout.childControlWidth = true;
            labelLayout.childControlHeight = false;
            labelLayout.childForceExpandWidth = true;
            labelLayout.childForceExpandHeight = false;

            TextMeshProUGUI categoryText = CreateText(labelColumn.transform, "Category", "공격", 18, FontStyles.Bold, TextAlignmentOptions.Left);
            categoryText.color = new Color(0.4f, 0.95f, 1f, 1f);
            AddLayout(categoryText.gameObject, 248f, 22f);

            TextMeshProUGUI indexText = CreateText(labelColumn.transform, "Index", $"CHOICE {index + 1}", 16, FontStyles.Bold, TextAlignmentOptions.Left);
            indexText.color = new Color(0.58f, 0.68f, 0.76f, 1f);
            AddLayout(indexText.gameObject, 248f, 20f);

            TextMeshProUGUI nameText = CreateText(cardObject.transform, "Name", "Upgrade", 28, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            nameText.color = Color.white;
            AddLayout(nameText.gameObject, 312f, 76f);

            TextMeshProUGUI levelText = CreateText(cardObject.transform, "Level", "NEW", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            levelText.color = new Color(0.88f, 0.75f, 0.35f, 1f);
            AddLayout(levelText.gameObject, 312f, 34f);

            TextMeshProUGUI descriptionText = CreateText(cardObject.transform, "Description", "Description", 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            descriptionText.color = new Color(0.78f, 0.86f, 0.92f, 1f);
            AddLayout(descriptionText.gameObject, 312f, 156f);

            UpgradeChoiceCardUI card = cardObject.AddComponent<UpgradeChoiceCardUI>();
            card.ConfigureBindings(
                button,
                nameText,
                levelText,
                descriptionText,
                iconImage,
                categoryText,
                borderImage,
                accentImage,
                iconBackground,
                cardOutline);
            return card;
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
            int size,
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

    }
}
