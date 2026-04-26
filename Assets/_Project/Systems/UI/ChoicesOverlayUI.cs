using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

        void Awake() => HideImmediate();

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
        }

        void HandlePicked(InvestigationChoice c)
        {
            if (flow != null) flow.SubmitChoice(c);
        }

        void Hide() => SetVisible(false);

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
