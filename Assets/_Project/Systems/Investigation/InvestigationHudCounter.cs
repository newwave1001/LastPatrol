using TMPro;
using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.Systems.Investigation
{
    // HUD 단서 카운터. v11의 "INVESTIGATION 0 / 4"와 동일 톤.
    public class InvestigationHudCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "{0} / {1}";

        private InvestigationSystem subscribed;

        void Start()
        {
            TrySubscribe();
            Refresh();
        }

        void OnDisable()
        {
            if (subscribed != null)
            {
                subscribed.OnClueDiscovered -= HandleDiscovered;
                subscribed.OnCaseCompleted -= HandleCompleted;
                subscribed = null;
            }
        }

        void TrySubscribe()
        {
            var inv = InvestigationSystem.Instance;
            if (inv == null || subscribed == inv) return;
            inv.OnClueDiscovered += HandleDiscovered;
            inv.OnCaseCompleted += HandleCompleted;
            subscribed = inv;
        }

        void HandleDiscovered(ClueDataSO _) => Refresh();
        void HandleCompleted(CaseDataSO _) => Refresh();

        void Refresh()
        {
            TrySubscribe();
            if (label == null) return;
            var inv = InvestigationSystem.Instance;
            var c = inv != null ? inv.CurrentCase : null;
            if (c == null) { label.text = ""; return; }

            int total = c.clues != null ? c.clues.Count : 0;
            int found = 0;
            if (c.clues != null)
                for (int i = 0; i < c.clues.Count; i++)
                    if (c.clues[i] != null && inv.HasDiscovered(c.clues[i].clueId)) found++;

            label.text = string.Format(format, found, total);
        }
    }
}
