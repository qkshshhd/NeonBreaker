using System.Collections.Generic;
using NeonBreaker.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class UpgradeBuildPanelUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private UpgradeManager upgradeManager;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private UpgradeBuildListItemUI listItemPrefab;

        [Header("Options")]
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool startVisible;
        [SerializeField] private KeyCode toggleKey = KeyCode.B;
        [SerializeField] private string title = "BUILD";
        [SerializeField] private string emptyMessage = "No upgrades acquired.";
        [SerializeField] private bool pauseWhenOpen;

        private readonly List<UpgradeBuildListItemUI> spawnedItems = new List<UpgradeBuildListItemUI>();
        private bool visible;
        private float previousTimeScale = 1f;
        private bool pausedByPanel;

        private void Awake()
        {
            ResolveSources();

            if (buildFallbackUiIfMissing && root == null)
            {
                BuildFallbackUi();
            }

            SetVisible(startVisible);
            Refresh();
        }

        private void OnEnable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.AcquiredUpgradesChanged += HandleAcquiredUpgradesChanged;
            }
        }

        private void OnDisable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.AcquiredUpgradesChanged -= HandleAcquiredUpgradesChanged;
            }

            RestoreTimeScale();
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                SetVisible(!visible);
            }
        }

        public void Refresh()
        {
            ResolveSources();

            IReadOnlyList<UpgradeManager.UpgradeRecord> records = upgradeManager != null
                ? upgradeManager.AcquiredUpgrades
                : null;

            SetText(titleText, title);
            SetText(summaryText, records != null ? $"UPGRADES {records.Count}" : "UPGRADES 0");
            RebuildList(records);
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;

            if (root != null)
            {
                root.alpha = visible ? 1f : 0f;
                root.interactable = visible;
                root.blocksRaycasts = visible;
            }

            if (visible)
            {
                Refresh();
                PauseTimeScale();
            }
            else
            {
                RestoreTimeScale();
            }
        }

        private void HandleAcquiredUpgradesChanged(IReadOnlyList<UpgradeManager.UpgradeRecord> records)
        {
            Refresh();
        }

        private void RebuildList(IReadOnlyList<UpgradeManager.UpgradeRecord> records)
        {
            ClearItems();

            bool hasRecords = records != null && records.Count > 0;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(!hasRecords);
                emptyText.text = emptyMessage;
            }

            if (!hasRecords || listRoot == null || listItemPrefab == null)
            {
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                UpgradeBuildListItemUI item = Instantiate(listItemPrefab, listRoot);
                item.gameObject.SetActive(true);
                item.Bind(records[i]);
                spawnedItems.Add(item);
            }
        }

        private void ClearItems()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null)
                {
                    Destroy(spawnedItems[i].gameObject);
                }
            }

            spawnedItems.Clear();
        }

        private void ResolveSources()
        {
            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }
        }

        private void PauseTimeScale()
        {
            if (!pauseWhenOpen || pausedByPanel)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByPanel = true;
        }

        private void RestoreTimeScale()
        {
            if (!pausedByPanel)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            pausedByPanel = false;
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Upgrade Build Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 245;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Upgrade Build Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0.5f);
            rootRect.anchoredPosition = new Vector2(32f, 0f);
            rootRect.sizeDelta = new Vector2(430f, 620f);

            Image panel = rootObject.AddComponent<Image>();
            panel.color = new Color(0.025f, 0.035f, 0.055f, 0.86f);

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleText = CreateText(rootObject.transform, "Title", title, 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            summaryText = CreateText(rootObject.transform, "Summary", "UPGRADES 0", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            emptyText = CreateText(rootObject.transform, "Empty", emptyMessage, 18f, FontStyles.Italic, TextAlignmentOptions.Left);

            GameObject listObject = CreateUiObject("List Root", rootObject.transform);
            listRoot = listObject.GetComponent<RectTransform>();
            VerticalLayoutGroup listLayout = listObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            listItemPrefab = BuildFallbackItemPrefab(listRoot);
        }

        private static UpgradeBuildListItemUI BuildFallbackItemPrefab(Transform parent)
        {
            GameObject itemObject = CreateUiObject("Build List Item Prefab", parent);
            itemObject.SetActive(false);

            Image background = itemObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.11f, 0.15f, 0.88f);

            VerticalLayoutGroup layout = itemObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement itemLayout = itemObject.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = 76f;

            TextMeshProUGUI nameText = CreateText(itemObject.transform, "Name", "Upgrade Lv.1", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            TextMeshProUGUI descriptionText = CreateText(itemObject.transform, "Description", "Description", 14f, FontStyles.Normal, TextAlignmentOptions.Left);

            UpgradeBuildListItemUI item = itemObject.AddComponent<UpgradeBuildListItemUI>();
            item.AssignBindings(nameText, descriptionText);
            return item;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.88f, 0.95f, 1f, 0.94f);
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
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
