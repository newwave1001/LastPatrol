using System;
using UnityEngine;

namespace LastPatrol.Systems.Battery
{
    /// <summary>
    /// 마렌의 배터리 충전통 — 0~3개 글로벌 상태. 씬 전환 시 보존(static).
    ///
    /// 한 배터리 = M-07 30% 충전. 따라서 풀 충전통 3개 = 90% 충전 가능.
    ///
    /// 사용:
    ///   BatteryInventory.Add(1)            → 폐로봇 루팅 시
    ///   BatteryInventory.TryConsume(1)     → R 충전 시
    ///   BatteryInventory.OnChanged         → UI 갱신용 이벤트
    /// </summary>
    public static class BatteryInventory
    {
        public const int Max = 3;

        public static int Count { get; private set; }
        public static event Action OnChanged;

        public static bool IsFull  => Count >= Max;
        public static bool IsEmpty => Count <= 0;

        // 에디터 Play 시작 시 정적 상태 리셋 — Domain Reload disabled 환경 대응.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Reset();

        /// <summary>Game Over 재시작 시 호출 — Count 초기화. OnChanged 구독자는 유지(UI 등 라이브 listener).</summary>
        public static void Reset()
        {
            Count = 0;
            OnChanged?.Invoke();
        }

        /// <summary>주울 수 있는 만큼 추가. 반환값은 실제로 추가된 개수.</summary>
        public static int Add(int n = 1)
        {
            if (n <= 0) return 0;
            int actual = Mathf.Min(n, Max - Count);
            if (actual <= 0) return 0;
            Count += actual;
            OnChanged?.Invoke();
            return actual;
        }

        /// <summary>요청 개수만큼 모두 있어야 소모. 부족하면 false 반환 + 변화 없음.</summary>
        public static bool TryConsume(int n = 1)
        {
            if (n <= 0) return false;
            if (Count < n) return false;
            Count -= n;
            OnChanged?.Invoke();
            return true;
        }

        // 디버그/세이브 복원용
        public static void DebugSet(int value)
        {
            Count = Mathf.Clamp(value, 0, Max);
            OnChanged?.Invoke();
        }
    }
}
