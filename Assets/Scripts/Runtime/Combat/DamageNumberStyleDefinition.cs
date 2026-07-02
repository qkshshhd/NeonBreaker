using TMPro;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [CreateAssetMenu(menuName = "Neon Breaker/Combat/Damage Number Style")]
    public sealed class DamageNumberStyleDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private DamageNumberPopup2D popupPrefab;

        [Header("Rules")]
        [SerializeField] private bool showCriticalNumbers = true;
        [SerializeField, Min(0f)] private float minAmountToShow = 0.5f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.55f, 0f);
        [SerializeField, Min(0f)] private float randomHorizontalOffset = 0.25f;

        [Header("Text")]
        [SerializeField] private string normalFormat = "{0}";
        [SerializeField] private string criticalFormat = "{0}!";
        [SerializeField] private FontStyles normalFontStyle = FontStyles.Normal;
        [SerializeField] private FontStyles criticalFontStyle = FontStyles.Bold;

        [Header("Look")]
        [SerializeField] private Color normalColor = new Color(0.86f, 0.96f, 1f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.78f, 0.22f, 1f);
        [SerializeField, Min(0.1f)] private float normalScale = 1f;
        [SerializeField, Min(0.1f)] private float criticalScale = 1.25f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 80;

        public DamageNumberPopup2D PopupPrefab => popupPrefab;
        public bool ShowCriticalNumbers => showCriticalNumbers;
        public float MinAmountToShow => minAmountToShow;
        public Vector3 SpawnOffset => spawnOffset;
        public float RandomHorizontalOffset => randomHorizontalOffset;
        public Color NormalColor => normalColor;
        public Color CriticalColor => criticalColor;
        public float NormalScale => normalScale;
        public float CriticalScale => criticalScale;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;

        public string FormatAmount(float amount, bool isCritical)
        {
            int roundedAmount = Mathf.CeilToInt(amount);
            string format = isCritical ? criticalFormat : normalFormat;
            return string.IsNullOrWhiteSpace(format) ? roundedAmount.ToString() : string.Format(format, roundedAmount);
        }

        public FontStyles GetFontStyle(bool isCritical)
        {
            return isCritical ? criticalFontStyle : normalFontStyle;
        }
    }
}
