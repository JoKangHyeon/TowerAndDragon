# Archer Pack 적용 구상

석궁 타워 발사체 교체를 시작으로 Archer Pack을 타워 이펙트에 쓰기 위한 작업 계획.
`Docs/타워이펙트_작업노트.md`의 함정들이 그대로 적용되므로 **작업 전 그 문서를 먼저 읽을 것.**

작성: 2026-08-24

---

## 0. 결론

- **쓸 수 있다.** ParticleSystem 기반이라 이 프로젝트(URP 2D Renderer)에서 그대로 동작한다.
- **URP unitypackage 임포트는 불필요하다.** 에디터에서 이미 정상 표시되는 것을 확인했다.
  (팩 문서는 2019.4 기준으로 쓰여 "마젠타로 뜬다"고 하지만 해당 없음.
  머티리얼 38개가 `fileID: 211` 내장 파티클 셰이더를 물고 `m_InvalidKeywords`가 비어 있다.)
- **`AP_Packages/*.unitypackage` 두 개는 임포트하지 말 것.** 팩 문서가 시키는
  "팩 소유 UniversalRenderPipelineAsset을 Graphics Settings에 넣어라"는 지시는
  프로젝트의 2D 렌더러 설정을 덮어쓴다.

### 같이 검토한 ArrowsVFX는 못 쓴다

`Assets/Imported/ArrowsVFX`는 VFX Graph(`.vfx`) 기반이다.

- `com.unity.visualeffectgraph`가 `Packages/manifest.json`에 없다
- 설치해도 **VFX Graph는 URP 2D Renderer를 지원하지 않는다**
- 렌더러를 Universal Renderer로 바꿔야 하는데 프로젝트 전체가 2D 아트다

팀에 새 에셋을 요청할 때 기준: **파티클 시스템 기반일 것.**

---

## 1. 팩 구성

`Assets/Imported/Archer Pack/AP_Particles/AP_Prefabs/` 아래 4단계 × 6속성 = 96개.

| 단계 | 개수 | 용도 | 파티클 수 |
|---|---|---|---|
| `Casting` | 24 | 시전 (타워에는 안 씀) | - |
| `Launch` | 24 | **머즐** | 5 + Light 1 |
| `Projectile` | 24 | **발사체 본체** | 3 + Light 1 |
| `Explosion` | 24 | **임팩트** | 6 + Light 1 |

속성: `Arcane`(5) · `Dark`(3) · `Fire`(5) · `Ice`(5) · `Light`(3) · `Storm`(3)

**단계별로 속성이 세트로 맞아 있다** — `ProjectileVisual`의 머즐/궤적/임팩트 슬롯에 그대로 대응한다.
같은 속성 안의 번호(`_2`~`_5`)는 변형이다.

---

## 2. 현재 타워 배선

| TowerData | 발사체 프리팹 |
|---|---|
| `TD_Arrow` | `Projectile_Tower_Arrow_Nature` |
| `TD_CrossBow` | `Projectile_Tower_Musket` ← **교체 대상** |
| `TD_Musket` | `Projectile_Tower_Musket_Orange` |
| `TD_AntiAir` | `Projectile_Tower_AntiAir` |
| `TD_FireTower` | `Projectile_Tower_Fire` |
| `TD_IceTower` | `Projectile_Tower_Ice` |
| `TD_StoneTower` | `Projectile_Tower_StoneMeteor` |

석궁이 지금 머스킷 발사체를 물고 있다. 작업노트 6절의 "Arrow와 CrossBow가 색만으로 구분된다"가
이 교체의 동기다.

---

## 3. 프리팹 손질 — 이게 작업의 본체

Archer 프리팹은 **스스로 날아가고 스스로 죽는** 구조라 이 프로젝트의 풀 구조와 정면충돌한다.
그대로 꽂으면 안 된다.

### 3-1. 떼야 할 것

| 대상 | 이유 |
|---|---|
| `AP_Projectiles` (MonoBehaviour) | `Start`에서 `rb.linearVelocity`로 스스로 날고, `OnCollisionEnter`에서 `Destroy(gameObject)` — 풀 반납이 아니라 파괴라 풀 장부가 샌다 |
| `Rigidbody` | 위 스크립트가 쓰는 것. `Projectile`은 `MoveTowards`로 간다 |
| `SphereCollider` | 명중 판정은 `Projectile`이 거리로 한다 |
| `Point Light` | 2D 씬에서의 영향 **확인 필요** (아래 5-2) |

### 3-2. 붙여야 할 것

`Projectile` + `ProjectileVisual` 두 개. 기존 타워 발사체와 같은 형태로 맞춘다.

기준 프리팹: `Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_Musket.prefab`
루트에 파티클 2개 + MonoBehaviour 2개만 있고 Rigidbody·Collider·Light가 없다.

`ProjectileVisual` 슬롯 (머스킷 실제 값):

```
_trailParticles: []                      ← 궤적을 코드로 제어할 때만 채운다
_muzzlePrefab:   Archer_Launch_<속성>
_muzzleLifetimeSeconds: 0.16
_impactPrefab:   Archer_Explosion_<속성>
_impactLifetimeSeconds: 0.22
_rotateImpactToTravelDirection: 1        ← 화살류는 1 (지면 이펙트만 0, 작업노트 3-8)
_impactPlacement: 1                      ← 0=TargetOrigin / 1=Body / 2=Ground
_nearTracerPrefab: ...                   ← 근거리 보정, 필요 시
```

### 3-3. 클론 위치

`Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_CrossBow.prefab`을 덮어쓰지 말고
**새 이름으로 만들어 TowerData 참조를 바꾼다.** 되돌리기가 쉽다.

원본(`AP_Prefabs/`)은 절대 수정하지 않는다 — `Assets/Imported`는 팀 공유 저장소다.

---

## 4. 작업 순서

1. **속성 후보 고르기** (아래 5-1) — 화살(Nature 계열)과 확실히 구분되는 것
2. `Archer_Projectile_<속성>`을 `Prefabs/Projectile/Tower/`로 복제, `Projectile_Tower_CrossBow_AP` 등으로 명명
3. 3-1 컴포넌트 제거 → 3-2 컴포넌트 추가·배선
4. `TD_CrossBow._projectilePrefab`을 새 프리팹으로 교체
5. 플레이 테스트 — 궤적·머즐·임팩트가 정사영 2D에서 어떻게 읽히는지
6. 크기·색 조정 (작업노트 3-6: 렌더 시트 비교 시 `orthographicSize` 통일할 것)
7. **`Assets/Imported`에서 커밋** (`git -C Assets/Imported`) + 메인 저장소에 TowerData 커밋 — 저장소 두 개다

---

## 5. 판단이 필요한 지점

### 5-1. 석궁을 어느 속성으로 할지

Arrow가 Nature 계열이므로 그것과 겹치지 않아야 한다. 후보:

- **`Storm`** — 번개 계열. 다만 `TD_AntiAir`가 이미 lightning 계열이라 겹칠 수 있다
- **`Light`** — 밝은 직선 궤적. 석궁의 "빠르고 관통하는" 느낌에 맞을 듯
- **`Arcane`** — 보라 계열. 기존 `StoneMeteor`가 색조 280(보라)이라 확인 필요

**속성 5종(Fire/Ice/Arcane 등)이 갖춰져 있으므로 화염·얼음 타워까지 한 팩으로 통일하는 것도 선택지다.**
지금은 Hovl + Eric + 자체 조합이 섞여 있어 톤이 일정하지 않다.

### 5-2. Point Light를 뺄지 말지

3단계 프리팹 전부에 `Light`가 하나씩 있다. URP 2D에서 3D Light가 스프라이트에 어떻게 작용하는지
눈으로 봐야 한다. 주변을 물들이면 제거, 무해하면 그대로.

### 5-3. 궤적 길이

모든 프리팹이 `lengthInSec: 5`인데 이 프로젝트 발사체 속도는 10이다.
사거리를 고려하면 실제 비행 시간이 훨씬 짧아 궤적이 안 끊기고 남을 수 있다.

**작업노트 3-2를 반드시 볼 것** — 궤적 문제는 두께가 아니라 **구조**인 경우가 많다.
배수로 해결하려다 실패한 기록이 있다.

---

## 6. 사라진 도구 — 필요하면 복구

커밋 `88578fdf`("생성기 스크립트 정리")에서 다음이 삭제됐다.

| 스크립트 | 하는 일 |
|---|---|
| `AssignTowerProjectiles.cs` | 타워별 클론 생성 + TowerData 배선 자동화 (643줄) |
| `RenderProjectileTrails.cs` | 발사체를 실제로 날리며 궤적 측정 |
| `BuildStatusVfx.cs` | 임팩트 조합 |
| `RenderImpactAlign.cs` | 여러 임팩트를 같은 시트에 굽기 |

복구:
```
git show 88578fdf~1:CoplayScripts/AssignTowerProjectiles.cs > CoplayScripts/AssignTowerProjectiles.cs
```

**`AssignTowerProjectiles`가 3~4단계를 자동화한다.** 속성 하나만 바꿀 거면 손으로 해도 되지만,
여러 타워를 한꺼번에 갈아끼울 거면 이걸 되살려 정의 테이블만 고치는 편이 빠르다.

`RenderProjectileTrails`는 5-3 판단에 필요하다 — 제자리에서 Simulate만 하면
World 공간 파티클이 한 점에 겹쳐 궤적을 볼 수 없다.

남아 있는 것: `MeasureImpactChildren.cs`, `VerifyTowerDataRefs.cs`

---

## 7. 주의

- **`AP_Demo/AP_Scripts/AP_Demo.cs`가 `Input.GetKeyDown`을 쓴다.**
  이 프로젝트는 새 Input System 전용(`activeInputHandler: 1`)이고 팩에 asmdef가 없어
  `Assembly-CSharp`에 포함된다. 컴파일 에러가 나면 `AP_Demo` 폴더째 삭제해도 무방하다
  (데모 씬 전용). 현재 로그에는 에러가 안 잡히니 실제로는 통과할 수도 있다.
- 원본 기울기는 건드리지 말 것 (작업노트 2-2)
- `HorizontalBillboard` 자식이 있으면 이 카메라에서 안 그려진다 (작업노트 2-1)
- 자식 이름으로 후보를 거르지 말 것 — 이름과 실제 그림이 자주 다르다 (작업노트 3-1)
