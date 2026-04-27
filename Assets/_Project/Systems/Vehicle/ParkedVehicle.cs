using UnityEngine;
using LastPatrol.Core;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 도시 어딘가에 주차된 차량. 마렌/M-07이 근처에서 [E] 인터랙션하면 탑승 (강탈).
    /// 시작 시 CarController 비활성(주차 상태). 인터랙션 시 VehicleDismount.SwitchToVehicle 호출.
    ///
    /// 디자인 (바이블):
    ///   "어떤 차든 M-07이 열 수 있다."
    ///   "마렌과 M-07은 도시에 버려진 차에 올라타고 다시 임무를 수행할 수 있다."
    /// </summary>
    [DisallowMultipleComponent]
    public class ParkedVehicle : MonoBehaviour, IInteractable
    {
        [SerializeField] private CarController carController;
        [SerializeField] private VehicleHealth vehicleHealth;
        [SerializeField] private string promptLabelKR = "차에 올라타기";
        [Tooltip("시작 시 비활성(주차 상태)으로 둘지. 마렌 시작 차는 false로 두면 운전 가능 + 강탈 대상도 됨.")]
        [SerializeField] private bool startParked = true;

        public string PromptLabel => promptLabelKR;
        public bool IsAlive => true;

        private VehicleDismount _dismount;

        void Awake()
        {
            if (carController == null) carController = GetComponent<CarController>();
            if (vehicleHealth == null) vehicleHealth = GetComponent<VehicleHealth>();

            // 주차 상태(default) — 마렌이 인터랙션할 때만 활성화.
            // 마렌 시작 차의 경우 startParked=false → 그대로 운전 가능.
            if (startParked && carController != null) carController.enabled = false;

            EnsureTriggerCollider();
        }

        private void EnsureTriggerCollider()
        {
            // 차량 본체 collider 외에 인터랙션용 별도 trigger.
            // 자식 GameObject에 추가하면 root collider(BoxCast 충돌용)와 분리.
            var interactZone = new GameObject("InteractTrigger");
            interactZone.transform.SetParent(transform, false);
            var box = interactZone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 2f, 4.5f); // 차량보다 약간 큼
            box.center = new Vector3(0f, 1f, 0f);
        }

        public bool CanInteract(GameObject actor)
        {
            if (carController == null) return false;
            if (carController.IsWreck) return false;

            if (_dismount == null) _dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            if (_dismount != null && _dismount.CurrentCar == carController) return false;

            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_dismount == null) _dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            if (_dismount == null)
            {
                Debug.LogError("[ParkedVehicle] VehicleDismount 없음 — 차량 교체 불가.", this);
                return;
            }
            _dismount.SwitchToVehicle(carController);
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
