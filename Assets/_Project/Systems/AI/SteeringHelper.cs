using UnityEngine;

namespace LastPatrol.Systems.AI
{
    /// <summary>
    /// 단순 장애물 회피 — 원하는 방향(desired) 주변 여러 각도로 raycast 해서 가장 빈 방향을 선택.
    /// NavMesh 도입 전 그레이박스용.
    /// EnemyVehicle, M-07 FollowBehavior 등 공통으로 사용.
    ///
    /// 원리:
    ///   - 후보 방향: desired 자체 + 좌/우 ±20°, ±45°, ±70°
    ///   - 각 방향으로 lookAhead 거리 raycast
    ///   - 점수 = (hit 안 까지 거리 / lookAhead) + (desired와 정렬도 × alignBonus)
    ///   - 점수 가장 높은 방향 반환
    ///
    /// origin이 자기 collider 안에 있으면 자기 자신은 hit 안 됨 (Unity 기본).
    /// </summary>
    public static class SteeringHelper
    {
        private static readonly float[] _angles = { 0f, 20f, -20f, 45f, -45f, 70f, -70f };

        public static Vector3 ResolveDirection(
            Vector3 desiredFlat,
            Vector3 origin,
            float lookAhead,
            LayerMask obstacleMask,
            float alignBonus = 0.4f)
        {
            if (desiredFlat.sqrMagnitude < 1e-4f) return desiredFlat;
            desiredFlat.y = 0f;
            desiredFlat = desiredFlat.normalized;

            Vector3 best = desiredFlat;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _angles.Length; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, _angles[i], 0f) * desiredFlat;
                float dist;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, lookAhead, obstacleMask, QueryTriggerInteraction.Ignore))
                    dist = hit.distance;
                else
                    dist = lookAhead;

                float alignment = Vector3.Dot(dir, desiredFlat); // 1.0 (직선) ~ 0.34 (70°)
                float score = (dist / lookAhead) + alignment * alignBonus;

                if (score > bestScore) { bestScore = score; best = dir; }
            }
            return best;
        }

        /// <summary>전방 단순 막힘 검사 (작은 결정에만).</summary>
        public static bool IsForwardBlocked(Vector3 origin, Vector3 forward, float lookAhead, LayerMask mask)
        {
            return Physics.Raycast(origin, forward, lookAhead, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
