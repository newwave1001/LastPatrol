using UnityEngine;

namespace LastPatrol.Characters
{
    // 사이드뷰 2.5D 이동. X 축 좌우 이동이 메인, Z 축은 깊이 이동(필요 시).
    // CharacterController 사용 — step offset이 자동 계단 등반 처리 (마렌은 점프 없음 규칙).
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float depthSpeedMultiplier = 0.6f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float maxFallSpeed = -30f;

        [Header("Facing")]
        [SerializeField] private bool flipSpriteOnTurn = true;

        private CharacterController controller;
        private float verticalVelocity;
        private float lastFacingSign = 1f;

        public Vector3 LastVelocity { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public float FacingSign => lastFacingSign;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public void Tick(Vector2 axis)
        {
            float horizontal = axis.x * walkSpeed;
            float depth      = axis.y * walkSpeed * depthSpeedMultiplier;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, maxFallSpeed);

            Vector3 motion = new Vector3(horizontal, verticalVelocity, depth) * Time.deltaTime;
            controller.Move(motion);
            LastVelocity = motion / Mathf.Max(Time.deltaTime, 0.0001f);

            if (Mathf.Abs(axis.x) > 0.01f)
            {
                lastFacingSign = Mathf.Sign(axis.x);
                if (flipSpriteOnTurn)
                {
                    Vector3 s = transform.localScale;
                    s.x = Mathf.Abs(s.x) * lastFacingSign;
                    transform.localScale = s;
                }
            }
        }

        public void Teleport(Vector3 worldPosition)
        {
            controller.enabled = false;
            transform.position = worldPosition;
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}
