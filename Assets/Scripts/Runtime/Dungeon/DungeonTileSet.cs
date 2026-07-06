using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Dungeon
{
    [CreateAssetMenu(menuName = "Neon Breaker/Dungeon/Dungeon Tile Set")]
    public sealed class DungeonTileSet : ScriptableObject
    {
        public enum OutlineDirection
        {
            North,
            South,
            East,
            West,
            NorthEast,
            NorthWest,
            SouthEast,
            SouthWest,
            OuterNorthEast,
            OuterNorthWest,
            OuterSouthEast,
            OuterSouthWest
        }

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
                    runtimeTile.flags = TileFlags.None;
                }

                return runtimeTile;
            }
        }

        [Header("Sprite Variants")]
        [SerializeField] private WeightedSprite[] floorSprites;
        [SerializeField] private WeightedSprite[] wallSprites;
        [SerializeField] private WeightedSprite[] doorSprites;
        [SerializeField] private WeightedSprite[] decorationSprites;

        [Header("Outline Sprites")]
        [SerializeField] private WeightedSprite[] outlineNorthSprites;
        [SerializeField] private WeightedSprite[] outlineSouthSprites;
        [SerializeField] private WeightedSprite[] outlineEastSprites;
        [SerializeField] private WeightedSprite[] outlineWestSprites;
        [SerializeField] private WeightedSprite[] outlineNorthEastSprites;
        [SerializeField] private WeightedSprite[] outlineNorthWestSprites;
        [SerializeField] private WeightedSprite[] outlineSouthEastSprites;
        [SerializeField] private WeightedSprite[] outlineSouthWestSprites;

        [Header("Outer Corner Outline Sprites")]
        [SerializeField] private WeightedSprite[] outlineOuterNorthEastSprites;
        [SerializeField] private WeightedSprite[] outlineOuterNorthWestSprites;
        [SerializeField] private WeightedSprite[] outlineOuterSouthEastSprites;
        [SerializeField] private WeightedSprite[] outlineOuterSouthWestSprites;

        public bool HasRequiredSprites => HasAnyValidSprite(floorSprites) && (HasAnyValidSprite(wallSprites) || HasAnyOutlineSprite());

        public TileBase GetRandomFloorTile()
        {
            return GetRandomTile(floorSprites, Tile.ColliderType.None);
        }

        public TileBase GetRandomWallTile()
        {
            return GetRandomTile(wallSprites, Tile.ColliderType.Grid);
        }

        public TileBase GetRandomOutlineTile(OutlineDirection direction)
        {
            TileBase outlineTile = GetRandomOutlineTileOnly(direction);
            if (outlineTile != null)
            {
                return outlineTile;
            }

            TileBase fallbackTile = GetRandomFallbackOutlineTile(direction);
            if (fallbackTile != null)
            {
                return fallbackTile;
            }

            TileBase wallTile = GetRandomWallTile();
            return wallTile != null ? wallTile : GetRandomAnyOutlineTile();
        }

        public TileBase GetRandomDoorTile()
        {
            TileBase doorTile = GetRandomTile(doorSprites, Tile.ColliderType.Grid);
            return doorTile != null ? doorTile : GetRandomWallTile();
        }

        public TileBase GetRandomDecorationTile()
        {
            return GetRandomTile(decorationSprites, Tile.ColliderType.None);
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

        private WeightedSprite[] GetOutlineSprites(OutlineDirection direction)
        {
            switch (direction)
            {
                case OutlineDirection.North:
                    return outlineNorthSprites;
                case OutlineDirection.South:
                    return outlineSouthSprites;
                case OutlineDirection.East:
                    return outlineEastSprites;
                case OutlineDirection.West:
                    return outlineWestSprites;
                case OutlineDirection.NorthEast:
                    return outlineNorthEastSprites;
                case OutlineDirection.NorthWest:
                    return outlineNorthWestSprites;
                case OutlineDirection.SouthEast:
                    return outlineSouthEastSprites;
                case OutlineDirection.SouthWest:
                    return outlineSouthWestSprites;
                case OutlineDirection.OuterNorthEast:
                    return outlineOuterNorthEastSprites;
                case OutlineDirection.OuterNorthWest:
                    return outlineOuterNorthWestSprites;
                case OutlineDirection.OuterSouthEast:
                    return outlineOuterSouthEastSprites;
                case OutlineDirection.OuterSouthWest:
                    return outlineOuterSouthWestSprites;
                default:
                    return wallSprites;
            }
        }

        private TileBase GetRandomOutlineTileOnly(OutlineDirection direction)
        {
            return GetRandomTile(GetOutlineSprites(direction), Tile.ColliderType.Grid);
        }

        private TileBase GetRandomFallbackOutlineTile(OutlineDirection direction)
        {
            switch (direction)
            {
                case OutlineDirection.OuterNorthEast:
                    return GetRandomOutlineTileOnly(OutlineDirection.NorthEast)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.North)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.East);
                case OutlineDirection.OuterNorthWest:
                    return GetRandomOutlineTileOnly(OutlineDirection.NorthWest)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.North)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.West);
                case OutlineDirection.OuterSouthEast:
                    return GetRandomOutlineTileOnly(OutlineDirection.SouthEast)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.South)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.East);
                case OutlineDirection.OuterSouthWest:
                    return GetRandomOutlineTileOnly(OutlineDirection.SouthWest)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.South)
                        ?? GetRandomOutlineTileOnly(OutlineDirection.West);
                case OutlineDirection.NorthEast:
                    return GetRandomOutlineTileOnly(OutlineDirection.North) ?? GetRandomOutlineTileOnly(OutlineDirection.East);
                case OutlineDirection.NorthWest:
                    return GetRandomOutlineTileOnly(OutlineDirection.North) ?? GetRandomOutlineTileOnly(OutlineDirection.West);
                case OutlineDirection.SouthEast:
                    return GetRandomOutlineTileOnly(OutlineDirection.South) ?? GetRandomOutlineTileOnly(OutlineDirection.East);
                case OutlineDirection.SouthWest:
                    return GetRandomOutlineTileOnly(OutlineDirection.South) ?? GetRandomOutlineTileOnly(OutlineDirection.West);
                case OutlineDirection.North:
                    return GetRandomOutlineTileOnly(OutlineDirection.South);
                case OutlineDirection.South:
                    return GetRandomOutlineTileOnly(OutlineDirection.North);
                case OutlineDirection.East:
                    return GetRandomOutlineTileOnly(OutlineDirection.West);
                case OutlineDirection.West:
                    return GetRandomOutlineTileOnly(OutlineDirection.East);
                default:
                    return null;
            }
        }

        private TileBase GetRandomAnyOutlineTile()
        {
            return GetRandomOutlineTileOnly(OutlineDirection.North)
                ?? GetRandomOutlineTileOnly(OutlineDirection.South)
                ?? GetRandomOutlineTileOnly(OutlineDirection.East)
                ?? GetRandomOutlineTileOnly(OutlineDirection.West)
                ?? GetRandomOutlineTileOnly(OutlineDirection.NorthEast)
                ?? GetRandomOutlineTileOnly(OutlineDirection.NorthWest)
                ?? GetRandomOutlineTileOnly(OutlineDirection.SouthEast)
                ?? GetRandomOutlineTileOnly(OutlineDirection.SouthWest)
                ?? GetRandomOutlineTileOnly(OutlineDirection.OuterNorthEast)
                ?? GetRandomOutlineTileOnly(OutlineDirection.OuterNorthWest)
                ?? GetRandomOutlineTileOnly(OutlineDirection.OuterSouthEast)
                ?? GetRandomOutlineTileOnly(OutlineDirection.OuterSouthWest);
        }

        private bool HasAnyOutlineSprite()
        {
            return HasAnyValidSprite(outlineNorthSprites)
                || HasAnyValidSprite(outlineSouthSprites)
                || HasAnyValidSprite(outlineEastSprites)
                || HasAnyValidSprite(outlineWestSprites)
                || HasAnyValidSprite(outlineNorthEastSprites)
                || HasAnyValidSprite(outlineNorthWestSprites)
                || HasAnyValidSprite(outlineSouthEastSprites)
                || HasAnyValidSprite(outlineSouthWestSprites)
                || HasAnyValidSprite(outlineOuterNorthEastSprites)
                || HasAnyValidSprite(outlineOuterNorthWestSprites)
                || HasAnyValidSprite(outlineOuterSouthEastSprites)
                || HasAnyValidSprite(outlineOuterSouthWestSprites);
        }
    }
}
