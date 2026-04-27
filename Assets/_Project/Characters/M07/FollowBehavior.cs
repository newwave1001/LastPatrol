using UnityEngine;
using LastPatrol.Systems.AI;

namespace LastPatrol.Characters.M07
{
    // M-07이 마렌을 호위하며 따라감.
    // 거리에 따라 3 모드: idle(가까움) / follow(중간) / catchup(멈)
    [RequireComponent(typeof(CharacterController))]
    public class FollowBehavior : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Distances")]
        [SerializeField] private float stopRadius = 1.6f;
        [SerializeField] private float followRadius = 4.0f;
        [SerializeField] private float catchupRadius = 8.0f;

        [Header("Speeds")]
        [SerializeField] private float followSpeed = 4.0f;
        [SerializeField] private float catchupSpeed = 6.5f;

        [Header("Side Offset")]
        [SerializeField] private float sideOffset = 1.0f;

        [Header("Obstacle Avoidance")]
        [Tooltip("좌/우 회피 raycast 거리. 차/벽 사이 통로 폭 정도가 적당.")]
        [SerializeField] private float steerLookAhead = 3f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private CharacterController controller;
        private float verticalVelocity;

        public Transform Target { get => target; set => target = value; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            // prefab 인스턴스로 들어왔을 때 target 참조 missing 가능 → 마렌 자동 검색.
            if (target == null)
            {
                var maren = FindAnyObjectByType<LastPatrol.Characters.MarenController>();
                if (maren != null) target = maren.transform;
            }
        }

        public void Tick()
        {
            if (target == null) return;

            Vector3 desired = target.position + new Vector3(-sideOffset * Mathf.Sign(target.localScale.x), 0f, 0f);
            Vector3 toTarget = desired - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            float speed = 0f;
            if (dist > catchupRadius) speed = catchupSpeed;
            else if (dist > followRadius) speed = Mathf.Lerp(followSpeed, catchupSpeed, (dist - followRadius) / Mathf.Max(0.001f, catchupRadius - followRadius));
            else if (dist > stopRadius) speed = followSpeed * Mathf.InverseLerp(stopRadius, followRadius, dist);

            // 장애물 회피 — 7방향 cast 중 가장 빈 방향으로 진행
            Vector3 dirNorm;
            if (dist > 0.001f && speed > 0.001f)
            {
                Vector3 steered = SteeringHelper.ResolveDirection(
                    toTarget, transform.position, steerLookAhead, obstacleMask);
                dirNorm = steered;
            }
            else
            {
                dirNorm = Vector3.zero;
            }
            Vector3 step = dirNorm * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += -20f * Time.deltaTime;

            controller.Move(new Vector3(step.x, verticalVelocity, step.z) * Time.deltaTime);
        }

        /// <summary>플레이어가 Tab으로 M-07 직접 조종할 때 호출. axis = (x: 좌우, y: 전후).</summary>
        public void ManualMove(Vector2 axis)
        {
            if (controller == null) return;

            Vector3 desired = new Vector3(axis.x, 0f, axis.y);
            float mag = desired.magnitude;
            Vector3 step = Vector3.zero;
            if (mag > 0.01f)
            {
                Vector3 dir = desired / mag;
                // 직접 조종에도 회피 적용 — 벽에 끼지 않게.
                Vector3 steered = SteeringHelper.ResolveDirection(
                    dir, transform.position, steerLookAhead, obstacleMask);
                step = steered * catchupSpeed * Mathf.Clamp01(mag);
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += -20f * Time.deltaTime;
            controller.Move(new Vector3(step.x, verticalVelocity, step.z) * Time.deltaTime);
        }
    }
}
