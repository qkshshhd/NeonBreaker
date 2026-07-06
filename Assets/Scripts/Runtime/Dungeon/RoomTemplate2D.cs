using System.Collections.Generic;
using NeonBreaker.Rooms;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Dungeon
{
    public sealed class RoomTemplate2D : MonoBehaviour
    {
        [Header("Metadata")]
        [SerializeField] private RoomType roomType = RoomType.Combat;
        [SerializeField] private RectInt localBounds = new RectInt(-8, -5, 16, 10);

        [Header("Anchors")]
        [SerializeField] private Transform centerPoint;
        [SerializeField] private Transform entrancePoint;
        [SerializeField] private Transform exitPoint;

        [Header("Gameplay")]
        [SerializeField] private RoomTemplateDoor2D entranceDoor;
        [SerializeField] private RoomTemplateDoor2D exitDoor;
        [SerializeField] private Transform[] spawnPoints;

        public RoomType RoomType => roomType;
        public RectInt LocalBounds => localBounds.width > 0 && localBounds.height > 0 ? localBounds : CalculateFallbackLocalBounds();
        public RoomTemplateDoor2D EntranceDoor => entranceDoor;
        public RoomTemplateDoor2D ExitDoor => exitDoor;

        public Vector3 CenterWorldPosition => centerPoint != null ? centerPoint.position : transform.TransformPoint((Vector3)LocalBounds.center);
        public Vector3 EntranceWorldPosition => entrancePoint != null ? entrancePoint.position : GetFallbackDoorWorldPosition(Vector2.left);
        public Vector3 ExitWorldPosition => exitPoint != null ? exitPoint.position : GetFallbackDoorWorldPosition(Vector2.right);

        public void CollectSpawnPositions(List<Vector3> results)
        {
            if (results == null || spawnPoints == null)
            {
                return;
            }

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    results.Add(spawnPoints[i].position);
                }
            }
        }

        private Vector3 GetFallbackDoorWorldPosition(Vector2 direction)
        {
            RectInt bounds = LocalBounds;
            Vector2 local = bounds.center;
            if (direction.x < 0f)
            {
                local.x = bounds.xMin;
            }
            else if (direction.x > 0f)
            {
                local.x = bounds.xMax;
            }

            if (direction.y < 0f)
            {
                local.y = bounds.yMin;
            }
            else if (direction.y > 0f)
            {
                local.y = bounds.yMax;
            }

            return transform.TransformPoint(local);
        }

        private RectInt CalculateFallbackLocalBounds()
        {
            Tilemap[] tilemaps = GetComponentsInChildren<Tilemap>(true);
            bool hasBounds = false;
            Vector3Int min = Vector3Int.zero;
            Vector3Int max = Vector3Int.zero;

            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null)
                {
                    continue;
                }

                BoundsInt bounds = tilemap.cellBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0)
                {
                    continue;
                }

                Vector3Int currentMin = bounds.min;
                Vector3Int currentMax = bounds.max;
                if (!hasBounds)
                {
                    min = currentMin;
                    max = currentMax;
                    hasBounds = true;
                }
                else
                {
                    min = new Vector3Int(Mathf.Min(min.x, currentMin.x), Mathf.Min(min.y, currentMin.y), 0);
                    max = new Vector3Int(Mathf.Max(max.x, currentMax.x), Mathf.Max(max.y, currentMax.y), 0);
                }
            }

            if (!hasBounds)
            {
                return new RectInt(-8, -5, 16, 10);
            }

            return new RectInt(min.x, min.y, Mathf.Max(1, max.x - min.x), Mathf.Max(1, max.y - min.y));
        }
    }
}
