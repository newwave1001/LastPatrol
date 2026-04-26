using UnityEngine;

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

        private CharacterController controller;
        private float verticalVelocity;

        public Transform Target { get => target; set => target = value; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
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

            Vector3 step = (dist > 0.001f ? toTarget / dist : Vector3.zero) * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += -20f * Time.deltaTime;

            controller.Move(new Vector3(step.x, verticalVelocity, step.z) * Time.deltaTime);
        }
    }
}
