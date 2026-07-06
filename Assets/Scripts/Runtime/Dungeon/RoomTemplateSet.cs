using System;
using NeonBreaker.Rooms;
using UnityEngine;

namespace NeonBreaker.Dungeon
{
    [CreateAssetMenu(menuName = "Neon Breaker/Dungeon/Room Template Set")]
    public sealed class RoomTemplateSet : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private RoomType roomType = RoomType.Combat;
            [SerializeField] private RoomTemplate2D[] templates;

            public RoomType RoomType => roomType;
            public RoomTemplate2D Pick()
            {
                if (templates == null || templates.Length == 0)
                {
                    return null;
                }

                for (int attempt = 0; attempt < templates.Length; attempt++)
                {
                    RoomTemplate2D template = templates[UnityEngine.Random.Range(0, templates.Length)];
                    if (template != null)
                    {
                        return template;
                    }
                }

                for (int i = 0; i < templates.Length; i++)
                {
                    if (templates[i] != null)
                    {
                        return templates[i];
                    }
                }

                return null;
            }
        }

        [SerializeField] private Entry[] entries;
        [SerializeField] private RoomTemplate2D[] fallbackTemplates;

        public RoomTemplate2D Pick(RoomType roomType)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].RoomType == roomType)
                    {
                        RoomTemplate2D picked = entries[i].Pick();
                        if (picked != null)
                        {
                            return picked;
                        }
                    }
                }
            }

            return PickFallback();
        }

        private RoomTemplate2D PickFallback()
        {
            if (fallbackTemplates == null || fallbackTemplates.Length == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < fallbackTemplates.Length; attempt++)
            {
                RoomTemplate2D template = fallbackTemplates[UnityEngine.Random.Range(0, fallbackTemplates.Length)];
                if (template != null)
                {
                    return template;
                }
            }

            return null;
        }
    }
}
