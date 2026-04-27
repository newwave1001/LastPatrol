using UnityEngine;
using LastPatrol.Core;

namespace LastPatrol.Characters
{
    // 마렌 주변의 IInteractable을 감지하고 F 키 조사 트리거.
    public class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private float interactRadius = 1.5f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        public IInteractable CurrentTarget { get; private set; }

        void Update()
        {
            CurrentTarget = FindNearest();
        }

        public bool TryInteract()
        {
            if (CurrentTarget == null) return false;
            if (!CurrentTarget.CanInteract(gameObject)) return false;
            CurrentTarget.Interact(gameObject);
            return true;
        }

        private IInteractable FindNearest()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius, interactableLayer);
            IInteractable best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                // 자식 collider도 root까지 검색 — IInteractable이 부모 GameObject에 있을 수 있음
                var i = h.GetComponentInParent<IInteractable>();
                if (i == null) continue;
                if (!i.CanInteract(gameObject)) continue;
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.54f, 0.29f, 0.4f); // 앰버 톤
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
