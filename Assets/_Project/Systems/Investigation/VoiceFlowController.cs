using System;
using System.Collections;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Dialogue;

namespace LastPatrol.Systems.Investigation
{
    // v12 단서 흐름 통제. 내면 보이스 → 선택지 → 스킬 체크 → 결과 → 부수 효과.
    // ChoicesOverlayUI / RollOverlayUI / SkillsPanelUI는 이벤트로 구독.
    public class VoiceFlowController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float beforeChoicesDelay = 0.3f;
        [SerializeField] private float afterRollDelay = 1.6f;
        [SerializeField] private float afterResultDelay = 1.0f;
        [SerializeField] private float beforeAdvanceDelay = 1.2f;

        public event Action<ClueDataSO> OnChoicesReady;          // ChoicesOverlayUI가 듣고 표시
        public event Action OnChoicesClosed;                      // 선택 후 오버레이 닫기 신호
        public event Action<PlayerSkills.RollResult> OnRollShown; // RollOverlayUI 표시 신호

        private ClueDataSO currentClue;
        private InvestigationChoice pickedChoice;
        private bool waitingForChoice;
        private Action<ClueDataSO, InvestigationChoice> onComplete;
        private Coroutine flow;

        public bool IsRunning => flow != null;
        public ClueDataSO CurrentClue => currentClue;

        public void Begin(ClueDataSO clue, Action<ClueDataSO, InvestigationChoice> onComplete)
        {
            if (clue == null) return;
            if (flow != null) StopCoroutine(flow);
            currentClue = clue;
            this.onComplete = onComplete;
            pickedChoice = null;
            waitingForChoice = false;
            flow = StartCoroutine(RunFlow());
        }

        // ChoicesOverlayUI에서 사용자 선택 시 호출.
        public void SubmitChoice(InvestigationChoice choice)
        {
            if (!waitingForChoice) return;
            pickedChoice = choice;
            waitingForChoice = false;
        }

        IEnumerator RunFlow()
        {
            var dialogue = DialogueSystem.Instance;

            // 1) 내면 보이스 순차 — 큐에 모두 넣고 끝날 때까지 대기
            if (currentClue.innerVoices != null)
            {
                for (int i = 0; i < currentClue.innerVoices.Count; i++)
                {
                    var v = currentClue.innerVoices[i];
                    if (v == null) continue;
                    if (dialogue != null)
                        dialogue.ShowInline(VoiceSpeakerId(v.voice), ThemeForVoice(v.voice), v.textKR, v.textEN);
                }
            }

            // 큐 비울 때까지 대기 (DialogueSystem이 줄 사이 gap 자동 처리)
            yield return new WaitUntil(() => dialogue == null || !dialogue.IsActive);
            yield return new WaitForSeconds(beforeChoicesDelay);

            // 2) 선택지 — 등록자(ChoicesOverlayUI)에게 표시 신호, 선택 대기
            if (currentClue.choices != null && currentClue.choices.Count > 0)
            {
                waitingForChoice = true;
                OnChoicesReady?.Invoke(currentClue);
                yield return new WaitUntil(() => !waitingForChoice);
                OnChoicesClosed?.Invoke();
            }

            // 3) 선택 결과 처리
            if (pickedChoice != null)
                yield return ResolveChoice(pickedChoice);

            // 4) 단서 발견 완결 콜백 (InvestigationSystem)
            onComplete?.Invoke(currentClue, pickedChoice);

            // 5) advanceTo가 있으면 다음 단서 자동 발견
            if (pickedChoice != null && pickedChoice.advanceTo != null)
            {
                yield return new WaitForSeconds(beforeAdvanceDelay);
                if (InvestigationSystem.Instance != null)
                    InvestigationSystem.Instance.DiscoverFromAdvance(pickedChoice.advanceTo);
            }

            currentClue = null;
            pickedChoice = null;
            flow = null;
        }

        IEnumerator ResolveChoice(InvestigationChoice c)
        {
            var dialogue = DialogueSystem.Instance;
            var skills = PlayerSkills.Instance;

            InvestigationResult result = c.result;
            bool? success = null;

            // 스킬 체크 분기
            if (c.skillCheck != null && c.skillCheck.HasCheck && skills != null)
            {
                var roll = skills.Roll(c.skillCheck.skill, c.skillCheck.dc);
                success = roll.success;
                result = roll.success ? c.resultSuccess : c.resultFail;
                OnRollShown?.Invoke(roll);
                yield return new WaitForSeconds(afterRollDelay);
            }

            // 결과 대사
            if (result != null && !(string.IsNullOrEmpty(result.textKR) && string.IsNullOrEmpty(result.textEN)))
            {
                if (dialogue != null)
                    dialogue.ShowInline(result.speakerId, result.theme, result.textKR, result.textEN);
                yield return new WaitUntil(() => dialogue == null || !dialogue.IsActive);
                yield return new WaitForSeconds(afterResultDelay);
            }

            // 스킬 언락/증가 (성공/실패 무관)
            if (c.unlockSkill != SkillId.None && c.unlockSkillAmount != 0 && skills != null)
                skills.AddLevel(c.unlockSkill, c.unlockSkillAmount);
        }

        public static ColorTheme ThemeForVoice(InvestigationVoice v)
        {
            switch (v)
            {
                case InvestigationVoice.Cop:      return ColorTheme.Amber;
                case InvestigationVoice.Hunch:    return ColorTheme.Hunch;
                case InvestigationVoice.Grief:    return ColorTheme.Blood;
                case InvestigationVoice.Cynicism: return ColorTheme.Subtle;
                case InvestigationVoice.Echo:     return ColorTheme.Cream;
                default:                          return ColorTheme.Subtle;
            }
        }

        // 라벨 색 통일 — 모든 보이스 흰색. AccentBar의 voice 색이 시각 구분 담당.
        public static Color VoiceLabelColor(InvestigationVoice v) => Color.white;

        public static string VoiceSpeakerId(InvestigationVoice v)
        {
            switch (v)
            {
                case InvestigationVoice.Cop:      return "수사 본능";
                case InvestigationVoice.Hunch:    return "직감";
                case InvestigationVoice.Grief:    return "비통";
                case InvestigationVoice.Cynicism: return "냉소";
                case InvestigationVoice.Echo:     return "메아리";
                case InvestigationVoice.Silence:  return "...";
                default:                          return "Maren";
            }
        }
    }
}
