using UnityEngine;
using LastPatrol.Systems.Combat;

namespace LastPatrol.Characters
{
    // coverHeld가 처음 눌린 순간 가장 가까운 Cover로 자동 스냅. 떼면 해제.
    // 마렌은 엄폐 중 이동 잠금 (MarenController가 입력 차단).
    public class CoverSystem : MonoBehaviour
    {
        [Header("Search")]
        [SerializeField] private float coverSearchRadius = 2.5f;
        [SerializeField] private LayerMask coverLayer = ~0;

        public bool IsInCover { get; private set; }
        public Cover CurrentCover { get; private set; }

        private CharacterMovement movement;

        void Awake()
        {
            movement = GetComponent<CharacterMovement>();
        }

        public void Tick(bool coverHeld)
        {
            if (!coverHeld)
            {
                if (IsInCover) ExitCover();
                return;
            }

            if (IsInCover) return;

            Cover best = FindNearest();
            if (best != null) EnterCover(best);
        }

        private Cover FindNearest()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, coverSearchRadius, coverLayer, QueryTriggerInteraction.Collide);
            Cover bestC = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].GetComponentInParent<Cover>();
                if (c == null) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; bestC = c; }
            }
            return bestC;
        }

        private void EnterCover(Cover c)
        {
            CurrentCover = c;
            IsInCover = true;
            Vector3 p = c.GetSnapWorldPosition();
            p.y = transform.position.y;
            if (movement != null) movement.Teleport(p);
            else transform.position = p;
        }

        private void ExitCover()
        {
            IsInCover = false;
            CurrentCover = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, coverSearchRadius);
        }
    }
}
