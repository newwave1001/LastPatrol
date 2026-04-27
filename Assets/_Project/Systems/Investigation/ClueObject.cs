using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Data;

namespace LastPatrol.Systems.Investigation
{
    // 씬에 배치된 단서 GameObject. ClueDataSO 참조.
    // 항상 F 조사 모드 — 마렌이 다가가면 [F] 조사 prompt 뜨고 F 눌러야 발견.
    // (이전 autoDiscover 자동 발견 모드는 제거. SO의 autoDiscover 플래그는 무시.)
    [RequireComponent(typeof(Collider))]
    public class ClueObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private ClueDataSO data;
        [SerializeField] private bool requireRoomClearOverride = false;

        public ClueDataSO Data => data;
        public string PromptLabel => data != null ? (string.IsNullOrEmpty(data.labelKR) ? data.labelEN : data.labelKR) : "단서";

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public bool CanInteract(GameObject actor)
        {
            if (data == null) return false;
            return InvestigationSystem.Instance == null
                || InvestigationSystem.Instance.CanDiscover(data, IsRoomCleared());
        }

        public void Interact(GameObject actor)
        {
            if (data == null) return;
            if (InvestigationSystem.Instance == null) return;
            InvestigationSystem.Instance.Discover(data, IsRoomCleared());
        }

        private bool IsRoomCleared()
        {
            if (requireRoomClearOverride) return false;
            // 단순 구현: 살아있는 EnemyAI가 0이면 클리어. 추후 방 단위로 분리.
            var enemies = FindObjectsByType<Characters.Enemies.EnemyAI>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i].IsAlive) return false;
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.5f); // 시안 — F 조사
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
