using System;
using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Dialogue;

namespace LastPatrol.Systems.Investigation
{
    // 챕터별 사건 진행. 단서 발견 → 다이얼로그 트리거 → 다음 단서 활성화.
    public class InvestigationSystem : MonoBehaviour
    {
        public static InvestigationSystem Instance { get; private set; }

        [Header("Current Case")]
        [SerializeField] private CaseDataSO currentCase;
        [SerializeField] private bool playIntroOnStart = true;

        private readonly HashSet<string> discovered = new HashSet<string>();

        public CaseDataSO CurrentCase => currentCase;
        public IReadOnlyCollection<string> DiscoveredClues => discovered;

        public event Action<ClueDataSO> OnClueDiscovered;
        public event Action<CaseDataSO> OnCaseCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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

            discovered.Add(clue.clueId);
            if (clue.discoveryDialogue != null && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Show(clue.discoveryDialogue);

            OnClueDiscovered?.Invoke(clue);
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
    }
}
