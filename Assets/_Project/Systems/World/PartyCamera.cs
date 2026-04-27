using UnityEngine;
using Unity.Cinemachine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// PartyController의 활성 캐릭터에 따라 Cinemachine VCam의 Follow 타겟을 마렌 ↔ M-07로 전환.
    /// Cinemachine 3.x (Unity 6) API 사용.
    ///
    /// 보통 S03 씬의 CinemachineCamera GameObject에 부착, 또는 빈 GO에 두고 vcam 슬롯 드래그.
    /// </summary>
    [DisallowMultipleComponent]
    public class PartyCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera vcam;
        [SerializeField] private PartyController party;
        [SerializeField] private Transform marenTarget;
        [SerializeField] private Transform m07Target;

        // 타겟 전환 시 부드러움/즉시 컷은 Cinemachine Brain의 Default Blend 설정으로 제어.
        // (CinemachineCamera.Target.TrackingTarget 변경 → Brain이 자동 blend.)

        void Awake()
        {
            if (vcam == null) vcam = GetComponent<CinemachineCamera>();
            if (vcam == null) vcam = FindAnyObjectByType<CinemachineCamera>();
            if (party == null) party = FindAnyObjectByType<PartyController>();

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
            if (party != null)
            {
                party.OnSwitched += HandleSwitched;
                HandleSwitched(party.Active); // 시작 시 현재 활성에 맞춤
            }
        }

        void OnDisable()
        {
            if (party != null) party.OnSwitched -= HandleSwitched;
        }

        private void HandleSwitched(PartyController.ActiveCharacter who)
        {
            if (vcam == null) return;
            Transform t = (who == PartyController.ActiveCharacter.Maren) ? marenTarget : m07Target;
            if (t == null) return;

            // Cinemachine 3.x: Target.TrackingTarget이 새 표준 + 옛 Follow도 호환.
            vcam.Target.TrackingTarget = t;
        }

        [ContextMenu("Apply Now (Debug)")]
        private void DebugApply()
        {
            if (party != null) HandleSwitched(party.Active);
        }
    }
}
