// 빌드된 HTML의 UI 스크립트를 최소 DOM 셤 위에서 실제로 굴린다.
// 목적: 정적 문법 검사로는 안 잡히는 런타임 오류(오타난 경로, undefined 접근)를 잡는다.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
const SRC = path.join(here, '..', 'BalanceSimulator.html');
const html = fs.readFileSync(SRC, 'utf8');

/* ── 마크업에서 id를 긁어 셤 노드를 만든다 ── */
const IDS = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);

let nodeCount = 0;
function makeNode(tag) {
  nodeCount++;
  const children = [];
  const listeners = {};
  const style = new Proxy({ cssText: '' }, { set(t, k, v) { t[k] = v; return true; } });
  const node = {
    tagName: String(tag).toUpperCase(), children, listeners, style,
    textContent: '', innerHTML: '', className: '', value: '', type: '',
    offsetWidth: 120, offsetHeight: 60,
    classList: {
      _s: new Set(),
      add(...c) { c.forEach(x => this._s.add(x)); },
      remove(...c) { c.forEach(x => this._s.delete(x)); },
      toggle(c, on) { on ? this._s.add(c) : this._s.delete(c); },
      contains(c) { return this._s.has(c); }
    },
    setAttribute(k, v) { node['attr_' + k] = v; },
    getAttribute(k) { return node['attr_' + k]; },
    appendChild(c) { children.push(c); return c; },
    append(...cs) { cs.forEach(c => children.push(c)); },
    addEventListener(ev, fn) { (listeners[ev] = listeners[ev] || []).push(fn); },
    removeEventListener() {},
    getBoundingClientRect() { return { left: 0, top: 0, right: 560, bottom: 250, width: 560, height: 250 }; },
    querySelector(sel) {
      const want = sel.replace(/[^a-z]/g, '').toUpperCase();
      const hit = children.find(c => c.tagName === want);
      if (hit) return hit;
      const made = makeNode(want || 'div');
      children.push(made);
      return made;
    },
    click() {}
  };
  return node;
}

const registry = new Map();
IDS.forEach(id => registry.set(id, makeNode('div')));

globalThis.document = {
  createElement: makeNode,
  createElementNS: (_ns, tag) => makeNode(tag),
  getElementById(id) {
    if (!registry.has(id)) {
      throw new Error(`getElementById('${id}') — 마크업에 없는 id`);
    }
    return registry.get(id);
  }
};
// 번들 스크립트 노드는 실제 JSON을 돌려줘야 한다
const bundleText = html.match(/<script id="bundle" type="application\/json">([\s\S]*?)<\/script>/)[1];
registry.get('bundle').textContent = bundleText;

globalThis.performance = { now: () => Date.now() };
globalThis.innerWidth = 1440; globalThis.innerHeight = 900;
globalThis.Blob = class { constructor(p) { this.parts = p; } };
globalThis.URL = { createObjectURL: () => 'blob:x', revokeObjectURL() {} };
globalThis.location = { reload() { throw new Error('reload 호출됨'); } };

const timers = [];
globalThis.setTimeout = (fn, ms) => { timers.push(fn); return timers.length; };
globalThis.clearTimeout = () => {};

/* ── 스크립트 실행 ── */
const scripts = [...html.matchAll(/<script(?![^>]*application\/json)[^>]*>([\s\S]*?)<\/script>/g)].map(m => m[1]);
console.log(`실행할 script 블록 ${scripts.length}개`);
for (const [i, body] of scripts.entries()) {
  try {
    (0, eval)(body);
    console.log(`  [${i}] 실행 OK`);
  } catch (e) {
    console.error(`  [${i}] 런타임 오류: ${e.message}\n${e.stack.split('\n').slice(1, 4).join('\n')}`);
    process.exit(1);
  }
}

/* ── 초기 렌더가 실제로 값을 채웠는지 ── */
function txt(id) { return registry.get(id).textContent; }
const checks = [
  ['v-main', v => /일차 사망|생존/.test(v)],
  ['v-pop', v => /^\d+$/.test(v)],
  ['v-own', v => /^\d+$/.test(v)],
  ['v-leak', v => /^\d+$/.test(v)],
  ['v-tl', v => /^\d+$/.test(v)],
  ['diffcount', v => String(v) === '0'],
];
let bad = 0;
for (const [id, ok] of checks) {
  const v = txt(id);
  if (!ok(v)) { console.error(`  ${id} = ${JSON.stringify(v)} — 예상과 다름`); bad++; }
  else console.log(`  ${id} = ${JSON.stringify(v)}`);
}
console.log(`strip 셀 ${registry.get('strip').children.length}개 (28 기대)`);
if (registry.get('strip').children.length !== 28) bad++;
console.log(`표 행 ${registry.get('tbl').children.length}개 섹션`);
console.log(`조정 그룹 ${registry.get('knobs').children.length}개`);
if (registry.get('knobs').children.length < 8) { console.error('조정 그룹이 너무 적다'); bad++; }
console.log(`plot-dps 자식 ${registry.get('plot-dps').children.length}개`);
if (registry.get('plot-dps').children.length < 5) { console.error('DPS 차트가 비었다'); bad++; }
console.log(`plot-pop 자식 ${registry.get('plot-pop').children.length}개`);
if (registry.get('plot-pop').children.length < 5) { console.error('인구 차트가 비었다'); bad++; }

/* ── 노브를 하나 돌려서 재시뮬이 도는지 ── */
const knobGroups = registry.get('knobs').children;
let ranged = null;
(function findRange(n) {
  if (ranged) return;
  if (n.type === 'range' && n.listeners.input) { ranged = n; return; }
  (n.children || []).forEach(findRange);
})({ children: knobGroups });
if (!ranged) { console.error('range 입력을 못 찾았다'); bad++; }
else {
  const before = txt('v-main');
  ranged.value = String(Number(ranged.getAttribute('max')) || 1);
  try {
    ranged.listeners.input.forEach(fn => fn());
    while (timers.length) timers.shift()();
    console.log(`노브 조작 후 판정: ${txt('v-main')} (이전 ${before}), 변경 ${txt('diffcount')}건`);
    if (String(txt('diffcount')) === '0') { console.error('변경 카운트가 안 올랐다'); bad++; }
  } catch (e) {
    console.error('노브 조작 중 오류: ' + e.message); bad++;
  }
}

console.log(bad ? `\n=== 실패 ${bad}건 ===` : '\n=== 스모크 테스트 통과 ===');
process.exit(bad ? 1 : 0);
