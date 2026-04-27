using System.Collections;
using UnityEngine;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 직부감 카메라 추종. v04 mockup의 lookahead + lerp 추종을 3D로 옮긴 단순 버전.
    ///
    /// **반드시 Camera 컴포넌트가 있는 GameObject에만 붙일 것.**
    /// 차량(P_Car_Temp) 같은 일반 GameObject에 붙이면 그 GameObject의 transform 자체가
    /// 카메라처럼 들려 올라가고 78°로 기울어진다(차가 하늘로 날아가는 증상).
    ///
    /// 그레이박스용. 폴리싱 단계에서 Cinemachine 가상 카메라(CinemachineCamera + Position Composer)로
    /// 교체 권장. Confiner2D / Confiner는 도로 영역 기반.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class TopDownCarCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private CarController car;

        [Header("Framing")]
        [Tooltip("타겟 위 높이 (Y). 직부감이라 비교적 높게.")]
        [SerializeField] private float height = 18f;
        [Tooltip("타겟 진행 방향으로 카메라가 살짝 미리 보는 거리. v04 CAM_LOOKAHEAD = 50px.")]
        [SerializeField] private float lookAhead = 4.5f;
        [Tooltip("차량 정지 시 카메라가 차량 forward 반대 방향으로 N미터. " +
                 "음수면 차량이 화면 위쪽에 위치, 양수면 화면 아래. " +
                 "pitch 78°일 때 약 -4 정도가 차량 화면 중앙.")]
        [SerializeField] private float restingForwardOffset = -4f;
        [Tooltip("카메라 추종 부드러움. v04 CAM_LERP = 0.08.")]
        [SerializeField, Range(0.01f, 1f)] private float followSmoothing = 0.08f;

        [Header("Tilt")]
        [Tooltip("완전 직부감(90)이면 게임 정보 손실. 살짝 기울이면 맥락 늘어남.")]
        [SerializeField, Range(20f, 90f)] private float pitch = 78f;

        [Header("Foot Mode (도보 — 쿼터뷰)")]
        [Tooltip("도보 모드 카메라 각도. 40도 권장 (쿼터뷰 — 캐릭터 입체).")]
        [SerializeField, Range(20f, 80f)] private float footPitch = 40f;
        [Tooltip("도보 모드 height 비율 (drive 대비). 더 작을수록 캐릭터에 가까움.")]
        [SerializeField, Range(0.1f, 1f)] private float footHeightRatio = 0.8f;
        [Tooltip("도보 모드 lookAhead 비율.")]
        [SerializeField, Range(0f, 1f)] private float footLookAheadRatio = 0f;
        [Tooltip("도보 모드 resting forward offset 비율.")]
        [SerializeField, Range(0f, 1f)] private float footRestOffsetRatio = 0f;
        [Tooltip("Drive ↔ Foot 모드 전환 보간 시간(초). 길수록 부드러움.")]
        [SerializeField] private float modeTransitionSeconds = 1.6f;

        [Header("Map Bounds (optional)")]
        [SerializeField] private bool clampToBounds = false;
        [SerializeField] private Vector2 minXZ = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 maxXZ = new Vector2( 50f,  50f);

        public Transform Target => target;

        // 베이스 framing 값 (인스펙터 default를 보존하고 zoom ratio로 곱하기 위함)
        private float _baseHeight, _baseLookAhead, _baseRestingFwdOffset;
        private bool _baseFramingCached;
        private float _framingZoom = 1f;

        public float FramingZoom => _framingZoom;

        /// <summary>외부에서 추종 타겟 변경 (하차 시 마렌 캐릭터로 전환 등).</summary>
        public void SetTarget(Transform t)
        {
            target = t;
        }

        /// <summary>줌 비율 변경. 1=인스펙터 default, 0.5=절반(가까움), 2=두 배(멀리).
        /// pitch는 안 건드림 — 쿼터뷰 전환은 SetFootMode 사용.</summary>
        public void SetFramingZoom(float ratio)
        {
            CacheBaseFraming();
            _framingZoom = Mathf.Max(0.1f, ratio);
            height = _baseHeight * _framingZoom;
            lookAhead = _baseLookAhead * _framingZoom;
            restingForwardOffset = _baseRestingFwdOffset * _framingZoom;
        }

        public void ResetFramingZoom() => SetFramingZoom(1f);

        private float _basePitch;
        private bool _isFootMode;
        private Coroutine _modeRoutine;
        private float _footWeight; // 0 = Drive, 1 = Foot. 위치 계산 보간 가중치.

        public bool IsFootMode => _isFootMode;

        /// <summary>도보(쿼터뷰) ↔ 운전(직부감) 모드 전환. 부드러운 보간.</summary>
        public void SetFootMode(bool footMode)
        {
            CacheBaseFraming();
            if (_isFootMode == footMode) return;
            _isFootMode = footMode;
            if (_modeRoutine != null) StopCoroutine(_modeRoutine);
            _modeRoutine = StartCoroutine(ModeTransitionRoutine(footMode, modeTransitionSeconds));
        }

        private IEnumerator ModeTransitionRoutine(bool toFoot, float duration)
        {
            float startHeight = height;
            float startLookAhead = lookAhead;
            float startRestOffset = restingForwardOffset;
            float startPitch = pitch;
            float startWeight = _footWeight;

            float endHeight, endLookAhead, endRestOffset, endPitch, endWeight;
            if (toFoot)
            {
                endHeight     = _baseHeight * footHeightRatio;
                endLookAhead  = _baseLookAhead * footLookAheadRatio;
                endRestOffset = _baseRestingFwdOffset * footRestOffsetRatio;
                endPitch      = footPitch;
                endWeight     = 1f;
            }
            else
            {
                endHeight     = _baseHeight;
                endLookAhead  = _baseLookAhead;
                endRestOffset = _baseRestingFwdOffset;
                endPitch      = _basePitch;
                endWeight     = 0f;
            }

            float t = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                height               = Mathf.Lerp(startHeight,    endHeight,    k);
                lookAhead            = Mathf.Lerp(startLookAhead, endLookAhead, k);
                restingForwardOffset = Mathf.Lerp(startRestOffset, endRestOffset, k);
                pitch                = Mathf.Lerp(startPitch,     endPitch,     k);
                _footWeight          = Mathf.Lerp(startWeight,    endWeight,    k);
                yield return null;
            }
            height = endHeight;
            lookAhead = endLookAhead;
            restingForwardOffset = endRestOffset;
            pitch = endPitch;
            _footWeight = endWeight;
            _modeRoutine = null;
        }

        private void CacheBaseFraming()
        {
            if (_baseFramingCached) return;
            _baseHeight = height;
            _baseLookAhead = lookAhead;
            _baseRestingFwdOffset = restingForwardOffset;
            _basePitch = pitch;
            _baseFramingCached = true;
        }

        void Reset()
        {
            // RequireComponent로 Camera가 없는 곳엔 못 붙지만, Reset에서 transform 변경은
            // 안전을 위해 제거. 직부감 회전은 LateUpdate에서 매 프레임 갱신되므로 초기값 무관.
        }

        void Awake()
        {
            // 추가 안전장치: 혹시라도 Camera 없는 곳에 붙으면 자기 자신을 비활성화.
            if (GetComponent<Camera>() == null)
            {
                Debug.LogError($"[TopDownCarCamera] '{name}' 에 Camera 컴포넌트가 없다. " +
                               "이 스크립트는 Main Camera 같은 카메라 GameObject 전용이다. " +
                               "자동차(P_Car_Temp)에는 붙이지 말 것.", this);
                enabled = false;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            // ---- Drive 모드 desired (직부감) ----
            Vector3 lookaheadOffset = Vector3.zero;
            if (car != null)
            {
                float speedRatio = Mathf.Clamp(car.CurrentSpeed / 18f, -1f, 1f);
                lookaheadOffset = car.Forward * (lookAhead * speedRatio + restingForwardOffset);
            }
            else
            {
                lookaheadOffset = new Vector3(0f, 0f, restingForwardOffset);
            }
            Vector3 driveDesired = target.position + lookaheadOffset + Vector3.up * height;

            // ---- Foot 모드 desired (쿼터뷰) ----
            // pitch가 90° 가까우면 distance 0 (직부감), 작아질수록 카메라가 target 뒤로 멀어짐.
            // distance = height / tan(pitch). yaw 0 고정이라 -Z 방향.
            float pitchRad = Mathf.Max(5f, pitch) * Mathf.Deg2Rad;
            float footDistance = height / Mathf.Tan(pitchRad);
            Vector3 footDesired = target.position + new Vector3(0f, height, -footDistance);

            // 두 desired 위치를 _footWeight로 보간 — 모드 전환 시 부드러움
            Vector3 desired = Vector3.Lerp(driveDesired, footDesired, _footWeight);

            if (clampToBounds)
            {
                desired.x = Mathf.Clamp(desired.x, minXZ.x, maxXZ.x);
                desired.z = Mathf.Clamp(desired.z, minXZ.y, maxXZ.y);
            }

            // v04 lerp(t,a,b) ≈ Lerp(a, b, lerp_factor) per frame.
            // Frame-rate independent: 1 - exp(-rate * dt)
            float t = 1f - Mathf.Exp(-followSmoothing * 60f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);

            // Pitch만 유지, yaw는 월드 고정(직부감 누아르 톤은 회전 없는 카메라가 더 안정)
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
