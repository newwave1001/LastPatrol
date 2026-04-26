using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Data
{
    // v12 — 마렌의 다섯 내면 목소리 + 침묵 옵션.
    public enum InvestigationVoice
    {
        None = 0,
        Cop = 1,         // 수사 본능 — amber
        Hunch = 2,       // 직감 — olive
        Grief = 3,       // 비통 — blood
        Cynicism = 4,    // 냉소 — subtle
        Echo = 5,        // 노아의 메아리 — cream (잠금)
        Silence = 6,     // 침묵 — italic subtle
    }

    // 5 활성 스킬. Echo는 시작 0(잠금), 특정 선택지로 언락.
    public enum SkillId
    {
        None = 0,
        Cop = 1,
        Hunch = 2,
        Grief = 3,
        Cynicism = 4,
        Echo = 5,
    }

    // 내면 보이스 한 줄. 단서 발견 시 순차 재생.
    [System.Serializable]
    public class InnerVoiceLine
    {
        public InvestigationVoice voice = InvestigationVoice.Cop;
        [TextArea(2, 4)] public string textKR;
        [TextArea(2, 4)] public string textEN;
    }

    // 2d6 + level vs DC. skill = None이면 체크 없음.
    [System.Serializable]
    public class SkillCheck
    {
        public SkillId skill = SkillId.None;
        public int dc = 6;

        public bool HasCheck => skill != SkillId.None;
    }

    // 결과 한 라인. 선택지의 result / result_success / result_fail에 사용.
    [System.Serializable]
    public class InvestigationResult
    {
        public string speakerId = "Maren";
        public ColorTheme theme = ColorTheme.Amber;
        [TextArea(2, 5)] public string textKR;
        [TextArea(2, 5)] public string textEN;
    }

    // 단서별 선택지 한 개. 보이스 + 표시 텍스트 + (선택) 스킬 체크 + 결과.
    [System.Serializable]
    public class InvestigationChoice
    {
        public string id;
        public InvestigationVoice voice = InvestigationVoice.Cop;
        [TextArea(1, 3)] public string choiceTextKR;
        [TextArea(1, 3)] public string choiceTextEN;

        [Tooltip("Skill = None이면 체크 없이 result 사용. 있으면 result_success / result_fail로 분기.")]
        public SkillCheck skillCheck;

        public InvestigationResult result;        // 체크 없을 때
        public InvestigationResult resultSuccess; // 체크 성공
        public InvestigationResult resultFail;    // 체크 실패

        [Header("Side Effects")]
        public SkillId unlockSkill = SkillId.None;
        public int unlockSkillAmount = 0;

        [Tooltip("이 선택을 고르면 해당 ClueDataSO를 자동 발견 처리 (body→note 식)")]
        public ClueDataSO advanceTo;
    }
}
