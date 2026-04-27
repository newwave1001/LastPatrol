using System;
using UnityEngine;
using LastPatrol.Core;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 차량 HP 컴포넌트. CarController가 붙은 GameObject(또는 EnemyVehicle 등)에 부착.
    /// 바이블 §10.2: "차 HP = 마렌 생명". 차 HP가 0 되면 OnDeath 발화 → 게임 오버 흐름.
    ///
    /// IDamageable 구현으로 EnemyVehicle 박치기 / 총알 / 환경 위험이 동일 인터페이스로 데미지.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleHealth : MonoBehaviour, IDamageable
    {
        [Header("Faction")]
        [Tooltip("M-07 자동 사격이 이 차를 적으로 볼지. 마렌 차/주차 차 false, 적 차량은 true.")]
        [SerializeField] private bool isEnemy = false;
        public bool IsEnemy => isEnemy;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startHealthOverride = -1f; // -1 이면 maxHealth로 시작

        [Header("Defense")]
        [Tooltip("받는 모든 데미지에 곱하는 계수. 1=원래대로, 0.5=절반, 0=무적.")]
        [SerializeField, Range(0f, 2f)] private float damageMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] private bool logDamage = false;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float Normalized => CurrentHealth / Mathf.Max(1f, maxHealth);
        public bool IsAlive => CurrentHealth > 0f;
        /// <summary>한 번 0 도달하면 영구 true. Heal 해도 안 풀림(파괴된 차).</summary>
        public bool IsDestroyed { get; private set; }

        public event Action<float, DamageSource> OnDamaged; // amount, source
        public event Action<float> OnHealed;
        public event Action OnDeath;

        void Awake()
        {
            CurrentHealth = startHealthOverride > 0f ? Mathf.Min(startHealthOverride, maxHealth) : maxHealth;
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive || amount <= 0f) return;
            float effective = amount * damageMultiplier;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - effective);
            if (logDamage) Debug.Log($"[Health] {name} -{effective:F1} from {source} → {CurrentHealth:F1}/{maxHealth}", this);
            OnDamaged?.Invoke(effective, source);
            if (CurrentHealth <= 0f)
            {
                IsDestroyed = true;
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealed?.Invoke(amount);
        }

        public void SetMaxHealth(float value, bool refill = false)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill) CurrentHealth = maxHealth;
            else CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        }

        [ContextMenu("Debug Damage 25")]
        private void DebugDamage25() => TakeDamage(25f, DamageSource.Environment);

        [ContextMenu("Debug Kill")]
        private void DebugKill() => TakeDamage(99999f, DamageSource.Environment);
    }
}
