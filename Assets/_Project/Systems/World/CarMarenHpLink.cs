using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Characters;
using LastPatrol.Systems.Vehicle;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 바이블 §10.2: "차 HP = 마렌 생명".
    /// 차에 타고 있을 때 차 데미지를 마렌 HP에 미러. 차 HP 0 → 마렌 HP 0 → OnDied → 게임 오버.
    ///
    /// 도보 모드에선 미러 X — 마렌 자체가 IDamageable로 직접 데미지 받음.
    /// 강탈로 차 바뀌면 자동으로 새 차의 VehicleHealth 추적.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarMarenHpLink : MonoBehaviour
    {
        [Header("References (자동 검색)")]
        [SerializeField] private VehicleDismount dismount;
        [SerializeField] private MarenController maren;

        [Header("Debug")]
        [SerializeField] private bool logMirror = false;

        private VehicleHealth _trackedHealth;

        void Awake()
        {
            if (dismount == null) dismount = FindAnyObjectByType<VehicleDismount>(FindObjectsInactive.Include);
            if (maren == null) maren = FindAnyObjectByType<MarenController>(FindObjectsInactive.Include);
        }

        void OnDisable()
        {
            if (_trackedHealth != null) _trackedHealth.OnDamaged -= HandleCarDamaged;
            _trackedHealth = null;
        }

        void Update()
        {
            if (dismount == null || maren == null) return;

            var car = dismount.CurrentCar;
            var newHealth = car != null ? car.GetComponent<VehicleHealth>() : null;
            if (newHealth == _trackedHealth) return;

            // 강탈 등으로 차 변경 → 구독 옮기기
            if (_trackedHealth != null) _trackedHealth.OnDamaged -= HandleCarDamaged;
            _trackedHealth = newHealth;
            if (_trackedHealth != null) _trackedHealth.OnDamaged += HandleCarDamaged;
        }

        private void HandleCarDamaged(float amount, DamageSource src)
        {
            if (dismount == null || maren == null) return;
            // 도보면 차 데미지 미러 X (마렌이 직접 맞은 게 아님)
            if (dismount.IsDismounted) return;
            if (logMirror) Debug.Log($"[CarMarenHpLink] mirror {amount:F1} → maren ({src})", this);
            maren.TakeDamage(amount, src);
        }
    }
}
