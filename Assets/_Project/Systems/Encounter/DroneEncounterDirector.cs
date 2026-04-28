using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Characters.M07;
using LastPatrol.Systems.Battery;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.Encounter
{
    /// <summary>
    /// 드론 어택 인카운터 디렉터.
    ///
    /// 흐름:
    ///   1) M-07 배터리 0% 도달 → 30초 grace 카운트다운 시작
    ///   2) 도중 배터리 회복 (>0%) → grace 취소, 정상 상태 복귀
    ///   3) 30초 만료 시 화면 밖 3마리 드론 스폰
    ///   4) wave 진행 중엔 grace 재발동 X (드론들이 다 격추되거나 사라질 때까지)
    ///   5) wave 종료 후 배터리 여전히 0이면 새 grace 시작 (반복)
    ///
    /// 의도:
    ///   배터리 = 자유. 떨어지면 30초 안에 충전해야 살아남음.
    ///   드론은 차보다 빨라 도주 불가 → 하차 + M-07 사격이 유일한 해법.
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneEncounterDirector : MonoBehaviour
    {
        [Header("References (자동 검색)")]
        [SerializeField] private M07Controller m07;
        [SerializeField] private VehicleDismount dismount;
        [SerializeField] private CarController playerCar;
        [SerializeField] private Drone dronePrefab;

        [Header("Trigger")]
        [Tooltip("배터리 0 도달 후 드론 스폰까지 유예 시간(초). 이 안에 충전하면 cancel.")]
        [SerializeField] private float gracePeriodSeconds = 30f;
        [Tooltip("이 퍼센트 미만이면 '배터리 0' 으로 간주 (0~100).")]
        [SerializeField, Range(0f, 5f)] private float batteryEmptyThreshold = 0.5f;

        [Header("Spawn")]
        [SerializeField] private int dronesPerWave = 3;
        [Tooltip("플레이어로부터 스폰 거리(m). 화면 밖이 되도록 충분히 멀게.")]
        [SerializeField] private float spawnRadius = 60f;
        [SerializeField] private float spawnAltitude = 10f;
        [Tooltip("스폰 시 드론 사이 각도 무작위성(도).")]
        [SerializeField] private float spawnAngleJitter = 25f;

        [Header("Stealth Evasion (회피 메카닉)")]
        [Tooltip("차 속도 이 값 미만 + 헤드라이트 OFF 상태 N초 유지 시 드론들 떠남.")]
        [SerializeField] private float stealthSpeedThreshold = 0.5f;
        [SerializeField] private float stealthDurationToFlee = 5f;

        [Header("Emergency Loot (드론 스폰 시 응급 폐로봇)")]
        [Tooltip("드론 wave 스폰과 함께 화면 밖에 폐로봇 1개 자동 spawn — 회피 후 회수.")]
        [SerializeField] private LootableRobot emergencyLootPrefab;
        [Tooltip("플레이어로부터 응급 loot 스폰 거리(m). 화면 밖이지만 도보로 갈만한 거리.")]
        [SerializeField] private float emergencyLootRadius = 80f;
        [SerializeField] private int emergencyLootBatteries = 1;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;
        [SerializeField] private bool drawGizmos = true;

        public enum DroneState { Idle, Grace, Wave }
        public DroneState State { get; private set; } = DroneState.Idle;
        public float GraceTimeRemaining => State == DroneState.Grace ? Mathf.Max(0f, _graceEndTime - Time.time) : 0f;
        public int ActiveDroneCount => _activeDrones.Count;

        private readonly List<Drone> _activeDrones = new List<Drone>();
        private float _graceEndTime;
        private float _stealthTimer;
        private bool _droneFleeTriggered;

        void Awake()
        {
            if (m07 == null) m07 = FindAnyObjectByType<M07Controller>(FindObjectsInactive.Include);
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            if (playerCar == null) playerCar = FindAnyObjectByType<CarController>(FindObjectsInactive.Include);
        }

        void Update()
        {
            CleanupActiveDrones();
            if (m07 == null) return;

            bool batteryEmpty = m07.IsAlive && (m07.BatteryPercent * 100f) < batteryEmptyThreshold;

            switch (State)
            {
                case DroneState.Idle:
                    if (batteryEmpty)
                    {
                        State = DroneState.Grace;
                        _graceEndTime = Time.time + gracePeriodSeconds;
                        if (logEvents) Debug.Log($"[DroneEncounter] grace started — {gracePeriodSeconds:F0}s 안에 충전 X 시 드론 어택.", this);
                    }
                    break;

                case DroneState.Grace:
                    if (!batteryEmpty)
                    {
                        State = DroneState.Idle;
                        if (logEvents) Debug.Log("[DroneEncounter] grace canceled — 배터리 회복.", this);
                        break;
                    }
                    if (Time.time >= _graceEndTime)
                    {
                        SpawnWave();
                        State = DroneState.Wave;
                    }
                    break;

                case DroneState.Wave:
                    // 스텔스 회피 — 차 정지 + 라이트 OFF 5초 유지 → 드론 도주
                    if (!_droneFleeTriggered)
                    {
                        if (IsStealthActive())
                        {
                            _stealthTimer += Time.deltaTime;
                            if (_stealthTimer >= stealthDurationToFlee)
                            {
                                TriggerDroneFlee();
                            }
                        }
                        else
                        {
                            _stealthTimer = 0f;
                        }
                    }

                    if (_activeDrones.Count == 0)
                    {
                        if (logEvents) Debug.Log("[DroneEncounter] wave 종료.", this);
                        _droneFleeTriggered = false;
                        _stealthTimer = 0f;
                        // 배터리 여전히 0 → 새 grace 시작
                        if (batteryEmpty)
                        {
                            State = DroneState.Grace;
                            _graceEndTime = Time.time + gracePeriodSeconds;
                            if (logEvents) Debug.Log($"[DroneEncounter] 배터리 여전히 0 — 다음 grace {gracePeriodSeconds:F0}s.", this);
                        }
                        else
                        {
                            State = DroneState.Idle;
                        }
                    }
                    break;
            }
        }

        private bool IsStealthActive()
        {
            if (playerCar == null) return false;
            if (Mathf.Abs(playerCar.CurrentSpeed) > stealthSpeedThreshold) return false;
            var hl = playerCar.GetComponent<Headlights>();
            if (hl != null && hl.IsOn) return false;
            return true;
        }

        private void TriggerDroneFlee()
        {
            _droneFleeTriggered = true;
            if (logEvents) Debug.Log("[DroneEncounter] 스텔스 회피 성공 — 드론들 도주 시작.", this);
            for (int i = 0; i < _activeDrones.Count; i++)
                if (_activeDrones[i] != null) _activeDrones[i].BeginFleeing();
        }

        private void SpawnWave()
        {
            Transform target = ResolveTarget();
            if (target == null)
            {
                Debug.LogWarning("[DroneEncounter] 타겟 없음 — wave skip.", this);
                return;
            }
            if (dronePrefab == null)
            {
                Debug.LogWarning("[DroneEncounter] dronePrefab 없음 — fallback으로 빈 GO 생성.", this);
            }

            float baseAngle = Random.Range(0f, 360f);
            for (int i = 0; i < dronesPerWave; i++)
            {
                float angle = baseAngle + (i / (float)dronesPerWave) * 360f + Random.Range(-spawnAngleJitter, spawnAngleJitter);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * spawnRadius;
                Vector3 spawnPos = target.position + offset + Vector3.up * spawnAltitude;

                Drone drone;
                if (dronePrefab != null)
                {
                    drone = Instantiate(dronePrefab, spawnPos, Quaternion.LookRotation(-offset.normalized));
                }
                else
                {
                    var go = new GameObject($"Drone_Wild_{i}");
                    go.transform.position = spawnPos;
                    go.transform.rotation = Quaternion.LookRotation(-offset.normalized);
                    drone = go.AddComponent<Drone>();
                }
                drone.SetTarget(target);
                _activeDrones.Add(drone);
            }
            if (logEvents) Debug.Log($"[DroneEncounter] {dronesPerWave}마리 스폰 — target {target.name}", this);

            // 응급 LootableRobot 동봉 — 회피 후 회수할 보급
            SpawnEmergencyLoot(target);
        }

        private void SpawnEmergencyLoot(Transform target)
        {
            if (target == null) return;

            float angle = Random.Range(0f, 360f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * emergencyLootRadius;
            Vector3 pos = target.position + offset;
            pos.y = 0.05f;

            LootableRobot loot;
            if (emergencyLootPrefab != null)
            {
                loot = Instantiate(emergencyLootPrefab, pos, Quaternion.identity);
            }
            else
            {
                var go = new GameObject("EmergencyLoot_Drone");
                go.transform.position = pos;
                loot = go.AddComponent<LootableRobot>();
            }
            loot.SetBatteryCount(emergencyLootBatteries);
            if (logEvents) Debug.Log($"[DroneEncounter] 응급 LootableRobot 스폰 @ {pos}", this);
        }

        private Transform ResolveTarget()
        {
            // 도보면 마렌, 운전 중이면 차
            if (dismount != null && dismount.IsDismounted && dismount.MarenTransform != null)
                return dismount.MarenTransform;
            if (playerCar != null) return playerCar.transform;
            return null;
        }

        private void CleanupActiveDrones()
        {
            for (int i = _activeDrones.Count - 1; i >= 0; i--)
                if (_activeDrones[i] == null) _activeDrones.RemoveAt(i);
        }

        // ---- Debug ----

        [ContextMenu("Force Spawn Wave Now")]
        private void DebugForceWave()
        {
            SpawnWave();
            State = DroneState.Wave;
        }

        [ContextMenu("Skip Grace (스폰 즉시)")]
        private void DebugSkipGrace()
        {
            if (State != DroneState.Grace)
            {
                Debug.Log("[DroneEncounter] grace 상태 아님 — skip 무시.");
                return;
            }
            _graceEndTime = Time.time;
        }

        [ContextMenu("Print State")]
        private void DebugPrintState()
        {
            float bp = m07 != null ? m07.BatteryPercent * 100f : -1f;
            Debug.Log($"[DroneEncounter] state={State} graceLeft={GraceTimeRemaining:F1}s active={ActiveDroneCount}/{dronesPerWave} m07Battery={bp:F1}%", this);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Transform t = ResolveTarget();
            if (t == null) return;
            Gizmos.color = new Color(0.85f, 0.2f, 0.18f, 0.4f);
            Gizmos.DrawWireSphere(t.position, spawnRadius);
        }
#endif
    }
}
