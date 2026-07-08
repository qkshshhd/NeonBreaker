using NeonBreaker.Combat;
using NeonBreaker.Shared.StateMachine;
using UnityEngine;

namespace NeonBreaker.Enemies
{
    [RequireComponent(typeof(EnemyMeleeAttack2D))]
    public sealed class ChaserMeleeEnemyBehavior : EnemyBehaviorBase
    {
        private readonly StateMachine stateMachine = new StateMachine();

        private EnemyMeleeAttack2D meleeAttack;
        private float attackCooldownTimer;

        private IdleState idleState;
        private ChaseState chaseState;
        private WindUpState windUpState;
        private AttackState attackState;
        private RecoveryState recoveryState;
        private HitReactState hitReactState;

        private EnemyDefinition Definition => Controller != null ? Controller.Definition : null;
        private bool CanAttack => attackCooldownTimer <= 0f
            && Definition != null
            && Definition.Attack != null
            && Controller != null
            && !Controller.IsAttackSuppressed;

        private void Awake()
        {
            meleeAttack = GetComponent<EnemyMeleeAttack2D>();
        }

        public override void Initialize(EnemyController controller)
        {
            base.Initialize(controller);

            idleState = new IdleState(this);
            chaseState = new ChaseState(this);
            windUpState = new WindUpState(this);
            attackState = new AttackState(this);
            recoveryState = new RecoveryState(this);
            hitReactState = new HitReactState(this);
        }

        public override void OnSpawned()
        {
            attackCooldownTimer = 0f;
            stateMachine.ChangeState(idleState);
        }

        public override void OnDespawned()
        {
            attackCooldownTimer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= deltaTime;
            }

            stateMachine.Tick(deltaTime);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            stateMachine.FixedTick(fixedDeltaTime);
        }

        public override void OnDamaged(DamageInfo damage)
        {
            if (damage.Knockback > 0f)
            {
                stateMachine.ChangeState(hitReactState);
            }
        }

        public override void OnDeath()
        {
            HideMotion();
        }

        private void MoveTowardTarget()
        {
            if (Definition == null)
            {
                HideMotion();
                return;
            }

            Controller.MoveTowardTarget(Definition.MoveSpeed, Definition.Acceleration);
            Controller.RotateToward(Controller.DirectionToTarget, Definition.RotationSpeed);
        }

        private void Stop()
        {
            if (Definition == null)
            {
                HideMotion();
                return;
            }

            Controller.Stop(Definition.Deceleration);
        }

        private void HideMotion()
        {
            if (Controller != null && Controller.Body != null)
            {
                Controller.Body.linearVelocity = Vector2.zero;
            }
        }

        private void ExecuteAttack()
        {
            if (Definition == null || Definition.Attack == null)
            {
                return;
            }

            meleeAttack.Execute(Definition.Attack, Controller.DirectionToTarget);
            attackCooldownTimer = Definition.Attack.Cooldown;
        }

        private void ChangeToChaseOrIdle()
        {
            if (Definition == null || !Controller.HasTarget || Controller.DistanceToTarget > Definition.DetectionRange)
            {
                stateMachine.ChangeState(idleState);
            }
            else
            {
                stateMachine.ChangeState(chaseState);
            }
        }

        private abstract class BehaviorState : IState
        {
            protected readonly ChaserMeleeEnemyBehavior Behavior;

            protected BehaviorState(ChaserMeleeEnemyBehavior behavior)
            {
                Behavior = behavior;
            }

            public virtual void Enter() { }
            public virtual void Tick(float deltaTime) { }
            public virtual void FixedTick(float fixedDeltaTime) { }
            public virtual void Exit() { }
        }

        private sealed class IdleState : BehaviorState
        {
            public IdleState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition definition = Behavior.Definition;
                if (definition != null
                    && Behavior.Controller.HasTarget
                    && Behavior.Controller.DistanceToTarget <= definition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.chaseState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class ChaseState : BehaviorState
        {
            public ChaseState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Tick(float deltaTime)
            {
                EnemyDefinition definition = Behavior.Definition;
                if (definition == null
                    || !Behavior.Controller.HasTarget
                    || Behavior.Controller.DistanceToTarget > definition.DetectionRange)
                {
                    Behavior.stateMachine.ChangeState(Behavior.idleState);
                    return;
                }

                if (Behavior.Controller.DistanceToTarget <= definition.AttackRange && Behavior.CanAttack)
                {
                    Behavior.stateMachine.ChangeState(Behavior.windUpState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                EnemyDefinition definition = Behavior.Definition;
                if (definition != null && Behavior.Controller.DistanceToTarget > definition.AttackRange)
                {
                    Behavior.MoveTowardTarget();
                }
                else
                {
                    Behavior.Stop();
                }
            }
        }

        private sealed class WindUpState : BehaviorState
        {
            private float timer;

            public WindUpState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                EnemyDefinition definition = Behavior.Definition;
                timer = definition != null && definition.Attack != null ? definition.Attack.WindUpTime : 0.1f;
                Behavior.Stop();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.WindUp);
            }

            public override void Tick(float deltaTime)
            {
                if (!Behavior.Controller.HasTarget)
                {
                    Behavior.ChangeToChaseOrIdle();
                    return;
                }

                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.stateMachine.ChangeState(Behavior.attackState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
                EnemyDefinition definition = Behavior.Definition;
                if (definition != null)
                {
                    Behavior.Controller.RotateToward(Behavior.Controller.DirectionToTarget, definition.RotationSpeed);
                }
            }
        }

        private sealed class AttackState : BehaviorState
        {
            public AttackState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Attack);
                Behavior.ExecuteAttack();
                Behavior.stateMachine.ChangeState(Behavior.recoveryState);
            }
        }

        private sealed class RecoveryState : BehaviorState
        {
            private float timer;

            public RecoveryState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                EnemyDefinition definition = Behavior.Definition;
                timer = definition != null && definition.Attack != null ? definition.Attack.RecoveryTime : 0.2f;
                Behavior.Stop();
                Behavior.RaiseAnimationSignal(EnemyAnimationSignal.Recovery);
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Behavior.ChangeToChaseOrIdle();
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Behavior.Stop();
            }
        }

        private sealed class HitReactState : BehaviorState
        {
            private float timer;

            public HitReactState(ChaserMeleeEnemyBehavior behavior) : base(behavior) { }

            public override void Enter()
            {
                timer = Behavior.Definition != null ? Behavior.Definition.HitStunDuration : 0.12f;
            }

            public override void Tick(float deltaTime)
            {
                timer -= deltaTime;
                if (timer > 0f)
                {
                    return;
                }

                if (Behavior.Controller.KnockbackReceiver != null && Behavior.Controller.KnockbackReceiver.IsReceivingKnockback)
                {
                    return;
                }

                Behavior.ChangeToChaseOrIdle();
            }
        }
    }
}
