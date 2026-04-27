# LAST PATROL — Unity Project

3D 누아르 수사 호위 슈터. 라플란드 가상 마을 에이리넨 배경. 솔로 개발 (Jerry) + Claude 페어 코딩.

## What This Is

여경 마렌과 해킹 면역 로봇 M-07이 ELI(슈퍼컴퓨터)에 장악된 세계에서 사건을 수사하는 게임. 장르: 누아르 수사 × 호위 슈터 × 드라이빙 인카운터.

**현재 단계**: 코어 씬 1·3 통합 그레이박스 사이클 완성 + 외부 전투 메카닉(인카운터/하차/차량 강탈/엄폐) 작동.

코어 씬:
1. **외부 자동차 운전** (직부감) — 그레이박스 + 도시 마스터 지도(Phase A) + dispatch + 인카운터 + 하차 도보 + 차량 강탈 ✅
2. **외부 인카운터 전투** (운전 중 적 조우 → 하차 도보) — Phase 2-B 6 sub 통합 ✅
3. **실내 수사** (2.5D 사이드뷰 + 깊이) — v12 inner voice + 단서·선택지·롤 ✅
4. 쓰러진 로봇 배터리 수거 — 미구현

## Engine & Stack

- **Unity 6 LTS** + URP (Universal Render Pipeline)
- **C#** (Claude 페어 코딩 친화)
- **Cinemachine 3.x** (S03 카메라)
- **New Input System** (Player + Drive 액션맵)
- **TextMeshPro** (UI 텍스트, 본고딕 SDF)
- **ProBuilder** (그레이박스)

## Build & Test

- 에디터 플레이: Play 버튼 (Ctrl+P)
- 빌드: File → Build Profiles → Windows
- 빌드 출력 경로: `Builds/Windows/`
- Build Settings의 Scene List에 S01, S03 등록 필수

## Key Mapping

| 키 | 액션 | 컨텍스트 |
|---|---|---|
| WASD / 화살표 | Move (Foot) / Drive (Drive) | 양쪽 |
| F | Mount/Exit (차량 승/하차) — ON SCENE 시 씬 진입 | 양쪽 |
| E | Interact (단서 조사 / 차량 인터랙션 / 사건 현장 진입) | Foot |
| Tab | Switch (마렌 ↔ M-07 캐릭터 전환) | Foot |
| C | Board (사건 보드 토글) | Foot |
| L | Headlight 토글 | Drive |
| Shift | Boost (부스트) | Drive |
| Space | Jump (마렌 점프) | Foot |
| R | Charge (M-07 충전) | Foot |
| Mouse Left | Cover / Fire | Foot |
| Esc | Pause | 양쪽 |

## Architecture

### 핵심 시스템 분리 원칙

각 코어 씬은 **독립 Scene**으로 시작하지만 공통 시스템을 공유:
- `Assets/_Project/Core/` — 공통 (Input, IDamageable, IInteractable)
- `Assets/_Project/Scenes/` — 씬별 자산
- `Assets/_Project/Characters/` — 마렌, M-07, 적 prefab
- `Assets/_Project/Data/` — ScriptableObject (대사, 사건, 적 능력치, 도시)
- `Assets/_Project/Systems/` — 각 게임 시스템 (Vehicle, Encounter, Dispatch, UI 등)
- `Assets/_Project/Editor/` — Editor 도구 (DialogueCsvTools 등)

### 핵심 컴포넌트

**캐릭터:**
- `MarenController` — 입력·이동·엄폐·충전·조사·점프 + ControlMode (Manual / Cower)
- `M07Controller` — 자동 사격(TurretController)·따라가기·배터리 + ControlMode (Follow / Manual)
- `FollowBehavior` — M-07 호위 추종 + SteeringHelper 회피
- `EnemyAI` — 인간형/드론/큐레이터/헌터/앰부시 공통 베이스 (실내 보병)
- `EnemyVehicle` — 외부 적 차량, chase + 박치기 + lifetime/flee
- `TurretController` — M-07 자동 사격, IDamageable + faction 기반 적 인식

**입력 / 모드:**
- `InputReader` — Player + Drive 액션맵, Lock 시스템 (대사 중 입력 차단)
- `PartyController` — Tab 마렌/M-07 전환 (Foot 모드 전용)

**대화 / 수사:**
- `DialogueSystem` — 하단 좌측 패널, 타이프라이터, OnQueueEnded, 클릭/Space/Enter 스킵, Lock 통합
- `InvestigationSystem` — 사건 진행, 단서 발견 (F→E), v12 inner voice 흐름
- `VoiceFlowController` — voice 라인 + 선택지 모달 + 스킬 롤
- `CaseBoardUI` — C 키 사건 보드 토글
- `InteractionPromptUI` — `[E] 조사 · {라벨}` 화면 표시 (자동 빌드)

**외부 운전 / 도시:**
- `CarController` — v04 mockup 차량 물리, WorldRelative 조향, BrakeToStop, 침투 분리, IsWreck
- `TopDownCarCamera` — 직부감 ↔ 쿼터뷰 모드 전환 (Drive 78° / Foot 40°), lookahead, 보간
- `Headlights` — L 토글 + 활성 차량만 응답, 자동 ON/OFF (mount/dismount/switch)
- `VehicleHealth` — IDamageable, isEnemy faction, OnDeath
- `VehicleDismount` — F 자유 하차, 점진 감속, M-07 동행, 카메라 쿼터뷰, 차량 자동 토글
- `ParkedVehicle` — 도시 다른 차 IInteractable, 자유 교체 (마렌 시작 차도 부착)
- `EncounterSpawner` — hunted 상태 + 헤드라이트 ON 시 적 스폰 (5게임초)
- `DispatchSystem` — 사건 호출 사이클 (STANDBY → EN ROUTE → NEAR → ON SCENE), 활성 위치 거리 체크
- `CaseMarker` — 사건 현장 IInteractable + Trigger collider 자동 부착
- `CaseExitController` — 사건 완료 → S01 복귀 (DialogueSystem.OnQueueEnded 대기)
- `CityBuilder` + `CityDataSO` — eirinen master map 좌표 자동 큐브 빌드 (Phase A 완료)
- `OutdoorPartyCamera` — 외부 활성 캐릭터 따라 TopDownCarCamera target 토글
- `PartyCamera` — 실내 Cinemachine target 토글

**글로벌 / 흐름:**
- `ActiveCase` — 정적, 현재 사건 SO (씬 전환 시 살아남음)
- `PlayerStatus` — IsHunted, CompletedCases, HasCompleted
- `PlayerSpawnPoint` — 차량 위치 1회 텔레포트 (S03 → S01 복귀 시 사건 현장 앞)
- `SceneTransitionService` — DontDestroyOnLoad 페이드 + 로드
- `GameClock` — 게임 시간 진행 (1초 = 실시간 0.1초, 라플란드 짧은 낮)
- `DriveHUD` — TMP Canvas, 속도/시계/dispatch/HP/HUNTED, SetCar/SetPrompt
- `SteeringHelper` — 7방향 raycast 회피 (EnemyVehicle/M-07 공유)

**Editor 도구:**
- `DialogueCsvTools` — Tools → LastPatrol → Localization (Export/Import CSV)

## Conventions

### 폴더 명명
- `_Project/` — 우리 프로젝트 자산만 (밑줄 prefix로 항상 맨 위)
- `_ThirdParty/` — 에셋 스토어 패키지
- 절대 `Assets/` 루트에 자산 두지 않음

### 스크립트 명명
- 컴포넌트: `MarenController.cs`, `EnemyAI.cs` (PascalCase)
- ScriptableObject: `DialogueLineSO.cs`, `EnemyDataSO.cs` (PascalCase + SO 접미사)
- 인터페이스: `IDamageable`, `IInteractable` (I prefix)
- 에디터 도구: `Editor/MarenInspector.cs` (Editor 폴더 안, `#if UNITY_EDITOR` 가드)

### 자산 명명
- 모델: `Maren_Body.fbx`, `M07_Robot.fbx` (캐릭터_파트)
- 머티리얼: `M_Snow.mat`, `M_Maren_Coat.mat` (M_ prefix)
- 텍스처: `T_Snow_Albedo.png`, `T_Maren_Diffuse.png` (T_ prefix)
- 프리팹: `P_Maren.prefab`, `P_Car_Temp.prefab`, `P_ParkedCar_Temp.prefab` (P_ prefix)
- ScriptableObject 인스턴스: `C_CH1_Nordman.asset`, `DL_CH1_Intro.asset`, `CL_CH1_Body.asset`

### 한글 절대 사용 금지
- 파일명 영문만
- 인게임 텍스트는 ScriptableObject로 관리, 한국어/영어 분리 (DialogueLineSO.textKR/textEN)
- 폴더, 씬, 프리팹, 머티리얼 모두 영문

### Tone Reference

라플란드 겨울 분위기:
- 푸른 황혼 (긴 저녁), 짧은 낮 (2시간)
- 영하 21도 추위, 눈 덮인 풍경
- 따뜻한 창문 빛 vs 차가운 눈
- 오로라는 결정적 장면에만

색상 팔레트 (URP 톤):
- 페이퍼: #F5EEE0 (UI 배경)
- 잉크: #3A2E28 (텍스트)
- 시안: #7CC8D8 (M-07 식별)
- 앰버: #D88A4A (마렌 식별)
- 블러드: #A8302A (피, 위험)
- 스노우: #E8EDF0 (눈 면)

## 게임플레이 규칙 (Do / Don't)

### 마렌
- **점프 가능** (Space) — 실내·외부 모두
- **무기 없음** — 적 사격 X. 외부 전투에선 도주 + 차량 뒤 엄폐만
- **엄폐 (좌클릭 홀드)** — 가까운 Cover로 자동 스냅. 차량 자식 VehicleCover에도 작동
- **차량 뒤 엄폐 중 차 파괴 시 사망** (Sub-F)
- **충전 (R 홀드, M-07 근처)** — 실내 메카닉

### M-07
- **자동 사격 (TurretController)** — 반경 내 IDamageable 적 자동 조준 (faction 기반)
- **점프 없음** — 직무적
- **이동 (Manual 모드)** — Tab 활성 시 WASD 직접 조종
- **Follow 모드** — 마렌 호위, SteeringHelper 회피

### 차량 (외부)
- **자유 하차 (F)** — 언제든 가능, 차 점진 감속 후 마렌+M-07 동행 하차, 카메라 쿼터뷰 전환
- **재승차 (F)** — 마렌이 차 근처(Mount Radius 4m) 도보 시
- **차량 강탈 (E)** — ParkedVehicle 옆에서 인터랙션, 옛 차/새 차 자동 라이트 ON/OFF
- **차 HP 0** — 영구 파괴 (Wreck), 탑승 거부, 검정 시각 표시
- **헤드라이트 (L)** — 활성 차만 토글, 자동 ON 시 인카운터 시작
- **차 HP = 마렌 생명** (바이블 §10.2): 차 HP 0 + 마렌 차량 뒤 엄폐 시 마렌 사망

### 인카운터 (외부 전투)
- **트리거**: PlayerStatus.IsHunted=true + 헤드라이트 ON → 5게임초 후 적 차량 스폰 (마렌 뒤 50m, 화면 밖)
- **헤드라이트 OFF → 인카운터 정지** ("어둠 속 잠적")
- **적 차량 lifetime 45게임초** → 도주 모드(flee) → 6초 후 자연 소멸
- **적 차량과 거리 80m 초과** → 즉시 despawn
- **적 처치 시** → VehicleHealth.OnDeath → 정지 → 1.5초 후 destroy

### 누아르 톤
- 마렌은 농담 안 함
- M-07은 농담 못 알아들음 (직무적, 가끔 이상하게 인간적)
- **나레이션 없음** (환경과 대사만)
- **회상 컷신 절대 만들지 않음** — 모든 정서는 환경 디테일과 대사로
- "오피서"가 아닌 "마렌"이라고 부름 (M-07이 노아에게 학습한 동작)
- **로봇 시안 아이는 노아의 유산** — 해킹 시 붉은색으로 변하는 게 핵심 시각 비트
- **헌티드 메카닉**: 사건 완료 = 진실 발견 → 표적이 됨 ("진실을 안 자가 표적")

### 기술
- **HTML v04/v11/v12 프로토 로직 정신**으로 이식하되, Unity 컴포넌트 패턴으로 분리
- **단일 거대 스크립트 X** — 컴포넌트 책임 명확히 (Movement / Cover / Interaction / Combat 분리)
- **ScriptableObject 데이터 외부화** (대사·사건·단서·도시 좌표·적 능력치)
- **DialogueCsvTools** (Editor 메뉴) — 대사 텍스트 라운드트립
- **prefab variants** — 시각 자산 교체 시 활용 (P_Car_Temp → P_Maren_Final 등)
- **시네매틱/컷신은 Cinemachine + Timeline** 활용
- **AnimatorBridge 패턴 권장** (그래픽 도입 시) — 캐릭터 컴포넌트 상태 폴링 → Animator 파라미터 갱신
- **Faction 시스템**: VehicleHealth.isEnemy bool, MarenController/M07Controller는 자동 friendly
- **자동 검색 + 인스펙터 명시 권장** — `FindAnyObjectByType<T>(FindObjectsInactive.Include)` 패턴, 핵심 슬롯은 인스펙터 드래그

## Reference Documents

기획·디자인 자료는 `Docs/` 폴더에:
- `Bible/last_patrol_bible_v03.md` — 메인 바이블 (가장 중요)
- `Architecture/s01_outdoor_driving_greybox_setup.md` — S01 셋업 + 백로그
- `Architecture/last_patrol_unity_architecture_v01.md`
- `Architecture/last_patrol_unity_folder_structure_v01.md`
- `Architecture/last_patrol_unity_greybox_guide_v01.md`
- `Architecture/ch1_nordman_data_setup.md`
- `Story/last_patrol_synopsis_*` — 시놉시스
- `Reference/eirinen_master_map_v01.html` — 마을 지도 SVG (CityBuilder 좌표 출처)
- `Reference/cop_robot_coop_prototype_v11.html` — 실내 메카닉 참조
- `Reference/cop_robot_coop_prototype_v12 (2).html` — v12 inner voice 참조
- `Reference/city_patrol_mockup_v04.html` — 외부 메카닉 참조

새 작업 시작 시 관련 Docs 먼저 확인.

## Current Sprint Goal

**진척**: 4-5주차(외부 운전) + 6-7주차(인카운터) **완료**.

10주 로드맵:
- 1주차: 환경 셋업, 첫 캐릭터 임포트 ✅
- 2-3주차: **씬 3 (실내 수사 2.5D)** — v12 inner voice ✅
- 4-5주차: **씬 1 (외부 운전)** — dispatch + 도시 마스터 지도 + 헤드라이트/HUD ✅
- 6-7주차: **씬 2 (인카운터 전투)** — 자유 하차 + 차량 강탈 + 외부 사격 + 엄폐 ✅
- **8주차 (현재)**: 씬 4 (배터리 수거) + 그래픽 리소스 도입 시작 + 음향
- 9-10주차: 통합 + 폴리싱

각 씬은 **그레이박스 → 에셋 적용 → 폴리싱** 순서.

## Solo Dev Discipline

혼자 개발 시 함정 회피:
- 매주 월요일에 "이번 주 1가지 목표" 작성
- 매주 금요일에 빌드 + 짧은 영상 (10초) 자기 검토
- 새 기능보다 **기존 기능 끝맺기** 우선
- "이것도 해보고 싶다"는 백로그에만 추가, 즉시 작업 금지
- **세션이 길어지면 push + 잠그기** — 다음 세션에서 새 컨텍스트로
