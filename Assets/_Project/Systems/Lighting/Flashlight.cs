using UnityEngine;
using UnityEngine.InputSystem;
using LastPatrol.Core.Input;

namespace LastPatrol.Systems.Lighting
{
    // 마렌의 손전등. 자식 SpotLight를 마우스 커서 방향으로 회전 + L 키 토글.
    //
    // 사용:
    //   마렌 GameObject 자식에 SpotLight (Light.type=Spot) 두고 이 컴포넌트를 마렌 root에 부착.
    //   spot 비워두면 자식에서 자동 검색.
    //   카메라는 Camera.main 자동 사용.
    //
    // Phase 3에서 TurretController가 IsOn + AimDirection을 읽어서 M-07 조준에 통합.
    [DisallowMultipleComponent]
    public class Flashlight : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Light spot;
        [Tooltip("회전 기준 Transform. 비워두면 spot.transform 자체를 회전.")]
        [SerializeField] private Transform pivot;
        [SerializeField] private Camera cam;
        [SerializeField] private InputReader input;

        [Header("Behavior")]
        [SerializeField] private bool defaultOn = true;
        [Tooltip("토글 키. 기본 L. 변경하려면 인스펙터에서 다른 Key 선택 필요(코드 수정).")]
        [SerializeField] private bool toggleEnabled = true;

        private bool _on;
        private Vector3 _aimDir = Vector3.forward;

        public bool IsOn => _on;
        // 월드 좌표에서 손전등이 비추는 방향 (정규화). M-07 TurretController가 조준에 사용.
        public Vector3 AimDirection => _aimDir;
        public Vector3 AimOrigin => pivot != null ? pivot.position : transform.position;
        public float ConeAngleDeg => spot != null ? spot.spotAngle : 60f;

        void Awake()
        {
            if (spot == null)
            {
                var lights = GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Spot) { spot = l; break; }
                }
            }
            if (pivot == null && spot != null) pivot = spot.transform;
            ResolveCamera();
            if (input == null) input = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);

            _aimDir = (pivot != null ? pivot.forward : transform.forward);
            SetOn(defaultOn);
        }

        // Camera.main은 MainCamera 태그가 있는 활성 카메라만 잡음.
        // Cinemachine Brain이 붙은 카메라가 그 태그 없으면 null이 되어 마우스 평면 캐스팅이 안 돼서
        // 손전등이 초기 forward에 그대로 머무름. fallback으로 씬의 아무 Camera나 사용.
        private void ResolveCamera()
        {
            if (cam != null) return;
            cam = Camera.main;
            if (cam != null) return;
            cam = FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        public void SetOn(bool on)
        {
            _on = on;
            if (spot != null) spot.enabled = on;
        }

        public void Toggle() => SetOn(!_on);

        void Update()
        {
            // 토글 — Foot 모드 + Lock 풀린 상태에서만 반응. (대사 중 입력 차단 존중)
            if (toggleEnabled
                && Keyboard.current != null
                && Keyboard.current.lKey.wasPressedThisFrame
                && (input == null || (!input.IsLocked && input.CurrentMode != InputReader.Mode.Drive)))
            {
                Toggle();
            }

            if (pivot == null) return;
            if (cam == null) ResolveCamera();
            if (cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mouseScreen = mouse.position.ReadValue();

            // pivot을 지나고 normal=-cam.forward 인 평면에 마우스 ray 캐스팅.
            // 2.5D 사이드뷰에서 마우스 좌표를 캐릭터 깊이 평면으로 매핑.
            // AimDirection은 손전등 ON/OFF 무관하게 항상 갱신 (M-07 사격 콘이 사용).
            Plane plane = new Plane(-cam.transform.forward, pivot.position);
            Ray ray = cam.ScreenPointToRay(mouseScreen);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 mouseWorld = ray.GetPoint(dist);
                Vector3 dir = mouseWorld - pivot.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    _aimDir = dir.normalized;
                    // 스폿라이트 회전은 켜져 있을 때만 (꺼져있으면 transform 안 건드림).
                    if (_on) pivot.rotation = Quaternion.LookRotation(_aimDir);
                }
            }
        }
    }
}
