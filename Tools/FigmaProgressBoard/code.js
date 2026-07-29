// =============================================================================
// Tower and Dragons — 프로젝트 진행 현황 보드 생성 플러그인
//
// Figma 캔버스에 한 장짜리 대형 발표 보드를 네이티브 노드로 생성합니다.
// - 오토레이아웃 기반 섹션 구성
// - 시스템 카드는 컴포넌트 + 인스턴스 구조 (한 번 고치면 전부 반영)
// - 컬러/텍스트 스타일을 파일 스타일로 등록 (테마 변경 용이)
//
// 데이터 기준일: 2026-07-29
// =============================================================================

// ----------------------------------------------------------------------------
// 0. 데이터 (수정은 여기만)
// ----------------------------------------------------------------------------

var META = {
  projectName: 'TOWER AND DRAGONS',
  title: '프로젝트 진행 현황',
  concept: '낮의 선택이 밤의 생존을 결정한다',
  asOf: '2026. 07. 29. 기준',
  team: '수성록 3팀 · 조강현 · 김지해 · 이하늘 · 나상욱',
  genre: '타워디펜스 × 도시건설 · 싱글플레이 · Windows PC · Unity / C#'
};

var KPIS = [
  { value: '587', label: '커밋', sub: '7/3 ~ 7/29 · 21일 작업' },
  { value: '232', label: 'C# 스크립트', sub: 'Assets/Scripts' },
  { value: '21,580', label: '코드 라인', sub: '테스트 · 에디터 툴 포함' },
  { value: '187', label: '데이터 에셋', sub: 'ScriptableObject 30종' },
  { value: '82', label: '프리팹', sub: '건물 · 몬스터 · UI · 포탈' },
  { value: '8', label: '작업 씬', sub: '통합 1 + 기능별 7' }
];

// 마일스톤 (CLAUDE.md 기준)
var TIMELINE = {
  start: '2026-07-08',
  end: '2026-09-03',
  today: '2026-07-29',
  phases: [
    { from: '2026-07-08', to: '2026-07-31', label: '프로토타입', state: 'current' },
    { from: '2026-07-31', to: '2026-08-14', label: '알파', state: 'next' },
    { from: '2026-08-14', to: '2026-08-28', label: '베타', state: 'future' },
    { from: '2026-08-28', to: '2026-09-03', label: '제출', state: 'future' }
  ],
  markers: [
    { at: '2026-07-31', label: '프로토타입 완료', date: '7 / 31' },
    { at: '2026-08-14', label: '알파', date: '8 / 14' },
    { at: '2026-08-28', label: '베타', date: '8 / 28' },
    { at: '2026-09-03', label: '최종 제출', date: '9 / 3' }
  ]
};

// 일별 커밋 수 (git log 집계)
var COMMITS = [
  ['7/3', 1], ['7/8', 4], ['7/9', 55], ['7/10', 69], ['7/11', 3], ['7/12', 2],
  ['7/13', 89], ['7/14', 52], ['7/15', 63], ['7/16', 20], ['7/17', 11],
  ['7/18', 3], ['7/20', 49], ['7/21', 39], ['7/22', 33], ['7/23', 28],
  ['7/24', 13], ['7/25', 2], ['7/27', 24], ['7/28', 23], ['7/29', 4]
];

// 시스템별 구현 현황
var SYSTEMS = [
  {
    title: '낮–밤 사이클 · 정산',
    pct: 100, status: 'done',
    desc: '낮 무제한 운영 → 밤 웨이브 → 전멸 시 자동 낮 전환. 정산 순서, 낮/밤 조명·안개 전환까지 동작.',
    meta: 'CycleManager · DailySettlementManager · CycleLight'
  },
  {
    title: '인구 배분',
    pct: 100, status: 'done',
    desc: '생산 · 방어 · 점령 · 연구 4방향 배치/회수. 식량 유지비와 기아 시 사망 우선순위까지 구현.',
    meta: '10파일 692줄 · PopulationManager · UpkeepSystem'
  },
  {
    title: '데이터 기반 설계',
    pct: 100, status: 'done',
    desc: '미션 필수 요건 충족. SO 30종 · 에셋 187개 · CSV 로컬라이제이션. 싱글톤 없이 Construct 주입 패턴.',
    meta: 'CreateAssetMenu 39곳 · 전용 에디터 툴 5종'
  },
  {
    title: '점령 시스템',
    pct: 95, status: 'done',
    desc: '청크 단위 상태 관리, 인접 청크 출격, 자원+인구 비용, 다일 소요, 주둔지 자동 배치, 시야 확장.',
    meta: '10파일 · ConquestManager · 땅 가치 → 적 강화 연동'
  },
  {
    title: '웨이브 · 보스 주기 · 포탈',
    pct: 90, status: 'done',
    desc: '28일 4주기, 포탈 N→S→E→W 순차 개방, UniTask 병렬 스폰, 4주기 완주 시 승리 처리.',
    meta: 'wave 1~28 데이터 완비 · 보스 데이터 1 / 4종'
  },
  {
    title: '몬스터 · 특수형',
    pct: 90, status: 'done',
    desc: '자폭 · 치유 · 감전 · 방어막 · 원거리 · 공중형. 상속 대신 SpecialBehaviorSO 조합 설계.',
    meta: '18파일 1,207줄 · MonsterData 8종'
  },
  {
    title: 'UI',
    pct: 85, status: 'done',
    desc: '인게임 HUD, 건물창, 점령창, 새끼용 인벤토리, 연구/스킬 트리 창. 전체 코드의 26%.',
    meta: '41파일 5,644줄 · 승리/패배 창은 최소 구현'
  },
  {
    title: '자원',
    pct: 80, status: 'partial',
    desc: '기초 3종 + 특화 4종 + 슬라임 5종. 셀 생산량 × 충원율 × 새끼용 버프 × 연구 배율 합산.',
    meta: '생산시설 9종 · 광물(기본 4번째) 미도입'
  },
  {
    title: '타워',
    pct: 75, status: 'partial',
    desc: '인구 On/Off로 가동, 체력 소진 시 파괴 대신 비활성화 후 재가동, 아침 회복, 철거 환급률.',
    meta: '타워 데이터 1종(TD_Normal) · 속성/티어 강화 미구현'
  },
  {
    title: '새끼용',
    pct: 65, status: 'partial',
    desc: '설치 · 이동 · 철거, 속성별 슬라임 먹이 소비, 생산 버프, 알 부화, 인벤토리 UI 완료.',
    meta: '공격 버프 · 지역 효과 · 보스 보상 연결 미구현'
  },
  {
    title: '연구',
    pct: 60, status: 'partial',
    desc: '3갈래 5티어 격자, RP 전역 풀, 주기 연동 티어 잠금, 선행 조건·자원 코스트 동작.',
    meta: '노드 27개 완비 · 효과 SO는 2개만 연결'
  },
  {
    title: '어미용 · 스킬트리',
    pct: 55, status: 'partial',
    desc: '5속성 × 6노드 방사형 트리 UI, 해금 판정·게이트·코스트, 낮 1회 속성 변경 구현.',
    meta: '패시브/액티브/새끼용 효과 전부 stub 상태'
  },
  {
    title: '자동화 테스트',
    pct: 20, status: 'weak',
    desc: '웨이브 주기 · 포탈 해금 규칙만 커버. 나머지는 런타임 디버그 하네스로 수동 검증.',
    meta: '2파일 148줄 · 디버그 하네스 9종'
  }
];

var STATUS_LABEL = { done: '구현 완료', partial: '부분 구현', weak: '보완 필요' };

// ----------------------------------------------------------------------------
// 1. 팔레트 / 레이아웃 상수
// ----------------------------------------------------------------------------

var P = {
  bg: '#0A0F1E',
  panel: '#131B31',
  panelAlt: '#0F1627',
  stroke: '#26324D',
  track: '#1B2338',
  textHi: '#F1F5F9',
  textMid: '#9AA8BF',
  textLow: '#5E6E8A',
  accent: '#F2B341',
  done: '#3ED598',
  partial: '#FBBF24',
  weak: '#F87171',
  info: '#60A5FA'
};

var STATUS_COLOR = { done: P.done, partial: P.partial, weak: P.weak };

var BOARD_W = 3600;
var PAD = 100;
var INNER = BOARD_W - PAD * 2;   // 3400
var CARD_GAP = 24;
var CARDS_PER_ROW = 4;
var CARD_W = Math.floor((INNER - CARD_GAP * (CARDS_PER_ROW - 1)) / CARDS_PER_ROW); // 832
var CARD_PAD = 32;
var CARD_INNER = CARD_W - CARD_PAD * 2; // 768

var F = {}; // 해석된 폰트 저장소

// ----------------------------------------------------------------------------
// 2. 유틸
// ----------------------------------------------------------------------------

function hexToRgb(hex) {
  return {
    r: parseInt(hex.slice(1, 3), 16) / 255,
    g: parseInt(hex.slice(3, 5), 16) / 255,
    b: parseInt(hex.slice(5, 7), 16) / 255
  };
}

function solid(hex, opacity) {
  return [{
    type: 'SOLID',
    color: hexToRgb(hex),
    opacity: opacity === undefined ? 1 : opacity
  }];
}

// 한글이 지원되는 폰트를 우선 탐색한다.
var FONT_FALLBACKS = {
  bold: [
    ['Pretendard', 'Bold'], ['Noto Sans KR', 'Bold'], ['Nanum Gothic', 'Bold'],
    ['Malgun Gothic', 'Bold'], ['Apple SD Gothic Neo', 'Bold'],
    ['Inter', 'Bold'], ['Arial', 'Bold'], ['Roboto', 'Bold']
  ],
  medium: [
    ['Pretendard', 'SemiBold'], ['Noto Sans KR', 'Medium'], ['Nanum Gothic', 'Bold'],
    ['Malgun Gothic', 'Bold'], ['Apple SD Gothic Neo', 'SemiBold'],
    ['Inter', 'Medium'], ['Arial', 'Bold'], ['Roboto', 'Medium']
  ],
  regular: [
    ['Pretendard', 'Regular'], ['Noto Sans KR', 'Regular'], ['Nanum Gothic', 'Regular'],
    ['Malgun Gothic', 'Regular'], ['Apple SD Gothic Neo', 'Regular'],
    ['Inter', 'Regular'], ['Arial', 'Regular'], ['Roboto', 'Regular']
  ]
};

async function resolveFonts() {
  var roles = Object.keys(FONT_FALLBACKS);
  for (var i = 0; i < roles.length; i++) {
    var role = roles[i];
    var list = FONT_FALLBACKS[role];
    for (var j = 0; j < list.length; j++) {
      var candidate = { family: list[j][0], style: list[j][1] };
      try {
        await figma.loadFontAsync(candidate);
        F[role] = candidate;
        break;
      } catch (e) { /* 다음 후보로 */ }
    }
    if (!F[role]) throw new Error('사용 가능한 폰트를 찾지 못했습니다 (' + role + ')');
  }
  if (!F.medium) F.medium = F.bold;
}

function mkText(chars, opts) {
  opts = opts || {};
  var t = figma.createText();
  t.fontName = opts.font || F.regular;
  t.characters = chars;
  t.fontSize = opts.size || 24;
  t.fills = solid(opts.color || P.textHi, opts.opacity);
  t.lineHeight = opts.lineHeight
    ? { value: opts.lineHeight, unit: 'PIXELS' }
    : { unit: 'AUTO' };
  if (opts.letterSpacing !== undefined) {
    t.letterSpacing = { value: opts.letterSpacing, unit: 'PERCENT' };
  }
  if (opts.align) t.textAlignHorizontal = opts.align;
  if (opts.name) t.name = opts.name;
  if (opts.width) {
    t.textAutoResize = 'HEIGHT';
    t.resize(opts.width, t.height);
  } else {
    t.textAutoResize = 'WIDTH_AND_HEIGHT';
  }
  return t;
}

function mkFrame(name, opts) {
  opts = opts || {};
  var f = figma.createFrame();
  f.name = name;
  f.fills = opts.fill ? solid(opts.fill, opts.fillOpacity) : [];
  f.cornerRadius = opts.radius || 0;
  f.clipsContent = opts.clip === undefined ? true : opts.clip;

  if (opts.stroke) {
    f.strokes = solid(opts.stroke, opts.strokeOpacity);
    f.strokeWeight = opts.strokeWeight || 2;
    f.strokeAlign = 'INSIDE';
  } else {
    f.strokes = [];
  }

  if (opts.layout) {
    f.layoutMode = opts.layout;
    f.itemSpacing = opts.spacing || 0;
    var p = opts.padding || [0, 0, 0, 0]; // top, right, bottom, left
    f.paddingTop = p[0]; f.paddingRight = p[1];
    f.paddingBottom = p[2]; f.paddingLeft = p[3];
    if (opts.primaryAlign) f.primaryAxisAlignItems = opts.primaryAlign;
    if (opts.counterAlign) f.counterAxisAlignItems = opts.counterAlign;

    // 축별 의도한 sizing mode. width/height를 지정한 축만 FIXED로 둔다.
    var wAxis = opts.layout === 'HORIZONTAL' ? 'primary' : 'counter';
    var hAxis = opts.layout === 'HORIZONTAL' ? 'counter' : 'primary';
    var want = { primary: 'AUTO', counter: 'AUTO' };
    if (opts.width !== undefined) want[wAxis] = 'FIXED';
    if (opts.height !== undefined) want[hAxis] = 'FIXED';

    f.primaryAxisSizingMode = want.primary;
    f.counterAxisSizingMode = want.counter;

    if (opts.width !== undefined || opts.height !== undefined) {
      f.resize(
        opts.width === undefined ? f.width : opts.width,
        opts.height === undefined ? f.height : opts.height
      );
      // resize()는 auto-layout 프레임의 sizing mode를 FIXED로 바꿀 수 있으므로
      // 의도한 값으로 되돌린다. (지정하지 않은 축은 계속 내용에 맞춰 hug)
      f.primaryAxisSizingMode = want.primary;
      f.counterAxisSizingMode = want.counter;
    }
  } else {
    f.layoutMode = 'NONE';
    if (opts.width !== undefined || opts.height !== undefined) {
      f.resize(
        opts.width === undefined ? f.width : opts.width,
        opts.height === undefined ? f.height : opts.height
      );
    }
  }
  return f;
}

function mkRect(name, x, y, w, h, hex, radius, opacity) {
  var r = figma.createRectangle();
  r.name = name;
  r.x = x; r.y = y;
  r.resize(Math.max(w, 0.01), Math.max(h, 0.01));
  r.fills = solid(hex, opacity);
  r.strokes = [];
  if (radius) r.cornerRadius = radius;
  return r;
}

function mkBadge(label, hex) {
  var badge = mkFrame('Badge', {
    layout: 'HORIZONTAL', spacing: 10, padding: [10, 20, 10, 20],
    radius: 999, fill: hex, fillOpacity: 0.14,
    stroke: hex, strokeOpacity: 0.45, strokeWeight: 2,
    counterAlign: 'CENTER'
  });
  var dot = figma.createEllipse();
  dot.name = 'Dot';
  dot.resize(14, 14);
  dot.fills = solid(hex);
  dot.strokes = [];
  badge.appendChild(dot);
  badge.appendChild(mkText(label, { name: 'BadgeLabel', font: F.bold, size: 22, color: hex }));
  return badge;
}

function daysBetween(a, b) {
  var MS = 24 * 60 * 60 * 1000;
  return Math.round((new Date(b + 'T00:00:00').getTime() - new Date(a + 'T00:00:00').getTime()) / MS);
}

// ----------------------------------------------------------------------------
// 3. 파일 스타일 등록 (컬러 / 텍스트)
// ----------------------------------------------------------------------------

function createStyles() {
  var colorMap = [
    ['TnD/배경', P.bg], ['TnD/패널', P.panel], ['TnD/패널-대체', P.panelAlt],
    ['TnD/테두리', P.stroke], ['TnD/트랙', P.track],
    ['TnD/텍스트-강조', P.textHi], ['TnD/텍스트-보통', P.textMid], ['TnD/텍스트-약함', P.textLow],
    ['TnD/포인트-앰버', P.accent], ['TnD/상태-완료', P.done],
    ['TnD/상태-부분', P.partial], ['TnD/상태-보완', P.weak], ['TnD/상태-정보', P.info]
  ];
  for (var i = 0; i < colorMap.length; i++) {
    var s = figma.createPaintStyle();
    s.name = colorMap[i][0];
    s.paints = solid(colorMap[i][1]);
  }

  var textMap = [
    ['TnD/디스플레이', F.bold, 104, -2],
    ['TnD/섹션 제목', F.bold, 46, -1],
    ['TnD/카드 제목', F.bold, 40, -1],
    ['TnD/수치', F.bold, 60, -2],
    ['TnD/본문', F.regular, 24, 0],
    ['TnD/캡션', F.regular, 21, 0]
  ];
  for (var k = 0; k < textMap.length; k++) {
    var ts = figma.createTextStyle();
    ts.name = textMap[k][0];
    ts.fontName = textMap[k][1];
    ts.fontSize = textMap[k][2];
    ts.letterSpacing = { value: textMap[k][3], unit: 'PERCENT' };
  }
}

// ----------------------------------------------------------------------------
// 4. 섹션 빌더
// ----------------------------------------------------------------------------

function sectionHeader(label, note) {
  var row = mkFrame('섹션 헤더 · ' + label, {
    layout: 'HORIZONTAL', spacing: 24, width: INNER, counterAlign: 'CENTER'
  });
  var bar = mkRect('Accent', 0, 0, 10, 46, P.accent, 5);
  row.appendChild(bar);
  row.appendChild(mkText(label, { font: F.bold, size: 46, color: P.textHi, letterSpacing: -1 }));
  if (note) {
    row.appendChild(mkText(note, { font: F.regular, size: 24, color: P.textLow }));
  }
  return row;
}

function buildHeader(overallPct) {
  var header = mkFrame('헤더', {
    layout: 'HORIZONTAL', width: INNER, spacing: 80,
    primaryAlign: 'SPACE_BETWEEN', counterAlign: 'MAX'
  });

  var left = mkFrame('타이틀', { layout: 'VERTICAL', spacing: 16 });
  left.appendChild(mkText(META.projectName, {
    font: F.bold, size: 34, color: P.accent, letterSpacing: 14
  }));
  left.appendChild(mkText(META.title, {
    font: F.bold, size: 104, color: P.textHi, letterSpacing: -2
  }));
  left.appendChild(mkText('“' + META.concept + '”', {
    font: F.medium, size: 34, color: P.textMid
  }));
  left.appendChild(mkText(META.genre, {
    font: F.regular, size: 24, color: P.textLow
  }));
  header.appendChild(left);

  var right = mkFrame('진척도', {
    layout: 'VERTICAL', spacing: 14, padding: [36, 44, 36, 44],
    radius: 28, fill: P.panel, stroke: P.stroke, counterAlign: 'MAX', width: 900
  });
  right.appendChild(mkText(META.asOf, { font: F.medium, size: 26, color: P.accent, align: 'RIGHT' }));
  right.appendChild(mkText(META.team, { font: F.regular, size: 24, color: P.textMid, align: 'RIGHT' }));

  var pctRow = mkFrame('전체 진척', {
    layout: 'HORIZONTAL', spacing: 20, counterAlign: 'CENTER', width: 812,
    primaryAlign: 'SPACE_BETWEEN'
  });
  pctRow.appendChild(mkText('전체 구현 진척도', { font: F.medium, size: 30, color: P.textMid }));
  pctRow.appendChild(mkText(overallPct + '%', { font: F.bold, size: 84, color: P.accent, letterSpacing: -3 }));
  right.appendChild(pctRow);

  var track = mkFrame('진척 트랙', { width: 812, height: 18, radius: 9, fill: P.track });
  track.appendChild(mkRect('Fill', 0, 0, 812 * overallPct / 100, 18, P.accent, 9));
  right.appendChild(track);
  right.appendChild(mkText('13개 시스템 완성도 평균 · 코어 루프 엔드투엔드 동작', {
    font: F.regular, size: 21, color: P.textLow, align: 'RIGHT'
  }));

  header.appendChild(right);
  return header;
}

function buildKpiRow() {
  var row = mkFrame('핵심 지표', {
    layout: 'HORIZONTAL', spacing: CARD_GAP, width: INNER
  });
  for (var i = 0; i < KPIS.length; i++) {
    var k = KPIS[i];
    var card = mkFrame('지표 · ' + k.label, {
      layout: 'VERTICAL', spacing: 8, padding: [32, 32, 32, 32],
      radius: 20, fill: P.panel, stroke: P.stroke
    });
    card.appendChild(mkText(k.value, { font: F.bold, size: 62, color: P.textHi, letterSpacing: -2 }));
    card.appendChild(mkText(k.label, { font: F.medium, size: 27, color: P.accent }));
    card.appendChild(mkText(k.sub, { font: F.regular, size: 20, color: P.textLow }));
    row.appendChild(card);
    card.layoutGrow = 1;          // 부모에 붙인 뒤에만 설정 가능
    card.layoutAlign = 'STRETCH'; // 카드 높이를 행에 맞춤
  }
  return row;
}

function buildTimeline() {
  var H = 380;
  var padX = 150;
  var trackW = INNER - padX * 2;
  var total = daysBetween(TIMELINE.start, TIMELINE.end);
  var xOf = function (date) { return padX + (daysBetween(TIMELINE.start, date) / total) * trackW; };

  var wrap = mkFrame('마일스톤 타임라인', {
    width: INNER, height: H, radius: 28, fill: P.panelAlt, stroke: P.stroke, clip: true
  });

  var todayX = xOf(TIMELINE.today);

  // 오늘 기준선
  wrap.appendChild(mkRect('오늘 기준선', todayX - 1.5, 74, 3, 258, P.accent, 2, 0.55));

  // 구간
  for (var i = 0; i < TIMELINE.phases.length; i++) {
    var ph = TIMELINE.phases[i];
    var x0 = xOf(ph.from), x1 = xOf(ph.to);
    var isCurrent = ph.state === 'current';
    var segColor = isCurrent ? P.accent : P.track;
    var seg = mkRect('구간 · ' + ph.label, x0 + 3, 118, (x1 - x0) - 6, 52, segColor, 14,
      isCurrent ? 0.9 : 1);
    wrap.appendChild(seg);

    var lbl = mkText(ph.label, {
      font: F.bold, size: 30,
      color: isCurrent ? P.bg : (ph.state === 'next' ? P.textMid : P.textLow),
      align: 'CENTER'
    });
    lbl.name = '구간 라벨 · ' + ph.label;
    lbl.textAutoResize = 'HEIGHT';
    lbl.resize((x1 - x0) - 6, lbl.height);
    lbl.x = x0 + 3;
    lbl.y = 118 + (52 - lbl.height) / 2;
    wrap.appendChild(lbl);

    var range = mkText(ph.from.slice(5).replace('-', '/') + ' – ' + ph.to.slice(5).replace('-', '/'), {
      font: F.regular, size: 21, color: P.textLow, align: 'CENTER'
    });
    range.name = '구간 기간 · ' + ph.label;
    range.textAutoResize = 'HEIGHT';
    range.resize((x1 - x0) - 6, range.height);
    range.x = x0 + 3;
    range.y = 82;
    wrap.appendChild(range);
  }

  // 베이스라인 + 진행
  wrap.appendChild(mkRect('베이스라인', padX, 206, trackW, 10, P.track, 5));
  wrap.appendChild(mkRect('진행', padX, 206, todayX - padX, 10, P.accent, 5));

  // 마일스톤 마커
  for (var m = 0; m < TIMELINE.markers.length; m++) {
    var mk = TIMELINE.markers[m];
    var mx = xOf(mk.at);
    var ring = figma.createEllipse();
    ring.name = '마커 · ' + mk.label;
    ring.resize(26, 26);
    ring.x = mx - 13; ring.y = 198;
    ring.fills = solid(P.panelAlt);
    ring.strokes = solid(mk.at === '2026-07-31' ? P.accent : P.textLow);
    ring.strokeWeight = 4;
    wrap.appendChild(ring);

    var dt = mkText(mk.date, { font: F.bold, size: 26, color: P.textHi, align: 'CENTER' });
    dt.textAutoResize = 'HEIGHT'; dt.resize(300, dt.height);
    dt.x = mx - 150; dt.y = 240;
    wrap.appendChild(dt);

    var nm = mkText(mk.label, { font: F.regular, size: 22, color: P.textLow, align: 'CENTER' });
    nm.textAutoResize = 'HEIGHT'; nm.resize(300, nm.height);
    nm.x = mx - 150; nm.y = 274;
    wrap.appendChild(nm);
  }

  // 오늘 칩
  var chip = mkFrame('오늘', {
    layout: 'HORIZONTAL', spacing: 10, padding: [12, 22, 12, 22],
    radius: 999, fill: P.accent, counterAlign: 'CENTER'
  });
  chip.appendChild(mkText('오늘 · 7 / 29', { font: F.bold, size: 26, color: P.bg }));
  wrap.appendChild(chip);
  chip.x = Math.min(Math.max(todayX - chip.width / 2, 12), INNER - chip.width - 12);
  chip.y = 26;

  // 프로토타입 진행률
  var protoDone = daysBetween('2026-07-08', TIMELINE.today);
  var protoTotal = daysBetween('2026-07-08', '2026-07-31');
  var note = mkText(
    '프로토타입 단계 ' + protoDone + ' / ' + protoTotal + '일 경과 (' +
    Math.round(protoDone / protoTotal * 100) + '%) · 마감까지 ' + (protoTotal - protoDone) + '일',
    { font: F.medium, size: 24, color: P.accent }
  );
  note.x = padX; note.y = 330;
  wrap.appendChild(note);

  return wrap;
}

function buildCommitChart() {
  var H = 440;
  var padX = 150, padTop = 96, padBottom = 118;
  var plotW = INNER - padX * 2;
  var plotH = H - padTop - padBottom;
  var gap = 16;
  var barW = (plotW - gap * (COMMITS.length - 1)) / COMMITS.length;
  var max = 0;
  for (var i = 0; i < COMMITS.length; i++) max = Math.max(max, COMMITS[i][1]);
  var baseY = padTop + plotH;

  var wrap = mkFrame('일별 커밋 활동', {
    width: INNER, height: H, radius: 28, fill: P.panelAlt, stroke: P.stroke, clip: true
  });

  var t = mkText('일별 커밋 활동 · 총 587건', { font: F.bold, size: 34, color: P.textHi });
  t.x = padX; t.y = 36;
  wrap.appendChild(t);

  var t2 = mkText('최대 89건 / 일 (7/13)', { font: F.regular, size: 22, color: P.textLow });
  t2.x = padX; t2.y = 76;
  wrap.appendChild(t2);

  wrap.appendChild(mkRect('축', padX, baseY, plotW, 2, P.stroke, 1));

  for (var b = 0; b < COMMITS.length; b++) {
    var label = COMMITS[b][0], count = COMMITS[b][1];
    var h = Math.max((count / max) * plotH, 4);
    var x = padX + b * (barW + gap);
    var isRecent = b >= COMMITS.length - 3;
    wrap.appendChild(mkRect('막대 · ' + label, x, baseY - h, barW, h,
      isRecent ? P.accent : P.info, 8, isRecent ? 1 : 0.55));

    var cv = mkText(String(count), { font: F.bold, size: 24, color: P.textMid, align: 'CENTER' });
    cv.textAutoResize = 'HEIGHT'; cv.resize(barW, cv.height);
    cv.x = x; cv.y = baseY - h - 36;
    wrap.appendChild(cv);

    var dv = mkText(label, { font: F.regular, size: 21, color: P.textLow, align: 'CENTER' });
    dv.textAutoResize = 'HEIGHT'; dv.resize(barW, dv.height);
    dv.x = x; dv.y = baseY + 16;
    wrap.appendChild(dv);
  }

  var legend = mkText('파랑 = 지난 작업일 · 앰버 = 최근 3일', {
    font: F.regular, size: 22, color: P.textLow
  });
  legend.x = padX; legend.y = baseY + 60;
  wrap.appendChild(legend);

  return wrap;
}

// 시스템 카드 마스터 컴포넌트
function buildCardComponent() {
  var card = mkFrame('시스템 카드', {
    layout: 'VERTICAL', spacing: 20, padding: [CARD_PAD, CARD_PAD, CARD_PAD, CARD_PAD],
    radius: 22, fill: P.panel, stroke: P.stroke, width: CARD_W, counterAlign: 'MIN'
  });

  card.appendChild(mkBadge(STATUS_LABEL.done, P.done));

  var title = mkText('시스템 이름', {
    name: 'CardTitle', font: F.bold, size: 40, color: P.textHi,
    letterSpacing: -1, width: CARD_INNER
  });
  card.appendChild(title);

  var pctRow = mkFrame('PctRow', {
    layout: 'HORIZONTAL', width: CARD_INNER, primaryAlign: 'SPACE_BETWEEN', counterAlign: 'CENTER'
  });
  pctRow.appendChild(mkText('구현 완성도', { name: 'PctLabel', font: F.regular, size: 23, color: P.textLow }));
  pctRow.appendChild(mkText('100%', { name: 'PctValue', font: F.bold, size: 38, color: P.done, letterSpacing: -1 }));
  card.appendChild(pctRow);

  var track = mkFrame('BarTrack', { width: CARD_INNER, height: 14, radius: 7, fill: P.track });
  track.appendChild(mkRect('BarFill', 0, 0, CARD_INNER, 14, P.done, 7));
  card.appendChild(track);

  card.appendChild(mkText('설명', {
    name: 'CardDesc', font: F.regular, size: 24, color: P.textMid,
    lineHeight: 38, width: CARD_INNER
  }));

  var divider = mkRect('Divider', 0, 0, CARD_INNER, 2, P.stroke, 1);
  card.appendChild(divider);

  card.appendChild(mkText('메타', {
    name: 'CardMeta', font: F.regular, size: 21, color: P.textLow,
    lineHeight: 32, width: CARD_INNER
  }));

  var component = figma.createComponent();
  component.name = '◆ 시스템 카드';
  component.resizeWithoutConstraints(CARD_W, card.height);
  component.layoutMode = 'VERTICAL';
  component.primaryAxisSizingMode = 'AUTO';
  component.counterAxisSizingMode = 'FIXED';
  component.itemSpacing = 20;
  component.paddingTop = CARD_PAD; component.paddingRight = CARD_PAD;
  component.paddingBottom = CARD_PAD; component.paddingLeft = CARD_PAD;
  component.fills = solid(P.panel);
  component.strokes = solid(P.stroke);
  component.strokeWeight = 2;
  component.strokeAlign = 'INSIDE';
  component.cornerRadius = 22;

  // card의 자식들을 컴포넌트로 이관
  var kids = card.children.slice();
  for (var i = 0; i < kids.length; i++) component.appendChild(kids[i]);
  card.remove();
  component.resize(CARD_W, component.height);
  component.primaryAxisSizingMode = 'AUTO';   // 높이는 내용에 맞춰 hug
  component.counterAxisSizingMode = 'FIXED';  // 너비는 CARD_W 고정
  return component;
}

function applyCardData(instance, data) {
  var color = STATUS_COLOR[data.status];

  var badgeLabel = instance.findOne(function (n) { return n.name === 'BadgeLabel'; });
  if (badgeLabel) { badgeLabel.characters = STATUS_LABEL[data.status]; badgeLabel.fills = solid(color); }

  var badge = instance.findOne(function (n) { return n.name === 'Badge'; });
  if (badge) {
    badge.fills = solid(color, 0.14);
    badge.strokes = solid(color, 0.45);
  }
  var dot = instance.findOne(function (n) { return n.name === 'Dot'; });
  if (dot) dot.fills = solid(color);

  var title = instance.findOne(function (n) { return n.name === 'CardTitle'; });
  if (title) title.characters = data.title;

  var pctValue = instance.findOne(function (n) { return n.name === 'PctValue'; });
  if (pctValue) { pctValue.characters = data.pct + '%'; pctValue.fills = solid(color); }

  var fill = instance.findOne(function (n) { return n.name === 'BarFill'; });
  if (fill) {
    fill.resize(Math.max(CARD_INNER * data.pct / 100, 8), 14);
    fill.fills = solid(color);
  }

  var desc = instance.findOne(function (n) { return n.name === 'CardDesc'; });
  if (desc) desc.characters = data.desc;

  var meta = instance.findOne(function (n) { return n.name === 'CardMeta'; });
  if (meta) meta.characters = data.meta;

  instance.name = '카드 · ' + data.title;
}

function buildSystemGrid(component) {
  var grid = mkFrame('시스템 현황 그리드', {
    layout: 'VERTICAL', spacing: CARD_GAP, width: INNER
  });
  var rowIndex = 0;
  for (var i = 0; i < SYSTEMS.length; i += CARDS_PER_ROW) {
    rowIndex++;
    var row = mkFrame('행 ' + rowIndex, {
      layout: 'HORIZONTAL', spacing: CARD_GAP, width: INNER, counterAlign: 'MIN'
    });
    for (var j = i; j < Math.min(i + CARDS_PER_ROW, SYSTEMS.length); j++) {
      var inst = component.createInstance();
      applyCardData(inst, SYSTEMS[j]);
      row.appendChild(inst);
      inst.layoutAlign = 'STRETCH';
    }
    grid.appendChild(row);
  }
  return grid;
}

function buildLegend() {
  var row = mkFrame('상태 범례', {
    layout: 'HORIZONTAL', spacing: 20, width: INNER, counterAlign: 'CENTER'
  });
  var items = [
    ['done', '구현 완료 — 기획 요건 충족, 플레이 검증됨'],
    ['partial', '부분 구현 — 프레임 완성, 데이터·효과 잔여'],
    ['weak', '보완 필요 — 커버리지 부족']
  ];
  for (var i = 0; i < items.length; i++) {
    var chip = mkFrame('범례 ' + i, {
      layout: 'HORIZONTAL', spacing: 12, padding: [14, 24, 14, 24],
      radius: 999, fill: P.panel, stroke: P.stroke, counterAlign: 'CENTER'
    });
    var e = figma.createEllipse();
    e.resize(16, 16);
    e.fills = solid(STATUS_COLOR[items[i][0]]);
    e.strokes = [];
    chip.appendChild(e);
    chip.appendChild(mkText(items[i][1], { font: F.regular, size: 23, color: P.textMid }));
    row.appendChild(chip);
  }
  return row;
}

function buildFooter() {
  var f = mkFrame('푸터', {
    layout: 'HORIZONTAL', width: INNER, primaryAlign: 'SPACE_BETWEEN', counterAlign: 'CENTER',
    padding: [28, 0, 0, 0]
  });
  f.appendChild(mkText('Tower and Dragons · 진행 현황 보드', {
    font: F.medium, size: 24, color: P.textLow
  }));
  f.appendChild(mkText('수치는 2026-07-29 master 브랜치 실측 기준', {
    font: F.regular, size: 22, color: P.textLow
  }));
  return f;
}

// ----------------------------------------------------------------------------
// 5. 메인
// ----------------------------------------------------------------------------

async function main() {
  await resolveFonts();

  var overall = 0;
  for (var i = 0; i < SYSTEMS.length; i++) overall += SYSTEMS[i].pct;
  overall = Math.round(overall / SYSTEMS.length);

  createStyles();

  var root = mkFrame('▣ TnD 진행현황 보드', {
    layout: 'VERTICAL', spacing: 64,
    padding: [PAD, PAD, PAD, PAD],
    fill: P.bg, radius: 0, width: BOARD_W, clip: true
  });

  root.appendChild(buildHeader(overall));
  root.appendChild(buildKpiRow());

  root.appendChild(sectionHeader('마일스톤', '프로토타입 → 알파 → 베타 → 최종 제출'));
  root.appendChild(buildTimeline());
  root.appendChild(buildCommitChart());

  root.appendChild(sectionHeader('시스템별 구현 현황', SYSTEMS.length + '개 시스템 · 완성도 내림차순'));
  root.appendChild(buildLegend());

  var component = buildCardComponent();
  root.appendChild(buildSystemGrid(component));
  root.appendChild(buildFooter());

  figma.currentPage.appendChild(root);
  root.x = 0; root.y = 0;

  // 마스터 컴포넌트는 보드 왼쪽에 따로 배치
  var holder = mkFrame('🧩 컴포넌트', {
    layout: 'VERTICAL', spacing: 24, padding: [48, 48, 48, 48],
    fill: P.bg, radius: 24, stroke: P.stroke, width: CARD_W + 96
  });
  holder.appendChild(mkText('시스템 카드 마스터', { font: F.bold, size: 32, color: P.textHi }));
  holder.appendChild(mkText('이 컴포넌트를 수정하면 보드의 카드 13개가 모두 함께 변경됩니다.', {
    font: F.regular, size: 21, color: P.textLow, lineHeight: 32, width: CARD_W
  }));
  holder.appendChild(component);
  figma.currentPage.appendChild(holder);
  holder.x = -(CARD_W + 96) - 200;
  holder.y = 0;

  figma.currentPage.selection = [root];
  figma.viewport.scrollAndZoomIntoView([root]);
  figma.notify('진행현황 보드 생성 완료 — 전체 진척도 ' + overall + '%');
}

main()
  .then(function () { figma.closePlugin(); })
  .catch(function (err) {
    console.error(err);
    figma.closePlugin('보드 생성 실패: ' + (err && err.message ? err.message : String(err)));
  });
