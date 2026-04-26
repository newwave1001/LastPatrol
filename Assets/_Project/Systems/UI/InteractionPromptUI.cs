using TMPro;
using UnityEngine;
using LastPatrol.Characters;

namespace LastPatrol.Systems.UI
{
    // 인터랙션 가능 대상 근처에서 [F] 라벨 표시.
    // InteractionSystem.CurrentTarget을 매 프레임 읽어 텍스트/표시 갱신.
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InteractionSystem source;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;

        [Header("Display")]
        [SerializeField] private string keyHint = "[F]";
        [SerializeField] private string format = "{0}  {1}"; // {0}=key, {1}=prompt
        [SerializeField] private float fadeSpeed = 10f;

        void Update()
        {
            var target = source != null ? source.CurrentTarget : null;
            float wantAlpha = target != null ? 1f : 0f;

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, wantAlpha, fadeSpeed * Time.deltaTime);

            if (target != null && label != null)
                label.text = string.Format(format, keyHint, target.PromptLabel);
        }
    }
}
