using UnityEngine;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 도시 어딘가에 주차된 차량 마커. CarController 시작 시 비활성(주차 상태).
    /// 마렌이 근처에서 [F] 누르면 VehicleDismount가 가장 가까운 차로 자동 mount/switch.
    /// (이전엔 [E] IInteractable로 hijack 했지만 F 키로 통일해 단일 인터페이스.)
    ///
    /// 디자인 (바이블):
    ///   "어떤 차든 M-07이 열 수 있다."
    ///   "마렌과 M-07은 도시에 버려진 차에 올라타고 다시 임무를 수행할 수 있다."
    /// </summary>
    [DisallowMultipleComponent]
    public class ParkedVehicle : MonoBehaviour
    {
        [SerializeField] private CarController carController;
        [SerializeField] private VehicleHealth vehicleHealth;
        [Tooltip("시작 시 비활성(주차 상태)으로 둘지. 마렌 시작 차는 false로 두면 운전 가능 + mount 대상도 됨.")]
        [SerializeField] private bool startParked = true;

        public CarController CarController => carController;
        public bool IsParked => carController != null && !carController.enabled;

        void Awake()
        {
            if (carController == null) carController = GetComponent<CarController>();
            if (vehicleHealth == null) vehicleHealth = GetComponent<VehicleHealth>();

            // 주차 상태(default) — VehicleDismount.SwitchToVehicle이 활성화 함.
            if (startParked && carController != null) carController.enabled = false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(3f, 2f, 4.5f));
        }
#endif
    }
}
