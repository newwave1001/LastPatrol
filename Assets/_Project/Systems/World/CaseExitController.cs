using System;
using System.Collections;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Dialogue;
using LastPatrol.Systems.Investigation;

namespace LastPatrol.Systems.World
{
    /// <summary>
    /// 사건 종료(InvestigationSystem.OnCaseCompleted) 시:
    ///   1. (옵션) outro 다이얼로그 끝날 때까지 잠깐 대기
    ///   2. ActiveCase.Clear()
    ///   3. SceneTransitionService.LoadScene(returnSceneName)
    ///
    /// 보통 S03 씬에 빈 GameObject로 두면 Awake에서 InvestigationSystem 자동 검색 후 구독.
    /// </summary>
    [DisallowMultipleComponent]
    public class CaseExitController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InvestigationSystem investigation;

        [Header("Return Scene")]
        [Tooltip("사건 종료 후 로드할 씬. 보통 외부 운전 씬으로 복귀.")]
        [SerializeField] private string returnSceneName = "S01_OutdoorDriving";

        [Header("Timing")]
        [Tooltip("outro 다이얼로그가 끝나는 시점을 기다림 (DialogueSystem.OnQueueEnded 구독). " +
                 "true면 delayBeforeTransition 무시, 다이얼로그 끝난 후 extraDelayAfterOutro만 추가 대기.")]
        [SerializeField] private bool waitForOutroEnd = true;

        [Tooltip("waitForOutroEnd=true 일 때 outro 끝난 후 추가 여유 시간(초).")]
        [SerializeField] private float extraDelayAfterOutro = 0.6f;

        [Tooltip("waitForOutroEnd=false (또는 outro가 없을 때) 사용되는 고정 대기 시간(초).")]
        [SerializeField] private float delayBeforeTransition = 4f;

        [Tooltip("waitForOutroEnd=true일 때 안전 timeout(초). 그 안에 outro 안 끝나면 강제 진행.")]
        [SerializeField] private float waitTimeout = 30f;

        [Tooltip("ActiveCase를 클리어할지 (다음 사건 시작 전에 잔재 방지).")]
        [SerializeField] private bool clearActiveCase = true;

        [Header("Player Status")]
        [Tooltip("사건 완료 시 PlayerStatus.IsHunted=true 마킹. 다음 사이클부터 인카운터 발생.")]
        [SerializeField] private bool markPlayerAsHunted = true;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        private bool _transitioning;

        void Awake()
        {
            if (investigation == null) investigation = FindFirstObjectByType<InvestigationSystem>();
        }

        void OnEnable()
        {
            if (investigation != null) investigation.OnCaseCompleted += HandleCaseCompleted;
        }

        void OnDisable()
        {
            if (investigation != null) investigation.OnCaseCompleted -= HandleCaseCompleted;
        }

        private void HandleCaseCompleted(CaseDataSO data)
        {
            if (_transitioning) return;
            _transitioning = true;
            if (logEvents) Debug.Log($"[CaseExit] case completed: {(data != null ? data.caseId : "?")} → returning to {returnSceneName} in {delayBeforeTransition:F1}s");
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            // outro 다이얼로그가 진행 중이면 끝까지 기다림, 그 다음 짧은 여유.
            // 다이얼로그가 없거나 비활성이면 fallback: delayBeforeTransition 고정 대기.
            if (waitForOutroEnd && DialogueSystem.Instance != null && DialogueSystem.Instance.IsActive)
            {
                bool ended = false;
                Action handler = () => ended = true;
                DialogueSystem.Instance.OnQueueEnded += handler;

                float t = 0f;
                while (!ended && t < waitTimeout)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                DialogueSystem.Instance.OnQueueEnded -= handler;

                if (extraDelayAfterOutro > 0f)
                    yield return new WaitForSeconds(extraDelayAfterOutro);

                if (logEvents) Debug.Log($"[CaseExit] outro ended after {t:F1}s, transitioning.");
            }
            else
            {
                if (delayBeforeTransition > 0f)
                    yield return new WaitForSeconds(delayBeforeTransition);
            }

            // 진실을 알게 됐으니 표적이 된다 — 다음 사이클부터 EncounterSpawner가 적 스폰.
            if (markPlayerAsHunted) PlayerStatus.SetHunted(true);
            PlayerStatus.NotifyCaseCompleted();

            if (clearActiveCase) ActiveCase.Clear();

            if (SceneTransitionService.Instance == null)
            {
                Debug.LogError("[CaseExit] SceneTransitionService 인스턴스 없음. " +
                               "씬에 SceneTransitionService GameObject를 추가하거나 S01에서 시작해서 DontDestroyOnLoad로 살아남아야 함.");
                _transitioning = false;
                yield break;
            }

            SceneTransitionService.Instance.LoadScene(returnSceneName);
        }
    }
}
