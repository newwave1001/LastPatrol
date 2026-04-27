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
        [Tooltip("v04 비율 유지: acceleration / friction ≈ maxSpeed × 0.95. " +
                 "마찰 2.1 + maxSpeed 18 기준 36이 평형/cap = 95% (v04 동일).")]
        [SerializeField] private float acceleration = 36f;
        [Tooltip("곱셈식 damping rate(1/sec). v04 0.965^60 ≈ 0.117 매칭 → -ln(0.117) ≈ 2.14")]
        [SerializeField] private float friction = 2.1f;
        [Tooltip("입력 없을 때만 적용되는 정지 스냅. 너무 크면 고주사율에서 출발 막힘.")]
        [SerializeField] private float stopThreshold = 0.05f;

        public enum SteeringMode
        {
            // 입력 방향(WASD)을 월드 절대 방향으로 해석. 차가 자동 회전.
            // 직부감 게임 직관: W=위쪽, A=왼쪽 — 차가 어느 방향을 보든 동일.
            WorldRelative,
            // v04 mockup 그대로 차량 기준 회전. 차가 뒤로 돌면 화면상 좌우 반전.
            // 마우스 클릭 자동 운전과 함께 쓸 때 자연스러움. 후진(S) 시뮬 가능.
            CarRelative,
        }

        [Header("Steering")]
        [Tooltip("WorldRelative 권장 (직부감 직관). CarRelative는 v04 정신(현실 핸들).")]
        [SerializeField] private SteeringMode steeringMode = SteeringMode.WorldRelative;
        [Tooltip("v04 0.048*60 ≈ 2.88 rad/s")]
        [SerializeField] private float maxTurnRate = 2.9f;
        [Tooltip("CarRelative에서 사용. 이 속도 이상이면 풀 회전, 미만이면 비례 감소 (정지 시 회전 X)")]
        [SerializeField] private float fullTurnSpeed = 9f;
        [Tooltip("WorldRelative에서 사용. 차가 입력 방향과 어긋난 만큼 가속 감소(cos). " +
                 "이 값이면 입력과 차 forward 사이 각도 90° 이상이면 가속 0.")]
        [SerializeField, Range(0f, 1f)] private float worldRelativeMinFacing = 0f;

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

            float capForward = boost ? maxSpeed * boostMultiplier : maxSpeed;
            float capReverse = maxSpeed * reverseMultiplier;

            // 모드별 가속/조향
            float throttleEquivalent;
            if (steeringMode == SteeringMode.WorldRelative)
                throttleEquivalent = UpdateWorldRelative(axis, dt);
            else
                throttleEquivalent = UpdateCarRelative(axis, dt);

            // Speed cap
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, -capReverse, capForward);

            // Friction (multiplicative, frame-rate independent).
            // v04: speed *= (1 - 0.035) at 60fps → 1초 후 ≈ 11.7% 잔존.
            CurrentSpeed *= Mathf.Exp(-friction * dt);

            // Stop snap — 입력 없을 때만 적용 (고주사율에서 출발 막힘 방지).
            if (Mathf.Abs(throttleEquivalent) < 0.01f && Mathf.Abs(CurrentSpeed) < stopThreshold)
                CurrentSpeed = 0f;

            // --- Movement + collision (BoxCast slide) ---
            Vector3 velocity = transform.forward * CurrentSpeed;
            Vector3 delta = velocity * dt;
            if (delta.sqrMagnitude > 1e-6f)
                MoveWithSlide(delta);
        }

        /// <summary>
        /// v04 mockup 그대로: 차량 기준 조향, 후진 가능. 정지 시 회전 X.
        /// 반환값은 stopThreshold 가드용 throttle 신호 (입력 누름 여부).
        /// </summary>
        private float UpdateCarRelative(Vector2 axis, float dt)
        {
            float throttle = axis.y;
            if (throttle > 0f)
                CurrentSpeed += acceleration * throttle * dt;
            else if (throttle < 0f)
                CurrentSpeed += acceleration * throttle * reverseMultiplier * dt;

            float steer = axis.x;
            if (Mathf.Abs(steer) > 0.01f && Mathf.Abs(CurrentSpeed) > 0.05f)
            {
                float speedFactor = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / fullTurnSpeed);
                float dir = Mathf.Sign(CurrentSpeed);
                float yawDelta = steer * maxTurnRate * speedFactor * dir * dt * Mathf.Rad2Deg;
                transform.Rotate(0f, yawDelta, 0f, Space.World);
            }
            return throttle;
        }

        /// <summary>
        /// 직부감 GTA 류 운전: 입력 방향을 월드 절대 방향으로 해석.
        /// 차량이 그 방향을 향해 자동 회전, 향한 정도에 비례해 가속.
        /// 후진 개념 없음(S = 화면 아래쪽으로 회전+이동).
        /// </summary>
        private float UpdateWorldRelative(Vector2 axis, float dt)
        {
            Vector3 desiredDir = new Vector3(axis.x, 0f, axis.y);
            float magnitude = desiredDir.magnitude;
            if (magnitude < 0.01f) return 0f;

            Vector3 dirNorm = desiredDir / magnitude;
            float desiredYawDeg = Mathf.Atan2(dirNorm.x, dirNorm.z) * Mathf.Rad2Deg;
            float currentYaw = transform.eulerAngles.y;
            float yawDiff = Mathf.DeltaAngle(currentYaw, desiredYawDeg);

            // 정지 시도 회전 가능 (직부감 직관 우선). 단 maxTurnRate 클램프.
            float maxYawThisFrame = maxTurnRate * Mathf.Rad2Deg * dt;
            transform.Rotate(0f, Mathf.Clamp(yawDiff, -maxYawThisFrame, maxYawThisFrame), 0f, Space.World);

            // 입력 방향과 차량 forward의 정렬도(cos). 0~1 클램프 후 worldRelativeMinFacing로 floor.
            float cos = Mathf.Cos(Mathf.Abs(yawDiff) * Mathf.Deg2Rad);
            float facing = Mathf.Max(worldRelativeMinFacing, cos);

            CurrentSpeed += acceleration * Mathf.Clamp01(magnitude) * facing * dt;
            return Mathf.Clamp01(magnitude); // 어떤 키든 누르고 있으면 stopThreshold 가드 통과
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
            GUI.Box(new Rect(8, 8, 380, 180), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            string mode  = input != null ? input.CurrentMode.ToString() : "(no input)";
            string axis  = input != null ? input.DriveAxis.ToString("F2") : "-";
            string boost = input != null && input.BoostHeld ? " <color=#7CC8D8>BOOST</color>" : "";
            float fps = 1f / Mathf.Max(Time.deltaTime, 1e-5f);
            GUI.Label(new Rect(16, 14, 360, 22), $"<b>CarController</b>  mode={mode}{boost}", style);
            GUI.Label(new Rect(16, 32, 360, 22), $"DriveAxis = {axis}    fps≈{fps:F0}", style);
            GUI.Label(new Rect(16, 50, 360, 22), $"<color=#D88A4A>Speed = {CurrentSpeed:F4}</color>  cap {maxSpeed}", style);
            GUI.Label(new Rect(16, 68, 360, 22), $"Pos   = {transform.position}", style);
            GUI.Label(new Rect(16, 86, 360, 22), $"Fwd   = {transform.forward.ToString("F2")}", style);
            GUI.Label(new Rect(16,104, 360, 22), $"Cast  = <color=#D88A4A>{_lastHitInfo}</color>", style);
            GUI.Label(new Rect(16,122, 360, 22), $"<i>(inspector params)</i>", style);
            GUI.Label(new Rect(16,140, 360, 22), $"accel={acceleration}  friction={friction}  stopThr={stopThreshold}", style);
            GUI.Label(new Rect(16,158, 360, 22), $"maxSpeed={maxSpeed}  boost×{boostMultiplier}  rev×{reverseMultiplier}", style);
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
