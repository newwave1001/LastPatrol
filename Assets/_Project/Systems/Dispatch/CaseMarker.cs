using UnityEngine;

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
    public class CaseMarker : MonoBehaviour
    {
        [Header("Case Info")]
        [Tooltip("무전 코드. 예: CASE-0417")]
        [SerializeField] private string caseId = "CASE-0417";
        [Tooltip("주소/장소 텍스트. ON SCENE 표시에 사용. 예: WESTSIDE 132")]
        [SerializeField] private string addressText = "WESTSIDE 132";
        [Tooltip("선택. 무전 첫 줄에 추가될 짧은 설명. 예: '주거지 이상 신고'")]
        [TextArea, SerializeField] private string dispatchBlurb = "주거지 이상 신고";

        [Header("Gizmo (editor)")]
        [SerializeField] private Color gizmoColor = new Color(0.659f, 0.188f, 0.165f, 0.85f);
        [SerializeField] private float gizmoRadius = 1.2f;
        [SerializeField] private float gizmoPoleHeight = 5f;

        public string CaseId => caseId;
        public string AddressText => addressText;
        public string DispatchBlurb => dispatchBlurb;
        public Vector3 Position => transform.position;

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
