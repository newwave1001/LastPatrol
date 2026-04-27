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
            if (input == null) input = FindFirstObjectByType<InputReader>();
            if (maren == null) maren = FindFirstObjectByType<MarenController>();
            if (m07   == null) m07   = FindFirstObjectByType<M07Controller>();
        }

        void Start()
        {
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
            ApplyMode(Active == ActiveCharacter.Maren ? ActiveCharacter.M07 : ActiveCharacter.Maren);
        }

        public void SetActive(ActiveCharacter who) => ApplyMode(who);

        private void ApplyMode(ActiveCharacter who, bool force = false)
        {
            if (!force && Active == who) return;
            Active = who;

            if (maren != null)
                maren.SetMode(who == ActiveCharacter.Maren ? MarenController.ControlMode.Manual : MarenController.ControlMode.Cower);
            if (m07 != null)
                m07.SetMode(who == ActiveCharacter.M07 ? M07Controller.ControlMode.Manual : M07Controller.ControlMode.Follow);

            OnSwitched?.Invoke(who);
        }

        private void HandleMarenDown()
        {
            if (Active != ActiveCharacter.M07) ApplyMode(ActiveCharacter.M07);
        }
    }
}
