using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastPatrol.Core.Input
{
    // Input Action Asset에서 "Generate C# Class" 옵션을 켜면 LastPatrolInputs 클래스가 자동 생성됨.
    // InputReader는 그 자동 생성 클래스를 감싸서 게임 시스템에 깔끔한 이벤트 API 제공.
    //
    // ActionMap 두 개:
    //   Player — 도보 (Maren 실내 수사). Move/Cover/Charge/Interact/Switch/Fire/Pause
    //   Drive  — 차량 운전 (Maren 외부 운전). Move/Boost/Exit/Pause
    // 한 번에 하나만 활성화. EnableFootControls / EnableDriveControls 로 전환.
    [DisallowMultipleComponent]
    public class InputReader : MonoBehaviour
    {
        // --- Foot (Player map) ---
        public Vector2 MoveAxis { get; private set; }
        public bool CoverHeld { get; private set; }
        public bool ChargeHeld { get; private set; }
        public bool FireHeld { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnSwitchPressed;
        public event Action OnPausePressed;

        // --- Drive map ---
        public Vector2 DriveAxis { get; private set; }   // x = steer, y = throttle/reverse
        public bool BoostHeld { get; private set; }

        public event Action OnExitVehiclePressed;

        public enum Mode { None, Foot, Drive }
        public Mode CurrentMode { get; private set; } = Mode.None;

        private LastPatrolInputs inputs;

        void Awake()
        {
            inputs = new LastPatrolInputs();

            // -- Player (Foot) --
            inputs.Player.Move.performed += ctx => MoveAxis = ctx.ReadValue<Vector2>();
            inputs.Player.Move.canceled  += _   => MoveAxis = Vector2.zero;

            inputs.Player.Cover.performed += _ => CoverHeld = true;
            inputs.Player.Cover.canceled  += _ => CoverHeld = false;

            inputs.Player.Charge.performed += _ => ChargeHeld = true;
            inputs.Player.Charge.canceled  += _ => ChargeHeld = false;

            inputs.Player.Fire.performed += _ => FireHeld = true;
            inputs.Player.Fire.canceled  += _ => FireHeld = false;

            inputs.Player.Interact.performed += _ => OnInteractPressed?.Invoke();
            inputs.Player.Switch.performed   += _ => OnSwitchPressed?.Invoke();
            inputs.Player.Pause.performed    += _ => OnPausePressed?.Invoke();

            // -- Drive --
            inputs.Drive.Move.performed += ctx => DriveAxis = ctx.ReadValue<Vector2>();
            inputs.Drive.Move.canceled  += _   => DriveAxis = Vector2.zero;

            inputs.Drive.Boost.performed += _ => BoostHeld = true;
            inputs.Drive.Boost.canceled  += _ => BoostHeld = false;

            inputs.Drive.Exit.performed  += _ => OnExitVehiclePressed?.Invoke();
            inputs.Drive.Pause.performed += _ => OnPausePressed?.Invoke();
        }

        void OnEnable()
        {
            // 기본은 Foot. 씬이 차량 모드로 시작해야 하면 Start에서 EnableDriveControls 호출.
            if (CurrentMode == Mode.None) EnableFootControls();
        }

        void OnDisable() => DisableAll();
        void OnDestroy() => inputs?.Dispose();

        public void EnableFootControls()
        {
            inputs.Drive.Disable();
            inputs.Player.Enable();
            CurrentMode = Mode.Foot;

            // 차량 상태 잔재 정리
            DriveAxis = Vector2.zero;
            BoostHeld = false;
        }

        public void EnableDriveControls()
        {
            inputs.Player.Disable();
            inputs.Drive.Enable();
            CurrentMode = Mode.Drive;

            // 도보 상태 잔재 정리
            MoveAxis = Vector2.zero;
            CoverHeld = false;
            ChargeHeld = false;
            FireHeld = false;
        }

        public void DisableAll()
        {
            inputs?.Player.Disable();
            inputs?.Drive.Disable();
            CurrentMode = Mode.None;
            MoveAxis = Vector2.zero;
            DriveAxis = Vector2.zero;
            CoverHeld = ChargeHeld = FireHeld = BoostHeld = false;
        }
    }
}
