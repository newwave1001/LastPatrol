using UnityEngine;
using LastPatrol.Characters;
using LastPatrol.Characters.M07;
using LastPatrol.Characters.Enemies;

namespace LastPatrol.Systems.World
{
    // 그레이박스 검증용 통합 HUD. 화면 좌상단에 모든 캐릭터 상태 표시.
    // 폴리싱 단계에 World Space Canvas + TextMeshPro로 교체.
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] private int fontSize = 14;
        [SerializeField] private Vector2 origin = new Vector2(12, 12);
        [SerializeField] private float lineHeight = 22f;

        private GUIStyle style;

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    normal = { textColor = Color.white }
                };
            }

            float y = origin.y;
            var prevColor = GUI.color;

            var maren = FindAnyObjectByType<MarenController>();
            if (maren != null)
            {
                string tag = "";
                var cover = maren.GetComponent<CoverSystem>();
                if (cover != null && cover.IsInCover) tag += "  [COVER]";
                if (maren.IsCharging) tag += "  [CHARGING]";
                GUI.color = maren.IsAlive ? new Color(1f, 0.7f, 0.4f) : Color.gray;
                GUI.Label(new Rect(origin.x, y, 600, lineHeight),
                    $"MAREN   HP {maren.CurrentHP:F0}{tag}", style);
                y += lineHeight;
            }

            var m07 = FindAnyObjectByType<M07Controller>();
            if (m07 != null)
            {
                GUI.color = m07.IsAlive
                    ? (m07.IsHacked ? new Color(1f, 0.4f, 0.4f) : new Color(0.6f, 0.95f, 1f))
                    : Color.gray;
                GUI.Label(new Rect(origin.x, y, 600, lineHeight),
                    $"M-07    HP {(m07.IsAlive ? "alive" : "down")}   Bat {m07.CurrentBattery:F0}/{m07.MaxBattery:F0}{(m07.IsHacked ? "  [HACKED]" : "")}", style);
                y += lineHeight;
            }

            var enemies = FindObjectsByType<EnemyAI>(FindObjectsInactive.Include);
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                GUI.color = e.IsAlive ? Color.white : Color.gray;
                GUI.Label(new Rect(origin.x, y, 600, lineHeight),
                    $"ENEMY{i}  HP {e.CurrentHP:F0}/{e.MaxHP:F0}   State: {e.CurrentState}", style);
                y += lineHeight;
            }

            GUI.color = prevColor;
        }
    }
}
