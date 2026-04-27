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
        [SerializeField, Range(60f, 90f)] private float pitch = 78f;

        [Header("Map Bounds (optional)")]
        [SerializeField] private bool clampToBounds = false;
        [SerializeField] private Vector2 minXZ = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 maxXZ = new Vector2( 50f,  50f);

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

            // 진행 방향 lookahead — 차량 forward × 현재 속도 비율 + resting offset
            Vector3 lookaheadOffset = Vector3.zero;
            if (car != null)
            {
                float speedRatio = Mathf.Clamp(car.CurrentSpeed / 18f, -1f, 1f);
                // resting offset이 차량 forward 반대 방향(음수)이면 정지 시 차량이 화면 위쪽.
                // 가속 시 lookahead가 더 큰 양의 forward로 → 차량이 점점 화면 중앙/아래로 (진행 방향 미리 보기).
                lookaheadOffset = car.Forward * (lookAhead * speedRatio + restingForwardOffset);
            }
            else
            {
                // car 참조 없으면 월드 +Z 기준 단순 offset (yaw=0 가정)
                lookaheadOffset = new Vector3(0f, 0f, restingForwardOffset);
            }

            Vector3 desired = target.position + lookaheadOffset + Vector3.up * height;

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
