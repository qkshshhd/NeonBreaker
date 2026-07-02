using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float acceleration = 80f;
        [SerializeField] private float deceleration = 100f;
        [SerializeField] private bool setRigidbodyInterpolation = true;
        [SerializeField] private RigidbodyInterpolation2D interpolation = RigidbodyInterpolation2D.Interpolate;

        private Rigidbody2D body;
        private PlayerStats stats;

        public bool CanMove { get; set; } = true;
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();

            if (setRigidbodyInterpolation)
            {
                body.interpolation = interpolation;
            }
        }

        public void Configure(PlayerDefinition definition)
        {
            moveSpeed = definition.MoveSpeed;
            acceleration = definition.Acceleration;
            deceleration = definition.Deceleration;
        }

        public void Move(Vector2 moveInput)
        {
            float effectiveMoveSpeed = stats != null ? stats.GetMoveSpeed(moveSpeed) : moveSpeed;
            Vector2 targetVelocity = CanMove ? moveInput * effectiveMoveSpeed : Vector2.zero;
            float rate = targetVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
