using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Data
{
    /// <summary>
    /// 에이리넨 도시 마스터 지도 (eirinen_master_map_v01.html SVG 기반).
    /// 도로/건물 좌표를 SVG 단위로 보관 → CityBuilder가 Unity 좌표로 환산해 큐브 자동 배치.
    ///
    /// 좌표 변환:
    ///   Unity X = SVG x * scale
    ///   Unity Z = (flipY ? svgSize.y - svg.y : svg.y) * scale  ← SVG y=0이 위쪽이라 보통 flip.
    /// </summary>
    [CreateAssetMenu(fileName = "C_Eirinen", menuName = "LastPatrol/City Data")]
    public class CityDataSO : ScriptableObject
    {
        [Header("Coordinate Conversion")]
        [Tooltip("SVG 캔버스 크기 (eirinen v01: 1000×800).")]
        public Vector2 svgSize = new Vector2(1000f, 800f);

        [Tooltip("1 SVG 단위 = N Unity 미터. 0.4 → 도시 400m × 320m.")]
        public float svgToUnityScale = 0.4f;

        [Tooltip("SVG y=0(위쪽)을 Unity z(큰 값)으로 매핑. true면 화면상 SVG와 동일 방향.")]
        public bool flipY = true;

        [Tooltip("도로/건물 베이스 Y. 그라운드와 z-fight 방지 위해 살짝 위.")]
        public float groundBaseY = 0.01f;

        [Header("Ground")]
        public bool buildGround = true;
        public Color groundColor = new Color(0.91f, 0.93f, 0.94f, 1f); // snow #E8EDF0
        public Material groundMaterial;

        [Header("Roads")]
        public Color roadColor = new Color(0.16f, 0.16f, 0.18f, 1f); // 짙은 회색
        public Material roadMaterial;
        public List<RoadDef> roads = new List<RoadDef>();

        [Header("Buildings")]
        public Color defaultBuildingColor = new Color(0.65f, 0.62f, 0.58f, 1f);
        public Material defaultBuildingMaterial;
        [Tooltip("건물 큐브에 적용할 Layer 이름 (CarController obstacleMask와 매칭).")]
        public string obstacleLayerName = "Obstacle";
        public List<BuildingDef> buildings = new List<BuildingDef>();

        // ---- helpers ----
        public Vector3 SvgToUnity(Vector2 svgXY)
        {
            float x = svgXY.x * svgToUnityScale;
            float z = (flipY ? svgSize.y - svgXY.y : svgXY.y) * svgToUnityScale;
            return new Vector3(x, groundBaseY, z);
        }

        public float SvgScale(float svg) => svg * svgToUnityScale;

        // ---- ContextMenu: Phase A 데이터 자동 채움 ----
        [ContextMenu("Fill Phase A (Eirinen Center)")]
        private void FillPhaseA()
        {
            roads.Clear();
            buildings.Clear();

            // ---- Roads (메인 동서 + 메인 남북) ----
            roads.Add(new RoadDef {
                name = "MainEW", svgFromXY = new Vector2(0, 440), svgToXY = new Vector2(1000, 440), widthSVG = 12f
            });
            roads.Add(new RoadDef {
                name = "MainNS", svgFromXY = new Vector2(500, 0), svgToXY = new Vector2(500, 800), widthSVG = 10f
            });

            // ---- Buildings (Phase A: town center + CH1 site) ----
            buildings.Add(new BuildingDef {
                code = "POI-01", displayName = "경찰서",
                svgPosXY = new Vector2(445, 395), svgSizeWH = new Vector2(50, 35),
                heightUnity = 8f, tint = new Color(0.55f, 0.55f, 0.52f)
            });
            buildings.Add(new BuildingDef {
                code = "POI-02", displayName = "교회",
                svgPosXY = new Vector2(380, 360), svgSizeWH = new Vector2(50, 70),
                heightUnity = 14f, tint = new Color(0.85f, 0.80f, 0.73f)
            });
            buildings.Add(new BuildingDef {
                code = "CH-01", displayName = "노르드만",
                svgPosXY = new Vector2(790, 300), svgSizeWH = new Vector2(36, 30),
                heightUnity = 7f, tint = new Color(0.66f, 0.19f, 0.16f) // blood
            });
            buildings.Add(new BuildingDef {
                code = "CH-02", displayName = "타코 술집",
                svgPosXY = new Vector2(515, 395), svgSizeWH = new Vector2(40, 35),
                heightUnity = 7f, tint = new Color(0.55f, 0.42f, 0.30f)
            });
            buildings.Add(new BuildingDef {
                code = "SUB-D", displayName = "아흐토 빌라",
                svgPosXY = new Vector2(720, 255), svgSizeWH = new Vector2(38, 90),
                heightUnity = 24f, tint = new Color(0.60f, 0.53f, 0.47f)
            });
            buildings.Add(new BuildingDef {
                code = "POI-03", displayName = "식료품점",
                svgPosXY = new Vector2(440, 460), svgSizeWH = new Vector2(32, 28),
                heightUnity = 6f, tint = new Color(0.72f, 0.66f, 0.56f)
            });
        }
    }

    [Serializable]
    public class RoadDef
    {
        public string name = "Road";
        public Vector2 svgFromXY;
        public Vector2 svgToXY;
        public float widthSVG = 8f;
    }

    [Serializable]
    public class BuildingDef
    {
        public string code = "POI";
        public string displayName = "건물";
        [Tooltip("SVG 좌상단 좌표 (x, y).")]
        public Vector2 svgPosXY;
        [Tooltip("SVG 폭/높이 (w, h).")]
        public Vector2 svgSizeWH;
        [Tooltip("건물 높이 (Unity 미터).")]
        public float heightUnity = 8f;
        public Color tint = Color.white;
        [Tooltip("필요 시 개별 머티리얼 (없으면 CityDataSO.defaultBuildingMaterial).")]
        public Material overrideMaterial;
    }
}
