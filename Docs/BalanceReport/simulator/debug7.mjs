import fs from 'fs'; import path from 'path'; import { fileURLToPath } from 'url';
const here = path.dirname(fileURLToPath(import.meta.url));
eval(fs.readFileSync(path.join(here, 'sim.js'), 'utf8'));
const S = globalThis.BalanceSim;
const raw = JSON.parse(fs.readFileSync(path.join(here, 'bundle.json'), 'utf8'));

const A = { laneLength: 30, defenseDepth: 3 };
function night(mutate, towerCount = 10, staffing = 0.34, day = 7, dragonSkill = null) {
  const b = JSON.parse(JSON.stringify(raw));
  if (mutate) mutate(b);
  const data = S.prepare(b);
  const r = S.simulateNight({
    data, day, assumptions: A,
    defense: { perPortal: { North: [{ key: 'TD_Arrow', count: towerCount, staffing }] } },
    research: {}, enhancement: {}, castleHp: b.economy.castleHp, dragonSkill,
  });
  return r;
}
const p = (label, r) => console.log(
  `  ${label.padEnd(40)} ${r.survived ? '방어 성공' : '패배  '}  처치 ${String(r.killed).padStart(3)}/${r.total}` +
  `  누수 ${String(r.leaked).padStart(2)}  타워손실 ${String(r.towersLost).padStart(2)}/${r.towerCount}` +
  `  성HP ${String(r.castleHp).padStart(3)}  ${r.seconds}초${r.stalled ? ' (교착)' : ''}`);

console.log('=== 7일차 밤 단독 (화살 10기, 충원율 0.34) ===');
p('기준선', night(null));
p('몬스터 체력 x0.5', night(b => Object.values(b.monsters).forEach(m => m.hp *= 0.5)));
p('몬스터 체력 x0.2', night(b => Object.values(b.monsters).forEach(m => m.hp *= 0.2)));
p('몬스터 공격력 x0', night(b => Object.values(b.monsters).forEach(m => {
  if (m.attack) { m.attack.damage = 0; m.attack.effects.forEach(e => { if (e.amount) e.amount = 0; }); }
})));
p('타워 체력 100 -> 1000', night(b => Object.values(b.towers).forEach(t => { t.hp = 1000; })));
p('성 체력 100 -> 2000', night(b => { b.economy.castleHp = 2000; }));
p('Boss_1 제거', night(b => { b.monsters.MD_Boss_1.hp = 1; }));

console.log('\n=== 타워 수 / 충원율 ===');
for (const [n, s] of [[5, 0.68], [10, 0.34], [20, 0.17], [10, 1.0], [20, 1.0], [40, 1.0]]) {
  p(`화살 ${n}기 @ 충원율 ${s}`, night(null, n, s));
}
console.log('\n=== 1~7일차 (화살 10기 @0.34) ===');
for (let d = 1; d <= 7; d++) p(`${d}일차`, night(null, 10, 0.34, d));

console.log('\n=== 어미용 ===');
p('불 액티브 (현재체력 30%)', night(null, 10, 0.34, 7, 'SK_Dragon_GlobalDamage'));
p('불 액티브 + 화살 20기', night(null, 20, 0.34, 7, 'SK_Dragon_GlobalDamage'));
