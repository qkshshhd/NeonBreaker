using TMPro;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class DamageNumberSpawner2D : MonoBehaviour
    {
        [SerializeField] private DamageNumberStyleDefinition style;
        [SerializeField] private DamageNumberPopup2D popupPrefab;
        [SerializeField] private bool buildFallbackPopupIfMissing = true;
        [SerializeField] private bool showCriticalNumbers = true;
        [SerializeField] private float minAmountToShow = 0.5f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.55f, 0f);
        [SerializeField] private float randomHorizontalOffset = 0.25f;
        [SerializeField] private Color normalColor = new Color(0.86f, 0.96f, 1f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.78f, 0.22f, 1f);
        [SerializeField, Min(0.1f)] private float normalScale = 1f;
        [SerializeField, Min(0.1f)] private float criticalScale = 1.25f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 80;

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(DamageInfo damage)
        {
            DamageNumberStyleDefinition activeStyle = GetStyle();

            if (damage.Amount < GetMinAmountToShow(activeStyle))
            {
                return;
            }

            if (damage.IsCritical && !GetShowCriticalNumbers(activeStyle))
            {
                return;
            }

            DamageNumberPopup2D popup = SpawnPopup(GetSpawnPosition(damage));
            if (popup == null)
            {
                return;
            }

            bool isCritical = damage.IsCritical;
            string value = FormatAmount(activeStyle, damage.Amount, isCritical);
            Color color = GetColor(activeStyle, isCritical);
            float scale = GetScale(activeStyle, isCritical);
            FontStyles fontStyle = GetFontStyle(activeStyle, isCritical);
            popup.Play(value, color, scale, GetSortingLayerName(activeStyle), GetSortingOrder(activeStyle), fontStyle);
        }

        public void ConfigureStyle(DamageNumberStyleDefinition newStyle)
        {
            style = newStyle;
        }

        private Vector3 GetSpawnPosition(DamageInfo damage)
        {
            DamageNumberStyleDefinition activeStyle = GetStyle();
            Vector3 activeSpawnOffset = GetSpawnOffset(activeStyle);
            float activeRandomOffset = GetRandomHorizontalOffset(activeStyle);
            Vector3 position = transform.position + activeSpawnOffset;

            if (damage.Point.sqrMagnitude > 0.0001f)
            {
                position = new Vector3(damage.Point.x, damage.Point.y, transform.position.z) + activeSpawnOffset;
            }

            position.x += Random.Range(-activeRandomOffset, activeRandomOffset);
            return position;
        }

        private DamageNumberPopup2D SpawnPopup(Vector3 position)
        {
            DamageNumberStyleDefinition activeStyle = GetStyle();
            DamageNumberPopup2D activePrefab = activeStyle != null && activeStyle.PopupPrefab != null
                ? activeStyle.PopupPrefab
                : popupPrefab;

            if (activePrefab != null)
            {
                return Instantiate(activePrefab, position, Quaternion.identity);
            }

            return buildFallbackPopupIfMissing ? BuildFallbackPopup(position) : null;
        }

        private DamageNumberStyleDefinition GetStyle()
        {
            if (style != null)
            {
                return style;
            }

            return DamageNumberStyleProvider2D.DefaultStyle;
        }

        private string FormatAmount(DamageNumberStyleDefinition activeStyle, float amount, bool isCritical)
        {
            return activeStyle != null
                ? activeStyle.FormatAmount(amount, isCritical)
                : isCritical
                    ? $"{Mathf.CeilToInt(amount)}!"
                    : Mathf.CeilToInt(amount).ToString();
        }

        private FontStyles GetFontStyle(DamageNumberStyleDefinition activeStyle, bool isCritical)
        {
            return activeStyle != null ? activeStyle.GetFontStyle(isCritical) : isCritical ? FontStyles.Bold : FontStyles.Normal;
        }

        private bool GetShowCriticalNumbers(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.ShowCriticalNumbers : showCriticalNumbers;
        }

        private float GetMinAmountToShow(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.MinAmountToShow : minAmountToShow;
        }

        private Vector3 GetSpawnOffset(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.SpawnOffset : spawnOffset;
        }

        private float GetRandomHorizontalOffset(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.RandomHorizontalOffset : randomHorizontalOffset;
        }

        private Color GetColor(DamageNumberStyleDefinition activeStyle, bool isCritical)
        {
            if (activeStyle == null)
            {
                return isCritical ? criticalColor : normalColor;
            }

            return isCritical ? activeStyle.CriticalColor : activeStyle.NormalColor;
        }

        private float GetScale(DamageNumberStyleDefinition activeStyle, bool isCritical)
        {
            if (activeStyle == null)
            {
                return isCritical ? criticalScale : normalScale;
            }

            return isCritical ? activeStyle.CriticalScale : activeStyle.NormalScale;
        }

        private string GetSortingLayerName(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.SortingLayerName : sortingLayerName;
        }

        private int GetSortingOrder(DamageNumberStyleDefinition activeStyle)
        {
            return activeStyle != null ? activeStyle.SortingOrder : sortingOrder;
        }

        private static DamageNumberPopup2D BuildFallbackPopup(Vector3 position)
        {
            GameObject popupObject = new GameObject("Damage Number Popup");
            popupObject.transform.position = position;

            TextMeshPro text = popupObject.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 2.4f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            return popupObject.AddComponent<DamageNumberPopup2D>();
        }
    }
}
