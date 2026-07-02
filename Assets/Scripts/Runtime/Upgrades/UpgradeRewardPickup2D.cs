using TMPro;
using UnityEngine;

namespace NeonBreaker.Upgrades
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class UpgradeRewardPickup2D : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoOpenOnTouch = true;
        [SerializeField] private bool requireInteractKey;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private TextMeshPro promptText;
        [SerializeField] private string promptMessage = "E";
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 4f;

        private Vector3 visualStartLocalPosition;
        private bool playerInRange;
        private bool opened;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            visualStartLocalPosition = visualRoot.transform.localPosition;
            SetPromptVisible(false);
        }

        private void Update()
        {
            AnimateVisual();

            if (!requireInteractKey || !playerInRange || opened)
            {
                return;
            }

            if (Input.GetKeyDown(interactKey))
            {
                Open();
            }
        }

        public void Initialize(UpgradeManager manager)
        {
            upgradeManager = manager;
        }

        public void ConfigureRuntime(
            UpgradeManager manager,
            bool openOnTouch,
            bool needsInteractKey,
            TextMeshPro prompt)
        {
            upgradeManager = manager;
            autoOpenOnTouch = openOnTouch;
            requireInteractKey = needsInteractKey;
            promptText = prompt;
            SetPromptVisible(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other) || opened)
            {
                return;
            }

            playerInRange = true;
            SetPromptVisible(requireInteractKey);

            if (autoOpenOnTouch && !requireInteractKey)
            {
                Open();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerInRange = false;
            SetPromptVisible(false);
        }

        private void Open()
        {
            if (opened || upgradeManager == null || !upgradeManager.HasActiveChoices)
            {
                return;
            }

            opened = true;
            SetPromptVisible(false);
            upgradeManager.ShowPreparedChoices();
            gameObject.SetActive(false);
        }

        private void AnimateVisual()
        {
            if (visualRoot == null || bobAmplitude <= 0f || bobSpeed <= 0f)
            {
                return;
            }

            float offset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            visualRoot.transform.localPosition = visualStartLocalPosition + Vector3.up * offset;
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = promptMessage;
            promptText.gameObject.SetActive(visible);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.CompareTag(playerTag))
            {
                return true;
            }

            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            {
                return true;
            }

            Transform current = other.transform.parent;
            while (current != null)
            {
                if (current.CompareTag(playerTag))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
