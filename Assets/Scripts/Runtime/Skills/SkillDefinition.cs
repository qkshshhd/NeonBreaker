using UnityEngine;

namespace NeonBreaker.Skills
{
    [CreateAssetMenu(menuName = "Neon Breaker/Skills/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Shockwave";
        [SerializeField] private SkillType skillType = SkillType.Shockwave;
        [SerializeField, Min(0f)] private float castDelay = 0.22f;
        [SerializeField] private float cooldown = 6f;
        [SerializeField] private float damage = 35f;
        [SerializeField] private float radius = 2.2f;
        [SerializeField] private float knockbackForce = 12f;
        [SerializeField] private float knockbackDuration = 0.12f;
        [SerializeField] private int maxHits = 32;
        [SerializeField] private LayerMask targetLayers = Physics2D.DefaultRaycastLayers;

        public string DisplayName => displayName;
        public SkillType SkillType => skillType;
        public float CastDelay => Mathf.Max(0f, castDelay);
        public float Cooldown => Mathf.Max(0.05f, cooldown);
        public float Damage => Mathf.Max(0f, damage);
        public float Radius => Mathf.Max(0.05f, radius);
        public float KnockbackForce => Mathf.Max(0f, knockbackForce);
        public float KnockbackDuration => Mathf.Max(0f, knockbackDuration);
        public int MaxHits => Mathf.Max(1, maxHits);
        public LayerMask TargetLayers => targetLayers;
    }
}
