using UnityEngine;
using LastPatrol.Core.Input;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 직부감 차량 컨트롤러 (city_patrol_mockup_v04.html 이식).
    /// 그레이박스 1차: Rigidbody 없이 Transform 직접 제어 + 단순 BoxCast 충돌.
    /// XZ 평면에서 작동 (Y는 지면 높이 고정).
    ///
    /// 좌표/축 매핑:
    ///   v04(2D) angle -PI/2 = 위쪽(-Y) → Unity(3D) +Z 방향(forward).
    ///   조향(Steer) = DriveAxis.x  (좌/우 회전, Y축 yaw)
    ///   가속/후진(Throttle) = DriveAxis.y  (위쪽 +가속, 아래쪽 +후진)
    /// </summary>
    [DisallowMultipleComponent]
    public class CarController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader input;
        [Tooltip("씬 시작 시 자동으로 Drive 모드로 전환할지")]
        [SerializeField] private bool autoEnableDriveOnStart = true;

        [Header("Speed (units/sec)")]
        [Tooltip("v04: 2.4 픽셀/프레임 ≈ 144 픽셀/초 → Unity 단위 기준 24 m/s 정도")]
        [SerializeField] private float maxSpeed = 18f;
        [SerializeField] private float boostMultiplier = 1.65f;
        [SerializeField] private float reverseMultiplier = 0.5f;

        [Header("Acceleration & Friction (per second)")]
        [SerializeField] private float acceleration = 12f;   // v04 0.08*60 ≈ 4.8 → Unity 12
        [SerializeField] private float friction = 2.1f;       // v04 0.035*60 ≈ 2.1
        [Tooltip("거의 멈추면 0으로 스냅. 작은 값일수록 더 오래 굴러감")]
        [SerializeField] private float stopThreshold = 0.15f;

        [Header("Steering (rad/sec)")]
        [Tooltip("v04 0.048*60 ≈ 2.88 rad/s")]
        [SerializeField] private float maxTurnRate = 2.9f;
        [Tooltip("이 속도 이상이면 풀 회전 가능, 그 이하면 비례 감소 (정지 시 회전 X)")]
        [SerializeField] private float fullTurnSpeed = 9f;

        [Header("Collision (BoxCast)")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.9f, 0.5f, 1.6f);
        [SerializeField] private float skin = 0.05f;
        [SerializeField] private float collisionSpeedDamp = 0.3f; // v04 *0.3

        [Header("Debug")]
        [Tooltip("화면 좌상단에 실시간 진단 HUD 표시")]
        [SerializeField] private bool showDebugHUD = true;

        // ---- runtime state ----
        public float CurrentSpeed { get; private set; } // signed: + forward, - reverse
        public Vector3 Forward => transform.forward;

        // 진단용
        private string _lastHitInfo = "(none)";
        private Collider[] _ownColliders;
        private static readonly RaycastHit[] _hitBuf = new RaycastHit[16];

        void Reset()
        {
            input = GetComponent<InputReader>();
        }

        void Awake()
        {
            // 자기 자신 + 자식의 모든 collider를 캐시 → BoxCast 시 무시.
            // 자식 Body cube의 Box Collider를 사용자가 안 지워도 안전하게 동작.
            _ownColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        // 진단 로그용 (한 번만 출력)
        private bool _diagLogged;

        void Start()
        {
            if (input == null) input = GetComponent<InputReader>();

            if (input == null)
            {
                Debug.LogError($"[CarController] '{name}': InputReader 참조 못 찾음. " +
                               "같은 GameObject에 InputReader 컴포넌트가 있어야 함, " +
                               "또는 Inspector의 Input 필드에 직접 드래그해줘야 함.", this);
                return;
            }

            if (autoEnableDriveOnStart)
                input.EnableDriveControls();

            Debug.Log($"[CarController] Start | Input='{input.name}' | Mode={input.CurrentMode} | Pos={transform.position}", this);
        }

        void Update()
        {
            if (input == null) return;

            // 첫 입력 발생 시 한 번 진단 로그 — Mode가 Drive가 아니면 여기서 잡힘
            if (!_diagLogged && (input.DriveAxis.sqrMagnitude > 0.01f || input.MoveAxis.sqrMagnitude > 0.01f))
            {
                _diagLogged = true;
                Debug.Log($"[CarController] First input | Mode={input.CurrentMode} | DriveAxis={input.DriveAxis} | FootMoveAxis={input.MoveAxis}", this);
            }

            if (input.CurrentMode != InputReader.Mode.Drive) return;

            float dt = Time.deltaTime;
            Vector2 axis = input.DriveAxis;
            bool boost = input.BoostHeld;

            // --- Throttle / Brake / Reverse ---
            float throttle = axis.y; // [-1..1]
            if (throttle > 0f)
                CurrentSpeed += acceleration * throttle * dt;
            else if (throttle < 0f)
                CurrentSpeed += acceleration * throttle * reverseMultiplier * dt;

            // Speed cap
            float capForward = boost ? maxSpeed * boostMultiplier : maxSpeed;
            float capReverse = maxSpeed * reverseMultiplier;
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, -capReverse, capForward);

            // Friction (always pulls toward 0)
            float frictionDelta = friction * dt;
            if (CurrentSpeed > 0f)      CurrentSpeed = Mathf.Max(0f, CurrentSpeed - frictionDelta);
            else if (CurrentSpeed < 0f) CurrentSpeed = Mathf.Min(0f, CurrentSpeed + frictionDelta);

            if (Mathf.Abs(CurrentSpeed) < stopThreshold) CurrentSpeed = 0f;

            // --- Steering ---
            // v04: 회전 속도가 현재 속도에 비례 (정지 시 회전 X). 후진 시 조향 반전.
            float steer = axis.x;
            if (Mathf.Abs(steer) > 0.01f && Mathf.Abs(CurrentSpeed) > 0.05f)
            {
                float speedFactor = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / fullTurnSpeed);
                float dir = Mathf.Sign(CurrentSpeed); // 후진 시 -1 → 핸들 반대
                float yawDelta = steer * maxTurnRate * speedFactor * dir * dt * Mathf.Rad2Deg;
                transform.Rotate(0f, yawDelta, 0f, Space.World);
            }

            // --- Movement + collision (BoxCast slide) ---
            Vector3 velocity = transform.forward * CurrentSpeed;
            Vector3 delta = velocity * dt;
            if (delta.sqrMagnitude > 1e-6f)
                MoveWithSlide(delta);
        }

        private void MoveWithSlide(Vector3 delta)
        {
            // Try full move
            if (!CastObstacle(delta))
            {
                transform.position += delta;
                return;
            }

            // Hit something — try axis-separated slide (XZ only).
            Vector3 dx = new Vector3(delta.x, 0f, 0f);
            Vector3 dz = new Vector3(0f, 0f, delta.z);

            bool blockedX = CastObstacle(dx);
            bool blockedZ = CastObstacle(dz);

            if (!blockedX) transform.position += dx;
            if (!blockedZ) transform.position += dz;

            // v04 같은 충돌 시 속도 감쇠
            CurrentSpeed *= collisionSpeedDamp;
        }

        private bool CastObstacle(Vector3 delta)
        {
            float dist = delta.magnitude;
            if (dist < 1e-5f) return false;
            Vector3 dir = delta / dist;

            int hitCount = Physics.BoxCastNonAlloc(
                transform.position,
                boxHalfExtents,
                dir,
                _hitBuf,
                transform.rotation,
                dist + skin,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _hitBuf[i].collider;
                if (c == null) continue;
                if (IsOwnCollider(c)) continue; // 자기/자식 무시
                _lastHitInfo = $"{c.name} (layer={LayerMask.LayerToName(c.gameObject.layer)})";
                return true;
            }

            _lastHitInfo = "(clear)";
            return false;
        }

        private bool IsOwnCollider(Collider c)
        {
            if (_ownColliders == null) return false;
            for (int i = 0; i < _ownColliders.Length; i++)
                if (_ownColliders[i] == c) return true;
            return false;
        }

        void OnGUI()
        {
            if (!showDebugHUD) return;
            GUI.color = Color.white;
            GUI.Box(new Rect(8, 8, 360, 130), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            string mode  = input != null ? input.CurrentMode.ToString() : "(no input)";
            string axis  = input != null ? input.DriveAxis.ToString("F2") : "-";
            string boost = input != null && input.BoostHeld ? " <color=#7CC8D8>BOOST</color>" : "";
            GUI.Label(new Rect(16, 14, 340, 22), $"<b>CarController</b>  mode={mode}{boost}", style);
            GUI.Label(new Rect(16, 32, 340, 22), $"DriveAxis = {axis}", style);
            GUI.Label(new Rect(16, 50, 340, 22), $"Speed = {CurrentSpeed:F2}  (cap {maxSpeed})", style);
            GUI.Label(new Rect(16, 68, 340, 22), $"Pos   = {transform.position}", style);
            GUI.Label(new Rect(16, 86, 340, 22), $"Fwd   = {transform.forward.ToString("F2")}", style);
            GUI.Label(new Rect(16,104, 340, 22), $"Cast  = <color=#D88A4A>{_lastHitInfo}</color>", style);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
            Gizmos.matrix = prev;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
        }
#endif
    }
}
