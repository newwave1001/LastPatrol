using UnityEngine;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.Battery
{
    /// <summary>
    /// 적 차량(VehicleHealth.IsEnemy=true) 파괴 시 옆에 LootableRobot 스폰.
    /// 적 차량 prefab에 부착 (VehicleHealth와 같은 GO).
    ///
    /// EnemyVehicle.HandleSelfDeath이 1.5초 후 차량 GO를 destroy하므로,
    /// 같은 OnDeath 시점에 우리가 LootableRobot을 별도 GO로 spawn해두면
    /// 차 잔해와 무관하게 남아 있음.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleHealth))]
    public class EnemyDeathLootDropper : MonoBehaviour
    {
        [Header("Loot")]
        [Tooltip("스폰할 폐로봇 prefab. null이면 빈 GO에 LootableRobot 동적 부착(자동 빌드).")]
        [SerializeField] private LootableRobot lootPrefab;
        [SerializeField] private int batteriesPerWreck = 1;

        [Header("Placement")]
        [Tooltip("차 옆쪽으로 띄우는 거리 (차 잔해와 분리).")]
        [SerializeField] private float sideOffset = 1.8f;
        [Tooltip("스폰 후 자체 despawn 시간(초). -1 이면 RobotWreckSpawner와 무관하게 남아 있음.")]
        [SerializeField] private float autoDespawnAfter = 90f;

        private VehicleHealth _health;
        private bool _dropped;

        void Awake()
        {
            _health = GetComponent<VehicleHealth>();
            if (_health != null) _health.OnDeath += HandleDeath;
        }

        void OnDestroy()
        {
            if (_health != null) _health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (_dropped) return;
            // Faction — 적만 떨군다.
            if (_health != null && !_health.IsEnemy) return;
            _dropped = true;
            DropLoot();
        }

        private void DropLoot()
        {
            // 차 우측으로 띄우기 — 차량 잔해와 시각 분리.
            Vector3 spawnPos = transform.position + transform.right * sideOffset;
            spawnPos.y = 0.01f;
            Quaternion rot = Quaternion.LookRotation(transform.forward);

            LootableRobot loot;
            if (lootPrefab != null)
            {
                loot = Instantiate(lootPrefab, spawnPos, rot);
            }
            else
            {
                var go = new GameObject($"LootableRobot_{name}");
                go.transform.position = spawnPos;
                go.transform.rotation = rot;
                loot = go.AddComponent<LootableRobot>();
            }
            loot.SetBatteryCount(batteriesPerWreck);
            if (autoDespawnAfter > 0f) Destroy(loot.gameObject, autoDespawnAfter);
        }
    }
}
