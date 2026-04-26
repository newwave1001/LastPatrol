using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastPatrol.Data;
using LastPatrol.Systems.Investigation;
using LastPatrol.Systems.Dialogue;

namespace LastPatrol.Systems.UI
{
    // ChoicesOverlayUI 안의 단일 버튼. 보이스 컬러 + 라벨 + 선택 텍스트 + (선택) 스킬 체크 표시.
    [RequireComponent(typeof(Button))]
    public class ChoiceButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image accentBar;
        [SerializeField] private TMP_Text voiceLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text checkLabel; // optional — skill check 표시
        [SerializeField] private bool preferKorean = true;

        public event Action<InvestigationChoice> OnClicked;

        private InvestigationChoice choice;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        public void Setup(InvestigationChoice c)
        {
            choice = c;
            if (c == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);

            if (voiceLabel != null) voiceLabel.text = VoiceFlowController.VoiceSpeakerId(c.voice).ToUpper();
            if (bodyLabel != null)
            {
                string txt = preferKorean
                    ? (string.IsNullOrEmpty(c.choiceTextKR) ? c.choiceTextEN : c.choiceTextKR)
                    : (string.IsNullOrEmpty(c.choiceTextEN) ? c.choiceTextKR : c.choiceTextEN);
                bodyLabel.text = txt;
            }

            // AccentBar는 통째 voice 색 (덩어리라 잘 보임).
            // VoiceLabel은 글자라 페이퍼에 묻히지 않게 어두운 변형 사용.
            if (accentBar != null) accentBar.color = DialogueSystem.ThemeColor(VoiceFlowController.ThemeForVoice(c.voice));
            if (voiceLabel != null) voiceLabel.color = VoiceFlowController.VoiceLabelColor(c.voice);

            if (checkLabel != null)
            {
                bool hasCheck = c.skillCheck != null && c.skillCheck.HasCheck;
                checkLabel.gameObject.SetActive(hasCheck);
                if (hasCheck)
                    checkLabel.text = $"{c.skillCheck.skill.ToString().ToUpper()} · DC {c.skillCheck.dc}";
            }
        }

        public void Clear()
        {
            choice = null;
            gameObject.SetActive(false);
        }

        void HandleClick()
        {
            if (choice != null) OnClicked?.Invoke(choice);
        }
    }
}
