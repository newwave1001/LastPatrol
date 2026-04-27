using System.Collections;
using UnityEngine;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Dispatch;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 외부 운전 씬에서 차에서 내리고 다시 타는 메카닉.
    ///   F (Drive 모드 Exit) → 조건 충족 시 하차 / 다시 누르면 승차.
    ///
    /// 우선순위 분기:
    ///   - DispatchSystem.State == OnScene 일 때는 DispatchSystem이 씬 전환 처리 → 하차 무시
    ///   - 그 외엔 인카운터 활성/PlayerStatus.IsHunted 조건이면 하차 가능
    ///
    /// 마렌 캐릭터는 외부 씬에 비활성으로 미리 배치되어 있어야 함 (P_Maren prefab 인스턴스).
    /// 하차 시 차량 옆 위치로 텔레포트 + 활성, 카메라 타겟도 마렌으로 자동 전환.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleDismount : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private CarController car;
        [Tooltip("외부 씬의 마렌 캐릭터 GameObject (비활성 배치). 하차 시 활성화.")]
        [SerializeField] private GameObject marenCharacter;
        [Tooltip("외부 씬의 M-07 캐릭터 GameObject (비활성 배치). 마렌이 내리면 같이 내림.")]
        [SerializeField] private GameObject m07Character;
        [SerializeField] private DispatchSystem dispatch;
        [SerializeField] private TopDownCarCamera carCamera;
        [Tooltip("하차 시 활성, 승차 시 비활성. Tab 캐릭터 전환을 외부 씬에서 통제.")]
        [SerializeField] private PartyController partyController;

        [Header("Dismount Position")]
        [Tooltip("차량 기준 마렌 스폰 offset. (right, up, forward). 기본 차 좌측 2.5m.")]
        [SerializeField] private Vector3 marenOffsetLocal = new Vector3(-2.5f, 0f, 0f);
        [Tooltip("차량 기준 M-07 스폰 offset. 보통 차 우측 또는 마렌 옆.")]
        [SerializeField] private Vector3 m07OffsetLocal = new Vector3(2.5f, 0f, 0f);

        [Header("Brake")]
        [Tooltip("F 누르면 차가 N초 동안 점진 감속하고 멈춘 후 하차. 0이면 즉시.")]
        [SerializeField] private float brakeDuration = 0.6f;

        [Header("Mount")]
        [Tooltip("도보 상태에서 마렌이 이 거리 안에 있으면 F로 다시 차에 탈 수 있음.")]
        [SerializeField] private float mountRadius = 4f;

        [Header("Camera Zoom")]
        [Tooltip("하차 시 도보 모드(쿼터뷰)로 전환. TopDownCarCamera.SetFootMode 사용. " +
                 "각도 / height / lookAhead 등은 카메라 인스펙터 'Foot Mode' 섹션에서 튜닝.")]
        [SerializeField] private bool useFootCameraMode = true;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        public bool IsDismounted { get; private set; }
        public Transform MarenTransform => marenCharacter != null ? marenCharacter.transform : null;

        void Awake()
        {
            if (input    == null) input    = FindAnyObjectByType<InputReader>();
            if (car      == null) car      = FindAnyObjectByType<CarController>();
            if (dispatch == null) dispatch = FindAnyObjectByType<DispatchSystem>();
            if (carCamera == null) carCamera = FindAnyObjectByType<TopDownCarCamera>();
            if (partyController == null) partyController = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);

            // 시작은 차 안 — PartyController 비활성 (Tab 무반응)
            if (partyController != null) partyController.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (input != null)
            {
                input.OnExitVehiclePressed += HandleExit;   // Drive 모드 F → 하차/씬전환
                input.OnMountPressed += HandleMount;        // Foot 모드 F → 차 근처면 승차
            }
        }

        void OnDisable()
        {
            if (input != null)
            {
                input.OnExitVehiclePressed -= HandleExit;
                input.OnMountPressed -= HandleMount;
            }
        }

        // Foot 모드 F — 차 근처면 mount. 멀면 무시.
        private void HandleMount()
        {
            if (!IsDismounted || _transitioning) return;
            if (marenCharacter == null || car == null) return;
            float d = Vector3.Distance(marenCharacter.transform.position, car.transform.position);
            if (d > mountRadius) return;
            Mount();
        }

        private bool _transitioning;

        private void HandleExit()
        {
            // ON SCENE 상태에선 DispatchSystem이 씬 전환 처리 — 우리는 양보
            if (dispatch != null && dispatch.State == DispatchSystem.DispatchState.OnScene) return;
            if (_transitioning) return;

            if (IsDismounted) Mount();
            else BeginDismount();
        }

        [ContextMenu("Force Dismount (Debug)")]
        public void BeginDismount()
        {
            if (car == null || marenCharacter == null) return;
            if (_transitioning) return;
            _transitioning = true;

            // 차 점진 감속 → 멈춘 후 실제 하차 + 활성화
            if (brakeDuration > 0.01f)
            {
                car.BrakeToStop(brakeDuration, FinalizeDismount);
            }
            else
            {
                FinalizeDismount();
            }
        }

        private void FinalizeDismount()
        {
            // 마렌 위치 = 차량 로컬 offset
            Vector3 marenWorld = car.transform.position
                + car.transform.right   * marenOffsetLocal.x
                + Vector3.up           * marenOffsetLocal.y
                + car.transform.forward * marenOffsetLocal.z;
            marenWorld.y = Mathf.Max(marenWorld.y, 0.1f);
            marenCharacter.transform.position = marenWorld;
            marenCharacter.transform.rotation = car.transform.rotation;
            marenCharacter.SetActive(true);

            // M-07도 따라 내림
            if (m07Character != null)
            {
                Vector3 m07World = car.transform.position
                    + car.transform.right   * m07OffsetLocal.x
                    + Vector3.up           * m07OffsetLocal.y
                    + car.transform.forward * m07OffsetLocal.z;
                m07World.y = Mathf.Max(m07World.y, 0.1f);
                m07Character.transform.position = m07World;
                m07Character.transform.rotation = car.transform.rotation;
                m07Character.SetActive(true);
            }

            // 차량 운행 중지
            car.enabled = false;

            // Foot 모드 + 카메라 마렌 추종 + 줌인
            if (input != null) input.EnableFootControls();
            if (carCamera != null)
            {
                carCamera.SetTarget(marenCharacter.transform);
                if (useFootCameraMode) carCamera.SetFootMode(true);
            }

            // PartyController 활성화 → Tab 전환 작동
            if (partyController != null) partyController.gameObject.SetActive(true);

            IsDismounted = true;
            _transitioning = false;
            if (logEvents) Debug.Log($"[Dismount] complete. Maren at {marenWorld}", this);
        }

        [ContextMenu("Force Mount (Debug)")]
        public void Mount()
        {
            if (car == null || marenCharacter == null) return;
            if (_transitioning) return;

            marenCharacter.SetActive(false);
            if (m07Character != null) m07Character.SetActive(false);

            car.enabled = true;
            if (input != null) input.EnableDriveControls();
            if (carCamera != null)
            {
                carCamera.SetTarget(car.transform);
                if (useFootCameraMode) carCamera.SetFootMode(false);
                else carCamera.ResetFramingZoom();
            }

            // PartyController 비활성 → Tab 무반응으로
            if (partyController != null) partyController.gameObject.SetActive(false);

            IsDismounted = false;
            if (logEvents) Debug.Log("[Dismount] re-mounted vehicle.", this);
        }
    }
}
