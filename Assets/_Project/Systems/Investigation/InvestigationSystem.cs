using System;
using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Dialogue;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Investigation
{
    // 챕터별 사건 진행. 단서 발견 → 다이얼로그 트리거 → 다음 단서 활성화.
    public class InvestigationSystem : MonoBehaviour
    {
        public static InvestigationSystem Instance { get; private set; }

        [Header("Current Case")]
        [SerializeField] private CaseDataSO currentCase;
        [SerializeField] private bool playIntroOnStart = true;

        [Header("Optional v12 Flow")]
        [Tooltip("연결되어 있으면 innerVoices/choices를 가진 단서는 이 컨트롤러로 위임됨")]
        [SerializeField] private VoiceFlowController voiceFlow;

        private readonly HashSet<string> discovered = new HashSet<string>();

        public CaseDataSO CurrentCase => currentCase;
        public IReadOnlyCollection<string> DiscoveredClues => discovered;

        public event Action<ClueDataSO> OnClueDiscovered;
        public event Action<CaseDataSO> OnCaseCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 외부 운전 씬(S01)에서 ActiveCase가 설정되어 진입했다면 그 사건으로 시작.
            // 직접 S03을 Play (디버그)할 땐 ActiveCase가 null이라 인스펙터 default 유지.
            if (ActiveCase.HasCase)
            {
                currentCase = ActiveCase.Current;
                discovered.Clear();
            }
        }

        void Start()
        {
            if (playIntroOnStart && currentCase != null && currentCase.introDialogue != null)
            {
                if (DialogueSystem.Instance != null)
                    DialogueSystem.Instance.Show(currentCase.introDialogue);
            }
        }

        public void SetCase(CaseDataSO data)
        {
            currentCase = data;
            discovered.Clear();
        }

        public bool HasDiscovered(string clueId) => discovered.Contains(clueId);

        public bool CanDiscover(ClueDataSO clue, bool roomCleared)
        {
            if (clue == null) return false;
            if (discovered.Contains(clue.clueId)) return false;
            if (clue.requiresRoomClear && !roomCleared) return false;
            if (!string.IsNullOrEmpty(clue.requiresPreviousClueId) && !discovered.Contains(clue.requiresPreviousClueId)) return false;
            return true;
        }

        public void Discover(ClueDataSO clue, bool roomCleared = true)
        {
            if (!CanDiscover(clue, roomCleared)) return;

            // v12 path: 내면 보이스 + 선택지가 있으면 VoiceFlowController에 위임.
            // 컨트롤러가 끝나면 CompleteDiscovery 콜백 호출.
            if (voiceFlow != null && clue.innerVoices != null && clue.innerVoices.Count > 0)
            {
                discovered.Add(clue.clueId); // 즉시 플래그 — 재트리거 차단
                OnClueDiscovered?.Invoke(clue);
                voiceFlow.Begin(clue, OnFlowComplete);
                return;
            }

            // Legacy fallback: 단일 라인 + 즉시 완료.
            discovered.Add(clue.clueId);
            if (clue.discoveryDialogue != null && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Show(clue.discoveryDialogue);
            OnClueDiscovered?.Invoke(clue);
            CheckCaseComplete();
        }

        // VoiceFlowController가 advance 단서를 발견 처리할 때 사용 — 씬에 ClueObject 없는 단서.
        public void DiscoverFromAdvance(ClueDataSO clue)
        {
            if (clue == null || discovered.Contains(clue.clueId)) return;
            discovered.Add(clue.clueId);
            OnClueDiscovered?.Invoke(clue);
            CheckCaseComplete();
        }

        private void OnFlowComplete(ClueDataSO clue, InvestigationChoice picked)
        {
            // 보이스 플로우 끝남 — 케이스 완결 체크.
            // advanceTo는 컨트롤러가 직접 DiscoverFromAdvance로 처리.
            CheckCaseComplete();
        }

        private void CheckCaseComplete()
        {
            if (currentCase == null) return;
            foreach (var c in currentCase.clues)
            {
                if (c == null) continue;
                if (!discovered.Contains(c.clueId)) return;
            }
            // 모든 단서 발견 → 사건 완결.
            if (currentCase.outroDialogue != null && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Show(currentCase.outroDialogue);
            OnCaseCompleted?.Invoke(currentCase);
        }

        // ---- Debug ----
        // 인스펙터에서 InvestigationSystem 컴포넌트 우클릭 → "Force Complete Case (Debug)"
        // 5개 단서 다 클릭 안 해도 종료 흐름(outro + OnCaseCompleted) 검증 가능.
        [ContextMenu("Force Complete Case (Debug)")]
        private void DebugForceComplete()
        {
            if (currentCase == null)
            {
                Debug.LogWarning("[Investigation] currentCase 없음. 강제 종료 무시.");
                return;
            }
            if (currentCase.clues != null)
                foreach (var c in currentCase.clues)
                    if (c != null) discovered.Add(c.clueId);

            if (currentCase.outroDialogue != null && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Show(currentCase.outroDialogue);
            OnCaseCompleted?.Invoke(currentCase);
            Debug.Log($"[Investigation] forced complete: {currentCase.caseId}");
        }
    }
}
