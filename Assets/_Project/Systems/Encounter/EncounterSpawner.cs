using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Systems.Dispatch;
using LastPatrol.Systems.Vehicle;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Encounter
{
    /// <summary>
    /// 시간 / 이벤트 기반 적 차량 스폰. 그레이박스용 단순 버전.
    /// 바이블 §10.2 "수사 중 상태 — 인카운터 빈도 증가" 정신 — dispatched 이후에만 스폰.
    ///
    /// 스폰 위치: 마렌 차 뒤편 (forward 반대 방향 N미터). 차가 정지 상태면 spawnDistanceBehind 만큼 -Z쪽.
    /// </summary>
    [DisallowMultipleComponent]
    public class EncounterSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private CarController target;
        [SerializeField] private DispatchSystem dispatchSystem;

        [Header("Trigger")]
        [Tooltip("hunted/dispatched 상태가 된 후 N게임초 경과 시 스폰. 게임시간 5 = 실시간 0.5초 (10× 가속).")]
        [SerializeField] private float spawnAfterSeconds = 5f;
        [SerializeField] private bool requireDispatched = false;
        [Tooltip("PlayerStatus.IsHunted = true 인 동안에만 스폰. " +
                 "사건 완료 후부터 활성 — 누아르 톤 '진실을 안 자가 표적'.")]
        [SerializeField] private bool requireHunted = true;
        [Tooltip("마렌 차 헤드라이트가 켜져있어야 스폰 (불빛이 적을 부른다).")]
        [SerializeField] private bool requireHeadlightsOn = true;
        [SerializeField] private LastPatrol.Systems.Vehicle.Headlights headlights;
        [SerializeField] private int maxSpawns = 1;
        [Tooltip("ON SCENE 도달 후 '이미 한 번 이상 스폰됐다면' 추가 스폰 안 함. " +
                 "처음 한 번은 OnScene이어도 시간 카운트 진행 (즉시 OnScene 시나리오 안전망).")]
        [SerializeField] private bool stopAfterArrival = true;

        [Header("Placement")]
        [Tooltip("마렌 차 뒤편 N미터에 스폰. 직부감 카메라 시야 폭(~30m)보다 커야 화면 밖에서 진입.")]
        [SerializeField] private float spawnDistanceBehind = 50f;
        [SerializeField] private float spawnLateralRandom = 6f;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        public int Spawned { get; private set; }
        public bool ArmedAndWaiting => CanSpawn() && Spawned < maxSpawns;

        private float _timer;
        private bool _wasDispatched;
        private readonly List<EnemyVehicle> _alive = new List<EnemyVehicle>();

        public IReadOnlyList<EnemyVehicle> AliveEnemies => _alive;

        /// <summary>차량 강탈 시 새 차로 갱신 — 적 스폰 위치 계산이 새 차 기준.</summary>
        public void SetTarget(CarController newTarget)
        {
            target = newTarget;
            headlights = newTarget != null ? newTarget.GetComponent<LastPatrol.Systems.Vehicle.Headlights>() : null;
        }

        void Awake()
        {
            if (target == null) target = FindAnyObjectByType<CarController>();
            if (dispatchSystem == null) dispatchSystem = FindAnyObjectByType<DispatchSystem>();
            if (headlights == null && target != null) headlights = target.GetComponent<LastPatrol.Systems.Vehicle.Headlights>();
        }

        void Update()
        {
            if (Spawned >= maxSpawns) return;

            // 첫 스폰 전엔 OnScene이어도 카운트 계속(즉시 OnScene 시나리오 안전망).
            // 한 번 이상 스폰된 사이클에서 OnScene 도달 시에만 추가 스폰 차단.
            if (stopAfterArrival && Spawned > 0 && dispatchSystem != null
                && dispatchSystem.State == DispatchSystem.DispatchState.OnScene) return;

            bool active = CanSpawn();
            if (!active) { _timer = 0f; return; }

            // dispatched 막 됐으면 타이머 리셋 (그 시점부터 카운트)
            if (requireDispatched && dispatchSystem != null)
            {
                bool isDispatched = dispatchSystem.State >= DispatchSystem.DispatchState.EnRoute;
                if (isDispatched && !_wasDispatched) _timer = 0f;
                _wasDispatched = isDispatched;
            }

            _timer += Time.deltaTime;
            if (_timer >= spawnAfterSeconds) Spawn();
        }

        private bool CanSpawn()
        {
            if (target == null || enemyPrefab == null) return false;
            if (requireHunted && !PlayerStatus.IsHunted) return false;
            if (requireDispatched)
            {
                if (dispatchSystem == null) return false;
                if (dispatchSystem.State < DispatchSystem.DispatchState.EnRoute) return false;
            }
            // 헤드라이트 OFF면 적 안 부름 — 어둠 속에 숨음
            if (requireHeadlightsOn)
            {
                if (headlights == null && target != null) headlights = target.GetComponent<LastPatrol.Systems.Vehicle.Headlights>();
                if (headlights == null || !headlights.IsOn) return false;
            }
            return true;
        }

        private void Spawn()
        {
            Vector3 forward = target.transform.forward;
            Vector3 right   = target.transform.right;
            float lateral   = Random.Range(-spawnLateralRandom, spawnLateralRandom);
            Vector3 pos     = target.transform.position - forward * spawnDistanceBehind + right * lateral;
            pos.y = target.transform.position.y;

            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
            var go = Instantiate(enemyPrefab, pos, rot);
            var enemy = go.GetComponent<EnemyVehicle>();
            if (enemy != null)
            {
                enemy.Target = target.transform;
                _alive.Add(enemy);
            }
            Spawned++;
            _timer = 0f;
            if (logEvents) Debug.Log($"[Encounter] spawned enemy #{Spawned} at {pos}", this);
        }

        [ContextMenu("Spawn Now (Debug)")]
        private void DebugSpawnNow()
        {
            if (!Application.isPlaying) return;
            if (target == null || enemyPrefab == null)
            {
                Debug.LogWarning("[Encounter] target/prefab 비어있음 — Inspector 확인");
                return;
            }
            Spawn();
        }

        [ContextMenu("Force Set Hunted (Debug)")]
        private void DebugSetHunted()
        {
            LastPatrol.Systems.World.PlayerStatus.SetHunted(true);
            Debug.Log("[Encounter] PlayerStatus.IsHunted = true (debug forced)");
        }

        [ContextMenu("Diagnose Now (Debug)")]
        private void DebugDiagnose()
        {
            string state = dispatchSystem != null ? dispatchSystem.State.ToString() : "(no dispatch)";
            string targetN = target != null ? target.name : "null";
            string prefabN = enemyPrefab != null ? enemyPrefab.name : "null";
            Debug.Log(
                $"[Encounter Diag]\n" +
                $"  spawned={Spawned}/{maxSpawns}  timer={_timer:F2}/{spawnAfterSeconds}\n" +
                $"  hunted={LastPatrol.Systems.World.PlayerStatus.IsHunted}  requireHunted={requireHunted}\n" +
                $"  dispatchState={state}  requireDispatched={requireDispatched}\n" +
                $"  stopAfterArrival={stopAfterArrival}  target={targetN}  prefab={prefabN}\n" +
                $"  CanSpawn={CanSpawn()}",
                this);
        }
    }
}
