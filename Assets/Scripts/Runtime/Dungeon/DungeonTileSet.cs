using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Dungeon
{
    [CreateAssetMenu(menuName = "Neon Breaker/Dungeon/Dungeon Tile Set")]
    public sealed class DungeonTileSet : ScriptableObject
    {
        [Serializable]
        public sealed class WeightedSprite
        {
            [SerializeField] private Sprite sprite;
            [SerializeField, Min(0f)] private float weight = 1f;

            [NonSerialized] private Tile runtimeTile;

            public bool IsValid => sprite != null && weight > 0f;
            public float Weight => Mathf.Max(0f, weight);

            public TileBase GetOrCreateTile(Tile.ColliderType colliderType)
            {
                if (sprite == null)
                {
                    return null;
                }

                if (runtimeTile == null || runtimeTile.sprite != sprite || runtimeTile.colliderType != colliderType)
                {
                    runtimeTile = CreateInstance<Tile>();
                    runtimeTile.name = $"{sprite.name}_RuntimeTile";
                    runtimeTile.sprite = sprite;
                    runtimeTile.colliderType = colliderType;
                }

                return runtimeTile;
            }
        }

        [Header("Sprite Variants")]
        [SerializeField] private WeightedSprite[] floorSprites;
        [SerializeField] private WeightedSprite[] wallSprites;
        [SerializeField] private WeightedSprite[] doorSprites;

        public bool HasRequiredSprites => HasAnyValidSprite(floorSprites) && HasAnyValidSprite(wallSprites);

        public TileBase GetRandomFloorTile()
        {
            return GetRandomTile(floorSprites, Tile.ColliderType.None);
        }

        public TileBase GetRandomWallTile()
        {
            return GetRandomTile(wallSprites, Tile.ColliderType.Grid);
        }

        public TileBase GetRandomDoorTile()
        {
            TileBase doorTile = GetRandomTile(doorSprites, Tile.ColliderType.Grid);
            return doorTile != null ? doorTile : GetRandomWallTile();
        }

        private static TileBase GetRandomTile(WeightedSprite[] variants, Tile.ColliderType colliderType)
        {
            if (variants == null || variants.Length == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null && variants[i].IsValid)
                {
                    totalWeight += variants[i].Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float pick = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < variants.Length; i++)
            {
                WeightedSprite variant = variants[i];
                if (variant == null || !variant.IsValid)
                {
                    continue;
                }

                pick -= variant.Weight;
                if (pick <= 0f)
                {
                    return variant.GetOrCreateTile(colliderType);
                }
            }

            return GetLastValidTile(variants, colliderType);
        }

        private static TileBase GetLastValidTile(WeightedSprite[] variants, Tile.ColliderType colliderType)
        {
            for (int i = variants.Length - 1; i >= 0; i--)
            {
                if (variants[i] != null && variants[i].IsValid)
                {
                    return variants[i].GetOrCreateTile(colliderType);
                }
            }

            return null;
        }

        private static bool HasAnyValidSprite(WeightedSprite[] variants)
        {
            if (variants == null)
            {
                return false;
            }

            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null && variants[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
