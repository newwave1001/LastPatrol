using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastPatrol.Systems.Dialogue
{
    // 하단 좌측 대사 패널의 UI 레퍼런스 보유.
    // DialogueSystem이 이 컴포넌트를 통해 텍스트/색상 갱신.
    public class DialoguePanelUI : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image accentBar;
        [SerializeField] private Image background;

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        public void SetSpeaker(string id)
        {
            if (speakerLabel != null) speakerLabel.text = id;
        }

        public void SetAccent(Color themeColor)
        {
            if (accentBar != null) accentBar.color = themeColor;
        }

        public void SetBody(string text)
        {
            if (bodyText != null) bodyText.text = text;
        }

        public void SetBackgroundAlpha(float alpha)
        {
            if (background == null) return;
            var c = background.color;
            c.a = alpha;
            background.color = c;
        }
    }
}
