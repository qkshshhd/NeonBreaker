using UnityEngine;

namespace NeonBreaker.Dungeon
{
    public sealed class RoomTemplateDoor2D : MonoBehaviour
    {
        [SerializeField] private Collider2D blocker;
        [SerializeField] private Renderer[] visuals;
        [SerializeField] private bool hideVisualsWhenUnlocked = true;

        private void Awake()
        {
            if (blocker == null)
            {
                blocker = GetComponentInChildren<Collider2D>(true);
            }

            if (visuals == null || visuals.Length == 0)
            {
                visuals = GetComponentsInChildren<Renderer>(true);
            }
        }

        public void SetLocked(bool locked)
        {
            if (blocker != null)
            {
                blocker.enabled = locked;
            }

            if (!hideVisualsWhenUnlocked || visuals == null)
            {
                return;
            }

            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                {
                    visuals[i].enabled = locked;
                }
            }
        }
    }
}
