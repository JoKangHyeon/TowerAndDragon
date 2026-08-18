# 시뮬레이터가 먹을 데이터 번들을 Unity 에셋에서 추출한다.
# 출력: bundle.json  (HTML에 그대로 인라인된다)
import os, re, json, sys, io, csv, collections
import os as _os
_HERE = _os.path.dirname(_os.path.abspath(__file__))
_REPO = _os.path.abspath(_os.path.join(_HERE, "..", "..", ".."))


ROOT = _REPO
DATA = os.path.join(ROOT, "Assets", "Data")
OUT = os.path.dirname(os.path.abspath(__file__))
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

guid2path, path2guid = {}, {}
for base, dirs, files in os.walk(DATA):
    for f in files:
        if not f.endswith(".meta"):
            continue
        p = os.path.join(base, f)
        m = re.search(r"^guid: (\w+)", open(p, encoding="utf-8", errors="replace").read(), re.M)
        if m:
            guid2path[m.group(1)] = p[:-5]
            path2guid[p[:-5]] = m.group(1)

def read(p): return open(p, encoding="utf-8", errors="replace").read()
def stem(p): return os.path.splitext(os.path.basename(p))[0]
def name_of(g):
    p = guid2path.get(g)
    return stem(p) if p else "?" + g[:8]

def num(txt, key, default=0):
    m = re.search(r"^\s*%s: (-?[\d.]+)$" % re.escape(key), txt, re.M)
    return float(m.group(1)) if m else default

def rel(p): return os.path.relpath(p, ROOT).replace("\\", "/")

# ---------- 상태이상 ----------
def parse_status(path):
    t = read(path)
    cls = re.search(r"Assembly-CSharp::(\w+)", t).group(1)
    s = dict(name=stem(path), cls=cls, duration=num(t, "_durationSeconds"))
    if cls == "DamageOverTimeStatusSO":
        s.update(dps=num(t, "_damagePerTick") / max(num(t, "_tickIntervalSeconds", 1), 0.001),
                 perTick=num(t, "_damagePerTick"), tick=num(t, "_tickIntervalSeconds", 1))
    elif cls == "MoveSpeedStatusSO":
        s.update(speedMul=num(t, "_speedMultiplier", 1))
    elif cls == "StackingStatusSO" or "_stacksToTrigger" in t:
        s.update(stacks=num(t, "_stacksToTrigger"),
                 trigger=name_of(re.search(r"_triggeredStatus: \{fileID: 11400000, guid: (\w+)", t).group(1))
                 if "_triggeredStatus" in t else None)
    return s

# ---------- 공격(AttackSO) ----------
def parse_attack(path):
    t = read(path)
    # file은 필수다. 여러 몬스터가 같은 AttackSO/DamageEffect를 공유하므로,
    # 툴이 "이 에셋을 참조하는 대상 전부"를 찾아 함께 바꿀 수 있어야 한다.
    out = dict(name=stem(path), file=rel(path),
               range=num(t, "_range"), interval=num(t, "_interval"),
               area=num(t, "_areaRadius"), damage=0.0, effects=[])
    seg = t.split("_effects:")[1] if "_effects:" in t else ""
    for g in re.findall(r"- \{fileID: 11400000, guid: (\w+), type: 2\}", seg):
        ep = guid2path.get(g)
        if not ep or not os.path.exists(ep):
            continue
        et = read(ep)
        cls = re.search(r"Assembly-CSharp::(\w+)", et).group(1)
        e = dict(name=stem(ep), cls=cls, file=rel(ep))
        if cls == "DamageEffectSO":
            e["amount"] = num(et, "_amount"); out["damage"] += e["amount"]
        elif cls == "ConditionalDamageEffectSO" or "_requiredStatusId" in et:
            e.update(cls="ConditionalDamage", amount=num(et, "_bonusAmount"),
                     requires=re.search(r"_requiredStatusId: (\S+)", et).group(1))
        elif cls == "StatusAttackEffectSO":
            sg = re.search(r"_status: \{fileID: 11400000, guid: (\w+)", et)
            if sg and guid2path.get(sg.group(1)):
                e["status"] = parse_status(guid2path[sg.group(1)])
        elif "_healAmount" in et:
            e.update(cls="TowerHeal", heal=num(et, "_healAmount"), radius=num(et, "_radius"))
        out["effects"].append(e)
    return out

def attack_of(t):
    g = re.search(r"_attack: \{fileID: 11400000, guid: (\w+)", t)
    if not g: return None
    p = guid2path.get(g.group(1))
    return parse_attack(p) if p and os.path.exists(p) else None

# ---------- 특수 행동 ----------
behaviors = {}
bdir = os.path.join(DATA, "MonsterData", "SpecialBehavior")
for f in sorted(os.listdir(bdir)):
    if not f.endswith(".asset"): continue
    t = read(os.path.join(bdir, f))
    behaviors[stem(f)] = dict(
        name=stem(f), file=rel(os.path.join(bdir, f)),
        radius=num(t, "_radius", num(t, "_blastRadius")),
        interval=num(t, "_interval", num(t, "_scanInterval")),
        heal=num(t, "_healAmount"), shield=num(t, "_shieldAmount"),
        fuse=num(t, "_fuseTime"))

# ---------- 몬스터 ----------
monsters = {}
mdir = os.path.join(DATA, "MonsterData")
for f in sorted(os.listdir(mdir)):
    if not f.endswith(".asset"): continue
    p = os.path.join(mdir, f); t = read(p)
    if "Assembly-CSharp::MonsterData" not in t: continue
    sb = []
    if "_specialBehaviors:" in t and "_specialBehaviors: []" not in t:
        seg = t.split("_specialBehaviors:")[1].split("_projectilePrefab")[0]
        sb = [name_of(g) for g in re.findall(r"- \{fileID: 11400000, guid: (\w+), type: 2\}", seg)]
    monsters[stem(f)] = dict(
        name=stem(f), file=rel(p), guid=path2guid.get(p),
        hp=num(t, "_maxHealth"), speed=num(t, "_moveSpeed"),
        shield=num(t, "_shieldAmount"), hasShield=int(num(t, "_hasShield")),
        movementType=int(num(t, "_movementType")),
        elementRule=int(num(t, "_elementRule")), element=int(num(t, "_element")),
        ccImmune=int(num(t, "_isCrowdControlImmune")),
        attack=attack_of(t), behaviors=sb)
g2m = {v["guid"]: k for k, v in monsters.items() if v["guid"]}

# ---------- 웨이브 ----------
wdir = os.path.join(DATA, "WaveData", "BalancingWave_Codex")
sched = read(os.path.join(wdir, "WaveCycleSchedule_Codex.asset"))
PORTAL = {1: "North", 2: "East", 3: "South", 4: "West"}
PORTAL_TERRAIN = {"North": "Snow", "South": "Volcano", "East": "Desert", "West": "Rock"}
waves, day = [], 0
for block in re.split(r"- _cycleNumber: ", sched)[1:]:
    cyc = int(block.split("\n")[0])
    unlock = int(re.search(r"_portalToUnlock: (-?\d+)", block).group(1))
    for wg in re.findall(r"- \{fileID: 11400000, guid: (\w+), type: 2\}", block):
        day += 1
        wp = guid2path[wg]; t = read(wp)
        groups = []
        for pb in re.split(r"- _portalDirectionId: ", t)[1:]:
            pid = int(pb.split("\n")[0])
            portalDelay = num(pb.split("_routeWaves")[0], "_startDelay")
            for rb in re.split(r"- _routeIndex: ", pb)[1:]:
                ri = int(rb.split("\n")[0])
                routeDelay = num(rb.split("_spawnGroups")[0], "_startDelay")
                for g, cnt, iv, dbg in re.findall(
                        r"_monsterData: \{fileID: 11400000, guid: (\w+), type: 2\}\s*\n"
                        r"\s*_spawnCount: (\d+)\s*\n\s*_spawnInterval: ([\d.]+)\s*\n"
                        r"\s*_delayBeforeGroup: ([\d.]+)", rb):
                    groups.append(dict(portal=PORTAL.get(pid, str(pid)), route=ri,
                                       monster=g2m.get(g, name_of(g)), count=int(cnt),
                                       interval=float(iv),
                                       startAt=portalDelay + routeDelay + float(dbg)))
        waves.append(dict(day=day, cycle=cyc, unlockPortal=PORTAL.get(unlock, str(unlock)),
                          asset=stem(wp), file=rel(wp), groups=groups))

# ---------- 타워 ----------
towers = {}
tdir = os.path.join(DATA, "TowerData")
RES = {1: "Food", 2: "Wood", 4: "Stone", 8: "FlameHeart", 16: "SnowCrystal",
       32: "TimeSand", 64: "PhilosopherStone"}
def costs(t):
    seg = t.split("_buildCost:")[1].split("_maxHealth")[0] if "_buildCost:" in t else ""
    return {RES.get(int(ty), str(ty)): int(am)
            for ty, am in re.findall(r"- Type: (\d+)\s*\n\s*Amount: (\d+)", seg)}
for f in sorted(os.listdir(tdir)):
    if not f.endswith(".asset"): continue
    p = os.path.join(tdir, f); t = read(p)
    if "TowerData" not in re.search(r"Assembly-CSharp::(\w+)", t).group(1): continue
    aura = None
    ag = re.search(r"_towerAura: \{fileID: 11400000, guid: (\w+)", t)
    if ag and guid2path.get(ag.group(1)):
        at = read(guid2path[ag.group(1)])
        aura = dict(name=name_of(ag.group(1)), file=rel(guid2path[ag.group(1)]),
                    radius=num(at, "_radius"), atkSpeed=num(at, "_attackSpeedMultiplier", 1),
                    damage=num(at, "_damageMultiplier", 1), revive=num(at, "_reviveSpeedMultiplier", 1),
                    maxHp=num(at, "_maxHealthMultiplier", 1), shield=num(at, "_shieldAmount"))
    towers[stem(f)] = dict(name=stem(f), file=rel(p), pop=int(num(t, "_populationCapacity")),
                           hp=num(t, "_maxHealth"), revive=num(t, "_reviveDelay"),
                           cost=costs(t), attack=attack_of(t), aura=aura,
                           elemental="ElementalTowerData" in t)
for sub in ["Fire", "Ice", "Stone", "Time"]:
    d = os.path.join(tdir, "ElementalTower", sub)
    if not os.path.isdir(d): continue
    for f in os.listdir(d):
        if f.startswith("TA_") and f.endswith(".asset"):
            a = parse_attack(os.path.join(d, f))
            for tw in towers.values():
                if tw["attack"] and tw["attack"]["name"] == a["name"]:
                    tw["attack"] = a

# ---------- 새끼용 ----------
babies = {}
for sub in ["Fire", "Ice", "Life", "Stone", "Time"]:
    d = os.path.join(DATA, "BabyDragon", sub)
    if not os.path.isdir(d): continue
    for f in os.listdir(d):
        if not (f.startswith("BD_") and f.endswith(".asset")): continue
        p = os.path.join(d, f); t = read(p)
        babies[stem(f)] = dict(name=stem(f), file=rel(p), pop=int(num(t, "_populationCapacity")),
                               hp=num(t, "_maxHealth"), revive=num(t, "_reviveDelay"),
                               attack=attack_of(t), baseFeed=num(t, "_baseFeed"),
                               feedSame=num(t, "_additionalFeedPerSameType"))

# ---------- 어미용 액티브 ----------
skills = {}
sd = os.path.join(DATA, "Dragon", "Skills")
for f in sorted(os.listdir(sd)):
    if not f.endswith(".asset"): continue
    p = os.path.join(sd, f); t = read(p)
    skills[stem(f)] = dict(name=stem(f), file=rel(p), cooldown=num(t, "DefaultCooltime"),
                           usePerDay=int(num(t, "DefaultUsePerDay", -1)),
                           pctCurrentHp=num(t, "DamagePercentOfCurrentHealth"),
                           flat=num(t, "FlatDamage"), area=num(t, "AreaRadius"),
                           heal=num(t, "HealAmount"))

# ---------- 점령 ----------
surv = {}
with open(os.path.join(DATA, "ConquestData", "ChunkTerrainSurvey.csv"), encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        surv[(int(r["ChunkCoordX"]), int(r["ChunkCoordY"]))] = r["RealDominantTerrain"]

ct = read(os.path.join(DATA, "ConquestData", "Data_ConquestChunkCostTable_Alpha.asset"))
chunks = []
for b in re.split(r"- ChunkCoord: ", ct)[1:]:
    x, y = int(re.search(r"x: (-?\d+)", b).group(1)), int(re.search(r"y: (-?\d+)", b).group(1))
    pg = re.search(r"EnemyEnhancementProfile: \{fileID: 11400000, guid: (\w+)", b)
    chunks.append(dict(x=x, y=y, terrain=surv.get((x, y), "?"),
                       pop=int(re.search(r"Population: (\d+)", b).group(1)),
                       food=int(re.search(r"Food: (\d+)", b).group(1)),
                       wood=int(re.search(r"Wood: (\d+)", b).group(1)),
                       stone=int(re.search(r"Stone: (\d+)", b).group(1)),
                       profile=name_of(pg.group(1)) if pg else None,
                       reward=int(re.search(r"PopulationReward: (-?\d+)", b).group(1)),
                       dist=abs(x) + abs(y)))

pdir = os.path.join(DATA, "ConquestData", "EnemyEnhancement_BalanceTest")
profiles = {}
for f in sorted(os.listdir(pdir)):
    if not f.endswith(".asset"): continue
    p = os.path.join(pdir, f); t = read(p)
    rules = []
    for b in re.split(r"- _targetMonster: ", t)[1:]:
        g = re.search(r"guid: (\w+)", b).group(1)
        def pair(k):
            m = re.search(r"_%s:\s*\n\s*_additiveBonus: (-?[\d.]+)\s*\n\s*_multiplierBonus: (-?[\d.]+)" % k, b)
            return [float(m.group(1)), float(m.group(2))] if m else [0.0, 0.0]
        rules.append(dict(target=g2m.get(g, name_of(g)),
                          spawn=int(re.search(r"_spawnCountBonus: (-?\d+)", b).group(1)),
                          hp=pair("maxHealth"), shield=pair("shieldAmount"),
                          atk=pair("attackPower"), speed=pair("moveSpeed"), iv=pair("spawnInterval")))
    profiles[stem(f)] = dict(name=stem(f), file=rel(p), rules=rules)

dur = read(os.path.join(DATA, "ConquestData", "Data_ConquestDurationTable.asset"))
TERR = {0: "Grass", 1: "Rock", 2: "Volcano", 3: "Desert", 4: "Snow", 5: "Default", 6: "Road"}
duration = {TERR[int(a)]: int(b) for a, b in re.findall(r"TerrainType: (\d+)\s*\n\s*DaysRequired: (\d+)", dur)}

# ---------- 생산 / 경제 ----------
FOOTPRINT = {"RPD_FarmField": 4, "RPD_LoggingCamp": 3, "RPD_Quarry": 4, "RPD_GrassSlime": 4}
prod = {}
rpdir = os.path.join(DATA, "ResourceProductionData")
for f in sorted(os.listdir(rpdir)):
    if not (f.startswith("RPD_") and f.endswith(".asset")): continue
    if "OverrideTable" in f: continue
    p = os.path.join(rpdir, f); t = read(p)
    prod[stem(f)] = dict(name=stem(f), file=rel(p), pop=int(num(t, "_populationCapacity")),
                         cost=costs(t), cells=FOOTPRINT.get(stem(f), 4))

eb = read(os.path.join(DATA, "Balance", "EB_EconomyBalance_Alpha.asset"))
economy = dict(baselineCellYield=num(eb, "_baselineCellYield", 5),
               diagonalBonus=num(eb, "_diagonalBonusFactor", .4),
               foodUpkeepPerPop=num(eb, "_foodUpkeepPerPopulation", 1),
               file=rel(os.path.join(DATA, "Balance", "EB_EconomyBalance_Alpha.asset")),
               startFood=350, startWood=350, startStone=350, startPop=50, castleHp=100,
               sceneFile="Assets/Scenes/SampleScene.unity")

# 셀 산출 오버라이드 분포 (농장 산출 추정용)
ov = read(os.path.join(rpdir, "RPD_CellYieldOverrideTable_Alpha.asset"))
dist = collections.defaultdict(list)
for b in re.split(r"- AnchorCoord: ", ov)[1:]:
    for ty, y in re.findall(r"- ResourceType: (\d+)\s*\n\s*Yield: (\d+)", b):
        dist[RES.get(int(ty), str(ty))].append(int(y))
cellYield = {k: dict(n=len(v), min=min(v), max=max(v), avg=round(sum(v) / len(v), 2))
             for k, v in dist.items()}

# 청크별 자원 배율 (특화자원 확보 가능 지점 파악용)
cy = read(os.path.join(DATA, "ResourceNodeData", "CYT_ChunkYieldTable_Alpha.asset"))
chunkYield = {}
for b in re.split(r"- ChunkCoord: ", cy)[1:]:
    x, y = int(re.search(r"x: (-?\d+)", b).group(1)), int(re.search(r"y: (-?\d+)", b).group(1))
    chunkYield["%d,%d" % (x, y)] = {RES.get(int(ty), str(ty)): float(mu)
                                    for ty, mu in re.findall(
                                        r"- ResourceType: (\d+)\s*\n\s*Multiplier: ([\d.]+)", b)}

# ---------- 연구 ----------
research = {}
rdir = os.path.join(DATA, "Research")
for f in sorted(os.listdir(rdir)):
    if not (f.startswith("RN_") and f.endswith(".asset")): continue
    p = os.path.join(rdir, f); t = read(p)
    effs = []
    if "_effects:" in t:
        seg = t.split("_effects:")[1].split("_requiredLandmark")[0]
        for g in re.findall(r"- \{fileID: 11400000, guid: (\w+), type: 2\}", seg):
            ep = guid2path.get(g)
            if not ep: continue
            et = read(ep)
            effs.append(dict(name=stem(ep), cls=re.search(r"Assembly-CSharp::(\w+)", et).group(1),
                             ratio=num(et, "_bonusRatio"), delta=num(et, "_capacityDelta"),
                             heal=num(et, "_healPerDay")))
    research[stem(f)] = dict(
        name=stem(f), file=rel(p),
        nodeId=(re.search(r"_nodeId: (\S+)", t).group(1) if "_nodeId:" in t else stem(f)),
        branch=int(num(t, "_branch")), tier=int(num(t, "_tier")),
        rp=int(num(t, "_researchPointCost")), cost=costs(t.replace("_resourceCost:", "_buildCost:")),
        effects=effs)

bundle = dict(
    meta=dict(source="feature/balancin-branch 작업본", scene="SampleScene",
              schedule="WaveCycleSchedule_Codex", costTable="Data_ConquestChunkCostTable_Alpha",
              penaltyFolder="EnemyEnhancement_BalanceTest", generated="2026-08-18"),
    economy=economy, cellYield=cellYield, chunkYield=chunkYield,
    production=prod, research=research,
    researchBalance=dict(rpPerPop=5, labPop=2, labCost={"Wood": 20, "Stone": 20}),
    towers=towers, babyDragons=babies, dragonSkills=skills,
    monsters=monsters, behaviors=behaviors, waves=waves,
    chunks=chunks, profiles=profiles, conquestDuration=duration,
    portalTerrain=PORTAL_TERRAIN,
    terrainPenalty={"Snow": {"woodUpkeepPerPop": 1}, "Rock": {"stoneUpkeepPerPop": 1},
                    "Desert": {"yieldReduction": .3, "towerAtkSpeedReduction": .3},
                    "Volcano": {}},
)
with open(os.path.join(OUT, "bundle.json"), "w", encoding="utf-8") as fh:
    json.dump(bundle, fh, ensure_ascii=False, separators=(",", ":"))
print("towers", len(towers), "monsters", len(monsters), "waves", len(waves),
      "chunks", len(chunks), "profiles", len(profiles), "research", len(research),
      "baby", len(babies), "skills", len(skills))
print("bundle.json", os.path.getsize(os.path.join(OUT, "bundle.json")), "bytes")
