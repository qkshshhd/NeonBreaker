using UnityEngine;

namespace NeonBreaker.UI
{
    public sealed class FaceCamera2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            transform.rotation = targetCamera.transform.rotation;
        }
    }
}

