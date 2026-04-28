using UnityEngine;
using LastPatrol.Systems.Encounter;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 외부 전투 상태 도우미.
    ///
    /// 정의 (재정의 v2):
    ///   - 적(OutdoorHumanoid/Drone/EnemyVehicle)이 카메라 viewport 안으로 들어와 보인 순간
    ///     "engaged" latch ON.
    ///   - latch ON 동안 살아있는 적이 하나라도 있으면 InCombat = true.
    ///   - 모든 적 사망/despawn → latch OFF, InCombat = false.
    ///
    /// 의도: 적 화면 등장 = 전투 시작 (카메라 zoom, BGM, HUD toast 모두 이 시점부터).
    /// 적이 잠깐 화면 밖으로 나가도 latch 유지 → 전투 BGM 끊김 방지.
    ///
    /// 정적 — Update 폴링 없이 호출 시점 검사.
    /// </summary>
    public static class CombatStatus
    {
        // 한번이라도 적이 시야 안으로 들어오면 true. 모든 적 사라지면 false.
        private static bool _engaged;

        // 마지막 검사 프레임 — 같은 프레임 내 다회 호출 시 재계산 회피.
        private static int _lastCheckFrame = -1;
        private static bool _cachedInCombat;

        public static bool InCombat
        {
            get
            {
                int frame = Time.frameCount;
                if (frame == _lastCheckFrame) return _cachedInCombat;
                _lastCheckFrame = frame;

                bool anyAlive = false;
                bool anyVisible = false;
                string firstVisibleName = null;
                Camera cam = ResolveCamera();

                // OutdoorHumanoid
                var humanoids = Object.FindObjectsByType<OutdoorHumanoid>(FindObjectsInactive.Exclude);
                for (int i = 0; i < humanoids.Length; i++)
                {
                    var h = humanoids[i];
                    if (h == null || !h.IsAlive || !h.IsEnemy) continue;
                    anyAlive = true;
                    if (cam != null && IsOnScreen(cam, h.transform.position))
                    {
                        anyVisible = true;
                        if (firstVisibleName == null) firstVisibleName = $"Humanoid({h.name})";
                    }
                }

                // Drone
                var drones = Object.FindObjectsByType<Drone>(FindObjectsInactive.Exclude);
                for (int i = 0; i < drones.Length; i++)
                {
                    var d = drones[i];
                    if (d == null || !d.IsAlive) continue;
                    anyAlive = true;
                    if (cam != null && IsOnScreen(cam, d.transform.position))
                    {
                        anyVisible = true;
                        if (firstVisibleName == null) firstVisibleName = $"Drone({d.name})";
                    }
                }

                // EnemyVehicle — 추격 단계에서 시야 안으로 들어왔을 때부터 인카운터.
                // VehicleHealth.IsEnemy가 true인 차만 (AMBUSH 후 false 전환된 차 제외).
                var enemyVehicles = Object.FindObjectsByType<EnemyVehicle>(FindObjectsInactive.Exclude);
                for (int i = 0; i < enemyVehicles.Length; i++)
                {
                    var ev = enemyVehicles[i];
                    if (ev == null) continue;
                    var vh = ev.GetComponent<VehicleHealth>();
                    if (vh == null || !vh.IsAlive || !vh.IsEnemy) continue;
                    anyAlive = true;
                    if (cam != null && IsOnScreen(cam, ev.transform.position))
                    {
                        anyVisible = true;
                        if (firstVisibleName == null) firstVisibleName = $"EnemyVehicle({ev.name})";
                    }
                }

                // Latch 전환 진단 로그
                bool prevEngaged = _engaged;
                if (anyVisible) _engaged = true;
                if (!anyAlive)  _engaged = false;
                if (_engaged != prevEngaged)
                {
                    if (_engaged)
                        Debug.Log($"[CombatStatus] ENGAGED — first visible: {firstVisibleName}");
                    else
                        Debug.Log("[CombatStatus] disengaged — all enemies dead/despawned");
                }

                _cachedInCombat = _engaged && anyAlive;
                return _cachedInCombat;
            }
        }

        private static bool IsOnScreen(Camera cam, Vector3 worldPos)
        {
            // 약간의 버퍼 — 화면 가장자리 살짝 밖도 visible로 간주 (직부감에서 ~10% margin).
            const float margin = 0.1f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f
                && vp.x >= -margin && vp.x <= 1f + margin
                && vp.y >= -margin && vp.y <= 1f + margin;
        }

        private static Camera _cachedCam;
        private static int _camResolveFrame = -1;

        private static Camera ResolveCamera()
        {
            int frame = Time.frameCount;
            if (_cachedCam != null && _cachedCam.gameObject.activeInHierarchy && frame == _camResolveFrame) return _cachedCam;
            _camResolveFrame = frame;
            _cachedCam = Camera.main;
            if (_cachedCam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                    {
                        _cachedCam = cams[i];
                        break;
                    }
                }
            }
            return _cachedCam;
        }

        /// <summary>외부 강제 reset — Game Over 재시작 등.</summary>
        public static void ResetEngagement()
        {
            _engaged = false;
            _lastCheckFrame = -1;
            _cachedInCombat = false;
        }
    }
}
