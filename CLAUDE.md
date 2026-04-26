# LAST PATROL — Unity Project

3D 누아르 수사 호위 슈터. 라플란드 가상 마을 에이리넨 배경. 솔로 개발 (Jerry) + Claude 페어 코딩.

## What This Is

여경 마렌과 해킹 면역 로봇 M-07이 ELI(슈퍼컴퓨터)에 장악된 세계에서 사건을 수사하는 게임. 장르: 누아르 수사 × 호위 슈터 × 드라이빙 인카운터.

**현재 단계**: 코어 씬 4개 검증 (Vertical Slice)

코어 씬:
1. 외부 자동차 운전 (직부감)
2. 외부 인카운터 전투 (운전 중 적 조우 → 하차 총격전)
3. 실내 수사 (2.5D 사이드뷰 + 깊이)
4. 쓰러진 로봇 배터리 수거

## Engine & Stack

- **Unity 6 LTS** + URP (Universal Render Pipeline)
- **C#** (Claude 페어 코딩 친화)
- **Cinemachine** (카메라 시스템)
- **New Input System** (인풋)
- **TextMeshPro** (UI 텍스트)
- **ProBuilder** (그레이박스)

## Build & Test

- 에디터 플레이: Play 버튼 (단축키 Ctrl+P)
- 빌드: File → Build Profiles → Windows
- 빌드 출력 경로: `Builds/Windows/`

## Architecture

### 핵심 시스템 분리 원칙

각 코어 씬은 **독립 Scene**으로 시작하지만 공통 시스템을 공유:
- `Assets/_Project/Core/` — 공통 시스템 (대화, 세이브, 입력, 상태 관리)
- `Assets/_Project/Scenes/` — 씬별 자산
- `Assets/_Project/Characters/` — 마렌, M-07, 적 프리팹
- `Assets/_Project/Data/` — ScriptableObject (대사, 사건, 적 능력치)

### 핵심 컴포넌트

**캐릭터:**
- `MarenController` — 경찰 입력·이동·엄폐·충전·조사
- `M07Controller` — 로봇 자동 사격·따라가기·점프
- `EnemyAI` — 인간형/드론/큐레이터/헌터/앰부시 공통 베이스

**시스템:**
- `DialogueSystem` — 하단 좌측 대사 패널, 타이프라이터, 프로필 표시
- `InvestigationSystem` — 단서 발견, 증거 조사, 사건 보드
- `DistanceRule` — 마렌-M-07 거리 모니터링, 인카운터 트리거
- `BatterySystem` — M-07 배터리, 마렌 충전 로직
- `EncounterSystem` — 외부 인카운터, 적 차량 추격, 하차 트리거
- `CarController` — 차량 운전, 카메라 추종
- `CameraSwitcher` — Cinemachine 가상 카메라 전환 (외부/실내/전투)

## Conventions

### 폴더 명명
- `_Project/` — 우리 프로젝트 자산만 (밑줄 prefix로 항상 맨 위)
- `_ThirdParty/` — 에셋 스토어 패키지
- 절대 `Assets/` 루트에 자산 두지 않음

### 스크립트 명명
- 컴포넌트: `MarenController.cs`, `EnemyAI.cs` (PascalCase)
- ScriptableObject: `DialogueLineSO.cs`, `EnemyDataSO.cs` (PascalCase + SO 접미사)
- 인터페이스: `IDamageable`, `IInteractable` (I prefix)
- 에디터 도구: `Editor/MarenInspector.cs` (Editor 폴더 안)

### 자산 명명
- 모델: `Maren_Body.fbx`, `M07_Robot.fbx` (캐릭터_파트)
- 머티리얼: `M_Snow.mat`, `M_Maren_Coat.mat` (M_ prefix)
- 텍스처: `T_Snow_Albedo.png`, `T_Maren_Diffuse.png` (T_ prefix)
- 프리팹: `P_Maren.prefab`, `P_M07.prefab` (P_ prefix)
- ScriptableObject 인스턴스: `SO_Dialogue_CH1_Intro.asset`

### 한글 절대 사용 금지
- 파일명 영문만
- 인게임 텍스트는 ScriptableObject로 관리, 한국어/영어 분리
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

## 주의사항 (Do Not)

### 게임플레이
- **마렌은 점프 없음** (계단 자동 등반)
- **마렌은 무기 없음** (전투 못 함, 엄폐만)
- **M-07 총알은 엄폐물 관통** (적 총알만 엄폐물에 막힘)
- **로봇 시안 아이는 노아의 유산** — 해킹 시 붉은색으로 변하는 게 핵심 시각 비트
- **회상 컷신 절대 만들지 않음** — 모든 정서는 환경 디테일과 대사로

### 톤
- 마렌은 농담 안 함
- M-07은 농담 못 알아들음 (직무적, 가끔 이상하게 인간적)
- 나레이션 없음 (환경과 대사만)
- "오피서"가 아닌 "마렌"이라고 부름 (M-07이 노아에게 학습한 동작)

### 기술
- HTML v11 프로토 로직 그대로 이식하되, Unity 컴포넌트 패턴으로 분리
- 단일 거대 스크립트 X — 컴포넌트 책임 명확히
- ScriptableObject로 데이터 외부화 (대사, 사건, 적 능력치)
- 시네매틱이나 컷신 시스템 직접 만들지 말고 Cinemachine + Timeline 활용

## Reference Documents

기획·디자인 자료는 `Docs/` 폴더에:
- `last_patrol_bible_v03.md` — 메인 바이블 (가장 중요)
- `last_patrol_synopsis_scenes_v01.md` — 장면 단위 시놉시스
- `last_patrol_subplot_matrix_v01.md` — 서브 플롯 분기
- `last_patrol_npc_profiles_v01.md` — NPC 프로파일
- `last_patrol_noah_matrix_v01.md` — 노아의 흔적 매트릭스
- `eirinen_master_map_v01.html` — 마을 지도
- `cop_robot_coop_prototype_v11.html` — 실내 메카닉 참조 (포팅 원본)
- `city_patrol_mockup_v04.html` — 외부 메카닉 참조

새 작업 시작 시 관련 Docs 먼저 확인.

## Current Sprint Goal

**현재 목표**: 코어 씬 4개 그레이박스 구현 (10주 로드맵)
- 1주차: 환경 셋업, 첫 캐릭터 임포트
- 2-3주차: **씬 3 (실내 수사 2.5D)** — v11 HTML 로직 이식
- 4-5주차: 씬 1 (외부 운전)
- 6-7주차: 씬 2 (인카운터 전투)
- 8주차: 씬 4 (배터리 수거)
- 9-10주차: 통합 + 폴리싱

각 씬은 **그레이박스 → 에셋 적용 → 폴리싱** 순서.

## Solo Dev Discipline

혼자 개발 시 함정 회피:
- 매주 월요일에 "이번 주 1가지 목표" 작성
- 매주 금요일에 빌드 + 짧은 영상 (10초) 자기 검토
- 새 기능보다 **기존 기능 끝맺기** 우선
- "이것도 해보고 싶다"는 백로그에만 추가, 즉시 작업 금지
