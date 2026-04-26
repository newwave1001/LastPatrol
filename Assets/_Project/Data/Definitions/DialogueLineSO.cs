using UnityEngine;

namespace LastPatrol.Data
{
    public enum ColorTheme { Cyan, Amber, Blood, Subtle }

    [CreateAssetMenu(fileName = "DL_NewLine", menuName = "LastPatrol/Dialogue Line")]
    public class DialogueLineSO : ScriptableObject
    {
        public string speakerId; // "MAREN", "M-07", "PEKKA", "NARR_ENV" 등
        [TextArea(2, 5)] public string textKR;
        [TextArea(2, 5)] public string textEN;
        public ColorTheme theme = ColorTheme.Subtle;
        public AudioClip voiceClip;

        [Header("Display")]
        [Tooltip("0이면 DialogueSystem의 기본 표시 시간 사용")]
        public float displaySecondsOverride = 0f;
    }
}
