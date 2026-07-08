using NeonBreaker.Combat;
using NeonBreaker.Shared.StateMachine;
using NeonBreaker.Skills;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerMovement2D))]
    [RequireComponent(typeof(PlayerDash2D))]
    [RequireComponent(typeof(MeleeAttack2D))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerSkillController))]
    [RequireComponent(typeof(PlayerRecoilCore))]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerDefinition definition;
        [SerializeField] private bool addDefaultDamageFeedback = true;
        [SerializeField, Min(0f)] private float dashInputBufferTime = 0.16f;
        [SerializeField] private bool dashCanCancelAttack = true;

        private readonly StateMachine stateMachine = new StateMachine();

        private PlayerInputReader input;
        private PlayerMovement2D movement;
        private PlayerDash2D dash;
        private MeleeAttack2D attack;
        private PlayerStats stats;
        private PlayerSkillController skillController;
        private PlayerRecoilCore recoilCore;
        private Health health;

        private PlayerIdleState idleState;
        private PlayerMoveState moveState;
        private PlayerAttackState attackState;
        private PlayerDashState dashState;
        private PlayerDeadState deadState;

        private bool isDead;
        private float dashBufferTimer;
        private Vector2 bufferedDashDirection = Vector2.right;

        public PlayerDefinition Definition => definition;
        public PlayerInputReader Input => input;
        public PlayerMovement2D Movement => movement;
        public PlayerDash2D Dash => dash;
        public MeleeAttack2D Attack => attack;
        public PlayerStats Stats => stats;
        public PlayerSkillController SkillController => skillController;
        public PlayerRecoilCore RecoilCore => recoilCore;
        public Health Health => health;
        public bool IsSkillCasting => skillController != null && skillController.IsCasting;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement2D>();
            dash = GetComponent<PlayerDash2D>();
            attack = GetComponent<MeleeAttack2D>();
            stats = GetComponent<PlayerStats>();
            skillController = GetComponent<PlayerSkillController>();
            recoilCore = GetComponent<PlayerRecoilCore>();
            if (recoilCore == null)
            {
                recoilCore = gameObject.AddComponent<PlayerRecoilCore>();
            }

            health = GetComponent<Health>();

            EnsureDefaultDamageFeedback();

            ApplyDefinition();

            idleState = new PlayerIdleState(this);
            moveState = new PlayerMoveState(this);
            attackState = new PlayerAttackState(this);
            dashState = new PlayerDashState(this);
            deadState = new PlayerDeadState(this);
        }

        private void OnEnable()
        {
            health.Died += HandleDied;

            if (skillController != null)
            {
                skillController.SkillStarted += HandleSkillStarted;
            }
        }

        private void OnDisable()
        {
            health.Died -= HandleDied;

            if (skillController != null)
            {
                skillController.SkillStarted -= HandleSkillStarted;
            }
        }

        private void Start()
        {
            stateMachine.ChangeState(idleState);
        }

        private void Update()
        {
            UpdateDashInputBuffer();

            if (isDead)
            {
                stateMachine.ChangeState(deadState);
                return;
            }

            stateMachine.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (IsSkillCasting)
            {
                movement.Stop();
                return;
            }

            stateMachine.FixedTick(Time.fixedDeltaTime);
        }

        public void ChangeToLocomotion()
        {
            if (input.MoveInput.sqrMagnitude > 0.001f)
            {
                stateMachine.ChangeState(moveState);
            }
            else
            {
                stateMachine.ChangeState(idleState);
            }
        }

        public bool TryStartActionState()
        {
            if (IsSkillCasting)
            {
                return true;
            }

            if (TryStartBufferedDash())
            {
                return true;
            }

            bool wantsAttack = definition == null || definition.AttackWhileHeld
                ? input.AttackHeld
                : input.AttackPressed;

            if (wantsAttack && attack.CooldownRemaining <= 0f)
            {
                stateMachine.ChangeState(attackState);
                return true;
            }

            return false;
        }

        private void UpdateDashInputBuffer()
        {
            if (IsSkillCasting)
            {
                dashBufferTimer = 0f;
                return;
            }

            if (dashBufferTimer > 0f)
            {
                dashBufferTimer -= Time.unscaledDeltaTime;
            }

            if (input != null && input.DashPressed)
            {
                bufferedDashDirection = GetPreferredDashDirection();
                dashBufferTimer = Mathf.Max(0.01f, dashInputBufferTime);
            }
        }

        private bool TryStartBufferedDash()
        {
            if (IsSkillCasting || dashBufferTimer <= 0f || dash == null || !dash.IsReady)
            {
                return false;
            }

            stateMachine.ChangeState(dashState);
            return true;
        }

        private Vector2 ConsumeDashDirection()
        {
            Vector2 direction = bufferedDashDirection.sqrMagnitude > 0.001f
                ? bufferedDashDirection
                : GetPreferredDashDirection();

            dashBufferTimer = 0f;
            return direction.normalized;
        }

        private Vector2 GetPreferredDashDirection()
        {
            if (input != null && input.MoveInput.sqrMagnitude > 0.001f)
            {
                return input.MoveInput.normalized;
            }

            if (input != null && input.AimDirection.sqrMagnitude > 0.001f)
            {
                return input.AimDirection.normalized;
            }

            return dash != null && dash.LastDashDirection.sqrMagnitude > 0.001f
                ? dash.LastDashDirection.normalized
                : Vector2.right;
        }

        private void ApplyDefinition()
        {
            if (definition == null)
            {
                return;
            }

            health.Initialize(definition.MaxHealth, definition.HitInvulnerabilityDuration);
            stats.ResetModifiers();
            movement.Configure(definition);
            dash.Configure(definition);
            attack.Configure(definition.BasicAttack);
            attack.ConfigureCombo(definition.BasicAttackCombo);
        }

        private void EnsureDefaultDamageFeedback()
        {
            if (!addDefaultDamageFeedback || GetComponent<PlayerDamageFeedback2D>() != null)
            {
                return;
            }

            gameObject.AddComponent<PlayerDamageFeedback2D>();
        }

        private void HandleDied()
        {
            isDead = true;
        }

        private void HandleSkillStarted(SkillDefinition skill)
        {
            dashBufferTimer = 0f;
            dash.CancelDash();
            movement.Stop();
        }

        private abstract class PlayerState : IState
        {
            protected readonly PlayerController Controller;

            protected PlayerState(PlayerController controller)
            {
                Controller = controller;
            }

            public virtual void Enter()
            {
            }

            public virtual void Tick(float deltaTime)
            {
            }

            public virtual void FixedTick(float fixedDeltaTime)
            {
            }

            public virtual void Exit()
            {
            }
        }

        private sealed class PlayerIdleState : PlayerState
        {
            public PlayerIdleState(PlayerController controller) : base(controller)
            {
            }

            public override void Tick(float deltaTime)
            {
                if (Controller.TryStartActionState())
                {
                    return;
                }

                if (Controller.input.MoveInput.sqrMagnitude > 0.001f)
                {
                    Controller.stateMachine.ChangeState(Controller.moveState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Controller.movement.Move(Vector2.zero);
            }
        }

        private sealed class PlayerMoveState : PlayerState
        {
            public PlayerMoveState(PlayerController controller) : base(controller)
            {
            }

            public override void Tick(float deltaTime)
            {
                if (Controller.TryStartActionState())
                {
                    return;
                }

                if (Controller.input.MoveInput.sqrMagnitude <= 0.001f)
                {
                    Controller.stateMachine.ChangeState(Controller.idleState);
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Controller.movement.Move(Controller.input.MoveInput);
            }
        }

        private sealed class PlayerAttackState : PlayerState
        {
            private float timer;

            public PlayerAttackState(PlayerController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                bool didAttack = Controller.attack.TryAttack(Controller.input.AimDirection);
                timer = didAttack ? Controller.attack.CurrentAttackStateDuration : 0f;
            }

            public override void Tick(float deltaTime)
            {
                if (Controller.dashCanCancelAttack && Controller.TryStartBufferedDash())
                {
                    return;
                }

                timer -= deltaTime;
                if (timer <= 0f)
                {
                    Controller.ChangeToLocomotion();
                }
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                Controller.movement.Move(Vector2.zero);
            }
        }

        private sealed class PlayerDashState : PlayerState
        {
            public PlayerDashState(PlayerController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                Vector2 direction = Controller.ConsumeDashDirection();

                if (!Controller.dash.TryDash(direction))
                {
                    Controller.ChangeToLocomotion();
                }
            }

            public override void Tick(float deltaTime)
            {
                if (!Controller.dash.IsDashing)
                {
                    Controller.ChangeToLocomotion();
                }
            }
        }

        private sealed class PlayerDeadState : PlayerState
        {
            public PlayerDeadState(PlayerController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                Controller.movement.Stop();
                Controller.dash.CancelDash();
            }
        }
    }
}
