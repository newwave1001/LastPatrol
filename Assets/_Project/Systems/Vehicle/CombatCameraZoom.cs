using System.Collections;
using UnityEngine;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Vehicle
{
    /// <summary>
    /// 전투 진입 시 카메라 zoom out (멀리), 종료 시 복귀.
    /// CombatStatus.InCombat 폴링 + 전환 시 TopDownCarCamera.SetFramingZoom 부드럽게 lerp.
    ///
    ///   InCombat false → true : 1.0 → combatZoomMultiplier (1.3) over zoomInDuration (3s)
    ///   InCombat true → false : 현재 → 1.0                 over zoomOutDuration
    ///
    /// 사용:
    ///   1. 빈 GameObject + CombatCameraZoom 부착 (TopDownCarCamera 자동 검색)
    ///   2. 인스펙터에서 multiplier / duration 조정 가능
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatCameraZoom : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TopDownCarCamera carCamera;

        [Header("Zoom")]
        [Tooltip("전투 진입 시 카메라 framing 배율. 1=정상, <1=가까이(줌인), >1=멀리(줌아웃). " +
                 "0.3 = 30% 거리 (강한 줌인, 액션 클로즈업).")]
        [SerializeField, Range(0.2f, 2.5f)] private float combatZoomMultiplier = 0.3f;
        [Tooltip("평시(전투 외) framing 배율. 1=인스펙터 default. <1로 두면 평시에도 가까운 시점.")]
        [SerializeField, Range(0.2f, 2.5f)] private float outOfCombatMultiplier = 1.0f;
        [Tooltip("전투 진입 시 transition 시간(초).")]
        [SerializeField, Range(0.1f, 8f)] private float zoomInDuration = 3f;
        [Tooltip("전투 종료 시 복귀 시간(초).")]
        [SerializeField, Range(0.1f, 8f)] private float zoomOutDuration = 2f;

        [Header("Behavior")]
        [Tooltip("전투 종료 시 outOfCombatMultiplier로 자동 복귀할지. " +
                 "끄면 전투 중 zoom 상태가 그대로 유지 (zoom-out 없음).")]
        [SerializeField] private bool revertOnCombatEnd = false;
        [SerializeField] private bool logEvents = true;

        private bool _wasInCombat;
        private Coroutine _zoomRoutine;

        void Awake()
        {
            if (carCamera == null) carCamera = FindAnyObjectByType<TopDownCarCamera>(FindObjectsInactive.Include);
        }

        void Update()
        {
            if (carCamera == null)
            {
                carCamera = FindAnyObjectByType<TopDownCarCamera>(FindObjectsInactive.Include);
                if (carCamera == null) return;
            }

            bool now = CombatStatus.InCombat;
            if (now != _wasInCombat)
            {
                _wasInCombat = now;
                if (now)
                {
                    // 전투 진입 — 항상 zoom in
                    StartZoomTransition(combatZoomMultiplier, zoomInDuration);
                    if (logEvents) Debug.Log($"[CombatCameraZoom] InCombat=True → zoom target={combatZoomMultiplier:F2} over {zoomInDuration:F1}s", this);
                }
                else if (revertOnCombatEnd)
                {
                    // 전투 종료 + revert ON
                    StartZoomTransition(outOfCombatMultiplier, zoomOutDuration);
                    if (logEvents) Debug.Log($"[CombatCameraZoom] InCombat=False → zoom target={outOfCombatMultiplier:F2} over {zoomOutDuration:F1}s", this);
                }
                else
                {
                    if (logEvents) Debug.Log($"[CombatCameraZoom] InCombat=False → zoom 유지 (revertOnCombatEnd=false)", this);
                }
            }
        }

        private void StartZoomTransition(float targetRatio, float duration)
        {
            if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
            _zoomRoutine = StartCoroutine(ZoomRoutine(targetRatio, duration));
        }

        private IEnumerator ZoomRoutine(float targetRatio, float duration)
        {
            if (carCamera == null) yield break;
            float start = carCamera.FramingZoom;
            float t = 0f;
            float dur = Mathf.Max(0.05f, duration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                float ratio = Mathf.Lerp(start, targetRatio, k);
                carCamera.SetFramingZoom(ratio);
                yield return null;
            }
            carCamera.SetFramingZoom(targetRatio);
            _zoomRoutine = null;
        }
    }
}
