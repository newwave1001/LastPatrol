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
        public enum ControlMode { Manual, Cower }

        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private M07.M07Controller robot;

        [Header("Stats")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float chargeRange = 6.0f;
        [SerializeField] private float chargeRatePerSecond = 25f;

        [Header("Cower (피신)")]
        [Tooltip("M-07 근처 이 거리 안이면 멈춤.")]
        [SerializeField] private float cowerStopRadius = 1.8f;
        [Tooltip("이 거리 이상 떨어져 있으면 catch-up 속도로 이동.")]
        [SerializeField] private float cowerCatchupRadius = 6f;
        [Tooltip("M-07 활성 시 마렌 자동 엄폐 시도 (가까운 엄폐물 자동 진입). 1차는 false.")]
        [SerializeField] private bool autoCoverWhenCowering = false;

        private CharacterMovement movement;
        private CoverSystem cover;
        private InteractionSystem interaction;

        private float currentHP;
        public float CurrentHP => currentHP;
        public bool IsAlive => currentHP > 0f;
        public bool IsCharging { get; private set; }

        public ControlMode CurrentMode { get; private set; } = ControlMode.Manual;

        public event Action OnDied;
        public event Action<ControlMode> OnModeChanged;

        public void SetMode(ControlMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

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

            if (CurrentMode == ControlMode.Manual)
            {
                // 엄폐 중에는 이동 잠금 (v11 규칙).
                Vector2 move = cover.IsInCover ? Vector2.zero : input.MoveAxis;
                movement.Tick(move);
                cover.Tick(input.CoverHeld);
                UpdateCharging();
            }
            else // Cower
            {
                Vector2 autoMove = ComputeCowerAxis();
                movement.Tick(autoMove);
                // Cower 동안엔 InputReader Charge/Interact 무시 (M-07이 활성).
                IsCharging = false;
                cover.Tick(autoCoverWhenCowering); // 자동 엄폐 옵션
            }
        }

        /// <summary>M-07 쪽으로 가는 입력 axis. CharacterMovement.Tick(Vector2)에 그대로 전달.</summary>
        private Vector2 ComputeCowerAxis()
        {
            if (robot == null) return Vector2.zero;
            Vector3 toRobot = robot.transform.position - transform.position;
            toRobot.y = 0f;
            float dist = toRobot.magnitude;
            if (dist <= cowerStopRadius) return Vector2.zero;
            // 멀수록 풀스피드, cowerStopRadius 근처에서 점진적 감속
            float t = Mathf.InverseLerp(cowerStopRadius, cowerCatchupRadius, dist);
            float magnitude = Mathf.Clamp01(t);
            Vector3 dir = toRobot / Mathf.Max(0.001f, dist);
            return new Vector2(dir.x, dir.z) * magnitude;
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
