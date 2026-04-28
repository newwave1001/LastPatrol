using System.Collections;
using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Dispatch;
using LastPatrol.Systems.Encounter;
using LastPatrol.Systems.Audio;
using LastPatrol.Systems.UI;
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
        [Tooltip("차량 강탈 시 새 차로 갱신할 시스템들 (속도/HP/적 스폰 등).")]
        [SerializeField] private DriveHUD driveHUD;
        [SerializeField] private EncounterSpawner encounterSpawner;

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
        public CarController CurrentCar => car;

        void Awake()
        {
            if (input    == null) input    = FindAnyObjectByType<InputReader>();
            if (car      == null) car      = FindAnyObjectByType<CarController>();
            if (dispatch == null) dispatch = FindAnyObjectByType<DispatchSystem>();
            if (carCamera == null) carCamera = FindAnyObjectByType<TopDownCarCamera>();
            if (partyController == null) partyController = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            if (driveHUD == null) driveHUD = FindAnyObjectByType<DriveHUD>();
            if (encounterSpawner == null) encounterSpawner = FindAnyObjectByType<EncounterSpawner>();

            // 시작은 차 안 — PartyController 비활성 (Tab 무반응)
            if (partyController != null) partyController.gameObject.SetActive(false);
        }

        void Start()
        {
            // 시작 시 모든 차의 헤드라이트 OFF — 활성 차만 ON
            var allHeadlights = FindObjectsByType<Headlights>(FindObjectsInactive.Include);
            foreach (var hl in allHeadlights) hl.SetOn(false);
            if (car != null)
            {
                var hl = car.GetComponent<Headlights>();
                if (hl != null) hl.SetOn(true);
            }
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

        // Foot 모드 F — 가장 가까운 차(자기 차든 주차 차든) mount/switch. 멀면 무시. 파괴된 차 제외.
        private void HandleMount()
        {
            if (!IsDismounted || _transitioning) return;
            if (marenCharacter == null) return;

            var nearest = FindNearestMountableCar();
            if (nearest == null) return;

            if (nearest == car) Mount();           // 자기 차 다시 탑승
            else SwitchToVehicle(nearest);          // 주차된 다른 차로 갈아타기
        }

        /// <summary>마렌 mountRadius 안의 가장 가까운 운전 가능 차량. 파괴된 차/null 제외.</summary>
        public CarController FindNearestMountableCar()
        {
            if (marenCharacter == null) return null;
            CarController nearest = null;
            float nearestDist = float.MaxValue;
            var allCars = FindObjectsByType<CarController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allCars.Length; i++)
            {
                var c = allCars[i];
                if (c == null || c.IsWreck) continue;
                float d = Vector3.Distance(marenCharacter.transform.position, c.transform.position);
                if (d > mountRadius) continue;
                if (d < nearestDist)
                {
                    nearest = c;
                    nearestDist = d;
                }
            }
            return nearest;
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
            AudioManager.PlaySfx(SfxKey.VehicleDismount, car.transform.position);
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

            // Foot 모드 + 카메라 추종 + 줌인
            // 전투 중이면 처음부터 M07 target — Lerp 도중 target 바꾸면 카메라 튀는 거 방지
            if (input != null) input.EnableFootControls();
            if (carCamera != null)
            {
                bool inCombat = LastPatrol.Systems.World.CombatStatus.InCombat;
                Transform initialTarget = (inCombat && m07Character != null)
                    ? m07Character.transform
                    : marenCharacter.transform;
                carCamera.SetTarget(initialTarget);
                if (useFootCameraMode) carCamera.SetFootMode(true);
            }

            // PartyController 활성화 → Tab 전환 작동
            if (partyController != null) partyController.gameObject.SetActive(true);

            // 하차 시 차 라이트 자동 OFF
            if (car != null)
            {
                var hl = car.GetComponent<Headlights>();
                if (hl != null) hl.SetOn(false);
            }

            // dismount 완료 마크 — ApplyCombatModeIfNeeded보다 먼저!
            // PartyController.SetActive 호출이 OnSwitched → OutdoorPartyCamera.Apply를 트리거하는데,
            // Apply 안의 IsDismounted 가드가 false면 skip되므로 미리 true로 둠.
            IsDismounted = true;
            _transitioning = false;

            // 전투 중 하차면 자동 대피 모드 — 마렌 도주 + Active=M07 (카메라 M07 자동 전환)
            ApplyCombatModeIfNeeded();

            if (logEvents) Debug.Log($"[Dismount] complete. Maren at {marenWorld}", this);
        }

        /// <summary>외부 전투 중 하차 시 — 마렌 자동 도주 + 활성 캐릭터=M07 (사용자가 바로 M07 직접 조종).</summary>
        private void ApplyCombatModeIfNeeded()
        {
            bool inCombat = CombatStatus.InCombat;
            var maren = marenCharacter != null ? marenCharacter.GetComponent<MarenController>() : null;
            var m07 = m07Character != null ? m07Character.GetComponent<M07Controller>() : null;

            if (inCombat)
            {
                if (maren != null) maren.SetMode(MarenController.ControlMode.Flee);
                if (m07 != null) m07.SetHold(false); // 직접 조종이라 Hold X
                // 활성 캐릭터 = M07 → 카메라 M07 추종 + 사용자 WASD/마우스로 M07 조종
                if (partyController != null) partyController.SetActive(PartyController.ActiveCharacter.M07);
                if (logEvents) Debug.Log("[Dismount] AMBUSH 전투 — 마렌 자동 도주, Active=M07 (직접 조종).", this);
            }
            else
            {
                // 일반 하차 — Manual 마렌, Follow M-07, 활성=Maren
                if (maren != null) maren.SetMode(MarenController.ControlMode.Manual);
                if (m07 != null) m07.SetHold(false);
                if (partyController != null) partyController.SetActive(PartyController.ActiveCharacter.Maren);
            }
        }

        /// <summary>도시 다른 차에 갈아타기. 기존 차 비활성, 새 차로 마운트.</summary>
        public void SwitchToVehicle(CarController newCar)
        {
            if (newCar == null) return;
            if (newCar == car) { Mount(); return; }
            if (newCar.IsWreck)
            {
                if (logEvents) Debug.Log("[Dismount] 그 차는 파괴된 상태 — 탑승 불가.", this);
                return;
            }

            // 기존 차 비활성 + 라이트 OFF
            if (car != null)
            {
                car.enabled = false;
                var oldHl = car.GetComponent<Headlights>();
                if (oldHl != null) oldHl.SetOn(false);
            }

            // 참조 교체
            car = newCar;

            // 새 차 라이트 ON (Mount가 다시 켜겠지만 SwitchToVehicle도 Mount 호출 안 할 수도 있어 명시)
            var newHl = newCar.GetComponent<Headlights>();
            if (newHl != null) newHl.SetOn(true);

            // 핵심: 새 차에 InputReader 주입 (ParkedVehicle은 InputReader 없음)
            if (input != null) car.SetInput(input);

            // 카메라 target 변경
            if (carCamera != null) carCamera.SetTarget(car.transform);

            // 다른 시스템들도 새 차로 갱신 — 속도/HP/적 스폰
            if (driveHUD != null) driveHUD.SetCar(car);
            if (dispatch != null) dispatch.SetCar(car);
            if (encounterSpawner != null) encounterSpawner.SetTarget(car);

            // 도보였으면 마운트로
            if (IsDismounted) Mount();
            else
            {
                car.enabled = true;
            }

            if (logEvents) Debug.Log($"[Dismount] switched to vehicle '{newCar.name}' (input transferred)", this);
        }

        [ContextMenu("Force Mount (Debug)")]
        public void Mount()
        {
            if (car == null || marenCharacter == null) return;
            if (_transitioning) return;

            marenCharacter.SetActive(false);
            if (m07Character != null) m07Character.SetActive(false);

            car.enabled = true;
            // AMBUSH 잔재 — 차 immobilized 잔존 시 다시 못 움직임. 승차 시 항상 해제.
            car.SetImmobilized(false);
            if (input != null) input.EnableDriveControls();
            AudioManager.PlaySfx(SfxKey.VehicleIgnition, car.transform.position);
            if (carCamera != null)
            {
                carCamera.SetTarget(car.transform);
                if (useFootCameraMode) carCamera.SetFootMode(false);
                else carCamera.ResetFramingZoom();
            }

            // PartyController 비활성 → Tab 무반응으로
            if (partyController != null) partyController.gameObject.SetActive(false);

            // 승차 시 차 라이트 자동 ON
            if (car != null)
            {
                var hl = car.GetComponent<Headlights>();
                if (hl != null) hl.SetOn(true);
            }

            IsDismounted = false;
            if (logEvents) Debug.Log("[Dismount] re-mounted vehicle.", this);
        }
    }
}
