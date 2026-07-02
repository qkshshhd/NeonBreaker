using UnityEngine;

namespace NeonBreaker.Rooms
{
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GeneratedRoomTrigger : MonoBehaviour
    {
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private int roomIndex;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool logTriggerEvents;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        public void Initialize(RoomRunManager manager, int index)
        {
            runManager = manager;
            roomIndex = index;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            if (runManager == null)
            {
                Debug.LogError($"[GeneratedRoomTrigger] Room {roomIndex} entered, but RoomRunManager is missing.", this);
                return;
            }

            if (logTriggerEvents)
            {
                Debug.Log($"[GeneratedRoomTrigger] Player entered room trigger {roomIndex}.", this);
            }

            runManager.TryEnterRoom(roomIndex);
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
