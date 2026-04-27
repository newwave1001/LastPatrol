using System;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 마렌의 글로벌 진행 상태. 사건 완료 카운터, 추격 대상 여부 등.
    /// 정적 클래스라 SceneManager.LoadScene 후에도 살아남음. 게임 종료/도메인 리로드 시 초기화.
    /// 영속 저장은 별도 SaveSystem 책임.
    ///
    /// 디자인 (Week 6 추격 도입):
    ///   - 사건 완료 직후 IsHunted = true → 다음 사이클부터 EncounterSpawner가 적 스폰.
    ///   - 첫 사건 전엔 IsHunted = false → 평화로운 도시 운전 + dispatch만.
    ///   - 누아르 톤: "진실을 알게 된 자가 표적이 된다."
    /// </summary>
    public static class PlayerStatus
    {
        public static bool IsHunted { get; private set; }
        public static int CompletedCases { get; private set; }

        public static event Action OnHuntedChanged;

        public static void SetHunted(bool value)
        {
            if (IsHunted == value) return;
            IsHunted = value;
            OnHuntedChanged?.Invoke();
        }

        public static void NotifyCaseCompleted()
        {
            CompletedCases++;
        }

        public static void Reset()
        {
            IsHunted = false;
            CompletedCases = 0;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode()
        {
            IsHunted = false;
            CompletedCases = 0;
            OnHuntedChanged = null;
        }
#endif
    }
}
