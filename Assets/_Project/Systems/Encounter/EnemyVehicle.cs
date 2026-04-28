using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Systems.AI;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.Encounter
{
    /// <summary>
    /// 인간형 적 차량. 마렌 차를 단순 추격 (Y축 yaw 자동 정렬 + 가속). BoxCast 충돌 시 박치기 데미지.
    /// 그레이박스용. 폴리싱 단계에서 NavMesh, 측면 박치기 패턴, 회피 등 추가.
    ///
    /// 자체 VehicleHealth도 부착해두면 마렌이 적을 박을 때 적도 데미지(상호 박치기).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyVehicle : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Driving")]
        [SerializeField] private float maxSpeed = 18.4f;
        [Tooltip("스폰 직후 시작 속도. 0이면 정지 상태로 가속, maxSpeed면 이미 풀스피드로 달려오는 느낌. " +
                 "낮을수록 플레이어가 인식할 시간 확보.")]
        [SerializeField] private float startSpeed = 9.2f;
        [SerializeField] private float acceleration = 32.2f;
        [Tooltip("damping rate(1/sec). 마렌 차 friction과 동일 정신.")]
        [SerializeField] private float friction = 1.6f;
        [Tooltip("자동 회전 속도(rad/sec).")]
        [SerializeField] private float maxTurnRate = 2.4f;
        [Tooltip("타겟이 이 거리 안에 들어오면 박치기 직전 약간 감속.")]
        [SerializeField] private float closeBrakeDistance = 6f;

        [Header("Ram (박치기)")]
        [SerializeField] private float ramDamage = 12f;
        [Tooltip("박치기 후 다시 데미지 가능까지 대기(초). 너무 짧으면 한 번에 다 깎임.")]
        [SerializeField] private float ramCooldown = 0.6f;
        [SerializeField] private DamageSource ramDamageSource = DamageSource.Enemy;
        [Header("Ram Charge (100km/h 돌진)")]
        [Tooltip("플레이어 거리 이 값 미만 + 정렬 시 차징 모드. 28 m/s ≒ 100 km/h.")]
        [SerializeField] private float ramChargeSpeed = 28f;
        [SerializeField] private float ramChargeDistance = 18f;
        [Tooltip("yawDiff 이 값 이내일 때만 차징 (도). 잘 정렬된 직진 박치기.")]
        [SerializeField] private float ramChargeAlignDeg = 30f;

        [Header("Ambush (박치기 후)")]
        [Tooltip("플레이어 차에 박치기 hit 시 spawn할 휴머노이드 prefab. " +
                 "어떤 prefab이든 드래그 가능 (OutdoorHumanoid 컴포넌트 자동 검출/추가).")]
        [SerializeField] private GameObject humanoidPrefab;
        [Tooltip("humanoidPrefab이 null일 때 fallback으로 만든 휴머노이드에 강제 부여할 bulletPrefab. " +
                 "P_DroneBullet 등 일반 적 총알 드래그.")]
        [SerializeField] private GameObject bulletPrefabForHumanoidFallback;
        [SerializeField] private int humanoidsPerAmbush = 2;
        [Tooltip("자기 차 옆쪽 어디에 배치할지 (좌/우 offset).")]
        [SerializeField] private float humanoidSideOffset = 2.5f;
        [Tooltip("스폰 후 이 시간(초) 안엔 AMBUSH 트리거 안 됨. 즉발 ambush 방지 안전망.")]
        [SerializeField] private float ambushGracePeriod = 1.5f;
        [Tooltip("AMBUSH 진단 로그 활성. 무엇에 박치고 무엇이 매칭됐는지 출력.")]
        [SerializeField] private bool logRamHits = true;

        [Header("Collision (BoxCast)")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.9f, 0.5f, 1.6f);
        [SerializeField] private float skin = 0.05f;
        [SerializeField, Range(0.1f, 1f)] private float collisionSpeedDamp = 0.4f;

        [Header("Steering / Avoidance")]
        [Tooltip("좌/우 회피 raycast 거리. 차량 박스 반대각 + 좀 더.")]
        [SerializeField] private float steerLookAhead = 6f;

        [Header("Lifecycle")]
        [Tooltip("자체 HP가 0 되면 N초 후 자동 소멸.")]
        [SerializeField] private float destroyAfterDeath = 1.5f;
        [Tooltip("스폰 후 추격 가능 시간(초). 이후 flee 모드 진입.")]
        [SerializeField] private float maxChaseLifetime = 45f;
        [Tooltip("flee 모드 지속 시간(초). 끝나면 destroy.")]
        [SerializeField] private float fleeDuration = 6f;
        [Tooltip("타겟과 이 거리 이상 멀어지면 즉시 destroy.")]
        [SerializeField] private float despawnDistance = 80f;

        public float CurrentSpeed { get; private set; }
        public Transform Target { get => target; set => target = value; }
        public bool IsFleeing { get; private set; }

        private VehicleHealth _selfHealth;
        private float _lastRamTime = -999f;
        private bool _disabled;
        private float _aliveTime;
        private float _fleeStartTime;
        private bool _rammedPlayer; // ambush 트리거용 sentinel
        private float _spawnTime;
        private Collider[] _ownColliders;
        private Collider _myColliderForPenetration;
        private static readonly RaycastHit[] _hitBuf = new RaycastHit[16];
        private static readonly Collider[] _penetBuf = new Collider[16];

        void Awake()
        {
            _ownColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _ownColliders.Length; i++)
            {
                if (_ownColliders[i] != null && !_ownColliders[i].isTrigger)
                {
                    _myColliderForPenetration = _ownColliders[i];
                    break;
                }
            }
            _selfHealth = GetComponent<VehicleHealth>();
            if (_selfHealth != null) _selfHealth.OnDeath += HandleSelfDeath;

            // 옛 직렬화 값(16/8/28) 자동 보정 — 15% 가속 권장값(18.4/9.2/32.2)으로 sync.
            // 인스펙터/prefab에 옛 값 박혀있어도 코드 변경 시점부터 자동 적용.
            if (Mathf.Approximately(maxSpeed, 16f) && Mathf.Approximately(startSpeed, 8f) && Mathf.Approximately(acceleration, 28f))
            {
                Debug.LogWarning("[EnemyVehicle] 옛 속도 값(16/8/28) 검출 → 권장(18.4/9.2/32.2)으로 자동 보정. prefab 저장 권장.", this);
                maxSpeed = 18.4f;
                startSpeed = 9.2f;
                acceleration = 32.2f;
            }

            if (target == null)
            {
                // VehicleDismount.CurrentCar 우선 — ParkedCar 같은 다른 CarController 잘못 잡지 않게
                var dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
                if (dismount != null && dismount.CurrentCar != null)
                    target = dismount.CurrentCar.transform;
                else
                {
                    var anyCar = FindAnyObjectByType<CarController>();
                    if (anyCar != null) target = anyCar.transform;
                }
            }

            // 위협적 진입 — 이미 달려오는 차로 시작
            CurrentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
            _spawnTime = Time.time;
        }

        void OnDestroy()
        {
            if (_selfHealth != null) _selfHealth.OnDeath -= HandleSelfDeath;
        }

        private void HandleSelfDeath()
        {
            _disabled = true;
            CurrentSpeed = 0f;
            // destroyAfterDeath 초 대기 → 그 후 화면 밖일 때만 destroy.
            // 시야 안에 잔재가 남아도 갑자기 사라지지 않음 (시각적 일관성).
            if (destroyAfterDeath >= 0f) StartCoroutine(DeathCleanupRoutine(destroyAfterDeath));
        }

        private System.Collections.IEnumerator DeathCleanupRoutine(float minWait)
        {
            // 최소 대기 — sound·effects 표현 시간
            yield return new WaitForSeconds(minWait);
            // 그 후 화면 밖이 될 때까지 polling (1초 간격)
            while (IsOnPlayerScreen())
                yield return new WaitForSeconds(1f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (_disabled || target == null) return;

            float dt = Time.deltaTime;
            _aliveTime += dt;

            // Despawn distance — 마렌과 멀리 떨어지고 화면 밖에 있을 때만 destroy
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget > despawnDistance && !IsOnPlayerScreen())
            {
                Destroy(gameObject);
                return;
            }

            // Lifetime 도달 → flee 모드 진입
            if (!IsFleeing && _aliveTime >= maxChaseLifetime)
            {
                IsFleeing = true;
                _fleeStartTime = Time.time;
            }
            // flee 시간 초과 + 화면 밖일 때만 destroy
            // (화면 안에 보이는 동안엔 절대 사라지지 않음 — 플레이어가 시야에서 놓친 후에만 정리)
            if (IsFleeing && Time.time - _fleeStartTime >= fleeDuration && !IsOnPlayerScreen())
            {
                Destroy(gameObject);
                return;
            }

            // 추격 또는 도주에 따른 desired direction
            Vector3 desired;
            if (IsFleeing)
            {
                // 마렌으로부터 멀어지는 방향
                desired = transform.position - target.position;
            }
            else
            {
                desired = target.position - transform.position;
            }
            desired.y = 0f;
            if (desired.sqrMagnitude < 0.01f) return;

            // 장애물 회피 — 7방향 cast 중 가장 빈 방향
            Vector3 steered = SteeringHelper.ResolveDirection(
                desired, transform.position, steerLookAhead, obstacleMask);

            // 자동 회전 (steered 방향으로)
            float desiredYaw = Mathf.Atan2(steered.x, steered.z) * Mathf.Rad2Deg;
            float currentYaw = transform.eulerAngles.y;
            float yawDiff = Mathf.DeltaAngle(currentYaw, desiredYaw);
            float maxYawThisFrame = maxTurnRate * Mathf.Rad2Deg * dt;
            transform.Rotate(0f, Mathf.Clamp(yawDiff, -maxYawThisFrame, maxYawThisFrame), 0f, Space.World);

            // 가속 (정렬도 cos 가중)
            float facing = Mathf.Max(0f, Mathf.Cos(Mathf.Abs(yawDiff) * Mathf.Deg2Rad));
            float throttle = facing;

            // chase 모드에서만 근접 감속. flee는 가능한 빠르게 도주.
            if (!IsFleeing && distanceToTarget < closeBrakeDistance)
                throttle *= Mathf.Clamp01(distanceToTarget / closeBrakeDistance);

            // Ram charge — 가까이 + 정렬 시 100km/h 차징
            float effectiveMax = maxSpeed;
            if (!IsFleeing && !_rammedPlayer && distanceToTarget < ramChargeDistance && Mathf.Abs(yawDiff) < ramChargeAlignDeg)
                effectiveMax = ramChargeSpeed;

            CurrentSpeed += acceleration * throttle * dt;
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, effectiveMax);
            CurrentSpeed *= Mathf.Exp(-friction * dt);

            Vector3 delta = transform.forward * CurrentSpeed * dt;
            if (delta.sqrMagnitude > 1e-6f) MoveWithCollision(delta);
        }

        private void MoveWithCollision(Vector3 delta)
        {
            float distMove = delta.magnitude;
            Vector3 dir = delta / distMove;
            int count = Physics.BoxCastNonAlloc(transform.position, boxHalfExtents, dir, _hitBuf, transform.rotation, distMove + skin, obstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = _hitBuf[i].collider;
                if (c == null || IsOwn(c)) continue;

                // 박치기 데미지 시도
                if (Time.time - _lastRamTime > ramCooldown)
                {
                    var dmg = c.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsAlive)
                    {
                        dmg.TakeDamage(ramDamage, ramDamageSource);
                        _lastRamTime = Time.time;

                        // 마렌 차에 박치기 → ambush 발동 (1회만)
                        // VehicleDismount.CurrentCar = 진짜 플레이어 차. ParkedCar 같은 다른 non-enemy 차량 잘못 트리거 방지.
                        if (!_rammedPlayer && dmg is VehicleHealth vh && !vh.IsEnemy)
                        {
                            CarController playerCar = null;
                            var dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
                            if (dismount != null) playerCar = dismount.CurrentCar;
                            bool isPlayerCar = playerCar != null && vh.GetComponent<CarController>() == playerCar;
                            bool gracePassed = Time.time - _spawnTime >= ambushGracePeriod;

                            if (logRamHits)
                                Debug.Log($"[EnemyVehicle] ram hit: vh='{vh.name}' playerCar='{(playerCar != null ? playerCar.name : "NULL")}' isPlayer={isPlayerCar} grace={gracePassed} (t-spawn={Time.time - _spawnTime:F2}s)", this);

                            if (isPlayerCar && gracePassed)
                            {
                                _rammedPlayer = true;
                                TriggerAmbush(vh);
                            }
                        }
                    }
                }
                CurrentSpeed *= collisionSpeedDamp;

                // 한 축 슬라이드
                Vector3 dx = new Vector3(delta.x, 0f, 0f);
                Vector3 dz = new Vector3(0f, 0f, delta.z);
                if (!CastBlocked(dx)) transform.position += dx;
                if (!CastBlocked(dz)) transform.position += dz;
                return;
            }

            transform.position += delta;
        }

        void LateUpdate()
        {
            if (_myColliderForPenetration == null) return;

            int count = Physics.OverlapBoxNonAlloc(
                transform.position,
                boxHalfExtents,
                _penetBuf,
                transform.rotation,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var other = _penetBuf[i];
                if (other == null || other.isTrigger) continue;
                if (IsOwn(other)) continue;
                if (Physics.ComputePenetration(
                    _myColliderForPenetration,
                    _myColliderForPenetration.transform.position,
                    _myColliderForPenetration.transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 dir, out float dist))
                {
                    if (dist > 0.001f)
                    {
                        transform.position += dir * dist;
                        CurrentSpeed *= 0.6f;
                    }
                }
            }
        }

        private bool CastBlocked(Vector3 d)
        {
            float dist = d.magnitude;
            if (dist < 1e-5f) return false;
            Vector3 dir = d / dist;
            int count = Physics.BoxCastNonAlloc(transform.position, boxHalfExtents, dir, _hitBuf, transform.rotation, dist + skin, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (_hitBuf[i].collider != null && !IsOwn(_hitBuf[i].collider)) return true;
            return false;
        }

        private bool IsOwn(Collider c)
        {
            if (_ownColliders == null) return false;
            for (int i = 0; i < _ownColliders.Length; i++)
                if (_ownColliders[i] == c) return true;
            return false;
        }

        // 플레이어 카메라 viewport 안에 있는지 — despawn 안전 가드.
        // 화면에 보이는 적 차가 갑자기 사라지면 어색하므로, 시야 안엔 무조건 유지.
        private static Camera _despawnCachedCam;
        private bool IsOnPlayerScreen()
        {
            if (_despawnCachedCam == null || !_despawnCachedCam.gameObject.activeInHierarchy)
                _despawnCachedCam = Camera.main;
            if (_despawnCachedCam == null) return false; // 카메라 없으면 안전 보수적으로 false (= 사라져도 됨)
            const float margin = 0.1f;
            Vector3 vp = _despawnCachedCam.WorldToViewportPoint(transform.position);
            return vp.z > 0f
                && vp.x >= -margin && vp.x <= 1f + margin
                && vp.y >= -margin && vp.y <= 1f + margin;
        }

        // 마렌 차 박치기 hit 시: 자기 정지 + 마렌 차 가동 불능 + 휴머노이드 2명 spawn
        private void TriggerAmbush(VehicleHealth playerVH)
        {
            // 중복 트리거 가드 — 이미 disabled 또는 _rammedPlayer 면 skip.
            // 빠르게 두 번 ram 충돌 시 같은 EnemyVehicle이 AMBUSH 두 번 호출될 수 있음.
            if (_disabled) return;
            // 자기 정지 (chase 멈춤, 자체 destroy도 stop)
            CurrentSpeed = 0f;
            _disabled = true;
            // 적 차량을 non-enemy로 전환 → M-07이 더 이상 사격하지 않음 (휴머노이드 우선 타겟)
            if (_selfHealth != null) _selfHealth.SetEnemy(false);

            // 콜라이더 모두 disable — 이후 다른 EnemyVehicle BoxCast나 물리 overlap이 이 차에
            // 끼이는 것 방지 (전투 중 프레임 락 원인 차단).
            var ownCols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownCols.Length; i++)
                if (ownCols[i] != null) ownCols[i].enabled = false;
            // Rigidbody 있으면 isKinematic + 운동 제거
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            // 일정 시간 후 정리 — 시야 안에 있으면 더 기다림 (DeathCleanupRoutine과 동일).
            StartCoroutine(DeathCleanupRoutine(8f));

            // 마렌 차 이동 불가
            var playerCar = playerVH.GetComponent<CarController>();
            if (playerCar != null) playerCar.SetImmobilized(true);

            // 휴머노이드 spawn — 적 차 "앞쪽 양 옆"에 배치.
            // 차량 콜라이더 bounds로 스폰 거리 자동 산정 (메시 겹침 방지).
            float carLengthHalf = 2.0f; // fallback (typical car ~4m long)
            float carWidthHalf  = 1.0f;
            var carCol = GetComponent<Collider>();
            if (carCol != null)
            {
                Vector3 ext = carCol.bounds.extents;
                carLengthHalf = Mathf.Max(carLengthHalf, ext.z);
                carWidthHalf  = Mathf.Max(carWidthHalf,  ext.x);
            }
            float forwardClearance = carLengthHalf + 1.0f;        // 차 앞쪽으로 빼서 메시 안 침범
            float sideClearance    = Mathf.Max(humanoidSideOffset, carWidthHalf + 1.0f);

            for (int i = 0; i < humanoidsPerAmbush; i++)
            {
                Vector3 sideOffset = transform.right * (i % 2 == 0 ? -sideClearance : sideClearance);
                Vector3 forwardOffset = transform.forward * (forwardClearance + (i / 2) * 1.2f);
                Vector3 pos = transform.position + sideOffset + forwardOffset;
                pos.y = 0.05f;
                Quaternion rot = Quaternion.LookRotation(target != null ? (target.position - pos).normalized : transform.forward);

                GameObject go;
                if (humanoidPrefab != null)
                {
                    go = Instantiate(humanoidPrefab, pos, rot);
                }
                else
                {
                    go = new GameObject($"Humanoid_{i}");
                    go.transform.position = pos;
                    go.transform.rotation = rot;
                }
                // OutdoorHumanoid 자동 검출 + 없으면 추가
                var h = go.GetComponent<OutdoorHumanoid>();
                if (h == null) h = go.AddComponent<OutdoorHumanoid>();
                h.SetTarget(target);
                // bulletPrefab 보장 — prefab 없이 new GameObject로 만들어진 휴머노이드는 bulletPrefab이 null이라
                // 직접 데미지로 fallback 됨. 이 EnemyVehicle 자체의 bulletPrefab 으로 보정.
                h.EnsureBulletPrefab(bulletPrefabForHumanoidFallback);
                Debug.Log($"[EnemyVehicle] spawn humanoid#{i} at {pos} (forwardClr={forwardClearance:F1}, sideClr={sideClearance:F1})", go);
            }

            Debug.Log($"[EnemyVehicle] AMBUSH! 마렌 차 immobilized + 휴머노이드 {humanoidsPerAmbush}명 spawn", this);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.66f, 0.19f, 0.16f, 0.6f);
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
            Gizmos.matrix = prev;
            if (target != null)
            {
                Gizmos.color = new Color(0.66f, 0.19f, 0.16f, 0.4f);
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
#endif
    }
}
