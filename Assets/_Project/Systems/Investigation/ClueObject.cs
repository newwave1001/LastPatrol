using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Data;

namespace LastPatrol.Systems.Investigation
{
    // 씬에 배치된 단서 GameObject. ClueDataSO 참조.
    // autoDiscover=true면 트리거 진입 시 자동 발견. false면 F 키 조사.
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
            if (col != null && data != null && data.autoDiscover) col.isTrigger = true;
        }

        public bool CanInteract(GameObject actor)
        {
            if (data == null) return false;
            if (data.autoDiscover) return false; // 자동 발견은 F 조사 X
            return InvestigationSystem.Instance == null
                || InvestigationSystem.Instance.CanDiscover(data, IsRoomCleared());
        }

        public void Interact(GameObject actor)
        {
            if (data == null) return;
            if (InvestigationSystem.Instance == null) return;
            InvestigationSystem.Instance.Discover(data, IsRoomCleared());
        }

        void OnTriggerEnter(Collider other)
        {
            if (data == null || !data.autoDiscover) return;
            if (other.GetComponentInParent<Characters.MarenController>() == null) return;
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
            Gizmos.color = data != null && data.autoDiscover
                ? new Color(0.85f, 0.54f, 0.29f, 0.5f)  // 앰버 — 자동
                : new Color(0.49f, 0.78f, 0.85f, 0.5f); // 시안 — F 조사
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
