namespace NeonBreaker.Rooms
{
    public readonly struct DifficultyContext
    {
        public static DifficultyContext Default => new DifficultyContext(0, 1, 1f, 1f, 0);

        public DifficultyContext(
            int roomIndex,
            int totalRoomCount,
            float enemyHealthMultiplier,
            float spawnCountMultiplier,
            int maxExtraEnemiesPerGroup)
        {
            RoomIndex = roomIndex;
            TotalRoomCount = totalRoomCount;
            EnemyHealthMultiplier = enemyHealthMultiplier;
            SpawnCountMultiplier = spawnCountMultiplier;
            MaxExtraEnemiesPerGroup = maxExtraEnemiesPerGroup;
        }

        public int RoomIndex { get; }
        public int TotalRoomCount { get; }
        public float EnemyHealthMultiplier { get; }
        public float SpawnCountMultiplier { get; }
        public int MaxExtraEnemiesPerGroup { get; }
    }
}
