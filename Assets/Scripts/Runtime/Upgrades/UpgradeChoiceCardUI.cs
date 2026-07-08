using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.Upgrades
{
    public enum UpgradeCardCategory
    {
        Attack,
        Skill,
        Mobility,
        Survival,
        Utility
    }

    public sealed class UpgradeChoiceCardUI : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Button selectButton;
        [SerializeField] private bool clickWholeCardToSelect = true;
        [SerializeField] private bool hideLegacySelectButton = true;
        [SerializeField] private Image borderImage;
        [SerializeField] private Outline borderOutline;
        [SerializeField] private Image accentImage;

        [Header("Content")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image iconBackgroundImage;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private GameObject emptyRoot;

        [Header("Normal Style")]
        [SerializeField] private Sprite normalBackgroundSprite;
        [SerializeField] private Sprite normalBorderSprite;
        [SerializeField] private Color normalBackgroundColor = new Color(0.07f, 0.08f, 0.12f, 0.96f);
        [SerializeField] private Color normalBorderColor = new Color(0.24f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color normalAccentColor = new Color(0.24f, 0.95f, 1f, 1f);

        [Header("Elite Style")]
        [SerializeField] private Sprite eliteBackgroundSprite;
        [SerializeField] private Sprite eliteBorderSprite;
        [SerializeField] private Color eliteBackgroundColor = new Color(0.12f, 0.07f, 0.11f, 0.98f);
        [SerializeField] private Color eliteBorderColor = new Color(1f, 0.46f, 0.82f, 1f);
        [SerializeField] private Color eliteAccentColor = new Color(1f, 0.78f, 0.25f, 1f);

        [Header("Auto Icons")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite skillIcon;
        [SerializeField] private Sprite mobilityIcon;
        [SerializeField] private Sprite survivalIcon;
        [SerializeField] private Sprite utilityIcon;
        [SerializeField] private bool forceIconImageSettings = true;
        [SerializeField] private Color attackColor = new Color(1f, 0.25f, 0.45f, 1f);
        [SerializeField] private Color skillColor = new Color(0.55f, 0.35f, 1f, 1f);
        [SerializeField] private Color mobilityColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color survivalColor = new Color(0.2f, 1f, 0.56f, 1f);
        [SerializeField] private Color utilityColor = new Color(1f, 0.82f, 0.28f, 1f);

        private static readonly Dictionary<UpgradeCardCategory, Sprite> GeneratedIcons = new Dictionary<UpgradeCardCategory, Sprite>();

        private Button cardButton;
        private Image backgroundImage;
        private int choiceIndex = -1;
        private Action<int> selected;

        private void Awake()
        {
            CacheBackgroundImage();
            EnsureCardButton();
            FindLegacySelectButton();
            ApplySelectionButtonMode();
        }

        private void OnEnable()
        {
            EnsureCardButton();
            ApplySelectionButtonMode();

            if (cardButton != null)
            {
                cardButton.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDisable()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Initialize(int index, Action<int> onSelected)
        {
            choiceIndex = index;
            selected = onSelected;
        }

        public void ConfigureBindings(
            Button button,
            TextMeshProUGUI upgradeName,
            TextMeshProUGUI upgradeLevel,
            TextMeshProUGUI upgradeDescription,
            Image upgradeIcon = null,
            TextMeshProUGUI upgradeCategory = null,
            Image cardBorder = null,
            Image cardAccent = null,
            Image upgradeIconBackground = null,
            Outline cardOutline = null)
        {
            selectButton = button;
            nameText = upgradeName;
            levelText = upgradeLevel;
            descriptionText = upgradeDescription;
            iconImage = upgradeIcon;
            categoryText = upgradeCategory;
            borderImage = cardBorder;
            accentImage = cardAccent;
            iconBackgroundImage = upgradeIconBackground;
            borderOutline = cardOutline;
            CacheBackgroundImage();
            EnsureCardButton();
            ApplySelectionButtonMode();
        }

        public void Bind(UpgradeDefinition upgrade, string levelLabel, bool isEliteReward = false)
        {
            bool hasUpgrade = upgrade != null;
            gameObject.SetActive(hasUpgrade);

            if (emptyRoot != null)
            {
                emptyRoot.SetActive(!hasUpgrade);
            }

            if (!hasUpgrade)
            {
                return;
            }

            SetText(nameText, upgrade.DisplayName);
            SetText(levelText, levelLabel);
            SetText(descriptionText, upgrade.Description);

            UpgradeCardCategory category = InferCategory(upgrade);
            ApplyCategory(category);
            ApplyRewardStyle(isEliteReward, category);

            if (iconImage != null)
            {
                Sprite icon = upgrade.Icon != null ? upgrade.Icon : GetIcon(category);
                ApplyIcon(iconImage, icon);
            }
        }

        private void HandleClicked()
        {
            if (choiceIndex < 0)
            {
                return;
            }

            selected?.Invoke(choiceIndex);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void ApplyRewardStyle(bool isEliteReward, UpgradeCardCategory category)
        {
            CacheBackgroundImage();

            if (backgroundImage != null)
            {
                Sprite backgroundSprite = isEliteReward ? eliteBackgroundSprite : normalBackgroundSprite;
                if (backgroundSprite != null)
                {
                    backgroundImage.sprite = backgroundSprite;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    backgroundImage.color = isEliteReward ? eliteBackgroundColor : normalBackgroundColor;
                }
            }

            if (borderImage != null)
            {
                Sprite borderSprite = isEliteReward ? eliteBorderSprite : normalBorderSprite;
                borderImage.sprite = borderSprite;
                borderImage.color = isEliteReward ? eliteBorderColor : normalBorderColor;
                borderImage.enabled = borderImage.sprite != null;
            }

            if (borderOutline != null)
            {
                borderOutline.effectColor = isEliteReward ? eliteBorderColor : normalBorderColor;
            }

            if (accentImage != null)
            {
                accentImage.color = isEliteReward ? eliteAccentColor : GetCategoryColor(category);
            }
        }

        private void CacheBackgroundImage()
        {
            if (backgroundImage != null)
            {
                return;
            }

            backgroundImage = GetComponent<Image>();

            if (backgroundImage == null && selectButton != null && selectButton.targetGraphic is Image targetImage)
            {
                backgroundImage = targetImage;
            }
        }

        private void EnsureCardButton()
        {
            if (!clickWholeCardToSelect)
            {
                cardButton = selectButton;
                return;
            }

            if (cardButton == null)
            {
                cardButton = GetComponent<Button>();
            }

            if (cardButton == null)
            {
                cardButton = gameObject.AddComponent<Button>();
            }

            CacheBackgroundImage();

            if (cardButton.targetGraphic == null && backgroundImage != null)
            {
                cardButton.targetGraphic = backgroundImage;
            }

            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = true;
            }
        }

        private void FindLegacySelectButton()
        {
            if (selectButton != null)
            {
                return;
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject != gameObject)
                {
                    selectButton = buttons[i];
                    return;
                }
            }

            selectButton = cardButton;
        }

        private void ApplySelectionButtonMode()
        {
            if (!clickWholeCardToSelect)
            {
                return;
            }

            if (selectButton != null && selectButton != cardButton && hideLegacySelectButton)
            {
                selectButton.onClick.RemoveListener(HandleClicked);
                selectButton.gameObject.SetActive(false);
            }
        }

        private void ApplyCategory(UpgradeCardCategory category)
        {
            SetText(categoryText, GetCategoryLabelSafe(category));

            Color categoryColor = GetCategoryColor(category);
            if (categoryText != null)
            {
                categoryText.color = categoryColor;
            }

            if (iconBackgroundImage != null)
            {
                iconBackgroundImage.color = new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.22f);
            }
        }

        private Sprite GetIcon(UpgradeCardCategory category)
        {
            Sprite assigned = category switch
            {
                UpgradeCardCategory.Attack => attackIcon,
                UpgradeCardCategory.Skill => skillIcon,
                UpgradeCardCategory.Mobility => mobilityIcon,
                UpgradeCardCategory.Survival => survivalIcon,
                _ => utilityIcon
            };

            if (assigned != null)
            {
                return assigned;
            }

            if (!GeneratedIcons.TryGetValue(category, out Sprite generated) || generated == null)
            {
                generated = CreateGeneratedIcon(category, GetCategoryColor(category));
                GeneratedIcons[category] = generated;
            }

            return generated;
        }

        private Color GetCategoryColor(UpgradeCardCategory category)
        {
            return category switch
            {
                UpgradeCardCategory.Attack => attackColor,
                UpgradeCardCategory.Skill => skillColor,
                UpgradeCardCategory.Mobility => mobilityColor,
                UpgradeCardCategory.Survival => survivalColor,
                _ => utilityColor
            };
        }

        private static string GetCategoryLabel(UpgradeCardCategory category)
        {
            return category switch
            {
                UpgradeCardCategory.Attack => "공격",
                UpgradeCardCategory.Skill => "스킬",
                UpgradeCardCategory.Mobility => "기동",
                UpgradeCardCategory.Survival => "생존",
                _ => "전술"
            };
        }

        private static string GetCategoryLabelSafe(UpgradeCardCategory category)
        {
            return category switch
            {
                UpgradeCardCategory.Attack => "공격",
                UpgradeCardCategory.Skill => "스킬",
                UpgradeCardCategory.Mobility => "기동",
                UpgradeCardCategory.Survival => "생존",
                _ => "전술"
            };
        }

        private void ApplyIcon(Image target, Sprite icon)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = icon;
            target.enabled = icon != null;
            if (icon == null)
            {
                return;
            }

            if (forceIconImageSettings)
            {
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                target.fillCenter = true;
                target.fillAmount = 1f;
                target.color = Color.white;
                target.raycastTarget = false;
            }

        }

        private static UpgradeCardCategory InferCategory(UpgradeDefinition upgrade)
        {
            UpgradeDefinition.UpgradeEffect[] effects = upgrade != null ? upgrade.Effects : null;
            if (effects == null || effects.Length == 0)
            {
                return UpgradeCardCategory.Utility;
            }

            int attack = 0;
            int skill = 0;
            int mobility = 0;
            int survival = 0;
            int utility = 0;

            for (int i = 0; i < effects.Length; i++)
            {
                UpgradeDefinition.UpgradeEffect effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.EffectType)
                {
                    case UpgradeEffectType.AddDamagePercent:
                    case UpgradeEffectType.ReduceAttackCooldownPercent:
                    case UpgradeEffectType.AddAttackRangePercent:
                    case UpgradeEffectType.AddAttackAnglePercent:
                    case UpgradeEffectType.AddCriticalChance:
                    case UpgradeEffectType.AddCriticalDamagePercent:
                    case UpgradeEffectType.AddKnockbackPercent:
                        attack += 2;
                        break;

                    case UpgradeEffectType.AddSkillDamagePercent:
                    case UpgradeEffectType.AddSkillRadiusPercent:
                    case UpgradeEffectType.ReduceSkillCooldownPercent:
                    case UpgradeEffectType.AddSkillShieldPerHit:
                        skill += 2;
                        break;

                    case UpgradeEffectType.AddMoveSpeedPercent:
                    case UpgradeEffectType.ReduceDashCooldownPercent:
                    case UpgradeEffectType.AddDashDistancePercent:
                    case UpgradeEffectType.EnableDashShockwave:
                    case UpgradeEffectType.AddDashShockwaveRadiusPercent:
                        mobility += 2;
                        break;

                    case UpgradeEffectType.AddMaxHealth:
                    case UpgradeEffectType.HealFlat:
                    case UpgradeEffectType.AddLifeStealPercent:
                    case UpgradeEffectType.AddHitInvulnerabilityDuration:
                        survival += 2;
                        break;

                    default:
                        utility++;
                        break;
                }
            }

            if (survival >= attack && survival >= skill && survival >= mobility && survival >= utility)
            {
                return UpgradeCardCategory.Survival;
            }

            if (skill >= attack && skill >= mobility && skill >= utility)
            {
                return UpgradeCardCategory.Skill;
            }

            if (mobility >= attack && mobility >= utility)
            {
                return UpgradeCardCategory.Mobility;
            }

            if (attack >= utility)
            {
                return UpgradeCardCategory.Attack;
            }

            return UpgradeCardCategory.Utility;
        }

        private static Sprite CreateGeneratedIcon(UpgradeCardCategory category, Color color)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Generated_{category}_UpgradeIcon"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            DrawIconPixels(pixels, size, category, color);
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            return sprite;
        }

        private static void DrawIconPixels(Color[] pixels, int size, UpgradeCardCategory category, Color color)
        {
            Color main = new Color(color.r, color.g, color.b, 1f);
            Color highlight = Color.Lerp(main, Color.white, 0.35f);
            Color outline = new Color(0.02f, 0.025f, 0.04f, 0.95f);

            switch (category)
            {
                case UpgradeCardCategory.Attack:
                    DrawLine(pixels, size, 8, 24, 23, 9, outline, 3);
                    DrawLine(pixels, size, 9, 23, 22, 10, main, 2);
                    DrawLine(pixels, size, 15, 16, 22, 9, highlight, 1);
                    DrawFilledRect(pixels, size, 6, 24, 13, 27, outline);
                    DrawFilledRect(pixels, size, 7, 25, 12, 26, main);
                    DrawFilledRect(pixels, size, 5, 27, 8, 30, outline);
                    DrawFilledRect(pixels, size, 6, 28, 7, 29, highlight);
                    break;

                case UpgradeCardCategory.Skill:
                    DrawDiamond(pixels, size, 16, 16, 12, outline);
                    DrawDiamond(pixels, size, 16, 16, 9, main);
                    DrawDiamond(pixels, size, 16, 16, 4, highlight);
                    SetPixel(pixels, size, 16, 7, highlight);
                    SetPixel(pixels, size, 25, 16, highlight);
                    break;

                case UpgradeCardCategory.Mobility:
                    DrawRightArrow(pixels, size, outline, 5, 8, 27, 24);
                    DrawRightArrow(pixels, size, main, 7, 11, 25, 21);
                    DrawFilledRect(pixels, size, 8, 14, 18, 16, highlight);
                    break;

                case UpgradeCardCategory.Survival:
                    DrawShield(pixels, size, outline, 16, 5, 12, 22);
                    DrawShield(pixels, size, main, 16, 8, 9, 17);
                    DrawFilledRect(pixels, size, 15, 11, 17, 21, highlight);
                    DrawFilledRect(pixels, size, 11, 15, 21, 17, highlight);
                    break;

                default:
                    DrawChip(pixels, size, outline, 8, 8, 23, 23);
                    DrawChip(pixels, size, main, 10, 10, 21, 21);
                    DrawFilledRect(pixels, size, 14, 14, 17, 17, highlight);
                    DrawFilledRect(pixels, size, 12, 12, 13, 13, highlight);
                    DrawFilledRect(pixels, size, 18, 18, 19, 19, highlight);
                    break;
            }
        }

        private static void DrawFilledRect(Color[] pixels, int size, int minX, int minY, int maxX, int maxY, Color color)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    SetPixel(pixels, size, x, y, color);
                }
            }
        }

        private static void DrawRightArrow(Color[] pixels, int size, Color color, int minX, int minY, int maxX, int maxY)
        {
            int centerY = (minY + maxY) / 2;
            int bodyRight = Mathf.Max(minX, maxX - 8);
            DrawFilledRect(pixels, size, minX, centerY - 3, bodyRight, centerY + 3, color);

            for (int x = bodyRight; x <= maxX; x++)
            {
                float t = maxX == bodyRight ? 1f : (float)(x - bodyRight) / (maxX - bodyRight);
                int arrowHalfHeight = Mathf.RoundToInt(Mathf.Lerp((maxY - minY) * 0.5f, 0f, t));
                for (int y = centerY - arrowHalfHeight; y <= centerY + arrowHalfHeight; y++)
                {
                    SetPixel(pixels, size, x, y, color);
                }
            }
        }

        private static void DrawShield(Color[] pixels, int size, Color color, int centerX, int topY, int halfWidth, int height)
        {
            for (int y = 0; y <= height; y++)
            {
                float t = height <= 0 ? 0f : (float)y / height;
                int currentHalfWidth = Mathf.RoundToInt(Mathf.Lerp(halfWidth, 1f, t * t));
                int rowY = topY + y;
                for (int x = centerX - currentHalfWidth; x <= centerX + currentHalfWidth; x++)
                {
                    SetPixel(pixels, size, x, rowY, color);
                }
            }
        }

        private static void DrawChip(Color[] pixels, int size, Color color, int minX, int minY, int maxX, int maxY)
        {
            DrawFilledRect(pixels, size, minX, minY, maxX, maxY, color);
            for (int offset = 3; offset <= maxX - minX - 3; offset += 4)
            {
                DrawFilledRect(pixels, size, minX + offset, minY - 2, minX + offset + 1, minY - 1, color);
                DrawFilledRect(pixels, size, minX + offset, maxY + 1, minX + offset + 1, maxY + 2, color);
            }

            for (int offset = 3; offset <= maxY - minY - 3; offset += 4)
            {
                DrawFilledRect(pixels, size, minX - 2, minY + offset, minX - 1, minY + offset + 1, color);
                DrawFilledRect(pixels, size, maxX + 1, minY + offset, maxX + 2, minY + offset + 1, color);
            }
        }

        private static void DrawDiamond(Color[] pixels, int size, int centerX, int centerY, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int halfWidth = radius - Mathf.Abs(y);
                for (int x = -halfWidth; x <= halfWidth; x++)
                {
                    SetPixel(pixels, size, centerX + x, centerY + y, color);
                }
            }
        }

        private static void DrawLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                DrawPoint(pixels, size, x0, y0, color, thickness);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void DrawPoint(Color[] pixels, int size, int x, int y, Color color, int thickness)
        {
            int radius = Mathf.Max(0, thickness - 1);
            for (int oy = -radius; oy <= radius; oy++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    SetPixel(pixels, size, x + ox, y + oy, color);
                }
            }
        }

        private static void SetPixel(Color[] pixels, int size, int x, int y, Color color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
            {
                return;
            }

            pixels[y * size + x] = color;
        }
    }
}
