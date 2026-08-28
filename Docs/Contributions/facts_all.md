# 기여도 팩트시트 — TowerAndDragon

| 항목 | 값 |
|---|---|
| 집계 범위 | 전체 이력 → HEAD (끝 = `435a4b4e`) |
| 저장소 전체 커밋 | 979개 (머지 제외) |
| 머지 커밋 | 209개 |
| 그중 GitHub PR 머지 | 110건 (나머지는 로컬 동기화 머지라 제외) |
| 기간 | 2026-07-03 ~ 2026-08-28 |
| 집계 단위 | 파일 터치 수 (.meta 제외 / 라인 수 아님) |
| 매핑 실패 | **없음 — 모든 커밋이 팀원에게 귀속됨** |

> 이 문서는 스크립트가 센 사실만 담는다. 역할·담당·설계 의도 같은 해석은 들어 있지 않다.

## 전원 요약

| 팀원 | 커밋 | 비중 | 담당 PR | 머지 수행 | 첫 커밋 | 마지막 커밋 | 스크립트 | 데이터 | 씬·프리팹 | 아트·연출 | 문서 | 기타 |
|---|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| 나상욱 | 389 | 39.7% | 50 | 10 | 2026-07-08 | 2026-08-28 | 757 | 1502 | 390 | 21 | 194 | 22 |
| 조강현 | 181 | 18.5% | 0 | 92 | 2026-07-03 | 2026-08-28 | 1016 | 618 | 377 | 27 | 13 | 19 |
| 이하늘 | 240 | 24.5% | 35 | 8 | 2026-07-09 | 2026-08-28 | 1367 | 1048 | 446 | 421 | 87 | 5 |
| 김지해 | 169 | 17.3% | 25 | 0 | 2026-07-09 | 2026-08-28 | 329 | 224 | 520 | 102 | 1 | 8 |

- **비중** — 저장소 전체 커밋 대비. **커밋 수는 기여량이 아니다.** 데이터 에셋을 다루면 늘고 스크립트를 깊게 파면 줄어든다.
- **담당 PR** — 그 PR 안의 커밋을 가장 많이 쓴 사람. 코드 기여의 축이다.
- **머지 수행** — PR 머지 버튼을 누른 사람. 통합·리뷰 담당의 축이며, 코드 기여와 별개다.
  두 숫자가 크게 어긋나도 오류가 아니라 역할 차이다.
- 숫자 열(스크립트~기타)은 커밋 수가 아니라 **파일을 만진 횟수**다. 같은 파일을 열 번 고치면 10이다.

## 마일스톤별 커밋 수

| 팀원 | 프로토타입 | 알파 | 베타 | 제출 |
|---|---:|---:|---:|---:|
| 나상욱 | 236 | 79 | 74 | 0 |
| 조강현 | 85 | 49 | 47 | 0 |
| 이하늘 | 115 | 57 | 68 | 0 |
| 김지해 | 99 | 36 | 34 | 0 |

마일스톤 경계는 CLAUDE.md 기준: 프로토타입 ~7/31 · 알파 ~8/14 · 베타 ~8/28 · 제출 그 이후 (2026년 기준)

---

# 나상욱

커밋 389개 · 2026-07-08 ~ 2026-08-28 · 이메일 `garidana@gmail.com`

## 가장 많이 만진 디렉터리

| 디렉터리 | 이 팀원 터치 | 이 팀원 작업 중 | 그 디렉터리 전체 중 |
|---|---:|---:|---:|
| `Assets/Data/WaveData` | 461 | 16.0% | **77.0%** |
| `Assets/Data/Dragon` | 248 | 8.6% | **64.9%** |
| `Assets/Data/MonsterData` | 232 | 8.0% | **67.6%** |
| `Assets/Data/ConquestData` | 213 | 7.4% | **93.4%** |
| `Assets/Scripts/Buildings` | 179 | 6.2% | **38.1%** |
| `Assets/Scenes` | 176 | 6.1% | **28.7%** |
| `Assets/Data/TowerData` | 146 | 5.1% | **67.9%** |
| `Assets/Data/Research` | 107 | 3.7% | **38.6%** |
| `Docs` | 107 | 3.7% | **53.5%** |
| `Assets/Scripts/Monster` | 104 | 3.6% | **73.8%** |
| `Docs/Sangwook` | 72 | 2.5% | **100.0%** |
| `Assets/StreamingAssets/Localization` | 59 | 2.0% | **25.9%** |
| `Assets/Scripts/Research` | 58 | 2.0% | **52.3%** |
| `Assets/Scripts` | 57 | 2.0% | **35.6%** |
| `Assets/Scripts/Wave_Main` | 51 | 1.8% | **76.1%** |

마지막 열이 높을수록 그 영역을 혼자 맡았다는 뜻이다. 낮으면 공동 작업 구역이다.

## 이 팀원만 만진 파일 (단독 소유)

전체 792개 중 상위 25개.

| 파일 | 수정 횟수 |
|---|---:|
| `Assets/Scenes/Sangwook_SampleScene.unity` | 23 |
| `Assets/Scenes/Sangwook_PortalScene.unity` | 22 |
| `Assets/Scripts/Wave_Main/WaveManager.cs` | 16 |
| `Assets/Prefabs/Monster_Ground_Basic.prefab` | 15 |
| `Assets/Scenes/Sangwook_DailyWaveScene.unity` | 14 |
| `Docs/Sangwook/인구_자원_점령_웨이브_통합_설계.md` | 11 |
| `Assets/Scenes/Sangwook_PopulationScene.unity` | 10 |
| `Assets/Scripts/Buildings/TowerData.cs` | 10 |
| `Docs/Sangwook/인구_시스템_코어_진행상황.md` | 9 |
| `Assets/Data/WaveData/WaveDefinition_Test.asset` | 9 |
| `Assets/Scripts/Monster/IMonsterTarget.cs` | 8 |
| `Assets/Scenes/Sangwook_EnemyAttackTest.unity` | 8 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Rock_Major_RangedReinforcement.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Snow_Minor.asset` | 7 |
| `Docs/Sangwook/인구_시스템_연동_가이드.md` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Rock_Standard_AttackReinforcement.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Volcano_Standard.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Rock_Minor.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Desert_Major_AirReinforcement.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Snow_Major_AirReinforcement.asset` | 7 |
| `Assets/Scripts/Buildings/Tower/TowerProjectile.cs` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Snow_Minor_BasicReinforcement.asset` | 7 |
| `Assets/Data/Attack1.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Snow_Major_RangedReinforcement.asset` | 7 |
| `Assets/Data/ConquestData/EnemyEnhancement_BalanceTest/Penalty_Snow_Standard.asset` | 7 |

## 병합된 PR·브랜치 (이 팀원 커밋이 가장 많은 것)

이 팀원이 직접 머지한 PR: **10건** (전체 110건 중 9.1%)

50건. 머지를 누른 사람이 아니라 **안에 담긴 커밋의 작성자**로 귀속했다.
로컬 동기화 머지는 제외하고 GitHub PR 머지만 센다.

| PR | 브랜치 | 머지일 | 담긴 커밋 |
|---|---|---|---|
| #12 | `feature/camera-control` | 2026-07-09 | 나상욱 2 |
| #23 | `feature/monster-system` | 2026-07-10 | 나상욱 19 |
| #28 | `feature/enemy-navigation` | 2026-07-10 | 나상욱 1 |
| #34 | `feature/tower-system` | 2026-07-10 | 나상욱 23 |
| #38 | `27-feature-alpha-enemydataSO` | 2026-07-13 | 나상욱 4 |
| #42 | `15-feature-enemy-attack-system` | 2026-07-13 | 나상욱 8 |
| #43 | `documentation/claude-md-unitask-policy` | 2026-07-14 | 나상욱 1 |
| #45 | `9-feature-portal-system` | 2026-07-14 | 나상욱 41 |
| #48 | `18-feature-ranged-enemy` | 2026-07-14 | 나상욱 6 |
| #51 | `47-bug-projectile-integration` | 2026-07-15 | 나상욱 10 |
| #53 | `feature/enemy-night-spawn` | 2026-07-15 | 나상욱 7 |
| #55 | `feature/monster-castle-attack` | 2026-07-15 | 나상욱 4 |
| #77 | `feature/16-poulation-system` | 2026-07-20 | 나상욱 16 |
| #80 | `feature/64-population-maintenance` | 2026-07-20 | 나상욱 10 |
| #82 | `feature/66-daily-wave` | 2026-07-21 | 나상욱 13 |
| #85 | `feature/81-special-enemy` | 2026-07-21 | 나상욱 4 |
| #90 | `feature/70-enemy-enhancement-system` | 2026-07-22 | 나상욱 7 |
| #91 | `feature/73-boss-wave` | 2026-07-22 | 나상욱 5 |
| #98 | `feature/86-research-system` | 2026-07-24 | 나상욱 11 |
| #115 | `feature/111-wave-and-bosscycle` | 2026-07-27 | 나상욱 6 |
| #116 | `feature/special-monster-self-destruct-type` | 2026-07-27 | 나상욱 3 |
| #118 | `feature/109-ui-population-allocation-sample` | 2026-07-28 | 나상욱 3 |
| #119 | `feature/105-victory` | 2026-07-28 | 나상욱 1 |
| #122 | `feature/special-enemy-paralyzer` | 2026-07-29 | 나상욱 1 |
| #125 | `feature/range-outliner` | 2026-07-29 | 나상욱 1 |
| #148 | `feature/135-elemental-towers` | 2026-08-03 | 나상욱 9 |
| #151 | `feature/144-tower-regeneration-staffingratio` | 2026-08-03 | 나상욱 1 |
| #152 | `feature/102-babydragon-reward` | 2026-08-03 | 나상욱 3 |
| #155 | `feature/134-enemy-addition` | 2026-08-04 | 나상욱 4 |
| #159 | `feature/157-monster-protector-addition` | 2026-08-04 | 나상욱 3 |
| #160 | `feature/154-shortcuts` | 2026-08-05 | 나상욱 2 |
| #169 | `feature/164-life-babydragon` | 2026-08-06 | 나상욱 3 |
| #171 | `feature/166-babydragon-ice` | 2026-08-06 | 나상욱 4 |
| #172 | `feature/168-babydragon-time` | 2026-08-07 | 나상욱 3 |
| #180 | `feature/140-dragon-skilltree` | 2026-08-12 | 나상욱 13 |
| #186 | `174-feature` | 2026-08-12 | 나상욱 1 |
| #193 | `192-feature-ui` | 2026-08-12 | 나상욱 1 |
| #194 | `173-bug` | 2026-08-12 | 나상욱 1 |
| #196 | `garidana-collab/bug` | 2026-08-12 | 나상욱 1 |
| #197 | `bug/188-ice-mother-dragon-rank-bug` | 2026-08-13 | 나상욱 2 |
| #207 | `feature/203-monster-addition` | 2026-08-13 | 나상욱 2 |
| #225 | `bug/build-bug-fix` | 2026-08-16 | 나상욱 2 |
| #244 | `feature/landmark-tower` | 2026-08-24 | 나상욱 15 |
| #246 | `beta` | 2026-08-24 | 나상욱 2 |
| #247 | `bug/214-dragon-upgrade` | 2026-08-24 | 나상욱 5 |
| #252 | `beta` | 2026-08-25 | 나상욱 4 |
| #254 | `beta` | 2026-08-26 | 나상욱 5 |
| #258 | `beta` | 2026-08-27 | 나상욱 1 |
| #268 | `beta` | 2026-08-27 | 나상욱 2 |
| #272 | `feature/hot-fix` | 2026-08-27 | 나상욱 2 |

## 커밋 전체 (시간순)

### 프로토타입 (236개)

- `e0fadc20` 2026-07-08 gi — `Assets`
- `a69cda96` 2026-07-08 imported init — `Assets`
- `d660c650` 2026-07-09 [Feature] Base Monster System — `Assets/Scripts`
- `42a66afa` 2026-07-09 [Feature] monster system test — `Assets/Scenes`, `Assets/Data`
- `214bd258` 2026-07-09 [Feature] Monster Health & IDamageable — `Assets/Scripts`
- `707c1d7b` 2026-07-09 [Feature] Monster Movement & Movement Type — `Assets/Scripts`
- `69d02d69` 2026-07-09 [Feature] Monster Data & Damage — `Assets/Scripts`
- `3be90488` 2026-07-09 [Feature] Monster Movement & Movement Type — `Assets/Scripts`
- `5cc16780` 2026-07-09 [Feature] Monster Data & Damage — `Assets/Scripts`
- `c21efa9a` 2026-07-09 [Feature] Monster Shield, Attack, spline movement — `Assets/Scripts`
- `cb4b9810` 2026-07-09 [Feature] Monster Shield, Attack, spline movement — `Assets/Scripts`
- `6927cbbe` 2026-07-09 [Feature] Monster Test done — `Assets/Scripts/Monster`, `Assets/Prefabs`
- `8d19c0f4` 2026-07-09 [Feature] monster system test — `Assets/Scenes`, `Assets/Data`
- `9b99b893` 2026-07-09 [Feature] Monster Health & IDamageable — `Assets/Scripts`
- `f0ae470e` 2026-07-09 [PR] 수정 사항 반영 — `Assets/Scripts`, `(저장소 루트)`
- `7c4665e0` 2026-07-09 gitignore update — `(저장소 루트)`
- `a9fcdffa` 2026-07-09 [feature] : camera movement — `Assets/Scripts`, `ProjectSettings`
- `d30f3004` 2026-07-09 [Feature] Base Monster System — `Assets/Scripts`
- `a5a6a8dd` 2026-07-09 [Feature] spline packages downloaded — `Packages`
- `d4c8b68a` 2026-07-10 타워 공격 투사체 샘플 표현 — `Assets/Scripts/Buildings`, `Assets/Prefabs`
- `a46ecf86` 2026-07-10 타워데이터SO 작업 시작 — `Assets/Scripts/Buildings`, `Docs`
- `a18db839` 2026-07-10 타워 관련 스크립트 위치 이동 — `Assets/Scripts/Buildings`, `Assets/Data`
- `090db068` 2026-07-10 타워 공격 시스템 — `Assets/Scripts/Buildings`
- `d6e33f1b` 2026-07-10 타워 공격 시스템 — `Assets/Scripts/Buildings`
- `ea85cc17` 2026-07-10 Health.cs 관련 수정사항 — `Assets/Scripts`
- `6dc6bc34` 2026-07-10 타워 세팅 Sangwook_TowerTestScene — `Assets/Data/Monster`, `Assets/Scenes`
- `3605b1d1` 2026-07-10 타워 어택 작업 시작 — `Assets/Scripts/Buildings`
- `4726da3f` 2026-07-10 타워 공격 / 데이터 SO / 타워 기본 — `Assets/Scripts/Buildings`
- `9718045c` 2026-07-10 타워 공격 구현 — `Assets/Scenes`, `Assets/Data`
- `31e54bfe` 2026-07-10 타워 어택 작업 시작 — `Assets/Scripts/Buildings`
- `a472dc08` 2026-07-10 타워 재활성화 함수 UniTask 적용 — `Assets/Scripts/Buildings`, `Docs`
- `c2c8a0bd` 2026-07-10 Health.cs 관련 수정사항 — `Assets/Scripts`
- `7c983904` 2026-07-10 타워 공격 / 데이터 SO / 타워 기본 — `Assets/Scripts/Buildings`
- `4f20f4d0` 2026-07-10 타워 공격 투사체 샘플 표현 — `Assets/Scripts/Buildings`, `Assets/Prefabs`
- `b9109523` 2026-07-10 타워 공격 구현 — `Assets/Scenes`, `Assets/Data`
- `f18b23d0` 2026-07-10 타워 세팅 Sangwook_TowerTestScene — `Assets/Data/Monster`, `Assets/Scenes`
- `be4e1427` 2026-07-10 적 시스템 설명서 — `Docs`
- `0ea9501b` 2026-07-10 IAttackEffect 인터페이스 — `Assets/Scripts/Combat`
- `e71c03b7` 2026-07-10 공격 및 공격 효과 — `Assets/Scripts/Combat`
- `def87644` 2026-07-10 스크립트 이동 — `Assets/Scripts/Combat`
- `7e404e6e` 2026-07-10 Monster 수정 — `Assets/Scripts/Monster`
- `002e3320` 2026-07-10 공격 효과와 데미지 — `Assets/Scripts/Combat`
- `adcc7bd5` 2026-07-10 타워 어택 작업 시작 — `Assets/Scripts/Buildings`
- `124778d6` 2026-07-10 타워 공격 시스템 — `Assets/Scripts/Buildings`
- `519bcfc1` 2026-07-10 타워 관련 스크립트 위치 이동 — `Assets/Scripts/Buildings`, `Assets/Data`
- `04e1141d` 2026-07-10 타워데이터SO 작업 시작 — `Assets/Scripts/Buildings`, `Docs`
- `73338374` 2026-07-10 타워 관련 스크립트 위치 이동 — `Assets/Scripts/Buildings`, `Assets/Data`
- `89ec43bf` 2026-07-10 타워데이터SO 작업 시작 — `Assets/Scripts/Buildings`, `Docs`
- `7e020abc` 2026-07-10 몬스터 포탈 -> 성 이동 테스트 완료 — `Assets/Scripts`, `Assets/Data`
- `031b87a6` 2026-07-11 빌드 및 연구 아이디어 샘플 — `Assets`, `Docs`
- `7c716dff` 2026-07-11 맥용 gitignore 추가 — `(저장소 루트)`
- `9efdd160` 2026-07-12 포탈 시스템 씬 생성 — `Assets/Scenes`
- `0ba18e3d` 2026-07-12 포탈 시스템 씬 생성 — `Assets/Scenes`
- `9f6c4106` 2026-07-13 포탈 관련 문서 — `Docs`
- `12830940` 2026-07-13 포탈 웨이브 데이터와 웨이브  SO — `Assets/Scripts/Wave`
- `40f01881` 2026-07-13 포탈 시스템 구현 시작 — `Assets/Scripts/Buildings`, `Assets/Scripts/Portal`
- `1116b387` 2026-07-13 포탈 관련 문서 — `Docs`
- `a5dd765a` 2026-07-13 포탈 시스템 구현 시작 — `Assets/Scenes`
- `a53daf67` 2026-07-13 포탈 웨이브 데이터와 웨이브  SO — `Assets/Scripts/Wave`
- `dffb23d5` 2026-07-13 포탈 Portal.cs와 테스트 씬 작업 — `Assets/Scenes`, `Assets/Scripts/Portal`
- `bba6bed9` 2026-07-13 포탈 웨이브 관련 데이터 — `Assets/Scripts/Wave`
- `f971e1e3` 2026-07-13 포탈 웨이브 관련 데이터 — `Assets/Scripts/Wave`
- `b30c74fc` 2026-07-13 공격형 몬스터 타워 감지 로직 추가 — `Assets/Prefabs`, `Assets/Data/MonsterData`
- `76ca5f6d` 2026-07-13 claude.md 비동기 UniTask관련 claude md — `(저장소 루트)`
- `70e5cf1e` 2026-07-13 몬스터 이동중 타겟 검사 로직 — `Assets/Scripts/Monster`, `Assets/Scripts/Buildings`
- `c7b02523` 2026-07-13 포탈 시스템 구현 시작 — `Assets/Scripts/Buildings`, `Assets/Scripts/Portal`
- `71cc4628` 2026-07-13 포탈 Portal.cs와 테스트 씬 작업 — `Assets/Scenes`, `Assets/Scripts/Portal`
- `2ad63dc8` 2026-07-13 포탈 관련 문서 — `Docs`
- `d9966877` 2026-07-13 코덱스 관련 gitignore추가 — `(저장소 루트)`
- `7f8ee27b` 2026-07-13 포탈 Portal.cs와 테스트 씬 작업 — `Assets/Scenes`
- `247009b9` 2026-07-13 BaseMonster & MonsterData 수정 — `Assets/Scripts/Monster`
- `9b3957c7` 2026-07-13 몬스터 데이터 확인 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `0681a0ae` 2026-07-13 적 공격 시스템 작업 시작 — `Assets/Scenes`
- `3ff1e82a` 2026-07-13 포탈 웨이브 데이터와 웨이브  SO — `Assets/Scripts/Wave`
- `718cddd4` 2026-07-13 포탈 시스템 구현 시작 — `Assets/Scripts/Buildings`, `Assets/Scripts/Portal`
- `9892e293` 2026-07-13 포탈 Portal.cs와 테스트 씬 작업 — `Assets/Scenes`, `Assets/Scripts/Portal`
- `14ac79f2` 2026-07-13 포탈 웨이브 관련 데이터 — `Assets/Scripts/Wave`
- `9c382181` 2026-07-13 웨이브 테스트 완료 — `Assets/Scripts/WaveData_Test`, `Assets/Data/WaveData`
- `095814b2` 2026-07-13 테스트용 웨이브 데이터 작업 — `Assets/Data/MonsterData`, `Assets/Scripts/WaveData_Test`
- `a0facd75` 2026-07-13 몬스터 스폰 그룹 & 웨이브 데이터 — `Assets/Scripts/WaveData_Test`
- `14ade35f` 2026-07-13 Auto stash before checking out "origin/master" — `Assets/Data/MonsterData`, `Assets/Data/TowerData`
- `f23db674` 2026-07-13 Auto stash before checking out "origin/master" — `Assets/Data/MonsterData`, `Assets/Data/TowerData`
- `b48fbad6` 2026-07-13 Buildmode UI 삭제 — `Assets/Prefabs/UI`
- `4cd982e7` 2026-07-13 적 공격 시스템 작업 시작 — `Assets/Scenes`
- `635278c0` 2026-07-14 씬 변경사항 — `Assets/Scenes`
- `7029053d` 2026-07-14 원거리 몬스터 공격 투사체 적용 — `Assets/Scripts/Monster`, `Assets/Prefabs`
- `1e0090aa` 2026-07-14 포탈 및 웨이브 작업 진행 현황 문서 — `Docs`
- `8138fa4b` 2026-07-14 원거리 몬스터 SO 생성 — `Assets/Data/DamageEffect`
- `1a5edfab` 2026-07-14 몬스터 배치후 동작 테스트 — `Assets/Prefabs/Monster`, `Assets/Data/WaveData`
- `b5c54a7a` 2026-07-14 원거리 몬스터 적용 — `Assets/Data/MonsterData`, `Assets/Data/DamageEffect`
- `a2c7fdb3` 2026-07-14 WaveManager 구조 완성 — `Assets/Scripts/Wave_Main`
- `e7c3a1f7` 2026-07-14 WaveDefintionSO — `Assets/Scripts/Wave`
- `493a741a` 2026-07-14 WaveManager 구조 완성 — `Assets/Scripts/Wave_Main`
- `6197930c` 2026-07-14 기반 작업 — `Assets/Scripts/Wave_Main`
- `29c5d4af` 2026-07-14 웨이브 통합 작업 시작 — `Assets/Scripts/Wave_Main`, `Assets/Scripts/Wave_Old`
- `1570512a` 2026-07-14 SerializedField new() 제거 — `Assets/Scripts/Wave_Main`
- `00eed395` 2026-07-14 씬 세팅 — `Assets/Data/WaveData`, `Assets/Scenes`
- `824f8642` 2026-07-14 PortalId 수정 — `Assets/Scripts/Portal`, `Assets/Scripts/Wave_Main`
- `7b8de1b3` 2026-07-14 포탈 테스트 씬 셋업 — `Assets/Prefabs`, `Assets/Scripts/Wave_Main`
- `cf088485` 2026-07-14 WaveManager 함수 채우는중 — `Assets/Scripts/Wave_Main`
- `004e10ca` 2026-07-14 WaveManager — `Assets/Scripts/Wave_Main`
- `65690fc3` 2026-07-14 포탈 테스트 진행중 — `Assets/Data/WaveData`, `Assets/Scenes`
- `eb173fe8` 2026-07-14 기반 작업 — `Assets/Scripts/Wave_Main`
- `e8aae94c` 2026-07-14 머티리얼 폴더 추가 — `Assets/Scenes`, `Assets/Material`
- `a276e434` 2026-07-14 WaveDefintionSO — `Assets/Scripts/Wave`
- `bff41f63` 2026-07-14 웨이브 통합 작업 시작 — `Assets/Scripts/Wave_Main`, `Assets/Scripts/Wave_Old`
- `11f0c3ba` 2026-07-14 포탈 테스트 진행중 — `Assets/Data/WaveData`, `Assets/Scenes`
- `f4ae6cd2` 2026-07-14 WaveManager — `Assets/Scripts/Wave_Main`
- `5c07d039` 2026-07-14 WaveManager 함수 채우는중 — `Assets/Scripts/Wave_Main`
- `9f39a7e5` 2026-07-14 포탈 테스트 씬 셋업 — `Assets/Prefabs`, `Assets/Scripts/Wave_Main`
- `be69737b` 2026-07-14 씬 세팅 — `Assets/Data/WaveData`, `Assets/Scenes`
- `286d0adf` 2026-07-14 PortalId 수정 — `Assets/Scripts/Portal`, `Assets/Scripts/Wave_Main`
- `ef0159da` 2026-07-14 SerializedField new() 제거 — `Assets/Scripts/Wave_Main`
- `ee0c74cb` 2026-07-15 Docs 문서 이동 — `Docs/Sangwook`
- `0245aa7b` 2026-07-15 기존 Old WaveData 제거 — `Assets/Data/WaveData`
- `1174cc32` 2026-07-15 Auto stash before checking out "origin/master" — `Docs/Sangwook`
- `9c1c1490` 2026-07-15 쌤플씬 복사 후 작업 — `Assets/Prefabs`, `Assets/Scenes`
- `9115d110` 2026-07-15 낮밤 몬스터 스폰 싸이클 테스트 완료 — `Assets/Scripts/Managers`, `Assets/Scenes`
- `fc0822e0` 2026-07-15 부분수정 — `Assets/Scripts/Managers`
- `fce9cca3` 2026-07-15 SampleScene 밤 전환시 웨이브 등장 — `Assets/Scenes`, `Assets/Scripts/Wave_Main`
- `a3370d01` 2026-07-15 몬스터 메인 성 도착후 성 공격 — `Assets/Scripts/Monster`, `Assets/Scripts/Buildings`
- `2e9064d4` 2026-07-15 몬스터 메인 성 공격 기능 — `Assets/Scripts/Monster`, `Assets/Data/MonsterData`
- `ed649512` 2026-07-15 몬스터 메인 성 도착후 성 공격 — `Assets/Scripts/Monster`, `Assets/Scripts/Buildings`
- `88f0770f` 2026-07-15 몬스터 메인 성 공격 기능 — `Assets/Scripts/Monster`, `Assets/Data/MonsterData`
- `c43e78fb` 2026-07-15 Auto stash before checking out "origin/master" — `Assets/Scripts/Managers`
- `5b224af0` 2026-07-15 공용 Projectile 작업중 — `Assets/Scripts/Monster`, `Assets/Scripts`
- `c16c821a` 2026-07-15 타워 투사체 작업 — `Assets/Scripts/Buildings`
- `a5eab3b3` 2026-07-15 Projectile.cs 완성 — `Assets/Scripts`, `Assets/Scripts/Monster`
- `d1c37490` 2026-07-15 투사체 시스템 통합 완료 — `Assets/Prefabs`, `Assets/Data/MonsterData`
- `ec5f9fa4` 2026-07-15 Projectile 스크립트 연결 — `Assets/Prefabs`, `Assets/Scripts/Monster`
- `d3a5945a` 2026-07-15 Projectile.cs 완성 — `Assets/Scripts`, `Assets/Scripts/Monster`
- `456ee745` 2026-07-15 타워 투사체 작업 — `Assets/Scripts/Buildings`
- `3643e928` 2026-07-15 공용 Projectile 작업중 — `Assets/Scripts/Monster`, `Assets/Scripts`
- `7478af8f` 2026-07-15 타워 투사체 작업 — `Assets/Scripts/Buildings`
- `3c18c89f` 2026-07-15 SampleScene 웨이브 매니저 및 포탈 수정 — `Assets/Scenes`
- `f710c7d5` 2026-07-15 필요없는 스크립트 제거 — `Assets/Scripts/Monster`, `Assets/Scripts/Buildings`
- `f7b7273b` 2026-07-16 인구 시스템 작업 계획 수립 — `Docs/Sangwook`, `Assets/Scripts/Portal`
- `bb51a0ce` 2026-07-16 인구 시스템 작업 계획 수립 — `Docs/Sangwook`, `Assets/Scripts/Portal`
- `ce1301f7` 2026-07-17 Sangwook_PopulationScene Test — `Assets/Scripts/Population`
- `23d9d6fb` 2026-07-17 PopulationDebugTest 테스트 — `Assets/Scenes`, `Assets/Scripts/Population`
- `ad0be6c7` 2026-07-17 PopulationManager — `Assets/Scripts/Population`
- `b7548917` 2026-07-17 PopulationManager.cs — `Assets/Scripts/Population`
- `63047b11` 2026-07-17 PopulationAssignmetType — `Assets/Scripts/Population`
- `d1ee8fdb` 2026-07-17 PopulationManager — `Assets/Scripts/Population`
- `bf55bcdb` 2026-07-17 PopulationManager — `Assets/Scripts/Population`
- `e810d0b2` 2026-07-17 PopulationAllocation — `Assets/Scripts/Population`
- `95fb326e` 2026-07-17 인구 관련 시스템 작업 — `Assets/Scripts/Population`
- `8d167742` 2026-07-17 PopulationManager — `Assets/Scripts/Population`
- `37046eb1` 2026-07-18 TowerData.cs — `Assets/Scripts/Buildings`
- `0e1f4c64` 2026-07-18 타워 인구 할당 및 테스트 UI 연동 — `Docs/Sangwook`, `Assets/Scripts/Buildings`
- `929613a6` 2026-07-18 인구 시스템 주석작 — `Assets/Scripts/Population`, `Docs/Sangwook`
- `030edb60` 2026-07-20 일차별 웨이브 진행 계획 문서 — `Docs`
- `1bd89463` 2026-07-20 인구 감소 시스템 — `Assets/Scripts/Managers`, `Assets/Scripts/Population`
- `895da1d6` 2026-07-20 인구 자원 소모 기반 — `Assets/Scripts/GameResources`
- `7e13d1af` 2026-07-20 하루 식량 감소 시스템 — `Assets/Scripts/Managers`
- `6673ae96` 2026-07-20 인구 시스템 관련 문서 갱신 — `Docs/Sangwook`, `Assets/Scenes`
- `f98263af` 2026-07-20 인구 씬 작업 — `Assets/Scenes`
- `5fffef70` 2026-07-20 인구 감소 테스트 완료 — `Assets/Scripts/Debug`, `Assets/Scenes`
- `c78e7c7e` 2026-07-20 일차별 웨이브 진행 계획 문서 — `Docs`
- `26529b85` 2026-07-20 인구 시스템 관련 문서 갱신 — `Docs/Sangwook`, `Assets/Scenes`
- `e3a661dc` 2026-07-20 인구 시스템 관련 문서 갱신 — `Docs/Sangwook`, `Assets/Scenes`
- `92dceef4` 2026-07-20 인구 씬 작업 — `Assets/Scenes`
- `8c5aee47` 2026-07-20 일차별 웨이브 진행 계획 문서 — `Docs`
- `1020c88e` 2026-07-20 인구 씬 작업 — `Assets/Scenes`
- `ec3539db` 2026-07-20 TowerAttack — `Assets/Scripts/Buildings`, `Assets/Scenes`
- `0adef40c` 2026-07-20 Docs업데이트 — `Docs/Sangwook`
- `923cc1a3` 2026-07-20 인구 자원 소모 기반 — `Assets/Scripts/GameResources`, `Assets/Scripts/Managers`
- `8e74194f` 2026-07-20 기아 감소 시스템 — `Assets/Scripts/Population`
- `a54c90d4` 2026-07-20 Docs업데이트 — `Docs/Sangwook`
- `d2771f18` 2026-07-20 하루 식량 감소 시스템 — `Assets/Scripts/Managers`
- `fbcfea9c` 2026-07-20 기아 감소 시스템 — `Assets/Scripts/Population`
- `b8ff5ffa` 2026-07-21 EnemyDailyScene 수정 — `Assets/Scenes`
- `2b6fc3b8` 2026-07-21 적 강화 시스템 연동 작업 — `Assets/Scripts/Conquest`, `Assets/Scripts/Managers`
- `7e050624` 2026-07-21 EnemyDailyScene 수정 — `Assets/Scenes`
- `ea6149c0` 2026-07-21 힐러형 적 구현 및 테스트 완료 — `Assets/Data/MonsterData`, `Docs`
- `6222310a` 2026-07-21 일찹별 웨이브 작업 완료 — `Assets/Data/MonsterData`, `Assets/Scripts/Debug`
- `81fd1d05` 2026-07-21 웨이브 관련 업데이트 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `98dd3d18` 2026-07-21 일찹별 웨이브 작업 완료 — `Assets/Data/MonsterData`, `Assets/Scripts/Debug`
- `11efa4d4` 2026-07-21 웨이브 관련 업데이트 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `bfee6345` 2026-07-21 특수형 적 구현 — `Assets/Data/MonsterData`, `Docs`
- `d7c706f4` 2026-07-21 특수형 적 설계 — `Assets/Scripts/Monster`, `Assets/Data/MonsterData`
- `55581094` 2026-07-21 특수형 적 설계 — `Assets/Scripts/Monster`, `Assets/Data/MonsterData`
- `500fab3a` 2026-07-22 보슨 프리팹 생성 — `Assets/Data/ConquestData`, `Assets/Data/MonsterData`
- `e78dab37` 2026-07-22 보스 웨이브 작업 시작 — `Assets/Scenes`
- `33750297` 2026-07-22 보스 웨이브 및 포탈 활성화 테스트 완료 — `Assets/Data/WaveData`, `Assets/Scenes`
- `0c449939` 2026-07-22 1주기 보스 웨이브 완료 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `b40ed801` 2026-07-22 테스트용 웨이브 완료 — `Assets/Data/WaveData`, `Assets/Data/ConquestData`
- `dc68ec10` 2026-07-22 웨이브 스케줄 미세 수정 — `Assets/Data/WaveData`
- `acd1bd51` 2026-07-22 미세 조정 — `Assets/Scripts/Monster`
- `d42a3414` 2026-07-22 EnhancementResolver 함수 수정 — `Assets/Scripts/Conquest`
- `a4e9aa9b` 2026-07-22 점령시 몬스터 강화 페널티 구현 — `Assets/Scenes`
- `a72040c1` 2026-07-22 점령시 몬스터 강화 페널티 구현 — `Assets/Scripts/Managers`, `Assets/Prefabs/PopulationDebugTester`
- `d0bd78f2` 2026-07-23 타워 데미지 강화 연구 — `Assets/Scripts/Research`, `Assets/Data/Research`
- `64bd0676` 2026-07-23 용 및 연구 시스템 계획 — `Docs`
- `1579040c` 2026-07-23 연구 시스템 틀 — `Assets/Scripts/Research`, `Assets/Data/Research`
- `2865cdb8` 2026-07-23 용 스킬 트리 계획 — `Docs`
- `82ffdbfc` 2026-07-23 로드맵 — `Docs`
- `28ec399c` 2026-07-23 연구 시스템 개발 계획 — `Docs`
- `1180c2ce` 2026-07-23 DebugTester 폴더명 변경 — `Assets/Prefabs/DebugTester`, `Assets/Scenes`
- `41b6dbe1` 2026-07-23 연구 씬 수정 — `Assets/Data/ConquestData`, `Assets/Scenes`
- `81c10cbc` 2026-07-23 씬 수정 — `Assets/Scenes`
- `e6a87bea` 2026-07-23 테스트용 웨이브 디자인 및 문서작업 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `e888c0e0` 2026-07-23 테스트 웨이브 데이터 수정 — `Assets/Data/WaveData`
- `99cd7a2e` 2026-07-23 연구 기획 및 웨이브 데이터 미세수정 — `Docs`, `Assets/Data/WaveData`
- `fc6359bd` 2026-07-24 Auto stash before checking out "origin/master" — `Assets/Scripts/Research`
- `c39f82d5` 2026-07-25 씬 조정 및 Research폴더 이동 — `Assets/Scenes`, `Assets/Scripts/Buildings`
- `19d81db4` 2026-07-25 보스 주기 구현 작업 — `Assets/Scripts/Wave_Main`, `Assets/Tests/Editor`
- `2dea3719` 2026-07-27 인구 할당 기초 UI 작업 — `Assets/Scripts/Buildings`, `Assets/Scripts/UI`
- `11c24bdb` 2026-07-27 인구 할당 UI 테스트 완료 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `85a2c041` 2026-07-27 건물 인구 할당 ui 작업 시작. — `Assets/Scenes`
- `0cb73779` 2026-07-27 웨이브 데이터 수정 — `Assets/Scripts/Debug`, `Assets/Scenes`
- `d6c79833` 2026-07-27 웨이브 시스템 완료 — `Docs/Sangwook`, `Assets/Scenes`
- `448e424a` 2026-07-27 웨이브 사이클 씬 — `Assets/Data/WaveData`, `Assets/Scenes`
- `2819314c` 2026-07-27 폴더 이동 및 웨이브 데이터 주입 — `Assets/Data/WaveData`, `Docs/Sangwook`
- `6c63acb1` 2026-07-27 웨이브 주기 스케줄과 씬 연결 추가 — `Docs/Sangwook`, `Assets/Data/WaveData`
- `977a4a4c` 2026-07-27 자폭형 구현 완료 — `Assets/Data/MonsterData`, `Assets/Scripts/Monster`
- `750f8435` 2026-07-27 자폭형 몬스터 작업 시작 — `Assets/Scripts`, `Assets/Scripts/Monster`
- `db9bf526` 2026-07-27 쌤플 신 최종 병합 — `Assets/Scenes`
- `eebf59e2` 2026-07-27 MonsterAttack — `Assets/Scripts/Monster`
- `7f5cc43a` 2026-07-27 자폭형 몬스터 구현 완료 — `Assets/Data/MonsterData`, `Assets/Scripts/Monster`
- `ea346ec0` 2026-07-28 보스 웨이브 수정 — `Assets/Data/WaveData`
- `3c556d3a` 2026-07-28 웨이브 데이터 수정 — `Assets/Data/WaveData`
- `91261306` 2026-07-28 감전형 몬스터 구현 — `Assets/Data/MonsterData`, `Assets/Scripts/Combat`
- `95c0c411` 2026-07-28 게임 승리 UI 연결 — `Assets/Scripts/Managers`, `Assets/Scenes`
- `9437e4a2` 2026-07-29 7/29 발표 자료 — `Tools/FigmaProgressBoard`, `Docs`
- `dacfed5c` 2026-07-29 previous samplescenes deleted — `Assets/Scenes`
- `ade26986` 2026-07-29 타워 종류 추가 — `Assets/Data/TowerData`, `Assets/Data/DamageEffect`
- `fa36c9ab` 2026-07-29 몬스터 웨이브 수정 — `Assets/Scenes`, `Assets/Scripts/Monster`
- `47e2b652` 2026-07-29 건설 전 타워 범위 표시 추가 — `Assets/Scripts`, `Assets/Scripts/Buildings`
- `d9b7639c` 2026-07-29 몬스터 밸런스 웨이브 추가 — `Assets/Data/WaveData`, `Assets/Data/MonsterData`
- `15522723` 2026-07-30 중간발표 자료 — `Docs`
- `e0fbf119` 2026-07-30 적 강화 데이터 추가 (밸런스 테스트) — `Assets/Data/ConquestData`, `Assets/Data/BabyDragon`
- `59c4317b` 2026-07-31 웨이브 및 몬스터 밸런싱 — `Assets/Data/ConquestData`, `Assets/Data/MonsterData`
- `bc20aef7` 2026-07-31 원거리 몬스터 공격력 증가 — `Assets/Data/DamageEffect`, `Assets/Data/MonsterData`
- `1c5c2269` 2026-07-31 청크 점령 페널티 밸런스 조절 — `Assets/Data/ConquestData`
- `4af3b215` 2026-07-31 게임 플레이 가이드 튜토리얼 — `Docs`

### 알파 (79개)

- `59a22a46` 2026-08-02 시간 타워 및 기반 건설 중 — `Assets/Scripts/Buildings`, `Assets/Scripts/Modifiers`
- `ef607e79` 2026-08-02 암석타워 추가 및 테스트 완료 — `Assets/Data/TowerData`, `Assets/Scenes`
- `a857e85d` 2026-08-02 얼음타워 및 화상 타워 추가 — `Assets/Data/TowerData`, `Assets/Scenes`
- `8d3f8c44` 2026-08-02 얼음 속성 타워 작업 — `Assets/Data/TowerData`, `Assets/Scripts/Buildings`
- `1ee1d617` 2026-08-02 광역 공격 Attack 추가 — `Assets/Scripts/Combat`, `Assets/Scripts/Monster`
- `ef4907bd` 2026-08-03 인구 감소 표시 및 연구소 배치 인원 기아 감소 추가 — `Assets/Scripts/Population`, `Assets/Scripts/Managers`
- `f65b4a94` 2026-08-03 웨이브 데이터 수정 및 추가 — `Assets/Data/MonsterData`, `Assets/Data/WaveData`
- `44262927` 2026-08-03 새끼용 보스주기 보상 연결 완료 — `Assets/Data/BabyDragon`, `Assets/Scenes`
- `f0636731` 2026-08-03 용사냥꾼 테스트 완료 — `Assets/Data/WaveData`, `Assets/Scenes`
- `0dcf8922` 2026-08-03 탱커형 및 용사냥꾼형 구현 — `Assets/Data/MonsterData`, `Assets/Scripts/Buildings`
- `e91d038a` 2026-08-03 속성형 및 속성 면역형 적 작업 — `Assets/Scripts/Combat`, `Assets/Data/MonsterData`
- `03c33dd9` 2026-08-03 속성 타워 관련 문서 — `Docs/Sangwook`
- `a5a63ada` 2026-08-03 타워 재활성화 시간 수정 및 충원율 연동 완료 — `Assets/Data/TowerData`, `Assets/Scripts/Buildings`
- `fcfcc974` 2026-08-03 버프 타워 사정거리 표시 연결 — `Assets/Scripts`, `Assets/Scripts/Buildings`
- `6eb4c2a5` 2026-08-03 시간 타워 오라 (버프) 적용 및 테스트 완료 — `Assets/Scripts/Buildings`, `Assets/Data/TowerData`
- `0532dfe7` 2026-08-03 속성 타워 구현 완료 — `Assets/Data/TowerData`, `Assets/Scenes`
- `6f5d75b6` 2026-08-03 과거 씬 삭제 — `Assets/Scenes`, `Assets`
- `f281e100` 2026-08-03 식량 부족 인구 감소 예상량 표시 — `Assets/Scripts/Population`, `Assets/Tests/Editor`
- `c6d55d61` 2026-08-03 주기 보상으로 랜덤 속성의 알 획득 작업 — `Assets/Scripts/Buildings`, `Assets/Data/BabyDragon`
- `0d82a4e7` 2026-08-04 몬스터 타입 추가 — `Assets/Data/MonsterData`, `Assets/Scripts/Monster`
- `e24dbebe` 2026-08-04 리커버리 커밋 제거 undo — `Assets/Data/MonsterData`, `Assets/Scenes`
- `218cd343` 2026-08-04 몬스터 MonsterShield 부착 — `Assets/Data/MonsterData`, `Assets/Data/WaveData`
- `2d27bac5` 2026-08-04 오라 타겟 범위 수정 — `Assets/Scripts/Monster`
- `4b9deff4` 2026-08-04 보호형 몬스터 작업중 — `Assets/Scripts/Monster`, `Assets/Scripts/Combat`
- `ae4ea987` 2026-08-04 보호형 몬스터 구현 완료 — `Assets/Data/MonsterData`, `Docs/Sangwook`
- `1ca0ba8f` 2026-08-05 쌤플씬 갱신 — `Assets/Scenes`
- `8c180683` 2026-08-05 쌤플씬 참조 연결 — `Assets/Scenes`, `Assets/Data/TowerData`
- `160b90e4` 2026-08-05 단축키 및 보호형 몬스터  버그 수정 — `Assets/Scripts/Managers`, `Assets/Scripts/Combat`
- `3fdd826c` 2026-08-05 쇼트컷 추가 — `Assets`
- `4b847395` 2026-08-05 프리팹 연결 — `Assets/Scripts/Managers`, `Assets/Scenes`
- `52e3eedd` 2026-08-05 _population 접근 protected로 변경 — `Assets/Scripts/Buildings`
- `3f2dc2e8` 2026-08-05 새끼용 공격모드 구현 및 테스트 완료 — `Assets/Data/BabyDragon`, `Assets/Scenes`
- `5a680734` 2026-08-05 생명 새끼용 버프 수정 — `Assets/Scripts/Buildings`, `Assets/Scenes`
- `959afc0d` 2026-08-06 암석 새끼용 구현 및 테스트 완료 — `Assets/Data/BabyDragon`
- `5e89151b` 2026-08-06 새끼 시간 용 작업 중 — `Assets/Data/BabyDragon`, `Assets/Scripts/Buildings`
- `cf50b14b` 2026-08-06 튜토리얼용 3일 웨이브 스케줄 — `Assets/Data/WaveData`
- `9a1533c2` 2026-08-06 새끼용 버프용 그리드 수정 중 — `Assets/Scripts/Grid`
- `bed475f7` 2026-08-06 얼음용 공격 모드 구현완료 — `Assets/Data/BabyDragon`, `Assets/Data/MonsterData`
- `c2a15263` 2026-08-06 얼음 용 구현 및 테스트 완료 — `Assets/Scenes`, `Assets/Data/BabyDragon`
- `f026b2eb` 2026-08-06 얼음용 버프 모드 구현 — `Assets/Scripts/Buildings`, `Assets/Scenes`
- `0b459b2f` 2026-08-07 어미 용 스킬 세부 스탯 수정 — `Assets/Data/Dragon`
- `1663515e` 2026-08-07 docs: 어미용 스킬 기획 갱신 — `Docs/Sangwook`, `Docs`
- `5d166ff6` 2026-08-07 gitignore 수정 및 빌드 노트 & 결과 보고서 — `Docs`, `(저장소 루트)`
- `a6c17df3` 2026-08-07 플레이 가이드에 단축키/타워/용/몬스터 정보 추가 — `Docs`
- `cd6dbad3` 2026-08-07 빌드용 웨이브 스케줄 밸런스 데이터 수정 — `Assets/Data/WaveData`, `Assets/Scenes`
- `dc2085bf` 2026-08-07 시간 용 구현 완료 — `Assets/Scripts/Buildings`, `Assets/Scripts/Grid`
- `bfbd5d61` 2026-08-07 새끼 불 용 구현 완료 — `Assets/Data/BabyDragon`, `Assets/Scripts/Combat`
- `0c1a8b2d` 2026-08-08 용 빌드트리 프로토타입 작업 — `Docs/Sangwook`, `Assets/Data/Dragon`
- `f5766fdd` 2026-08-09 용 빌드 트리 전면 개편 작업 중 — `Assets/Data/Dragon`, `Assets/Scripts/Dragon`
- `e14c1129` 2026-08-10 암석 어미용 액티브 스킬 방벽 구현 완료 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `a2fae2a4` 2026-08-10 방벽 미세 스탯 조정 — `Assets/Data/Dragon`
- `c8ac8bd3` 2026-08-10 생명 어미용 패시브 자원에 목재 추가 — `Assets/Data/Dragon`
- `3a19c380` 2026-08-11 암석 어미용 메테오 스킬 사용 개선 — `Assets/Scripts/UI`
- `1670af0c` 2026-08-11 스킬 쿨타임 표시 처리 — `Assets/Scripts/UI`
- `cbab1195` 2026-08-11 새끼용 어디든지 설치 및 소환 가능 — `Assets/Scripts/Grid`, `Assets/Scripts/Combat`
- `e54901f9` 2026-08-11 csv 업데이트 — `Assets/StreamingAssets/Localization`
- `456e2042` 2026-08-11 얼음용 빙결 수정 — `Assets/Scripts/Buildings`, `Assets/Scripts/Monster`
- `8b1ece5a` 2026-08-11 용스킬트리 csv 업데이트 — `Docs`, `Assets/StreamingAssets/Localization`
- `ff138811` 2026-08-12 Auto stash before checking out "origin/master" — `Assets/Scenes`, `Assets/Data`
- `16a1abec` 2026-08-12 몬스터 스탯 수정 — `Assets/Data/MonsterData`
- `c63df9bd` 2026-08-12 용 스킬 트리 생성기 수정 — `Assets/Scripts/Dragon`
- `9b4650d9` 2026-08-12 전역빙결 세분화 — `Assets/Scenes`, `Assets/Data/Dragon`
- `af6b437d` 2026-08-12 보스 몬스터 군중제어 면역 구현 (#174) — `Assets/Scripts/Combat`, `Assets/Data/MonsterData`
- `0d5a139c` 2026-08-12 Refactor: 성능 및 구조 최적화 (GetComponent 캐싱 및 GC 할당 제거) — `Assets/Scripts/Monster`, `Assets/Scripts/UI`
- `878400c8` 2026-08-12 이슈 185 버그 및 코드 개선 사항 수정 — `Assets/Scripts/Grid`, `Assets/Scripts/Dragon`
- `65848b6e` 2026-08-12 [bugfix] 밤 동안 새끼용의 공격/버프 모드 변경 차단 (#173) — `Assets/StreamingAssets/Localization`, `Assets/Scripts/Buildings`
- `15c59aed` 2026-08-12 얼음어미용 빙결 스킬 상태이상 SO 참조 연결 — `Assets/Data/Dragon`
- `e9e81455` 2026-08-12 [feature] 용 스킬트리 UI 시인성 개선 - 노드 디자인 분리·간격 확대·속성 색 강조 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `35857f2c` 2026-08-13 보호막 재생형 몬스터 구현 완료 — `Assets/Data/MonsterData`, `Assets/Data/WaveData`
- `7a9b6837` 2026-08-13 Add 8 new monsters and update element immunity logic — `Assets/Data/MonsterData`, `Assets/Data/DamageEffect`
- `1fc1cb49` 2026-08-13 Auto stash before checking out "origin/master" — `Docs`
- `4fd54d36` 2026-08-13 Docs 문서 내용 갱신 — `Docs/Sangwook`, `Docs`
- `9fb44866` 2026-08-14 클로드, 안티그래비티 웨이브 삭제 및 밸런스 노트 작성 — `Assets/Data/WaveData`, `Assets/Data/ConquestData`
- `cf89a80b` 2026-08-14 EnemyEnhancement 페널티 갱신 (새로운 몬스터 추가) — `Assets/Data/ConquestData`, `Docs`
- `2fcc2c9a` 2026-08-14 샘플씬 웨이브 수정 — `Assets/Data/WaveData`, `Assets/Scenes`
- `1c142d8e` 2026-08-14 Auto stash before checking out "origin/master" — `Assets/Data/WaveData`, `Assets/Editor`
- `6743f368` 2026-08-14 AI별 웨이브 밸런스 조절 — `Assets/Data/WaveData`, `Assets/Editor`
- `eb571ebb` 2026-08-14 밸런스 패치 — `Docs`, `Assets/Scenes`
- `40b0a1e7` 2026-08-14 AGY 웨이브 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`

### 베타 (74개)

- `0a802bcc` 2026-08-16 구현현황 문서 생긴 — `Docs`
- `b3ed52ac` 2026-08-16 밸런스 작업 — `Assets/Scenes`, `Docs/BalanceReport`
- `c42d219c` 2026-08-17 실제 밸런스 테스트 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `371c6748` 2026-08-17 밸런스 조정 — `Assets/Data/WaveData`, `Docs/BalanceReport`
- `d2bde58d` 2026-08-17 새끼용 불 화상을 어미용 화상과 분리 — `Assets/Data/Dragon`, `Assets/StreamingAssets/Localization`
- `732cad39` 2026-08-17 새끼용 효과 밸런스 버프 — `Assets/Data/BabyDragon`
- `967457f3` 2026-08-17 암석용 관련 미세 버그 수정후 밸런스 조정 — `Assets/Scenes`, `Assets/Scripts/Monster`
- `19f3fbd6` 2026-08-17 점령 비용 감소 — `Assets/Tests/Editor`, `Assets/Scripts/Managers`
- `10c0d9b8` 2026-08-17 씬 조정 — `Assets/Scenes`
- `7487cc7d` 2026-08-18 저격병 버그 수정 — `Assets/Data/MonsterData`, `Assets/StreamingAssets/Localization`
- `3628fff7` 2026-08-18 연구 트리 시뮬레이션 툴 — `Docs/BalanceReport`, `Docs`
- `fcad4467` 2026-08-18 점령 페널티 데이터 조정 — `Assets/Data/ConquestData`, `Docs/BalanceReport`
- `940fdf4e` 2026-08-18 점령 페널티 다양화 — `Assets/Data/ConquestData`, `Assets/Data/TowerData`
- `ea0bf35a` 2026-08-18 밸런스 패치 — `Assets/Data/ConquestData`, `Docs/BalanceReport/simulator`
- `1fd08bba` 2026-08-18 용 스킬트리 ui 개편 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `3992c557` 2026-08-18 밸런스 조정 — `Assets/Data/MonsterData`, `Assets/Data/TowerData`
- `23107326` 2026-08-19 저격병 하향 — `Assets/StreamingAssets/Localization`, `Assets/Data/MonsterData`
- `0bde6dc9` 2026-08-19 중간보고 빌드본 — `Assets/Data/MonsterData`, `Assets/StreamingAssets/Localization`
- `db840c8d` 2026-08-19 몬스터 이미지 변경 / 밸런스 수정 / 대공 타워 추가 — `Assets/Data/MonsterData`, `Assets/Data/TowerData`
- `f1e2862e` 2026-08-19 발표자료 — `Docs`, `Assets/Scenes`
- `91249b48` 2026-08-19 대공 타워 buildmode ui에 추가 — `Assets/Scenes`, `Assets/Data/TowerData`
- `d1f22822` 2026-08-19 최종 보스 웨이브 및 보스 이미지 수정 — `Assets/Data/MonsterData`, `Assets/Data/DamageEffect`
- `b400d0c3` 2026-08-20 feat(tower): 은신 타워 코어 시스템 추가 — `Assets/Scripts/Buildings`, `Assets/Scripts/Monster`
- `0d3a9be3` 2026-08-20 feat(tower): 생명 타워 전용 힐러 공격 컴포넌트 추가 — `Assets/Scripts/Buildings`, `Assets/Scripts`
- `b3f7aa4f` 2026-08-20 fix(tower): TowerPopulation 컴포넌트가 없는 특수 타워가 에러를 발생시키지 않도록 예외 처리 — `Assets/Scenes`, `Assets/Scripts/Buildings`
- `d2d599ff` 2026-08-20 feat(tower): 영혼 타워(인구수 부스팅) 구현 — `Assets/Data/TowerData`, `Assets/Scripts/Buildings`
- `a0e905a3` 2026-08-20 feat(tower): 인구 할당 없이 가동되는 AlwaysOnStaffing 컴포넌트 추가 — `Assets/Data/TowerData`, `Assets/Scenes`
- `39556458` 2026-08-20 영혼 타워 구현 — `Assets/Data/TowerData`
- `fe342307` 2026-08-20 어미용 궁극기 효과 부여 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `60ea8af7` 2026-08-20 Auto stash before rebase of "master" onto "origin/master" — `Assets/StreamingAssets/Localization`, `Assets/Data/MonsterData`
- `c8ec5b80` 2026-08-21 특수 타워 — `Assets/Data/TowerData`, `Assets/StreamingAssets/Localization`
- `1b7c4aea` 2026-08-21 빌드노트 — `Docs`
- `604435b3` 2026-08-22 특수 타워 데이터 생성 — `Assets/Scripts/Buildings`, `Assets/Data/TowerData`
- `4b6b8b05` 2026-08-22 연구 트리 대규모 개편 작업 시작 — `Assets/Data/Research`, `Assets/Scripts/Research`
- `9bab621b` 2026-08-22 Auto stash before merge of "beta" and "feature/landmark-tower" — `Assets/Data/Research`, `Assets/Scripts/Research`
- `5265d35e` 2026-08-22 Auto stash before merge of "feature/landmark-tower" and "beta" — `Assets/Data/TowerData`, `Assets/Sprites/Tower`
- `cbc399d5` 2026-08-24 Fix: 궁극 잔존 상태이상이 몬스터 툴팁에 표시되지 않던 문제 수정 — `Assets/Data/Dragon`, `Assets/Scripts/Dragon`
- `411da37e` 2026-08-24 Fix: 어미용 "가동 강화"가 새끼용까지 강화하던 문제 수정 — `Assets/Scripts/Dragon`
- `88bee089` 2026-08-24 Chore: 용 스킬트리 상태이상 에셋 9개 재직렬화 반영 — `Assets/Data/Dragon`
- `9fb3f11d` 2026-08-24 Chore: 용 스킬트리 v2 잔재 고아 에셋 32개 삭제 및 생성기 문구 정정 — `Assets/Data/Dragon`, `Assets/Scripts/Dragon`
- `5458cf09` 2026-08-24 자원 +500 및 연구 완료 키 구현 — `Assets/Scripts/Managers`, `Assets/Scripts/Debug`
- `cec68679` 2026-08-24 연구 트리 전면 개편 완료 — `Assets/Data/Research`, `Assets/Scripts/Research`
- `441aa958` 2026-08-24 TowerMaxHealthApplier 샘플신에 추가 — `Assets/Scenes`
- `ec2d625b` 2026-08-24 연구 시스템 확인 — `Assets/Data/TowerData`, `Assets/Scripts/Buildings`
- `132d1efb` 2026-08-24 버그 정리 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `cad64577` 2026-08-24 Auto stash before rebase of "bug/214-dragon-upgrade" onto "origin/master" — `Assets/Sprites/Tower`, `Assets/Data/TowerData`
- `3f5fbff8` 2026-08-24 Fix: 새끼용 버프 관련 스킬트리 업그레이드 시 생산량이 즉시 갱신되지 않는 버그 수정 (#214) — `Assets/Scripts/Buildings`
- `360cee62` 2026-08-25 타워 랜드마크 이미지 생성 — `Assets/Sprites/Landmark`, `Docs`
- `427836b4` 2026-08-25 프리팹 수정 및 빌딩 카탈로그 갱신 — `Assets/Scenes`, `Assets/Data`
- `f5d36efa` 2026-08-25 가시타워 구현 & 타워 분류 추가 — `Assets/Scripts/Buildings`, `Assets/Data/Research`
- `ac0cf50f` 2026-08-25 연구소 초기 인구 cap +1 — `Assets/Data/Research`
- `049ea795` 2026-08-25 가시타워 이미지 추가 — `Assets/Sprites/Tower`
- `8cc13fbe` 2026-08-25 가시타워 인구 없음 처리 및 점령 보상 인구 청크당 +2 — `Assets/Scripts/Buildings`, `Assets/Data/ConquestData`
- `df2c81e4` 2026-08-25 타워 역설계 -> 특수 타워 해금으로 조건 임시 변경 — `Assets/Scripts/Buildings`, `Assets/Data/Research`
- `51867bdf` 2026-08-25 가시타워 이미지 적용 — `Assets/Data/TowerData`
- `868f1d72` 2026-08-25 가시타워 프리팹 사이즈수정 — `Assets/Data/TowerData`
- `00c38e78` 2026-08-26 BabyDragonEggNotifier 파일 이름 변경 — `Assets/Scripts/Buildings`, `Assets/Prefabs/UI`
- `3a6e8985` 2026-08-26 타워 투사체 프리팹 재연결 — `Assets/Data/TowerData`
- `8c5f8cd0` 2026-08-26 몬스터 애니메이션 수정 및 씬 배선 작업 — `Assets/Data/MonsterData`, `Assets/Scenes`
- `64b27330` 2026-08-26 랜드마크 유물 추가 — `Assets/Data/LandmarkData`, `Assets/Scripts/Landmark`
- `5309bac7` 2026-08-26 랜드마크 점령시 이미지 제거 — `Assets/Data/LandmarkData`
- `ac0960be` 2026-08-26 랜드마크와 연구 연동 — `Assets/Data/LandmarkData`, `Assets/Data/Research`
- `432affce` 2026-08-26 Speed_settings 프리팹 다시 연결 — `Assets/Scenes`
- `d04065a9` 2026-08-26 랜드마크 이미지 에셋 추가 — `Assets/Sprites/Landmark`
- `3dc57373` 2026-08-26 유물 획득 알림 & 특수 타워 건설 가능 알림 — `Assets/Data/LandmarkData`, `Assets/StreamingAssets/Localization`
- `16b40564` 2026-08-27 2주기 보스 스프라이트 수정 — `Assets/Data/MonsterData`, `Assets/Scripts/Buildings`
- `b474a86d` 2026-08-27 웨이브 데이터 수정 — `Assets/Data/WaveData`, `Assets/Scripts/Monster`
- `7b107fd1` 2026-08-27 몬스터 공격 애니메이션 활성화 — `Assets/Scripts/Monster`, `Assets/StreamingAssets/Localization`
- `81762ef3` 2026-08-27 팀원 당담역할 클로드 스킬 — `.claude/skills/contribution-report`, `(저장소 루트)`
- `587e0823` 2026-08-27 알림 개편 — `Assets/Data/TowerData`, `Assets/Scripts/Buildings`
- `5f71affa` 2026-08-27 이슈 #260 작업 — `Assets/Scripts/UI`
- `18417661` 2026-08-27 이슈#259 수정 — `Assets/Scripts/Buildings`, `Assets/Scripts/Research`
- `881f5567` 2026-08-27 시연영상 구성안 추가 — `Docs`
- `435a4b4e` 2026-08-28 Docs 내부 폴더 추가 — `Docs/BuildNote`, `Docs/Lcoalizing`

---

# 조강현

커밋 181개 · 2026-07-03 ~ 2026-08-28 · 이메일 `jokh980324@naver.com`

## 가장 많이 만진 디렉터리

| 디렉터리 | 이 팀원 터치 | 이 팀원 작업 중 | 그 디렉터리 전체 중 |
|---|---:|---:|---:|
| `Assets/Scripts/UI` | 291 | 14.1% | **41.9%** |
| `Assets/Data/WaveData` | 128 | 6.2% | **21.4%** |
| `Assets/Scenes` | 127 | 6.1% | **20.7%** |
| `Assets/Prefabs/UI` | 124 | 6.0% | **22.8%** |
| `Assets/Scripts/Buildings` | 102 | 4.9% | **21.7%** |
| `Assets/StreamingAssets/Localization` | 73 | 3.5% | **32.0%** |
| `Assets/Scripts/Grid` | 73 | 3.5% | **25.8%** |
| `Assets/Scripts/Managers` | 66 | 3.2% | **37.9%** |
| `Assets/Data/Research` | 64 | 3.1% | **23.1%** |
| `Assets/Data/MonsterData` | 64 | 3.1% | **18.7%** |
| `Assets/Data/Dragon` | 64 | 3.1% | **16.8%** |
| `Assets/Scripts/Save` | 54 | 2.6% | **100.0%** |
| `Assets/Prefabs/Building` | 38 | 1.8% | **20.9%** |
| `ProjectSettings` | 37 | 1.8% | **66.1%** |
| `Assets/Scripts` | 35 | 1.7% | **21.9%** |

마지막 열이 높을수록 그 영역을 혼자 맡았다는 뜻이다. 낮으면 공동 작업 구역이다.

## 이 팀원만 만진 파일 (단독 소유)

전체 489개 중 상위 25개.

| 파일 | 수정 횟수 |
|---|---:|
| `Assets/Scripts/UI/UI_ConfigWindow.cs` | 10 |
| `Assets/Scripts/Save/SaveDtos.cs` | 9 |
| `Assets/Scripts/Save/SaveService.cs` | 9 |
| `Assets/Scripts/Save/SaveCapture.cs` | 9 |
| `Assets/Scripts/Save/SaveRestore.cs` | 9 |
| `Assets/Scripts/Settings/SettingsService.cs` | 6 |
| `Assets/Scripts/ResourceNode/ResourceForecast.cs` | 6 |
| `Assets/Scenes/SampleScene_Dragon.unity` | 6 |
| `Assets/Scripts/TerrainPenalty/TerrainUpkeepSystem.cs` | 5 |
| `Assets/Scripts/UI/Tooltip/UI_TooltipTrigger.cs` | 5 |
| `Assets/Scripts/TerrainPenalty/TerrainPenaltySystem.cs` | 5 |
| `Assets/Scripts/Portal/PortalWavePreviewRenderer.cs` | 5 |
| `Assets/Prefabs/Waves.prefab` | 4 |
| `Assets/Scripts/Managers/GlobalInputBootstrap.cs` | 4 |
| `Assets/Scripts/Managers/GameSpeedManager.cs` | 4 |
| `Assets/Scripts/Save/SaveResult.cs` | 4 |
| `Assets/Settings/UniversalRP.asset` | 4 |
| `Assets/Scripts/UI/BuildingEffect/BuildingEffectResolver.cs` | 3 |
| `Assets/Scripts/UI/Tooltip/UI_MonsterTooltipDriver.cs` | 3 |
| `Assets/Prefabs/UI/Config_window.prefab` | 3 |
| `Assets/Scripts/TerrainPenalty/TerrainPenaltyCoordinator.cs` | 3 |
| `Assets/Scripts/Save/SaveLocKeys.cs` | 3 |
| `Assets/Scripts/UI/BuildingEffect/BuildingEffectBadgeSystem.cs` | 3 |
| `Assets/Scripts/GameResources/ResourceLocKeys.cs` | 3 |
| `Assets/Scripts/Help/HelpDiscoveryController.cs` | 3 |

## 병합된 PR·브랜치 (이 팀원 커밋이 가장 많은 것)

이 팀원이 직접 머지한 PR: **92건** (전체 110건 중 83.6%)

커밋 최다 작성자로 잡힌 PR은 없다.
위 머지 건수를 볼 것 — 이 팀원은 남의 PR을 받아 통합하는 쪽에 서 있었다.

## 커밋 전체 (시간순)

### 프로토타입 (85개)

- `e0807670` 2026-07-03 Init UnityProjects — `ProjectSettings`, `Assets`
- `a6f1c6a2` 2026-07-08 add coplay MCP — `Packages`, `ProjectSettings`
- `4816fda2` 2026-07-09 버그 해결 — `Assets/Scripts`
- `15439316` 2026-07-09 샘플 맵 — `Assets/Scenes`
- `b2c6e31b` 2026-07-09 추가 파일 — `Assets`
- `f5870cc8` 2026-07-09 타일맵 렌더 오류 해결 — `Assets/Settings`
- `1c175aba` 2026-07-09 Issue Template 추가 — `.github/ISSUE_TEMPLATE`
- `949826ec` 2026-07-09 CLAUDE.md 추가, 기본 폴더 구조 추가 — `Docs`, `(저장소 루트)`
- `e034e686` 2026-07-10 지도 업데이트 — `CoplayScripts`, `Assets/Scenes`
- `09e7ffe3` 2026-07-10 맵 프리팹 — `Assets/Prefabs`
- `4a5e8cff` 2026-07-10 Unitask 추가 — `Packages`, `(저장소 루트)`
- `07823ccf` 2026-07-10 GameManager / CycleManager — `Assets/Scripts/Managers`
- `1e50a709` 2026-07-14 라이팅(밤/낮) — `Assets/Prefabs`, `Assets/Scripts/Managers`
- `3a0f48cf` 2026-07-14 타워 스포트라이트 추가 — `Assets/Scripts/Buildings`, `ProjectSettings`
- `db72b0cb` 2026-07-14 Gridmap FloorDiv연산방식 변경 — `Assets/Scripts/Grid`
- `6029bf82` 2026-07-14 장식 자동 숨김 기능 — `Assets/Scenes`, `Assets/Scripts/Grid`
- `7191c567` 2026-07-15 고저차맵 — `Assets/Scenes`, `Assets/Scripts/Grid`
- `59f032c9` 2026-07-15 미니맵 — `Assets/Scripts`, `Assets/Scenes`
- `e69c47fa` 2026-07-15 성과 점령지가 파괴/이동되는 버그 수정 — `Assets/Prefabs/Building`, `Assets/Scripts/Grid`
- `d92e70c4` 2026-07-15 점령지 외곽선 — `Assets/Scripts/Grid`, `Assets/Scripts/Managers`
- `74ffd94d` 2026-07-15 메인 성을 이동하려고 할 수 있는 문제 해결 — `Assets/Scripts/Grid`
- `46682799` 2026-07-15 프리팹 버그 수정 1 — `Assets`
- `50efa8a1` 2026-07-15 UI버그 — `Assets`, `Assets/Prefabs/UI`
- `6a97c42a` 2026-07-15 프리팹 버그 수정 2 — `Assets`
- `ab8daff3` 2026-07-15 씬 옮김 — `Assets/Scenes`, `Assets`
- `436bec36` 2026-07-15 버그픽스 / 코드 정리 — `Assets/Prefabs/Building`, `Assets/Scripts/Grid`
- `5edafb00` 2026-07-15 타워 스포트라이트 추가 / 적을 조준 중일때만 스포트라이트 출력 — `Assets/Prefabs/Building`, `Assets/Scripts/Buildings`
- `34adef2b` 2026-07-16 미니맵 가시성 개선 — `Assets/Scripts`, `Assets/Scenes`
- `dbca3930` 2026-07-16 근접 몬스터가 성을 공격하지 않는 문제 수정 — `Assets/Prefabs/Monster`, `Assets/Scripts/Monster`
- `a687156f` 2026-07-16 디버그용 빌드에 디버그 점령 포함되도록 수정 — `Assets/Scripts/Conquest`
- `b980ef92` 2026-07-16 Scene 컴포넌트 재연결 — `Assets/Scenes`, `Assets/Data/MonsterData`
- `d135fb6d` 2026-07-16 Stringtable — `Assets/Packages/CsvHelper.33.1.0`, `Assets/Scripts/Util`
- `86487224` 2026-07-16 미니맵 시각화 강화 — `Assets/Scripts`, `ProjectSettings`
- `f79182e6` 2026-07-20 스킬 구현 — `Assets/Scripts/UI`, `Assets/Data/Skills`
- `945782a6` 2026-07-20 큰 건물 마우스오버 시 투명화 — `Assets/Prefabs/Building`, `Assets/Scripts/Buildings`
- `d09a8d80` 2026-07-20 stringtable 업데이트 — `Assets/Scripts/Util`, `Assets/StreamingAssets/Localization`
- `b6ae2a75` 2026-07-21 빌드 노트 gitIgnore — `(저장소 루트)`
- `53fe9af1` 2026-07-21 전장의 안개 1차 — `Assets/Data`, `ProjectSettings`
- `518e55de` 2026-07-21 적 타워 공격 데이터 수정 — `Assets/Data/MonsterData`, `Assets/Scripts/Grid`
- `b89b9dc9` 2026-07-21 Git 오류 해결2 — `Assets/Data/ResourceProductionData`, `Assets/Data/ResourceNodeData`
- `e2758129` 2026-07-21 Git 오류 해결 1 — `Assets/Data/ResourceProductionData`, `Assets/Data/ResourceNodeData`
- `e1f4a471` 2026-07-21 전장의 안개 — `Assets/Scripts/Grid`, `Assets/Scenes`
- `b7790240` 2026-07-21 스킬 가시성 개선 — `Assets/Scripts/UI`, `Assets/Scenes`
- `9a0d670a` 2026-07-21 전장의 안개 1차 — `Assets/Data`, `ProjectSettings`
- `ca251391` 2026-07-22 타워가 적을 죽일 떄마다 깜빡거리는 문제 해결 — `Assets/Scripts/Buildings`
- `960bd2f5` 2026-07-22 전장의 안개 개선(천천히 어두워지게) — `Assets/Scenes`, `Assets/Scripts/Grid`
- `0ac487a0` 2026-07-22 카메라 컨트롤러가 UI를 스크롤할때에도 화면을 축소/확대하는 문제 해결 — `Assets/Scripts`
- `a6ac4c33` 2026-07-22 Minimap Navigator — `Assets/Scenes`, `Assets/Prefabs/UI`
- `73e2f0cf` 2026-07-22 Action -> UnityEvent 통일 — `Assets/Scripts/Grid`, `Assets/Scripts/Managers`
- `f7a09b6f` 2026-07-22 상호 베타적 창 정비 — `Assets/Scripts/UI`, `Assets/Scripts/Managers`
- `a1c8a9b6` 2026-07-22 Event 발화 순서 정비 — `Assets/Scripts/Managers`, `(저장소 루트)`
- `5df1bc6c` 2026-07-22 점령 윈도우 수정 — `Assets/Scripts/UI`
- `199a25e3` 2026-07-22 SampleScene 수리 — `Assets/Scenes`, `Assets`
- `8039d4e2` 2026-07-23 낮밤 변환 시 빛이 천천히 바뀌도록 변경 — `Assets/Scenes`, `ProjectSettings`
- `bb825ecf` 2026-07-23 에디터 세팅 오류 해결 — `ProjectSettings`
- `22569dae` 2026-07-23 안개 변경 / 밤 낮 변경시 천천히 바뀌도록 변경 — `Assets/Data/Tiles`, `Assets/Scenes`
- `79e1b917` 2026-07-24 우클릭으로 건설 취소, GlobalInputBootstrap — `Assets/Scripts/Conquest`, `Assets/Scenes`
- `a58bd709` 2026-07-24 씬 플레이 느린 이슈 해결 — `Assets/Scenes`, `Assets/Prefabs`
- `5b5d45da` 2026-07-24 Build2 — `Assets/Data/MonsterData`, `Assets/Scripts/Debug`
- `707dd0bd` 2026-07-24 게임 시작 시 빛이 이상하게 출력되는 문제 해결 — `Assets/Scripts/Lighting`
- `93cc86e4` 2026-07-24 타워 공격범위 Isometirc에 맞춰서 변경/표시 — `Assets/Scripts/UI`, `Assets/Scripts/Util`
- `2a558f95` 2026-07-27 연구 UI — `Assets/Data/Research`, `Assets/Scripts/UI`
- `e1d51e1e` 2026-07-27 용 스킬트리 — `Assets/Data/Dragon`, `Assets/Scripts/Dragon`
- `f696dbf8` 2026-07-27 Exit Playmode 느린 이슈 해결 — `Assets/Scenes`, `ProjectSettings`
- `78aa35bb` 2026-07-27 용 스킬 작업2 — `Assets/Scripts/UI`, `Assets/Scenes`
- `68dce1c5` 2026-07-27 몹 스프라이트 적용 — `Assets/Data/MonsterData`, `Assets/Scripts/Monster`
- `8b0fc81d` 2026-07-28 타워(용) 애니메이션 — `Assets/Animation/BabyDragon`, `Assets/Scripts/Buildings`
- `1ae1f5e9` 2026-07-28 단차 관련 위치 불일치 문제 해결, 연구 4티어 조건 변경 — `Assets/Scripts/Grid`, `Assets/Scripts/Research`
- `42ad3e0e` 2026-07-28 merge stash — `Assets/Scripts/UI`
- `736c702b` 2026-07-28 Tower Population Debug GUI 버그 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `0c468335` 2026-07-28 각 건물 창 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `e1c810f9` 2026-07-28 Animator Null check — `Assets/Scripts/Monster`
- `d0a52860` 2026-07-29 체력바 추가 — `Assets/Scripts/Buildings`, `Assets/Scripts/UI`
- `42cbff67` 2026-07-29 용 액티브 스킬 — `Assets/Scripts/UI`, `Assets/Data/Dragon`
- `fdbcb8a7` 2026-07-29 미니맵 표시가 안개 아래로 깔리는 문제 수정 — `Assets/Scripts/Monster`, `Assets/Prefabs`
- `f0cb2986` 2026-07-29 Victory Window 적용 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `5d537bce` 2026-07-29 새끼용 / 어미용 UI / Build 3 — `Assets/Scripts/Buildings`, `Assets/Prefabs/UI`
- `12d079fa` 2026-07-29 낮에 스킬창 숨기도록 변경 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `83d37f2a` 2026-07-29 연구창/드래곤스킬창 ESC로 닫기 — `Assets/Scripts/UI`, `Assets/Scenes`
- `b4d463e0` 2026-07-29 아침이 되어도 타워 체력이 차지 않는 문제 수정 — `Assets/Scenes`, `Assets/Scripts/Buildings`
- `b5057465` 2026-07-29 공격로 표시 — `Assets`, `Assets/Scenes`
- `83da1e60` 2026-07-29 scene 오류 — `Assets/Scenes`
- `64c4dfa2` 2026-07-30 와이어링 누락 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `d5f2d432` 2026-07-30 small fixes — `Assets/Scripts/Monster`, `Assets/Scripts/Buildings`
- `c51bea19` 2026-07-31 MERGE — `Assets/Prefabs`, `Assets/Scenes`

### 알파 (49개)

- `613a8fa1` 2026-08-03 인구 배치 모드 추가 — `Assets/Scripts/Population`, `Assets/Prefabs/UI`
- `aa01d1d5` 2026-08-03 구 데이터 정리 — `Assets/Data/WaveData`, `Assets/Scripts/Wave_Main`
- `ee519b35` 2026-08-03 웨이브 생성 방식 변경, 웨이브 표시기 개선 — `Assets/Data/WaveData`, `Assets/Scripts/Wave_Main`
- `c79dfa6f` 2026-08-03 WorkerMode 연결 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `ccd103a4` 2026-08-03 적 예고 / 새끼용 cyclemanager 이벤트 더이상 사용하지 않는 코드 삭제 — `Assets/Prefabs/UI`, `Assets/Prefabs`
- `223042ee` 2026-08-04 저장용 캡처 기능 추가 — `Assets/Scripts/Save`, `Assets/Scenes`
- `4e5faa0d` 2026-08-04 SoundManager — `Assets/Scripts/Sound`, `Assets/Prefabs/UI`
- `6cdcf2d1` 2026-08-04 저장/로드 틀, 설정창 — `Assets/Scripts/Save`, `Assets/Scripts/Managers`
- `cfa51bd2` 2026-08-05 점령모드 점령지역 표시선 연결 — `Assets/Scenes`
- `4522d2d0` 2026-08-05 버그 수정, 새끼용이 버프 모드에서도 공격할 수 있는 문제 해결 — `Assets/Scripts/Grid`, `Assets/Scenes`
- `e6b31ea4` 2026-08-05 시작화면/세이브,로드/사운드메니저 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `cc0ca134` 2026-08-05 슬라임 농장에 슬라임 표시 — `Assets/Scripts/Buildings`, `Assets/Scenes`
- `94307e50` 2026-08-06 드래곤 관리창에서 슬라임 생산량에 따라 UI가 맞춰지도록 변경 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `187c6f29` 2026-08-06 [Bug] 마지막 주기 (최종보스웨이브) 후에도 새끼용 알 보상 나옴 — `Assets/Scripts/Save`, `Assets/Tests/Editor`
- `48633a07` 2026-08-06 scene 갱신 — `Assets/Scenes`
- `674b867b` 2026-08-06 툴팁 버그 수정 — `Assets/Scripts/UI`, `Assets/StreamingAssets/Localization`
- `bc8c353f` 2026-08-06 자원 소모량 표시 / 툴팁 추가 — `Assets/Scripts/UI`, `Assets/Scripts/ResourceNode`
- `2a83b8f7` 2026-08-06 세이브 관련 오류 수정 — `Assets/Scripts/Save`, `Assets/Scripts/UI`
- `d9da2b8f` 2026-08-06 스킬 사용 시 다른 창을 닫도록 수정 — `Assets/Scripts/UI`, `Assets/Scripts/Managers`
- `7cd982f8` 2026-08-06 지형별 패널티 추가 — `Assets/Scripts/TerrainPenalty`, `Assets/Tests/Editor`
- `068f06fa` 2026-08-06 Null체크  관련 오루 수정/ 효과 과다 해제 방지 — `Assets/Scripts/Modifiers`, `Assets/Scripts/Managers`
- `6b9aa4ec` 2026-08-06 UI관련 버그 수정 — `Assets/Scripts/UI`
- `8e8b05ef` 2026-08-06 와이어링 실수 막는 기능 추가. — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `511b5067` 2026-08-06 자원UI 레이아웃 꼬이는 문제 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `b9d23aa8` 2026-08-11 밤에 저장버튼 비활성화 — `Assets/Scripts/Save`, `Assets/Scripts/UI`
- `b9ad3b78` 2026-08-11 CameraController 연결 복구 — `Assets/Scenes`
- `8db9e88f` 2026-08-11 몹 툴팁 / 지역 디버프 툴팁 — `Assets/Data/MonsterData`, `Assets/Scripts/UI`
- `2e366a31` 2026-08-11 툴팁 추가 — `Assets/Scripts/Monster`, `Assets/Scripts/UI`
- `c3b4dd8d` 2026-08-11 타워에 공중/지상 필터링 추가 — `Assets/Scripts/Combat`, `Assets/Scripts/Buildings`
- `0cae331a` 2026-08-11 자원 예상에 슬라임 증감(새끼용)추가 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `adb24c1a` 2026-08-11 지도 클릭으로 카메라 이동 시 카메라가 맵 밖으로 나갈 수 있는 문제 해결 — `Assets/Scripts`, `Assets/Scenes`
- `e349ff75` 2026-08-11 언어 변경시 툴팁이 바뀌지 않는 오류 수정 — `Assets/Scenes`, `Assets/Scripts/UI`
- `eb6861ff` 2026-08-11 클로드 스킬 추가 — `.claude/skills/stringtable-diff`
- `93ee8288` 2026-08-11 게임 시작 시 카메라가 일정 위치로 튕겨나가는 오류 수정 — `Assets/Scenes`, `Assets/Scripts`
- `c7d2a893` 2026-08-12 랜드마크(새끼용 알) 추가 / ESC로 설정창 열기 / 설정창 열리면 게임 일시정지 — `Assets/Data/LandmarkData`, `Assets/Scripts/Landmark`
- `6cdc78a1` 2026-08-12 와이어링 수정 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `0e9f626d` 2026-08-12 쳥크 구조 변경 — `Assets/Scripts/Grid`, `Assets/Data/Tiles`
- `4bbbf7f5` 2026-08-12 랜드마크 시스템 — `Assets/Data/Research`, `Assets/Scripts/Landmark`
- `7560f1e4` 2026-08-12 [bugfix] 메인 성에 작동정지 툴팁이 표시되는 오류 수정 — `Assets/Scripts/Buildings`, `Assets/Scripts/UI`
- `df343099` 2026-08-12 키설정창 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `139362c2` 2026-08-13 타워 명칭 Stringtable 적용 — `Assets/Data/ResourceProductionData`, `Assets/Data/TowerData`
- `eb81863d` 2026-08-13 밤에 단축키로 여러 모드에 접근할 수 있던 문제 수정 — `Assets/Scripts/UI`, `Assets/Scripts/Population`
- `6b18c992` 2026-08-13 연구창 아이콘 겹쳐지는 문제 수정 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `9a8a7fc4` 2026-08-13 카메라 Missing Script 해결 — `Assets/Scenes`
- `8117baef` 2026-08-13 missing script 수정 — `Assets/Scenes`
- `5023dfda` 2026-08-13 밤에도 어미용 속성을 바꿀 수 있는 문제 수정 — `Assets/Scripts/UI`
- `8a526959` 2026-08-13 벨런싱 조절 — `Assets/Data/Research`, `Assets/Tests/Editor`
- `2b725e58` 2026-08-14 불 슬라임이 슬라임 목장에 표시되지 않는 문제 해결 / 와이어링 체크 스킬 생성 — `Assets/Scripts/UI`, `Assets/Scripts/Util`
- `846b9519` 2026-08-14 소규모 수정점들. 건설 모드 관련 버그 디버그 다수 — `Assets/Prefabs/Building`, `Assets/Data/TowerData`

### 베타 (47개)

- `80620605` 2026-08-18 도움말 도감 틀 / 가이드 퀘스트 초기본 — `Assets/Data/Guide`, `Assets/Scripts/Guide`
- `5a636576` 2026-08-18 도움말 창 — `Assets/Data/Help`, `Assets/Scripts/Help`
- `a552e784` 2026-08-19 새끼용이 굶어도 바닥에 안 쓰러지는 문제 수정 — `Assets/Scripts/Buildings`
- `ab20d892` 2026-08-19 시작씬 설정창 복구 / 시작창 설정에서 튜토리얼 보임,안보임 설정 표시하지 않도록 수정 — `Assets/Scripts/UI`, `Assets/Scenes`
- `a5330ef1` 2026-08-19 1일차 재시작/새 게임 시에도 자동저장되는 문제 수정 — `Assets/StreamingAssets/Localization`, `Assets/Prefabs/UI`
- `5da77ec2` 2026-08-19 세이브/로드창 레터박스 제거 — `Assets/Prefabs/UI`, `Assets/Scripts/Save`
- `7f42b929` 2026-08-19 필드 적 툴팁 — `Assets/Data/Dragon`, `Assets/Data/BabyDragon`
- `2930a890` 2026-08-19 미니맵 레터박스 제거 — `Assets/Scripts`, `Assets/Scenes`
- `f9532b6e` 2026-08-19 밤 진입 시 식량 확인 — `Assets/Scripts/UI`, `Assets/Scripts/Managers`
- `bd9466c2` 2026-08-19 조언자 UI 위치 오류 — `Assets/Scripts/UI`
- `9e307196` 2026-08-19 도움말 추가 — `Assets/Data/Help`, `Assets/Scripts/Tutorial`
- `a1d6cc53` 2026-08-19 가이드 퀘스트 개선 — `Assets/Scripts/UI`, `Assets/StreamingAssets/Localization`
- `521d51e3` 2026-08-19 다른 건물 뒤에 가려진 건물을 선택할 수 없는 문제 수정(라운드로빈) — `Assets/Scripts/UI`, `Assets/Scripts/Grid`
- `3ac3e989` 2026-08-19 조언자 카드 위치 오류 수정 — `Assets/Scenes`, `Assets/Scripts/UI`
- `716947f2` 2026-08-19 rebase 오류 수정 — `Assets/Data/Tutorial`, `Assets/Scripts/Guide`
- `c6a347a4` 2026-08-21 100개 이상의 재료가 들어가는 타워의 줄바꿈 수정 — `Assets/Prefabs/UI`
- `aa8bacb9` 2026-08-21 건설 전 생산량 미리보기 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `f5c86996` 2026-08-21 Build 6 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `5937c422` 2026-08-21 슬라임 잔량 UI 툴팁 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `9c2a80c3` 2026-08-24 건설모드 철거 환급비용 잘못 출력되는 오류 수정 — `Assets/StreamingAssets/Localization`, `Assets/Prefabs/UI`
- `6268c16d` 2026-08-24 읽지 않은 도움말 표시기 — `Assets/Scripts/Help`, `Assets/Scripts/UI`
- `619515ff` 2026-08-24 지형별 슬라임 도움말 개선 — `Assets/Data/Help`, `Assets/Scripts/Help`
- `b73e4ffe` 2026-08-24 건설 창에 필요없는 정보 삭제 / 설정창 메인메뉴로 돌아가기/게임종료 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `6131e435` 2026-08-24 바위 새끼용이 암석지대 패널티 완화 — `CoplayScripts`, `Assets/Data/BabyDragon`
- `e48f0c05` 2026-08-25 세이브/로드 시 알 획득 툴팁이 출력되는 오류 수정 — `Assets/StreamingAssets/Localization`, `Assets/Scripts/Managers`
- `54a15633` 2026-08-25 하드모드 추가 — `Assets/Scripts/UI`, `Assets/Data/Mutators`
- `ef51c850` 2026-08-25 몬스터 애니메이션 — `Assets/Data/MonsterData`
- `f8228a34` 2026-08-25 세이브/로드로 새끼용의 상태(굶음/정상)를 초기화할 수 있던 오류 수정 — `Assets/Scripts/Save`, `Assets/Scenes`
- `9bdcb7a3` 2026-08-25 도움말 창 각 탭 제목으로 드래그할 시 스크롤이 드래그되지 않는 문제 수정 — `Assets/Prefabs/UI`
- `c55584d0` 2026-08-25 하드모드 추가 작업 — `Assets/Data/Mutators`, `Assets/StreamingAssets/Localization`
- `92c923e5` 2026-08-26 영혼 타워 버그 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `9ce77f42` 2026-08-26 '놀고 있는 건물을 정리한다'가이드퀘스트 클리어되지 않는 문제 수정 — `Assets/Scripts/Tutorial`, `Assets/StreamingAssets/Localization`
- `d778726e` 2026-08-27 UI에 마우스가 올라가 있을 때 화면이 스크롤링 되는 문제 수정 — `Assets/Scripts`
- `19271917` 2026-08-27 드래그로 카메라 이동이 울렁거리는 문제 수정 — `Assets/Scripts`
- `55fa6d5b` 2026-08-27 랜드마크 크기 조절 — `Assets/Prefabs/Landmark`
- `d86a4521` 2026-08-27 배속 기능 재연결 — `Assets/Scenes`
- `96bda9ec` 2026-08-27 타워 판정 위치 정확히 맞춤 — `Assets/Scripts/Buildings`, `Assets/Scripts`
- `2b77e412` 2026-08-27 와이어링 오류 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `ffd7aecb` 2026-08-27 튜토리얼 오류 수정 / 연구소 지형 효과 적용 — `Assets/Scripts/UI`, `Assets/Scripts/Research`
- `e15a03bb` 2026-08-27 다른 창이 떠있는 중에 카메라 접근 가능한 문제 수정 — `Assets/Prefabs/UI`, `(저장소 루트)`
- `032a410c` 2026-08-27 각 건물/타워/새끼용의 효과 사거리 기준위치를 스프라이트 중앙이 아니라 셀 중앙으로 변경 — `Assets/Scripts/Buildings`, `Assets/Scripts/Grid`
- `e3d544eb` 2026-08-27 [Feature] 아웃라인 기능 확장 — `Assets/Prefabs/Building`, `Assets/Scripts/UI`
- `d5e63b06` 2026-08-27 오류 수정 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `1cfd7adb` 2026-08-27 적 도감 — `Assets/Scripts/Help`, `Assets/Data/Help`
- `43532b79` 2026-08-27 시작화면 '하드모드' 버튼이 보이지 않을 때에도 자리를 점유하는 문제 수정 — `Assets/Scenes`, `CoplayScripts`
- `545de212` 2026-08-28 Contributions — `Docs/Contributions`
- `95307d66` 2026-08-28 용 스킬 툴팁 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`

---

# 이하늘

커밋 240개 · 2026-07-09 ~ 2026-08-28 · 이메일 `skyee46@gmail.com`, `150312623+smflsmfqh@users.noreply.github.com`

## 가장 많이 만진 디렉터리

| 디렉터리 | 이 팀원 터치 | 이 팀원 작업 중 | 그 디렉터리 전체 중 |
|---|---:|---:|---:|
| `Assets/Data/Tutorial` | 662 | 19.6% | **99.5%** |
| `Assets/Sprites/Buildings` | 336 | 10.0% | **98.5%** |
| `CoplayScripts` | 285 | 8.4% | **92.8%** |
| `Assets/Scripts/UI` | 193 | 5.7% | **27.8%** |
| `Assets/Scenes` | 174 | 5.2% | **28.3%** |
| `Assets/Scripts/Grid` | 170 | 5.0% | **60.1%** |
| `Assets/Scripts/Buildings` | 168 | 5.0% | **35.7%** |
| `Assets/Scripts/Tutorial` | 139 | 4.1% | **91.4%** |
| `Assets/Prefabs/UI` | 110 | 3.3% | **20.2%** |
| `Assets/Prefabs/Building` | 102 | 3.0% | **56.0%** |
| `Docs` | 87 | 2.6% | **43.5%** |
| `Assets/Scripts/Conquest` | 78 | 2.3% | **72.9%** |
| `Assets/Scripts` | 66 | 2.0% | **41.3%** |
| `Assets/Data/Dragon` | 65 | 1.9% | **17.0%** |
| `Assets/Scripts/Managers` | 62 | 1.8% | **35.6%** |

마지막 열이 높을수록 그 영역을 혼자 맡았다는 뜻이다. 낮으면 공동 작업 구역이다.

## 이 팀원만 만진 파일 (단독 소유)

전체 738개 중 상위 25개.

| 파일 | 수정 횟수 |
|---|---:|
| `Assets/Scenes/GridScene.unity` | 56 |
| `Assets/Scenes/Haneul_ResourceNodeScene.unity` | 20 |
| `Assets/Prefabs/Tutorial System.prefab` | 17 |
| `Assets/Scripts/Tutorial/TutorialStepSO.cs` | 14 |
| `Assets/Scenes/Haneul_TowerEffectScene.unity` | 11 |
| `Assets/Prefabs/Building/LoggingCamp.prefab` | 11 |
| `Assets/Scenes/Haneul_BabyDragonGuide.unity` | 11 |
| `Assets/Scripts/Grid/TerrainTileMap.cs` | 9 |
| `Assets/Scripts/GridMap.cs` | 9 |
| `Assets/Scripts/Tutorial/TutorialTipChainController.cs` | 9 |
| `Assets/Scenes/Haneul_SealStoneScene.unity` | 9 |
| `Assets/Prefabs/Building/Quarry.prefab` | 9 |
| `Assets/Sprites/Buildings/LoggingCamp.png` | 9 |
| `Assets/Scripts/Tutorial/TutorialCastleGuard.cs` | 8 |
| `CoplayScripts/BuildStatusVfx.cs` | 8 |
| `Assets/Scripts/Tutorial/TutorialScenarioController.cs` | 8 |
| `Docs/타워이펙트_작업노트.md` | 8 |
| `Docs/타워_전투_효과음_설계.md` | 8 |
| `CoplayScripts/AssignTowerProjectiles.cs` | 8 |
| `Assets/Scripts/UI/Guide/GuideAnchorId.cs` | 8 |
| `Assets/Prefabs/UI/ResourceCost.prefab` | 7 |
| `Assets/Scripts/Combat/ProjectilePool.cs` | 7 |
| `Assets/Scripts/Grid/Editor/TerrainTileMapEditor.cs` | 7 |
| `Assets/Data/Tutorial/TS_260_ClickCastle.asset` | 7 |
| `Assets/Prefabs/Building/FarmField.prefab` | 7 |

## 병합된 PR·브랜치 (이 팀원 커밋이 가장 많은 것)

이 팀원이 직접 머지한 PR: **8건** (전체 110건 중 7.3%)

35건. 머지를 누른 사람이 아니라 **안에 담긴 커밋의 작성자**로 귀속했다.
로컬 동기화 머지는 제외하고 GitHub PR 머지만 센다.

| PR | 브랜치 | 머지일 | 담긴 커밋 |
|---|---|---|---|
| #13 | `feature/1` | 2026-07-09 | 이하늘 11 |
| #25 | `feature/building` | 2026-07-10 | 이하늘 11 |
| #33 | `feature/building` | 2026-07-10 | 이하늘 2 |
| #35 | `feature/building` | 2026-07-10 | 이하늘 4 |
| #40 | `feature/chunk` | 2026-07-13 | 이하늘 7 |
| #41 | `feature/re-chunk` | 2026-07-13 | 이하늘 1 |
| #49 | `feature/conquer` | 2026-07-15 | 이하늘 6 |
| #59 | `feature/conquer` | 2026-07-16 | 이하늘 11 |
| #79 | `feature/conquer` | 2026-07-20 | 이하늘 9 |
| #83 | `feature/ResourceNode` | 2026-07-21 | 이하늘 10 |
| #88 | `feature/ResourceNode` | 2026-07-22 | 이하늘 5 |
| #94 | `feature/rotateBuilding` | 2026-07-23 | 이하늘 4 |
| #95 | `feature/buildingCost` | 2026-07-23 | 이하늘 1 |
| #99 | `feature/buildingCostUI` | 2026-07-24 | 이하늘 3 |
| #117 | `feature/babyDragon` | 2026-07-28 | 이하늘 9 |
| #120 | `feature/babyDragonUI` | 2026-07-28 | 이하늘 1 |
| #121 | `feature/specialFactory` | 2026-07-28 | 이하늘 1 |
| #123 | `feature/buildingAssets` | 2026-07-29 | 이하늘 3 |
| #130 | `feature/stoneEnding` | 2026-07-31 | 이하늘 7 |
| #131 | `feature/conquesetBugFix` | 2026-07-31 | 이하늘 1 |
| #153 | `feature/conquestResidual` | 2026-08-03 | 이하늘 5 |
| #176 | `feature/babyDragonGuide` | 2026-08-11 | 이하늘 28 |
| #179 | `feature/babyDragonToolTip` | 2026-08-11 | 이하늘 5 |
| #195 | `feature/tutorialBugFix` | 2026-08-13 | 이하늘 8 |
| #205 | `feature/tutorialBridge` | 2026-08-13 | 이하늘 2 |
| #206 | `feature/tutorialBridge` | 2026-08-13 | 이하늘 1 |
| #210 | `feature/tutorialSound` | 2026-08-14 | 이하늘 1 |
| #215 | `feature/constructNodeFix` | 2026-08-14 | 이하늘 2 |
| #237 | `feature/new-tutorial` | 2026-08-19 | 이하늘 20, 조강현 1 |
| #239 | `feature/new-tutorial` | 2026-08-20 | 이하늘 6 |
| #245 | `feature/towerEffect` | 2026-08-24 | 이하늘 11 |
| #261 | `feature/towerEffect` | 2026-08-27 | 이하늘 15 |
| #269 | `feature/tutorial-bug-fix` | 2026-08-27 | 이하늘 1 |
| #273 | `feature/tower-sound` | 2026-08-27 | 이하늘 4 |
| #277 | `feature/enemy-effect` | 2026-08-28 | 이하늘 6 |

## 커밋 전체 (시간순)

### 프로토타입 (115개)

- `0eb2f77a` 2026-07-09 [Feature] 건물 배치 - 각 프리팹 local position 조정 및 디버깅 메시지 추가 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `2440ca99` 2026-07-09 [Feature] 건물 배치 - 각 프리팹 local position 조정 및 디버깅 메시지 추가 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `c5f55338` 2026-07-09 [Feature] 건물 배치 - 각 프리팹 local position 조정 및 디버깅 메시지 추가 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `c51df0ed` 2026-07-09 [Feature] 그리드 데이터 클래스와 그리드 맵 클래스 틀 — `Assets/Scripts`, `ProjectSettings`
- `11844581` 2026-07-09 [Feature] 그리드 데이터 클래스와 그리드 맵 클래스 틀 — `Assets/Scripts`, `ProjectSettings`
- `a6f30d99` 2026-07-09 [Feature] 그리드 시스템 - 터레인타일맵 에셋 및 터레인타일 스크립터블 오브젝트 추가 — `Assets/Scripts/Grid`, `Assets/Scenes`
- `c2a909c5` 2026-07-09 [Feature] 그리드 데이터 클래스와 그리드 맵 클래스 틀 — `Assets/Scripts`
- `7929cb53` 2026-07-09 [Feature] 그리드 시스템 - 터레인타일맵 에셋 및 터레인타일 스크립터블 오브젝트 추가 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `b6caeb9f` 2026-07-09 [Feature] 그리드 시스템 - 터레인타일맵 에셋 및 터레인타일 스크립터블 오브젝트 추가 — `Assets/Scripts`
- `8ea54924` 2026-07-09 [Feature] 그리드 시스템 - 파일 위치 분리 및 enemy 삭제 — `Assets/Scripts/Buildings`, `Assets/Scripts/Grid`
- `a754ac1d` 2026-07-09 [Feature] 그리드 시스템 - 터레인타일맵 에셋 및 터레인타일 스크립터블 오브젝트 추가 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `6f624f8c` 2026-07-09 [Feature] 그리드 데이터 클래스와 그리드 맵 클래스 틀 — `Assets/Scripts`
- `fec893f7` 2026-07-09 [Feature] 그리드 시스템 - 파일 위치 분리 및 enemy 삭제 — `Assets/Scripts/Buildings`, `Assets/Scripts/Grid`
- `785a26bb` 2026-07-09 [Feature] 그리드 데이터 클래스와 그리드 맵 클래스 틀 — `Assets/Scripts`
- `2b18a2f6` 2026-07-10 건물 재배치 완료 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `0d1b2389` 2026-07-10 건물 재배치 수정 완료 및 건물 타일 점유 이미지 추가 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `26d0d0de` 2026-07-10 건물 재배치 완료 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `f67cccf4` 2026-07-10 건물 재배치 수정 완료 및 건물 타일 점유 이미지 추가 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `f6f558b7` 2026-07-10 타일 마우스 선택 완료 — `Assets/Scripts`, `Assets/Scenes`
- `10dd646f` 2026-07-10 타일 - 마우스 셀렉트 추가 — `Assets/Scripts`, `Assets/Scenes`
- `0d78143f` 2026-07-10 건물 셀 사이즈에 따라 마우스 타일 선택 및 마우스 클릭 시 건물 배치 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `26071f8c` 2026-07-10 타일 마우스 선택 완료 — `Assets/Scripts`, `Assets/Scenes`
- `4d137b7e` 2026-07-10 타일 - 마우스 셀렉트 추가 — `Assets/Scripts`, `Assets/Scenes`
- `3a5cac19` 2026-07-10 타일 - 마우스 셀렉트 추가 — `Assets/Scripts`, `Assets/Scenes`
- `267429b3` 2026-07-10 건물 배치 시 고스트 이미지 추가 및 건물 배치 타일 수동 조정 툴 추가 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `0190ece9` 2026-07-10 건물 셀 사이즈에 따라 마우스 타일 선택 및 마우스 클릭 시 건물 배치 — `Assets/Scripts/Grid`, `Assets/Scripts`
- `f4e23e0c` 2026-07-10 타일 마우스 선택 완료 — `Assets/Scripts`, `Assets/Scenes`
- `1a55f03b` 2026-07-10 건물 배치 시 고스트 이미지 추가 및 건물 배치 타일 수동 조정 툴 추가 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `d830d0a2` 2026-07-13 청크 디버거 스크립트 추가 및 캐슬 중심으로 청크 좌표 계산 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `0ccc719d` 2026-07-13 청크 클래스 추가 &  GridMap에 청크 생성 및 디버깅 코드 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `928ef250` 2026-07-13 청크 디버깅 코드 삭제 — `Assets/Scripts/Grid`
- `15a0c0e1` 2026-07-13 청크 클래스 추가 &  GridMap에 청크 생성 및 디버깅 코드 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `9af95dc1` 2026-07-13 맵 프리팹과 건설 시스템 연결 테스트 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `8f0e6f3d` 2026-07-13 맵 프리팹과 건설 시스템 연결 테스트 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `3fc369f1` 2026-07-13 청크 클래스 추가 &  GridMap에 청크 생성 및 디버깅 코드 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `4a554896` 2026-07-13 청크 디버깅 코드 삭제 — `Assets/Scripts/Grid`
- `2396a2c6` 2026-07-14 점령 시스템 구현 - 디버깅 스크립트 추가 및 스크립터블 오브젝트 에셋 생성 — `Assets/Scripts/Conquest`, `Assets/Scripts/Grid`
- `76592a34` 2026-07-14 점령 시스템 구현 중 - 점령 데이터 클래스, 점령 매니저, 점령 기간 스크립터블 오브젝트 틀 생성 — `Assets/Scripts/Grid`, `Assets/Scripts/Conquest`
- `9d360d70` 2026-07-14 상태 이넘명 변경 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `736109ab` 2026-07-14 점령 시스템 구현 - 디버깅 스크립트 추가 및 스크립터블 오브젝트 에셋 생성 — `Assets/Scripts/Conquest`, `Assets/Scripts/Grid`
- `c8ce5376` 2026-07-14 상태 이넘명 변경 — `Assets/Scripts/Grid`, `Assets/Scripts/Buildings`
- `af760dce` 2026-07-14 점령 시스템 구현 중 - 점령 데이터 클래스, 점령 매니저, 점령 기간 스크립터블 오브젝트 틀 생성 — `Assets/Scripts/Grid`, `Assets/Scripts/Conquest`
- `038b253e` 2026-07-15 점령 UI 틀 구현 및 기능 연결 — `Assets/Scripts/Conquest`, `Assets/Scripts/UI`
- `b67a30cc` 2026-07-15 점령 UI 틀 구현 및 기능 연결 — `Assets/Scripts/Conquest`, `Assets/Scripts/UI`
- `aec15218` 2026-07-15 점령 UI 틀 구현 및 기능 연결 — `Assets/Scripts/Conquest`, `Assets/Scripts/UI`
- `258b0ec3` 2026-07-15 점령 UI 씬 변경 — `Assets/Scenes`
- `2b090574` 2026-07-15 점령 UI 레이아웃 수정 중 — `Assets/Prefabs/UI`, `Assets/Scripts/Conquest`
- `d4d6713a` 2026-07-15 점령 UI 씬 변경 — `Assets/Scenes`
- `fd246521` 2026-07-15 점령 UI 레이아웃 수정 중 — `Assets/Prefabs/UI`, `Assets/Scripts/Conquest`
- `cb76a16f` 2026-07-15 점령 UI 수정 중 — `Assets/Prefabs/UI`, `Assets/Scripts/Conquest`
- `940e85af` 2026-07-15 점령 UI 수정 중 — `Assets/Prefabs/UI`, `Assets/Scripts/Conquest`
- `8711a643` 2026-07-16 점령 중인 청크의 UI 추가 및 점령 시스템과 cycleManager 임시 연결 — `Assets/Scripts/Conquest`, `Assets/Scripts/Grid`
- `8461c695` 2026-07-16 점령 중인 청크의 UI 추가 및 점령 시스템과 cycleManager 임시 연결 — `Assets/Scripts/Grid`, `Assets/Sprites/UI`
- `3f426e67` 2026-07-16 점령모드 esc 눌러서 끄는 기능 추가 중 — `Assets/Scripts/Managers`
- `2d0c860c` 2026-07-16 점령모드 esc 눌러서 끄는 기능 추가 중 — `Assets/Scripts/Managers`, `Assets/Scripts/Conquest`
- `76cc93d2` 2026-07-16 점령 UI 틀 및 기능 연결 완성 — `Assets/Scripts/Conquest`, `Assets/Prefabs/UI`
- `a42bed5a` 2026-07-16 점령 UI 틀 및 기능 연결 완성 — `Assets/Scripts/Conquest`, `Assets/Prefabs/UI`
- `94ee4b13` 2026-07-20 자원 노드 데이터 생성 툴 및 생성, 자원 노드 디버거 구현 — `Assets/Scripts/Grid`, `Assets/Sprites/Buildings`
- `a2273344` 2026-07-20 자원 노드 데이터 생성 툴 및 생성, 자원 노드 디버거 구현 — `Assets/Scripts/Grid`, `Assets/Sprites/Buildings`
- `16dad4a5` 2026-07-20 자원 노드 데이터 생성 툴 및 생성, 자원 노드 디버거 구현 — `Assets/Data/ResourceProductionData`, `Assets/Data/ResourceNodeData`
- `75670fa6` 2026-07-20 자원 노드 데이터 생성 툴 및 생성, 자원 노드 디버거 구현 — `Assets/Scripts/Grid`, `Assets/Sprites/Buildings`
- `b7ce51df` 2026-07-20 프리팹 오버라이드 추가 — `Assets/Prefabs/Conquest`, `Assets/Scenes`
- `51152765` 2026-07-20 점령 UI 및 기본 시스템 완료 — `Assets/Scripts/Managers`, `Assets/Scripts/Debug`
- `072148e0` 2026-07-20 인풋 enable, disable 수정 및 불필요한 널 체크 삭제 — `Assets/Scripts/Conquest`, `Assets/Scripts/Grid`
- `ff7906f6` 2026-07-20 점령 cancel 오류 수정 및 디폴트 타입 청크 점령 불가 가드 추가 — `Assets/Scripts/Conquest`, `Assets/Prefabs/UI`
- `7cf65a86` 2026-07-20 점령 cancel 오류 수정 및 디폴트 타입 청크 점령 불가 가드 추가 — `Assets/Scripts/Conquest`, `Assets/Prefabs/UI`
- `c8c8ad4e` 2026-07-21 씬 저장 — `Assets/Scenes`
- `5895dc34` 2026-07-21 stash 병합 — `Assets/Scripts/ResourceNode`, `Assets/Data/ResourceNodeData`
- `1bc45f52` 2026-07-21 인구 시스템 도입 -> 점령 UI 인구 코스트 연결 — `Assets/Data/ResourceNodeData`, `Assets/Scripts/Conquest`
- `42bc823b` 2026-07-21 초원지역 청크 배율 추가 — `Assets/Data/ResourceNodeData`
- `34e6a066` 2026-07-21 자원 노드에 따른 생산 시설 건설 — `Assets/Data/ResourceNodeData`, `Assets/Data/ResourceProductionData`
- `9ab9657c` 2026-07-21 자원 노드에 따른 생산 시설 건설 — `Assets/Data/ResourceNodeData`, `Assets/Data/ResourceProductionData`
- `de2eff22` 2026-07-21 자원노드 시스템 문서 및 생산시설에 인구 배치 및 자원 생산, 점령 시 인구 할당과 점령 후 인구 회수 연결 — `Assets/Data/ResourceProductionData`, `Assets/Scripts/Buildings`
- `abbfeaac` 2026-07-21 자원 노드에 따른 생산 시설 건설 — `Assets/Data/ResourceNodeData`, `Assets/Data/ResourceProductionData`
- `0698e835` 2026-07-21 자원 노드에 따른 생산 시설 건설 — `Assets/Data/ResourceNodeData`
- `e64c5b9d` 2026-07-21 자원노드 시스템 문서 및 생산시설에 인구 배치 및 자원 생산, 점령 시 인구 할당과 점령 후 인구 회수 연결 — `Assets/Data/ResourceProductionData`, `Assets/Scripts/Buildings`
- `e4498f1e` 2026-07-22  건물 회전 및 건설 비용 추가, 슬라임 자원 노드 추가 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `dc7ac0dd` 2026-07-22 자원노드 셀 단위, 자원별 생산량 저장 방식으로 변경 — `Assets/Data/ResourceProductionData`, `Assets/Prefabs/Building`
- `4af6e0ae` 2026-07-23 타워에 건설 비용 추가 및 빌딩에 건설 비용 귀속되게 리팩토링, 건물 슬롯 UI 수정 — `Assets/Scripts/Buildings`, `Assets/Sprites/Buildings`
- `0e6d242f` 2026-07-23 건물 에셋 생성 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `8002c62e` 2026-07-23 inputaction onenable 추가 — `Assets/Scenes`, `Assets/Prefabs/Building`
- `a07d88b3` 2026-07-23 벌목장 회전 스프라이트 추가 — `Assets/Prefabs/Building`
- `fdff22a2` 2026-07-23 enable 삭제 — `Assets/Scripts/Grid`
- `a085666b` 2026-07-23 건물 철거 시 비용 회수 분기 추가 — `Assets/Scripts/Managers`, `Assets/Sprites/Buildings`
- `a4232871` 2026-07-24 건물 이미지 생성 중 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `0b517d39` 2026-07-24 건물 이미지 생성 중 — `Assets/Sprites/Buildings`, `ProjectSettings`
- `e0fa980e` 2026-07-24 채석장 이미지 추가 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `14c8e01c` 2026-07-27 점령 및 빌드 모드 창 편의성 추가 - esc 누르면 패널 닫힘 및 모드 종료 — `Assets/Scripts/UI`, `Assets/Scripts/Conquest`
- `eeed375f` 2026-07-27 건물 회전 이미지 생성 중 — `Assets/Sprites/Buildings`, `Assets/Art/Generated`
- `aa5769a8` 2026-07-27 새끼용 시스템 - 설치 및 이동 / 생산 버프 / 공격 버프 — `Assets/Sprites/Buildings`, `Assets/Scripts/Buildings`
- `1d931ed6` 2026-07-27 새끼용 GUI 및 나머지 용도 더미 데이터 추가 — `Assets/Data/BabyDragon`, `Assets/Scenes`
- `bc3b3016` 2026-07-28 새끼용 이벤트 순서 버그 — `Assets/Scripts/Buildings`
- `bdb8a713` 2026-07-28 rebase — `Assets/Data/Research`, `Assets/Scripts/Research`
- `224cde22` 2026-07-28 용 스킬 트리 진행 중 — `Assets/Data/Dragon`, `Assets/Scripts/Dragon`
- `a057223e` 2026-07-28 새끼용 인벤토리 구현 완료 — `Assets/Data/ResourceData`, `Assets/Prefabs/Building`
- `5d4955a4` 2026-07-28 특화자원 생산 시설 스프라이트 및 프리팹 생성 — `Assets/Prefabs/Building`, `Assets/Sprites/Buildings`
- `c03df69c` 2026-07-28 건물 이미지 추가 및 새끼용 디버깅로그 추가 및 새끼용 인벤토리 UI 틀 — `Assets/Sprites/Buildings`, `Assets/Art/Generated`
- `74bfeada` 2026-07-28 새끼용 알 상태 슬라임 먹이 제거 — `Assets/Sprites/Buildings`, `Assets/Data/BabyDragon`
- `c3844a5b` 2026-07-28 리베이스 후 씬 설정 — `Assets/Scenes`
- `6c805025` 2026-07-29 벌목장 프리팹 수정 및 slot_babyDragon 프리팹 UI 살짝 수정 — `Assets/Prefabs/Building`, `Assets/Sprites/Buildings`
- `dcf987cd` 2026-07-29 특수자원 생산 시설 missing spritie 할당 — `Assets/Prefabs/Building`
- `fd776d7d` 2026-07-29 벌목장 회전 에셋 생성 및 MouseSelectController 버그 수정 — `Assets/Sprites/Buildings`, `Assets/Prefabs/Building`
- `3bafb037` 2026-07-29 점령 코스트 인구 추가 — `Assets/Scripts/UI`
- `e46df6af` 2026-07-29 봉인석 에셋 생성 중 — `Assets/Sprites/Buildings`, `Assets/Scenes`
- `9c79a333` 2026-07-29 봉인석 이미지 생성 중 & 봉인석 비용 통일 — `Assets/Sprites/Portals`, `Assets/Scenes`
- `c2bdaf95` 2026-07-29 봉인석 작업 중 — `Assets/Scripts/Portal`, `Assets/Tests/Editor`
- `cb9f5869` 2026-07-29 점령 - 주둔지 제거 — `Assets/Scripts/Managers`, `Assets/Sprites/Buildings`
- `637aacbe` 2026-07-29 flameheart 프리팹 수정 및 timesand 위치 조정 — `Assets/Prefabs/Building`, `Assets/Scripts/Managers`
- `cb8371bf` 2026-07-29 그리드맵 오류 수정 - 점령 디버그 테스트 코드 수정에 따른 메서드 추가 — `Assets/Scripts/Grid`
- `2b8749f5` 2026-07-30 봉인석 엔딩 기초 구현 완료 — `Assets/Sprites/Portals`, `Assets/Sprites/Buildings`
- `7bb08811` 2026-07-30 봉인석 설치 가능 위치 변경 — `Assets/Scenes`, `Assets/Data/Portal`
- `5b975ee8` 2026-07-30 안쓰는 이미지 정리 — `Assets/Sprites/Buildings`
- `54deacd8` 2026-07-30 건물 밤에 배치 및 제거 버그 수정 / 새끼용 이동 및 제거 UIUX 통합 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `9e07427d` 2026-07-30 씬 수정 — `Assets/Sprites/Buildings`, `Assets/Scenes`
- `ffc36cd2` 2026-07-31 봉인석 건설 가능위치 확장 및 디폴트인 지역 점령 패널 뜨는 버그 수정 — `Assets/Scenes`, `Assets/Scripts/Managers`

### 알파 (57개)

- `f4436159` 2026-08-03 그리드 오버라이드 함 및 워커모드에서 사용하는 highlight cell groups 메서드 부활시킴 — `Assets/Scenes`, `Assets/Scripts`
- `fb07dbde` 2026-08-03 짜투리 점령지 — `Assets/Scenes`, `Assets/Scripts/Conquest`
- `4cb924c5` 2026-08-03 짜투리 점령지도 하이라이트되게 추가 — `Assets/Scripts/Managers`, `Assets/Scripts/Conquest`
- `c698a4db` 2026-08-03 점령지 및 점령 불가 하이라이트 방식 수정 완료 — `Assets/Scripts/Grid`, `Assets/Scripts/Conquest`
- `1732ce6d` 2026-08-03 맵 외곽선에 맞게 경계선 그려지게 수정 및 castle 밑에 타일 교체 — `Assets/Scripts/Grid`, `Assets/Scripts/Managers`
- `2514c39d` 2026-08-03 씬 리베이스 및 암석용 - 시간용 색 스왑 — `Assets/Scenes`
- `1aa50239` 2026-08-03 새끼용 가이드 시스템 기초 구현 — `Assets/Scripts/Buildings`
- `a2b41e13` 2026-08-03 씬 리베이스 및 암석용 - 시간용 색 스왑 — `Assets/Scenes`, `Assets/Scripts/Dragon`
- `623ef282` 2026-08-03 새끼용 가이드 시스템 기초 구현 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `3867e056` 2026-08-03 새끼용 가이드 시스템 기초 구현 — `Assets/StreamingAssets/Localization`
- `79c125ef` 2026-08-04 Badge_Dot 프리팹화 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `2d10c97a` 2026-08-04 새끼용 가이드 시스템 완료 — `Assets/Scripts/UI`
- `437c9321` 2026-08-04 새끼용 가이드 시스템 완료 — `Assets/Scripts/UI`, `Assets/Scripts/Buildings`
- `ea01b448` 2026-08-04 봉인석 씬 제거 및 샘플씬에 초기 지급 알 설정 — `Assets/Scenes`
- `a47dbce8` 2026-08-04 Badge_Dot 프리팹화 — `Assets/Scenes`
- `b7f8c95f` 2026-08-04 씬 설정 — `Assets/Scenes`
- `fb82b255` 2026-08-04 씬 설정 — `Assets/Scenes`
- `6e652628` 2026-08-04 새끼용 가이드 시스템 완료 — `Assets/Scenes`
- `dc17d5a6` 2026-08-05 튜토리얼 초기 구현 — `Assets/Scenes`, `Assets/Scripts/Grid`
- `8377e1ed` 2026-08-05 튜토리얼 초기 구현 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `c34082dd` 2026-08-05 튜토리얼 초기 구현 — `Assets/Scripts/UI`
- `a4c0cbee` 2026-08-06 튜토리얼 새 씬에서 작업 중 — `Assets/Scripts/UI`, `Assets/Data/Tutorial`
- `9cd57300` 2026-08-06 튜토리얼 새 씬에서 작업 중 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `6984b257` 2026-08-07 씬 저장 — `Assets/Scenes`
- `91063816` 2026-08-07 리베이스 후 튜토리얼 수정 중 — `Assets/Scripts/UI`, `Assets/Data/Tutorial`
- `22b27bd3` 2026-08-07 튜토리얼 완료 - 미세 수정 필요 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `68c67610` 2026-08-07 리베이스 후 튜토리얼 수정 중 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `751d868b` 2026-08-09 guid 중복 오류 수정 및 튜토리얼 시스템 구조 재설계 문서 작성 — `Assets/Scenes`, `Assets/Scripts/UI`
- `6899af6c` 2026-08-10 튜토리얼 구성 수정 중 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `e3af6846` 2026-08-10 슬라임 농장 및 메인 성 튜토리얼 누락됨 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `22d96a64` 2026-08-10 튜토리얼 수정 중 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `d0bf6319` 2026-08-11 튜토리얼 유아이 중복 수정 중 — `Assets/Scenes`, `Assets/Prefabs`
- `9d62614d` 2026-08-11 새끼용 툴팁 및 새끼용 토스트 UI 배선 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `0f7182fa` 2026-08-11 코드 리뷰 바탕으로 수정 — `Assets/Scripts/Buildings`, `Assets/Scripts/UI`
- `3bd0f891` 2026-08-11 코드 리뷰 바탕으로 코드 수정 및 엔딩 데이터 추가 — `Assets/StreamingAssets/Localization`, `Assets/Scripts/Tutorial`
- `f4ddb1ac` 2026-08-11 튜토리얼 안내 흐름 수정 및 미사용 에셋·문구 정리 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `44c9ef5e` 2026-08-11 씬 와이어링 — `Assets/Scenes`
- `8a940dc1` 2026-08-11 새끼용 토스트 UI raycast 끔 및 공격모드 일때 새끼용이 굶주리면 굶주림으로 비활성화 문구 나오게 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `f223e38e` 2026-08-11 새끼용 툴팁과 알 획득 및 부화 알림 추가 — `Assets/Scripts/Buildings`, `Assets/Scripts/UI`
- `d6bd8c94` 2026-08-11 씬 정리 — `Assets/Scenes`
- `c08d2755` 2026-08-12 오브젝티브 패널 위치 수정 — `Assets/Scenes`
- `ca6d82e1` 2026-08-12 claimList_Window 누락 되돌림 — `Assets/Prefabs/UI`, `Assets/Scripts/Managers`
- `ca59f810` 2026-08-12 tab 단축키 추가 및 단축키 배타모드 튜토리얼 씬에 배선하면서 튜토리얼 팁 안내 도중 단축키 동작하지 못하게 방어 보강 — `Assets/Scripts/UI`, `Assets/Scripts/Tutorial`
- `06ff129e` 2026-08-12 튜토리얼 팁 중 다른 버튼 클릭 방어 추가 및 새끼용 모드 전환 안내 추가 — `Assets/Scripts/UI`, `Assets/Scripts/Tutorial`
- `b99c8371` 2026-08-12 새끼용 토스트 알림, 점령 리스트와 묶어서 스택 구조로 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `f5a41374` 2026-08-13 Tutorial Quit 패널 디자인 변경 — `Assets/Prefabs/UI`
- `393d01ff` 2026-08-13 tab 배선 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `da88aef1` 2026-08-13 화산 지역에 돌 자원 노드 깔아둔 거(디버깅용) 삭제 — `Assets/Data/ResourceNodeData`
- `3a74e2c4` 2026-08-13 샘플씬 - babydragonEggToast 패널 위치 수정 및 튜토리얼에 inputaction 배선 — `Assets/Scenes`
- `a0bc2b22` 2026-08-13 죽은 필드 정리에 따라 에셋 수정 및 coroutine -> Unitask로 변환 및 start에서 토스트 지연 추가 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `21655725` 2026-08-13 튜토리얼 씬으로의 전환 및 튜토리얼에서 샘플씬 전환 로딩 오버레이 추가 — `Assets/Sprites/Cinematics`, `Assets/Data/Tutorial`
- `346ba23d` 2026-08-13 튜토리얼 -> 샘플씬 로딩 시계 돌아가는 걸로 변경 — `Assets/Scripts/Flow`, `Assets/Scenes`
- `12500e37` 2026-08-13 튜토리얼 진입점 구현 — `Assets/Scripts/UI`, `Assets/StreamingAssets/Localization`
- `7272634a` 2026-08-14 건설 불가 사유 이넘 분기 -> warning_window 추가 — `Assets/Scripts/Grid`, `Assets/StreamingAssets/Localization`
- `1022e435` 2026-08-14 warning_window에 봉인석 문구 배선 연결 — `Assets/Scenes`
- `377d2364` 2026-08-14 추가된 warning_window 프리팹 문구 샘플씬에 배선 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `ba6c5b8d` 2026-08-14 튜토리얼 버튼 및 로딩 사운드 추가 — `Assets/Scripts/UI`, `Assets/Scripts/Sound`

### 베타 (68개)

- `d97c42a1` 2026-08-15 SlimeFactory 부모와 중복해서 gridmap 필드 선언 수정 — `Assets/Scripts/Buildings`
- `e133df1f` 2026-08-15 SlimeFactory 부모와 중복해서 gridmap 필드 선언 수정 — `Assets/Scripts/Buildings`
- `a6c78f83` 2026-08-15 SlimeFactory 부모와 중복해서 gridmap 필드 선언 수정 — `Assets/Scripts/Buildings`
- `ca786c09` 2026-08-18 튜토리얼 재설계 진행 중 - m5-A — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `9f40be47` 2026-08-18 씬 저장 — `Assets/Scenes`
- `242c081d` 2026-08-18 튜토리얼 재구성 구현 중 - m4 완료 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `023930ef` 2026-08-18 튜토리얼 에셋 문구 위치 다듬기 및 엔딩 이미지 스프라이트 교체 — `Assets/Data/Tutorial`, `Assets/Sprites/Cinematics`
- `c7f376c3` 2026-08-18 튜토리얼 재설계 진행 중 - m5-A — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `5f00b498` 2026-08-18 튜토리얼 재구성 진행 중 - 마일스톤 단계 M3 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `55dc100c` 2026-08-18 튜토리얼 에셋 문구 위치 다듬기 및 엔딩 이미지 스프라이트 교체 — `Assets/Data/Tutorial`, `Assets/Sprites/Cinematics`
- `973e7aa8` 2026-08-18 씬 저장 — `Assets/Scenes`
- `4c249c10` 2026-08-18 튜토리얼 재구성 진행 중 - 마일스톤 단계 M3 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `85584dda` 2026-08-18 튜토리얼 재구성 진행 중 - 마일스톤 단계 M3 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `aedcf2ea` 2026-08-18 씬 저장 — `Assets/Scenes`
- `75368b13` 2026-08-18 튜토리얼 재구성 구현 중 - m4 완료 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `d732b23b` 2026-08-19 코드 리뷰 1번 수정 -> victory_window 인스펙터에서 배선 제외 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `31a90e54` 2026-08-19 안쓰는 에셋 정리 및 자원 제공 및 1일차 타워 개수 제한 추가 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `12297a40` 2026-08-19 튜토리얼 재구성 진행 중 - m7 구현 중 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `8d8b4d7d` 2026-08-19 튜토리얼 재구성 진행 중 - m7 구현 중 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `907e8a91` 2026-08-19 튜토리얼 재구성 1차 완료 - 추후 조금 더 다듬을 예정 — `Assets/Scripts/Tutorial`, `Assets/Data/Tutorial`
- `9d2217e2` 2026-08-19 튜토리얼 재구성 1차 완료 - 추후 조금 더 다듬을 예정 — `Assets/Scripts/Tutorial`, `Assets/Data/Tutorial`
- `93265f74` 2026-08-20 타워 발사체 이펙트 추가 중 - 현재 화살 타워 진행 중/ 불 속성 타워 완료 — `Assets/Scripts/Combat`, `Assets/Data/TowerData`
- `455fada4` 2026-08-20 타워 발사체 이펙트 추가 중 - 현재 화살 타워 진행 중/ 불 속성 타워 완료
- `638abf88` 2026-08-20 용 속성 변경 및 스킬해금 버그 수정 및 마지막 보스 예고 타이밍 변경 — `Assets/Scripts/UI`, `Assets/Scripts/Tutorial`
- `4ffaac55` 2026-08-20 csv - 키 배선 추가 및 스톨 제거 — `Assets/Data/Tutorial`, `Assets/Scripts/Tutorial`
- `437b5b50` 2026-08-20 타워 이펙트 계획안 작성 — `Docs`
- `af3b98ea` 2026-08-20 창 닫기 관문 통일 — `Assets/Data/Tutorial`, `Assets/Scripts/UI`
- `f2670e32` 2026-08-20 필드 위치 변경 — `Assets/Scripts/Tutorial`
- `59c14cee` 2026-08-21 유료 외부 에셋 프리팹을 저장소에서 분리 — `CoplayScripts`, `Docs`
- `e5198cf5` 2026-08-21 coplay 이펙트 생성기 스크립트 경로 수정 및 상태 이상 시스템 구현 계획 문서 작성 — `CoplayScripts`
- `bece1da1` 2026-08-21 안내 끝나고 밤 버튼 가능 안내 문구와 퀘스트 완료 타이밍이 살짝 어긋남 수정 — `Assets/Scripts/Managers`, `Assets/Scripts/Tutorial`
- `5e03b2b5` 2026-08-21 타워 이펙트 가공 스크립트 — `CoplayScripts`
- `42c0ed2d` 2026-08-21 빌드 문서 — `Docs`
- `e08eae50` 2026-08-21 유료 외부 에셋 프리팹을 저장소에서 분리 — `CoplayScripts`, `(저장소 루트)`
- `fe49e75e` 2026-08-21 coplay 이펙트 생성기 스크립트 경로 수정 및 상태 이상 시스템 구현 계획 문서 작성 — `CoplayScripts`, `(저장소 루트)`
- `66b01202` 2026-08-21 타워 이펙트 구현 중 — `Assets/Data/TowerData`
- `14c1362a` 2026-08-21 타워 이펙트 가공 스크립트 — `CoplayScripts`
- `3251c8a4` 2026-08-21 빌드 문서 — `Docs`
- `e1537bba` 2026-08-21 튜토리얼에 인구 확인창 뜨지 않게 설정 — `Assets/Scripts/Managers`, `Assets/Scripts/UI`
- `cddf1755` 2026-08-24 타워 이펙트 구현 중 - 암석 / 불 / 얼음 타워 파티클 완료 및 석궁 타워 파티클 교체 및 몬스터 상태 이상 파티클 시스템 구현 중 — `CoplayScripts`
- `6eed085e` 2026-08-24 생성기 스크립트 정리 — `CoplayScripts`
- `203b7073` 2026-08-24 근거리 투사체 시스템 추가 - 근거리에서 발사되는 궤적이 잘 보이도록 — `Docs`, `Assets/Scripts/Combat`
- `382ba69e` 2026-08-24 타워 이펙트 구현 중 - 암석 / 불 / 얼음 타워 파티클 완료 및 석궁 타워 파티클 교체 및 몬스터 상태 이상 파티클 시스템 구현 중 — `CoplayScripts`, `Assets/Scripts/Combat`
- `e3c1bf63` 2026-08-24 projecitle 가드 상수 수정 및 풀 가로채기 수정 — `Assets/Scripts/Combat`, `Assets/Scripts/Managers`
- `0d55f76a` 2026-08-24 생성기 스크립트 정리 — `CoplayScripts`, `Assets/Data/BabyDragon`
- `acdd8c91` 2026-08-24 빙결 상태 이상 이펙트 위치 - 몬스터 발밑으로 변경 — `Assets/Scripts/Combat`
- `f314c212` 2026-08-25 새끼용 이펙트 및 버프 타워 이펙트 구현 중 — `CoplayScripts`, `Docs`
- `f31ebc02` 2026-08-25 tracer 시스템 삭제 — `Assets/Scripts/Combat`, `Docs`
- `f95f8a65` 2026-08-25 근거리 투사체 관련 수정 — `Assets/Scripts/Combat`, `Docs`
- `9e2c5fc0` 2026-08-26 새끼용 이펙트 수정 중 — `Assets/Data/BabyDragon`, `Assets/Scripts/Buildings`
- `7f2d5ebd` 2026-08-26 버프 타워 및 새끼용 이펙트 1차 구현 완료 — `CoplayScripts`, `Assets/Data/TowerData`
- `26099e55` 2026-08-27 사운드 에셋 배선 에디터 툴 및 타워  사운드 배선 완료(새끼용 제외) — `Assets/Data/TowerData`, `Assets/Data/BabyDragon`
- `771057c2` 2026-08-27 m1 & m2 - 타워 발사, 명중 효과음 및 버프 타워 인구 할당(활성) 효과음 연동 — `Assets/Data/TowerData`, `Assets/Scripts/Sound`
- `d0cea5e2` 2026-08-27 m0 - 타워 전투 효과음 데이터 구조 추가 — `Docs`, `Assets/Scripts/Sound`
- `ed87d3d3` 2026-08-27 튜토리얼 씬에 호버 효과 배선 재적용 — `Assets/Scenes`
- `5f6ab1d2` 2026-08-27 연구 해금 단계 안내문 위치 변경 — `Assets/Data/Tutorial`
- `22d092c8` 2026-08-27 적 이펙트 연출 설계 문서 추가 — `Docs`
- `26bbfd92` 2026-08-27 새끼용 효과음 추가 및 전투 효과음(타워종류) 마스터 볼륨 조절 추가 — `Assets/Data/BabyDragon`, `Assets/Scripts/Sound`
- `64a016b9` 2026-08-27 배속 연결 재적용 오버라이드 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `a77d53d8` 2026-08-27 TowerBuffOrgCoordinator 샘플씬에 배선 — `Assets/Scenes`
- `988ea936` 2026-08-27 TowerAuraSystem 깨진 거 복구 및 babydraonTower 프리팹 배선 복구 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `9539334d` 2026-08-27 죽은 메서드 삭제 및 인디케이터 표시 분기 수정 — `Docs`, `Assets/Scripts/Grid`
- `9994c014` 2026-08-27 튜토리얼 슬롯 버그 수정 및 씬 샘플씬에 맞게 리베이스 — `Assets/Scripts/UI`, `Assets/Scenes`
- `f0a8f9a5` 2026-08-28 발표 자료 - 기여도 스킬 사용 문서 추가 (이하늘) — `Docs/Contributions`
- `865f9c6f` 2026-08-28 베타 빌드 문서 — `Docs`
- `6d7607ca` 2026-08-28 어미용 전역 스킬 이펙트 설계 문서 작성 — `Docs`
- `7b924c81` 2026-08-28 이펙트 디폴트 시간 수정 및 몬스터 발밑 위치 캐싱 — `Assets/Scripts/Combat`, `Assets/Scripts/Monster`
- `126a1884` 2026-08-28 튜토리얼 연구 챕터 하이라이트 수정 및 몬스터 이펙트 구현 — `Assets/Data/MonsterData`, `Assets/Scripts/Combat`

---

# 김지해

커밋 169개 · 2026-07-09 ~ 2026-08-28 · 이메일 `bmj1644035@gmail.com`

## 가장 많이 만진 디렉터리

| 디렉터리 | 이 팀원 터치 | 이 팀원 작업 중 | 그 디렉터리 전체 중 |
|---|---:|---:|---:|
| `Assets/Prefabs/UI` | 295 | 24.9% | **54.1%** |
| `Assets/Scripts/UI` | 165 | 13.9% | **23.8%** |
| `Assets/Scenes` | 137 | 11.6% | **22.3%** |
| `Assets/Data/Research` | 76 | 6.4% | **27.4%** |
| `Assets/Prefabs/Villager` | 54 | 4.6% | **100.0%** |
| `Assets/Sprites/UI` | 46 | 3.9% | **92.0%** |
| `Assets/TextMesh Pro/Shaders` | 46 | 3.9% | **100.0%** |
| `Assets/StreamingAssets/Localization` | 43 | 3.6% | **18.9%** |
| `Assets/Data/MonsterData` | 34 | 2.9% | **9.9%** |
| `Assets/Prefabs/Building` | 33 | 2.8% | **18.1%** |
| `Assets/TextMesh Pro/Resources` | 22 | 1.9% | **95.7%** |
| `Assets/Data/ResourceData` | 21 | 1.8% | **77.8%** |
| `Assets/Scripts/Buildings` | 21 | 1.8% | **4.5%** |
| `Assets/Scripts/Population` | 21 | 1.8% | **21.4%** |
| `Assets/Scripts/Grid` | 20 | 1.7% | **7.1%** |

마지막 열이 높을수록 그 영역을 혼자 맡았다는 뜻이다. 낮으면 공동 작업 구역이다.

## 이 팀원만 만진 파일 (단독 소유)

전체 171개 중 상위 25개.

| 파일 | 수정 횟수 |
|---|---:|
| `Assets/Prefabs/UI/BuildMode_window.prefab` | 22 |
| `Assets/Prefabs/UI/Ingame_window.prefab` | 20 |
| `Assets/Scripts/Population/Villager/Villager.cs` | 6 |
| `Assets/Scripts/UI/HealthChunkEffect.cs` | 6 |
| `Assets/Scripts/Debug/CastleDamageTester.cs` | 5 |
| `Assets/Sprites/Tower/tower_01.png` | 5 |
| `Assets/Scenes/CastleTest.unity` | 5 |
| `Assets/Scripts/Population/Villager/VillagerMovement.cs` | 4 |
| `Assets/Prefabs/Villager/Villager_04.prefab` | 4 |
| `Assets/Prefabs/Villager/Villager_01.prefab` | 4 |
| `Assets/Prefabs/UI/Slot_ClaimList.prefab` | 4 |
| `Assets/Prefabs/Villager/Villager_03.prefab` | 4 |
| `Assets/Sprites/Building/castle.png` | 4 |
| `Assets/Prefabs/Villager/Villager_02.prefab` | 4 |
| `Assets/Prefabs/UI/Popup_Factory_Info.prefab` | 4 |
| `Assets/Prefabs/UI/Slot_BabyDragon_List.prefab` | 3 |
| `Assets/Resources/DOTweenSettings.asset` | 3 |
| `Assets/Prefabs/Villager/Villager_05.prefab` | 3 |
| `Assets/Prefabs/Villager/Soldier_05.prefab` | 3 |
| `Assets/Prefabs/Villager/Soldier_03.prefab` | 3 |
| `Assets/Prefabs/UI/Button_01.prefab` | 3 |
| `Assets/Prefabs/UI/TabMenu.prefab` | 3 |
| `Assets/Prefabs/Villager/Soldier_02.prefab` | 3 |
| `Assets/Prefabs/Villager/Soldier_04.prefab` | 3 |
| `Assets/Prefabs/Villager/Soldier_01.prefab` | 3 |

## 병합된 PR·브랜치 (이 팀원 커밋이 가장 많은 것)

25건. 머지를 누른 사람이 아니라 **안에 담긴 커밋의 작성자**로 귀속했다.
로컬 동기화 머지는 제외하고 GitHub PR 머지만 센다.

| PR | 브랜치 | 머지일 | 담긴 커밋 |
|---|---|---|---|
| #24 | `feature/3-UISample` | 2026-07-10 | 김지해 8 |
| #37 | `feature/5-Castle` | 2026-07-13 | 김지해 9 |
| #39 | `feature/36-BuildModeUI` | 2026-07-13 | 김지해 6 |
| #50 | `feature/21-BuildModeWindow-Setting` | 2026-07-15 | 김지해 32 |
| #54 | `feature/26-UIConnect` | 2026-07-15 | 김지해 1 |
| #78 | `feature/57-ResourceUI` | 2026-07-20 | 김지해 8 |
| #89 | `feature/56-ConquestUI` | 2026-07-22 | 김지해 4 |
| #92 | `feature/69-ClaimListUI` | 2026-07-23 | 김지해 4 |
| #96 | `feature/93-BossWaveUI` | 2026-07-23 | 김지해 2 |
| #124 | `feature/97-ResourceAddUI` | 2026-07-29 | 김지해 6 |
| #127 | `feature/68-Stringtable` | 2026-07-30 | 김지해 2 |
| #150 | `feature/129-ClaimStringtable` | 2026-08-03 | 김지해 4 |
| #170 | `feature/128-DragonUI` | 2026-08-06 | 김지해 8 |
| #178 | `feature/138-MotherDragonTypeUI` | 2026-08-11 | 김지해 5 |
| #187 | `feature/142-BuildingInfoPopup` | 2026-08-12 | 김지해 3 |
| #200 | `feature/191-TitleSceneUI` | 2026-08-13 | 김지해 3 |
| #208 | `feature/189-SettingUI` | 2026-08-13 | 김지해 4 |
| #212 | `feature/211-FixUI` | 2026-08-14 | 김지해 2 |
| #235 | `feature/175-PeopleDirection` | 2026-08-18 | 김지해 3 |
| #238 | `feature/236-VillagerObjectPooling` | 2026-08-19 | 김지해 5 |
| #243 | `feature/230-SkillTreeUI` | 2026-08-21 | 김지해 6 |
| #251 | `feature/241-OutlineShader` | 2026-08-25 | 김지해 3 |
| #267 | `feature/229-ResearchUI` | 2026-08-27 | 김지해 5 |
| #274 | `feature/242-EnemyCardUI` | 2026-08-27 | 김지해 3 |
| #279 | `feature/232-HelpCodexUI` | 2026-08-28 | 김지해 5 |

## 커밋 전체 (시간순)

### 프로토타입 (99개)

- `d441ed14` 2026-07-09 [Feature] 인게임 ui 추가 — `Assets/Scenes`, `Assets/Prefabs`
- `64b11162` 2026-07-09 TMP 패키지 다운 — `Assets/TextMesh Pro/Shaders`, `Assets/TextMesh Pro/Resources`
- `1edfca48` 2026-07-09 [Feature] 인게임 ui 추가 — `Assets/Scenes`, `Assets/Prefabs`
- `583b9aeb` 2026-07-09 인게임UI 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `af543fba` 2026-07-09 인게임UI 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `82fdf72f` 2026-07-09 TMP 패키지 다운 — `Assets/TextMesh Pro/Shaders`, `Assets/TextMesh Pro/Resources`
- `c0ec486b` 2026-07-10 성 체력 구현 — `Assets/Scenes`, `ProjectSettings`
- `cd3d1dc3` 2026-07-10 체력바 연출 추가 — `Assets/Scripts/UI`, `Assets/Scripts/Debug`
- `6c1690ad` 2026-07-10 성 체력 구현 — `Assets/Scenes`, `ProjectSettings`
- `9b71a48e` 2026-07-10 체력바 연출 추가 — `Assets/Scripts/UI`, `Assets/Scripts/Debug`
- `7860da7f` 2026-07-10 인게임 ui 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `29e4d42d` 2026-07-10 인게임 ui 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `95bc8762` 2026-07-13 BuildModeWindow 프리팹 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `cb6a4648` 2026-07-13 BuildModeWindow 프리팹 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `9eb33cec` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `4f4a39d5` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `ed7d3c57` 2026-07-13 BuildModeWindow UI 기능 연결 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `f3bd66a5` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `3898a3db` 2026-07-13 BuildModeWindow UI 기능 연결 — `Assets/Scenes`, `Assets/Scripts/UI`
- `3c60fad8` 2026-07-13 BuildModeWindow 프리팹 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `7789fc18` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `16fa3dd9` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `392abc88` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `88172133` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `e8b2188a` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `5c6cc262` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `6396bb1b` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `6abe3d97` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `dcf96085` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `a45cbb94` 2026-07-13 BuildModeWindow UI 기능 연결 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `98b15821` 2026-07-13 BuildModeWindow 프리팹 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `0711245c` 2026-07-13 UI_CastleHealth 스크립트 수정 — `Assets/Scripts/UI`
- `1c8a9941` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `3dcc7741` 2026-07-13 UIScene 씬 수정 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `6e58900d` 2026-07-13 UI_CastleHealth 스크립트 수정 — `Assets/Scripts/UI`
- `412c75a1` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `0ff75b6c` 2026-07-13 UIScene 씬 수정 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `d5444208` 2026-07-13 체력바 연출 수정 — `Assets/Scripts/UI`, `Assets/Scenes`
- `8b1d6641` 2026-07-13 체력바 연출 수정 — `Assets/Scripts/UI`, `Assets/Scenes`
- `d11a5087` 2026-07-13 체력바 연출 수정 — `Assets/Scripts/UI`, `Assets/Scenes`
- `0b8b5853` 2026-07-13 성 그리드 값 부여 — `Assets/Scripts/Grid`, `Assets/Scenes`
- `2f99ecbd` 2026-07-13 체력바 연출 수정 — `Assets/Scripts/UI`, `Assets/Scenes`
- `a0f9cf8b` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `ad8ea5d1` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Scenes`
- `81ddcfb2` 2026-07-13 UI_CastleHealth 스크립트 수정 — `Assets/Scripts/UI`
- `3c332854` 2026-07-13 BuildMode Window UI 더미 작업 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `f2b1d60d` 2026-07-13 UIScene 씬 수정 — `Assets/Scenes`
- `155113f4` 2026-07-13 BuildModeWindow UI 기능 연결 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `49e9fd2f` 2026-07-13 BuildModeWindow UI 기능 연결 — `Assets/Prefabs/Building`, `Assets/Scenes`
- `482ab64f` 2026-07-14 BuildWindow Slot 작업 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `b72a20ec` 2026-07-14 slot UI 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/Buildings`
- `54a7e8cc` 2026-07-14 slot UI 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/Buildings`
- `030b2a82` 2026-07-14 BuildWindow Slot 작업 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `540b33aa` 2026-07-14 BuildWindow Slot 작업 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `635598a8` 2026-07-14 BuildWindow Slot 작업 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `cbe12aa3` 2026-07-15 밤낮 사이클 UI 연결 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `948519bf` 2026-07-15 스크롤뷰 ui수정, day 아이콘 수정 — `Assets/Prefabs/UI`, `Assets/Sprites/UI`
- `8623aee2` 2026-07-15 씬 연결 수정 — `Assets/Scenes`
- `701251fd` 2026-07-15 게임오버 처리 — `Assets/Sprites/Building`, `Assets/Prefabs/Building`
- `c27c3543` 2026-07-15 오류수정 — `Assets/Prefabs/Building`, `Assets/Scripts/Managers`
- `f19e985f` 2026-07-15 게임오버 처리 — `Assets/Sprites/Building`, `Assets/Scripts/Managers`
- `053e9baa` 2026-07-16 자원 아이콘 추가, UI 수정 — `Assets/Sprites/UI`, `Assets/Prefabs/UI`
- `1067fc2b` 2026-07-16 자원 아이콘 추가, UI 수정 — `Assets/Sprites/UI`, `Assets/Prefabs/UI`
- `50cf2025` 2026-07-16 성 스프라이트 수정, 줌인아웃 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `1d414b03` 2026-07-16 밤이 시작되면 빌드모드 패널이 자동으로 닫히게 구현 — `Assets/Scenes`, `Assets/Scripts/UI`
- `eab51e57` 2026-07-20 점령 관련 UI 작업중 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `e5b45e70` 2026-07-20 점령 버튼 추가 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `c9dd5f3d` 2026-07-20 UI_CastleHealth: Castle 미연결 씬에서 NRE 방지 — `Assets/Scripts/UI`
- `b9a9f6df` 2026-07-20 null 가드추가 — `Assets/Scenes`
- `e52c03f8` 2026-07-20 자원 시스템 추가 — `Assets/Data/ResourceData`, `Assets/Scripts/Managers`
- `5f8be91e` 2026-07-20 UI_CastleHealth: Castle 미연결 씬에서 NRE 방지 — `Assets/Scripts/UI`
- `deddd900` 2026-07-20 자원 시스템 추가 — `Assets/Data/ResourceData`, `Assets/Scripts/Managers`
- `239c38dc` 2026-07-20 null 가드추가 — `Assets/Scenes`
- `4e34e10c` 2026-07-22 ClaimListSlot UI 연출 추가, 미니맵 네비게이터 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `b2e30a72` 2026-07-22 점령 리스트 UI 구현 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `7afcee22` 2026-07-22 UI 씬 수정 — `Assets/Scenes`
- `3fb34981` 2026-07-22 점령 관련 UI수정, 청크 정보 UI추가 — `Assets/Prefabs/UI`, `Assets/Prefabs/Conquest`
- `45bf7b17` 2026-07-22 점령 타이머 UI 추가, UI기능 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `2b7a9044` 2026-07-22 Sample 씬 셋팅 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `30fcca54` 2026-07-23 점령관련 ui 수정 — `Assets/Prefabs/UI`, `Packages`
- `4f37e3d9` 2026-07-23 Day, Wave UI 연결 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `fc96c11c` 2026-07-23 Factory 관련 셋팅 — `Assets/Scenes`
- `3a1dc2a1` 2026-07-24 편의성 추가 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `51ade1ad` 2026-07-24 Claim 패널에 소비자원 인구도 노출되게 추가 — `Assets/Prefabs/UI`
- `100ed471` 2026-07-24 리소스매니저 기본값 셋팅 중 Everthing 적용안되던거 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/Managers`
- `d62bece3` 2026-07-24 UI로 하루 자원 생산량 표기 추가 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `70da04d6` 2026-07-24 Castle 셋팅값 수정 — `Assets/Prefabs/Building`, `Assets/Prefabs`
- `95b23f25` 2026-07-29 Claim_window 프리팹 복구 — `Assets/Prefabs/UI`
- `24bf6068` 2026-07-29 UI 버튼크기 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `2eef4c4d` 2026-07-29 CostToConquerPanel 스크롤 되게 수정(스크롤바는 삭제예정) — `Assets/Prefabs/UI`
- `1f9f4cdf` 2026-07-29 CostToConquerPanel 스크롤바 삭제 — `Assets/Prefabs/UI`
- `1e86421d` 2026-07-29 SampleScene 수정 및 기타 UI 수정 — `Assets/Scenes`, `Assets/Scripts/Grid`
- `7271cf72` 2026-07-29 리베이스후 끊긴 와이어링 연결 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/Grid`
- `26c62318` 2026-07-29 자원량 ui 글자수에 맞게 패널 늘어나게 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `77176af6` 2026-07-30 Stringtable 구현 — `Assets/Scripts/Util`, `Assets/Scripts/UI`
- `bb01485b` 2026-07-30 Ingame_window UI 스트링테이블 연결 — `Assets/Prefabs/UI`, `Assets/StreamingAssets/Localization`
- `0766cfbf` 2026-07-31 ChunkInfoCard UI 프리팹 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `f50bef96` 2026-07-31 claim 관련 UI 스트링테이블 연결 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `f4380a94` 2026-07-31 와이어링 끊긴거 수정 — `Assets/Prefabs/UI`

### 알파 (36개)

- `be914aab` 2026-08-03 경고 메세지 스트링테이블 연결 — `Assets/Prefabs/UI`
- `6923972e` 2026-08-03 경고 메세지 스트링테이블 key 지정 — `Assets/StreamingAssets/Localization`, `Assets/Prefabs/UI`
- `8d4ec056` 2026-08-03 Dragon_window UI 더미작업 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `07bbf79b` 2026-08-04 새끼용 UI 레이아웃 및 디자인 추가 — `Assets/Sprites/UI`, `Assets/Data/BabyDragon`
- `f3716992` 2026-08-04 새끼용 기능 UI 연결 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `1e9baf55` 2026-08-05 어미용 속성 기능, 슬라임 자원량 표기 UI 수정 — `Assets/Scripts/UI`, `Assets/Sprites/UI`
- `6593bb8b` 2026-08-06 속성 아이콘 추가(임시용) — `Assets/Sprites/UI`, `Assets/Scenes`
- `98f320da` 2026-08-06 용 UI 관련 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `2a3dcc9a` 2026-08-06 어미용 속성 변경 제한 경고 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `697af7be` 2026-08-06 연결 누락된거 수정 — `Assets/Scenes`
- `9165e2df` 2026-08-06 용 관련 UI 연결 누락된거 수정 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `b008b6e9` 2026-08-06 DragonSkill_window_Blocker 삭제 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `868637cb` 2026-08-06 용 UI 연출 추가 — `Assets/Prefabs/UI`, `Assets/Sprites/UI`
- `0afacdec` 2026-08-06 babyDragonWindow 삭제 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `ff409a91` 2026-08-06 babyDragon 버튼 삭제 — `Assets/Prefabs/UI`
- `90af4ac9` 2026-08-07 어미용 패널에 스킬 정보 UI 추가 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `d598443f` 2026-08-07 어미용 속성별로 인게임에서 확인할 수 있게 구현 — `Assets/Prefabs`, `Assets/Scenes`
- `bf29d690` 2026-08-10 어미용 스킬 UI 연결 및 아이콘 추가 — `Assets/Data/Dragon`, `Assets/Prefabs/UI`
- `ce487cb6` 2026-08-10 배치된 건물이 없을때 workerMode 버튼 누를시 경고 메세지 추가 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `7415e687` 2026-08-11 스킬 슬롯에 mask 추가, 스킬 설명 UI 패널 ScrollView로 변경 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `6879418c` 2026-08-11 BuildMode 타워 슬롯 Info UI 추가 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `01ad0ded` 2026-08-12 StartScene 타이틀 이미지 추가 — `Assets/Sprites/UI`, `Assets/Prefabs/UI`
- `ff8c879f` 2026-08-12 StartScene 버튼 수정 — `Assets/Scenes`, `Assets/Sprites/UI`
- `6cfc7fb7` 2026-08-12 셋팅 메뉴 작업중 — `Assets/Prefabs/UI`
- `e4853183` 2026-08-12 Factory Info Popup UI 추가, 어미용 스킬정보에 Lock 패널 추가 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `10091780` 2026-08-12 자원 셋팅 되돌림 — `Assets/Scenes`
- `e31c8a8c` 2026-08-12 씬 연결 확인 — `Assets/Scenes`, `Assets/StreamingAssets/Localization`
- `9018e257` 2026-08-13 설정 창 UI 디자인 수정 — `Assets/Prefabs/UI`, `Assets/StreamingAssets/Localization`
- `8a86e007` 2026-08-13 씬 배선 및 프리팹 수정 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `78d86e8c` 2026-08-13 게임 불러오기 창 UI 디자인 수정 — `Assets/Prefabs/UI`, `Assets/StreamingAssets/Localization`
- `8f0ccc87` 2026-08-13 세이브 데이터 삭제시 팝업창 뜨게 추가 구현 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `0b0f4096` 2026-08-13 타이틀씬 연출 작업 — `Assets/StreamingAssets/Localization`, `Assets/Scenes`
- `e965c550` 2026-08-14 빌드모드 탭에서 지을 수 없는 건물은 비활성화 처리되게 구현 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `ecc7be73` 2026-08-14 캐릭터 이동 연출 구현 중 — `Assets/Prefabs/Villager`, `Assets/Scripts/Population`
- `625b204e` 2026-08-14 새끼용 배치 및 포커스 기능을 버튼 전체로 확대 구현 — `Assets/Prefabs/UI`, `Assets/Scripts/Buildings`
- `552ae0b3` 2026-08-14 Setting Window 해상도에 맞춰지게 수정 — `Assets/Scenes`, `Assets/Prefabs/UI`

### 베타 (34개)

- `c462073c` 2026-08-18 주민 캐릭터 연출 및 이펙트 추가 — `Assets/Prefabs/Villager`, `Assets/Scripts/Population`
- `aad44dcb` 2026-08-18 주민 캐릭터 애니메이터 셋팅 — `Assets/Prefabs/Villager`, `Assets/Scripts/Population`
- `45bc2c34` 2026-08-19 오브젝트 풀링 작업 — `Assets/Scripts/Population`, `Assets/Scripts/Monster`
- `4b1e3ef1` 2026-08-19 건물 위 청크에 건물지으면 캐릭터가 움직이던 버그 수정 — `Assets/Prefabs/Villager`, `Assets/Scripts/Population`
- `a15e1f2f` 2026-08-19 캐릭터 안나오는 버그 수정 — `Assets/Scripts/Population`
- `9d738d77` 2026-08-19 캐릭터 연출 prewarm 구현 — `Assets/Scripts/Util`, `Assets/Scripts/Population`
- `3ffafe94` 2026-08-19 prewarm 프리팹 생성 20개로 수정 — `Assets/Scripts/Population`
- `4506934c` 2026-08-19 주민연출 SampleScene 적용 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `d05122ea` 2026-08-19 타워 스프라이트 추가 및 프리팹 셋팅 — `Assets/Data/TowerData`, `Assets/Sprites/Tower`
- `d32a6b17` 2026-08-20 스킬트리 UI 자물쇠 연출 추가 — `Assets/Scripts/UI`, `Assets/Prefabs/UI`
- `0e3e0998` 2026-08-20 UIPaticle 패키지 추가 및 스킬트리 자물쇠 해금 이펙트 추가 — `Assets/Prefabs/UI`, `Packages`
- `92e83b23` 2026-08-21 특정 건물(연구소,봉인석,새끼용)을 마우스 오버하면 아웃라인이 보이게 구현 — `Assets/Prefabs/Building`, `Assets/Scripts/Buildings`
- `321381a2` 2026-08-21 스킬 트리 디테일 패널 UI 수정, 작은 버그 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `2309bbb7` 2026-08-21 건물 설치시 자원소비량 노출 구현 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `fb17650c` 2026-08-21 용 스킬트리 UI 수정 및 기타 UI 개선 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `43e67bdc` 2026-08-21 새끼용 키값 수정 — `Assets/Prefabs/UI`, `Assets/StreamingAssets/Localization`
- `3fe72db2` 2026-08-21 씬 배선 — `Assets/Scenes`
- `fbb31116` 2026-08-25 연구 UI 디자인 수정 및 연출, Rank 추가 — `Assets/Data/Research`, `Assets/Scripts/UI`
- `522387d0` 2026-08-25 Research_window UI 수정중 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `b1f1b70f` 2026-08-25 성 아웃라인 추가, 버튼 디자인 수정 및 연출 추가 — `Assets/Scripts/UI`, `Assets/Scenes`
- `f5318bb4` 2026-08-25 SampleScene 배선 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `0cca28fd` 2026-08-25 주석 오타 수정: 호버 아웃라인 클래스명 — `Assets/Scripts/Buildings`
- `028047e0` 2026-08-26 연구 슬롯 데이터 icon 추가 및 패널 디자인 수정 — `Assets/Data/Research`, `Assets/Scripts/UI`
- `6e96980c` 2026-08-26 오버라이드 된거 취소 — `Assets/Scenes`, `Assets/Prefabs/UI`
- `7ade7833` 2026-08-27 적 등장 예고 UI 수정 — `Assets/Data/MonsterData`, `Assets/Scripts/Monster`
- `50ef583d` 2026-08-27 인구 전체 할당 시 캐릭터 연출 겹치지않게 수정 — `Assets/Scripts/Population`
- `62976846` 2026-08-27 BuildMode UI  수정, 인게임 UI 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `354ff340` 2026-08-27 가이드, 인게임, 드래곤 패널 UI 일부 수정 — `Assets/Prefabs/UI`, `Assets/Scripts/UI`
- `a1fe1796` 2026-08-27 연구 UI 수정 및 연출 추가 — `Assets/Scripts/UI`, `Assets/Data/Research`
- `bc05cc29` 2026-08-27 UI씬 저장 — `Assets/Scenes`
- `dc3f9c52` 2026-08-28 모드 선택 표시를 매 프레임 폴링에서 이벤트 구독으로 전환 — `Assets/Scripts/UI`, `Assets/Scripts/Population`
- `3750f81d` 2026-08-28 인게임 창 UI 배선 복원 — `Assets/Prefabs/UI`
- `19720211` 2026-08-28 배속버튼 수정 후 프리팹화 — `Assets/Prefabs/UI`, `Assets/Scenes`
- `6ce17e5f` 2026-08-28 가이드 도감 UI 수정 — `Assets/Prefabs/UI`

