// 공유 에셋 회귀 테스트.
// 검증하는 것: 툴에서 값 하나를 바꿨을 때, 그 에셋을 참조하는 대상이 시뮬 안에서도
// 전부 함께 바뀌는가. 안 바뀌면 시뮬 판정과 apply_balance.py의 결과가 어긋난다.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));

const SRC = path.join(here, '..', 'BalanceSimulator.html');
const html = fs.readFileSync(SRC, 'utf8');

/* ── 최소 DOM 셤 (smoke.mjs와 동일) ── */
const IDS = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
function makeNode(tag) {
  const children = [], listeners = {};
  const node = {
    tagName: String(tag).toUpperCase(), children, listeners,
    style: { cssText: '' }, textContent: '', innerHTML: '', className: '', value: '', type: '',
    offsetWidth: 120, offsetHeight: 60,
    classList: { _s: new Set(), add(...c) { c.forEach(x => this._s.add(x)); },
      remove(...c) { c.forEach(x => this._s.delete(x)); },
      toggle(c, on) { on ? this._s.add(c) : this._s.delete(c); },
      contains(c) { return this._s.has(c); } },
    setAttribute(k, v) { node['attr_' + k] = v; }, getAttribute(k) { return node['attr_' + k]; },
    appendChild(c) { children.push(c); return c; }, append(...cs) { cs.forEach(c => children.push(c)); },
    addEventListener(ev, fn) { (listeners[ev] = listeners[ev] || []).push(fn); },
    removeEventListener() {},
    getBoundingClientRect: () => ({ left: 0, top: 0, right: 560, bottom: 250, width: 560, height: 250 }),
    querySelector(sel) {
      const want = sel.replace(/[^a-z]/g, '').toUpperCase();
      const hit = children.find(c => c.tagName === want);
      if (hit) return hit;
      const made = makeNode(want || 'div'); children.push(made); return made;
    },
    click() {}
  };
  return node;
}
const registry = new Map();
IDS.forEach(id => registry.set(id, makeNode('div')));
globalThis.document = {
  createElement: makeNode, createElementNS: (_n, t) => makeNode(t),
  getElementById: id => { if (!registry.has(id)) throw new Error('없는 id: ' + id); return registry.get(id); }
};
registry.get('bundle').textContent =
  html.match(/<script id="bundle" type="application\/json">([\s\S]*?)<\/script>/)[1];
globalThis.performance = { now: () => Date.now() };
globalThis.innerWidth = 1440; globalThis.innerHeight = 900;
globalThis.Blob = class { constructor(p) { this.parts = p; } };
globalThis.URL = { createObjectURL: () => 'blob:x', revokeObjectURL() {} };
globalThis.location = { reload() {} };
const timers = [];
globalThis.setTimeout = fn => { timers.push(fn); return timers.length; };
globalThis.clearTimeout = () => {};
const flush = () => { while (timers.length) timers.shift()(); };

for (const [, body] of html.matchAll(/<script(?![^>]*application\/json)[^>]*>([\s\S]*?)<\/script>/g)) {
  (0, eval)(body);
}
const API = globalThis.balanceSim;
let bad = 0;
const ok = (cond, msg) => { console.log((cond ? '  OK   ' : '  FAIL ') + msg); if (!cond) bad++; };

/* ── 1) 인덱스가 공유를 제대로 잡았나 ── */
const SHARED = 'Assets/Data/DamageEffect/MA_DamageEffect.asset';
const refs = API.effectRefs[SHARED] || [];
console.log(`\n[1] 공유 인덱스`);
ok(refs.length >= 14, `${SHARED.split('/').pop()} 참조 ${refs.length}건 (14+ 기대)`);

/* ── 2) 노브 하나를 돌리면 참조자 전부가 바뀌나 ── */
console.log(`\n[2] 편집 전파`);
// 몬스터 목록은 기본 'Boss' 필터라 공유 에셋을 쓰는 지상 잡몹이 안 보인다.
// 사용자가 검색창에 치는 것과 같은 경로로 목록을 바꾼다.
let searchBox = null;
(function walk(n) {
  if (searchBox) return;
  if (n.getAttribute && n.getAttribute('aria-label') === '몬스터 이름 필터') { searchBox = n; return; }
  (n.children || []).forEach(walk);
})({ children: registry.get('knobs').children });
ok(!!searchBox, '몬스터 필터 입력을 찾았다');
if (searchBox) {
  searchBox.value = 'Ground_Basic';
  searchBox.listeners.input.forEach(f => f());
}
const knob = API.knobs.find(k => k.kind === 'effectAmount' && k.file === SHARED);
ok(!!knob, `공유 피해 노브를 찾았다 (${knob && knob.label})`);
if (knob) {
  ok(knob.shared >= 14, `노브가 공유 개수를 알고 있다: ${knob.shared}`);
  // knobRow가 만든 range 입력을 찾아 실제 사용자 조작처럼 굴린다
  let input = null;
  (function walk(n) {
    if (input) return;
    if (n.type === 'range' && n.listeners.input && n.getAttribute('aria-label') === knob.label) { input = n; return; }
    (n.children || []).forEach(walk);
  })({ children: registry.get('knobs').children });
  ok(!!input, '해당 노브의 range 입력을 찾았다');
  if (input) {
    input.value = '1';
    input.listeners.input.forEach(f => f());
    flush();
    const cur = API.current;
    const vals = refs.map(p => p.split('.').reduce((a, k) => a[/^\d+$/.test(k) ? +k : k], cur).amount);
    const allOne = vals.every(v => v === 1);
    ok(allOne, `참조자 ${refs.length}건 전부 1로 바뀜 (실제: ${[...new Set(vals)].join(',')})`);

    // damage 재계산도 따라왔나
    const basic = cur.monsters.MD_Ground_Basic.attack;
    ok(basic.damage === 1, `MD_Ground_Basic.attack.damage 재계산됨: ${basic.damage}`);
    const paladin = cur.monsters.MD_Ground_Paladin.attack;
    ok(paladin.damage === 1, `MD_Ground_Paladin.attack.damage 재계산됨: ${paladin.damage}`);

    // 공유하지 않는 대상은 그대로여야 한다
    ok(cur.monsters.MD_Ground_Bomber.attack.damage === 100,
      `공유 안 하는 Bomber는 그대로: ${cur.monsters.MD_Ground_Bomber.attack.damage}`);

    // 판정이 실제로 완화됐나 (16종의 공격력이 5 → 1)
    console.log(`  판정: ${registry.get('v-main').textContent}`);
  }
}

/* ── 3) 속성 타워 attack 경로가 실재하는가 ── */
console.log(`\n[3] 내보내기 경로 실재 확인`);
const REPO = path.join(here, '..', '..', '..') + path.sep;
const missing = [];
for (const k of API.knobs) {
  if (!k.asset || k.asset.endsWith('.unity')) continue;
  if (!fs.existsSync(REPO + k.asset)) missing.push(`${k.label} → ${k.asset}`);
}
ok(missing.length === 0, `노브 ${API.knobs.filter(k => k.asset).length}개의 에셋 경로 전부 실재` +
  (missing.length ? `\n         없음: ${missing.slice(0, 6).join('\n         ')}` : ''));

/* ── 4) 속성 타워 노브가 실제로 생성됐는지 ── */
console.log(`\n[4] 속성 타워`);
['불', '얼음', '암석'].forEach(nm => {
  const k = API.knobs.find(x => x.label === nm + ' 공격 간격');
  ok(!!k && !!k.asset && fs.existsSync(REPO + k.asset), `${nm} 공격 간격 → ${k ? k.asset : '노브 없음'}`);
});

console.log(bad ? `\n=== 실패 ${bad}건 ===` : '\n=== 공유 에셋 테스트 통과 ===');
process.exit(bad ? 1 : 0);
