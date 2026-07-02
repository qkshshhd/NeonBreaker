using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public interface IEnemyBehavior
    {
        void Initialize(EnemyController controller);
        void OnSpawned();
        void OnDespawned();
        void Tick(float deltaTime);
        void FixedTick(float fixedDeltaTime);
        void OnDamaged(DamageInfo damage);
        void OnDeath();
        void OnCollisionEnter2D(Collision2D collision);
        void OnTriggerEnter2D(Collider2D other);
    }
}
