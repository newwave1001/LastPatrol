using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Characters.Enemies;
using LastPatrol.Systems.Audio;
using LastPatrol.Systems.Encounter;

namespace LastPatrol.Systems.Combat
{
    public enum BulletSource { Robot, Enemy }

    // 비대칭 엄폐 규칙: M-07 총알은 Cover 관통, 적 총알은 Cover에 차단.
    // raycast 방식 — 빠른 총알이 콜라이더 통과해버리는 문제 회피.
    public class Bullet : MonoBehaviour
    {
        /// <summary>M-07 총알이 IDamageable 적에 명중 시 발생 — Hit Confirmation UI용.</summary>
        public static event System.Action<Vector3> OnRobotHitEvent;

        [Header("Stats (런타임에 spawner가 설정)")]
        public BulletSource source = BulletSource.Robot;
        public float speed = 30f;
        public float damage = 10f;
        public float maxLifetime = 3f;
        public LayerMask hitMask = ~0;

        private float age;

        public static Bullet Spawn(GameObject prefab, Vector3 position, Vector3 direction, BulletSource source, float speed, float damage)
        {
            var go = Instantiate(prefab, position, Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward));
            var b = go.GetComponent<Bullet>();
            b.source = source;
            b.speed = speed;
            b.damage = damage;
            // 사격 SFX — source별
            AudioManager.PlaySfx(source == BulletSource.Robot ? SfxKey.BulletFireRobot : SfxKey.BulletFireEnemy, position);
            return b;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= maxLifetime) { Destroy(gameObject); return; }

            float step = speed * Time.deltaTime;
            // 트리거(LootableRobot, dispatch zone, encounter zone 등)는 무시.
            // 트리거는 IDamageable/Cover 둘 다 없어서 무차별 Destroy 시 총알이 휴머노이드 도달 전 사라짐.
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (HandleHit(hit)) return;
            }
            transform.position += transform.forward * step;
        }

        // returns true → bullet destroyed/handled. false → 통과, 계속 이동.
        private bool HandleHit(RaycastHit hit)
        {
            var go = hit.collider.gameObject;

            var cover = go.GetComponentInParent<Cover>();
            if (cover != null)
            {
                if (source == BulletSource.Enemy)
                {
                    // 적 총알 → 엄폐 차단.
                    Debug.Log($"[Bullet] {source} bullet blocked by Cover ({go.name})");
                    Destroy(gameObject);
                    return true;
                }
                // M-07 총알 → 통과. 충돌 지점 너머로 이동시켜 다음 프레임에 재처리.
                transform.position = hit.point + transform.forward * 0.05f;
                return false;
            }

            var damageable = go.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                // 자기편이면 통과 — 데미지 X, 총알 계속 진행
                bool friendlyPassThrough = false;
                if (source == BulletSource.Robot && damageable is MarenController) friendlyPassThrough = true;
                else if (source == BulletSource.Robot && damageable is M07Controller) friendlyPassThrough = true;
                else if (source == BulletSource.Robot && damageable is LastPatrol.Systems.Vehicle.VehicleHealth vhRobot && !vhRobot.IsEnemy) friendlyPassThrough = true;
                else if (source == BulletSource.Enemy && damageable is EnemyAI) friendlyPassThrough = true;
                else if (source == BulletSource.Enemy && damageable is OutdoorHumanoid) friendlyPassThrough = true;
                else if (source == BulletSource.Enemy && damageable is Drone) friendlyPassThrough = true;
                else if (source == BulletSource.Enemy && damageable is LastPatrol.Systems.Vehicle.VehicleHealth vhEnemy && vhEnemy.IsEnemy) friendlyPassThrough = true;

                if (friendlyPassThrough)
                {
                    Debug.Log($"[Bullet] {source} bullet pass through friendly {damageable.GetType().Name} ({go.name})");
                    transform.position = hit.point + transform.forward * 0.05f;
                    return false; // 다음 프레임 계속 진행
                }

                DamageSource src = source == BulletSource.Robot ? DamageSource.Robot : DamageSource.Enemy;
                Debug.Log($"[Bullet] {source} bullet HIT {damageable.GetType().Name} ({go.name}) for {damage} damage");
                damageable.TakeDamage(damage, src);
                AudioManager.PlaySfx(SfxKey.BulletHit, hit.point);
                if (source == BulletSource.Robot) OnRobotHitEvent?.Invoke(hit.point);
            }
            else
            {
                // 의외의 충돌 — 건물·지면 등. 디버그용.
                Debug.Log($"[Bullet] {source} bullet absorbed by non-damageable ({go.name}, layer={LayerMask.LayerToName(go.layer)})");
                AudioManager.PlaySfx(SfxKey.BulletAbsorb, hit.point);
            }
            Destroy(gameObject);
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = source == BulletSource.Robot ? Color.cyan : Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
        }
    }
}
