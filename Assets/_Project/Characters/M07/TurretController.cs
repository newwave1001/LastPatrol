using UnityEngine;
using LastPatrol.Characters.Enemies;
using LastPatrol.Systems.Combat;

namespace LastPatrol.Characters.M07
{
    // M-07 자동 사격. 반경 내 가장 가까운 살아있는 적을 자동 조준 + 사격.
    // 배터리 소모. 해킹 시 비활성 (M07Controller가 isHacked 체크).
    public class TurretController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private M07Controller controller;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform muzzle;

        [Header("Detection")]
        [SerializeField] private float detectRange = 12f;

        [Header("Fire")]
        [SerializeField] private float fireCooldown = 0.4f;
        [SerializeField] private float bulletSpeed = 25f;
        [SerializeField] private float damagePerShot = 12f;
        [SerializeField] private float batteryPerShot = 1f;

        [Header("Telegraph")]
        [SerializeField] private LineRenderer aimLine;

        private EnemyAI currentTarget;
        private float cooldownTimer;

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

            currentTarget = FindNearestEnemy();
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

        private EnemyAI FindNearestEnemy()
        {
            var enemies = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
            EnemyAI best = null;
            float bestDist = detectRange;
            Vector3 myPos = transform.position;
            foreach (var e in enemies)
            {
                if (!e.IsAlive) continue;
                float d = Vector3.Distance(myPos, e.transform.position);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        private void Fire()
        {
            if (bulletPrefab == null || currentTarget == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;
            Vector3 to = currentTarget.transform.position + Vector3.up * 1.0f;
            Vector3 dir = (to - from).normalized;
            Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Robot, bulletSpeed, damagePerShot);
        }

        private void UpdateAimVisual()
        {
            if (aimLine == null || currentTarget == null) return;
            if (!aimLine.enabled) aimLine.enabled = true;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.0f;
            Vector3 to = currentTarget.transform.position + Vector3.up * 1.0f;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, to);
        }

        private void Disable()
        {
            if (aimLine != null) aimLine.enabled = false;
            currentTarget = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectRange);
        }
    }
}
