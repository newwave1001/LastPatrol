using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Data;
using LastPatrol.Systems.Dialogue;

namespace LastPatrol.Systems.Investigation
{
    // 구석방 문 — 특정 단서 발견 후만 열림.
    // 그레이박스: 열릴 때 GameObject 비활성화 또는 자식 시각 토글.
    [RequireComponent(typeof(Collider))]
    public class HiddenDoor : MonoBehaviour, IInteractable
    {
        [Header("Unlock Condition")]
        [SerializeField] private string requiresClueId;
        [SerializeField] private bool requiresRoomClear = true;

        [Header("Dialogue")]
        [SerializeField] private DialogueLineSO lockedDialogue;
        [SerializeField] private DialogueLineSO openedDialogue;

        [Header("Visual")]
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openedVisual;
        [SerializeField] private Collider blockingCollider;

        public string PromptLabel => "문 조사";

        public bool IsOpen { get; private set; }

        void Awake()
        {
            ApplyVisual();
        }

        public bool CanInteract(GameObject actor) => !IsOpen;

        public void Interact(GameObject actor)
        {
            if (IsOpen) return;

            bool clueOk = string.IsNullOrEmpty(requiresClueId) ||
                          (InvestigationSystem.Instance != null && InvestigationSystem.Instance.HasDiscovered(requiresClueId));
            bool roomOk = !requiresRoomClear || IsRoomCleared();

            if (!clueOk || !roomOk)
            {
                if (lockedDialogue != null && DialogueSystem.Instance != null)
                    DialogueSystem.Instance.Show(lockedDialogue);
                return;
            }

            Open();
        }

        public void Open()
        {
            IsOpen = true;
            ApplyVisual();
            if (openedDialogue != null && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Show(openedDialogue);
        }

        private void ApplyVisual()
        {
            if (closedVisual != null) closedVisual.SetActive(!IsOpen);
            if (openedVisual != null) openedVisual.SetActive(IsOpen);
            if (blockingCollider != null) blockingCollider.enabled = !IsOpen;
        }

        private bool IsRoomCleared()
        {
            var enemies = FindObjectsByType<Characters.Enemies.EnemyAI>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i].IsAlive) return false;
            return true;
        }
    }
}
