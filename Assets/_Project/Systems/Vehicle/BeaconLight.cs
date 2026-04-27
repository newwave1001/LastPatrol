using UnityEngine;
using LastPatrol.Core.Input;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 경찰 비콘 — 적/청 SpotLight 교차 깜빡임. v04 mockup의 c.beacon 토글 이식.
    ///
    /// 사용:
    ///   P_Car_Temp 자식에 Beacon_Red, Beacon_Blue 두 SpotLight 두고 이 컴포넌트를 차량 root에 부착.
    ///   InputReader는 자동 검색(같은 GO 또는 씬 전체).
    ///   B 키 토글 (InputReader.OnBeaconPressed).
    ///
    /// 패턴: cycleSeconds 동안 적 절반 / 청 절반 켜짐. 0.6초 기본.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeaconLight : MonoBehaviour
    {
        [Header("Lights (children of car)")]
        [SerializeField] private Light redLight;
        [SerializeField] private Light blueLight;

        [Header("Optional: emissive bulbs (MeshRenderer with _EmissionColor material)")]
        [Tooltip("적색 큐브 등 emissive 머티리얼이 있는 렌더러. 깜빡임에 맞춰 emission 강도 조절.")]
        [SerializeField] private Renderer redBulb;
        [SerializeField] private Renderer blueBulb;
        [SerializeField, ColorUsage(false, true)] private Color redEmissionOn  = new Color(8f, 0.5f, 0.5f);
        [SerializeField, ColorUsage(false, true)] private Color blueEmissionOn = new Color(0.5f, 1.5f, 8f);
        [SerializeField, ColorUsage(false, true)] private Color emissionOff    = Color.black;

        [Header("Pattern")]
        [Tooltip("한 사이클 길이(초). 적 절반/청 절반 alternating.")]
        [SerializeField] private float cycleSeconds = 0.6f;

        [Header("Input")]
        [SerializeField] private InputReader input;
        [SerializeField] private bool startOn = false;

        public bool IsActive { get; private set; }

        private float _t;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (input == null) input = FindFirstObjectByType<InputReader>();
            _mpb = new MaterialPropertyBlock();
            SetActive(startOn);
        }

        void OnEnable()
        {
            if (input != null) input.OnBeaconPressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.OnBeaconPressed -= Toggle;
        }

        public void Toggle() => SetActive(!IsActive);

        public void SetActive(bool active)
        {
            IsActive = active;
            if (!active)
            {
                ApplyState(redOn: false, blueOn: false);
                _t = 0f;
            }
        }

        void Update()
        {
            if (!IsActive) return;
            _t += Time.deltaTime;
            if (cycleSeconds <= 0.01f) cycleSeconds = 0.6f;
            float phase = _t % cycleSeconds;
            bool redOn  = phase < cycleSeconds * 0.5f;
            bool blueOn = !redOn;
            ApplyState(redOn, blueOn);
        }

        private void ApplyState(bool redOn, bool blueOn)
        {
            if (redLight  != null) redLight.enabled  = redOn;
            if (blueLight != null) blueLight.enabled = blueOn;

            if (redBulb != null)
            {
                redBulb.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, redOn ? redEmissionOn : emissionOff);
                redBulb.SetPropertyBlock(_mpb);
            }
            if (blueBulb != null)
            {
                blueBulb.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, blueOn ? blueEmissionOn : emissionOff);
                blueBulb.SetPropertyBlock(_mpb);
            }
        }
    }
}
