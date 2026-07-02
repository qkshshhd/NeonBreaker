using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    public abstract class EnemyBehaviorBase : MonoBehaviour, IEnemyBehavior
    {
        protected EnemyController Controller { get; private set; }

        public virtual void Initialize(EnemyController controller)
        {
            Controller = controller;
        }

        public virtual void OnSpawned() { }
        public virtual void OnDespawned() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void OnDamaged(DamageInfo damage) { }
        public virtual void OnDeath() { }
        public virtual void OnCollisionEnter2D(Collision2D collision) { }
        public virtual void OnTriggerEnter2D(Collider2D other) { }

        protected void RaiseAnimationSignal(EnemyAnimationSignal signal)
        {
            Controller?.RaiseAnimationSignal(signal);
        }

        protected static T FindComponentInParents<T>(Collider2D source) where T : class
        {
            MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T component)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
