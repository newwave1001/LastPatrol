# LAST PATROL — Unity 프로젝트 폴더 구조 v0.1

**목적**: 모든 기획 문서, 참조 자료, 코드, 에셋이 어디에 들어갈지 명확히

---

## 1. 전체 구조

```
LastPatrol/                              ← 프로젝트 루트
├─ Assets/                               ← 유니티 자산
│  ├─ _Project/                          ← 우리 프로젝트 (밑줄 prefix)
│  │  ├─ Core/
│  │  │  ├─ Input/
│  │  │  │  ├─ InputReader.cs
│  │  │  │  └─ LastPatrolInputs.inputactions
│  │  │  ├─ GameManager.cs
│  │  │  ├─ SceneController.cs
│  │  │  └─ SaveSystem.cs
│  │  │
│  │  ├─ Characters/
│  │  │  ├─ Maren/
│  │  │  │  ├─ MarenController.cs
│  │  │  │  ├─ CoverSystem.cs
│  │  │  │  ├─ CharacterMovement.cs
│  │  │  │  ├─ InteractionSystem.cs
│  │  │  │  └─ P_Maren.prefab
│  │  │  ├─ M07/
│  │  │  │  ├─ M07Controller.cs
│  │  │  │  ├─ TurretController.cs
│  │  │  │  ├─ RobotEyes.cs
│  │  │  │  ├─ FollowBehavior.cs
│  │  │  │  └─ P_M07.prefab
│  │  │  └─ Enemies/
│  │  │     ├─ EnemyAI.cs              ← 베이스
│  │  │     ├─ HumanoidAI.cs
│  │  │     ├─ DroneAI.cs
│  │  │     ├─ AmbushAI.cs
│  │  │     ├─ CuratorAI.cs            ← 보스
│  │  │     ├─ HunterAI.cs             ← 특수 고속
│  │  │     └─ Prefabs/
│  │  │        ├─ P_Enemy_Humanoid.prefab
│  │  │        ├─ P_Enemy_Drone.prefab
│  │  │        ├─ P_Enemy_Curator.prefab
│  │  │        └─ P_Enemy_Hunter.prefab
│  │  │
│  │  ├─ Systems/
│  │  │  ├─ Dialogue/
│  │  │  │  ├─ DialogueSystem.cs
│  │  │  │  ├─ DialoguePanelUI.cs
│  │  │  │  ├─ TypewriterEffect.cs
│  │  │  │  └─ ProfileRenderer.cs
│  │  │  ├─ Investigation/
│  │  │  │  ├─ InvestigationSystem.cs
│  │  │  │  ├─ ClueObject.cs
│  │  │  │  ├─ HiddenDoor.cs
│  │  │  │  └─ CaseBoard.cs            ← S10 사건 보드
│  │  │  ├─ Combat/
│  │  │  │  ├─ Bullet.cs
│  │  │  │  ├─ Cover.cs
│  │  │  │  ├─ DistanceRule.cs
│  │  │  │  ├─ BatterySystem.cs
│  │  │  │  └─ DamageReceiver.cs
│  │  │  ├─ Encounter/
│  │  │  │  ├─ EncounterDirector.cs
│  │  │  │  └─ AmbushSpawner.cs
│  │  │  ├─ Vehicle/
│  │  │  │  ├─ CarController.cs
│  │  │  │  ├─ CarHP.cs
│  │  │  │  └─ EnterExitVehicle.cs
│  │  │  └─ World/
│  │  │     ├─ WindowLight.cs          ← "살아남은 창문"
│  │  │     ├─ DayNightCycle.cs
│  │  │     └─ WeatherSystem.cs        ← 눈, 눈보라
│  │  │
│  │  ├─ Data/                          ← ScriptableObject 정의 + 인스턴스
│  │  │  ├─ Definitions/                ← SO 클래스 정의
│  │  │  │  ├─ EnemyDataSO.cs
│  │  │  │  ├─ DialogueLineSO.cs
│  │  │  │  ├─ DialogueSequenceSO.cs
│  │  │  │  ├─ CaseDataSO.cs
│  │  │  │  ├─ ClueDataSO.cs
│  │  │  │  └─ EncounterDataSO.cs
│  │  │  ├─ Enemies/                    ← .asset 인스턴스들
│  │  │  │  ├─ ED_Humanoid_Basic.asset
│  │  │  │  ├─ ED_Drone_Basic.asset
│  │  │  │  ├─ ED_Curator.asset
│  │  │  │  └─ ED_Hunter.asset
│  │  │  ├─ Dialogue/
│  │  │  │  ├─ CH1/
│  │  │  │  │  ├─ DL_CH1_Intro_Maren.asset
│  │  │  │  │  ├─ DL_CH1_BloodFound.asset
│  │  │  │  │  └─ ...
│  │  │  │  ├─ CH2/
│  │  │  │  └─ ...
│  │  │  ├─ Cases/
│  │  │  │  ├─ C_CH1_Nordman.asset
│  │  │  │  ├─ C_CH2_Tako.asset
│  │  │  │  ├─ C_SubA_Mia.asset
│  │  │  │  └─ ...
│  │  │  └─ Clues/
│  │  │     ├─ CL_CH1_DriedBlood.asset
│  │  │     ├─ CL_CH1_Note.asset
│  │  │     └─ ...
│  │  │
│  │  ├─ Scenes/
│  │  │  ├─ S00_MainMenu/
│  │  │  │  └─ S00_MainMenu.unity
│  │  │  ├─ S01_OutdoorDriving/
│  │  │  │  └─ S01_OutdoorDriving.unity
│  │  │  ├─ S02_OutdoorEncounter/
│  │  │  │  └─ S02_OutdoorEncounter.unity
│  │  │  ├─ S03_IndoorInvestigation/    ← 첫 작업
│  │  │  │  └─ S03_IndoorInvestigation.unity
│  │  │  └─ S04_RobotBattery/
│  │  │     └─ S04_RobotBattery.unity
│  │  │
│  │  ├─ Art/
│  │  │  ├─ Materials/
│  │  │  │  ├─ M_Snow.mat
│  │  │  │  ├─ M_Maren_Coat.mat
│  │  │  │  └─ ...
│  │  │  ├─ Textures/
│  │  │  │  ├─ T_Snow_Albedo.png
│  │  │  │  └─ ...
│  │  │  ├─ Models/
│  │  │  │  └─ (커스텀 모델, 에셋 패키지는 _ThirdParty)
│  │  │  ├─ Shaders/
│  │  │  │  └─ ToonSnow.shadergraph
│  │  │  ├─ VFX/
│  │  │  │  ├─ Snow_Particles.prefab
│  │  │  │  ├─ MuzzleFlash.prefab
│  │  │  │  ├─ BatteryStream.prefab     ← 시안 입자 흐름
│  │  │  │  └─ ...
│  │  │  └─ UI/
│  │  │     ├─ DialoguePanel.uxml
│  │  │     ├─ HUD.uxml
│  │  │     └─ Sprites/
│  │  │
│  │  ├─ Audio/
│  │  │  ├─ Music/
│  │  │  │  └─ Talvi.wav                 ← 노아의 곡
│  │  │  ├─ SFX/
│  │  │  │  ├─ Footstep_Snow.wav
│  │  │  │  ├─ Robot_Fire.wav
│  │  │  │  └─ ...
│  │  │  └─ Voice/                       ← (선택) 보이스
│  │  │
│  │  └─ Editor/                         ← 에디터 확장
│  │     ├─ DialogueLineSOEditor.cs
│  │     └─ CaseBoardWindow.cs
│  │
│  ├─ _ThirdParty/                       ← 외부 에셋 (밑줄로 _Project와 같이 위)
│  │  ├─ Synty/
│  │  │  ├─ POLYGON_Town/
│  │  │  ├─ POLYGON_Snow/
│  │  │  └─ POLYGON_SciFi/
│  │  ├─ Mixamo/
│  │  │  ├─ Maren_Idle.fbx
│  │  │  ├─ Maren_Walk.fbx
│  │  │  └─ ...
│  │  ├─ Quaternius/
│  │  │  └─ Robots_Pack/
│  │  └─ DOTween/
│  │
│  ├─ Plugins/                           ← 네이티브 플러그인 (Steamworks 등)
│  │
│  └─ Scenes/                            ← 유니티 기본, 사용 안 함 (삭제 가능)
│
├─ Docs/                                 ← 모든 기획·디자인 문서
│  ├─ Bible/
│  │  ├─ last_patrol_bible_v03.md       ← 메인 바이블
│  │  ├─ last_patrol_bible_v02.md       ← 이전 버전 (참조용)
│  │  └─ last_patrol_bible_v01.md
│  │
│  ├─ Story/
│  │  ├─ last_patrol_synopsis_main_v01.md
│  │  ├─ last_patrol_synopsis_scenes_v01.md  ← 장면 단위 시놉시스 (가장 상세)
│  │  ├─ last_patrol_subplot_matrix_v01.md   ← 서브 플롯 분기
│  │  ├─ last_patrol_npc_profiles_v01.md
│  │  └─ last_patrol_noah_matrix_v01.md      ← 노아 흔적
│  │
│  ├─ Design/
│  │  ├─ last_patrol_story_structure_v01.md
│  │  ├─ last_patrol_map_design_v01.md
│  │  └─ last_patrol_gdd_v02.docx
│  │
│  ├─ Architecture/
│  │  ├─ last_patrol_unity_architecture_v01.md
│  │  ├─ last_patrol_unity_greybox_guide_v01.md
│  │  └─ last_patrol_unity_folder_structure_v01.md  ← 이 문서
│  │
│  ├─ Reference/                         ← 게임 로직 참조용
│  │  ├─ cop_robot_coop_prototype_v11.html
│  │  ├─ city_patrol_mockup_v04.html
│  │  └─ eirinen_master_map_v01.html
│  │
│  └─ Logs/
│     ├─ WEEKLY_LOG.md                   ← 매주 진행 기록
│     └─ DECISIONS.md                    ← 주요 결정 기록
│
├─ Builds/                               ← 빌드 출력 (.gitignore)
│  └─ Windows/
│
├─ ProjectSettings/
├─ Packages/
├─ .gitignore
├─ .gitattributes                        ← LFS 설정
├─ CLAUDE.md                             ← Claude 페어 코딩용 프로젝트 가이드
└─ README.md                             ← 프로젝트 개요 (선택)
```

---

## 2. 문서 배치 매핑

### 2.1 Bible (세계관·캐릭터 정수)
- `Docs/Bible/last_patrol_bible_v03.md` — 모든 결정의 출발점

### 2.2 Story (이야기 자료)
- `Docs/Story/last_patrol_synopsis_main_v01.md` — 챕터 요약
- `Docs/Story/last_patrol_synopsis_scenes_v01.md` — **장면 단위 시놉시스, 작업 시 가장 자주 참조**
- `Docs/Story/last_patrol_subplot_matrix_v01.md` — 서브 플롯 분기
- `Docs/Story/last_patrol_npc_profiles_v01.md` — NPC 캐릭터
- `Docs/Story/last_patrol_noah_matrix_v01.md` — 노아의 흔적 (가장 섬세)

### 2.3 Design (디자인 문서)
- `Docs/Design/last_patrol_story_structure_v01.md` — 10시간 구조
- `Docs/Design/last_patrol_map_design_v01.md` — 마을 지도 설계

### 2.4 Architecture (이번에 추가)
- `Docs/Architecture/last_patrol_unity_architecture_v01.md` — 컴포넌트 설계
- `Docs/Architecture/last_patrol_unity_greybox_guide_v01.md` — 1주차 시작 가이드
- `Docs/Architecture/last_patrol_unity_folder_structure_v01.md` — 이 문서

### 2.5 Reference (코드 참조용)
- `Docs/Reference/cop_robot_coop_prototype_v11.html` — **씬 3 이식 시 참조**
- `Docs/Reference/city_patrol_mockup_v04.html` — 씬 1, 2 이식 시 참조
- `Docs/Reference/eirinen_master_map_v01.html` — 마을 전체 배치 참조

---

## 3. 명명 규칙 정리

### 3.1 폴더
- `_Project/` — 우리 코드 (밑줄)
- `_ThirdParty/` — 에셋 (밑줄)
- 나머지 표준 유니티 폴더는 그대로

### 3.2 C# 클래스
- 컴포넌트: `MarenController.cs`
- ScriptableObject 정의: `EnemyDataSO.cs` (SO 접미사)
- 인터페이스: `IDamageable.cs`, `IInteractable.cs`
- 에디터: `Editor/CaseBoardWindow.cs` (Editor 폴더 안)

### 3.3 자산 prefix
- `P_` — Prefab (`P_Maren.prefab`, `P_Enemy_Humanoid.prefab`)
- `M_` — Material (`M_Snow.mat`)
- `T_` — Texture (`T_Snow_Albedo.png`)
- `SM_` — Static Mesh (`SM_House_01.fbx`)
- `SK_` — Skeletal Mesh (`SK_Maren.fbx`)
- `A_` — Animation (`A_Maren_Walk.anim`)
- `S_` — Sprite (`S_Icon_Battery.png`)
- `VFX_` — Visual Effect (`VFX_BatteryStream.prefab`)
- `SFX_` — Sound Effect (`SFX_FootstepSnow.wav`)

### 3.4 ScriptableObject 인스턴스 prefix
- `ED_` — EnemyData (`ED_Humanoid_Basic.asset`)
- `DL_` — DialogueLine (`DL_CH1_Intro_Maren.asset`)
- `DS_` — DialogueSequence
- `C_` — Case (`C_CH1_Nordman.asset`)
- `CL_` — Clue (`CL_CH1_DriedBlood.asset`)
- `EN_` — Encounter

### 3.5 씬
- `S00_`, `S01_`, ... — 번호 prefix (탐색 정렬용)
- 영문만, snake_case 또는 PascalCase 일관

---

## 4. 작업 시작 체크리스트

Cowork로 전환 후 첫 작업:

- [ ] 폴더 만들기: `LastPatrol/Docs/Bible/`, `Story/`, `Design/`, `Architecture/`, `Reference/`, `Logs/`
- [ ] 모든 .md 문서를 위 폴더로 분류해서 복사
- [ ] HTML 참조 파일들을 `Docs/Reference/`로 복사
- [ ] `CLAUDE.md`를 프로젝트 루트에 복사
- [ ] Unity Hub에서 새 프로젝트 생성 (3D URP, 이름 LastPatrol)
- [ ] `.gitignore`, `.gitattributes` 작성
- [ ] Git 초기화, GitHub Private 리포 push
- [ ] 첫 커밋 메시지: "Initial setup with bible and architecture docs"

---

## 5. Cowork에서 Claude와 작업 시 워크플로우

1. Jerry가 작업 시작 시 Claude에게 컨텍스트 제공:
   - "지금 씬 3 작업 중이야. v11 HTML의 updateCop 로직을 MarenController.cs로 옮기고 싶어"
   - Claude가 `Docs/Reference/cop_robot_coop_prototype_v11.html`과 `Docs/Architecture/last_patrol_unity_architecture_v01.md`를 자동으로 참조

2. Claude가 코드 작성 시:
   - 적절한 위치 (`Assets/_Project/Characters/Maren/`)에 파일 생성
   - 명명 규칙 준수
   - 주석 한국어로 (Jerry 작업 편의)

3. Jerry가 유니티에서 통합:
   - 컴포넌트 추가, 인스펙터 설정
   - 그레이박스에 적용
   - 플레이 테스트

4. 매주 금요일 `WEEKLY_LOG.md` 업데이트, 다음 주 목표 설정

---

## 6. 다음 단계 (이 문서 다음에 할 일)

- [ ] 유니티 설치 + 프로젝트 생성
- [ ] 폴더 구조 만들기 (이 문서 그대로)
- [ ] 모든 문서 분류해서 배치
- [ ] CLAUDE.md 루트에 두기
- [ ] Git + LFS 셋업
- [ ] 첫 그레이박스 씬 (`S03_IndoorInvestigation.unity`) 만들기
- [ ] Cowork로 전환해서 Claude와 첫 코드 작성

---

*v0.1 — Cowork 전환 시점 기준 폴더·문서 구조 확정.*
