using UnityEngine;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 게임 내 시간 진행. 다른 시스템(HUD, 사건 dispatch, 환경 라이트, 조명 컬러 그라데이션 등)이
    /// 시간 정보를 공유하기 위한 글로벌 상태 컴포넌트.
    ///
    /// v04 mockup: 매 6프레임(60fps 기준)마다 게임 1초 → 실시간 0.1초 = 게임 1초 (10× 가속).
    /// 라플란드 겨울 짧은 낮 (2시간) 분위기 살리기 위한 빠른 시간 흐름.
    ///
    /// 씬 1개에 1개만. 다른 시스템은 FindFirstObjectByType<GameClock>() 또는 SerializeField로 참조.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameClock : MonoBehaviour
    {
        [Header("Start Time")]
        [SerializeField, Range(0, 23)] private int startHour = 7;
        [SerializeField, Range(0, 59)] private int startMinute = 14;
        [SerializeField, Range(0, 59)] private int startSecond = 0;

        [Header("Time Flow")]
        [Tooltip("실시간 N초 동안 게임 내 1초 진행. v04: 0.1초 (10× 빠름).")]
        [SerializeField] private float realSecondsPerGameSecond = 0.1f;
        [SerializeField] private bool paused = false;

        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public int Second { get; private set; }

        /// <summary>전체 게임 초 누적값 (틱 카운터). 사건 dispatch 등에서 경과시간 비교용.</summary>
        public int TotalSeconds => Hour * 3600 + Minute * 60 + Second;

        public string Display => $"{Hour:D2}:{Minute:D2}:{Second:D2}";

        private float _accumulator;

        public void SetPaused(bool value) => paused = value;
        public void SetTimeScale(float realSecPerGameSec) => realSecondsPerGameSecond = Mathf.Max(0.0001f, realSecPerGameSec);

        void Awake()
        {
            Hour = Mathf.Clamp(startHour, 0, 23);
            Minute = Mathf.Clamp(startMinute, 0, 59);
            Second = Mathf.Clamp(startSecond, 0, 59);
            _accumulator = 0f;
        }

        void Update()
        {
            if (paused) return;
            _accumulator += Time.deltaTime;

            // 한 프레임에 여러 게임 초가 흐를 수 있음 (low fps 또는 시간 가속)
            // realSecondsPerGameSecond=0.1, dt=0.0167 → 매 6프레임마다 한 번 Tick.
            int safety = 1000; // 무한 루프 방지
            while (_accumulator >= realSecondsPerGameSecond && safety-- > 0)
            {
                _accumulator -= realSecondsPerGameSecond;
                Tick();
            }
        }

        private void Tick()
        {
            Second++;
            if (Second >= 60) { Second = 0; Minute++; }
            if (Minute >= 60) { Minute = 0; Hour = (Hour + 1) % 24; }
        }
    }
}
