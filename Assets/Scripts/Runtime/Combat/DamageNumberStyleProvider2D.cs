using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class DamageNumberStyleProvider2D : MonoBehaviour
    {
        private static DamageNumberStyleProvider2D instance;

        [SerializeField] private DamageNumberStyleDefinition defaultStyle;

        public static DamageNumberStyleDefinition DefaultStyle => instance != null ? instance.defaultStyle : null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[DamageNumberStyleProvider2D] Multiple providers were found. The newest provider will be used.", this);
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
