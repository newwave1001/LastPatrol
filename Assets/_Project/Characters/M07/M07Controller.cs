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

        public ControlMode CurrentMode { get; private set; } = ControlMode.Follow;
        public event Action<ControlMode> OnModeChanged;

        public void SetMode(ControlMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

        public float CurrentBattery => currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryPercent => maxBattery > 0f ? currentBattery / maxBattery : 0f;
        public bool IsAlive => currentHP > 0f;
        public bool IsHacked => isHacked;

        void Awake()
        {
            follow = GetComponent<FollowBehavior>();
            currentBattery = maxBattery;
            currentHP = maxHP;
            if (input == null) input = FindFirstObjectByType<InputReader>();
        }

        void Update()
        {
            if (!IsAlive) return;

            if (CurrentMode == ControlMode.Follow)
            {
                follow.Tick();
            }
            else // Manual — 플레이어가 Tab으로 M-07 직접 조종
            {
                Vector2 axis = input != null ? input.MoveAxis : Vector2.zero;
                follow.ManualMove(axis);
            }

            DrainBattery();
        }

        private void DrainBattery()
        {
            currentBattery = Mathf.Max(0f, currentBattery - idleDrainPerSecond * Time.deltaTime);
        }

        public void AddBattery(float amount)
        {
            currentBattery = Mathf.Min(maxBattery, currentBattery + amount);
        }

        public bool ConsumeBattery(float amount)
        {
            if (currentBattery < amount) return false;
            currentBattery -= amount;
            return true;
        }

        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0f, currentHP - amount);
            if (currentHP <= 0f) OnDisabled();
        }

        public void Hack()
        {
            if (isHacked) return;
            isHacked = true;
            if (eyes != null) eyes.SwitchToRed();
            // TODO: 사격 대상 변경, 음성 톤, 카메라 컷 — 핵심 비트 연출 (2-3주차).
        }

        public void Restore()
        {
            isHacked = false;
            if (eyes != null) eyes.SwitchToCyan();
        }

        private void OnDisabled()
        {
            // 배터리 수거 가능 상태로 전환 (씬 4 메카닉 대비).
            // TODO 2주차: AnimationController에 "shutdown" 트리거, IInteractable 활성화.
            follow.enabled = false;
        }

    }
}
