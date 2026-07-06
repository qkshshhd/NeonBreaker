using NeonBreaker.Dungeon;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;

namespace NeonBreaker.Upgrades
{
    public sealed class RoomRewardSpawner : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;
        [SerializeField] private UpgradeRewardPickup2D rewardPrefab;
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;
        [SerializeField] private bool disableImmediateUpgradeChoice = true;
        [SerializeField] private bool destroyPreviousReward = true;
        [SerializeField] private bool fallbackAutoOpenOnTouch = true;
        [SerializeField] private bool fallbackRequireInteractKey;
        [SerializeField] private string fallbackLabel = "UPGRADE";

        private UpgradeRewardPickup2D activeReward;

        public UpgradeRewardPickup2D ActiveReward => activeReward;

        private void Awake()
        {
            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }

            if (disableImmediateUpgradeChoice && upgradeManager != null)
            {
                upgradeManager.SetOfferChoicesImmediately(false);
            }
        }

        private void OnEnable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.RewardChoicesPrepared += HandleRewardChoicesPrepared;
                upgradeManager.UpgradeSelected += HandleUpgradeSelected;
            }
        }

        private void OnDisable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.RewardChoicesPrepared -= HandleRewardChoicesPrepared;
                upgradeManager.UpgradeSelected -= HandleUpgradeSelected;
            }
        }

        private void HandleRewardChoicesPrepared(int roomIndex, RoomDefinition room)
        {
            if (destroyPreviousReward)
            {
                ClearActiveReward();
            }

            Vector3 spawnPosition = GetRewardPosition(roomIndex) + spawnOffset;
            activeReward = SpawnReward(spawnPosition);
            activeReward.Initialize(upgradeManager);
        }

        private void HandleUpgradeSelected(UpgradeDefinition selected)
        {
            ClearActiveReward();
        }

        private Vector3 GetRewardPosition(int roomIndex)
        {
            if (dungeonGenerator != null)
            {
                return dungeonGenerator.GetRoomRewardWorldPosition(roomIndex);
            }

            return transform.position;
        }

        private UpgradeRewardPickup2D SpawnReward(Vector3 position)
        {
            if (rewardPrefab != null)
            {
                UpgradeRewardPickup2D reward = Instantiate(rewardPrefab, position, Quaternion.identity);
                reward.transform.SetParent(transform, true);
                return reward;
            }

            return BuildFallbackReward(position);
        }

        private UpgradeRewardPickup2D BuildFallbackReward(Vector3 position)
        {
            GameObject rewardObject = new GameObject("Upgrade Reward Pickup");
            rewardObject.transform.SetParent(transform, true);
            rewardObject.transform.position = position;

            CircleCollider2D trigger = rewardObject.AddComponent<CircleCollider2D>();
            trigger.radius = 0.8f;
            trigger.isTrigger = true;

            GameObject labelObject = new GameObject("Reward Label");
            labelObject.transform.SetParent(rewardObject.transform, false);
            labelObject.transform.localPosition = Vector3.up * 0.35f;

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = fallbackLabel;
            label.fontSize = 3.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.35f, 0.95f, 1f, 1f);

            GameObject promptObject = new GameObject("Interact Prompt");
            promptObject.transform.SetParent(rewardObject.transform, false);
            promptObject.transform.localPosition = Vector3.down * 0.55f;

            TextMeshPro prompt = promptObject.AddComponent<TextMeshPro>();
            prompt.text = "E";
            prompt.fontSize = 2.2f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = new Color(1f, 0.9f, 0.35f, 1f);

            UpgradeRewardPickup2D rewardPickup = rewardObject.AddComponent<UpgradeRewardPickup2D>();
            SetPrivateFallbackOptions(rewardPickup, prompt);
            return rewardPickup;
        }

        private void SetPrivateFallbackOptions(UpgradeRewardPickup2D rewardPickup, TextMeshPro prompt)
        {
            if (rewardPickup == null)
            {
                return;
            }

            rewardPickup.ConfigureRuntime(
                upgradeManager,
                fallbackAutoOpenOnTouch,
                fallbackRequireInteractKey,
                prompt);
        }

        private void ClearActiveReward()
        {
            if (activeReward == null)
            {
                return;
            }

            Destroy(activeReward.gameObject);
            activeReward = null;
        }
    }
}
