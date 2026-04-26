using System;
using System.Collections.Generic;
using UnityEngine;
using LastPatrol.Data;

namespace LastPatrol.Systems.Investigation
{
    // 마렌의 5 내면 스킬. 시작값 cop:3 / hunch:2 / grief:2 / cynicism:1 / echo:0(잠김).
    // Echo는 0이면 잠긴 상태. 특정 선택지로 +1 unlock.
    // 스킬 체크: 2d6 + level vs DC.
    public class PlayerSkills : MonoBehaviour
    {
        public static PlayerSkills Instance { get; private set; }

        [Serializable]
        public struct InitialLevel
        {
            public SkillId skill;
            public int level;
        }

        [Header("Starting Levels")]
        [SerializeField] private List<InitialLevel> startingLevels = new List<InitialLevel>
        {
            new InitialLevel { skill = SkillId.Cop,      level = 3 },
            new InitialLevel { skill = SkillId.Hunch,    level = 2 },
            new InitialLevel { skill = SkillId.Grief,    level = 2 },
            new InitialLevel { skill = SkillId.Cynicism, level = 1 },
            new InitialLevel { skill = SkillId.Echo,     level = 0 },
        };

        private readonly Dictionary<SkillId, int> levels = new Dictionary<SkillId, int>();

        public event Action<SkillId, int> OnSkillChanged;
        public event Action<RollResult> OnRolled;

        public struct RollResult
        {
            public SkillId skill;
            public int dc;
            public int d1;
            public int d2;
            public int level;
            public int total;
            public bool success;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            foreach (var entry in startingLevels)
                if (entry.skill != SkillId.None) levels[entry.skill] = entry.level;
        }

        public int GetLevel(SkillId skill)
        {
            if (skill == SkillId.None) return 0;
            return levels.TryGetValue(skill, out var v) ? v : 0;
        }

        public bool IsUnlocked(SkillId skill) => GetLevel(skill) > 0;

        public void AddLevel(SkillId skill, int amount)
        {
            if (skill == SkillId.None || amount == 0) return;
            int next = Mathf.Max(0, GetLevel(skill) + amount);
            levels[skill] = next;
            OnSkillChanged?.Invoke(skill, next);
        }

        public RollResult Roll(SkillId skill, int dc)
        {
            int d1 = UnityEngine.Random.Range(1, 7);
            int d2 = UnityEngine.Random.Range(1, 7);
            int level = GetLevel(skill);
            int total = d1 + d2 + level;
            var result = new RollResult
            {
                skill = skill,
                dc = dc,
                d1 = d1,
                d2 = d2,
                level = level,
                total = total,
                success = total >= dc,
            };
            OnRolled?.Invoke(result);
            return result;
        }
    }
}
