using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.Upgrades
{
    public sealed class UpgradeChoiceCardUI : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private GameObject emptyRoot;

        private int choiceIndex = -1;
        private Action<int> selected;

        private void Awake()
        {
            if (selectButton == null)
            {
                selectButton = GetComponent<Button>();
            }

            if (selectButton == null)
            {
                selectButton = GetComponentInChildren<Button>(true);
            }
        }

        private void OnEnable()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDisable()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Initialize(int index, Action<int> onSelected)
        {
            choiceIndex = index;
            selected = onSelected;
        }

        public void ConfigureBindings(
            Button button,
            TextMeshProUGUI upgradeName,
            TextMeshProUGUI upgradeLevel,
            TextMeshProUGUI upgradeDescription,
            Image upgradeIcon = null)
        {
            selectButton = button;
            nameText = upgradeName;
            levelText = upgradeLevel;
            descriptionText = upgradeDescription;
            iconImage = upgradeIcon;
        }

        public void Bind(UpgradeDefinition upgrade, string levelLabel)
        {
            bool hasUpgrade = upgrade != null;
            gameObject.SetActive(hasUpgrade);

            if (emptyRoot != null)
            {
                emptyRoot.SetActive(!hasUpgrade);
            }

            if (!hasUpgrade)
            {
                return;
            }

            SetText(nameText, upgrade.DisplayName);
            SetText(levelText, levelLabel);
            SetText(descriptionText, upgrade.Description);

            if (iconImage != null)
            {
                iconImage.sprite = upgrade.Icon;
                iconImage.enabled = upgrade.Icon != null;
            }
        }

        private void HandleClicked()
        {
            if (choiceIndex < 0)
            {
                return;
            }

            selected?.Invoke(choiceIndex);
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
