using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastPatrol.Data;
using LastPatrol.Systems.Investigation;

namespace LastPatrol.Systems.UI
{
    // HUD 코너에 5스킬 표시. Echo 잠금 시 "—". PlayerSkills 변경 이벤트 구독.
    public class SkillsPanelUI : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public SkillId skill;
            public TMP_Text valueLabel;
        }

        [SerializeField] private PlayerSkills source;
        [SerializeField] private List<Row> rows = new List<Row>();

        void Start()
        {
            if (source == null) source = PlayerSkills.Instance;
            if (source != null) source.OnSkillChanged += HandleChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (source != null) source.OnSkillChanged -= HandleChanged;
        }

        void HandleChanged(SkillId _, int __) => Refresh();

        public void Refresh()
        {
            if (source == null) source = PlayerSkills.Instance;
            if (source == null) return;

            foreach (var r in rows)
            {
                if (r == null || r.valueLabel == null) continue;
                int lv = source.GetLevel(r.skill);
                r.valueLabel.text = lv <= 0 ? "—" : lv.ToString();
            }
        }
    }
}
