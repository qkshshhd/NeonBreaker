using NeonBreaker.Upgrades;
using TMPro;
using UnityEngine;

namespace NeonBreaker.UI
{
    public sealed class UpgradeBuildListItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private GameObject maxLevelRoot;

        public void AssignBindings(TextMeshProUGUI nameLabel, TextMeshProUGUI descriptionLabel)
        {
            nameText = nameLabel;
            descriptionText = descriptionLabel;
        }

        public void Bind(UpgradeManager.UpgradeRecord record)
        {
            UpgradeDefinition definition = record.Definition;
            if (definition == null)
            {
                SetText(nameText, "Unknown Upgrade");
                SetText(descriptionText, string.Empty);
                SetMaxVisible(false);
                return;
            }

            string levelText = record.Level > 0 ? $" Lv.{record.Level}" : string.Empty;
            SetText(nameText, $"{definition.DisplayName}{levelText}");
            SetText(descriptionText, definition.Description);
            SetMaxVisible(definition.HasMaxLevel && record.Level >= definition.MaxLevel);
        }

        private void SetMaxVisible(bool visible)
        {
            if (maxLevelRoot != null)
            {
                maxLevelRoot.SetActive(visible);
            }
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
