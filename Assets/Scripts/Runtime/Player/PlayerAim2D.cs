using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerAim2D : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot = null;
        [SerializeField] private Transform weaponPivot = null;

        private PlayerInputReader input;

        public Vector2 AimDirection => input != null ? input.AimDirection : Vector2.right;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
        }

        private void LateUpdate()
        {
            Vector2 aimDirection = input.AimDirection;
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (visualRoot != null)
            {
                visualRoot.right = aimDirection;
            }

            if (weaponPivot != null)
            {
                weaponPivot.right = aimDirection;
            }
        }
    }
}
