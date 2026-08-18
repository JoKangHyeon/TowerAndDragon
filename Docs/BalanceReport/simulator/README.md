# 밸런스 시뮬레이터 — 빌드와 검증

`../BalanceSimulator.html`을 만드는 소스다. 결과물은 외부 의존성이 없는 단일 HTML이라
브라우저에서 그냥 열면 된다. 이 폴더는 **에셋 값이 바뀌었을 때 다시 굽기 위한 것**이다.

## 다시 굽기

```bash
cd Docs/BalanceReport/simulator
python build_bundle.py     # Unity 에셋 -> bundle.json  (수 초)
python build_html.py       # template.html + sim.js + bundle.json -> ../BalanceSimulator.html
python check_html.py       # 정적 검사
node smoke.mjs             # 헤드리스로 UI를 실제 구동
node shared_test.mjs       # 공유 에셋 전파 회귀
```

`build_bundle.py`는 `Assets/Data/` 아래만 훑는다. `Assets/` 전체를 훑으면 `Imported/` 때문에
2분 이상 걸린다.

## 파일

| 파일 | 역할 |
|---|---|
| `build_bundle.py` | `.meta` GUID 역매핑으로 에셋에서 수치를 긁어 `bundle.json`을 만든다 |
| `sim.js` | 시뮬레이션 코어. 브라우저와 node 양쪽에서 돈다 |
| `template.html` | 페이지 뼈대 + UI. `/*__BUNDLE__*/`, `/*__SIM__*/` 자리에 위 둘이 들어간다 |
| `build_html.py` | 셋을 합쳐 `../BalanceSimulator.html`을 쓴다 |
| `check_html.py` | 플레이스홀더 잔존·JSON·JS 문법·CSS 변수·id 참조·외부 리소스 검사 |
| `smoke.mjs` | 최소 DOM 셤 위에서 UI를 실제로 굴려 런타임 오류를 잡는다 |
| `shared_test.mjs` | 공유 에셋 편집이 참조자 전부에 전파되는지 검증 |
| `validate.mjs` | 웨이브 파서 정합성 + 경로 길이/방어선 깊이 보정 격자 |
| `sensitivity.mjs` | 조정이 상식적인 방향으로 반응하는지 |
| `debug7.mjs` | 특정 일차 밤을 단독으로 돌려 원인 추적 |
| `bundle.json` | `build_bundle.py`가 만드는 중간 산출물. 지워도 되지만, 있으면 재빌드 없이 테스트를 바로 돌릴 수 있다 |

## 시뮬이 지키는 규칙 (게임 코드에서 확인한 것)

- 타워 발사 간격 = `Interval / 충원율 / 공속배율` — `TowerAttack.cs:187`
- 생산량 = `풋프린트 산출 × 충원율` — `ResourceProductionData.cs:34`
- 몬스터는 사거리 안에 타워가 들어오면 **멈춰서** 때린다 — `MonsterAttack.SetCurrentTarget` → `_movement.Stop()`
- 타워는 단일 대상을 잡고 죽거나 벗어날 때까지 유지 — `TowerAttack.IsCurrentTargetValid`
- 성에 도달한 몬스터는 성을 직접 때린다 — `BaseMonster.HandleArrivedAtCastle` → `SetFinalTarget`
- 폭탄병은 자동공격이 꺼져 있고, 폭발은 `IMonsterTarget`(타워/방벽)만 맞는다.
  `Castle`은 `IAttackTarget`이라 폭발에 안 맞는다 — `SelfDestructBehaviorSO`, `MonsterAttack.ExecuteBlast`
- 보호막 오라는 대상당 1회만 부여(`_grantedOnce`), 범위 이탈 시 회수 — `ProtectionAuraBehaviorSO`
- 점령 페널티는 점령한 청크의 지형이 배정된 포탈에만 적용 —
  `ConquestManager.ApplyProfile(chunk.DominantTerrain)` → `WaveRoutePlanner.GetProfiles(portal.TerrainType)`
- 속성 규칙 0=Normal 1=OnlyMatching 2=ImmuneToMatching 3=AllImmune — `MonsterData.AcceptsElement`

## 추정 파라미터

**경로 길이**와 **방어선 깊이** 둘뿐이다. 나머지는 전부 에셋 값이다.

출고값 그대로 돌리면 7일차에 죽고, 이는 플레이 테스트 관측(7~10일차)과 맞는다.
경로 길이 16~45 × 방어선 깊이 2~8의 30개 조합 중 27개가 같은 판정을 낸다 —
추정 파라미터가 결과를 좌우하지 않는다는 뜻이다. `validate.mjs`가 이 격자를 다시 찍는다.

## 공유 에셋 주의

여러 몬스터·타워가 같은 `AttackSO` / `DamageEffect` 에셋을 쓴다.
가장 큰 것은 `MA_DamageEffect.asset` 하나를 **16종이 공유**한다.
툴에서 값을 하나 바꾸면 그 에셋을 참조하는 대상 전부가 함께 바뀌고, 노브 라벨에
"이 에셋을 16종이 공유"라고 표시된다. 이래야 시뮬 판정과 `apply_balance.py`의 실제 결과가 일치한다.

`shared_test.mjs`가 이 동작을 지킨다. 노브를 새로 추가할 때는 `kind: 'effectAmount'` 또는
`kind: 'attackField'`와 `file`을 함께 지정할 것 — 경로에 직접 쓰면 전파가 안 된다.

## Unity에 되돌리기

툴 좌측 상단 **변경분 내보내기** → `balance-changes.json` →

```bash
python Docs/BalanceReport/apply_balance.py balance-changes.json          # 드라이런
python Docs/BalanceReport/apply_balance.py balance-changes.json --apply  # 실제 반영
```

Unity를 닫고 실행할 것. 원본은 `.bak`으로 남는다.
