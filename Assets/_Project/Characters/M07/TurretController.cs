using UnityEngine;
using UnityEngine.InputSystem;
using LastPatrol.Core;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Combat;
using LastPatrol.Systems.Lighting;

namespace LastPatrol.Characters.M07
{
    // M-07 사격.
    //
    // 두 가지 모드:
    //  1) 자동 사격 모드 (S01 인카운터, aimSource 없음): 반경 내 적 자동 조준 + 자동 사격.
    //  2) 마우스 조준 모드 (S03 실내, aimSource=Flashlight): 마우스 방향 콘 안 가장 가까운 적
    //     자동 타겟팅 + 마우스 좌클릭 홀드 시에만 사격. 손전등 ON/OFF는 사격에 영향 없음(시각만).
    //
    // 적 = IDamageable이지만 마렌/M-07이 아닌 모든 것. 차량은 isEnemy=true만.
    // 배터리 소모. 해킹 시 비활성.
    public class TurretController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private M07Controller controller;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform muzzle;
        [Tooltip("마우스 조준 모드에서 LMB 홀드 입력 읽기. 비워두면 자동 검색.")]
        [SerializeField] private InputReader input;

        [Header("Detection")]
        [Tooltip("M-07 자동 타겟팅 범위. 외부 전투 환경 고려 25m 권장.")]
        [SerializeField] private float detectRange = 25f;
        [Tooltip("적 검색 시 OverlapSphere mask. ~0(Everything)이면 모든 collider 검사 후 진영으로 필터.")]
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("Aim Source (optional)")]
        [Tooltip("마렌 손전등 같은 외부 조준 소스. 있으면 그 AimDirection으로 콘 게이팅 + " +
                 "사격은 LMB 홀드 시에만. 비워두면 Awake에서 씬에서 자동 검색.")]
        [SerializeField] private Flashlight aimSource;
        [Tooltip("Flashlight 없는 씬(S01 외부)에서도 마우스 조준+LMB 사격 강제. " +
                 "Camera.main + Mouse 위치로 ground plane에 투사해 조준 방향 계산.")]
        [SerializeField] private bool useMouseAimAlways = true;
        [Tooltip("내장 마우스 조준 시 콘 각도(도). 60° = 뚜렷한 cone.")]
        [SerializeField, Range(15f, 180f)] private float internalConeAngleDeg = 60f;

        [Header("Fire")]
        [SerializeField] private float fireCooldown = 0.4f;
        [SerializeField] private float bulletSpeed = 25f;
        [SerializeField] private float damagePerShot = 12f;
        [SerializeField] private float batteryPerShot = 1f;

        [Header("Telegraph")]
        [SerializeField] private LineRenderer aimLine;

        // currentTarget을 일반화 — Transform만 추적 (IDamageable + GameObject Active 체크).
        private Transform currentTarget;
        private IDamageable currentTargetDamageable;
        private float cooldownTimer;
        private static readonly Collider[] _hitBuf = new Collider[32];

        // 내장 마우스 조준 (Flashlight 없을 때)
        private Camera _cachedCam;
        private Vector3 _internalAimOrigin;
        private Vector3 _internalAimDir;
        private bool _internalAimValid;

        // 외부에서 crosshair 색·상태 결정용
        public bool HasTarget => currentTarget != null;
        public bool MouseAimActive => aimSource != null || useMouseAimAlways;
        public Vector3 AimWorldPosition => _internalAimValid ? _internalAimOrigin + _internalAimDir * 25f : transform.position + transform.forward * 5f;
        public Transform CurrentTargetTransform => currentTarget;
        /// <summary>0 = ready to fire, 1 = just fired (cooldown 진행 중).</summary>
        public float CooldownNormalized => fireCooldown <= 0f ? 0f : Mathf.Clamp01(cooldownTimer / fireCooldown);
        public Vector3 MuzzleWorldPosition => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;

        /// <summary>사격 시 발사 — UI/MuzzleFlash 등 외부 시각 효과용.</summary>
        public static event System.Action<Vector3, Vector3> OnFireEvent; // (muzzlePos, dir)

        void Awake()
        {
            if (controller == null) controller = GetComponent<M07Controller>();
            if (aimSource == null) aimSource = FindAnyObjectByType<Flashlight>(FindObjectsInactive.Include);
            if (input == null) input = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);

            // aimLine 자동 빌드 — 인스펙터에서 안 잡혀있으면 LineRenderer 자식 생성.
            if (aimLine == null) aimLine = BuildAimLine();
            if (aimLine != null) aimLine.enabled = false;
        }

        private LineRenderer BuildAimLine()
        {
            var go = new GameObject("AimLine_Auto");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.startWidth = 0.04f;
            lr.endWidth = 0.04f;
            // 머티리얼 — 기본 default-line 셰이더 (렌더 파이프라인 무관 fallback)
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.49f, 0.78f, 0.85f, 0.85f);
            lr.endColor   = new Color(0.49f, 0.78f, 0.85f, 0.85f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = 5;
            return lr;
        }

        void Update()
        {
            // 1. 종합 상태 덤프 (디버깅용 — 2초마다)
            if (verboseStateDump && Time.time - _lastStateDiagTime > 2f)
            {
                _lastStateDiagTime = Time.time;
                DumpState();
            }

            if (controller == null || !controller.IsAlive || controller.IsHacked)
            {
                Disable();
                return;
            }

            // InputReader 참조가 null이 되면(Maren 비활성/씬 정리 등) 다시 검색.
            // 이게 빠지면 마우스 모드에서 wantFire = false → 영원히 사격 안 함.
            if (input == null)
                input = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);

            // 내장 마우스 조준 계산 (aimSource 없고 useMouseAimAlways 일 때)
            if (aimSource == null && useMouseAimAlways)
                ComputeInternalMouseAim();

            FindNearestEnemy();

            // 시각 갱신은 타겟 유무 무관 — UpdateAimVisual 내부에서 분기 (타겟 있으면 lock line, 없으면 마우스 가이드).
            UpdateAimVisual();

            if (currentTarget == null)
            {
                // 진단 — 사격 안 되는데 적이 분명 있는 경우 게이팅 사유 출력 (1초 throttle).
                if (Time.time - _lastNoTargetDiagTime > 1f)
                {
                    _lastNoTargetDiagTime = Time.time;
                    int aliveCount = CountVisibleAliveEnemies();
                    if (aliveCount > 0)
                        Debug.Log($"[M07.Turret] no target picked despite {aliveCount} alive enemies in range — likely outside aim cone (mouse 방향 확인). aimMode={(aimSource!=null?"flashlight":(useMouseAimAlways?"mouse":"auto"))}");
                }
                return;
            }

            cooldownTimer -= Time.deltaTime;

            // 사격 트리거: aimSource 또는 useMouseAimAlways가 활성이면 LMB 홀드 필요. 둘 다 없으면 자동.
            bool mouseModeActive = aimSource != null || useMouseAimAlways;
            bool wantFire = !mouseModeActive || (input != null && input.FireHeld);

            if (cooldownTimer <= 0f && wantFire)
            {
                if (controller.ConsumeBattery(batteryPerShot))
                {
                    Fire();
                    cooldownTimer = fireCooldown;
                }
                else if (Time.time - _lastBatteryDiagTime > 1f)
                {
                    _lastBatteryDiagTime = Time.time;
                    Debug.Log($"[M07.Turret] fire blocked — battery insufficient ({controller.CurrentBattery:F0}/{controller.MaxBattery:F0})");
                }
            }
            else if (cooldownTimer > 0f && Time.time - _lastWaitDiagTime > 2f)
            {
                _lastWaitDiagTime = Time.time;
                if (mouseModeActive && (input == null || !input.FireHeld))
                    Debug.Log($"[M07.Turret] holding fire — LMB not held (target locked: {currentTarget.name})");
            }
        }

        private float _lastNoTargetDiagTime;
        private float _lastBatteryDiagTime;
        private float _lastWaitDiagTime;
        private float _lastStateDiagTime;

        [Header("Diagnostics")]
        [Tooltip("M-07 사격 안 될 때 모든 게이트 상태를 2초마다 콘솔에 출력. 디버그 끝나면 끄기.")]
        [SerializeField] private bool verboseStateDump = true;

        private void DumpState()
        {
            string ctrlState = controller == null ? "null"
                : $"alive={controller.IsAlive} hacked={controller.IsHacked} hold={controller.IsHolding} battery={controller.CurrentBattery:F0}/{controller.MaxBattery:F0}";
            string aimMode = aimSource != null ? "flashlight" : (useMouseAimAlways ? "mouse" : "auto");
            string targetState = currentTarget != null ? currentTarget.name : "NONE";
            string fireInput = input == null ? "input=null" : $"FireHeld={input.FireHeld} (mode={input.CurrentMode})";
            int visibleEnemies = CountVisibleAliveEnemies();
            Debug.Log($"[M07.Turret.State] ctrl={{{ctrlState}}}, aimMode={aimMode}, target={targetState}, cooldown={cooldownTimer:F2}, {fireInput}, visibleEnemies={visibleEnemies}");
        }

        private int CountVisibleAliveEnemies()
        {
            int n = 0;
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, _hitBuf, enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var col = _hitBuf[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                if (dmg is M07Controller) continue;
                if (dmg is LastPatrol.Characters.MarenController) continue;
                if (dmg is LastPatrol.Systems.Vehicle.VehicleHealth vh && !vh.IsEnemy) continue;
                n++;
            }
            return n;
        }

        private void FindNearestEnemy()
        {
            currentTarget = null;
            currentTargetDamageable = null;

            int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, _hitBuf, enemyMask, QueryTriggerInteraction.Collide);
            float bestDist = detectRange;
            float bestDistFallback = detectRange; // cone 밖이라도 최단거리 적 — fallback
            Transform fallbackTarget = null;
            IDamageable fallbackDamageable = null;
            Vector3 myPos = transform.position;

            // 마우스 조준 모드 — Flashlight (aimSource) 또는 내장 useMouseAimAlways
            bool useCone = false;
            Vector3 coneOrigin = Vector3.zero;
            Vector3 coneDir = Vector3.zero;
            float coneCos = -1f;
            if (aimSource != null)
            {
                useCone = true;
                coneOrigin = aimSource.AimOrigin;
                coneDir = aimSource.AimDirection;
                coneCos = Mathf.Cos(aimSource.ConeAngleDeg * 0.5f * Mathf.Deg2Rad);
            }
            else if (useMouseAimAlways && _internalAimValid)
            {
                useCone = true;
                coneOrigin = _internalAimOrigin;
                coneDir = _internalAimDir;
                coneCos = Mathf.Cos(internalConeAngleDeg * 0.5f * Mathf.Deg2Rad);
            }

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuf[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                // 자기 진영(M-07 자체) 제외
                if (dmg is M07Controller) continue;
                if (dmg is LastPatrol.Characters.MarenController) continue;
                // 차량은 isEnemy=true인 것만 적으로 인식 (마렌 차/주차 차 보호)
                if (dmg is LastPatrol.Systems.Vehicle.VehicleHealth vh && !vh.IsEnemy) continue;

                Vector3 targetPos = col.transform.position;
                float d = Vector3.Distance(myPos, targetPos);

                // Fallback — cone 무시하고 최단거리 적 추적 (cone 안 적 없을 때 backup)
                if (d < bestDistFallback)
                {
                    bestDistFallback = d;
                    fallbackTarget = col.transform;
                    fallbackDamageable = dmg;
                }

                // 손전등 콘 게이팅
                if (useCone)
                {
                    Vector3 toTarget = targetPos - coneOrigin;
                    if (toTarget.sqrMagnitude < 0.0001f) continue;
                    Vector3 toTargetN = toTarget.normalized;
                    if (Vector3.Dot(coneDir, toTargetN) < coneCos) continue; // 콘 밖
                }

                if (d < bestDist)
                {
                    bestDist = d;
                    currentTarget = col.transform;
                    currentTargetDamageable = dmg;
                }
            }

            // Cone 안 타겟 없으면 fallback 사용 — 마우스가 가리키지 않는 적도 자동 사격 (전투 중 끊김 방지).
            if (currentTarget == null && fallbackTarget != null)
            {
                currentTarget = fallbackTarget;
                currentTargetDamageable = fallbackDamageable;
            }
        }

        private void Fire()
        {
            if (bulletPrefab == null) { Debug.LogWarning("[M07.Turret] bulletPrefab null — cannot fire"); return; }
            if (currentTarget == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;
            Vector3 to = currentTarget.position + Vector3.up * 1.0f;
            Vector3 dir = (to - from).normalized;
            Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Robot, bulletSpeed, damagePerShot);
            OnFireEvent?.Invoke(from, dir);
            Debug.Log($"[M07.Turret] fire → {currentTarget.name} (dist={Vector3.Distance(from, currentTarget.position):F1}m)");
        }

        private void UpdateAimVisual()
        {
            if (aimLine == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;

            if (currentTarget != null)
            {
                // 타겟 락 — 두꺼운 cyan 라인으로 무기 → 적
                aimLine.enabled = true;
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, from);
                aimLine.SetPosition(1, currentTarget.position + Vector3.up * 1.0f);

                aimLine.startWidth = 0.06f;
                aimLine.endWidth = 0.04f;
                Color hot = new Color(0.49f, 0.78f, 0.85f, 0.95f); // cyan
                aimLine.startColor = hot;
                aimLine.endColor = hot;
            }
            else if (MouseAimActive && _internalAimValid)
            {
                // 타겟 없음 — 마우스 방향 가이드 라인 (얇고 흐림)
                aimLine.enabled = true;
                aimLine.positionCount = 2;
                Vector3 to = _internalAimOrigin + _internalAimDir * 18f;
                to.y = from.y;
                aimLine.SetPosition(0, from);
                aimLine.SetPosition(1, to);

                aimLine.startWidth = 0.03f;
                aimLine.endWidth = 0.005f;
                Color cool = new Color(0.49f, 0.78f, 0.85f, 0.35f);
                Color tail = new Color(0.49f, 0.78f, 0.85f, 0.0f);
                aimLine.startColor = cool;
                aimLine.endColor = tail;
            }
            else
            {
                aimLine.enabled = false;
            }
        }

        private void Disable()
        {
            if (aimLine != null) aimLine.enabled = false;
            currentTarget = null;
            currentTargetDamageable = null;
        }

        // Camera.main + Mouse 위치를 ground plane에 투사해 마우스 조준 방향 계산.
        // Flashlight 없는 외부 운전 씬에서도 동일한 마우스 조준+LMB 사격 사용 가능.
        private void ComputeInternalMouseAim()
        {
            if (_cachedCam == null || !_cachedCam.gameObject.activeInHierarchy)
            {
                _cachedCam = Camera.main;
                if (_cachedCam == null)
                {
                    var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                    if (cams != null && cams.Length > 0) _cachedCam = cams[0];
                }
            }
            if (_cachedCam == null || Mouse.current == null)
            {
                _internalAimValid = false;
                return;
            }

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = _cachedCam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            Plane ground = new Plane(Vector3.up, transform.position.y);
            if (ground.Raycast(ray, out float t))
            {
                Vector3 worldHit = ray.GetPoint(t);
                _internalAimOrigin = transform.position;
                Vector3 dir = (worldHit - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-3f)
                {
                    _internalAimDir = dir.normalized;
                    _internalAimValid = true;
                    return;
                }
            }
            _internalAimValid = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectRange);
        }
    }
}
