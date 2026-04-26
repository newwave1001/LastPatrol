using UnityEngine;

namespace LastPatrol.Data
{
    public enum InvestigationAct { Intro, Rising, Turn, Resolution } // 기 / 승 / 전 / 결

    [CreateAssetMenu(fileName = "CL_NewClue", menuName = "LastPatrol/Clue Data")]
    public class ClueDataSO : ScriptableObject
    {
        public string clueId;
        public InvestigationAct act = InvestigationAct.Intro;

        [Header("Display")]
        public string labelKR;
        public string labelEN;

        [Header("Discovery")]
        [Tooltip("true: 근접 시 자동 발견 / false: F 키로 조사")]
        public bool autoDiscover;
        [Tooltip("이 ID의 단서가 먼저 발견되어야 이 단서 활성화")]
        public string requiresPreviousClueId;
        [Tooltip("방 안 적이 모두 정리되어야 발견 가능")]
        public bool requiresRoomClear;

        [Header("Dialogue")]
        public DialogueLineSO discoveryDialogue;
    }
}
