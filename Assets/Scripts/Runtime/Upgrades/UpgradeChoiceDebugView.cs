using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Upgrades
{
    public sealed class UpgradeChoiceDebugView : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private Rect windowRect = new Rect(0f, 0f, 420f, 280f);

        private IReadOnlyList<UpgradeDefinition> choices;
        private bool visible;
        private const int WindowId = 30101;

        private void Awake()
        {
            if (FindAnyObjectByType<UpgradeChoiceUI>() != null)
            {
                enabled = false;
                return;
            }

            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }
        }

        private void OnEnable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.ChoicesOffered += HandleChoicesOffered;
                upgradeManager.UpgradeSelected += HandleUpgradeSelected;
            }
        }

        private void OnDisable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.ChoicesOffered -= HandleChoicesOffered;
                upgradeManager.UpgradeSelected -= HandleUpgradeSelected;
            }
        }

        private void Update()
        {
            if (!visible || upgradeManager == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                upgradeManager.SelectChoice(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                upgradeManager.SelectChoice(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                upgradeManager.SelectChoice(2);
            }
        }

        private void OnGUI()
        {
            if (!visible || choices == null)
            {
                return;
            }

            windowRect.x = (Screen.width - windowRect.width) * 0.5f;
            windowRect.y = (Screen.height - windowRect.height) * 0.5f;
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "Choose Upgrade");
        }

        private void DrawWindow(int windowId)
        {
            IReadOnlyList<UpgradeDefinition> visibleChoices = choices;
            if (visibleChoices == null || upgradeManager == null)
            {
                return;
            }

            GUILayout.Space(8f);

            for (int i = 0; i < visibleChoices.Count; i++)
            {
                UpgradeDefinition choice = visibleChoices[i];
                if (choice == null)
                {
                    continue;
                }

                GUILayout.BeginVertical("box");
                GUILayout.Label($"{i + 1}. {choice.DisplayName}");
                GUILayout.Label(choice.Description);
                if (GUILayout.Button("Select"))
                {
                    upgradeManager.SelectChoice(i);
                    GUILayout.EndVertical();
                    return;
                }
                GUILayout.EndVertical();
            }
        }

        private void HandleChoicesOffered(IReadOnlyList<UpgradeDefinition> offeredChoices)
        {
            choices = offeredChoices != null ? new List<UpgradeDefinition>(offeredChoices) : null;
            visible = true;
        }

        private void HandleUpgradeSelected(UpgradeDefinition selected)
        {
            visible = false;
            choices = null;
        }
    }
}
