using System;
using LastPatrol.Data;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 현재 진행 중인 사건의 글로벌 컨텍스트.
    /// 외부 운전 씬(S01)에서 사건 도착 → 실내 수사 씬(S03)으로 넘어갈 때 어떤 사건인지 전달.
    ///
    /// 정적 클래스라 SceneManager.LoadScene 후에도 자동으로 살아남음 (런타임 메모리).
    /// 다만 게임 종료/도메인 리로드 시 초기화. 영속 저장은 별도 SaveSystem 책임.
    ///
    /// 사용:
    ///   ActiveCase.SetCurrent(marker.CaseData);
    ///   var c = ActiveCase.Current;          // CaseDataSO 또는 null
    ///   ActiveCase.Clear();                  // 사건 종료 시
    ///   ActiveCase.OnChanged += case => ...; // 변경 알림
    /// </summary>
    public static class ActiveCase
    {
        public static CaseDataSO Current { get; private set; }
        public static bool HasCase => Current != null;

        public static event Action<CaseDataSO> OnChanged;

        public static void SetCurrent(CaseDataSO data)
        {
            if (Current == data) return;
            Current = data;
            OnChanged?.Invoke(Current);
        }

        public static void Clear()
        {
            if (Current == null) return;
            Current = null;
            OnChanged?.Invoke(null);
        }

#if UNITY_EDITOR
        // 도메인 리로드/플레이 모드 전환 시 정적 상태 리셋. 에디터에서만.
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode()
        {
            Current = null;
            OnChanged = null;
        }
#endif
    }
}
