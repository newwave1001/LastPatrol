using UnityEngine;
using LastPatrol.Core;
using LastPatrol.Data;

namespace LastPatrol.Systems.Dispatch
{
    /// <summary>
    /// 사건 현장 위치 컴포넌트. 빈 GameObject에 붙이면 그 Transform이 마커 위치.
    /// DispatchSystem이 시간 경과 후 활성화하고, 차량과의 거리를 검사.
    ///
    /// CaseMarker 자체는 비주얼 요소를 강요하지 않음 — 그레이박스 단계는 Gizmo로 충분.
    /// 자식에 시각 마커(폴 + 테이프 등) 두면 Active 토글 시 함께 켜짐.
    /// </summary>
    [DisallowMultipleComponent]
    public class CaseMarker : MonoBehaviour, IInteractable
    {
        [Header("Case Info")]
        [Tooltip("선택. 연결되면 ActiveCase로 전달되어 실내 씬에서 자동 로드됨.")]
        [SerializeField] private CaseDataSO caseData;
        [Tooltip("무전 코드. caseData 있으면 caseData.caseId 우선.")]
        [SerializeField] private string caseId = "CASE-0417";
        [Tooltip("주소/장소 텍스트. caseData 있으면 caseData.caseAddressKR 우선.")]
        [SerializeField] private string addressText = "WESTSIDE 132";
        [Tooltip("선택. 무전 첫 줄에 추가될 짧은 설명.")]
        [TextArea, SerializeField] private string dispatchBlurb = "주거지 이상 신고";

        [Header("Gizmo (editor)")]
        [SerializeField] private Color gizmoColor = new Color(0.659f, 0.188f, 0.165f, 0.85f);
        [SerializeField] private float gizmoRadius = 1.2f;
        [SerializeField] private float gizmoPoleHeight = 5f;

        public CaseDataSO CaseData => caseData;
        public string CaseId => caseData != null && !string.IsNullOrEmpty(caseData.caseId) ? caseData.caseId : caseId;
        public string AddressText => caseData != null && !string.IsNullOrEmpty(caseData.caseAddressKR) ? caseData.caseAddressKR : addressText;
        public string DispatchBlurb => dispatchBlurb;
        public Vector3 Position => transform.position;

        // ---- IInteractable (도보 마렌이 마커 근처면 [E] 진입) ----
        public string PromptLabel => "사건 현장 진입";
        public bool IsAlive => true;

        private DispatchSystem _dispatch;

        void Awake()
        {
            // Trigger Collider 자동 부착 (InteractionSystem.OverlapSphere가 잡도록)
            EnsureTriggerCollider();
        }

        private void EnsureTriggerCollider()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(4f, 3f, 4f);
                box.center = new Vector3(0f, 1.5f, 0f);
            }
            else
            {
                if (!col.isTrigger) col.isTrigger = true;
            }
        }

        public bool CanInteract(GameObject actor)
        {
            return gameObject.activeInHierarchy;
        }

        public void Interact(GameObject actor)
        {
            if (_dispatch == null) _dispatch = FindAnyObjectByType<DispatchSystem>();
            if (_dispatch == null)
            {
                Debug.LogError("[CaseMarker] DispatchSystem 없음 — 씬 전환 불가.", this);
                return;
            }
            _dispatch.EnterSceneNow();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * gizmoPoleHeight);
            // top cap
            Gizmos.DrawWireSphere(transform.position + Vector3.up * gizmoPoleHeight, gizmoRadius * 0.4f);
        }
    }
}
