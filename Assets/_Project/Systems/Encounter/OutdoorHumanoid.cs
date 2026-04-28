using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Core;
using LastPatrol.Systems.Battery;
using LastPatrol.Systems.Combat;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.Encounter
{
    /// <summary>
    /// 외부 휴머노이드 적. 차량 ambush 시 EnemyVehicle이 2명 spawn.
    /// 정지 상태로 타겟(마렌/차)을 향해 회전 + 주기 사격. M-07이 IDamageable 자동 사격 대상.
    ///
    /// 격추 시 LootableRobot 1개 드랍 (드론과 동일 보상 패턴).
    /// </summary>
    [DisallowMultipleComponent]
    public class OutdoorHumanoid : MonoBehaviour, IDamageable
    {
        [Header("Faction")]
        [SerializeField] private bool isEnemy = true;
        public bool IsEnemy => isEnemy;

        [Header("Stats")]
        [SerializeField] private float maxHP = 50f;

        [Header("Attack")]
        [SerializeField] private float fireInterval = 1.5f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float bulletSpeed = 25f;
        [SerializeField] private float damage = 8f;
        [Tooltip("Bullet 컴포넌트 부착된 prefab.")]
        [SerializeField] private GameObject bulletPrefab;
        [Tooltip("총알 발사 위치. null이면 transform 위치 기준.")]
        [SerializeField] private Transform muzzle;

        [Header("Loot Drop")]
        [SerializeField] private LootableRobot lootPrefab;
        [SerializeField] private int batteriesPerKill = 1;

        [Header("Visual (auto)")]
        [SerializeField] private bool autoBuildVisual = true;

        private Transform _target;
        private float _currentHP;
        private float _lastFireTime = -999f;

        public bool IsAlive => _currentHP > 0f;
        public Transform Target { get => _target; set => _target = value; }

        public void SetTarget(Transform t) => _target = t;

        /// <summary>외부 spawn 시 bulletPrefab 누락 보정. 이미 있으면 skip.</summary>
        public void EnsureBulletPrefab(GameObject fallback)
        {
            if (bulletPrefab == null && fallback != null) bulletPrefab = fallback;
        }

        void Awake()
        {
            _currentHP = maxHP;
            if (autoBuildVisual && transform.childCount == 0) BuildVisual();
            EnsureCollider();
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            Debug.Log($"[Humanoid] {name} TakeDamage {amount} from {source} → HP {_currentHP}/{maxHP}");
            if (_currentHP <= 0f) Die();
        }

        void Update()
        {
            if (!IsAlive) return;

            // 매 프레임 타겟 갱신 — 마렌이 활성이면 우선, 아니면 차 (마렌 차에 탑승)
            ResolveActiveTarget();
            if (_target == null) return;

            // 타겟 바라봄 (수평만)
            Vector3 toT = _target.position - transform.position;
            toT.y = 0f;
            float dist = toT.magnitude;
            if (dist > 1e-3f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toT.normalized), 5f * Time.deltaTime);

            // 사격
            if (dist <= attackRange && Time.time - _lastFireTime >= fireInterval)
            {
                Fire();
                _lastFireTime = Time.time;
            }
        }

        // 매 프레임 타겟 갱신. 가장 가까운 적(마렌/M-07/차) 으로 잡음.
        // 마렌 도주(Flee)로 멀어졌어도 M-07이 근처에 있으면 그쪽 사격.
        private void ResolveActiveTarget()
        {
            Vector3 self = transform.position;
            float bestDist = float.MaxValue;
            Transform bestTarget = null;

            var maren = FindAnyObjectByType<LastPatrol.Characters.MarenController>(FindObjectsInactive.Include);
            if (maren != null && maren.gameObject.activeInHierarchy && maren.IsAlive)
            {
                float d = Vector3.Distance(self, maren.transform.position);
                if (d < bestDist) { bestDist = d; bestTarget = maren.transform; }
            }

            var m07 = FindAnyObjectByType<LastPatrol.Characters.M07.M07Controller>(FindObjectsInactive.Include);
            if (m07 != null && m07.gameObject.activeInHierarchy && m07.IsAlive)
            {
                float d = Vector3.Distance(self, m07.transform.position);
                if (d < bestDist) { bestDist = d; bestTarget = m07.transform; }
            }

            // 도보 캐릭터 둘 다 멀거나 비활성이면 차 타겟 (탑승 중 추정)
            if (bestTarget == null)
            {
                var car = FindAnyObjectByType<LastPatrol.Systems.Vehicle.CarController>(FindObjectsInactive.Include);
                if (car != null) bestTarget = car.transform;
            }

            _target = bestTarget;
        }

        private void Fire()
        {
            if (_target == null) { Debug.Log("[Humanoid] fire skip — target null"); return; }
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.5f + transform.forward * 0.4f;
            Vector3 to = _target.position + Vector3.up * 0.8f;
            Vector3 dir = (to - from);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();

            if (bulletPrefab != null && bulletPrefab.GetComponent<Bullet>() != null)
            {
                Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Enemy, bulletSpeed, damage);
                Debug.Log($"[Humanoid] fire bullet → {_target.name}");
            }
            else
            {
                var dmg = _target.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive) dmg.TakeDamage(damage, DamageSource.Enemy);
                Debug.Log($"[Humanoid] direct damage {damage} → {_target.name} (no bulletPrefab)");
            }
        }

        private void Die()
        {
            Debug.Log($"[Humanoid] {name} Die() — drop pill + destroy");
            DropBatteryPill();
            Destroy(gameObject);
        }

        private void DropBatteryPill()
        {
            if (batteriesPerKill <= 0) return;
            Vector3 pos = transform.position;
            pos.y = 0.05f;
            var go = new GameObject($"BatteryDrop_{name}");
            go.transform.position = pos;
            var loot = go.AddComponent<LootableRobot>();
            loot.SetVisualStyle(LootableRobot.VisualStyle.BatteryPill);
            loot.SetBatteryCount(batteriesPerKill);
        }

        // ---- Auto build (placeholder visual) ----

        private void BuildVisual()
        {
            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 1.6f, 0.5f);
            DestroyChildCollider(body);
            ApplyTint(body.GetComponent<Renderer>(), new Color(0.30f, 0.32f, 0.30f)); // dark olive

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            DestroyChildCollider(head);
            ApplyTint(head.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.40f)); // skin

            // Gun
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Gun";
            gun.transform.SetParent(transform, false);
            gun.transform.localPosition = new Vector3(0.25f, 1.2f, 0.45f);
            gun.transform.localScale = new Vector3(0.1f, 0.1f, 0.7f);
            DestroyChildCollider(gun);
            ApplyTint(gun.GetComponent<Renderer>(), new Color(0.20f, 0.20f, 0.22f));
        }

        private void EnsureCollider()
        {
            // 기존 콜라이더 정리 — prefab에 trigger collider가 박혀있으면
            // Bullet의 QueryTriggerInteraction.Ignore raycast가 통과해버려 데미지 X.
            var existing = GetComponent<Collider>();
            if (existing != null)
            {
                if (existing.isTrigger)
                {
                    Debug.Log($"[Humanoid] {name} parent collider was trigger — disabling trigger");
                    existing.isTrigger = false;
                }
            }
            else
            {
                var c = gameObject.AddComponent<CapsuleCollider>();
                c.height = 2.0f;
                c.radius = 0.4f;
                c.center = new Vector3(0f, 1.0f, 0f);
            }

            // 자식 메시 콜라이더도 trigger면 off (raycast 통과 방지).
            var childCols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < childCols.Length; i++)
            {
                var cc = childCols[i];
                if (cc == null || cc.gameObject == gameObject) continue;
                if (cc.isTrigger)
                {
                    Debug.Log($"[Humanoid] {name} child collider {cc.name} was trigger — disabling");
                    cc.isTrigger = false;
                }
            }

            var pc = GetComponent<Collider>();
            Debug.Log($"[Humanoid] {name} colliders ready: parent={(pc != null ? pc.GetType().Name : "null")} trigger={(pc != null ? pc.isTrigger.ToString() : "n/a")}, childCount={transform.childCount}");
        }

        private static void DestroyChildCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static void ApplyTint(Renderer r, Color c)
        {
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.20f, 0.18f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
