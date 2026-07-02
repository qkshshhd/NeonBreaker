using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    [CreateAssetMenu(menuName = "Neon Breaker/Rooms/Room Sequence Definition")]
    public sealed class RoomSequenceDefinition : ScriptableObject
    {
        [Header("Length")]
        [SerializeField, Min(2)] private int minRoomCount = 6;
        [SerializeField, Min(2)] private int maxRoomCount = 8;
        [SerializeField, Min(0)] private int firstSpecialRoomIndex = 2;

        [Header("Room Pools")]
        [SerializeField] private RoomDefinition[] startRooms;
        [SerializeField] private RoomDefinition[] combatRooms;
        [SerializeField] private RoomDefinition[] eliteRooms;
        [SerializeField] private RoomDefinition[] rewardRooms;
        [SerializeField] private RoomDefinition[] restRooms;
        [SerializeField] private RoomDefinition[] bossRooms;

        [Header("Special Room Chance")]
        [SerializeField, Range(0f, 1f)] private float eliteChance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float rewardChance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float restChance = 0.12f;
        [SerializeField] private bool preventConsecutiveSpecialRooms = true;
        [SerializeField] private bool preventImmediateRoomRepeat = true;

        public RoomDefinition[] Build()
        {
            int minCount = Mathf.Max(2, minRoomCount);
            int maxCount = Mathf.Max(minCount, maxRoomCount);
            int roomCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            RoomDefinition[] sequence = new RoomDefinition[roomCount];

            sequence[0] = PickRoom(startRooms, combatRooms, null);

            bool previousWasSpecial = IsSpecial(sequence[0]);
            for (int i = 1; i < roomCount - 1; i++)
            {
                RoomType nextType = ChooseMiddleRoomType(i, previousWasSpecial);
                sequence[i] = PickRoomForType(nextType, sequence[i - 1]);
                previousWasSpecial = IsSpecial(sequence[i]);
            }

            sequence[roomCount - 1] = PickRoom(bossRooms, combatRooms, sequence[roomCount - 2]);
            RemoveNullRooms(sequence);
            return sequence;
        }

        private RoomType ChooseMiddleRoomType(int roomIndex, bool previousWasSpecial)
        {
            if (roomIndex < firstSpecialRoomIndex || !CanPlaceSpecial(previousWasSpecial))
            {
                return RoomType.Combat;
            }

            float totalChance = GetUsableSpecialChance(RoomType.Elite)
                + GetUsableSpecialChance(RoomType.Reward)
                + GetUsableSpecialChance(RoomType.Rest);

            if (totalChance <= 0f || UnityEngine.Random.value > Mathf.Clamp01(totalChance))
            {
                return RoomType.Combat;
            }

            float roll = UnityEngine.Random.value * totalChance;
            roll -= GetUsableSpecialChance(RoomType.Elite);
            if (roll <= 0f)
            {
                return RoomType.Elite;
            }

            roll -= GetUsableSpecialChance(RoomType.Reward);
            if (roll <= 0f)
            {
                return RoomType.Reward;
            }

            return RoomType.Rest;
        }

        private bool CanPlaceSpecial(bool previousWasSpecial)
        {
            return !preventConsecutiveSpecialRooms || !previousWasSpecial;
        }

        private float GetUsableSpecialChance(RoomType roomType)
        {
            return HasPool(roomType) ? GetSpecialChance(roomType) : 0f;
        }

        private float GetSpecialChance(RoomType roomType)
        {
            return roomType switch
            {
                RoomType.Elite => eliteChance,
                RoomType.Reward => rewardChance,
                RoomType.Rest => restChance,
                _ => 0f
            };
        }

        private RoomDefinition PickRoomForType(RoomType roomType, RoomDefinition previousRoom)
        {
            return roomType switch
            {
                RoomType.Elite => PickRoom(eliteRooms, combatRooms, previousRoom),
                RoomType.Reward => PickRoom(rewardRooms, combatRooms, previousRoom),
                RoomType.Rest => PickRoom(restRooms, combatRooms, previousRoom),
                RoomType.Boss => PickRoom(bossRooms, combatRooms, previousRoom),
                _ => PickRoom(combatRooms, startRooms, previousRoom)
            };
        }

        private RoomDefinition PickRoom(RoomDefinition[] primaryPool, RoomDefinition[] fallbackPool, RoomDefinition previousRoom)
        {
            RoomDefinition room = PickFromPool(primaryPool, previousRoom);
            if (room != null)
            {
                return room;
            }

            room = PickFromPool(fallbackPool, previousRoom);
            if (room != null)
            {
                return room;
            }

            return previousRoom;
        }

        private RoomDefinition PickFromPool(RoomDefinition[] pool, RoomDefinition previousRoom)
        {
            List<RoomDefinition> validRooms = GetValidRooms(pool);
            if (validRooms.Count <= 0)
            {
                return null;
            }

            if (!preventImmediateRoomRepeat || previousRoom == null || validRooms.Count <= 1)
            {
                return validRooms[UnityEngine.Random.Range(0, validRooms.Count)];
            }

            RoomDefinition pickedRoom;
            do
            {
                pickedRoom = validRooms[UnityEngine.Random.Range(0, validRooms.Count)];
            }
            while (pickedRoom == previousRoom);

            return pickedRoom;
        }

        private static List<RoomDefinition> GetValidRooms(RoomDefinition[] pool)
        {
            List<RoomDefinition> validRooms = new List<RoomDefinition>();
            if (pool == null)
            {
                return validRooms;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null)
                {
                    validRooms.Add(pool[i]);
                }
            }

            return validRooms;
        }

        private bool HasPool(RoomType roomType)
        {
            RoomDefinition[] pool = roomType switch
            {
                RoomType.Elite => eliteRooms,
                RoomType.Reward => rewardRooms,
                RoomType.Rest => restRooms,
                RoomType.Boss => bossRooms,
                _ => combatRooms
            };

            return GetValidRooms(pool).Count > 0;
        }

        private static bool IsSpecial(RoomDefinition room)
        {
            if (room == null)
            {
                return false;
            }

            return room.RoomType == RoomType.Elite
                || room.RoomType == RoomType.Reward
                || room.RoomType == RoomType.Rest;
        }

        private static void RemoveNullRooms(RoomDefinition[] sequence)
        {
            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] != null)
                {
                    continue;
                }

                throw new InvalidOperationException("[RoomSequenceDefinition] Generated sequence contains an empty room. Check room pools.");
            }
        }
    }
}
