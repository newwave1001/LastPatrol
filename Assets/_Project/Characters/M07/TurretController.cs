using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Systems.Combat;

namespace LastPatrol.Characters.M07
{
    // M-07 자동 사격. 반경 내 IDamageable 적을 자동 조준 + 사격.
    // 적 = IDamageable이지만 마렌/M-07이 아닌 모든 것 (적 차량, 적 보병, 드론 등).
    // 배터리 소모. 해킹 시 비활성.
    public class TurretController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private M07Controller controller;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform muzzle;

        [Header("Detection")]
        [SerializeField] private float detectRange = 12f;
        [Tooltip("적 검색 시 OverlapSphere mask. ~0(Everything)이면 모든 collider 검사 후 진영으로 필터.")]
        [SerializeField] private LayerMask enemyMask = ~0;

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
            if (cooldownTimer <= 0f)
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

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuf[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                // 자기 진영(M-07 자체) 제외 — IDamageable이 M07Controller이거나 같은 transform 계층이면 건너뜀
                if (dmg is M07Controller) continue;
                if (dmg is LastPatrol.Characters.MarenController) continue;

                float d = Vector3.Distance(myPos, col.transform.position);
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
