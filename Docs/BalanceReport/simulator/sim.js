/* Tower and Dragons — 밸런스 시뮬레이터 코어
 *
 * 게임 코드에서 확인한 메커닉만 구현한다. 각 규칙 옆에 근거 파일을 남긴다.
 *  - 타워 발사 간격 = Attack.Interval / 충원율 / 공속배율      (TowerAttack.cs:187)
 *  - 몬스터는 사거리 안에 타워가 들어오면 "멈춰서" 때린다      (MonsterAttack.SetCurrentTarget → _movement.Stop())
 *  - 타워는 단일 대상을 잡고 죽거나 벗어날 때까지 유지          (TowerAttack.IsCurrentTargetValid)
 *  - 성에 도달한 몬스터는 SetFinalTarget으로 성을 때린다        (BaseMonster.HandleArrivedAtCastle)
 *  - 폭탄병은 자동공격이 꺼져 있고(SelfDestructBehavior.Initialize),
 *    폭발은 IMonsterTarget(타워/방벽)만 때린다. Castle은 IAttackTarget이라 폭발에 안 맞는다.
 *  - 속성 규칙 0=Normal 1=OnlyMatching 2=ImmuneToMatching 3=AllImmune (MonsterData.AcceptsElement)
 *  - 타워 targetMovementFilter 0 = All (TargetMovementFilter.cs)
 *
 * 추정 파라미터는 assumptions에만 둔다(레인 길이, 방어선 깊이). 그 외는 전부 에셋 값이다.
 */
(function (root) {
  'use strict';

  var DT = 0.1;                 // 시뮬 틱(초)
  var MAX_NIGHT_SECONDS = 900;  // 15분. 이 안에 못 끝나면 교착으로 본다.
  var PORTALS = ['North', 'East', 'South', 'West'];

  // DragonType: Ice0 Fire1 Time2 Stone3 Life4 (RunData.cs)
  var ELEMENT_OF_TOWER = {
    TD_FireTower: 1, TD_IceTower: 0, TD_StoneTower: 3, TD_TimeTower: 2,
    BD_Ice: 0, BD_Fire: 1, BD_Time: 2, BD_Stone: 3, BD_Life: 4
  };

  function acceptsElement(rule, monsterElement, attackElement) {
    if (rule === 0) return true;
    if (rule === 1) return attackElement !== null && attackElement === monsterElement;
    if (rule === 2) return attackElement === null || attackElement !== monsterElement;
    if (rule === 3) return attackElement === null;
    return true;
  }

  /* ---------------- 점령 페널티 ---------------- */

  // 소유 청크 목록 → 포탈별/몬스터별 강화 스냅샷.
  // ConquestManager.ApplyProfile(chunk.DominantTerrain) → WaveRoutePlanner.GetProfiles(portal.TerrainType)
  function buildEnhancement(data, ownedChunkKeys) {
    var byPortal = {};
    PORTALS.forEach(function (p) { byPortal[p] = {}; });
    var terrainToPortal = {};
    Object.keys(data.portalTerrain).forEach(function (p) {
      terrainToPortal[data.portalTerrain[p]] = p;
    });

    ownedChunkKeys.forEach(function (key) {
      var ch = data.chunkByKey[key];
      if (!ch || !ch.profile) return;
      var portal = terrainToPortal[ch.terrain];
      if (!portal) return;
      var prof = data.profiles[ch.profile];
      if (!prof) return;
      prof.rules.forEach(function (r) {
        var slot = byPortal[portal][r.target] || (byPortal[portal][r.target] = {
          spawn: 0, hp: [0, 1], shield: [0, 1], atk: [0, 1], speed: [0, 1], iv: [0, 1]
        });
        slot.spawn += r.spawn;
        ['hp', 'shield', 'atk', 'speed', 'iv'].forEach(function (k) {
          slot[k][0] += r[k][0];
          slot[k][1] *= (1 + r[k][1]);     // ResolvedEnemyStatModifier.Accumulate
        });
      });
    });
    return byPortal;
  }

  function applyMod(mod, base) { return mod ? (base + mod[0]) * mod[1] : base; }

  /* ---------------- 야간 전투 ---------------- */

  function makeMonster(data, md, portal, lane, enh, spawnTime, id) {
    var e = enh && enh[md.name];
    var atk = md.attack || { range: 0, interval: 1, damage: 0, area: 0, effects: [] };
    return {
      id: id, name: md.name, data: md, portal: portal, lane: lane,
      pos: 0, spawnAt: spawnTime, alive: false, done: false,
      hp: applyMod(e && e.hp, md.hp),
      maxHp: applyMod(e && e.hp, md.hp),
      shield: md.hasShield ? applyMod(e && e.shield, md.shield) : 0,
      baseSpeed: applyMod(e && e.speed, md.speed),
      damage: applyMod(e && e.atk, atk.damage),
      interval: atk.interval || 1,
      range: atk.range || 0,
      blastArea: atk.area || 0,
      isAir: md.movementType === 1,
      ccImmune: !!md.ccImmune,
      rule: md.elementRule, element: md.element,
      behaviors: md.behaviors || [],
      hasOwnShield: !!md.hasShield,
      grantedShield: 0,
      slowUntil: 0, slowMul: 1, frozenUntil: 0,
      dots: [],                 // {dps, until}
      nextAttack: 0, target: null,
      fuse: -1,                 // 폭탄병 도화선
      timers: {},               // 오라 종류별 다음 발동 시각
      granted: {}, protecting: {},   // 보호막 오라 부여 이력(대상당 1회)
      atCastle: false
    };
  }

  // 타워는 레인이 아니라 "포탈 접근로"에 속한다. 위치는 성으로부터의 거리(dist)로 잡는다.
  // 한 포탈의 루트 2개는 성 앞에서 합류하므로, 플레이어가 길목에 세운 타워는 두 루트를 함께 막는다.
  // 대신 두 루트의 몬스터가 같은 타워를 놓고 경쟁하므로 타겟 경합은 그대로 살아 있다.
  function makeTower(data, key, lane, pos, staffing, auraSpeed, research, terrainAtkSpeedCut) {
    var td = data.towers[key] || data.babyDragons[key];
    var atk = td.attack;
    var isBaby = !!data.babyDragons[key];
    var dmgMul = 1 + (research.towerDamage || 0);
    var spdMul = (1 + (research.towerFireRate || 0)) * auraSpeed * (1 - (terrainAtkSpeedCut || 0));
    var rngMul = 1 + (research.towerRange || 0);
    var base = 0, dots = [], slow = null, cond = null, freezeStacks = null;
    if (atk) {
      atk.effects.forEach(function (ef) {
        if (ef.cls === 'DamageEffectSO') base += ef.amount;
        else if (ef.cls === 'ConditionalDamage') cond = ef;
        else if (ef.status) {
          var s = ef.status;
          if (s.cls === 'DamageOverTimeStatusSO') dots.push(s);
          else if (s.cls === 'MoveSpeedStatusSO') slow = s;
          else if (s.stacks) freezeStacks = s;
        }
      });
    }
    return {
      key: key, name: td.name, lane: lane, pos: pos, isBaby: isBaby,
      maxHp: td.hp, hp: td.hp, alive: true, reviveLeft: 0,
      reviveDelay: td.revive, staffing: isBaby ? 1 : staffing,
      pop: td.pop,
      canAttack: !!atk,
      damage: base * dmgMul,
      condBonus: cond ? cond.amount * dmgMul : 0,
      condStatus: cond ? cond.requires : null,
      dots: dots, slow: slow, freezeStacks: freezeStacks,
      area: atk ? atk.area : 0,
      range: atk ? atk.range * rngMul : 0,
      baseInterval: atk ? atk.interval : Infinity,
      speedMul: spdMul,
      element: ELEMENT_OF_TOWER.hasOwnProperty(key) ? ELEMENT_OF_TOWER[key] : null,
      nextFire: 0, target: null
    };
  }

  function towerInterval(t) {
    if (!t.canAttack || t.staffing <= 0) return Infinity;
    return t.baseInterval / t.staffing / t.speedMul;
  }

  /**
   * 하루 밤을 시뮬레이션한다.
   * defense.perPortal[portal] = [{key, count, staffing}]
   * 반환: {survived, castleHp, leaked, killed, total, seconds, towersLost, log}
   */
  function simulateNight(opts) {
    var data = opts.data, day = opts.day;
    var A = opts.assumptions, defense = opts.defense, research = opts.research || {};
    var wave = data.waveByDay[day];
    if (!wave) return null;

    var enh = opts.enhancement || {};
    var L = A.laneLength, depth = A.defenseDepth;

    // --- 스폰 목록 ---
    var monsters = [], id = 0;
    var lanesOf = {};   // portal -> Set(route)
    wave.groups.forEach(function (g) {
      var md = data.monsters[g.monster];
      if (!md) return;
      var e = enh[g.portal] && enh[g.portal][g.monster];
      var iv = applyMod(e && e.iv, g.interval);
      // 스폰 보너스는 포탈 단위로 몬스터당 1회만 (WaveRoutePlanner 주석)
      var bonus = 0;
      if (e && e.spawn) {
        var mark = g.portal + '|' + g.monster;
        if (!lanesOf[mark]) { lanesOf[mark] = 1; bonus = e.spawn; }
      }
      var n = g.count + bonus;
      for (var i = 0; i < n; i++) {
        monsters.push(makeMonster(data, md, g.portal, g.portal + '#' + g.route, e,
          g.startAt + i * iv, id++));
      }
    });

    // --- 타워 배치 ---
    var activePortals = {};
    monsters.forEach(function (m) { activePortals[m.portal] = true; });
    var laneKeys = {};
    monsters.forEach(function (m) { (laneKeys[m.portal] = laneKeys[m.portal] || {})[m.lane] = true; });

    var towers = [];
    Object.keys(activePortals).forEach(function (portal) {
      var list = (defense.perPortal && defense.perPortal[portal]) || [];
      var flat = [];
      list.forEach(function (spec) {
        for (var i = 0; i < spec.count; i++) flat.push(spec);
      });
      // 성으로부터 0 ~ depth 구간에 고르게 편다(0 = 성 바로 앞).
      flat.forEach(function (spec, i) {
        var dist = flat.length === 1 ? depth / 2 : depth * (i / (flat.length - 1));
        towers.push(makeTower(data, spec.key, portal, dist, spec.staffing,
          1, research, A.terrainAtkSpeedCut || 0));
      });
    });

    // 시간 타워 오라: 반경 안 타워 공속 ×1.2 (TowerAuraDataSO)
    var timeAura = data.towers.TD_TimeTower && data.towers.TD_TimeTower.aura;
    if (timeAura) {
      towers.filter(function (t) { return t.key === 'TD_TimeTower'; }).forEach(function (src) {
        towers.forEach(function (t) {
          if (t === src || t.lane !== src.lane) return;
          if (Math.abs(t.pos - src.pos) <= timeAura.radius) {
            t.speedMul *= timeAura.atkSpeed;
          }
        });
      });
    }

    // 몬스터의 "성까지 남은 거리" — 타워 위치와 같은 축으로 비교한다.
    function distOf(m) { return L - m.pos; }
    function sameApproach(tw, m) { return tw.lane === m.portal; }

    var behaviors = data.behaviors;
    var castleHp = opts.castleHp != null ? opts.castleHp : data.economy.castleHp;
    var castleMax = castleHp;
    var t = 0, killed = 0, leaked = 0, towersLost = 0;
    var dragon = opts.dragonSkill ? data.dragonSkills[opts.dragonSkill] : null;
    var dragonNext = dragon ? 0 : Infinity;
    var dragonUses = 0;

    function damageMonster(m, amount, element) {
      if (!m.alive || m.hp <= 0) return false;
      if (!acceptsElement(m.rule, m.element, element === undefined ? null : element)) return false;
      if (m.shield > 0) {
        var absorbed = Math.min(m.shield, amount);
        m.shield -= absorbed; amount -= absorbed;
      }
      if (amount <= 0) return true;
      m.hp -= amount;
      if (m.hp <= 0) { m.alive = false; m.done = true; killed++; }
      return true;
    }

    var alive = monsters.length;
    while (t < MAX_NIGHT_SECONDS) {
      // 스폰
      for (var si = 0; si < monsters.length; si++) {
        var mm = monsters[si];
        if (!mm.done && !mm.alive && t >= mm.spawnAt) mm.alive = true;
      }

      // 어미용 액티브 (현재 체력 비례 / 고정)
      if (dragon && t >= dragonNext &&
        (dragon.usePerDay < 0 || dragonUses < dragon.usePerDay)) {
        var hit = false;
        monsters.forEach(function (m) {
          if (!m.alive) return;
          hit = true;
          if (dragon.pctCurrentHp > 0) damageMonster(m, m.hp * dragon.pctCurrentHp, null);
          else if (dragon.flat > 0) damageMonster(m, dragon.flat, null);
        });
        if (hit) { dragonUses++; dragonNext = t + dragon.cooldown; }
      }

      // 타워 부활
      towers.forEach(function (tw) {
        if (tw.alive) return;
        // Tower.cs:205 — 부활 진행도 = deltaTime * staffingRatio * reviveSpeedMultiplier
        tw.reviveLeft -= DT * Math.min(1, tw.staffing);
        if (tw.reviveLeft <= 0) { tw.alive = true; tw.hp = tw.maxHp; tw.target = null; }
      });

      // 몬스터 오라
      monsters.forEach(function (m) {
        if (!m.alive) return;
        m.behaviors.forEach(function (bn) {
          var b = behaviors[bn];
          if (!b) return;

          if (bn === 'HealAuraBehavior') {
            // HealAuraBehaviorSO: interval마다 반경 안 아군 회복
            if (t < (m.timers[bn] || 0)) return;
            m.timers[bn] = t + b.interval;
            monsters.forEach(function (o) {
              if (o.alive && o.lane === m.lane && Math.abs(o.pos - m.pos) <= b.radius)
                o.hp = Math.min(o.maxHp, o.hp + b.heal);
            });

          } else if (bn === 'ProtectionAuraBehavior') {
            // ProtectionAuraBehaviorSO: 대상당 "1회만" 부여(_grantedOnce, 재부여 금지).
            // 자체 방어막을 이미 가진 대상에겐 부여되지 않고, 범위를 벗어나면 회수한다.
            if (t < (m.timers[bn] || 0)) return;
            m.timers[bn] = t + Math.max(b.interval, DT);   // _scanInterval 0 → 매 틱
            monsters.forEach(function (o) {
              if (!o.alive || o.lane !== m.lane) return;
              var inRange = Math.abs(o.pos - m.pos) <= b.radius;
              if (inRange && !m.granted[o.id]) {
                m.granted[o.id] = true;
                if (o.hasOwnShield) return;                // GrantShield 실패 경로
                m.protecting[o.id] = true;
                o.shield += b.shield;
                o.grantedShield = (o.grantedShield || 0) + b.shield;
              } else if (!inRange && m.protecting[o.id]) {
                delete m.protecting[o.id];                 // RevokeOutOfRangeTargets
                var back = Math.min(o.shield, b.shield);
                o.shield -= back; o.grantedShield -= back;
              }
            });

          } else if (bn === 'RegeneratingShieldBehavior') {
            if (t < (m.timers[bn] || 0)) return;
            m.timers[bn] = t + b.interval;
            m.shield = Math.max(m.shield, b.shield);
          }
        });
      });

      // 몬스터 갱신
      monsters.forEach(function (m) {
        if (!m.alive) return;

        // 지속 피해
        for (var i = m.dots.length - 1; i >= 0; i--) {
          if (t > m.dots[i].until) { m.dots.splice(i, 1); continue; }
          damageMonster(m, m.dots[i].dps * DT, m.dots[i].element);
        }
        if (!m.alive) return;

        // 폭탄병/마비병 도화선
        var selfDestruct = m.behaviors.indexOf('SelfDestructBehavior') >= 0;
        if (selfDestruct && m.fuse >= 0) {
          m.fuse -= DT;
          if (m.fuse <= 0) {
            // ExecuteBlast: IMonsterTarget(타워)만 맞는다. 성은 안 맞는다.
            var b = behaviors.SelfDestructBehavior;
            var blastR = b.radius || 3;
            towers.forEach(function (tw) {
              if (!tw.alive || !sameApproach(tw, m)) return;
              if (Math.abs(tw.pos - distOf(m)) <= blastR) {
                tw.hp -= m.damage;
                if (tw.hp <= 0) { tw.alive = false; tw.reviveLeft = tw.reviveDelay; towersLost++; }
              }
            });
            m.alive = false; m.done = true; killed++;
            return;
          }
          return; // 도화선 중엔 정지
        }

        var frozen = t < m.frozenUntil;
        if (frozen) return;

        // 타겟 탐색: 사거리 안 타워가 있으면 멈춰서 공격 (MonsterAttack.SetCurrentTarget → Stop)
        var tgt = null, best = Infinity;
        var md = distOf(m);
        for (var ti = 0; ti < towers.length; ti++) {
          var tw = towers[ti];
          if (!tw.alive || !sameApproach(tw, m)) continue;
          var d = Math.abs(tw.pos - md);
          if (d <= Math.max(m.range, 0.5) && d < best) { best = d; tgt = tw; }
        }

        if (tgt) {
          if (selfDestruct) { m.fuse = behaviors.SelfDestructBehavior.fuse; return; }
          if (t >= m.nextAttack) {
            m.nextAttack = t + m.interval;
            tgt.hp -= m.damage;
            if (tgt.hp <= 0) { tgt.alive = false; tgt.reviveLeft = tgt.reviveDelay; towersLost++; }
          }
          return; // 멈춰서 때린다
        }

        if (m.pos >= L) {
          if (!m.atCastle) { m.atCastle = true; leaked++; }
          if (selfDestruct) {
            // 성에서 자폭 — 폭발은 타워만 때리므로 성엔 0. 그대로 소멸.
            m.alive = false; m.done = true; return;
          }
          if (t >= m.nextAttack) { m.nextAttack = t + m.interval; castleHp -= m.damage; }
          return;
        }

        var spd = m.baseSpeed * (t < m.slowUntil ? m.slowMul : 1);
        m.pos += spd * DT;
      });

      if (castleHp <= 0) {
        return result(false, 0);
      }

      // 타워 사격
      towers.forEach(function (tw) {
        if (!tw.alive || !tw.canAttack || tw.staffing <= 0) return;
        var valid = tw.target && tw.target.alive &&
          Math.abs(distOf(tw.target) - tw.pos) <= tw.range &&
          acceptsElement(tw.target.rule, tw.target.element, tw.element);
        if (!valid) {
          tw.target = null;
          var bestD = Infinity;
          for (var mi = 0; mi < monsters.length; mi++) {
            var m = monsters[mi];
            if (!m.alive || !sameApproach(tw, m)) continue;
            if (!acceptsElement(m.rule, m.element, tw.element)) continue;
            var d = Math.abs(distOf(m) - tw.pos);
            if (d <= tw.range && d < bestD) { bestD = d; tw.target = m; }
          }
        }
        if (!tw.target) return;
        var iv = towerInterval(tw);
        if (t < tw.nextFire) return;
        tw.nextFire = t + iv;

        var victims = [tw.target];
        if (tw.area > 0) {
          var center = distOf(tw.target);
          victims = monsters.filter(function (m) {
            return m.alive && sameApproach(tw, m) && Math.abs(distOf(m) - center) <= tw.area;
          });
        }
        victims.forEach(function (v) {
          var dmg = tw.damage;
          if (tw.condStatus && v.dots.some(function (d) { return d.id === tw.condStatus; })) {
            dmg += tw.condBonus;
          }
          damageMonster(v, dmg, tw.element);
          if (!v.alive) return;
          tw.dots.forEach(function (s) {
            var ex = v.dots.find(function (d) { return d.id === s.statusId || d.name === s.name; });
            var entry = { id: s.statusId || s.name, name: s.name, dps: s.dps,
              until: t + s.duration, element: tw.element };
            if (ex) { ex.until = entry.until; } else { v.dots.push(entry); }
          });
          if (tw.slow && !v.ccImmune) {
            v.slowUntil = t + tw.slow.duration;
            v.slowMul = tw.slow.speedMul;
          }
          if (tw.freezeStacks && !v.ccImmune) {
            v.freezeCount = (v.freezeCount || 0) + 1;
            if (v.freezeCount >= tw.freezeStacks.stacks) {
              v.freezeCount = 0;
              v.frozenUntil = t + 3;   // TS_BD_IceFreeze duration
            }
          }
        });
      });

      alive = 0;
      for (var k = 0; k < monsters.length; k++) if (monsters[k].alive || !monsters[k].done) alive++;
      if (alive === 0) return result(true, castleHp);

      t += DT;
    }
    return result(castleHp > 0, castleHp, true);

    function result(survived, hp, stalled) {
      // 적 화력 = 그 밤에 스폰되는 전 개체의 공격 DPS 합(점령 강화 반영 후).
      // 플레이어 화력 = 배치 인구가 실제로 내는 타워 DPS 합(충원율·연구·오라 반영 후).
      // 둘 다 "비율"이라 서로 직접 비교된다.
      var enemyDps = 0;
      monsters.forEach(function (m) {
        if (m.interval > 0) enemyDps += m.damage / m.interval;
      });
      var playerDps = 0, capacity = 0, staffed = 0;
      towers.forEach(function (tw) {
        var iv = towerInterval(tw);
        if (isFinite(iv) && iv > 0) playerDps += tw.damage / iv;
        capacity += tw.pop; staffed += tw.pop * Math.min(1, tw.staffing);
      });
      return {
        survived: survived, castleHp: Math.max(0, Math.round(hp)), castleMax: castleMax,
        leaked: leaked, killed: killed, total: monsters.length,
        seconds: Math.round(t), towersLost: towersLost, stalled: !!stalled,
        towerCount: towers.length,
        enemyDps: enemyDps, playerDps: playerDps,
        staffing: capacity > 0 ? staffed / capacity : 0
      };
    }
  }

  /* ---------------- 낮: 경제 / 인구 정책 ---------------- */

  // 정책 슬라이더 → 하루치 인구·자원 배분. 결과는 그대로 화면에 보여준다(블랙박스 금지).
  function planDay(data, st, policy) {
    var econ = data.economy;
    var farmYield = policy.farmYield;          // 농장 1채 만충 산출
    var woodYield = policy.woodYield;
    var stoneYield = policy.stoneYield;
    var farm = data.production.RPD_FarmField, log = data.production.RPD_LoggingCamp,
      quarry = data.production.RPD_Quarry;
    var popCut = st.prodManpower ? 1 : 0;
    var farmCap = Math.max(1, farm.pop - popCut),
      logCap = Math.max(1, log.pop - popCut),
      quarryCap = Math.max(1, quarry.pop - popCut);

    var pop = st.maxPop;

    // 1) 식량은 선택이 아니다. 유지비를 policy.foodMargin 배로 덮을 만큼 농장을 돌린다.
    var needFood = pop * econ.foodUpkeepPerPop * policy.foodMargin;
    var farms = Math.max(1, Math.ceil(needFood / Math.max(1, farmYield)));
    var farmPop = Math.min(pop, farms * farmCap);
    var farmStaff = farms > 0 ? farmPop / (farms * farmCap) : 0;
    var rest = pop - farmPop;

    // 2) 점령 원정에 묶인 인구는 이미 나가 있다.
    var conquestPop = Math.min(rest, st.conquestLocked);
    rest -= conquestPop;

    // 3) 원정에 쓸 인구 총량은 policy.conquestReserve로 묶는다. 이미 나가 있는 만큼은
    //    위에서 뺐으므로, 여기서는 "추가로 더 내보낼 수 있는 몫"만 떼어 둔다.
    var reserve = Math.max(0, Math.min(rest, (policy.conquestReserve || 0) - conquestPop));
    rest -= reserve;

    // 4) 남은 인구를 방어(타워) vs 후방(벌목·채석·연구)으로 가른다 — 핵심 다이얼.
    var towerPop = Math.max(0, Math.round(rest * policy.towerShare));
    var budget = rest - towerPop;

    // 4) 후방 예산 안에서 벌목 → 채석 → 연구소 순으로 채운다. 정원 미달이면 그만큼만 가동.
    function fill(count, cap) {
      var assigned = 0, built = 0;
      for (var i = 0; i < count && budget >= 1; i++) {
        var take = Math.min(cap, budget);
        assigned += take; budget -= take; built++;
      }
      return { assigned: assigned, built: built, staff: built ? assigned / (built * cap) : 0 };
    }
    var lg = fill(policy.minLogging, logCap);
    var qr = fill(policy.minQuarry, quarryCap);
    var lb = fill(policy.labs, data.researchBalance.labPop);

    // 남은 후방 예산은 놀리지 않고 타워로 돌린다(어차피 식량은 먹는다).
    towerPop += budget; budget = 0;

    return {
      farms: farms, farmPop: farmPop, farmStaff: farmStaff,
      logs: lg.built, quarries: qr.built, labs: lb.built,
      prodPop: lg.assigned + qr.assigned, labPop: lb.assigned,
      conquestPop: conquestPop, conquestReserve: reserve, towerPop: towerPop, idle: 0,
      // 생산량 = 시설 수 × 만충 산출 × 충원율 (ResourceProductionData.CalculateYield)
      food: Math.round(farms * farmYield * farmStaff),
      wood: Math.round(lg.built * woodYield * lg.staff),
      stone: Math.round(qr.built * stoneYield * qr.staff),
      foodUpkeep: pop * econ.foodUpkeepPerPop,
      rp: lb.assigned * data.researchBalance.rpPerPop
    };
  }

  /* ---------------- 28일 전체 ---------------- */

  function runCampaign(data, cfg) {
    var policy = cfg.policy, A = cfg.assumptions, research = {};
    var st = {
      maxPop: data.economy.startPop,
      food: data.economy.startFood, wood: data.economy.startWood, stone: data.economy.startStone,
      rp: 0, conquestLocked: 0, owned: [], research: {}, castleHp: data.economy.castleHp,
      towers: {}   // key -> count
    };
    // 시작 타워는 없음. 매일 목재가 되는 만큼 정책 우선순위대로 짓는다.
    var order = cfg.towerPriority;
    var days = [], conquestQueue = conquestPlan(data, policy);
    var qi = 0, pending = [];

    for (var day = 1; day <= 28; day++) {
      var plan = planDay(data, st, policy);

      // 자원 정산
      st.food += plan.food - plan.foodUpkeep;
      st.wood += plan.wood;
      st.stone += plan.stone;
      st.rp += plan.rp;
      if (st.food < 0) st.food = 0;   // 기아 처리는 단순화(경고로 표시)
      var starving = plan.food < plan.foodUpkeep;

      // 연구 (RP 순서대로 자동 취득)
      cfg.researchOrder.forEach(function (id) {
        if (st.research[id]) return;
        var node = data.researchById[id];
        if (!node || st.rp < node.rp) return;
        st.rp -= node.rp; st.research[id] = true;
      });
      // 연구 효과는 하드코딩하지 않고 취득한 노드의 효과 에셋에서 직접 읽는다 —
      // 툴에서 RE_* 비율을 바꾸면 그대로 반영된다.
      research = { towerDamage: 0, towerFireRate: 0, towerRange: 0,
        towerManpower: false, prodManpower: false, castleRegen: 0 };
      Object.keys(st.research).forEach(function (id) {
        if (st.research[id] !== true) return;
        var node = data.researchById[id];
        if (!node) return;
        node.effects.forEach(function (e) {
          switch (e.cls) {
            case 'TowerDamageMultiplierEffectSO': research.towerDamage += e.ratio; break;
            case 'TowerAttackSpeedMultiplierEffectSO': research.towerFireRate += e.ratio; break;
            case 'TowerRangeMultiplierEffectSO': research.towerRange += e.ratio; break;
            case 'CastleRegenEffectSO': research.castleRegen += e.heal; break;
            case 'PopulationCapacityEffectSO':
              // _targetAssignmentType 1=타워 2=생산. 이름으로 갈라도 결과는 같다.
              if (e.name.indexOf('Tower') >= 0) research.towerManpower = true;
              else if (e.name.indexOf('Production') >= 0) research.prodManpower = true;
              break;
          }
        });
      });
      st.prodManpower = research.prodManpower;

      // 점령 진행
      pending = pending.filter(function (p) {
        p.left -= 1;
        if (p.left > 0) return true;
        st.owned.push(p.key); st.maxPop += p.reward; st.conquestLocked -= p.pop;
        return false;
      });
      while (qi < conquestQueue.length && pending.length < policy.parallelConquest) {
        var c = conquestQueue[qi];
        if (st.wood < c.wood || st.stone < c.stone || st.food < c.food) break;
        if (plan.conquestReserve < c.pop) break;
        plan.conquestReserve -= c.pop;
        st.wood -= c.wood; st.stone -= c.stone; st.food -= c.food;
        st.conquestLocked += c.pop;
        pending.push({ key: c.key, left: c.days, reward: c.reward, pop: c.pop });
        qi++;
      }

      // 타워 건설: 목재/석재가 되는 만큼, 인구 배분 한도 안에서
      buildTowers(data, st, plan, order, research, policy.towerSpread);

      // 방어 편성
      var defense = assignDefense(data, st, plan, day, research);
      var enh = buildEnhancement(data, st.owned);
      var night = simulateNight({
        data: data, day: day, assumptions: A, defense: defense,
        research: research, enhancement: enh, castleHp: st.castleHp,
        dragonSkill: cfg.dragonSkill
      });

      st.castleHp = night.survived
        ? Math.min(data.economy.castleHp, night.castleHp + research.castleRegen)
        : 0;

      days.push({
        day: day, plan: plan, night: night, pop: st.maxPop, owned: st.owned.length,
        food: Math.round(st.food), wood: Math.round(st.wood), stone: Math.round(st.stone),
        rp: st.rp, starving: starving, towers: JSON.parse(JSON.stringify(st.towers)),
        research: Object.keys(st.research).filter(function (k) { return st.research[k] === true; }).length
      });
      if (!night.survived) break;
    }
    return days;

  }

  function conquestPlan(data, policy) {
    var terrOrder = policy.conquestDirection;  // 지형 우선순위 배열
    var list = data.chunks.filter(function (c) { return c.dist > 0; }).slice();
    list.sort(function (a, b) {
      var ta = terrOrder.indexOf(a.terrain), tb = terrOrder.indexOf(b.terrain);
      if (ta < 0) ta = 99; if (tb < 0) tb = 99;
      if (ta !== tb) return ta - tb;
      return a.dist - b.dist;
    });
    return list.map(function (c) {
      var base = data.conquestDuration[c.terrain] || 3;
      var days = c.dist <= 2 ? Math.max(1, base - 1) : base;   // 성 인접 보정
      return { key: c.x + ',' + c.y, wood: c.wood, stone: c.stone, food: c.food,
        pop: c.pop, reward: c.reward, days: days, terrain: c.terrain };
    });
  }

  // 타워 DPS는 충원율에 완전 선형이라(TowerAttack.cs:187) 같은 인구를 여러 타워에 얇게 펴도
  // 총 DPS가 같다. 대신 타워 총 체력과 커버리지는 늘어난다. policy.towerSpread가 그 배율이다.
  // 1.0 = 정원을 꽉 채워 운영, 3.0 = 정원의 3배수만큼 지어 33% 충원율로 운영.
  function buildTowers(data, st, plan, order, research, spread) {
    var budgetPop = plan.towerPop * Math.max(1, spread || 1);
    var used = 0;
    Object.keys(st.towers).forEach(function (k) {
      used += st.towers[k] * towerPop(data, k, research);
    });
    for (var i = 0; i < order.length; i++) {
      var key = order[i], td = data.towers[key];
      if (!td) continue;
      var guard = 0;
      while (guard++ < 200) {
        var cost = td.cost;
        var ok = Object.keys(cost).every(function (r) {
          return (r === 'Wood' ? st.wood : r === 'Stone' ? st.stone : r === 'Food' ? st.food : 0) >= cost[r];
        });
        if (!ok) break;
        if (used + towerPop(data, key, research) > budgetPop) break;
        Object.keys(cost).forEach(function (r) {
          if (r === 'Wood') st.wood -= cost[r];
          else if (r === 'Stone') st.stone -= cost[r];
          else if (r === 'Food') st.food -= cost[r];
        });
        st.towers[key] = (st.towers[key] || 0) + 1;
        used += towerPop(data, key, research);
      }
    }
  }

  function towerPop(data, key, research) {
    var td = data.towers[key] || data.babyDragons[key];
    return Math.max(1, td.pop - (research.towerManpower && !data.babyDragons[key] ? 1 : 0));
  }

  function assignDefense(data, st, plan, day, research) {
    var wave = data.waveByDay[day];
    var active = {};
    wave.groups.forEach(function (g) { active[g.portal] = (active[g.portal] || 0) + g.count; });
    var portals = Object.keys(active);
    var totalMobs = portals.reduce(function (a, p) { return a + active[p]; }, 0);

    var perPortal = {};
    portals.forEach(function (p) { perPortal[p] = []; });

    Object.keys(st.towers).forEach(function (key) {
      var n = st.towers[key];
      // 위협 비례 배분
      var alloc = portals.map(function (p) {
        return { p: p, want: n * (active[p] / totalMobs) };
      });
      alloc.sort(function (a, b) { return b.want - a.want; });
      var left = n;
      alloc.forEach(function (a, i) {
        var give = i === alloc.length - 1 ? left : Math.round(a.want);
        give = Math.min(give, left); left -= give;
        if (give > 0) perPortal[a.p].push({ key: key, count: give, staffing: 1 });
      });
    });

    // 충원율: 타워 인구 예산을 총 정원으로 나눈 값(선형이므로 균등 분배와 동치)
    var capacity = 0;
    Object.keys(st.towers).forEach(function (k) {
      capacity += st.towers[k] * towerPop(data, k, research);
    });
    var staffing = capacity > 0 ? Math.min(1, plan.towerPop / capacity) : 0;
    portals.forEach(function (p) {
      perPortal[p].forEach(function (s) { s.staffing = staffing; });
    });
    return { perPortal: perPortal, staffing: staffing };
  }

  /* ---------------- 인덱싱 ---------------- */

  function prepare(bundle) {
    var d = JSON.parse(JSON.stringify(bundle));
    d.waveByDay = {};
    d.waves.forEach(function (w) { d.waveByDay[w.day] = w; });
    d.chunkByKey = {};
    d.chunks.forEach(function (c) { d.chunkByKey[c.x + ',' + c.y] = c; });
    d.researchById = {};
    Object.keys(d.research).forEach(function (k) {
      d.researchById[d.research[k].nodeId] = d.research[k];
    });
    return d;
  }

  root.BalanceSim = {
    prepare: prepare, simulateNight: simulateNight, runCampaign: runCampaign,
    buildEnhancement: buildEnhancement, planDay: planDay, PORTALS: PORTALS,
    acceptsElement: acceptsElement, DT: DT
  };
})(typeof globalThis !== 'undefined' ? globalThis : this);
