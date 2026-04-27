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

        [ContextMenu("Fill Phase A+B (Eirinen Town Center)")]
        private void FillPhaseAB()
        {
            FillPhaseA();
            AppendPhaseB();
        }

        [ContextMenu("Fill Phase A+B+C (Eirinen Full)")]
        private void FillPhaseABC()
        {
            FillPhaseA();
            AppendPhaseB();
            AppendPhaseC();
        }

        [ContextMenu("Fill Full Map (Eirinen + Ruin + Slum)")]
        private void FillFullMap()
        {
            // 맵 3배 확장 — 1000×800 → 3000×2400 SVG (Unity 1200×960m)
            svgSize = new Vector2(3000f, 2400f);
            FillPhaseA();
            AppendPhaseB();
            AppendPhaseC();
            AppendPhaseD(); // 건물별 접근 도로
            AppendPhaseE(); // 폐허 마을 + 로봇폐기장 + 마렌의 집
            AppendBanditVillage(); // 강도 마을 (빈민가 대체)
        }

        // 기존 데이터 보존하고 휴게소만 ADD. 사용자가 인스펙터에서 수정한 내용 유지.
        // 중복 호출 시 자동으로 skip (REST-GAS 코드로 판별).
        [ContextMenu("Add Rest Stop (Triangle Center)")]
        private void AddRestStop()
        {
            if (buildings.Exists(b => b != null && b.code == "REST-GAS"))
            {
                Debug.Log("[CityDataSO] Rest stop already exists. Skip.");
                return;
            }
            AppendRestStop();
        }

        private void AppendPhaseB()
        {
            // ---- Roads (보조 도로 — 주거구 cross streets) ----
            roads.Add(new RoadDef {
                name = "EastResidEW_North", svgFromXY = new Vector2(500, 340), svgToXY = new Vector2(900, 340), widthSVG = 6f
            });
            roads.Add(new RoadDef {
                name = "EastResidEW_South", svgFromXY = new Vector2(500, 540), svgToXY = new Vector2(900, 540), widthSVG = 6f
            });
            roads.Add(new RoadDef {
                name = "EastResidNS_W", svgFromXY = new Vector2(620, 340), svgToXY = new Vector2(620, 620), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "EastResidNS_E", svgFromXY = new Vector2(760, 340), svgToXY = new Vector2(760, 620), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "WestCrossEW", svgFromXY = new Vector2(140, 360), svgToXY = new Vector2(500, 360), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "WestNS", svgFromXY = new Vector2(260, 440), svgToXY = new Vector2(260, 620), widthSVG = 4f
            });

            // ---- Buildings (광장 / 도서관 / 주유소 + 추가 주거) ----
            buildings.Add(new BuildingDef {
                code = "S-07", displayName = "마을 광장",
                svgPosXY = new Vector2(480, 370), svgSizeWH = new Vector2(40, 20),
                heightUnity = 0.3f, tint = new Color(0.85f, 0.80f, 0.73f) // 페이퍼 톤 (광장은 낮게)
            });
            buildings.Add(new BuildingDef {
                code = "S-09", displayName = "도서관",
                svgPosXY = new Vector2(340, 460), svgSizeWH = new Vector2(36, 30),
                heightUnity = 8f, tint = new Color(0.66f, 0.60f, 0.49f)
            });
            buildings.Add(new BuildingDef {
                code = "S-05", displayName = "폐 주유소",
                svgPosXY = new Vector2(560, 460), svgSizeWH = new Vector2(28, 26),
                heightUnity = 5f, tint = new Color(0.54f, 0.54f, 0.53f)
            });

            // 동측 주거구 — 대규모 주거 블록 (마스터 맵의 일반 residence 표현)
            buildings.Add(new BuildingDef {
                code = "RES-E1", displayName = "주거 E1",
                svgPosXY = new Vector2(540, 380), svgSizeWH = new Vector2(60, 50),
                heightUnity = 9f, tint = new Color(0.70f, 0.62f, 0.54f)
            });
            buildings.Add(new BuildingDef {
                code = "RES-E2", displayName = "주거 E2",
                svgPosXY = new Vector2(660, 380), svgSizeWH = new Vector2(80, 50),
                heightUnity = 9f, tint = new Color(0.66f, 0.60f, 0.52f)
            });
            buildings.Add(new BuildingDef {
                code = "RES-E3", displayName = "주거 E3",
                svgPosXY = new Vector2(800, 380), svgSizeWH = new Vector2(70, 50),
                heightUnity = 9f, tint = new Color(0.72f, 0.65f, 0.56f)
            });

            // 서측 주거구
            buildings.Add(new BuildingDef {
                code = "RES-W1", displayName = "주거 W1",
                svgPosXY = new Vector2(180, 280), svgSizeWH = new Vector2(60, 60),
                heightUnity = 8f, tint = new Color(0.62f, 0.58f, 0.52f)
            });
            buildings.Add(new BuildingDef {
                code = "RES-W2", displayName = "주거 W2",
                svgPosXY = new Vector2(180, 470), svgSizeWH = new Vector2(60, 50),
                heightUnity = 8f, tint = new Color(0.66f, 0.60f, 0.54f)
            });
        }

        private void AppendPhaseC()
        {
            // ---- Roads (외곽 — 북측 진입로 + 호수/숲 접근로) ----
            roads.Add(new RoadDef {
                name = "NorthApproach", svgFromXY = new Vector2(500, 0), svgToXY = new Vector2(500, 200), widthSVG = 8f
            });
            roads.Add(new RoadDef {
                name = "WestOuterNS", svgFromXY = new Vector2(220, 200), svgToXY = new Vector2(220, 440), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "EastOuterEW", svgFromXY = new Vector2(800, 440), svgToXY = new Vector2(950, 440), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "LakeApproach", svgFromXY = new Vector2(500, 540), svgToXY = new Vector2(500, 720), widthSVG = 5f
            });

            // ---- Buildings (외곽 — 서측 산업/저항 구역) ----
            buildings.Add(new BuildingDef {
                code = "POI-04", displayName = "폐 임업소",
                svgPosXY = new Vector2(170, 280), svgSizeWH = new Vector2(60, 40),
                heightUnity = 10f, tint = new Color(0.50f, 0.46f, 0.42f)
            });
            buildings.Add(new BuildingDef {
                code = "SOVIET", displayName = "폐건물 (소비에트)",
                svgPosXY = new Vector2(180, 380), svgSizeWH = new Vector2(60, 45),
                heightUnity = 12f, tint = new Color(0.45f, 0.42f, 0.40f)
            });
            buildings.Add(new BuildingDef {
                code = "GARAGE", displayName = "차고",
                svgPosXY = new Vector2(260, 280), svgSizeWH = new Vector2(40, 30),
                heightUnity = 6f, tint = new Color(0.55f, 0.52f, 0.48f)
            });
            buildings.Add(new BuildingDef {
                code = "SUB-B", displayName = "카리 레토넨 집",
                svgPosXY = new Vector2(175, 480), svgSizeWH = new Vector2(30, 24),
                heightUnity = 7f, tint = new Color(0.68f, 0.60f, 0.50f)
            });

            // ---- Buildings (외곽 — 동측 / 농장) ----
            buildings.Add(new BuildingDef {
                code = "S-01", displayName = "할머니의 가판",
                svgPosXY = new Vector2(860, 380), svgSizeWH = new Vector2(22, 18),
                heightUnity = 4f, tint = new Color(0.78f, 0.70f, 0.58f)
            });
            buildings.Add(new BuildingDef {
                code = "S-03", displayName = "빈 학교",
                svgPosXY = new Vector2(850, 550), svgSizeWH = new Vector2(50, 32),
                heightUnity = 9f, tint = new Color(0.72f, 0.64f, 0.52f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM", displayName = "농장",
                svgPosXY = new Vector2(910, 400), svgSizeWH = new Vector2(32, 22),
                heightUnity = 5f, tint = new Color(0.66f, 0.58f, 0.46f)
            });

            // ---- Buildings (호수/숲 — 저항군 은신처 + 단서 마커) ----
            buildings.Add(new BuildingDef {
                code = "CH-04", displayName = "저항군 은신처",
                svgPosXY = new Vector2(265, 670), svgSizeWH = new Vector2(50, 40),
                heightUnity = 6f, tint = new Color(0.40f, 0.45f, 0.42f)
            });
            // 호수 위 마커들 — 낮은 큐브 (이정표 느낌)
            buildings.Add(new BuildingDef {
                code = "S-04", displayName = "얼어붙은 낚시꾼",
                svgPosXY = new Vector2(555, 665), svgSizeWH = new Vector2(10, 10),
                heightUnity = 0.5f, tint = new Color(0.85f, 0.88f, 0.92f)
            });
            buildings.Add(new BuildingDef {
                code = "S-08", displayName = "쪽지가 든 병",
                svgPosXY = new Vector2(745, 640), svgSizeWH = new Vector2(8, 8),
                heightUnity = 0.4f, tint = new Color(0.55f, 0.65f, 0.62f)
            });
        }

        // ---- Phase D: 건물별 접근 도로 (좁은 거리, 클러스터 연결) ----
        private void AppendPhaseD()
        {
            // 중심 — 경찰서/타코/식료품점
            roads.Add(new RoadDef { name = "AccessCenter_N", svgFromXY = new Vector2(470, 395), svgToXY = new Vector2(470, 440), widthSVG = 3.5f });
            roads.Add(new RoadDef { name = "AccessCenter_S", svgFromXY = new Vector2(456, 460), svgToXY = new Vector2(456, 488), widthSVG = 3.5f });
            // 교회/도서관
            roads.Add(new RoadDef { name = "AccessChurch", svgFromXY = new Vector2(405, 360), svgToXY = new Vector2(405, 430), widthSVG = 3.5f });
            roads.Add(new RoadDef { name = "AccessLib", svgFromXY = new Vector2(358, 460), svgToXY = new Vector2(358, 490), widthSVG = 3.5f });
            // 노르드만 / 아흐토
            roads.Add(new RoadDef { name = "AccessNordmann", svgFromXY = new Vector2(808, 300), svgToXY = new Vector2(808, 340), widthSVG = 4f });
            roads.Add(new RoadDef { name = "AccessAhto", svgFromXY = new Vector2(620, 300), svgToXY = new Vector2(720, 300), widthSVG = 4f });
            // 주거구 East 내부
            roads.Add(new RoadDef { name = "AccessResE_NS", svgFromXY = new Vector2(700, 380), svgToXY = new Vector2(700, 540), widthSVG = 3f });
            roads.Add(new RoadDef { name = "AccessResE_Inner", svgFromXY = new Vector2(540, 460), svgToXY = new Vector2(870, 460), widthSVG = 3f });
            // 주거구 West 내부
            roads.Add(new RoadDef { name = "AccessResW", svgFromXY = new Vector2(210, 310), svgToXY = new Vector2(210, 470), widthSVG = 3f });
            // 외곽 서측 산업
            roads.Add(new RoadDef { name = "AccessSawmill", svgFromXY = new Vector2(220, 280), svgToXY = new Vector2(260, 280), widthSVG = 3.5f });
            roads.Add(new RoadDef { name = "AccessSoviet", svgFromXY = new Vector2(220, 380), svgToXY = new Vector2(260, 380), widthSVG = 3.5f });
            roads.Add(new RoadDef { name = "AccessKari", svgFromXY = new Vector2(195, 480), svgToXY = new Vector2(220, 480), widthSVG = 3f });
            // 외곽 동측 / 농장
            roads.Add(new RoadDef { name = "AccessGrandma", svgFromXY = new Vector2(870, 380), svgToXY = new Vector2(900, 380), widthSVG = 3f });
            roads.Add(new RoadDef { name = "AccessSchool", svgFromXY = new Vector2(900, 540), svgToXY = new Vector2(900, 580), widthSVG = 3.5f });
            roads.Add(new RoadDef { name = "AccessFarm", svgFromXY = new Vector2(900, 411), svgToXY = new Vector2(940, 411), widthSVG = 3f });
            // 호수/저항군
            roads.Add(new RoadDef { name = "AccessResistance", svgFromXY = new Vector2(290, 620), svgToXY = new Vector2(290, 670), widthSVG = 3.5f });
        }

        // ---- Phase E: 폐허 마을 + 로봇폐기장 + 마렌의 집 (Eirinen 남쪽) ----
        private void AppendPhaseE()
        {
            // 연결 하이웨이 (Eirinen MainNS 남단 → Ruin MainEW까지 직통) — 끊김 없이 잇음
            roads.Add(new RoadDef {
                name = "Highway_S", svgFromXY = new Vector2(500, 800), svgToXY = new Vector2(500, 1500), widthSVG = 16f
            });
            // 폐허 ↔ 빈민가 연결 하이웨이 (Ruin MainEW 동단 ↔ Slum MainNS 남단)
            roads.Add(new RoadDef {
                name = "Highway_RuinSlum", svgFromXY = new Vector2(1100, 1500), svgToXY = new Vector2(2300, 1500), widthSVG = 16f
            });
            // 컨트리 대각선 — Eirinen 남서 → Ruin 북블록 진입로 (지름길 비포장)
            roads.Add(new RoadDef {
                name = "Diagonal_Country_NW", svgFromXY = new Vector2(220, 800), svgToXY = new Vector2(280, 1380), widthSVG = 6f
            });
            // 폐허 마을 메인 도로 (균열 표현은 비주얼 단계에서)
            roads.Add(new RoadDef {
                name = "Ruin_MainEW", svgFromXY = new Vector2(180, 1500), svgToXY = new Vector2(1100, 1500), widthSVG = 7f
            });
            roads.Add(new RoadDef {
                name = "Ruin_MainNS", svgFromXY = new Vector2(620, 1380), svgToXY = new Vector2(620, 1900), widthSVG = 7f
            });
            // 컨트리 대각선 — Ruin 동단(MainEW + Highway_RuinSlum 교차) → Slum 서측 중간
            roads.Add(new RoadDef {
                name = "Diagonal_Country_SE", svgFromXY = new Vector2(1100, 1500), svgToXY = new Vector2(1750, 1110), widthSVG = 6f
            });

            // ---- Highway_S 길가 흩뿌려진 집 (3채) ----
            buildings.Add(new BuildingDef {
                code = "FARM-HwyS-1", displayName = "외딴 농가",
                svgPosXY = new Vector2(380, 880), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.62f, 0.55f, 0.46f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyS-2", displayName = "외딴 농가",
                svgPosXY = new Vector2(570, 1020), svgSizeWH = new Vector2(35, 30),
                heightUnity = 5.5f, tint = new Color(0.58f, 0.50f, 0.42f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyS-3", displayName = "외딴 농가",
                svgPosXY = new Vector2(430, 1300), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.60f, 0.52f, 0.44f)
            });
            // ---- Highway_RuinSlum 길가 흩뿌려진 집 (3채) ----
            buildings.Add(new BuildingDef {
                code = "FARM-HwyRS-1", displayName = "외딴 농가",
                svgPosXY = new Vector2(1280, 1430), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.60f, 0.52f, 0.44f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyRS-2", displayName = "외딴 농가",
                svgPosXY = new Vector2(1700, 1540), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.58f, 0.50f, 0.42f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyRS-3", displayName = "외딴 농가",
                svgPosXY = new Vector2(2050, 1430), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.62f, 0.55f, 0.46f)
            });
            roads.Add(new RoadDef {
                name = "Ruin_JunkAccess", svgFromXY = new Vector2(620, 1340), svgToXY = new Vector2(1050, 1340), widthSVG = 5f
            });
            roads.Add(new RoadDef {
                name = "Ruin_HomeAccess", svgFromXY = new Vector2(380, 1700), svgToXY = new Vector2(380, 1830), widthSVG = 4f
            });
            roads.Add(new RoadDef {
                name = "Ruin_NorthBlock", svgFromXY = new Vector2(280, 1380), svgToXY = new Vector2(620, 1380), widthSVG = 4f
            });

            // 무너진 집들 (낮은 높이, 어두운 회갈색 톤)
            buildings.Add(new BuildingDef {
                code = "RUIN-01", displayName = "무너진 집",
                svgPosXY = new Vector2(280, 1300), svgSizeWH = new Vector2(60, 60),
                heightUnity = 4f, tint = new Color(0.42f, 0.40f, 0.38f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-02", displayName = "무너진 집",
                svgPosXY = new Vector2(440, 1300), svgSizeWH = new Vector2(70, 60),
                heightUnity = 3f, tint = new Color(0.38f, 0.36f, 0.34f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-03", displayName = "무너진 창고",
                svgPosXY = new Vector2(260, 1530), svgSizeWH = new Vector2(70, 50),
                heightUnity = 5f, tint = new Color(0.40f, 0.38f, 0.36f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-04", displayName = "무너진 집",
                svgPosXY = new Vector2(420, 1540), svgSizeWH = new Vector2(60, 50),
                heightUnity = 4f, tint = new Color(0.44f, 0.40f, 0.36f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-05", displayName = "무너진 집",
                svgPosXY = new Vector2(280, 1700), svgSizeWH = new Vector2(60, 60),
                heightUnity = 3.5f, tint = new Color(0.40f, 0.37f, 0.34f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-06", displayName = "무너진 집",
                svgPosXY = new Vector2(490, 1720), svgSizeWH = new Vector2(60, 50),
                heightUnity = 4f, tint = new Color(0.42f, 0.39f, 0.35f)
            });
            buildings.Add(new BuildingDef {
                code = "RUIN-07", displayName = "무너진 학교",
                svgPosXY = new Vector2(700, 1700), svgSizeWH = new Vector2(110, 60),
                heightUnity = 5f, tint = new Color(0.36f, 0.34f, 0.32f)
            });
            // 마렌의 집 — 폐허 외곽, 따뜻한 톤 (살아있는 집의 표시)
            buildings.Add(new BuildingDef {
                code = "MAREN-HOME", displayName = "마렌의 집",
                svgPosXY = new Vector2(330, 1830), svgSizeWH = new Vector2(50, 45),
                heightUnity = 7f, tint = new Color(0.72f, 0.55f, 0.40f)
            });

            // 로봇폐기장 — 다단 큰 더미 + 본관
            buildings.Add(new BuildingDef {
                code = "JUNK-MAIN", displayName = "로봇폐기장 본관",
                svgPosXY = new Vector2(890, 1240), svgSizeWH = new Vector2(120, 80),
                heightUnity = 11f, tint = new Color(0.30f, 0.30f, 0.32f)
            });
            buildings.Add(new BuildingDef {
                code = "JUNK-A", displayName = "폐기물 더미 A",
                svgPosXY = new Vector2(800, 1380), svgSizeWH = new Vector2(80, 50),
                heightUnity = 4f, tint = new Color(0.34f, 0.32f, 0.30f)
            });
            buildings.Add(new BuildingDef {
                code = "JUNK-B", displayName = "폐기물 더미 B",
                svgPosXY = new Vector2(900, 1450), svgSizeWH = new Vector2(70, 60),
                heightUnity = 5f, tint = new Color(0.32f, 0.30f, 0.28f)
            });
            buildings.Add(new BuildingDef {
                code = "JUNK-C", displayName = "폐기물 더미 C",
                svgPosXY = new Vector2(990, 1370), svgSizeWH = new Vector2(60, 50),
                heightUnity = 6f, tint = new Color(0.36f, 0.34f, 0.32f)
            });
            buildings.Add(new BuildingDef {
                code = "JUNK-D", displayName = "폐로봇 야적장",
                svgPosXY = new Vector2(820, 1560), svgSizeWH = new Vector2(150, 100),
                heightUnity = 2.5f, tint = new Color(0.38f, 0.36f, 0.34f)
            });
        }

        // ---- 강도 마을 (Eirinen 동쪽 — 빈민가 대체) ----
        // 산업 잔재(공장)를 점령한 약탈자 거점. 격자 X, 곡선/가지치기 도로 + 시설 위주 + 14채 집 흩어짐.
        private void AppendBanditVillage()
        {
            // 인터타운 큰 도로 (Eirinen 동단 → 강도 마을 입구)
            roads.Add(new RoadDef {
                name = "Highway_E", svgFromXY = new Vector2(1000, 440), svgToXY = new Vector2(1750, 440), widthSVG = 16f
            });
            // Highway_E 길가 흩뿌려진 외딴 농가 (3채)
            buildings.Add(new BuildingDef {
                code = "FARM-HwyE-1", displayName = "외딴 농가",
                svgPosXY = new Vector2(1130, 380), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.60f, 0.52f, 0.44f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyE-2", displayName = "외딴 농가",
                svgPosXY = new Vector2(1330, 480), svgSizeWH = new Vector2(35, 30),
                heightUnity = 5f, tint = new Color(0.58f, 0.50f, 0.42f)
            });
            buildings.Add(new BuildingDef {
                code = "FARM-HwyE-3", displayName = "외딴 농가",
                svgPosXY = new Vector2(1550, 390), svgSizeWH = new Vector2(35, 28),
                heightUnity = 5f, tint = new Color(0.62f, 0.55f, 0.46f)
            });

            // ---- 강도 마을 진입로 (불규칙 곡선) ----
            // 북쪽 진입 (Highway_E에서 분기, 약간 굽이지며 마을 안으로)
            roads.Add(new RoadDef { name = "Bandit_Entry_N", svgFromXY = new Vector2(1750, 440), svgToXY = new Vector2(1820, 700), widthSVG = 7f });
            // 남쪽 진입 (Highway_RuinSlum 동단 → 마을 남부)
            roads.Add(new RoadDef { name = "Bandit_Entry_S", svgFromXY = new Vector2(2300, 1500), svgToXY = new Vector2(2400, 1330), widthSVG = 7f });

            // ---- 메인 길 (가지치기 곡선, 격자 아님) ----
            roads.Add(new RoadDef { name = "Bandit_Main_1", svgFromXY = new Vector2(1820, 700), svgToXY = new Vector2(2050, 810), widthSVG = 6f });
            roads.Add(new RoadDef { name = "Bandit_Main_2", svgFromXY = new Vector2(2050, 810), svgToXY = new Vector2(2300, 800), widthSVG = 6f });
            roads.Add(new RoadDef { name = "Bandit_Main_3", svgFromXY = new Vector2(2300, 800), svgToXY = new Vector2(2500, 760), widthSVG = 5f });
            roads.Add(new RoadDef { name = "Bandit_Main_4", svgFromXY = new Vector2(2500, 760), svgToXY = new Vector2(2700, 580), widthSVG = 5f });
            roads.Add(new RoadDef { name = "Bandit_Main_5", svgFromXY = new Vector2(2700, 580), svgToXY = new Vector2(2700, 525), widthSVG = 5f });

            // ---- 분기 지선 (각 시설/구역 접근) ----
            roads.Add(new RoadDef { name = "Bandit_Br_Tower", svgFromXY = new Vector2(2500, 760), svgToXY = new Vector2(2520, 580), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Bandit_Br_S1", svgFromXY = new Vector2(2300, 800), svgToXY = new Vector2(2350, 1150), widthSVG = 5f });
            roads.Add(new RoadDef { name = "Bandit_Br_S2", svgFromXY = new Vector2(2350, 1150), svgToXY = new Vector2(2400, 1330), widthSVG = 5f });
            roads.Add(new RoadDef { name = "Bandit_Br_Junk", svgFromXY = new Vector2(2400, 1330), svgToXY = new Vector2(2640, 1300), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Bandit_Br_W1", svgFromXY = new Vector2(2050, 810), svgToXY = new Vector2(1900, 950), widthSVG = 5f });
            roads.Add(new RoadDef { name = "Bandit_Br_W2", svgFromXY = new Vector2(1900, 950), svgToXY = new Vector2(1900, 1240), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Bandit_Br_Radio", svgFromXY = new Vector2(1900, 950), svgToXY = new Vector2(1820, 870), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Bandit_Br_Store", svgFromXY = new Vector2(2050, 810), svgToXY = new Vector2(2050, 880), widthSVG = 4f });

            // ---- 시설 7개 (공장/주유소/수리/고물상/급수타워/라디오국/편의점) ----
            buildings.Add(new BuildingDef {
                code = "BANDIT-FACTORY", displayName = "공장",
                svgPosXY = new Vector2(2700, 400), svgSizeWH = new Vector2(150, 120),
                heightUnity = 12f, tint = new Color(0.40f, 0.38f, 0.35f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-GAS", displayName = "주유소",
                svgPosXY = new Vector2(2120, 670), svgSizeWH = new Vector2(50, 35),
                heightUnity = 4f, tint = new Color(0.65f, 0.60f, 0.50f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-PUMP", displayName = "주유 펌프",
                svgPosXY = new Vector2(2150, 720), svgSizeWH = new Vector2(30, 15),
                heightUnity = 2f, tint = new Color(0.50f, 0.50f, 0.52f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-REPAIR", displayName = "차량 수리점",
                svgPosXY = new Vector2(2440, 800), svgSizeWH = new Vector2(80, 55),
                heightUnity = 6f, tint = new Color(0.50f, 0.42f, 0.36f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-JUNK", displayName = "고물상",
                svgPosXY = new Vector2(2480, 1230), svgSizeWH = new Vector2(80, 70),
                heightUnity = 5f, tint = new Color(0.42f, 0.40f, 0.36f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-JUNKPILE-A", displayName = "폐자재 더미",
                svgPosXY = new Vector2(2580, 1180), svgSizeWH = new Vector2(50, 30),
                heightUnity = 2.5f, tint = new Color(0.38f, 0.36f, 0.32f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-JUNKPILE-B", displayName = "폐자재 더미",
                svgPosXY = new Vector2(2380, 1230), svgSizeWH = new Vector2(40, 40),
                heightUnity = 2f, tint = new Color(0.40f, 0.38f, 0.34f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-WATER", displayName = "급수타워",
                svgPosXY = new Vector2(2510, 530), svgSizeWH = new Vector2(25, 25),
                heightUnity = 18f, tint = new Color(0.60f, 0.30f, 0.25f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-RADIO", displayName = "라디오국",
                svgPosXY = new Vector2(1780, 830), svgSizeWH = new Vector2(45, 35),
                heightUnity = 8f, tint = new Color(0.45f, 0.45f, 0.40f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-ANTENNA", displayName = "라디오 안테나",
                svgPosXY = new Vector2(1830, 905), svgSizeWH = new Vector2(6, 6),
                heightUnity = 22f, tint = new Color(0.30f, 0.30f, 0.32f)
            });
            buildings.Add(new BuildingDef {
                code = "BANDIT-STORE", displayName = "편의점",
                svgPosXY = new Vector2(2010, 870), svgSizeWH = new Vector2(40, 30),
                heightUnity = 4f, tint = new Color(0.70f, 0.60f, 0.40f)
            });

            // ---- 14채 집 (불규칙 위치, 산만하게 흩어짐) ----
            AddBanditHouse("01", 1850, 750, 40, 30, 4.5f, 0);
            AddBanditHouse("02", 1880, 1010, 45, 35, 5f, 1);
            AddBanditHouse("03", 1820, 1100, 40, 35, 4.5f, 2);
            AddBanditHouse("04", 1810, 1240, 50, 35, 5f, 3);
            AddBanditHouse("05", 1980, 1100, 45, 40, 5f, 4);
            AddBanditHouse("06", 2110, 990, 40, 30, 4.5f, 5);
            AddBanditHouse("07", 2210, 990, 45, 35, 5f, 0);
            AddBanditHouse("08", 2080, 1180, 50, 35, 5f, 1);
            AddBanditHouse("09", 2210, 1190, 45, 40, 5f, 2);
            AddBanditHouse("10", 2400, 1180, 40, 35, 4.5f, 3);
            AddBanditHouse("11", 2400, 880, 45, 30, 5f, 4);
            AddBanditHouse("12", 2620, 870, 40, 30, 4.5f, 5);
            AddBanditHouse("13", 2640, 700, 50, 35, 5f, 0);
            AddBanditHouse("14", 2620, 560, 40, 30, 5f, 1);
        }

        // 강도 마을 집 헬퍼 — 6색 팔레트 + 높이/폭 약간 변주.
        private void AddBanditHouse(string id, float x, float y, float w, float d, float h, int paletteIdx)
        {
            Color[] palette = new Color[] {
                new Color(0.55f, 0.48f, 0.40f), // brown
                new Color(0.50f, 0.45f, 0.40f), // gray-brown
                new Color(0.48f, 0.42f, 0.36f), // dark brown
                new Color(0.55f, 0.42f, 0.36f), // rust brown
                new Color(0.45f, 0.42f, 0.40f), // gray
                new Color(0.50f, 0.38f, 0.32f)  // rust
            };
            buildings.Add(new BuildingDef {
                code = $"BANDIT-HOUSE-{id}",
                displayName = "강도 마을 집",
                svgPosXY = new Vector2(x, y),
                svgSizeWH = new Vector2(w, d),
                heightUnity = h + (paletteIdx % 3) * 0.3f,
                tint = palette[paletteIdx % palette.Length]
            });
        }

        // ---- 인스턴스 데이터에서 빈민가 제거 + 강도 마을 추가 (사용자 수정 보존) ----
        // 기존 데이터의 SLUM-* / Slum_* / Alley_* / DRONE-HUB 등만 정확히 제거.
        // Highway_E + FARM-HwyE-* 도 강도 마을이 새로 만드므로 같이 청소.
        [ContextMenu("Replace Slum with Bandit Village")]
        private void ReplaceSlumWithBandit()
        {
            int rRem = roads.RemoveAll(r => r != null && (
                r.name.StartsWith("Slum_") ||
                r.name.StartsWith("Alley_") ||
                r.name == "Highway_E"
            ));
            int bRem = buildings.RemoveAll(b => b != null && (
                b.code.StartsWith("SLUM-") ||
                b.code == "DRONE-HUB" ||
                b.code.StartsWith("FARM-HwyE-")
            ));
            Debug.Log($"[CityDataSO] Slum cleanup: {rRem} roads, {bRem} buildings removed.");

            // 이미 강도 마을이 추가됐으면 skip (이중 추가 방지)
            if (buildings.Exists(b => b != null && b.code == "BANDIT-FACTORY"))
            {
                Debug.Log("[CityDataSO] Bandit village already present.");
                return;
            }
            AppendBanditVillage();
        }

        // ---- 휴게소 (3개 마을 삼각형 중앙) ----
        // 위치: SVG ~(1100~1300, 870~1010). 3개 마을 도심 사이의 중심부.
        // 연결: Highway_E에서 분기되는 짧은 NS 도로 + 작은 사각 루프.
        // 추가만 하므로 기존 도시 데이터 유지됨.
        private void AppendRestStop()
        {
            // Highway_E에서 남쪽으로 분기되는 진입 도로
            roads.Add(new RoadDef {
                name = "Rest_Connector", svgFromXY = new Vector2(1200, 440), svgToXY = new Vector2(1200, 870), widthSVG = 6f
            });
            // 휴게소 작은 루프 도로 (사각형)
            roads.Add(new RoadDef { name = "Rest_Loop_N", svgFromXY = new Vector2(1100, 870), svgToXY = new Vector2(1300, 870), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Rest_Loop_S", svgFromXY = new Vector2(1100, 1010), svgToXY = new Vector2(1300, 1010), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Rest_Loop_W", svgFromXY = new Vector2(1100, 870), svgToXY = new Vector2(1100, 1010), widthSVG = 4f });
            roads.Add(new RoadDef { name = "Rest_Loop_E", svgFromXY = new Vector2(1300, 870), svgToXY = new Vector2(1300, 1010), widthSVG = 4f });

            // 주유소 본관 (캐노피 + 매점)
            buildings.Add(new BuildingDef {
                code = "REST-GAS", displayName = "주유소",
                svgPosXY = new Vector2(1115, 895), svgSizeWH = new Vector2(50, 35),
                heightUnity = 4f, tint = new Color(0.78f, 0.74f, 0.66f)
            });
            // 주유 펌프 아일랜드 (낮은 큐브)
            buildings.Add(new BuildingDef {
                code = "REST-PUMP", displayName = "주유 펌프",
                svgPosXY = new Vector2(1145, 945), svgSizeWH = new Vector2(30, 15),
                heightUnity = 2f, tint = new Color(0.55f, 0.55f, 0.58f)
            });
            // 모텔 (긴 형태, 동쪽)
            buildings.Add(new BuildingDef {
                code = "REST-MOTEL", displayName = "모텔",
                svgPosXY = new Vector2(1190, 895), svgSizeWH = new Vector2(80, 40),
                heightUnity = 5f, tint = new Color(0.68f, 0.55f, 0.45f)
            });
            // 외딴 집 (관리인/투숙객 사용)
            buildings.Add(new BuildingDef {
                code = "REST-HOUSE", displayName = "쉼터 집",
                svgPosXY = new Vector2(1115, 965), svgSizeWH = new Vector2(40, 30),
                heightUnity = 4.5f, tint = new Color(0.62f, 0.55f, 0.46f)
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
