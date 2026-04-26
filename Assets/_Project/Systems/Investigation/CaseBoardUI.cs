using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using LastPatrol.Data;

namespace LastPatrol.Systems.Investigation
{
    // 사건 보드 패널. Tab 토글. 케이스의 단서를 4개 act (기/승/전/결) 섹션으로 그룹화.
    // 미발견은 ???, 발견은 labelKR/EN. v11 status-panel 톤 맞춰 페이퍼+잉크+시안 보더.
    public class CaseBoardUI : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [Header("Behavior")]
        [SerializeField] private bool startVisible = false;

        [Header("Locale")]
        [SerializeField] private bool preferKorean = true;

        private bool visible;
        private InvestigationSystem subscribed;

        static readonly string[] ActHeadersKR = { "기 · 起", "승 · 承", "전 · 轉", "결 · 結" };
        static readonly string[] ActHeadersEN = { "INTRO", "RISING", "TURN", "RESOLUTION" };

        void Start()
        {
            TrySubscribe();
            SetVisible(startVisible);
            Refresh();
        }

        void OnDisable()
        {
            if (subscribed != null)
            {
                subscribed.OnClueDiscovered -= HandleClueDiscovered;
                subscribed.OnCaseCompleted -= HandleCaseCompleted;
                subscribed = null;
            }
        }

        void Update()
        {
            // InputReader에 Board 액션 추가 전까지 임시 직접 읽기.
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle() => SetVisible(!visible);

        void SetVisible(bool v)
        {
            visible = v;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = v ? 1f : 0f;
                canvasGroup.interactable = v;
                canvasGroup.blocksRaycasts = v;
            }
            else gameObject.SetActive(v);
        }

        void TrySubscribe()
        {
            var inv = InvestigationSystem.Instance;
            if (inv == null || subscribed == inv) return;
            inv.OnClueDiscovered += HandleClueDiscovered;
            inv.OnCaseCompleted += HandleCaseCompleted;
            subscribed = inv;
        }

        void HandleClueDiscovered(ClueDataSO _) => Refresh();
        void HandleCaseCompleted(CaseDataSO _) => Refresh();

        void Refresh()
        {
            TrySubscribe();
            var inv = InvestigationSystem.Instance;
            var c = inv != null ? inv.CurrentCase : null;

            if (headerLabel != null) headerLabel.text = BuildHeader(inv, c);
            if (bodyLabel != null) bodyLabel.text = BuildBody(inv, c);
        }

        string BuildHeader(InvestigationSystem inv, CaseDataSO c)
        {
            if (c == null) return "";
            string title = Pick(c.caseTitleKR, c.caseTitleEN);
            string address = Pick(c.caseAddressKR, c.caseAddressEN);

            int total = c.clues != null ? c.clues.Count : 0;
            int found = 0;
            if (c.clues != null && inv != null)
                for (int i = 0; i < c.clues.Count; i++)
                    if (c.clues[i] != null && inv.HasDiscovered(c.clues[i].clueId)) found++;

            var sb = new StringBuilder();
            sb.Append("<b>").Append(title).Append("</b>\n");
            if (!string.IsNullOrEmpty(address))
                sb.Append("<color=#3A2E28AA>").Append(address).Append("</color>\n");
            sb.Append("<color=#3A2E2888>").Append(found).Append(" / ").Append(total).Append("</color>");
            return sb.ToString();
        }

        string BuildBody(InvestigationSystem inv, CaseDataSO c)
        {
            if (c == null) return "";
            var sb = new StringBuilder();
            for (int act = 0; act < 4; act++)
            {
                var header = preferKorean ? ActHeadersKR[act] : ActHeadersEN[act];
                sb.Append("<b>").Append(header).Append("</b>\n");

                bool any = false;
                if (c.clues != null)
                {
                    for (int i = 0; i < c.clues.Count; i++)
                    {
                        var cl = c.clues[i];
                        if (cl == null || (int)cl.act != act) continue;
                        any = true;
                        bool discovered = inv != null && inv.HasDiscovered(cl.clueId);
                        if (discovered)
                        {
                            string lbl = Pick(cl.labelKR, cl.labelEN);
                            sb.Append("  • ").Append(lbl).Append('\n');
                        }
                        else
                        {
                            sb.Append("  <color=#3A2E2888>◦ ???</color>\n");
                        }
                    }
                }
                if (!any) sb.Append("  <color=#3A2E2855>—</color>\n");
                if (act < 3) sb.Append('\n');
            }
            return sb.ToString();
        }

        string Pick(string kr, string en)
        {
            if (preferKorean) return string.IsNullOrEmpty(kr) ? en : kr;
            return string.IsNullOrEmpty(en) ? kr : en;
        }
    }
}
