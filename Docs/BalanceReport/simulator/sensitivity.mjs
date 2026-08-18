// 시뮬이 조정에 "상식적으로" 반응하는지 확인한다. 방향이 틀리면 모델이 잘못된 것이다.
import fs from 'fs'; import path from 'path'; import { fileURLToPath } from 'url';
const here = path.dirname(fileURLToPath(import.meta.url));
eval(fs.readFileSync(path.join(here, 'sim.js'), 'utf8'));
const S = globalThis.BalanceSim;
const raw = JSON.parse(fs.readFileSync(path.join(here, 'bundle.json'), 'utf8'));

const POLICY = {
  foodMargin: 1.05, minLogging: 1, minQuarry: 1, labs: 1,
  towerShare: 0.75, towerSpread: 2.5, parallelConquest: 1, conquestReserve: 8,
  farmYield: 30, woodYield: 26, stoneYield: 39,
  conquestDirection: ['Volcano', 'Rock', 'Desert', 'Snow'],
};
const A = { laneLength: 30, defenseDepth: 3 };
const CFG = (over = {}) => ({
  policy: { ...POLICY, ...(over.policy || {}) }, assumptions: A,
  towerPriority: over.towerPriority || ['TD_Arrow', 'TD_CrossBow', 'TD_Musket'],
  researchOrder: ['tower_damage', 'tower_firerate_1', 'tower_manpower_1', 'tower_range_1', 'production_optimize', 'convenience_castle_regen_1'],
  dragonSkill: over.dragonSkill || null,
});
function run(mutate, over) {
  const b = JSON.parse(JSON.stringify(raw));
  if (mutate) mutate(b);
  const days = S.runCampaign(S.prepare(b), CFG(over));
  const last = days[days.length - 1];
  return { died: last.night.survived ? null : last.day, days: days.length, last };
}
const show = (label, r) =>
  console.log(`  ${label.padEnd(46)} ${r.died ? `${r.died}일차 사망` : '28일 생존'}` +
    `  (최종 성HP ${r.last.night.castleHp}, 인구 ${r.last.pop}, 점령 ${r.last.owned})`);

console.log('=== 민감도 검사 (기준선 = 출고값) ===');
show('기준선', run(null));
console.log('\n-- 적 약화 → 더 오래 버텨야 한다');
show('Boss_1 체력 800 → 400', run(b => { b.monsters.MD_Boss_1.hp = 400; }));
show('모든 몬스터 체력 ×0.5', run(b => { Object.values(b.monsters).forEach(m => m.hp *= 0.5); }));
show('모든 몬스터 공격력 ×0.5', run(b => {
  Object.values(b.monsters).forEach(m => {
    if (m.attack) { m.attack.damage *= 0.5; m.attack.effects.forEach(e => { if (e.amount) e.amount *= 0.5; }); }
  });
}));
console.log('\n-- 플레이어 강화 → 더 오래 버텨야 한다');
show('화살 타워 피해 10 → 20', run(b => {
  b.towers.TD_Arrow.attack.effects[0].amount = 20;
  b.towers.TD_Arrow.attack.damage = 20;
}));
show('타워 체력 100 → 300', run(b => {
  Object.values(b.towers).forEach(t => { t.hp = 300; });
}));
show('성 체력 100 → 500', run(b => { b.economy.castleHp = 500; }));
show('농장 정원 10 → 6', run(b => { b.production.RPD_FarmField.pop = 6; }));
console.log('\n-- 정책 변화');
show('타워 배분 0.75 → 0.4 (후방 투자)', run(null, { policy: { towerShare: 0.4 } }));
show('타워 배분 0.75 → 0.95 (전방 몰빵)', run(null, { policy: { towerShare: 0.95 } }));
show('얇게 펴기 2.5 → 1.0 (정원 만충)', run(null, { policy: { towerSpread: 1.0 } }));
show('얇게 펴기 2.5 → 5.0', run(null, { policy: { towerSpread: 5.0 } }));
show('점령 안 함 (reserve 0)', run(null, { policy: { conquestReserve: 0 } }));
show('점령 병렬 3건', run(null, { policy: { parallelConquest: 3, conquestReserve: 24 } }));
console.log('\n-- 용');
show('어미용 불 액티브 (현재체력 30%)', run(null, { dragonSkill: 'SK_Dragon_GlobalDamage' }));
show('어미용 암석 메테오 (고정 40)', run(null, { dragonSkill: 'SK_Dragon_Meteor' }));
console.log('\n-- 타워 선택');
show('머스킷 우선', run(null, { towerPriority: ['TD_Musket', 'TD_Arrow'] }));
show('석궁 우선', run(null, { towerPriority: ['TD_CrossBow', 'TD_Arrow'] }));
