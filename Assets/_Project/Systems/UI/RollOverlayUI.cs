using System.Collections;
using TMPro;
using UnityEngine;
using LastPatrol.Core.Input;
using LastPatrol.Systems.Investigation;

namespace LastPatrol.Systems.UI
{
    // 스킬 체크 시 잠깐 등장하는 주사위 굴림 오버레이. v12 roll-overlay 대응.
    public class RollOverlayUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private VoiceFlowController flow;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text labelText;    // "COP CHECK · DC 7"
        [SerializeField] private TMP_Text valueText;    // "12"
        [SerializeField] private TMP_Text formulaText;  // "4 + 5 + 3"
        [SerializeField] private TMP_Text resultText;   // "성공 · SUCCESS"

        [Header("Timing")]
        [SerializeField] private float fadeIn = 0.25f;
        [SerializeField] private float hold = 1.4f;
        [SerializeField] private float fadeOut = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color successColor = new Color(0.49f, 0.78f, 0.85f);
        [SerializeField] private Color failColor    = new Color(0.66f, 0.19f, 0.16f);

        [Header("Input Lock")]
        [SerializeField] private InputReader inputToLock;
        [SerializeField] private bool lockInputWhileVisible = true;

        private Coroutine running;
        private bool _inputLocked;

        void Awake()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (inputToLock == null) inputToLock = FindAnyObjectByType<InputReader>();
        }

        void OnEnable()  { if (flow != null) flow.OnRollShown += Show; }
        void OnDisable()
        {
            if (flow != null) flow.OnRollShown -= Show;
            ReleaseInputLock();
        }

        private void AcquireInputLock()
        {
            if (!lockInputWhileVisible || _inputLocked || inputToLock == null) return;
            inputToLock.PushLock();
            _inputLocked = true;
        }

        private void ReleaseInputLock()
        {
            if (!_inputLocked || inputToLock == null) return;
            inputToLock.PopLock();
            _inputLocked = false;
        }

        void Show(PlayerSkills.RollResult r)
        {
            if (labelText != null)   labelText.text   = $"{r.skill.ToString().ToUpper()} CHECK · DC {r.dc}";
            if (valueText != null)   valueText.text   = r.total.ToString();
            if (formulaText != null) formulaText.text = $"{r.d1} + {r.d2} + {r.level}";
            if (resultText != null)
            {
                resultText.text = r.success ? "성공" : "실패";
                resultText.color = r.success ? successColor : failColor;
            }
            if (running != null) StopCoroutine(running);
            AcquireInputLock();
            running = StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            yield return Fade(0f, 1f, fadeIn);
            yield return new WaitForSeconds(hold);
            yield return Fade(1f, 0f, fadeOut);
            running = null;
            ReleaseInputLock();
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = to;
        }
    }
}
