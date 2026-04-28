using System;
using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Core.Input;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 마렌과 M-07의 활성 캐릭터 토글. Tab(InputReader.OnSwitchPressed) 입력 처리.
    ///
    /// 활성 = Maren:
    ///   - MarenController.Manual (직접 조종)
    ///   - M07Controller.Follow   (마렌 호위)
    /// 활성 = M07:
    ///   - MarenController.Cower (M-07 근처로 자동 이동, 피신)
    ///   - M07Controller.Manual  (직접 조종)
    ///
    /// 보통 S03 같은 Foot 모드 씬에 빈 GameObject로 두면 Awake에서 자동 검색.
    /// 외부 운전(S01) 씬에선 의미 없음 — 마렌이 차량 운전이므로.
    /// </summary>
    [DisallowMultipleComponent]
    public class PartyController : MonoBehaviour
    {
        public enum ActiveCharacter { Maren, M07 }

        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private MarenController maren;
        [SerializeField] private M07Controller m07;

        [Header("Settings")]
        [SerializeField] private ActiveCharacter startWith = ActiveCharacter.Maren;
        [Tooltip("마렌 사망 시 자동으로 M-07로 전환할지 (사망 처리는 별개).")]
        [SerializeField] private bool autoSwitchOnMarenDown = true;

        public ActiveCharacter Active { get; private set; }
        public event Action<ActiveCharacter> OnSwitched;

        void Awake()
        {
            if (input == null) input = FindAnyObjectByType<InputReader>();
            if (maren == null) maren = FindAnyObjectByType<MarenController>();
            if (m07   == null) m07   = FindAnyObjectByType<M07Controller>();
        }

        void Start()
        {
            // 전투 중엔 외부(VehicleDismount)가 Active 설정 후. 자동 적용 X — 안 그럼 Flee 덮어씀.
            if (CombatStatus.InCombat) return;
            ApplyMode(startWith, force: true);
        }

        void OnEnable()
        {
            if (input != null) input.OnSwitchPressed += Toggle;
            if (autoSwitchOnMarenDown && maren != null) maren.OnDied += HandleMarenDown;
        }

        void OnDisable()
        {
            if (input != null) input.OnSwitchPressed -= Toggle;
            if (maren != null) maren.OnDied -= HandleMarenDown;
        }

        public void Toggle()
        {
            Debug.Log($"[Party] Tab pressed. Active before={Active}");
            ApplyMode(Active == ActiveCharacter.Maren ? ActiveCharacter.M07 : ActiveCharacter.Maren);
        }

        // 전투 중 자동 모드 보정 — combat 시작 또는 휴머노이드 spawn 후에도 Active=M07이면 마렌 Flee 강제.
        // (Tab 안 눌렀어도 combat 시작 시 자동 도주)
        void Update()
        {
            if (!CombatStatus.InCombat) return;
            if (Active == ActiveCharacter.M07 && maren != null
                && maren.CurrentMode != MarenController.ControlMode.Flee
                && maren.IsAlive)
            {
                maren.SetMode(MarenController.ControlMode.Flee);
            }
        }

        public void SetActive(ActiveCharacter who) => ApplyMode(who);

        private void ApplyMode(ActiveCharacter who, bool force = false)
        {
            if (!force && Active == who) return;
            Active = who;

            bool inCombat = CombatStatus.InCombat;

            // 마렌 모드 결정 — 활성이면 Manual, 비활성+전투면 Flee, 비활성+평시면 Cower
            if (maren != null)
            {
                MarenController.ControlMode marenMode;
                if (who == ActiveCharacter.Maren) marenMode = MarenController.ControlMode.Manual;
                else if (inCombat)               marenMode = MarenController.ControlMode.Flee;
                else                              marenMode = MarenController.ControlMode.Cower;
                maren.SetMode(marenMode);
            }

            // M-07 모드 — 활성이면 Manual, 비활성이면 Follow. Hold는 안 씀 (직접 조종 모델).
            if (m07 != null)
            {
                m07.SetMode(who == ActiveCharacter.M07 ? M07Controller.ControlMode.Manual : M07Controller.ControlMode.Follow);
                m07.SetHold(false);
            }

            Debug.Log($"[Party] ApplyMode → {who} (combat={inCombat}, marenMode={(maren!=null?maren.CurrentMode.ToString():"null")}, m07Mode={(m07!=null?m07.CurrentMode.ToString():"null")})");
            OnSwitched?.Invoke(who);
        }

        private void HandleMarenDown()
        {
            if (Active != ActiveCharacter.M07) ApplyMode(ActiveCharacter.M07);
        }
    }
}
