using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Data;
using LastPatrol.Systems.Combat;

namespace LastPatrol.Characters.Enemies
{
    // 모든 적의 베이스. 단순 상태 머신: Idle → Aim → Fire → Cooldown.
    // 텔레그래프 (aimTime) 동안 시각/레이저로 표시 → 마렌이 엄폐할 시간 줌.
    public abstract class EnemyAI : MonoBehaviour, IDamageable
    {
        public enum State { Idle, Aim, Cooldown }

        [Header("Data")]
        [SerializeField] protected EnemyDataSO data;

        [Header("Refs")]
        [SerializeField] protected Transform target;        // 마렌
        [SerializeField] protected GameObject bulletPrefab;
        [SerializeField] protected Transform muzzle;        // 사격 위치 (없으면 자기 위치)

        [Header("Telegraph (그레이박스 시각)")]
        [SerializeField] protected LineRenderer aimLine;    // 조준선

        protected float currentHP;
        protected State state = State.Idle;
        protected float stateTimer;

        public bool IsAlive => currentHP > 0f;
        public State CurrentState => state;
        public float CurrentHP => currentHP;
        public float MaxHP => data != null ? data.maxHP : 0f;
        public EnemyDataSO Data => data;

        protected virtual void Awake()
        {
            currentHP = data != null ? data.maxHP : 30f;
            if (aimLine != null) aimLine.enabled = false;
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;
            if (target == null) { TryAcquireTarget(); return; }

            switch (state)
            {
                case State.Idle:     TickIdle(); break;
                case State.Aim:      TickAim(); break;
                case State.Cooldown: TickCooldown(); break;
            }
        }

        protected virtual void TickIdle()
        {
            if (HasLineOfSight())
            {
                EnterAim();
            }
        }

        protected virtual void TickAim()
        {
            stateTimer -= Time.deltaTime;
            UpdateAimVisual();
            if (stateTimer <= 0f)
            {
                Fire();
                EnterCooldown();
            }
        }

        protected virtual void TickCooldown()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) state = State.Idle;
        }

        protected virtual void EnterAim()
        {
            state = State.Aim;
            stateTimer = data.aimTimeSeconds;
            if (aimLine != null) aimLine.enabled = true;
        }

        protected virtual void EnterCooldown()
        {
            state = State.Cooldown;
            stateTimer = data.cooldownSeconds;
            if (aimLine != null) aimLine.enabled = false;
        }

        protected virtual void Fire()
        {
            if (bulletPrefab == null || target == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
            Vector3 dir = (target.position + Vector3.up * 1.0f - from).normalized;
            Bullet.Spawn(bulletPrefab, from, dir, BulletSource.Enemy, data.bulletSpeed, data.damageVsCop);
        }

        protected virtual bool HasLineOfSight()
        {
            if (target == null) return false;
            Vector3 from = transform.position + Vector3.up * 1.2f;
            Vector3 to = target.position + Vector3.up * 1.0f;
            float dist = Vector3.Distance(from, to);
            if (dist > data.losRange) return false;
            // 단순 거리 체크. 벽 차단은 raycast로 추후 추가 (그레이박스에선 생략).
            return true;
        }

        protected virtual void UpdateAimVisual()
        {
            if (aimLine == null || target == null) return;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
            Vector3 to = target.position + Vector3.up * 1.0f;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, to);
        }

        protected virtual void TryAcquireTarget()
        {
            // 씬에서 마렌 자동 검색 — 그레이박스 편의용.
            var maren = FindAnyObjectByType<MarenController>();
            if (maren != null) target = maren.transform;
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0f, currentHP - amount);
            if (currentHP <= 0f) OnDeath();
        }

        protected virtual void OnDeath()
        {
            if (aimLine != null) aimLine.enabled = false;

            // 그레이박스 시각: 회색 + 쓰러짐 (90도 회전).
            var renderers = GetComponentsInChildren<Renderer>();
            var mpb = new MaterialPropertyBlock();
            Color dead = new Color(0.35f, 0.35f, 0.35f);
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", dead);
                r.SetPropertyBlock(mpb);
            }
            transform.Rotate(0f, 0f, 90f, Space.Self);
            enabled = false;
        }

    }
}
