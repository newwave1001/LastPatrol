using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Characters.Enemies;

namespace LastPatrol.Systems.Combat
{
    public enum BulletSource { Robot, Enemy }

    // 비대칭 엄폐 규칙: M-07 총알은 Cover 관통, 적 총알은 Cover에 차단.
    // raycast 방식 — 빠른 총알이 콜라이더 통과해버리는 문제 회피.
    public class Bullet : MonoBehaviour
    {
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
            return b;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= maxLifetime) { Destroy(gameObject); return; }

            float step = speed * Time.deltaTime;
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step, hitMask, QueryTriggerInteraction.Collide))
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
                // 자기편 사격 무시 (M-07 → M-07/마렌, 적 → 적).
                if (source == BulletSource.Robot && damageable is MarenController) { /* skip */ }
                else if (source == BulletSource.Robot && damageable is M07Controller) { /* skip */ }
                else if (source == BulletSource.Enemy && damageable is EnemyAI) { /* skip */ }
                else
                {
                    DamageSource src = source == BulletSource.Robot ? DamageSource.Robot : DamageSource.Enemy;
                    damageable.TakeDamage(damage, src);
                }
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
