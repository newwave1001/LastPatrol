using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 차량 헤드라이트. 자식 SpotLight들을 묶어서 ON/OFF + 속도 비례 미세 흔들림.
    ///
    /// 사용:
    ///   P_Car_Temp 자식에 Headlight_L, Headlight_R 두 개 SpotLight 두고 이 컴포넌트를 차량 root에 부착.
    ///   Spots 리스트 비워두면 Awake에서 자식 Light 자동 검색 (Spot 타입만).
    /// </summary>
    [DisallowMultipleComponent]
    public class Headlights : MonoBehaviour
    {
        [SerializeField] private List<Light> spots = new List<Light>();
        [SerializeField] private bool defaultOn = true;
        [SerializeField] private CarController car;

        [Header("Subtle motion (Perlin)")]
        [Tooltip("최대 속도일 때 헤드라이트 떨림 강도(도). 0이면 비활성. " +
                 "라플란드 비포장 도로 느낌은 0.6~1.2.")]
        [SerializeField, Range(0f, 3f)] private float jitterDegreesAtMaxSpeed = 0.8f;
        [SerializeField] private float maxSpeedRef = 18f;
        [SerializeField] private float noiseFrequency = 6f;

        private Quaternion[] _baseLocalRot;
        private bool _on;

        public bool IsOn => _on;

        void Awake()
        {
            if (spots == null || spots.Count == 0)
            {
                spots = new List<Light>(GetComponentsInChildren<Light>(true));
                spots.RemoveAll(l => l == null || l.type != LightType.Spot);
            }

            _baseLocalRot = new Quaternion[spots.Count];
            for (int i = 0; i < spots.Count; i++)
                if (spots[i] != null)
                    _baseLocalRot[i] = spots[i].transform.localRotation;

            if (car == null) car = GetComponent<CarController>();
            SetOn(defaultOn);
        }

        public void SetOn(bool on)
        {
            _on = on;
            for (int i = 0; i < spots.Count; i++)
                if (spots[i] != null) spots[i].enabled = on;
        }

        public void Toggle() => SetOn(!_on);

        void Update()
        {
            if (!_on || car == null || jitterDegreesAtMaxSpeed <= 0f) return;

            float ratio = Mathf.Clamp01(Mathf.Abs(car.CurrentSpeed) / Mathf.Max(0.1f, maxSpeedRef));
            float jitter = jitterDegreesAtMaxSpeed * ratio;
            if (jitter < 0.001f) return;

            for (int i = 0; i < spots.Count; i++)
            {
                var light = spots[i];
                if (light == null) continue;
                float pitch = (Mathf.PerlinNoise(Time.time * noiseFrequency, i * 13.7f) - 0.5f) * 2f;
                float yaw   = (Mathf.PerlinNoise(Time.time * noiseFrequency * 0.8f + i * 17f, 0.7f) - 0.5f) * 2f;
                Quaternion noise = Quaternion.Euler(pitch * jitter, yaw * jitter, 0f);
                light.transform.localRotation = _baseLocalRot[i] * noise;
            }
        }
    }
}
