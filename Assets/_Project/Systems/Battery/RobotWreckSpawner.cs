using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.Systems.Battery
{
    /// <summary>
    /// CityDataSO의 도로 segment를 따라 폐로봇을 무작위로 길가에 스폰.
    /// 화면 밖에서만 스폰(플레이어와 minSpawnDistance 이상), 너무 멀면 despawn.
    /// 동시 active 개수 제한 (maxActive).
    ///
    /// 사용:
    ///   1) 빈 GameObject "RobotWreckSpawner" 만들고 이 컴포넌트 부착
    ///   2) city 슬롯에 C_Eirinen.asset
    ///   3) lootPrefab은 비워둬도 됨(LootableRobot 자동 빌드) — 폴리싱 단계에서 prefab으로 교체
    /// </summary>
    [DisallowMultipleComponent]
    public class RobotWreckSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CityDataSO city;
        [SerializeField] private LootableRobot lootPrefab;
        [Tooltip("플레이어 Transform. 비워두면 Awake에서 CarController 자동 검색.")]
        [SerializeField] private Transform player;

        [Header("Spawn Cadence")]
        [Tooltip("동시 active 폐로봇 최대.")]
        [SerializeField, Range(1, 10)] private int maxActive = 4;
        [Tooltip("스폰 시도 주기(초).")]
        [SerializeField] private float checkInterval = 6f;
        [Tooltip("씬 시작 시 즉시 N개 스폰 시도.")]
        [SerializeField] private int initialSpawnCount = 2;

        [Header("Distance")]
        [Tooltip("플레이어로부터 이 거리 이상에서만 스폰 (화면 밖). " +
                 "AMBUSH 등 전투 위치에서 wreck이 안 보이도록 충분히 크게. " +
                 "권장값: 90. 우클릭 → 'Apply Recommended Distances' 로 일괄 설정.")]
        [SerializeField] private float minSpawnDistance = 90f;
        [Tooltip("이 거리 초과면 스폰 안 함 (너무 멀면 발견 못 함). 권장값: 450.")]
        [SerializeField] private float maxSpawnDistance = 450f;
        [Tooltip("도로 중심에서 측면 offset (도로 옆에 위치).")]
        [SerializeField] private float roadSideOffset = 4.5f;
        [Tooltip("기존 wreck과 이 거리 안이면 스폰 skip.")]
        [SerializeField] private float minWreckSeparation = 25f;
        [Tooltip("플레이어로부터 이 거리 이상이면 자동 despawn.")]
        [SerializeField] private float despawnDistance = 380f;

        [Header("Road Filter")]
        [Tooltip("이 폭(SVG) 미만인 도로(좁은 골목)는 스폰에서 제외.")]
        [SerializeField] private float minRoadWidthSVG = 4f;

        [Header("Obstacle Avoidance")]
        [Tooltip("CityDataSO obstacleLayerName 자동 매핑. 0이면 검사 skip.")]
        [SerializeField] private LayerMask obstacleLayer;
        [Tooltip("스폰 위치 OverlapBox 절반 크기 — 폐로봇 footprint.")]
        [SerializeField] private Vector3 obstacleCheckHalfExtents = new Vector3(0.6f, 0.8f, 0.6f);

        private readonly List<LootableRobot> _active = new List<LootableRobot>();
        private float _nextCheckTime;

        public int ActiveCount => _active.Count;

        void Awake()
        {
            ResolvePlayerIfNeeded();
            ResolveObstacleLayer();
        }

        // CityDataSO.obstacleLayerName 기반 자동 매핑. 인스펙터에서 직접 설정해도 OK.
        private void ResolveObstacleLayer()
        {
            if (obstacleLayer != 0) return; // 인스펙터에서 이미 설정됨
            if (city == null) return;
            int idx = LayerMask.NameToLayer(city.obstacleLayerName);
            if (idx >= 0) obstacleLayer = 1 << idx;
        }

        // 모든 콜라이더 검사 후 Ground/Road 제외 → 그 외(건물·차·다른 wreck)면 차단.
        // Obstacle 레이어 매핑이 안 돼 있어도 작동하도록 fallback.
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
                if (n.StartsWith("Road_")) continue; // 도로는 콜라이더 제거됐지만 안전망
                return true;
            }
            return false;
        }

        // 인스펙터 우클릭 → 'Apply Recommended Distances' 실행 시 권장값 일괄 재적용.
        // (씬에 직렬화된 옛 값을 코드 신규 기본값으로 강제 sync)
        [ContextMenu("Apply Recommended Distances")]
        private void ApplyRecommendedDistances()
        {
            minSpawnDistance = 90f;
            maxSpawnDistance = 450f;
            despawnDistance  = 500f;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            Debug.Log($"[RobotWreckSpawner] 권장 거리 재적용: minSpawn={minSpawnDistance}, maxSpawn={maxSpawnDistance}, despawn={despawnDistance}", this);
        }

        void Start()
        {
            // 셋업 검증 — 누락 시 한 번 명확히 경고.
            if (city == null)
                Debug.LogError("[RobotWreckSpawner] city 슬롯이 비어 있음. 인스펙터에서 C_Eirinen.asset 드래그.", this);
            if (city != null && (city.roads == null || city.roads.Count == 0))
                Debug.LogError("[RobotWreckSpawner] city 데이터에 roads가 비어있음. CityDataSO Fill Full Map 실행 필요.", this);

            // 직렬화된 옛 값 자동 보정 — 150/280 같은 옛 기본값이면 권장값으로 sync
            if (Mathf.Approximately(minSpawnDistance, 150f) && Mathf.Approximately(maxSpawnDistance, 280f))
            {
                Debug.LogWarning("[RobotWreckSpawner] 옛 직렬화 값(150/280) 검출 → 권장값(90/450)으로 자동 보정. 씬 저장 권장.", this);
                minSpawnDistance = 90f;
                maxSpawnDistance = 450f;
            }

            ResolvePlayerIfNeeded();
            if (player == null)
                Debug.LogWarning("[RobotWreckSpawner] player(CarController) 못 찾음. Awake/Start 시점에 차량 prefab이 씬에 있어야 함.", this);

            int spawned = 0;
            for (int i = 0; i < initialSpawnCount; i++)
                if (TrySpawn()) spawned++;
            Debug.Log($"[RobotWreckSpawner] Start: initial spawn {spawned}/{initialSpawnCount}, active={ActiveCount}/{maxActive}", this);
            _nextCheckTime = Time.time + checkInterval;
        }

        private void ResolvePlayerIfNeeded()
        {
            if (player != null) return;
            var car = FindAnyObjectByType<LastPatrol.Systems.Vehicle.CarController>(FindObjectsInactive.Include);
            if (car != null) player = car.transform;
        }

        void Update()
        {
            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + checkInterval;

            CleanupActive();
            DespawnDistant();

            if (_active.Count < maxActive)
                TrySpawn();
        }

        private void CleanupActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null) _active.RemoveAt(i);
        }

        private void DespawnDistant()
        {
            if (player == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var w = _active[i];
                if (w == null) { _active.RemoveAt(i); continue; }
                if (w.IsLooted) continue; // 루팅된 wreck은 lootedDestroyDelay에 의해 자체 정리
                float d = Vector3.Distance(w.transform.position, player.position);
                if (d > despawnDistance)
                {
                    Destroy(w.gameObject);
                    _active.RemoveAt(i);
                }
            }
        }

        private bool TrySpawn()
        {
            if (city == null || city.roads == null || city.roads.Count == 0) return false;
            ResolvePlayerIfNeeded();
            if (player == null) return false;

            int rejectedRoadWidth = 0;
            int rejectedTooClose = 0;
            int rejectedTooFar = 0;
            int rejectedNearWreck = 0;
            int rejectedOnBuilding = 0;

            ResolveObstacleLayer();

            // 30회까지 후보 도로/위치 재시도
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var road = city.roads[Random.Range(0, city.roads.Count)];
                if (road == null) continue;
                if (road.widthSVG < minRoadWidthSVG) { rejectedRoadWidth++; continue; }

                Vector3 a = city.SvgToUnity(road.svgFromXY);
                Vector3 b = city.SvgToUnity(road.svgToXY);
                a.y = 0.01f; b.y = 0.01f;

                float t = Random.Range(0.15f, 0.85f); // 도로 끝(교차로) 회피
                Vector3 onRoad = Vector3.Lerp(a, b, t);

                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector3 perp = Vector3.Cross(Vector3.up, dir);
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 spawnPos = onRoad + perp * (roadSideOffset * side);
                spawnPos.y = 0.01f;

                // 거리 체크
                float dToPlayer = Vector3.Distance(spawnPos, player.position);
                if (dToPlayer < minSpawnDistance) { rejectedTooClose++; continue; }
                if (dToPlayer > maxSpawnDistance) { rejectedTooFar++; continue; }

                // 기존 wreck 거리 체크
                bool tooClose = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i] == null) continue;
                    if (Vector3.Distance(_active[i].transform.position, spawnPos) < minWreckSeparation)
                    { tooClose = true; break; }
                }
                if (tooClose) { rejectedNearWreck++; continue; }

                // 건물 위/안 회피 — Obstacle 레이어 우선, 없으면 name 기반 fallback
                if (IsAreaBlocked(spawnPos, obstacleCheckHalfExtents, Quaternion.identity))
                { rejectedOnBuilding++; continue; }

                // 스폰
                Quaternion rot = Quaternion.LookRotation(perp * side);
                LootableRobot wreck;
                if (lootPrefab != null)
                {
                    wreck = Instantiate(lootPrefab, spawnPos, rot);
                }
                else
                {
                    var go = new GameObject("LootableRobot_Wild");
                    go.transform.position = spawnPos;
                    go.transform.rotation = rot;
                    wreck = go.AddComponent<LootableRobot>();
                }
                _active.Add(wreck);
                return true;
            }
            // 30회 모두 실패 — 원인 표시.
            Debug.LogWarning($"[RobotWreckSpawner] 30 attempts failed. " +
                $"rejected: roadWidth={rejectedRoadWidth} tooClose={rejectedTooClose} tooFar={rejectedTooFar} nearWreck={rejectedNearWreck} onBuilding={rejectedOnBuilding}. " +
                $"playerPos={player.position}, minSpawn={minSpawnDistance}, maxSpawn={maxSpawnDistance}", this);
            return false;
        }

        [ContextMenu("Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"[RobotWreckSpawner] " +
                $"city={(city != null ? city.name : "NULL")} " +
                $"player={(player != null ? player.name : "NULL")} " +
                $"prefab={(lootPrefab != null ? lootPrefab.name : "null(auto)")} " +
                $"active={ActiveCount}/{maxActive} " +
                $"roads={(city != null && city.roads != null ? city.roads.Count : 0)}", this);
            if (player != null)
                Debug.Log($"  player position={player.position}", this);
        }

        // ---- Debug ----

        [ContextMenu("Force Spawn Now")]
        private void DebugForceSpawn()
        {
            bool ok = TrySpawn();
            Debug.Log($"[RobotWreckSpawner] Force spawn: {(ok ? "OK" : "FAIL — 후보 자리 없음")} — active {ActiveCount}/{maxActive}");
        }

        [ContextMenu("Clear All Wrecks")]
        private void DebugClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] != null) Destroy(_active[i].gameObject);
            _active.Clear();
        }
    }
}
