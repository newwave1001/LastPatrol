using UnityEngine;
using UnityEngine.InputSystem;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;

namespace LastPatrol.Systems.Battery
{
    /// <summary>
    /// 외부 운전 씬(S01)에서 R 키 누름을 항상 받음 — 차 안/밖 모두 작동.
    ///
    /// 역할 분담:
    ///   - 도보 모드 (Maren active) → MarenController.HandleChargePress가 처리 (chargeRange 체크 포함)
    ///   - 운전 모드 (Maren inactive) → 이 컴포넌트가 직접 처리 (M-07도 같이 차에 있다고 가정, 거리 무시)
    ///
    /// 중복 방지: Maren이 active 상태면 OutdoorChargeHandler는 skip.
    ///
    /// 사용:
    ///   S01 씬에 빈 GameObject "OutdoorChargeHandler" 만들고 부착. 자동 검색.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutdoorChargeHandler : MonoBehaviour
    {
        [Header("References (자동 검색)")]
        [SerializeField] private MarenController maren;
        [SerializeField] private M07Controller m07;

        [Header("Charge")]
        [Tooltip("R 1회 누름당 M-07 충전 양 (배터리 1개 = 30%).")]
        [SerializeField] private float chargePerBattery = 30f;

        [Header("Debug")]
        [SerializeField] private bool logCharges = true;

        private InputAction _chargeAction;

        void Awake()
        {
            if (maren == null) maren = FindAnyObjectByType<MarenController>(FindObjectsInactive.Include);
            if (m07 == null) m07 = FindAnyObjectByType<M07Controller>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            _chargeAction = new InputAction("OutdoorCharge", binding: "<Keyboard>/r");
            _chargeAction.performed += HandleChargePerformed;
            _chargeAction.Enable();
        }

        void OnDisable()
        {
            if (_chargeAction != null)
            {
                _chargeAction.performed -= HandleChargePerformed;
                _chargeAction.Disable();
                _chargeAction.Dispose();
                _chargeAction = null;
            }
        }

        private void HandleChargePerformed(InputAction.CallbackContext ctx)
        {
            // 도보 모드 (Maren GO active) → MarenController 처리. 중복 충전 방지.
            if (maren != null && maren.gameObject.activeInHierarchy) return;

            // 운전 모드 — 직접 처리. M-07도 차 안에 있다고 가정 (거리 체크 생략).
            if (m07 == null || !m07.IsAlive) return;

            if (!BatteryInventory.TryConsume(1))
            {
                if (logCharges) Debug.Log("[OutdoorChargeHandler] 배터리 없음 — 충전 무시.", this);
                return;
            }
            m07.AddBattery(chargePerBattery);
            if (logCharges) Debug.Log($"[OutdoorChargeHandler] 차 안 충전 +{chargePerBattery}% → M-07 {m07.CurrentBattery:F0}/{m07.MaxBattery:F0}", this);
        }
    }
}
