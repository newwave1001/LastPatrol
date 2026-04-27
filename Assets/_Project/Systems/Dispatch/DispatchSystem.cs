using UnityEngine;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Vehicle;
using LastPatrol.Systems.UI;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Dispatch
{
    /// <summary>
    /// 외부 운전 씬의 사건 dispatch 사이클.
    ///   STANDBY → (N게임초 경과) → EN ROUTE → (마커 근처) → NEAR → (도착) → ON SCENE
    ///
    /// v04 mockup의 dispatchTime / caseMarker proximity 흐름 이식.
    /// HUD 텍스트/색상 갱신, 마커 활성화, 이벤트 발행을 담당.
    /// 다음 단계(실내 진입 트리거, 사건 보드 갱신 등)는 OnDispatched / OnArrived 이벤트 구독으로 연결.
    /// </summary>
    [DisallowMultipleComponent]
    public class DispatchSystem : MonoBehaviour
    {
        public enum DispatchState { Standby, EnRoute, Near, OnScene }

        [Header("References")]
        [SerializeField] private GameClock clock;
        [SerializeField] private DriveHUD hud;
        [SerializeField] private CarController car;
        [SerializeField] private CaseMarker marker;
        [SerializeField] private InputReader input;

        [Header("Scene Transition (ON SCENE → F → next scene)")]
        [Tooltip("ON SCENE 도착 후 InputReader.Drive.Exit (F) 입력 시 로드할 씬 이름. " +
                 "Build Settings의 Scene List에 등록되어 있어야 함.")]
        [SerializeField] private string nextSceneOnArrival = "S03_IndoorInvestigation";
        [Tooltip("프롬프트를 dispatch 텍스트에 붙임 (예: 'ON SCENE · WESTSIDE 132  ·  [F] ENTER')")]
        [SerializeField] private bool showEnterPrompt = true;

        [Header("Trigger Timing")]
        [Tooltip("게임 시간(초) 기준 dispatch 트리거 시점. v04: 35초.")]
        [SerializeField] private int dispatchAtGameSecond = 35;

        [Header("Distances (Unity meters)")]
        [Tooltip("v04 200px ≈ 25m. 그레이박스 도시(100m)에 맞춰 18m 권장.")]
        [SerializeField] private float nearDistance = 18f;
        [Tooltip("v04 80px ≈ 10m. 그레이박스 6m 권장.")]
        [SerializeField] private float arrivedDistance = 6f;

        [Header("Initial HUD")]
        [SerializeField] private string standbyText = "STANDBY";

        [Header("Marker Activation")]
        [Tooltip("dispatch 전엔 마커를 비활성화 (씬 시작 시 SetActive(false)).")]
        [SerializeField] private bool hideMarkerUntilDispatch = true;

        public DispatchState State { get; private set; } = DispatchState.Standby;

        public event System.Action OnDispatched;
        public event System.Action OnArrived;

        private int _baseSeconds;

        void Awake()
        {
            if (clock  == null) clock  = FindFirstObjectByType<GameClock>();
            if (hud    == null) hud    = FindFirstObjectByType<DriveHUD>();
            if (car    == null) car    = FindFirstObjectByType<CarController>();
            if (marker == null) marker = FindFirstObjectByType<CaseMarker>();
            if (input  == null) input  = FindFirstObjectByType<InputReader>();
        }

        void OnEnable()
        {
            if (input != null) input.OnExitVehiclePressed += HandleExitPressed;
        }

        void OnDisable()
        {
            if (input != null) input.OnExitVehiclePressed -= HandleExitPressed;
        }

        private void HandleExitPressed()
        {
            if (State != DispatchState.OnScene) return;
            if (string.IsNullOrEmpty(nextSceneOnArrival)) return;
            if (SceneTransitionService.Instance == null)
            {
                Debug.LogError("[Dispatch] SceneTransitionService 인스턴스 없음. " +
                               "씬에 SceneTransitionService 컴포넌트 GameObject 하나 추가하거나 " +
                               "별도 부트스트랩 씬에서 DontDestroyOnLoad 인스턴스를 만들어야 함.");
                return;
            }

            // 사건 컨텍스트 전달 — 다음 씬(InvestigationSystem)이 ActiveCase.Current를 읽어 자동 로드.
            if (marker != null && marker.CaseData != null)
                ActiveCase.SetCurrent(marker.CaseData);

            // 사건 종료 후 S01 복귀 시 차량을 "내가 내린 자리"에 다시 두기.
            if (car != null) PlayerSpawnPoint.SetFromTransform(car.transform);

            SceneTransitionService.Instance.LoadScene(nextSceneOnArrival);
        }

        void Start()
        {
            if (clock != null) _baseSeconds = clock.TotalSeconds;

            if (hud != null) hud.SetDispatch(standbyText, DriveHUD.DispatchTone.Ink);

            if (marker != null && hideMarkerUntilDispatch)
                marker.gameObject.SetActive(false);
        }

        void Update()
        {
            if (clock == null || hud == null) return;

            int elapsed = clock.TotalSeconds - _baseSeconds;

            // STANDBY → EN ROUTE
            if (State == DispatchState.Standby && elapsed >= dispatchAtGameSecond)
            {
                State = DispatchState.EnRoute;
                if (marker != null) marker.gameObject.SetActive(true);

                string id = marker != null ? marker.CaseId : "CASE-????";
                hud.SetDispatch($"EN ROUTE  ·  {id}", DriveHUD.DispatchTone.Amber);
                Debug.Log($"[Dispatch] {id} {(marker != null ? marker.DispatchBlurb : string.Empty)}");
                OnDispatched?.Invoke();
            }

            // 거리 체크는 EN ROUTE 이상에서만
            if (State < DispatchState.EnRoute || marker == null || car == null) return;
            if (State == DispatchState.OnScene) return;

            float d = Vector3.Distance(car.transform.position, marker.Position);

            if (d < arrivedDistance)
            {
                State = DispatchState.OnScene;
                string addr = marker.AddressText;
                hud.SetDispatch($"ON SCENE  ·  {addr}", DriveHUD.DispatchTone.Blood);
                if (showEnterPrompt && !string.IsNullOrEmpty(nextSceneOnArrival))
                    hud.SetPrompt("[F] ENTER", DriveHUD.DispatchTone.Amber);
                OnArrived?.Invoke();
            }
            else if (State == DispatchState.EnRoute && d < nearDistance)
            {
                State = DispatchState.Near;
                hud.SetDispatch($"NEAR  ·  {marker.AddressText}", DriveHUD.DispatchTone.Cyan);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (marker == null) return;
            Gizmos.color = new Color(0.486f, 0.784f, 0.847f, 0.4f); // cyan, near
            Gizmos.DrawWireSphere(marker.transform.position, nearDistance);
            Gizmos.color = new Color(0.659f, 0.188f, 0.165f, 0.6f); // blood, arrived
            Gizmos.DrawWireSphere(marker.transform.position, arrivedDistance);
        }
#endif
    }
}
