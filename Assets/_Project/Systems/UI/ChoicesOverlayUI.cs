using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastPatrol.Core.Input;
using LastPatrol.Data;
using LastPatrol.Systems.Investigation;

namespace LastPatrol.Systems.UI
{
    // 단서 내면 보이스 끝나면 등장하는 선택지 모달. v12 choices-overlay 대응.
    public class ChoicesOverlayUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private VoiceFlowController flow;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text header;
        [SerializeField] private List<ChoiceButtonUI> buttons = new List<ChoiceButtonUI>();

        [Header("Display")]
        [SerializeField] private string headerText = "어떻게 할까";

        [Header("Input Lock")]
        [Tooltip("모달 표시 중 캐릭터 이동 입력 차단. 비워두면 자동 검색.")]
        [SerializeField] private InputReader inputToLock;
        [SerializeField] private bool lockInputWhileVisible = true;

        private bool _inputLocked;

        void Awake()
        {
            HideImmediate();
            if (inputToLock == null) inputToLock = FindFirstObjectByType<InputReader>();
        }

        void OnEnable()
        {
            if (flow != null)
            {
                flow.OnChoicesReady += HandleChoicesReady;
                flow.OnChoicesClosed += Hide;
            }
            foreach (var b in buttons) if (b != null) b.OnClicked += HandlePicked;
        }

        void OnDisable()
        {
            if (flow != null)
            {
                flow.OnChoicesReady -= HandleChoicesReady;
                flow.OnChoicesClosed -= Hide;
            }
            foreach (var b in buttons) if (b != null) b.OnClicked -= HandlePicked;
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

        void HandleChoicesReady(ClueDataSO clue)
        {
            if (header != null) header.text = headerText;

            int count = clue.choices != null ? clue.choices.Count : 0;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                if (i < count) buttons[i].Setup(clue.choices[i]);
                else buttons[i].Clear();
            }
            SetVisible(true);
            AcquireInputLock();
        }

        void HandlePicked(InvestigationChoice c)
        {
            if (flow != null) flow.SubmitChoice(c);
        }

        void Hide()
        {
            SetVisible(false);
            ReleaseInputLock();
        }

        void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            foreach (var b in buttons) if (b != null) b.Clear();
        }

        void SetVisible(bool v)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = v ? 1f : 0f;
                canvasGroup.interactable = v;
                canvasGroup.blocksRaycasts = v;
            }
        }
    }
}
