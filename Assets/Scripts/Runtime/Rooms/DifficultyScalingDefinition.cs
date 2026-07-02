using UnityEngine;

namespace NeonBreaker.Rooms
{
    [CreateAssetMenu(menuName = "Neon Breaker/Rooms/Difficulty Scaling Definition")]
    public sealed class DifficultyScalingDefinition : ScriptableObject
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float firstRoomHealthMultiplier = 1f;
        [SerializeField, Min(1f)] private float finalRoomHealthMultiplier = 1.8f;
        [SerializeField, Min(1f)] private float bossHealthMultiplier = 1.35f;
        [SerializeField] private AnimationCurve healthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Spawn Count")]
        [SerializeField, Min(1f)] private float firstRoomSpawnCountMultiplier = 1f;
        [SerializeField, Min(1f)] private float finalRoomSpawnCountMultiplier = 1.45f;
        [SerializeField] private AnimationCurve spawnCountCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool scaleBossSpawnCount = false;

        [Header("Limits")]
        [SerializeField, Min(1)] private int maxExtraEnemiesPerGroup = 5;

        public DifficultyContext Evaluate(int roomIndex, int totalRoomCount, RoomDefinition room)
        {
            float progress = CalculateProgress(roomIndex, totalRoomCount);
            float healthMultiplier = Mathf.Lerp(
                firstRoomHealthMultiplier,
                finalRoomHealthMultiplier,
                EvaluateCurve(healthCurve, progress));

            float spawnCountMultiplier = Mathf.Lerp(
                firstRoomSpawnCountMultiplier,
                finalRoomSpawnCountMultiplier,
                EvaluateCurve(spawnCountCurve, progress));

            if (room != null && room.RoomType == RoomType.Boss)
            {
                healthMultiplier *= bossHealthMultiplier;
                if (!scaleBossSpawnCount)
                {
                    spawnCountMultiplier = 1f;
                }
            }

            return new DifficultyContext(
                roomIndex,
                totalRoomCount,
                Mathf.Max(1f, healthMultiplier),
                Mathf.Max(1f, spawnCountMultiplier),
                Mathf.Max(0, maxExtraEnemiesPerGroup));
        }

        public static DifficultyContext EvaluateDefault(int roomIndex, int totalRoomCount, RoomDefinition room)
        {
            float progress = CalculateProgress(roomIndex, totalRoomCount);
            float healthMultiplier = Mathf.Lerp(1f, 1.65f, progress);
            float spawnCountMultiplier = room != null && room.RoomType == RoomType.Boss
                ? 1f
                : Mathf.Lerp(1f, 1.35f, progress);

            if (room != null && room.RoomType == RoomType.Boss)
            {
                healthMultiplier *= 1.25f;
            }

            return new DifficultyContext(
                roomIndex,
                totalRoomCount,
                healthMultiplier,
                spawnCountMultiplier,
                4);
        }

        private static float CalculateProgress(int roomIndex, int totalRoomCount)
        {
            if (totalRoomCount <= 1)
            {
                return 0f;
            }

            return Mathf.Clamp01(roomIndex / (float)(totalRoomCount - 1));
        }

        private static float EvaluateCurve(AnimationCurve curve, float progress)
        {
            return curve != null ? Mathf.Clamp01(curve.Evaluate(progress)) : progress;
        }
    }
}
