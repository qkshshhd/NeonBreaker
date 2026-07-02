using UnityEngine;

namespace NeonBreaker.Rooms
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RoomExit : MonoBehaviour
    {
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private GameObject visualRoot;

        private bool isUnlocked;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;

            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }
        }

        private void Start()
        {
            SetUnlocked(false);
        }

        public void SetRunManager(RoomRunManager manager)
        {
            runManager = manager;
        }

        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;

            if (visualRoot != null)
            {
                visualRoot.SetActive(unlocked);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isUnlocked || runManager == null || !other.CompareTag(playerTag))
            {
                return;
            }

            runManager.TryEnterNextRoom();
        }
    }
}

