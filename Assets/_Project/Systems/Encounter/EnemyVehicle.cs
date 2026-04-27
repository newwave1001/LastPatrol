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
        [SerializeField] private float maxSpeed = 16f;
        [Tooltip("스폰 직후 시작 속도. 0이면 정지 상태로 가속, maxSpeed면 이미 풀스피드로 달려오는 느낌. " +
                 "위협적 진입을 위해 maxSpeed 80% 정도 권장.")]
        [SerializeField] private float startSpeed = 13f;
        [SerializeField] private float acceleration = 28f;
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

            if (target == null)
            {
                var maren = FindAnyObjectByType<CarController>();
                if (maren != null) target = maren.transform;
            }

            // 위협적 진입 — 이미 달려오는 차로 시작
            CurrentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        }

        void OnDestroy()
        {
            if (_selfHealth != null) _selfHealth.OnDeath -= HandleSelfDeath;
        }

        private void HandleSelfDeath()
        {
            _disabled = true;
            CurrentSpeed = 0f;
            if (destroyAfterDeath >= 0f) Destroy(gameObject, destroyAfterDeath);
        }

        void Update()
        {
            if (_disabled || target == null) return;

            float dt = Time.deltaTime;
            _aliveTime += dt;

            // Despawn distance — 마렌과 멀리 떨어지면 그냥 사라짐
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget > despawnDistance)
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
            // flee 시간 초과 → destroy
            if (IsFleeing && Time.time - _fleeStartTime >= fleeDuration)
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

            CurrentSpeed += acceleration * throttle * dt;
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, maxSpeed);
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
