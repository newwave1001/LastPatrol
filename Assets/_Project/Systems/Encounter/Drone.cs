using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Systems.Battery;
using LastPatrol.Systems.Combat;

namespace LastPatrol.Systems.Encounter
{
    /// <summary>
    /// 드론 어택 인카운터의 비행 적. M-07 배터리 0 + 30초 grace 만료 시 3마리 스폰.
    /// 차보다 빠른 속도, 따돌릴 수 없음. 차에서 내려서 M-07이 격추해야 함.
    ///
    /// AI:
    ///   1) 타겟 향해 비행 (Approach)
    ///   2) 사거리 안 → 궤도 비행 (Orbit) + 주기적 사격
    ///   3) HP 0 → 추락 → LootableRobot 스폰 (배터리 1개)
    ///
    /// IDamageable + isEnemy=true → M-07 TurretController가 자동 사격 대상.
    /// </summary>
    [DisallowMultipleComponent]
    public class Drone : MonoBehaviour, IDamageable
    {
        [Header("Faction")]
        [Tooltip("M-07 자동 사격이 적으로 인식.")]
        [SerializeField] private bool isEnemy = true;
        public bool IsEnemy => isEnemy;

        [Header("Stats")]
        [SerializeField] private float maxHP = 40f;
        [Tooltip("차(maxSpeed=18)보다 빠르게 — 따돌릴 수 없게.")]
        [SerializeField] private float maxSpeed = 25f;
        [SerializeField] private float acceleration = 30f;

        [Header("Flight")]
        [SerializeField] private float flightAltitude = 6f;
        [SerializeField] private float altitudeAdjustSpeed = 4f;
        [Tooltip("타겟 주변 궤도 반경(m).")]
        [SerializeField] private float orbitRadius = 10f;

        [Header("Attack")]
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float fireInterval = 1.2f;
        [Tooltip("사거리 안에서만 사격.")]
        [SerializeField] private float attackRange = 14f;
        [Tooltip("발사할 총알 prefab. 어떤 prefab이든 드래그 가능 (Bullet 컴포넌트 자동 검출). " +
                 "비워두면 visible projectile 없이 직접 데미지 (fallback).")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 28f;
        [Tooltip("총알 발사 위치. 비워두면 transform 위치.")]
        [SerializeField] private Transform muzzle;

        [Header("Separation (드론 끼리 안 겹치게)")]
        [SerializeField] private float separationRange = 5f;
        [SerializeField] private float separationStrength = 14f;

        [Header("Loot Drop")]
        [SerializeField] private LootableRobot lootPrefab;
        [SerializeField] private int batteriesPerKill = 1;

        [Header("Crash")]
        [SerializeField] private float crashDuration = 1.0f;

        [Header("Flee (스텔스 회피 시)")]
        [Tooltip("도주 모드 진입 후 destroy까지 시간(초). 그 사이 멀리 날아감.")]
        [SerializeField] private float fleeDuration = 4f;
        [SerializeField] private float fleeSpeedMultiplier = 0.7f;

        [Header("Visual (auto)")]
        [SerializeField] private bool autoBuildVisual = true;

        private Transform _target;
        private float _currentHP;
        private float _lastFireTime = -999f;
        private bool _crashed;
        private bool _fleeing;
        private float _fleeEndTime;
        private Vector3 _velocity;

        // 활성 드론 정적 리스트 — separation 계산용 (FindObjectsByType 매 프레임 회피).
        private static readonly List<Drone> _allDrones = new List<Drone>();

        public bool IsAlive => _currentHP > 0f && !_crashed;
        public Transform Target { get => _target; set => _target = value; }

        public void SetTarget(Transform t) => _target = t;

        void Awake()
        {
            _currentHP = maxHP;
            if (autoBuildVisual && transform.childCount == 0) BuildVisual();
            EnsureCollider();
        }

        void OnEnable() { if (!_allDrones.Contains(this)) _allDrones.Add(this); }
        void OnDisable() { _allDrones.Remove(this); }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            if (_currentHP <= 0f) StartCrash();
        }

        /// <summary>
        /// DroneEncounterDirector가 호출. 차 정지+라이트 OFF 5초 만족 시 드론 도주 모드.
        /// fleeDuration 후 자동 destroy. 추격/사격 정지.
        /// </summary>
        public void BeginFleeing()
        {
            if (_fleeing || _crashed) return;
            _fleeing = true;
            _fleeEndTime = Time.time + fleeDuration;
        }

        void Update()
        {
            if (_crashed) return;

            // 도주 모드 — 타겟에서 멀어지는 방향으로 비행, 일정 시간 후 자체 destroy
            if (_fleeing)
            {
                if (Time.time >= _fleeEndTime || _target == null)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 awayDir = (transform.position - _target.position);
                awayDir.y = 0f;
                if (awayDir.sqrMagnitude < 1e-3f) awayDir = transform.forward;
                else awayDir.Normalize();

                _velocity = Vector3.MoveTowards(_velocity, awayDir * (maxSpeed * fleeSpeedMultiplier), acceleration * Time.deltaTime);
                Vector3 fleePos = transform.position;
                fleePos.y = Mathf.Lerp(fleePos.y, flightAltitude + 4f, altitudeAdjustSpeed * Time.deltaTime); // 떠나면서 높이 상승
                transform.position = fleePos + new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;
                if (awayDir.sqrMagnitude > 1e-3f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(awayDir), 4f * Time.deltaTime);
                return;
            }

            if (_target == null) return;

            Vector3 toTarget = _target.position - transform.position;
            Vector3 horiz = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizDist = horiz.magnitude;
            Vector3 horizDir = horizDist > 1e-3f ? horiz / horizDist : Vector3.forward;

            // 고도 유지
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, flightAltitude, altitudeAdjustSpeed * Time.deltaTime);
            transform.position = pos;

            // Approach vs Orbit
            Vector3 desiredVel;
            if (horizDist > orbitRadius + 2f)
            {
                // 접근 — 직진
                desiredVel = horizDir * maxSpeed;
            }
            else
            {
                // 궤도 비행 + 사격
                Vector3 perp = Vector3.Cross(Vector3.up, horizDir);
                desiredVel = perp * (maxSpeed * 0.7f);
                // 너무 가까우면 멀어지는 성분 추가
                if (horizDist < orbitRadius - 1f)
                    desiredVel += -horizDir * (orbitRadius - horizDist) * 1.5f;
                if (horizDist <= attackRange) TryFire();
            }

            // 드론 끼리 분리 — desiredVel에 separation 합산
            desiredVel += ComputeSeparation();

            _velocity = Vector3.MoveTowards(_velocity, desiredVel, acceleration * Time.deltaTime);

            // 수평만 — y는 위에서 별도 처리
            transform.position += new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;

            // 타겟 바라봄
            Vector3 lookDir = horizDir;
            if (lookDir.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 8f * Time.deltaTime);
        }

        private void TryFire()
        {
            if (Time.time - _lastFireTime < fireInterval) return;
            if (_target == null) return;

            Vector3 from = muzzle != null ? muzzle.position : transform.position;
            // 약간 앞으로 미세 offset — 자기 콜라이더 안에서 발사돼 즉시 자가 피격되는 거 방지
            from += transform.forward * 0.6f;
            Vector3 to = _target.position + Vector3.up * 1.0f;
            Vector3 dir = (to - from);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            // Bullet 컴포넌트 있으면 visible projectile, 아니면 fallback 직접 데미지
            bool usedBullet = false;
            if (bulletPrefab != null && bulletPrefab.GetComponent<Bullet>() != null)
            {
                Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Enemy, bulletSpeed, attackDamage);
                usedBullet = true;
                Debug.Log($"[Drone] fire bullet from={from} dir={dir} → target={_target.name}");
            }
            if (!usedBullet)
            {
                var dmg = _target.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    dmg.TakeDamage(attackDamage, DamageSource.Enemy);
            }
            _lastFireTime = Time.time;
        }

        /// <summary>다른 드론들과 너무 가까우면 멀어지는 desired velocity 합산.</summary>
        private Vector3 ComputeSeparation()
        {
            Vector3 sep = Vector3.zero;
            for (int i = 0; i < _allDrones.Count; i++)
            {
                var other = _allDrones[i];
                if (other == this || other == null || other._crashed) continue;
                Vector3 toMe = transform.position - other.transform.position;
                toMe.y = 0f;
                float d = toMe.magnitude;
                if (d > 0.01f && d < separationRange)
                {
                    float strength = (separationRange - d) / separationRange;
                    sep += (toMe / d) * strength;
                }
            }
            return sep * separationStrength;
        }

        private void StartCrash()
        {
            _crashed = true;
            StartCoroutine(CrashSequence());
        }

        private IEnumerator CrashSequence()
        {
            Vector3 start = transform.position;
            Vector3 ground = new Vector3(start.x, 0.05f, start.z);
            float t = 0f;
            // 회전 + 추락
            Quaternion startRot = transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(45f, 0f, 30f);
            while (t < crashDuration)
            {
                float a = t / crashDuration;
                transform.position = Vector3.Lerp(start, ground, a * a); // 가속 추락
                transform.rotation = Quaternion.Slerp(startRot, endRot, a);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = ground;

            // LootableRobot 생성
            SpawnLoot(ground);
            Destroy(gameObject);
        }

        private void SpawnLoot(Vector3 pos)
        {
            pos.y = 0.05f;
            LootableRobot loot;
            if (lootPrefab != null)
            {
                loot = Instantiate(lootPrefab, pos, Quaternion.identity);
            }
            else
            {
                var go = new GameObject($"DroneWreck_{name}");
                go.transform.position = pos;
                loot = go.AddComponent<LootableRobot>();
            }
            // 격파 보상 — 알약 배터리 모양
            loot.SetVisualStyle(LootableRobot.VisualStyle.BatteryPill);
            loot.SetBatteryCount(batteriesPerKill);
        }

        // ---- Auto build ----

        private void BuildVisual()
        {
            // Body — 어두운 큐브
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(1f, 0.3f, 1f);
            DestroyChildCollider(body);
            ApplyTint(body.GetComponent<Renderer>(), new Color(0.25f, 0.25f, 0.28f));

            // 4개 로터 (X자 배치)
            for (int i = 0; i < 4; i++)
            {
                var rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rotor.name = $"Rotor_{i}";
                rotor.transform.SetParent(transform, false);
                float x = (i % 2 == 0) ? 0.55f : -0.55f;
                float z = (i / 2 == 0) ? 0.55f : -0.55f;
                rotor.transform.localPosition = new Vector3(x, 0.18f, z);
                rotor.transform.localScale = new Vector3(0.55f, 0.04f, 0.04f);
                rotor.transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 45f : -45f), 0f);
                DestroyChildCollider(rotor);
                ApplyTint(rotor.GetComponent<Renderer>(), new Color(0.40f, 0.40f, 0.42f));
            }

            // 빨간 발광 (적 식별)
            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Eye";
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
            eye.transform.localScale = new Vector3(0.15f, 0.08f, 0.05f);
            DestroyChildCollider(eye);
            ApplyTint(eye.GetComponent<Renderer>(), new Color(0.85f, 0.20f, 0.18f));
        }

        private void EnsureCollider()
        {
            if (GetComponent<Collider>() != null) return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.3f, 0.6f, 1.3f);
            box.center = Vector3.zero;
            // trigger 아님 — bullets/raycast가 hit해야 함
        }

        private static void DestroyChildCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

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
            Gizmos.color = new Color(0.85f, 0.20f, 0.18f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, orbitRadius);
        }
#endif
    }
}
