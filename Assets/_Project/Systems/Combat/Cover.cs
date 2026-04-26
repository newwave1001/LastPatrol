using UnityEngine;

namespace LastPatrol.Systems.Combat
{
    // 엄폐 가능 영역. BoxCollider 자동 추가.
    // Bullet이 GetComponentInParent<Cover>()로 검출 — 시각 메시는 자식에 자유롭게 둘 수 있음.
    [RequireComponent(typeof(Collider))]
    public class Cover : MonoBehaviour
    {
        [Header("Snap")]
        [Tooltip("마렌이 엄폐 시 위치할 카메라 쪽(앞쪽) 오프셋. 콜라이더 중심 기준 로컬 좌표.")]
        [SerializeField] private Vector3 snapOffset = new Vector3(0f, 0f, -1.0f);

        public Vector3 GetSnapWorldPosition()
        {
            return transform.TransformPoint(snapOffset);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.49f, 0.78f, 0.85f, 0.6f); // 시안
            Vector3 p = transform.TransformPoint(snapOffset);
            Gizmos.DrawWireSphere(p, 0.35f);
            Gizmos.DrawLine(transform.position, p);
        }
    }
}
