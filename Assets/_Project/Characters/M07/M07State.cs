using UnityEngine;

namespace LastPatrol.Characters.M07
{
    /// <summary>
    /// M-07 상태 (배터리/HP/해킹) 정적 영속 저장소.
    /// 씬 전환 시 GameObject 재생성되어도 값 유지. (BatteryInventory / ActiveCase 와 같은 패턴.)
    ///
    /// M07Controller가 Awake/EnsureInitialized 시 여기서 읽고,
    /// 변경 시마다 Save()로 다시 동기화.
    /// </summary>
    public static class M07State
    {
        public static bool HasState { get; private set; }

        public static float Battery { get; private set; }
        public static float HP      { get; private set; }
        public static bool  IsHacked { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            HasState = false;
            Battery = 0f;
            HP = 0f;
            IsHacked = false;
        }

        public static void Save(float battery, float hp, bool hacked)
        {
            Battery = battery;
            HP = hp;
            IsHacked = hacked;
            HasState = true;
        }

        public static void Reset()
        {
            HasState = false;
            Battery = 0f;
            HP = 0f;
            IsHacked = false;
        }
    }
}
