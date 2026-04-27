using UnityEngine;
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
        [SerializeField] private float detectRange = 12f;
        [Tooltip("적 검색 시 OverlapSphere mask. ~0(Everything)이면 모든 collider 검사 후 진영으로 필터.")]
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("Aim Source (optional)")]
        [Tooltip("마렌 손전등 같은 외부 조준 소스. 있으면 그 AimDirection으로 콘 게이팅 + " +
                 "사격은 LMB 홀드 시에만. 비워두면 Awake에서 씬에서 자동 검색.")]
        [SerializeField] private Flashlight aimSource;

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

        void Awake()
        {
            if (controller == null) controller = GetComponent<M07Controller>();
            if (aimLine != null) aimLine.enabled = false;
            if (aimSource == null) aimSource = FindAnyObjectByType<Flashlight>(FindObjectsInactive.Include);
            if (input == null) input = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);
        }

        void Update()
        {
            if (controller == null || !controller.IsAlive || controller.IsHacked)
            {
                Disable();
                return;
            }

            FindNearestEnemy();
            if (currentTarget == null)
            {
                Disable();
                return;
            }

            UpdateAimVisual();

            cooldownTimer -= Time.deltaTime;

            // 마우스 조준 모드(aimSource 있음)에선 LMB 홀드 시에만 사격.
            // 자동 사격 모드(aimSource 없음)는 기존대로 쿨다운 풀리면 자동 발사.
            bool wantFire = (aimSource == null) || (input != null && input.FireHeld);

            if (cooldownTimer <= 0f && wantFire)
            {
                if (controller.ConsumeBattery(batteryPerShot))
                {
                    Fire();
                    cooldownTimer = fireCooldown;
                }
            }
        }

        private void FindNearestEnemy()
        {
            currentTarget = null;
            currentTargetDamageable = null;

            int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, _hitBuf, enemyMask, QueryTriggerInteraction.Collide);
            float bestDist = detectRange;
            Vector3 myPos = transform.position;

            // 마우스 조준 모드(aimSource 있음)에선 콘 안 적만 후보. ON/OFF 무관 — 손전등이 꺼져도
            // 마우스 방향이 갱신되므로 사격 콘은 살아있음. 콘 각도는 손전등 spotAngle 그대로 (단일 소스).
            bool useCone = aimSource != null;
            Vector3 coneOrigin = useCone ? aimSource.AimOrigin : Vector3.zero;
            Vector3 coneDir    = useCone ? aimSource.AimDirection : Vector3.zero;
            float coneCos      = useCone ? Mathf.Cos(aimSource.ConeAngleDeg * 0.5f * Mathf.Deg2Rad) : -1f;

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

                // 손전등 콘 게이팅
                if (useCone)
                {
                    Vector3 toTarget = targetPos - coneOrigin;
                    if (toTarget.sqrMagnitude < 0.0001f) continue;
                    Vector3 toTargetN = toTarget.normalized;
                    if (Vector3.Dot(coneDir, toTargetN) < coneCos) continue; // 콘 밖
                }

                float d = Vector3.Distance(myPos, targetPos);
                if (d < bestDist)
                {
                    bestDist = d;
                    currentTarget = col.transform;
                    currentTargetDamageable = dmg;
                }
            }
        }

        private void Fire()
        {
            if (bulletPrefab == null || currentTarget == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;
            Vector3 to = currentTarget.position + Vector3.up * 1.0f;
            Vector3 dir = (to - from).normalized;
            Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Robot, bulletSpeed, damagePerShot);
        }

        private void UpdateAimVisual()
        {
            if (aimLine == null || currentTarget == null) return;
            if (!aimLine.enabled) aimLine.enabled = true;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;
            Vector3 to = currentTarget.position + Vector3.up * 1.0f;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, to);
        }

        private void Disable()
        {
            if (aimLine != null) aimLine.enabled = false;
            currentTarget = null;
            currentTargetDamageable = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectRange);
        }
    }
}
