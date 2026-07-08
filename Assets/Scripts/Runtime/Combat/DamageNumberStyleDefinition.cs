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
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.75f, 0f);
        [SerializeField, Min(0f)] private float randomHorizontalOffset = 0.35f;

        [Header("Text")]
        [SerializeField] private string normalFormat = "{0}";
        [SerializeField] private string criticalFormat = "{0}!";
        [SerializeField] private FontStyles normalFontStyle = FontStyles.Normal;
        [SerializeField] private FontStyles criticalFontStyle = FontStyles.Bold;
        [SerializeField, Min(0.1f)] private float fontSize = 4.2f;
        [SerializeField, Min(0.1f)] private float criticalFontSizeMultiplier = 1.12f;
        [SerializeField] private bool useOutline = true;
        [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.18f;
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.92f);

        [Header("Look")]
        [SerializeField] private Color normalColor = new Color(0.86f, 0.96f, 1f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.78f, 0.22f, 1f);
        [SerializeField, Min(0.1f)] private float normalScale = 1.35f;
        [SerializeField, Min(0.1f)] private float criticalScale = 1.7f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 180;

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.82f;
        [SerializeField, Min(0f)] private float riseDistance = 1.05f;
        [SerializeField, Min(0f)] private float horizontalDrift = 0.28f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1.22f, 1f, 0.9f);

        public DamageNumberPopup2D PopupPrefab => popupPrefab;
        public bool ShowCriticalNumbers => showCriticalNumbers;
        public float MinAmountToShow => minAmountToShow;
        public Vector3 SpawnOffset => spawnOffset;
        public float RandomHorizontalOffset => randomHorizontalOffset;
        public Color NormalColor => normalColor;
        public Color CriticalColor => criticalColor;
        public float FontSize => fontSize;
        public float CriticalFontSizeMultiplier => criticalFontSizeMultiplier;
        public bool UseOutline => useOutline;
        public float OutlineWidth => outlineWidth;
        public Color OutlineColor => outlineColor;
        public float NormalScale => normalScale;
        public float CriticalScale => criticalScale;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;
        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public float RiseDistance => Mathf.Max(0f, riseDistance);
        public float HorizontalDrift => Mathf.Max(0f, horizontalDrift);
        public AnimationCurve AlphaCurve => alphaCurve;
        public AnimationCurve ScaleCurve => scaleCurve;

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
