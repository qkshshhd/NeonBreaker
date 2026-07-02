using System;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [Obsolete("Use DashAttackDefinition instead. This class remains so older Dasher assets can still be assigned.")]
    [CreateAssetMenu(menuName = "Neon Breaker/Enemies/Dasher Enemy Definition (Legacy)")]
    public sealed class DasherEnemyDefinition : DashAttackDefinition
    {
    }
}
