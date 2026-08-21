# 상태이상 VFX 시스템 구현 계획

> **대상:** 몬스터에게 걸린 지속 상태(화상·둔화 등)를 화면에 표시하는 시스템
>
> **기준:** `feature/towerEffect` (`c30b20af`)
>
> **선행 완료:** 얼음·암석 타워 명중 이펙트(`FX_Impact_IceCrack`, `FX_Impact_StoneCrack`) 배선

---

## 1. 왜 명중 이펙트로는 안 되는가

- 명중은 **1회 사건**, 상태는 **지속 구간** — 얼음 둔화 2초(`TS_IceSlow`), 불 화상 3초(`TS_FireBurn`)
- 명중 이펙트는 월드 좌표에 고정 생성 → 몬스터가 걸어가면 이펙트만 뒤에 남는다
- 명중 이펙트는 **상태가 안 걸려도 재생된다** — 보스 군중제어 면역, `MP_Ground_ElementImmune_*` 대상에게 거짓 신호가 된다
- 에셋 성격도 갈린다 — Crack 계열은 전부 `loop=0` 버스트(일회성), `FX_Dust_Fire`만 `loop=1` 연속 방출(지속형)

결론: 두 연출은 대체 관계가 아니라 **보완 관계**. 상태 표시는 몬스터에 붙여야 한다.

---

## 2. 준비된 에셋

| 프리팹 | 용도 | 성격 |
|---|---|---|
| `Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn` | 화상 지속 표시 | `loop=1`, 연속 방출 |
| (미제작) 둔화용 | 둔화 지속 표시 | 얼음 계열에서 지속형 원본 선정 필요 |

- `FX_Status_FireBurn` 구성: `Fire_A`(0.45) · `Fire_Sheet`(0.45) · `Spark`(0.05), 루트 스케일 0.18
- 정렬 레이어 `Projectile`로 통일 완료
- 재생성 경로: `CoplayScripts/BuildStatusVfx.cs`

---

## 3. 구현 범위

### 3.1 데이터 — `StatusEffectSO`

- `[SerializeField] private GameObject _activeVfxPrefab;` 추가
- 상태 데이터가 자기 연출을 들고 있게 해 새 상태 추가 시 배선표를 늘리지 않는다
- 채울 대상: `TS_FireBurn`, `TS_IceSlow` 2개

### 3.2 알림 — `MonsterStatusReceiver`

- 현재 상태 시작/종료 이벤트가 **없다**
- 만료 판정이 이미 `anyExpired` 플래그를 들고 있고(282·315행) `_monster.RefreshMoveSpeed()`를 부르는 지점이 있어 발화 자리는 확보돼 있다
- `StatusesChanged` 이벤트 추가
- 매 프레임 `CollectActiveStatuses` 폴링은 채택하지 않는다 — 화면의 몬스터 수만큼 매 프레임 도는 낭비

### 3.3 표시 — `MonsterStatusVfx` (신규)

- `BaseMonster`가 `[RequireComponent(typeof(MonsterStatusReceiver))]`로 리시버를 강제하는 것과 같은 방식으로 자동 부착
- **몬스터 프리팹이 33종**이라 손으로 붙이는 방식은 불가
- statusId별 VFX 인스턴스를 몬스터의 **자식**으로 두고 켜고 끔 → 따라다니는 문제 자동 해결
- 앵커 분리 필요: 얼음은 발밑, 화상(`FX_Dust_Fire` 계열)은 몸통 중앙

### 3.4 풀링

- 매번 `Instantiate` 금지
- `ProjectilePool`은 "시간 되면 반납" 전용이라 지속 효과에 맞지 않음
- 상태 해제 시점과 몬스터 사망 시점에 반납하는 `Acquire`/`Release` 쌍이 별도로 필요

---

## 4. 작업 순서

1. `StatusEffectSO`에 필드 추가 → `TS_FireBurn` 연결
2. `MonsterStatusReceiver`에 `StatusesChanged` 이벤트 추가
3. 전용 풀 구현
4. `MonsterStatusVfx` 구현 · `BaseMonster`에 부착
5. 화상 하나로 검증 → 둔화 확장

---

## 5. 검증 항목

- 몬스터가 이동하는 동안 VFX가 따라오는가
- 상태 만료 시점에 정확히 사라지는가
- 몬스터 사망 시 VFX가 남지 않는가
- 같은 상태가 재부여될 때 중복 생성되지 않는가
- 군중제어 면역 보스·속성 면역 몬스터에게 표시되지 않는가
- 화면에 상태 걸린 몬스터가 여럿일 때 전투가 가려지지 않는가

---

## 6. 판단이 필요한 지점

- **일정** — 베타 8/28, 제출 9/3. 런타임 시스템을 새로 넣는 작업이라 명중 이펙트만으로 속성 타격감이 충분한지 먼저 판단할 것
- **강도** — 상태 표시는 명중 임팩트보다 **약하고 작아야** 한다. 동등하면 다수 몬스터 화면에서 전투가 안 보인다
- **둔화 원본 미선정** — 얼음 계열 지속형 후보를 아직 고르지 않았다

---

## 7. 선행 작업에서 남은 항목

상태 VFX와 별개로 명중 이펙트 쪽에 남은 것.

- **임팩트 잘림 4종** — 설정 수명보다 입자 수명이 길어 페이드 없이 끊긴다. Arrow 0.2초/1.0초(80% 잘림), CrossBow 0.26/0.7, AntiAir 0.2/0.3, Musket 0.22/0.3. 생성기에 입자 수명 배수 옵션 추가로 해결
- **크기 사다리 불일치** — Arrow 0.38 ~ AntiAir 2.40으로 6배 차이. 피해량·광역 여부와 무관한 순서
- **Ice 겹침** — 임팩트 수명 1.5초 vs 발사 간격 1.0초, 최대 2개 중첩
- **이름 중복** — `Impact_Tower_StoneMeteor`의 `ground` 2개, `Impact_Tower_AntiAir`의 `Flash` 2개. `ChildScales` 대상 지정 시 양쪽 다 걸린다
- **AntiAir 발사체 미렌더** — `Projectile_V2_13_lightning`은 `renderMode = None`이라 몸통을 그리지 않는다(현 배선 유지 중)
- **플레이 미검증** — 7종 타워 이펙트 전부 실제 전투에서 확인하지 않았다
