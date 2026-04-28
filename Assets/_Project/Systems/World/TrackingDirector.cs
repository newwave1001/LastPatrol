using UnityEngine;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 헌티드(추적 받는 중) 상태를 매 프레임 자동 판정. PlayerStatus.IsHunted를 갱신.
    ///
    /// 바이블 §: M-07이 해킹으로 ELI의 위치 추적을 막는 게 핵심 메카닉.
    /// 배터리가 있으면 추적 차단, 없으면 즉시 추적 노출.
    /// 헤드라이트는 "불빛이 위치를 외친다" — M-07 상태 무관 즉시 추적.
    ///
    /// 헌티드 = ON 조건:
    ///   1) 활성 차량 헤드라이트 ON  → 무조건 hunted
    ///   2) M-07 죽음 또는 배터리 0% → hunted (jamming 실패)
    /// 헌티드 = OFF 조건:
    ///   - 헤드라이트 OFF + M-07 살아 있고 배터리 > 0%
    ///
    /// 사용:
    ///   S01 씬에 빈 GO "TrackingDirector" 만들고 이 컴포넌트 부착. 참조는 자동 검색.
    ///   EncounterSpawner는 PlayerStatus.IsHunted만 보면 됨 (requireHeadlightsOn 사용 안 함).
    /// </summary>
    [DisallowMultipleComponent]
    public class TrackingDirector : MonoBehaviour
    {
        [Header("References (자동 검색)")]
        [SerializeField] private M07Controller m07;
        [Tooltip("활성 차량을 알기 위해 사용. 없으면 모든 Headlights 검색하는 fallback.")]
        [SerializeField] private VehicleDismount dismount;

        [Header("Tuning")]
        [Tooltip("M-07 배터리가 이 퍼센트(0~100) 아래면 jamming 실패로 간주.")]
        [SerializeField, Range(0f, 100f)] private float minBatteryPercent = 0.01f;

        [Header("Debug")]
        [SerializeField] private bool logTransitions = true;

        public bool HeadlightsOn { get; private set; }
        public bool M07Defending { get; private set; } // 살아있고 배터리 > minBatteryPercent
        public bool IsTracking => !M07Defending || HeadlightsOn; // 위에서 정의한 hunted 조건

        private bool _lastHunted;

        void Awake()
        {
            if (m07 == null) m07 = FindAnyObjectByType<M07Controller>(FindObjectsInactive.Include);
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            // M-07이 차 안에서 시작(GameObject inactive)이면 Awake 안 돌아 배터리가 0으로 남음 → 강제 초기화.
            if (m07 != null) m07.EnsureInitialized();
        }

        void Update()
        {
            // M-07이 차에 탑승해 비활성이면 자체 Update가 안 돌므로 드레인이 멈춤.
            // 활성 상태에선 M07Controller.Update가 자체 처리하므로 여기선 비활성일 때만 틱.
            if (m07 != null && !m07.gameObject.activeInHierarchy)
                m07.TickIdleDrain(Time.deltaTime);

            HeadlightsOn = ResolveHeadlightsOn();
            M07Defending = m07 != null && m07.IsAlive && (m07.BatteryPercent * 100f) > minBatteryPercent;

            bool hunted = IsTracking;
            if (hunted != _lastHunted)
            {
                if (logTransitions)
                    Debug.Log($"[TrackingDirector] hunted {_lastHunted} → {hunted}  (headlights={HeadlightsOn}, m07Defending={M07Defending})", this);
                _lastHunted = hunted;
            }

            if (PlayerStatus.IsHunted != hunted)
                PlayerStatus.SetHunted(hunted);
        }

        private bool ResolveHeadlightsOn()
        {
            // 활성 차량의 Headlights 우선
            if (dismount != null && dismount.CurrentCar != null)
            {
                var h = dismount.CurrentCar.GetComponent<Headlights>();
                if (h != null) return h.IsOn;
            }
            // Fallback: 어떤 Headlights든 IsOn = true 인 게 있으면 (활성 차만 ON 상태이긴 함)
            var lights = FindObjectsByType<Headlights>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].IsOn) return true;
            return false;
        }

        [ContextMenu("Print State")]
        private void DebugPrint()
        {
            float b = m07 != null ? m07.BatteryPercent * 100f : -1f;
            Debug.Log($"[TrackingDirector] m07={(m07 != null ? m07.name : "NULL")} battery={b:F1}% alive={(m07 != null ? m07.IsAlive : false)} headlights={HeadlightsOn} → hunted={IsTracking}", this);
        }
    }
}
