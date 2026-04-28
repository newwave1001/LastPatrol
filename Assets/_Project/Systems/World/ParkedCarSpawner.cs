using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// CityDataSO 도로 옆에 ParkedVehicle prefab을 무작위 동적 스폰. RobotWreckSpawner와 동일 정신.
    /// 플레이어가 [E]로 옮겨 탈 수 있는 차들이 도시 곳곳에 흩어져 있는 효과.
    ///
    /// 보호 규칙:
    ///   - 현재 운전 중인 차는 절대 destroy 안 함
    ///   - 한 번이라도 hijack 된 차는 _hijackedSet에 추가 → despawn 면제 (플레이어 관계 유지)
    ///
    /// 사용:
    ///   1) 빈 GameObject "ParkedCarSpawner" 만들고 이 컴포넌트 부착
    ///   2) city = C_Eirinen.asset / parkedCarPrefab = P_ParkedCar_Temp
    ///   3) ParkedCar prefab의 ParkedVehicle 컴포넌트 startParked=true 권장
    /// </summary>
    [DisallowMultipleComponent]
    public class ParkedCarSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CityDataSO city;
        [Tooltip("스폰할 주차 차량 prefab (CarController + VehicleHealth + ParkedVehicle 부착).")]
        [SerializeField] private GameObject parkedCarPrefab;
        [Tooltip("플레이어 Transform. 비워두면 자동 검색 (CarController).")]
        [SerializeField] private Transform player;
        [Tooltip("현재 운전 중인 차 추적용. 비워두면 자동 검색.")]
        [SerializeField] private VehicleDismount dismount;

        [Header("Spawn Cadence")]
        [SerializeField, Range(1, 12)] private int maxActive = 5;
        [SerializeField] private float checkInterval = 8f;
        [SerializeField] private int initialSpawnCount = 3;

        [Header("Distance")]
        [SerializeField] private float minSpawnDistance = 60f;
        [SerializeField] private float maxSpawnDistance = 280f;
        [Tooltip("도로 중심에서 측면 offset — 차 폭 + 갓길 여유.")]
        [SerializeField] private float roadSideOffset = 2.8f;
        [Tooltip("기존 주차 차량과 이 거리 안이면 스폰 skip.")]
        [SerializeField] private float minSeparation = 14f;
        [Tooltip("플레이어로부터 이 거리 이상이면 자동 despawn (단, 운전·hijack 차는 면제).")]
        [SerializeField] private float despawnDistance = 380f;

        [Header("Road Filter")]
        [Tooltip("이 폭(SVG) 미만 도로 제외 — 좁은 골목. 권장 3 (도시 master 도로 폭 대부분 4~10).")]
        [SerializeField] private float minRoadWidthSVG = 3f;
        [Tooltip("이 폭(SVG) 초과 도로 제외 — 대형 하이웨이. 권장 14.")]
        [SerializeField] private float maxRoadWidthSVG = 14f;

        [Header("Placement")]
        [Tooltip("스폰 시 y 좌표 offset (prefab origin 위치에 따라 조정 — 차 바닥이 지면에 닿도록).")]
        [SerializeField] private float spawnBaseY = 0.5f;

        [Header("Obstacle Avoidance")]
        [Tooltip("CityDataSO obstacleLayerName 자동 매핑. 0이면 검사 skip.")]
        [SerializeField] private LayerMask obstacleLayer;
        [Tooltip("스폰 위치 OverlapBox 절반 크기 — 차 footprint (~ 1m × 0.7m × 2m).")]
        [SerializeField] private Vector3 obstacleCheckHalfExtents = new Vector3(1.0f, 0.7f, 2.0f);

        private readonly List<GameObject> _active = new List<GameObject>();
        private readonly HashSet<GameObject> _hijackedSet = new HashSet<GameObject>();
        private float _nextCheckTime;

        public int ActiveCount => _active.Count;

        void Awake()
        {
            ResolvePlayerIfNeeded();
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            ResolveObstacleLayer();
        }

        // CityDataSO.obstacleLayerName 기반 자동 매핑.
        private void ResolveObstacleLayer()
        {
            if (obstacleLayer != 0) return;
            if (city == null) return;
            int idx = LayerMask.NameToLayer(city.obstacleLayerName);
            if (idx >= 0) obstacleLayer = 1 << idx;
        }

        // 모든 콜라이더 검사 후 Ground/Road/이미 자체 스폰한 차들은 제외.
        // Obstacle 레이어 매핑 실패해도 작동하는 fallback.
        private static readonly Collider[] _spawnCheckBuf = new Collider[16];
        private bool IsAreaBlocked(Vector3 pos, Vector3 halfExtents, Quaternion rot)
        {
            Vector3 center = pos + Vector3.up * halfExtents.y;
            int mask = obstacleLayer != 0 ? (int)obstacleLayer : ~0;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _spawnCheckBuf, rot, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = _spawnCheckBuf[i];
                if (c == null) continue;
                string n = c.gameObject.name;
                if (n == "Ground") continue;
                if (n.StartsWith("Road_")) continue;
                return true;
            }
            return false;
        }

        // 인스펙터 우클릭 → 권장 roadWidth 일괄 재적용
        [ContextMenu("Apply Recommended Road Width")]
        private void ApplyRecommendedRoadWidth()
        {
            minRoadWidthSVG = 3f;
            maxRoadWidthSVG = 14f;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            Debug.Log($"[ParkedCarSpawner] 권장 roadWidth 적용: min={minRoadWidthSVG}, max={maxRoadWidthSVG}", this);
        }

        void Start()
        {
            if (city == null)
                Debug.LogError("[ParkedCarSpawner] city 슬롯 비어있음. C_Eirinen.asset 드래그 필요.", this);
            if (parkedCarPrefab == null)
                Debug.LogError("[ParkedCarSpawner] parkedCarPrefab 슬롯 비어있음. P_ParkedCar_Temp 드래그 필요.", this);

            // 옛 직렬화 값 자동 보정 — 5/12 → 3/14 (sampling 후보 늘림).
            if (Mathf.Approximately(minRoadWidthSVG, 5f) && Mathf.Approximately(maxRoadWidthSVG, 12f))
            {
                Debug.LogWarning("[ParkedCarSpawner] 옛 roadWidth 필터(5/12) 검출 → 권장(3/14)으로 자동 보정. 씬 저장 권장.", this);
                minRoadWidthSVG = 3f;
                maxRoadWidthSVG = 14f;
            }

            ResolvePlayerIfNeeded();

            int spawned = 0;
            for (int i = 0; i < initialSpawnCount; i++)
                if (TrySpawn()) spawned++;
            Debug.Log($"[ParkedCarSpawner] Start: initial spawn {spawned}/{initialSpawnCount}, active={ActiveCount}/{maxActive}", this);
            _nextCheckTime = Time.time + checkInterval;
        }

        private void ResolvePlayerIfNeeded()
        {
            if (player != null) return;
            var car = FindAnyObjectByType<CarController>(FindObjectsInactive.Include);
            if (car != null) player = car.transform;
        }

        void Update()
        {
            // hijack 추적 — 우리가 스폰한 차가 현재 운전 차가 되면 면제 set에 추가
            if (dismount != null && dismount.CurrentCar != null)
            {
                var cur = dismount.CurrentCar.gameObject;
                if (_active.Contains(cur)) _hijackedSet.Add(cur);
            }

            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + checkInterval;

            CleanupActive();
            DespawnDistant();
            if (_active.Count < maxActive) TrySpawn();
        }

        private void CleanupActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null) _active.RemoveAt(i);
        }

        private void DespawnDistant()
        {
            if (player == null) return;
            GameObject currentCar = (dismount != null && dismount.CurrentCar != null) ? dismount.CurrentCar.gameObject : null;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var car = _active[i];
                if (car == null) { _active.RemoveAt(i); continue; }
                if (car == currentCar) continue;        // 운전 중 보호
                if (_hijackedSet.Contains(car)) continue; // hijack 된 차 보호

                float d = Vector3.Distance(car.transform.position, player.position);
                if (d > despawnDistance)
                {
                    Destroy(car);
                    _active.RemoveAt(i);
                }
            }
        }

        private bool TrySpawn()
        {
            if (city == null || city.roads == null || city.roads.Count == 0) return false;
            if (parkedCarPrefab == null) return false;
            ResolvePlayerIfNeeded();
            if (player == null) return false;

            int rRoadWidth = 0, rTooClose = 0, rTooFar = 0, rNearOther = 0, rOnBuilding = 0;
            ResolveObstacleLayer();

            for (int attempt = 0; attempt < 30; attempt++)
            {
                var road = city.roads[Random.Range(0, city.roads.Count)];
                if (road == null) continue;
                if (road.widthSVG < minRoadWidthSVG || road.widthSVG > maxRoadWidthSVG) { rRoadWidth++; continue; }

                Vector3 a = city.SvgToUnity(road.svgFromXY);
                Vector3 b = city.SvgToUnity(road.svgToXY);
                a.y = 0f; b.y = 0f;

                float t = Random.Range(0.2f, 0.8f); // 교차로 회피
                Vector3 onRoad = Vector3.Lerp(a, b, t);

                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector3 perp = Vector3.Cross(Vector3.up, dir);
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 spawnPos = onRoad + perp * (roadSideOffset * side);
                spawnPos.y = spawnBaseY;

                float dToPlayer = Vector3.Distance(spawnPos, player.position);
                if (dToPlayer < minSpawnDistance) { rTooClose++; continue; }
                if (dToPlayer > maxSpawnDistance) { rTooFar++; continue; }

                bool tooClose = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i] == null) continue;
                    if (Vector3.Distance(_active[i].transform.position, spawnPos) < minSeparation)
                    { tooClose = true; break; }
                }
                if (tooClose) { rNearOther++; continue; }

                // 차량 진행 방향 — 도로 따라 양쪽 무작위 (자연스러움)
                Vector3 facingDir = Random.value < 0.5f ? dir : -dir;
                Quaternion rot = Quaternion.LookRotation(facingDir, Vector3.up);

                // 건물 위/안 회피 — Obstacle 레이어 우선, 없으면 name 기반 fallback
                if (IsAreaBlocked(spawnPos, obstacleCheckHalfExtents, rot))
                { rOnBuilding++; continue; }

                var car = Instantiate(parkedCarPrefab, spawnPos, rot);
                car.name = $"ParkedCar_Wild_{Time.frameCount}";
                // 스폰 차량은 headlights OFF (활성 차만 ON 규칙)
                var lights = car.GetComponentInChildren<Headlights>(true);
                if (lights != null) lights.SetOn(false);
                _active.Add(car);
                return true;
            }

            Debug.LogWarning($"[ParkedCarSpawner] 30 attempts failed. " +
                $"rejected: roadWidth={rRoadWidth} tooClose={rTooClose} tooFar={rTooFar} nearOther={rNearOther} onBuilding={rOnBuilding}. " +
                $"playerPos={player.position}", this);
            return false;
        }

        // ---- Debug ----

        [ContextMenu("Force Spawn Now")]
        private void DebugForceSpawn()
        {
            bool ok = TrySpawn();
            Debug.Log($"[ParkedCarSpawner] Force spawn: {(ok ? "OK" : "FAIL")} — active={ActiveCount}/{maxActive}");
        }

        [ContextMenu("Clear All Spawned (운전·hijack 차 제외)")]
        private void DebugClear()
        {
            GameObject currentCar = (dismount != null && dismount.CurrentCar != null) ? dismount.CurrentCar.gameObject : null;
            int removed = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var car = _active[i];
                if (car == null) { _active.RemoveAt(i); continue; }
                if (car == currentCar) continue;
                if (_hijackedSet.Contains(car)) continue;
                Destroy(car);
                _active.RemoveAt(i);
                removed++;
            }
            Debug.Log($"[ParkedCarSpawner] Cleared {removed}, remaining(보호)={_active.Count}");
        }

        [ContextMenu("Print State")]
        private void DebugPrint()
        {
            Debug.Log($"[ParkedCarSpawner] " +
                $"city={(city != null ? city.name : "NULL")} " +
                $"player={(player != null ? player.name : "NULL")} " +
                $"prefab={(parkedCarPrefab != null ? parkedCarPrefab.name : "NULL")} " +
                $"active={ActiveCount}/{maxActive} hijacked={_hijackedSet.Count}", this);
        }
    }
}
