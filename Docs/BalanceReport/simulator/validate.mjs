// 시뮬 코어 헤드리스 검증.
// 검증 1: 파서 정합성 — 야간 스폰 수/기본 EHP가 report.txt(정적 추출)와 일치하는가
// 검증 2: 보정 — 출고값 그대로 돌렸을 때 7~10일차에 죽는가 (플레이 테스트 관측)
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
eval(fs.readFileSync(path.join(here, 'sim.js'), 'utf8'));
const S = globalThis.BalanceSim;
const bundle = JSON.parse(fs.readFileSync(path.join(here, 'bundle.json'), 'utf8'));
const data = S.prepare(bundle);

/* ---------- 검증 1: 웨이브 파서 ---------- */
const EXPECT = {   // report.txt [2] 표에서 (day: [count, baseEHP])
  1: [6, 300], 6: [20, 1040], 7: [21, 1960], 8: [22, 900], 10: [28, 1400],
  13: [38, 2340], 14: [41, 4140], 15: [39, 2280], 16: [42, 2130], 20: [51, 2850],
  21: [55, 5760], 22: [68, 3960], 25: [80, 4720], 27: [100, 6040], 28: [92, 14600],
};
let ok = true;
console.log('=== 검증 1: 웨이브 파서 정합성 ===');
for (const [dayStr, [expCount, expEhp]] of Object.entries(EXPECT)) {
  const w = data.waveByDay[+dayStr];
  let count = 0, ehp = 0;
  for (const g of w.groups) {
    const m = data.monsters[g.monster];
    count += g.count;
    ehp += g.count * (m.hp + (m.hasShield ? m.shield : 0));
  }
  const pass = count === expCount && ehp === expEhp;
  if (!pass) ok = false;
  console.log(`  day ${dayStr.padStart(2)}  ${count}마리/${ehp} EHP   기대 ${expCount}/${expEhp}  ${pass ? 'OK' : '<<< 불일치'}`);
}
console.log(ok ? '  → 파서 일치\n' : '  → 파서 불일치. 시뮬 결과 신뢰 불가\n');

/* ---------- 기본 설정 ---------- */
// "유능한 플레이어" 기준 정책 — 방어 우선, 확장은 1건씩, 식량은 최소 마진만.
const basePolicy = {
  foodMargin: 1.05, minLogging: 1, minQuarry: 1, labs: 1,
  towerShare: 0.75, towerSpread: 2.5, parallelConquest: 1, conquestReserve: 8,
  farmYield: 30, woodYield: 26, stoneYield: 39,
  conquestDirection: ['Volcano', 'Rock', 'Desert', 'Snow'],
};
const baseCfg = (A) => ({
  policy: basePolicy,
  assumptions: A,
  towerPriority: ['TD_Arrow', 'TD_CrossBow', 'TD_Musket'],
  researchOrder: ['tower_damage', 'tower_firerate_1', 'tower_manpower_1', 'tower_range_1', 'production_optimize', 'convenience_castle_regen_1'],
  dragonSkill: null,
});

/* ---------- 검증 2: L/depth 보정 격자 ---------- */
console.log('=== 검증 2: 레인 길이 L / 방어선 깊이 D 보정 ===');
console.log('  (출고값 기준. 플레이 테스트 관측 = 7~10일차 사망)');
console.log('    L\\D' + [2, 3, 4, 6, 8].map(d => String(d).padStart(6)).join(''));
const grid = {};
for (const L of [16, 20, 24, 30, 36, 45]) {
  const row = [];
  for (const D of [2, 3, 4, 6, 8]) {
    const days = S.runCampaign(data, baseCfg({ laneLength: L, defenseDepth: D }));
    const last = days[days.length - 1];
    const died = last.night.survived ? null : last.day;
    grid[`${L}|${D}`] = died;
    row.push((died === null ? '생존' : `D${died}`).padStart(6));
  }
  console.log(`  ${String(L).padStart(4)}` + row.join(''));
}
const hits = Object.entries(grid).filter(([, d]) => d !== null && d >= 7 && d <= 10);
console.log(`\n  7~10일차 사망을 재현하는 (L,D) 조합: ${hits.length}개`);
hits.slice(0, 10).forEach(([k, d]) => console.log(`    L=${k.split('|')[0]} D=${k.split('|')[1]} → ${d}일차`));

/* ---------- 상세 1회 ---------- */
const PICK = hits.length ? hits[Math.floor(hits.length / 2)][0].split('|').map(Number) : [24, 4];
console.log(`\n=== 상세 실행 (L=${PICK[0]}, D=${PICK[1]}) ===`);
const days = S.runCampaign(data, baseCfg({ laneLength: PICK[0], defenseDepth: PICK[1] }));
console.log(' 일차 인구 농장 타워인구 타워수 점령  자원(F/W/S)      밤: 처치/전체 누수 타워손실 성HP');
for (const d of days) {
  const tw = Object.entries(d.towers).map(([k, v]) => `${k.replace('TD_', '')}x${v}`).join(' ');
  console.log(
    `  ${String(d.day).padStart(2)}  ${String(d.pop).padStart(3)}  ${String(d.plan.farms).padStart(2)}    ` +
    `${String(d.plan.towerPop).padStart(3)}   ${String(d.night.towerCount).padStart(3)}  ${String(d.owned).padStart(2)}  ` +
    `${String(d.food).padStart(4)}/${String(d.wood).padStart(4)}/${String(d.stone).padStart(4)}  ` +
    `${String(d.night.killed).padStart(3)}/${String(d.night.total).padStart(3)} ` +
    `${String(d.night.leaked).padStart(4)} ${String(d.night.towersLost).padStart(5)}   ` +
    `${String(d.night.castleHp).padStart(3)}${d.night.survived ? '' : '  <<< 패배'}${d.starving ? ' 기아' : ''}  ${tw}`
  );
}
