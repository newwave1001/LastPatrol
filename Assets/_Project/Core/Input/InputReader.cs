using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastPatrol.Core.Input
{
    // Input Action Asset에서 "Generate C# Class" 옵션을 켜면 LastPatrolInputs 클래스가 자동 생성됨.
    // InputReader는 그 자동 생성 클래스를 감싸서 게임 시스템에 깔끔한 이벤트 API 제공.
    [DisallowMultipleComponent]
    public class InputReader : MonoBehaviour
    {
        public Vector2 MoveAxis { get; private set; }
        public bool CoverHeld { get; private set; }
        public bool ChargeHeld { get; private set; }
        public bool FireHeld { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnSwitchPressed;
        public event Action OnPausePressed;

        private LastPatrolInputs inputs;

        void Awake()
        {
            inputs = new LastPatrolInputs();

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
        }

        void OnEnable()  => inputs?.Player.Enable();
        void OnDisable() => inputs?.Player.Disable();
        void OnDestroy() => inputs?.Dispose();
    }
}
