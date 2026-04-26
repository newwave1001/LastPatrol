# LAST PATROL

3D 누아르 수사 호위 슈터. Unity 6 LTS + URP. 솔로 개발 (Jerry) + Claude 페어 코딩.

## 폴더 개요

```
Last Patrol/
├─ Assets/                    Unity 자산 (프로젝트 생성 후 자동 인식)
│  ├─ _Project/               우리 코드/자산
│  └─ _ThirdParty/            에셋 스토어 패키지 (비어있음)
├─ Docs/                      모든 기획·디자인·아키텍처 문서
│  ├─ Bible/                  세계관 바이블
│  ├─ Story/                  시놉시스, 서브 플롯, NPC
│  ├─ Architecture/           Unity 아키텍처 + 폴더 구조 + 그레이박스 가이드
│  ├─ Reference/              v11 HTML 프로토 + 마을 지도 (이식 참조용)
│  └─ Logs/                   주간 로그 + 결정 기록
├─ Builds/                    빌드 출력 (gitignored)
├─ CLAUDE.md                  Claude 페어 코딩 가이드
├─ .gitignore                 Unity 표준
└─ .gitattributes             Git LFS 트래킹
```

## 다음 작업 (Unity Hub에서 직접)

코드는 미리 작성되어 있음. Unity 프로젝트만 생성하면 즉시 사용 가능.

### 1. Unity 6 LTS 프로젝트 생성
- Unity Hub → Projects → New Project
- **에디터 버전: 6000.0.73f1** (LTS) — Windows Build Support 포함
- 템플릿: **Universal 3D** (URP)
- 프로젝트 이름: `Last Patrol` (이미 있는 폴더 그대로 선택)
- 위치: `c:\` 선택 → 결과 경로가 `c:\Last Patrol\`이 되도록
- Create

> Unity가 기존 `Assets/_Project/` 트리를 감지하고 .meta 파일을 자동 생성.
> 첫 임포트 시 5-10분 걸릴 수 있음.

### 2. 패키지 확인 (Window → Package Manager → Unity Registry)
- **Cinemachine 3.x** — Unity 6 기본 포함 가능. 없으면 설치. ⚠️ API가 2.x와 다름: `CinemachineCamera` 컴포넌트 사용 (옛 `CinemachineVirtualCamera` 아님)
- **Input System** — 설치 시 "Apply" → 에디터 재시작 (Input Handling 자동 전환됨)
- **ProBuilder** — 그레이박스용
- **TextMeshPro** — Unity 6에서 UGUI에 통합되어 있어 자동 사용 가능

### 3. Input System 활성화 확인
- Project Settings → Player → Active Input Handling: **Input System Package (New)** 또는 **Both**

### 4. Input Action 코드 생성
- `Assets/_Project/Core/Input/LastPatrolInputs.inputactions` 선택
- Inspector에서 **Generate C# Class** 체크 → Apply
- 자동으로 `LastPatrolInputs.cs` 생성됨 (`InputReader.cs`가 이를 사용)

### 5. 첫 씬 (S03_IndoorInvestigation)
`Docs/Architecture/last_patrol_unity_greybox_guide_v01.md` 4단계부터 참조해서 그레이박스 환경 만들기.

### 6. Git 초기화 (선택)
```bash
git lfs install
git init
git add .gitattributes .gitignore
git add .
git commit -m "Initial Unity project setup with bible, architecture, and week-1 scaffolding"
```
GitHub Private 리포 만들고 push.

## 현재 1주차 진행 상황 (자동 작성됨)

- [x] Docs 폴더 구조 + 바이블/시놉시스/아키텍처 배치
- [x] .gitignore + .gitattributes (LFS)
- [x] Assets/_Project 폴더 스켈레톤
- [x] 인터페이스: `IDamageable`, `IInteractable`
- [x] Input System: `LastPatrolInputs.inputactions` + `InputReader.cs`
- [x] 마렌: `CharacterMovement`, `CoverSystem`(stub), `InteractionSystem`, `MarenController`
- [x] M-07: `FollowBehavior`, `RobotEyes`, `M07Controller`
- [ ] Unity 프로젝트 생성 (Jerry)
- [ ] 그레이박스 환경 + 임시 캐릭터 (큐브/실린더)
- [ ] Cinemachine 사이드뷰 카메라 셋업
- [ ] 플레이 테스트: A/D 이동 + M-07 따라오기

자세한 1주차 가이드: `Docs/Architecture/last_patrol_unity_greybox_guide_v01.md`
