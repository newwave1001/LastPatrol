using System;
using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Core.Input;

namespace LastPatrol.Characters
{
    // v11 HTML state.cop / updateCop 대응. 마렌의 메인 컨트롤러.
    // 점프 없음, 무기 없음 — 엄폐/충전/조사가 전부.
    [RequireComponent(typeof(CharacterMovement))]
    [RequireComponent(typeof(CoverSystem))]
    [RequireComponent(typeof(InteractionSystem))]
    public class MarenController : MonoBehaviour, IDamageable
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private M07.M07Controller robot;

        [Header("Stats")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float chargeRange = 6.0f;
        [SerializeField] private float chargeRatePerSecond = 25f;

        private CharacterMovement movement;
        private CoverSystem cover;
        private InteractionSystem interaction;

        private float currentHP;
        public float CurrentHP => currentHP;
        public bool IsAlive => currentHP > 0f;
        public bool IsCharging { get; private set; }

        public event Action OnDied;

        void Awake()
        {
            movement    = GetComponent<CharacterMovement>();
            cover       = GetComponent<CoverSystem>();
            interaction = GetComponent<InteractionSystem>();
            currentHP   = maxHP;
        }

        void OnEnable()
        {
            if (input != null) input.OnInteractPressed += HandleInteract;
        }

        void OnDisable()
        {
            if (input != null) input.OnInteractPressed -= HandleInteract;
        }

        void Update()
        {
            if (!IsAlive) return;
            if (input == null) return;

            // 엄폐 중에는 이동 잠금 (v11 규칙 — 엄폐 시 위치 고정).
            Vector2 move = cover.IsInCover ? Vector2.zero : input.MoveAxis;
            movement.Tick(move);
            cover.Tick(input.CoverHeld);
            UpdateCharging();
        }

        private void UpdateCharging()
        {
            IsCharging = false;
            if (robot == null || !input.ChargeHeld) return;

            float dist = Vector3.Distance(transform.position, robot.transform.position);
            if (dist > chargeRange) return;

            float delta = chargeRatePerSecond * Time.deltaTime;
            robot.AddBattery(delta);
            IsCharging = true;
        }

        private void HandleInteract()
        {
            if (!IsAlive) return;
            interaction.TryInteract();
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            // 엄폐 중 + 적 총알이면 차단 (2주차에 Bullet에서 Cover 통과 판정으로 옮길 수도).
            if (cover.IsInCover && source == DamageSource.Enemy) return;

            currentHP = Mathf.Max(0f, currentHP - amount);
            if (currentHP <= 0f) OnDeath();
        }

        private void OnDeath()
        {
            enabled = false;
            OnDied?.Invoke();
        }

    }
}
