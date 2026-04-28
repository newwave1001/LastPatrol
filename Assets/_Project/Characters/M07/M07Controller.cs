using System;
using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Core.Input;

namespace LastPatrol.Characters.M07
{
    // v11 HTML state.robot / updateRobot 대응.
    // 1주차: 따라가기 + 배터리 관리만. 자동 사격(TurretController)은 2주차 추가.
    [RequireComponent(typeof(FollowBehavior))]
    public class M07Controller : MonoBehaviour, IDamageable
    {
        public enum ControlMode { Follow, Manual }

        [Header("References")]
        [SerializeField] private InputReader input;

        [Header("Battery")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float idleDrainPerSecond = 0.5f;

        [Header("Health")]
        [SerializeField] private float maxHP = 200f;

        [Header("Visuals")]
        [SerializeField] private RobotEyes eyes;

        private FollowBehavior follow;
        private float currentBattery;
        private float currentHP;
        private bool isHacked;
        private bool _stateInitialized;

        public ControlMode CurrentMode { get; private set; } = ControlMode.Follow;
        public event Action<ControlMode> OnModeChanged;

        public void SetMode(ControlMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

        // Hold 모드 — 자리 사수. AMBUSH 전투 시 마렌이 도주하는 동안 M-07이 따라가지 않고 그 자리 방어.
        public bool IsHolding { get; private set; }
        public void SetHold(bool value) { IsHolding = value; }

        public float CurrentBattery => currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryPercent => maxBattery > 0f ? currentBattery / maxBattery : 0f;
        public bool IsAlive => currentHP > 0f;
        public bool IsHacked => isHacked;

        void Awake()
        {
            follow = GetComponent<FollowBehavior>();
            EnsureInitialized();
            if (input == null) input = FindAnyObjectByType<InputReader>();
        }

        /// <summary>
        /// 외부에서 강제 초기화. M-07이 차 안에서 시작(GameObject inactive) → Awake 안 돌면
        /// currentBattery=0으로 남는 버그 방지. TrackingDirector 등이 Awake/Start에서 호출.
        ///
        /// M07State (정적 영속) 가 있으면 거기서 복원 → 씬 전환 시 배터리/HP 유지.
        /// 없으면 maxBattery/maxHP로 초기화하고 즉시 Save.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_stateInitialized) return;
            if (M07State.HasState)
            {
                currentBattery = M07State.Battery;
                currentHP = M07State.HP;
                isHacked = M07State.IsHacked;
            }
            else
            {
                currentBattery = maxBattery;
                currentHP = maxHP;
                isHacked = false;
            }
            M07State.Save(currentBattery, currentHP, isHacked);
            _stateInitialized = true;
        }

        void Update()
        {
            if (!IsAlive) return;

            // 전투 종료 시 Hold 자동 해제 + Follow 복귀
            // (PartyController가 Tab 전환 시 Manual로 바꿨을 수 있어, Follow를 강제 적용)
            if (IsHolding && !LastPatrol.Systems.World.CombatStatus.InCombat)
            {
                IsHolding = false;
                SetMode(ControlMode.Follow);
            }

            // 배터리 0 → 멈춤 (이동 X, 사격은 TurretController가 ConsumeBattery 실패로 자동 차단)
            bool batteryDead = currentBattery <= 0f;

            if (!batteryDead && !IsHolding)
            {
                if (CurrentMode == ControlMode.Follow)
                {
                    follow.Tick();
                }
                else // Manual — 플레이어가 Tab으로 M-07 직접 조종
                {
                    Vector2 axis = input != null ? input.MoveAxis : Vector2.zero;
                    follow.ManualMove(axis);
                }
            }

            DrainBattery();
        }

        private void DrainBattery()
        {
            // 실내·외부 모두 idle drain 적용 — 배터리는 시간이 지남에 따라 계속 깎임.
            // M07State 정적 보존이라 씬 전환 시에도 배터리 값 이어짐.
            // 0 도달 시 사격 불가 → 마렌이 총격 받다 사망하면 Game Over (자연스러운 패배 조건).
            currentBattery = Mathf.Max(0f, currentBattery - idleDrainPerSecond * Time.deltaTime);
            SyncState();
        }

        /// <summary>
        /// 외부 매니저에서 호출용. M-07 GameObject가 비활성(차에 탑승)일 때도 배터리 닳도록.
        /// 이미 활성이면 자체 Update가 처리하므로 호출 X.
        /// </summary>
        public void TickIdleDrain(float deltaTime)
        {
            EnsureInitialized();
            if (!IsAlive) return;
            currentBattery = Mathf.Max(0f, currentBattery - idleDrainPerSecond * deltaTime);
            SyncState();
        }

        public void AddBattery(float amount)
        {
            currentBattery = Mathf.Min(maxBattery, currentBattery + amount);
            SyncState();
        }

        public bool ConsumeBattery(float amount)
        {
            if (currentBattery < amount) return false;
            currentBattery -= amount;
            SyncState();
            return true;
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0f, currentHP - amount);
            SyncState();
            if (currentHP <= 0f) OnDisabled();
        }

        public void Hack()
        {
            if (isHacked) return;
            isHacked = true;
            SyncState();
            if (eyes != null) eyes.SwitchToRed();
            // TODO: 사격 대상 변경, 음성 톤, 카메라 컷 — 핵심 비트 연출 (2-3주차).
        }

        public void Restore()
        {
            isHacked = false;
            SyncState();
            if (eyes != null) eyes.SwitchToCyan();
        }

        // 모든 상태 변화를 정적 M07State에 즉시 미러 — 씬 전환 시 보존.
        private void SyncState()
        {
            M07State.Save(currentBattery, currentHP, isHacked);
        }

        private void OnDisabled()
        {
            // 배터리 수거 가능 상태로 전환 (씬 4 메카닉 대비).
            // TODO 2주차: AnimationController에 "shutdown" 트리거, IInteractable 활성화.
            follow.enabled = false;
        }

    }
}
