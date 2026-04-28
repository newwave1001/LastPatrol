using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Vehicle;
using LastPatrol.Core.Input;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 외부 운전 씬용 PartyCamera 변형. PartyController.OnSwitched를 받아
    /// TopDownCarCamera.SetTarget을 마렌 ↔ M-07로 토글.
    ///
    /// 차에 탄 상태에선 PartyController가 비활성이라 작동 안 함 (VehicleDismount가 토글).
    /// 차 밖일 때만 의미 — 마렌이 활성이면 카메라 마렌 추종, M-07이 활성이면 M-07 추종.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutdoorPartyCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PartyController party;
        [SerializeField] private TopDownCarCamera cam;
        [SerializeField] private Transform marenTarget;
        [SerializeField] private Transform m07Target;
        [SerializeField] private VehicleDismount dismount;

        void Awake()
        {
            if (party == null) party = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            if (cam == null) cam = FindAnyObjectByType<TopDownCarCamera>();
            // 차 안 시작 시 마렌/M-07 GO 비활성 → FindObjectsInactive.Include 필수
            if (marenTarget == null)
            {
                var m = FindAnyObjectByType<MarenController>(FindObjectsInactive.Include);
                if (m != null) marenTarget = m.transform;
            }
            if (m07Target == null)
            {
                var r = FindAnyObjectByType<M07Controller>(FindObjectsInactive.Include);
                if (r != null) m07Target = r.transform;
            }
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            if (party == null) return;
            party.OnSwitched += Apply;
            // OnEnable에서 즉시 Apply 안 함 — 카메라 초기 target은 VehicleDismount.Mount/FinalizeDismount가 관리.
            // 이후 Tab으로 활성 전환 시 OnSwitched 이벤트로 Apply가 호출됨.
        }

        void OnDisable()
        {
            if (party != null) party.OnSwitched -= Apply;
        }

        private void Apply(PartyController.ActiveCharacter who)
        {
            if (cam == null) { Debug.LogWarning("[OutdoorCam] cam == null — TopDownCarCamera 못 찾음."); return; }
            // 차 안일 땐 카메라 target 변경 X — VehicleDismount.Mount/Switch가 차로 관리.
            if (dismount != null && !dismount.IsDismounted)
            {
                Debug.Log($"[OutdoorCam] Apply({who}) skip — 차 안.");
                return;
            }
            Transform t = (who == PartyController.ActiveCharacter.Maren) ? marenTarget : m07Target;
            Debug.Log($"[OutdoorCam] Apply({who}) → target={(t!=null?t.name:"NULL")}");
            if (t != null) cam.SetTarget(t);
            else Debug.LogWarning($"[OutdoorCam] {who} target null — Inspector에서 직접 드래그 필요");
        }
    }
}
