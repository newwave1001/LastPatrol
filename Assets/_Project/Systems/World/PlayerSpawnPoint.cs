using UnityEngine;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 다음 씬 로드 시 마렌 차(Player)의 시작 위치를 미리 박아두는 글로벌 컨텍스트.
    /// ActiveCase처럼 정적이라 SceneManager.LoadScene 후에도 살아남음.
    ///
    /// 보통 흐름:
    ///   - DispatchSystem이 S03 진입 직전 SetFromCar(car) 호출 → "내가 내린 자리" 기록.
    ///   - S01 로드 후 CarController.Start가 ConsumeIfAny() 호출 → 그 위치로 텔레포트.
    /// </summary>
    public static class PlayerSpawnPoint
    {
        public static bool HasOverride { get; private set; }
        public static Vector3 Position { get; private set; }
        public static Quaternion Rotation { get; private set; }

        public static void Set(Vector3 pos, Quaternion rot)
        {
            Position = pos;
            Rotation = rot;
            HasOverride = true;
        }

        /// <summary>차량 transform 기준으로 기록 (가장 흔한 케이스).</summary>
        public static void SetFromTransform(Transform t)
        {
            if (t == null) return;
            Set(t.position, t.rotation);
        }

        /// <summary>한 번 소비. 다음 씬 진입 후 CarController.Start에서 호출하고 자동 클리어.</summary>
        public static bool ConsumeIfAny(out Vector3 pos, out Quaternion rot)
        {
            if (!HasOverride) { pos = default; rot = Quaternion.identity; return false; }
            pos = Position;
            rot = Rotation;
            HasOverride = false;
            return true;
        }

        public static void Clear() { HasOverride = false; }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode() => HasOverride = false;
#endif
    }
}
