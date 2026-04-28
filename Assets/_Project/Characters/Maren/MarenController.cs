using System;
using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Battery;
using LastPatrol.Systems.Encounter;
using LastPatrol.Systems.Audio;

namespace LastPatrol.Characters
{
    // v11 HTML state.cop / updateCop 대응. 마렌의 메인 컨트롤러.
    // 점프 없음, 무기 없음 — 엄폐/충전/조사가 전부.
    [RequireComponent(typeof(CharacterMovement))]
    [RequireComponent(typeof(CoverSystem))]
    [RequireComponent(typeof(InteractionSystem))]
    public class MarenController : MonoBehaviour, IDamageable
    {
        public enum ControlMode { Manual, Cower, Flee }

        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private M07.M07Controller robot;

        [Header("Stats")]
        [SerializeField] private float maxHP = 100f;
        [Tooltip("M-07이 이 거리 안에 있어야 R 충전 가능.")]
        [SerializeField] private float chargeRange = 6.0f;
        [Tooltip("R 1회 누름당 M-07 충전 양 (마렌의 배터리 1개 = 30%).")]
        [SerializeField] private float chargePerBattery = 30f;

        [Header("Cower (피신)")]
        [Tooltip("M-07 근처 이 거리 안이면 멈춤.")]
        [SerializeField] private float cowerStopRadius = 1.8f;
        [Tooltip("이 거리 이상 떨어져 있으면 catch-up 속도로 이동.")]
        [SerializeField] private float cowerCatchupRadius = 6f;
        [Tooltip("M-07 근처 도달 후 가까운 엄폐물 자동 진입.")]
        [SerializeField] private bool autoCoverWhenCowering = true;

        [Header("Flee (도주 — AMBUSH 전투)")]
        [Tooltip("이 거리 안 적이 있으면 도주 시작.")]
        [SerializeField] private float fleeDetectRadius = 36f;
        [Tooltip("적과 이 거리 이상 떨어지면 정지.")]
        [SerializeField] private float fleeSafeDistance = 22f;

        private CharacterMovement movement;
        private CoverSystem cover;
        private InteractionSystem interaction;

        private float currentHP;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public bool IsAlive => currentHP > 0f;
        // 배터리 1회 누름 시 0.2초 동안 true (UI 깜박이용).
        private float _chargeFlashUntil;
        public bool IsCharging => Time.time < _chargeFlashUntil;

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

            // 외부 씬에서 prefab 인스턴스로 들어왔을 때 참조가 비어있을 수 있음 → 자동 검색.
            // M-07 GO가 차에 탑승 중 비활성일 수 있어 FindObjectsInactive.Include 필수.
            if (input == null) input = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);
            if (robot == null) robot = FindAnyObjectByType<M07.M07Controller>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            if (input != null)
            {
                input.OnInteractPressed += HandleInteract;
                input.OnJumpPressed += HandleJump;
                input.OnChargePressed += HandleChargePress;
            }
        }

        void OnDisable()
        {
            if (input != null)
            {
                input.OnInteractPressed -= HandleInteract;
                input.OnJumpPressed -= HandleJump;
                input.OnChargePressed -= HandleChargePress;
            }
        }

        private void HandleJump()
        {
            if (!IsAlive) return;
            if (CurrentMode != ControlMode.Manual) return; // Cower 중엔 점프 X
            if (cover != null && cover.IsInCover) return;  // 엄폐 중 점프 X
            movement.TryJump();
        }

        void Update()
        {
            if (!IsAlive) return;
            if (input == null) return;

            // 전투 종료 시 Flee 자동 해제 → Manual 복귀
            if (CurrentMode == ControlMode.Flee && !LastPatrol.Systems.World.CombatStatus.InCombat)
                SetMode(ControlMode.Manual);

            if (CurrentMode == ControlMode.Manual)
            {
                // 엄폐 중에는 이동 잠금 (v11 규칙).
                Vector2 move = cover.IsInCover ? Vector2.zero : input.MoveAxis;
                movement.Tick(move);
                cover.Tick(input.CoverHeld);
                // 충전은 OnChargePressed 이벤트 핸들러에서 1회 누름 = 배터리 1개 소모.
            }
            else if (CurrentMode == ControlMode.Cower)
            {
                Vector2 autoMove = ComputeCowerAxis();
                bool reachedRobot = autoMove.sqrMagnitude < 1e-4f;

                // M-07 근처 도달 후에만 엄폐 시도 — 이동 중엔 엄폐 진입 막아서 멈추는 현상 방지.
                if (reachedRobot && autoCoverWhenCowering)
                {
                    movement.Tick(Vector2.zero);
                    cover.Tick(true);
                }
                else
                {
                    // 이동 중이거나 자동 엄폐 비활성 → 단순 이동, 엄폐 해제 유지
                    movement.Tick(autoMove);
                    cover.Tick(false);
                }

                // Cower 동안엔 InputReader Charge/Interact 무시 (M-07이 활성).
            }
            else // Flee — 적에게서 도주, safeDistance 도달 시 정지
            {
                Vector2 fleeAxis = ComputeFleeAxis();
                movement.Tick(fleeAxis);
                cover.Tick(fleeAxis.sqrMagnitude < 1e-4f); // 정지 시 엄폐 시도
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

        /// <summary>가장 가까운 적(휴머노이드/드론)에서 멀어지는 axis. safeDistance 도달 시 정지.</summary>
        private Vector2 ComputeFleeAxis()
        {
            Transform closest = null;
            float closestDist = float.MaxValue;
            Vector3 selfPos = transform.position;

            var humanoids = FindObjectsByType<OutdoorHumanoid>(FindObjectsInactive.Exclude);
            for (int i = 0; i < humanoids.Length; i++)
            {
                var h = humanoids[i];
                if (h == null || !h.IsEnemy || !h.IsAlive) continue;
                float d = Vector3.Distance(selfPos, h.transform.position);
                if (d < closestDist) { closestDist = d; closest = h.transform; }
            }
            var drones = FindObjectsByType<Drone>(FindObjectsInactive.Exclude);
            for (int i = 0; i < drones.Length; i++)
            {
                var d2 = drones[i];
                if (d2 == null || !d2.IsEnemy || !d2.IsAlive) continue;
                float d = Vector3.Distance(selfPos, d2.transform.position);
                if (d < closestDist) { closestDist = d; closest = d2.transform; }
            }

            // 위협 없거나 안전 거리 도달 → 정지
            if (closest == null || closestDist >= fleeSafeDistance || closestDist > fleeDetectRadius)
                return Vector2.zero;

            Vector3 awayDir = (selfPos - closest.position);
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 1e-4f) return Vector2.zero;
            awayDir.Normalize();
            return new Vector2(awayDir.x, awayDir.z);
        }

        // R 1회 누름 = 마렌 배터리 1개 소모 → M-07 +chargePerBattery%.
        // M-07 chargeRange 안 + 배터리 보유 + 살아있을 때만. Mode(Manual/Cower/Flee) 무관.
        private void HandleChargePress()
        {
            if (!IsAlive) { Debug.Log("[Maren.Charge] Maren dead — skip"); return; }
            // M-07 ref 늦게 해결 — Awake 시점에 비활성이었던 케이스
            if (robot == null) robot = FindAnyObjectByType<M07.M07Controller>(FindObjectsInactive.Include);
            if (robot == null) { Debug.Log("[Maren.Charge] M07 못 찾음 — skip"); return; }

            float dist = Vector3.Distance(transform.position, robot.transform.position);
            if (dist > chargeRange)
            {
                Debug.Log($"[Maren.Charge] M07 너무 멀음 dist={dist:F1}m > range={chargeRange}m (mode={CurrentMode})");
                return;
            }

            if (!BatteryInventory.TryConsume(1))
            {
                Debug.Log($"[Maren.Charge] 배터리 없음 ({BatteryInventory.Count}/{BatteryInventory.Max})");
                return;
            }
            robot.AddBattery(chargePerBattery);
            _chargeFlashUntil = Time.time + 0.2f;
            AudioManager.PlaySfx(SfxKey.BatteryCharge, transform.position);
            Debug.Log($"[Maren.Charge] OK +{chargePerBattery}% → M07 {robot.CurrentBattery:F0}/{robot.MaxBattery:F0} (mode={CurrentMode})");
        }

        private void HandleInteract()
        {
            if (!IsAlive) return;
            if (CurrentMode != ControlMode.Manual) return;
            if (interaction == null) return;
            interaction.TryInteract();
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            bool inCover = cover != null && cover.IsInCover;
            if (inCover && source == DamageSource.Enemy)
            {
                Debug.Log($"[Maren] TakeDamage {amount} blocked by cover (src={source})");
                return;
            }

            currentHP = Mathf.Max(0f, currentHP - amount);
            AudioManager.PlaySfx(SfxKey.MarenHit, transform.position);
            Debug.Log($"[Maren] TakeDamage {amount} from {source} → HP {currentHP}/{maxHP}");
            if (currentHP <= 0f) OnDeath();
        }

        private void OnDeath()
        {
            enabled = false;
            OnDied?.Invoke();
        }

    }
}
