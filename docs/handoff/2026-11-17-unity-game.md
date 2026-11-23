# GameCreateProject 개발 인수인계 · 2026.11.17

> 최신 변경: 그림자·걷기·대기 모션을 추가했고, 사용자 요청으로 증강 선택은 비활성화했습니다. 스탯 시스템은 추가하지 않았습니다. [구현·검증](../concepts/carapace-boss/locomotion-v1/README.md). 아래 증강 관련 기록은 이전 상태입니다.

> 후속 구현: [궁수 2차 구현·검증 기록](2026-11-17-second-archer.md)을 함께 읽으세요. 현재 씬에는 30레벨 궁수 전직과 시간별 적 체력 증가가 적용되어 있습니다.

> 2026-11-20: 실제 게임의 5분 보스를 황금갑각으로 연결했습니다. 범위·피해·검증은 [전투 기록](../concepts/carapace-boss/combat.md)을 확인하세요.

## 새 세션 시작

프로젝트 루트에서 다음 메시지로 시작합니다.

> `C:\Users\minwo\.agent\agent.md`를 먼저 읽고 적용해줘. 그다음 `docs/handoff/2026-11-17-unity-game.md`와 `README.md`를 읽고 이어가자. 현재 프로젝트 파일과 미커밋 변경을 확인하고, 앞으로 요청하는 변경은 최신 파일을 기준으로 직접 수정하고 검증해줘.

- 프로젝트 루트는 `C:\Users\minwo\OneDrive\바탕 화면\게임제작 프로젝트\게임파일\GameCreateProject`입니다.
- Unity 버전은 `6000.6.2f1`, 게임 씬은 `Assets/Scenes/GameCreateProject.unity`입니다. Quality Settings에 지정된 URP 2D 파이프라인을 사용합니다.
- 사용자 호칭은 **민우님**, 응답은 한국어 존댓말입니다. 실제 실행 경로는 Unity 에디터 Play입니다.
- 최신 코드와 씬·프리팹에 저장된 Inspector 값을 함께 확인합니다. 과거 대화의 제안값보다 현재 파일을 우선합니다.
- 기존 코드·사용자 변경·에셋 GUID와 `.meta`를 보존합니다. `SlingshotGoblin.cs`는 현재 UTF-16 계열 인코딩이며 Git에서 바이너리로 표시됩니다. 편집 전 실제 인코딩을 확인합니다.
- 기존 `2026-11-13-unity-game.md`는 이전 시점의 자료입니다. 당시 후속 후보였던 원거리 적은 현재 구현돼 있습니다.

## README와 기능 현황

현재 README 첫 항목은 **Ver 1.0 (2026.11.17)**입니다. 아래 고블린·궁수·상자·HUD 변경은 README에도 기록돼 있습니다. 가장 최근의 장애물 가림 표시 기능은 아직 README에 추가되지 않았습니다.

### 새총 고블린

- 원거리 적 프리팹은 `Assets/Perfeb/SlingshotGoblin.prefab`입니다. 프로젝트 폴더 이름은 실제로 `Perfeb`입니다.
- 이동 8장·공격 8장, 좌우 반전과 발사 원점 반전을 적용했습니다. 공격 05번에서 조준을 유지하고 06번 진입 시 돌을 발사합니다.
- 현재 프리팹 기준 사거리 4.5, 조준 유지 0.45초, 공격 후 쿨타임 1.25초입니다. 돌 이동 거리는 코드상 사거리 + 1유닛입니다.
- 조준 시작 시 방향을 고정합니다. 쿨타임 중 플레이어가 2유닛 이내로 접근하면 초당 0.8유닛으로 후퇴하고, 2.5유닛 확보 또는 다음 공격 준비 시 멈춥니다.
- 예고선은 중앙의 0.022유닛 실선으로 시작합니다. 0.1초 뒤부터 같은 색의 채워진 띠가 대칭으로 넓어지며, 0.18유닛 폭에 도달하면 예고선을 끄고 발사합니다. 기본 예고 시간은 0.85초입니다.
- Inspector의 `예고선` 아래 `채움색`, `경계선 색`과 각 색의 A(알파)를 조절할 수 있습니다. 색은 예고선 생성 시 적용됩니다. 실행 중 기존 인스턴스의 색을 즉시 갱신하는 기능은 구현돼 있지 않습니다.
- 관련 코드는 `Assets/Scripts/SlingshotGoblin.cs`, `Assets/Scripts/GoblinStone.cs`입니다.

### 궁수 이펙트와 HUD

- 실제 이펙트 선택은 **Z2 제비꼬리촉·X3 흰 깃 화살·C5 고드름촉**, HUD 아이콘 선택은 **Z3·X1·C5**입니다. 이펙트와 아이콘의 후보 번호를 구분합니다.
- Z는 12발 부채꼴 연사이며 표시 폭을 실제 판정 폭 0.56유닛에 맞췄습니다.
- X는 반지름 2.5유닛 안에서 착탄 위치를 무작위로 정하고, 긴 낙하 궤적으로 한 발씩 연속 표시합니다. 기본 시전당 화살 40발, 피해 2씩 10회입니다.
- X 조준은 범위 후보에 포함된 적의 가중치 합으로 선택합니다. 적별 점수는 `4 - 3 × Clamp01(플레이어와의 거리 / 시전 사거리)`입니다.
- C는 첫 직격·분열 때의 큰 원을 제거했습니다. 표시 폭은 본체 1.20 → 첫 분열 0.40 → 두 번째 0.30 → 세 번째 0.225유닛입니다. 후속 단계마다 전체 화살을 75%로 줄이고, 분열탄 판정 폭은 0.40유닛입니다.
- HUD는 쿨타임 중 아이콘·배경을 검게 표시하고 사용 가능 시 원색으로 복원합니다. 미전직 상태에서는 궁수 아이콘을 숨깁니다.
- 주요 구현 위치는 `Assets/Scripts/SecondJob/`, `Assets/Scripts/Skills/ArcherSkillFrames.cs`, `Assets/Scripts/Skills/SkillTargeting.cs`, `Assets/Scripts/UI/GameHUD.cs`입니다.

### 상자와 물약

- 상자 폭 1.08유닛, 보상 폭 0.48유닛으로 확대했습니다. 발밑 그림자와 닫힌 상자의 잠금쇠 반짝임을 추가했습니다.
- 개봉 후 0.35초부터 획득 가능한 보상이 거리 제한 없이 플레이어를 추적합니다. 청크 이동 중에도 미획득 보상을 처리합니다.
- 보상을 실제로 획득한 순간부터 상자와 그림자가 1.2초 동안 페이드아웃합니다. 청크 기록에 획득 시각을 저장합니다.
- 보상은 물약과 경험치 자석이며 기본 가중치는 50:20입니다. 대쉬 충전 보상은 제거했습니다.
- 물약은 최대 체력에서도 저장되며 기본 `1`번 키로 한 개를 소비해 최대 체력 3을 회복합니다. 최대 체력·보유량 0·일시정지·게임오버에서는 소모하지 않습니다.
- 체력바 왼쪽 아래에 물약 슬롯, 보유 개수, 현재 사용 키를 표시합니다. 사용자로부터 슬롯이 안 보인다는 과거 피드백이 있었고 실행 경로는 Unity 에디터 Play였습니다. 새로 문제가 보고되면 현재 씬 HUD 연결부터 확인합니다.
- 주요 코드는 `Assets/Scripts/ChestManager.cs`, `Assets/Scripts/ChestView.cs`, `Assets/Scripts/UI/GameHUD.cs`, `Assets/Scripts/UI/KeyBindings.cs`입니다.

## 가장 최근 작업 — 장애물 정렬과 가림 실루엣

### 적용 상태와 설정

- 실제 프로젝트의 게임 씬, 플레이어, 일반 적·고블린·맵 청크 프리팹에 적용했습니다.
- 캐릭터와 장애물은 고정된 발밑 오프셋으로 Y좌표 정렬합니다. 화면 아래쪽에 발밑이 있는 대상이 앞에 표시됩니다.
- 캐릭터의 기준점은 기존 바닥 그림자의 `footOffset`입니다. 나무는 기둥, 돌은 활성 본체의 투명 여백을 제외한 밑면으로 초기 설정했습니다. 돌의 비활성 원본 Renderer를 기준으로 잡지 않도록 주의합니다.
- 가려진 부분에만 실루엣을 합성합니다. 플레이어 기본 채움색은 청백색 RGBA `(0.65, 0.9, 1, 0.6)`, 적은 붉은색 `(0.65, 0.08, 0.1, 0.4)`입니다.
- `Character Occlusion` Inspector에서 `Fill Color`, `Outline Color`, `Outline Pixels`, `Transition Time`을 조절합니다. 기본 테두리는 화면 기준 1픽셀, 전환 시간은 0.1초입니다.
- 전환 강도는 앞쪽 장애물과 캐릭터의 Renderer bounds 겹침을 기준으로 계산하고, 최종 표시 픽셀은 셰이더의 가림 마스크로 제한합니다. 픽셀별 가림 이력을 누적하는 방식은 구현돼 있지 않습니다.
- 현재 스프라이트·좌우 반전·크기·피격 알파를 따라갑니다. 일시정지 중 전환이 멈추고, 사망·비활성화 시 실루엣을 숨깁니다.

### 구현 구조

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/WorldDepthSort.cs` | 카메라 Y좌표와 발밑 Y좌표 차이에 100을 곱해 정렬 순서 계산, 활성 장애물 등록 |
| `Assets/Scripts/CharacterOcclusion.cs` | 별도 런타임 SpriteRenderer로 실루엣 생성, 원본 모션·알파 추적 |
| `Assets/Scripts/WorldOcclusionCamera.cs` | 게임 카메라 렌더 시작 시 화면 내 장애물의 이미지·정렬 순서를 RGFloat 마스크에 기록 |
| `Assets/Art/Occlusion/ObstacleMask.shader` | 실제 불투명 장애물 픽셀과 정렬 순서 기록 |
| `Assets/Art/Occlusion/CharacterSilhouette.shader` | 캐릭터 앞의 장애물에 가려진 픽셀에 채움·테두리 합성 |
| `Assets/Scripts/Editor/WorldVisibilitySetup.cs` | 씬·프리팹·재질·정렬 레이어 연결 도구 |

- 셰이더별 `.mat`도 `Assets/Art/Occlusion/`에 있습니다. 마스크 RenderTexture와 스프라이트 메시를 재사용하며, 해상도 변경과 카메라 비활성화 때 자원을 정리합니다.
- 정렬 레이어는 뒤에서부터 `WorldGround` → `WorldActors` → `WorldSilhouettes` → `Default`입니다. ID는 각각 17911001, 17911002, 17911003, 0입니다.
- 지면·그림자·상자·경험치 픽업은 `WorldGround`, 캐릭터·장애물은 `WorldActors`, 실루엣은 `WorldSilhouettes`입니다. 고블린 예고선은 `Default`에서 장애물 위에 표시합니다.
- `CharacterGroundShadow.cs`와 고블린의 예고선 생성 코드도 이 레이어 구성에 맞춰 수정했습니다.
- 설정 메뉴는 `Tools > Game UI > Setup World Visibility`입니다. 이미 연결된 상태입니다. 도구를 다시 실행하면 활성 게임 씬과 관련 프리팹에 설정을 적용하므로, 현재 사용자 설정과 변경 범위를 먼저 확인합니다.
- 기존 스크립트·셰이더 `.meta`의 GUID를 보존하고, 검증용 프로젝트에서 생성한 연결은 실제 프로젝트 GUID로 맞췄습니다.

## 검증 자료와 한계

이번 가림 표시 검증은 실제 프로젝트 파일을 복사한 별도 Unity 에디터 Play에서 수행했습니다. 마지막 검증은 실제 프로젝트에 반영한 씬·프리팹·GUID 연결을 다시 복사해, 설정 도구 재실행 없이 그대로 로드했습니다.

- 확인한 항목은 앞뒤 정렬, 부분·전체 가림, 다수 적, 플레이어 피격 알파 동기화, 고블린 예고선 표시 순서와 공격 06번 해제, 일시정지·재개, 해상도 변경, 적 사망, 장애물 비활성화·재활성화, 청크 이탈·재방문입니다.
- 최종 로그는 `Logs/WorldVisibilityApplied.log`이며 `VISIBILITY_PASS`를 확인했습니다. 이 파일과 기타 `Logs` 자료는 로컬 검증 자료로, Git에 포함되지 않을 수 있습니다.
- 적 24마리와 장애물이 있는 장면에서 CPU `Camera.Render` 호출을 상태별 20회 측정했습니다. 평균은 마스크 비활성 0.991165ms, 활성 1.0536ms입니다. 전체 게임 프레임 시간·GPU 시간·장시간 부하는 미측정입니다.
- 검증 로그에는 Unity Editor Search 초기화의 `ArgumentOutOfRangeException`도 있었습니다. 해당 예외 이후 게임 검사는 통과했으며, 로그 전체가 오류 없이 깨끗하다고 보고하지 않았습니다.
- 검증 복사본은 `C:\Users\minwo\AppData\Local\Temp\ArcherArrowGameCheck-20261113`입니다. 그 안의 `Assets/Scripts/Editor/WorldVisibilityCheck.cs`와 `VisibilityFrames/`는 로컬 재현 자료입니다. 임시 폴더이므로 사라질 수 있으며, 최신 구현의 기준은 실제 프로젝트입니다.

| 자료 | 위치 |
| --- | --- |
| 최신 장애물 가림 GIF | `docs/concepts/world-visibility.gif` |
| 고블린 채움 예고선 GIF | `docs/concepts/goblin-indicator/split-guide.gif` |
| 고블린 이동·공격 GIF | `docs/concepts/slingshot-goblin-walk.gif`, `docs/concepts/slingshot-goblin-attack.gif` |
| 궁수 플레이 GIF | `docs/concepts/archer-gameplay/Z.gif`, `X.gif`, `C.gif` |
| 상자 획득 후 페이드 GIF | `docs/concepts/chest/reward-fade.gif` |
| 고블린 색·알파 검사 로그 | `Logs/GuideColorCheck.log` |

가림 GIF는 검증용 배치에서 게임 카메라를 촬영한 960×540, 약 4.7초 반복 영상입니다. 플레이어와 고블린을 장애물 뒤로 이동시키고 마지막에 앞쪽 모습을 잠깐 유지합니다. 궁수·상자 GIF는 이전 작업 산출물이며, 이번 가림 변경 후 모든 스킬을 재촬영한 자료는 아닙니다.

## 인계 시점 Git 상태

브랜치는 `main`입니다. 이번 작업에서는 커밋을 생성하지 않았습니다. 새 세션에서 `git status --short`로 아래 상태가 달라졌는지 먼저 확인합니다.

수정된 파일은 다음과 같습니다.

- `Assets/Art/Chests/Chest.prefab`
- `Assets/Art/ExperienceDrops/ExperiencePickup.prefab`
- `Assets/Perfeb/Enemy.prefab`, `MapChunk.prefab`, `SlingshotGoblin.prefab`
- `Assets/Scenes/GameCreateProject.unity`
- `Assets/Scripts/CharacterGroundShadow.cs`, `SlingshotGoblin.cs`
- `ProjectSettings/TagManager.asset`

신규 파일은 `Assets/Art/Occlusion/`과 폴더 `.meta`, `WorldDepthSort.cs`, `CharacterOcclusion.cs`, `WorldOcclusionCamera.cs`, `Editor/WorldVisibilitySetup.cs` 및 각각의 `.meta`, `docs/concepts/world-visibility.gif`, 이 인수인계 문서입니다.

다음 4개 삭제는 가림 표시 작업을 시작하기 전부터 존재했습니다. 사용자 변경으로 취급하고 임의로 복원하거나 이번 작업의 삭제로 설명하지 않습니다.

- `Assets/Resources/PerformanceTestRunInfo.json`
- `Assets/Resources/PerformanceTestRunInfo.json.meta`
- `Assets/Resources/PerformanceTestRunSettings.json`
- `Assets/Resources/PerformanceTestRunSettings.json.meta`

## 다음 세션에서 이어갈 일

요청받은 구현과 GIF 전달은 완료했습니다. 새 세션의 첫 작업은 위 상태와 현재 파일을 확인하고 민우님의 다음 요청을 받는 것입니다. README에 최신 장애물 정렬·실루엣 내용을 추가하거나 전체 프레임 성능을 측정하는 일은 후속 후보입니다. 이미 연결된 기능을 처음부터 재적용하거나 과거 임시 프로젝트로 덮어쓰지 않습니다.
