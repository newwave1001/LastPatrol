using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Vehicle;

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

        void Awake()
        {
            if (party == null) party = FindAnyObjectByType<PartyController>();
            if (cam == null) cam = FindAnyObjectByType<TopDownCarCamera>();
            if (marenTarget == null)
            {
                var m = FindAnyObjectByType<MarenController>();
                if (m != null) marenTarget = m.transform;
            }
            if (m07Target == null)
            {
                var r = FindAnyObjectByType<M07Controller>();
                if (r != null) m07Target = r.transform;
            }
        }

        void OnEnable()
        {
            if (party == null) return;
            party.OnSwitched += Apply;
            Apply(party.Active);
        }

        void OnDisable()
        {
            if (party != null) party.OnSwitched -= Apply;
        }

        private void Apply(PartyController.ActiveCharacter who)
        {
            if (cam == null) return;
            Transform t = (who == PartyController.ActiveCharacter.Maren) ? marenTarget : m07Target;
            if (t != null) cam.SetTarget(t);
        }
    }
}
