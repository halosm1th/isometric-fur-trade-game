"""Lay out the towns of tools/world/towns.json on the map and people them.

Run after build_world.py (and again whenever towns.json changes):

    python tools/world/place_towns.py

Each town is set down on dry ground at its real place: its garrison's
palisade first, then the town lot by lot on a street lattice, spreading out
over connected dry ground from the centre - the church, the hall, the market
and the stores at the heart, the tradesmen and labourers round them, the farms
at the edge with their fields beyond. Nothing is placed on a post, a camp or
another town. The layout goes into Data/World/world.json (layout.towns), and
every person into Scenes/World/YorkFactoryExterior.tscn as an NpcSpawn marker
under Cast/Towns/<town>/<house>, with a bed in the house and a place of work.
The town markers are rewritten on every run; nothing else in the scene is.
The names and families come from a generator seeded by the town's name, so a
re-run with the same towns.json gives the same people.
"""
import json, math, os, random, re, sys, unicodedata
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import build_world as B   # the projection and the cell geometry (main() is not run)

WORLD = os.path.join(ROOT, "Data", "World", "world.json")
GROUND = os.path.join(ROOT, "Data", "World", "ground.png")
SCENE = os.path.join(ROOT, "Scenes", "World", "YorkFactoryExterior.tscn")
PLANS = os.path.join(ROOT, "Data", "World", "post_plans.json")

# Building sizes (Posts.SizeOf): width, depth, door offset.
SIZE = dict(Townhouse=(4, 4, 1), Church=(8, 4, 3), Barracks=(8, 5, 3), Store=(8, 4, 3), Workshop=(4, 4, 1), Hall=(8, 4, 3),
            FactorsHouse=(5, 6, 2), PowderMagazine=(3, 3, 1))
# A lot is one house wide: the houses stand wall to wall along the street in
# front of each row, with a cross street every eight lots. At the map's scale
# (buildings life size, the country compressed) that is the only way a town of
# seven hundred keeps to the country round its real place.
LOT_I, LOT_J, STREET, CROSS_EVERY, CROSS = 4, 4, 2, 8, 2
ROLE = dict(Labourer=2, Woman=3, Wife=10, Farmer=13, Merchant=14, Artisan=15, Soldier=16, Officer=17, Priest=18, Child=19)
FACTION = dict(AMER=14, CROWN=17)
HOME_TOWN = 7

# ---- the ground ---------------------------------------------------------------
img = Image.open(GROUND).convert("RGB")
W, H = img.size
px = img.load()
WET = {B.COL[k] for k in ("Ocean", "Lake", "River", "Ford")}
def dry(c):
    x, y = c
    return 0 <= x < W and 0 <= y < H and px[x, y] not in WET

world = json.load(open(WORLD, encoding="utf-8"))
L = world["layout"]
plans = json.load(open(PLANS, encoding="utf-8")) if os.path.exists(PLANS) else {}
taken = set()
def take(cells, margin_top=None):
    taken.update(cells)
for p in L["posts"]:
    pl = plans.get(p["name"]); w, d = (pl["width"], pl["depth"]) if isinstance(pl, dict) else (12, 9)
    taken.update(B.Rhombus(B.Cell(tuple(p["top"]), -4, -4), w + 8, d + 8))
for c in L["camps"]:
    taken.update(B.Rhombus(B.Cell(tuple(c["centre"]), -7, -7), 15, 15))

def free(cells): return all(dry(c) and c not in taken for c in cells)

# ---- names ------------------------------------------------------------------------
FR_SUR = ["Campau", "Moran", "Beaubien", "Chene", "Reaume", "Rivard", "Godfroy", "Dequindre", "Cicotte", "Meloche", "Labadie", "Bondy", "Pelletier", "Gouin",
          "Tremblay", "St. Aubin", "Chapoton", "Riopelle", "Baby", "Navarre", "Lasselle", "Drouillard", "Ouellette", "Janisse", "Laframboise", "Bourassa",
          "Langlois", "Parent", "Marentette", "Rousseau", "Lajeunesse", "Dupuis", "Bertrand", "Cloutier", "Ducharme", "Gagnon", "Hamelin", "Lefebvre", "Morand", "Trudelle"]
FR_M = ["Jean-Baptiste", "Joseph", "Pierre", "Francois", "Louis", "Antoine", "Charles", "Jacques", "Gabriel", "Michel", "Alexis", "Etienne", "Hyacinthe", "Isidore", "Basile", "Simon", "Laurent", "Nicolas"]
FR_F = ["Marie", "Marie-Josephte", "Angelique", "Catherine", "Therese", "Genevieve", "Madeleine", "Marguerite", "Monique", "Archange", "Cecile", "Elisabeth", "Agathe", "Victoire", "Josette", "Felicite"]
EN_SUR = {
    "loyalist": ["Smith", "Ridout", "Robinson", "Baldwin", "Small", "Denison", "Ketchum", "Hamilton", "Butler", "Servos", "Ball", "Clench", "Kerr", "Muirhead",
                 "McNabb", "Brown", "Thompson", "Wilson", "Clark", "Hall", "Ward", "Willcocks", "Beman", "Playter", "Scadding", "Gamble", "Heron", "Lyons",
                 "Hunter", "Kirby", "Swayze", "Nelles", "Pell", "Johnson", "McDonell", "Fraser", "Grant", "Stuart", "Macaulay", "Cameron", "Ross", "Warren",
                 "Secord", "Lampman", "Stevens", "House", "Vrooman", "Ten Broeck", "Markle", "Showers", "Durham", "Hare"],
    "american": ["Sibley", "Brush", "Woodbridge", "Witherell", "Hunt", "Whipple", "Dyson", "Knaggs", "Macomb", "Williams", "Griswold", "Larned", "Tuttle",
                 "Coe", "Dodemead", "Hanks", "Fuller", "Pease", "Jones", "Smith", "Brown", "Miller", "Wilson", "Moore", "Taylor", "Thomas", "White",
                 "Harris", "Martin", "Allen", "Baker", "Wells", "Stone", "Chapin", "Root", "Hart"],
}
EN_M = ["John", "William", "James", "Thomas", "George", "Robert", "Samuel", "Joseph", "Richard", "Henry", "David", "Peter", "Daniel", "Benjamin", "Jacob",
        "Abraham", "Isaac", "Edward", "Charles", "Alexander", "Andrew", "Hugh", "Archibald", "Duncan", "Ebenezer", "Nathaniel", "Elijah", "Moses"]
EN_F = ["Mary", "Elizabeth", "Sarah", "Ann", "Margaret", "Catherine", "Jane", "Hannah", "Rebecca", "Susannah", "Charlotte", "Eleanor", "Nancy", "Phoebe",
        "Martha", "Abigail", "Isabella", "Christiana", "Lydia", "Esther"]
SOLDIER_SUR = ["Kelly", "Murphy", "Walsh", "Byrne", "Ryan", "O'Brien", "Doyle", "Connor", "Brennan", "Flynn", "Shaw", "Taylor", "Wright", "Walker", "Hughes",
               "Edwards", "Green", "Hill", "Wood", "Jackson", "Cooper", "Ward", "Turner", "Parker", "Bell", "Morris", "Cook", "Watson", "Mitchell", "Robertson",
               "Campbell", "Stewart", "Murray", "Reid", "Graham", "Black", "Fletcher", "Harding", "Dunn", "Lowe", "Price", "Hayes"]

scene_text = open(SCENE, encoding="utf-8", newline="").read()
# Everyone outside the towns keeps his name; a town's people must not take one.
outside = re.sub(r"(?ms)^\[node name=\"[^\"]*\" type=\"Node2D\" parent=\"Cast/Towns[^\"]*\"[^\n]*\n.*?(?=^\[|\Z)", "", scene_text)
used = set(re.findall(r'(?m)^DisplayName = "([^"]+)"', outside))

def node_name(n): return re.sub(r"[^A-Za-z0-9 \-]", "", unicodedata.normalize("NFKD", n).encode("ascii", "ignore").decode()).strip() or "Someone"

class Namer:
    def __init__(self, rng, pool, french):
        self.rng, self.pool, self.french = rng, pool, french
    def surname(self, fr):
        return self.rng.choice(FR_SUR if fr else EN_SUR[self.pool])
    def unique(self, given_pool, sur, prefix=""):
        for _ in range(60):
            n = f"{prefix}{self.rng.choice(given_pool)} {sur}"
            if n not in used and n not in reserved: used.add(n); return n
        k = 2
        while f"{prefix}{given_pool[0]} {sur} {k}" in used: k += 1
        n = f"{prefix}{given_pool[0]} {sur} {k}"; used.add(n); return n
    def fixed(self, n):
        if n in used:
            raise SystemExit(f"{n} is already someone in the scene")
        used.add(n); return n

# ---- laying a town out ----------------------------------------------------------------
def anchor_of(t):
    # "at": a cell to build round instead of the real place, where the real
    # place lies inside a bigger neighbour at the map's scale (towns.json).
    c = tuple(t["at"]) if t.get("at") else B.world_cell(*B.world(t["lon"], t["lat"]))
    if dry(c) and c not in taken: return c
    for r in range(1, 40):
        for dx in range(-r, r + 1):
            for dy in range(-2 * r, 2 * r + 1):
                n = (c[0] + dx, c[1] + dy)
                if dry(n) and n not in taken: return n
    raise SystemExit(f"nowhere dry near {t['name']}")

def lot_origin(a, b):
    return a * LOT_I + (a // CROSS_EVERY) * CROSS, b * (LOT_J + STREET)

def lot_cells(anchor, a, b):
    i, j = lot_origin(a, b)
    top = B.Cell(anchor, i, j)
    cells = list(B.Rhombus(top, LOT_I, LOT_J)) + list(B.Rhombus(B.Cell(top, 0, LOT_J), LOT_I, STREET))
    if a % CROSS_EVERY == CROSS_EVERY - 1: cells += list(B.Rhombus(B.Cell(top, LOT_I, 0), CROSS, LOT_J + STREET))
    return top, cells

def place_fort(anchor, spec):
    """The garrison's palisade: barracks two to a row, then the officers'
    quarters and the magazine, round a parade."""
    nb = max(1, math.ceil((spec["soldiers"] + 6) / 40))
    per_row = 3 if nb > 4 else 2
    rows = math.ceil(nb / per_row)
    fw, fd = 2 + per_row * 9, 2 + rows * 7 + 8 + 4
    best = None
    for r in range(0, 60):
        for di in range(-r, r + 1):
            for dj in (-r, r) if abs(di) != r else range(-r, r + 1):
                top = B.Cell(anchor, di * 2, dj * 2)
                cells = list(B.Rhombus(B.Cell(top, -2, -2), fw + 4, fd + 4))
                if free(cells): best = (di * 2, dj * 2); break
            if best: break
        if best: break
    if best is None: raise SystemExit(f"no room for {spec['name']}")
    fi, fj = best
    blds = []
    for k in range(nb):
        row, col = divmod(k, per_row)
        blds.append(("Barracks", f"the barracks" + (f" ({k + 1})" if nb > 1 else ""), fi + 1 + col * 9, fj + 2 + row * 7, "garrison"))
    blds.append(("FactorsHouse", "the officers' quarters", fi + 2, fj + 2 + rows * 7, "garrison"))
    blds.append(("PowderMagazine", "the powder magazine", fi + 14, fj + 2 + rows * 7 + 1, ""))
    taken.update(B.Rhombus(B.Cell(anchor, fi - 2, fj - 2), fw + 4, fd + 4))
    return dict(i=fi, j=fj, width=fw, depth=fd, gate=fw // 2, name=spec["name"]), blds, (fi + fw // 2, fj + fd - 6)

def lay_out(t, rng):
    anchor = anchor_of(t)
    fort, fort_blds, parade = (None, [], None)
    if t.get("fort"):
        fort, fort_blds, parade = place_fort(anchor, t["fort"])
    return anchor, fort, fort_blds, parade

SHORT = []
DEBUG = []   # (cells, colour) for the --debug picture

def reachable(anchor, radius_px=30000):
    """Dry ground joined to the town's centre without crossing water: a town
    does not spill over the river into its neighbour's country."""
    ax, ay = B.cell_world(*anchor)
    seen = {anchor}; todo = [anchor]
    while todo:
        c = todo.pop()
        for n in (B.DR(c), B.DL(c), B.UL(c), B.UR(c)):
            if n in seen or not dry(n): continue
            x, y = B.cell_world(*n)
            if math.hypot(x - ax, y - ay) > radius_px: continue
            seen.add(n); todo.append(n)
    return seen

def town_lots(anchor, need):
    """Lots spreading over connected dry ground from the centre, nearest first."""
    import heapq
    region = reachable(anchor)
    ok = lambda cells: free(cells) and all(c in region for c in cells)
    ax, ay = B.cell_world(*anchor)
    def dist(a, b):
        top, _ = lot_cells(anchor, a, b)
        x, y = B.cell_world(*top)
        return math.hypot(x - ax, y - ay)
    start = None
    for r in range(0, 12):
        for a in range(-r, r + 1):
            for b in range(-r, r + 1):
                if start is None and ok(lot_cells(anchor, a, b)[1]): start = (a, b)
    if start is None:
        # The heart is taken: go out over it to the nearest free ground.
        for r in range(0, 12):
            for a in range(-r, r + 1):
                for b in range(-r, r + 1):
                    if start is None and all(c in region for c in lot_cells(anchor, a, b)[1]): start = (a, b)
    if start is None: raise SystemExit(f"no dry lot near {anchor}")
    seen = {start}; pq = [(dist(*start), start)]; lots = []
    while pq and len(lots) < need:
        _, (a, b) = heapq.heappop(pq)
        top, cells = lot_cells(anchor, a, b)
        if not ok(cells):
            # Ground another town or a camp holds is passed over, not water:
            # the town goes on beyond it on the same side of the river.
            if not all(c in region for c in cells): continue
        else: lots.append((a, b))
        for n in ((a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)):
            if n not in seen:
                seen.add(n); heapq.heappush(pq, (dist(*n), n))
    if len(lots) < need: SHORT.append(f"only {len(lots)} of {need} lots on dry ground near {anchor}")
    for a, b in lots: taken.update(lot_cells(anchor, a, b)[1])
    DEBUG.append((anchor, "core", [c for a, b in lots for c in lot_cells(anchor, a, b)[1]]))
    return lots

def place_farms(anchor, n, rng):
    """The farms of the town's people: homesteads out in the country round it,
    each a house and its fields with room between them, nearest first, on dry
    ground on the town's own side of the water."""
    region = reachable(anchor)
    ax, ay = B.cell_world(*anchor)
    cand = []
    for i in range(-160, 161, 8):
        for j in range(-160, 161, 8):
            c = B.Cell(anchor, i, j)
            x, y = B.cell_world(*c)
            d = math.hypot(x - ax, y - ay)
            if d < 22000: cand.append((d * (0.8 + 0.4 * rng.random()), i, j))
    cand.sort()
    out = []
    for _, i, j in cand:
        if len(out) >= n: break
        yard = list(B.Rhombus(B.Cell(anchor, i - 1, j - 1), 10, 8))
        if not all(c in region and c not in taken for c in yard): continue
        taken.update(yard)
        out.append((i, j))
    if len(out) < n: print(f"  {n - len(out)} of {n} farms found no ground near {anchor} and are in the town")
    DEBUG.append((anchor, "farms", [c for i, j in out for c in B.Rhombus(B.Cell(anchor, i - 1, j - 1), 10, 8)]))
    return out

# ---- people ------------------------------------------------------------------------------
class Person:
    def __init__(self, name, role, female, age, **kw):
        self.name, self.role, self.female, self.age = name, role, female, age
        self.leader = kw.get("leader", False); self.spouse = None; self.father = None
        self.frames = kw.get("frames"); self.tint = kw.get("tint"); self.inv = kw.get("inv", "")
        self.work = None; self.bed = None

def family(nm, rng, sur_fr, head_role, head_name=None, head_age=None, alone=False):
    fr = sur_fr
    sur = head_name.split()[-1] if head_name else nm.surname(fr)
    gm, gf = (FR_M, FR_F) if fr else (EN_M, EN_F)
    age = head_age or rng.randint(24, 62)
    head = Person(nm.fixed(head_name) if head_name else nm.unique(gm, sur), head_role, False, age, leader=True)
    people = [head]
    if alone: return people, sur
    if rng.random() < 0.9:
        wife = Person(nm.unique(gf, sur), "Wife", True, max(17, age - rng.randint(0, 9)))
        wife.spouse = head; head.spouse = wife
        people.append(wife)
        # Children, two years or so apart, from the eldest down.
        n = min(8, max(0, int(rng.gauss(3.4, 1.8))))
        oldest = min(wife.age - 18, 22)
        for k in range(n):
            ca = oldest - k * rng.randint(1, 3)
            if ca < 0: break
            girl = rng.random() < 0.5
            if ca < 14: role = "Child"
            elif girl: role = "Woman"
            else: role = "Farmer" if head_role == "Farmer" else "Artisan" if head_role == "Artisan" else "Labourer"
            c = Person(nm.unique(gf if girl else gm, sur), role, girl, ca); c.father = head
            people.append(c)
    elif rng.random() < 0.5:
        # A widower and his children, or a single man with a hired man.
        people.append(Person(nm.unique(gm, nm.surname(fr)), "Labourer", False, rng.randint(17, 40)))
    if head_role in ("Farmer", "Merchant") and rng.random() < 0.25:
        people.append(Person(nm.unique(gf if rng.random() < 0.5 else gm, nm.surname(fr)), "Woman" if rng.random() < 0.5 else "Labourer", False, rng.randint(15, 30)))
        if people[-1].role == "Woman": people[-1].female = True
    return people, sur

def garrison(spec, polity, rng, nm):
    brit = polity == "CROWN"
    sol_tint = (1.0, 0.55, 0.5) if brit else (0.6, 0.7, 1.0)
    out = []
    officers = []
    cmd = spec.get("commandant")
    ranks = ["Lieutenant", "Lieutenant", "Ensign", "Lieutenant", "Captain", "Ensign", "Lieutenant", "Surgeon"]
    for k in range(spec["officers"]):
        if k == 0 and cmd:
            o = Person(nm.fixed(cmd), "Officer", False, rng.randint(36, 50), leader=True)
        else:
            rank = "Captain" if k == 0 else ranks[k % len(ranks)]
            o = Person(nm.unique(EN_M, rng.choice(EN_SUR["loyalist" if brit else "american"]), rank + " "), "Officer", False, rng.randint(22, 45), leader=(k == 0))
        o.frames, o.tint, o.inv = "33_factor", sol_tint, "Musket, Capote, Knife"
        officers.append(o); out.append(o)
        if k < 2 and rng.random() < 0.6:
            w = Person(nm.unique(EN_F, o.name.split()[-1]), "Wife", True, max(18, o.age - rng.randint(0, 10))); w.spouse = o; o.spouse = w
            out.append(w)
    for k in range(spec["soldiers"]):
        rank = "Sergeant " if k % 15 == 0 else "Corporal " if k % 15 == 1 else "Private "
        s = Person(nm.unique(EN_M, rng.choice(SOLDIER_SUR), rank), "Soldier", False, rng.randint(18, 40))
        s.frames, s.tint, s.inv = "32_lab", sol_tint, "Musket, Capote, Knife"
        out.append(s)
        # The few wives allowed on the strength, and their children.
        if k % 16 == 3:
            w = Person(nm.unique(EN_F, s.name.split()[-1]), "Wife", True, max(18, s.age - rng.randint(0, 6))); w.spouse = s; s.spouse = w
            out.append(w)
            for c in range(rng.randint(0, 3)):
                girl = rng.random() < 0.5
                ch = Person(nm.unique(EN_F if girl else EN_M, s.name.split()[-1]), "Child", girl, rng.randint(0, 11)); ch.father = s
                out.append(ch)
    return out

def look(p, french):
    if p.frames: return
    if p.role == "Child": p.frames = "33_girl" if p.female else "33_boy"
    elif p.female: p.frames = "32_cwife"
    elif p.role == "Farmer": p.frames = "32_voy" if french else "32_lab"
    elif p.role == "Merchant": p.frames = "33_clerk"
    elif p.role == "Officer": p.frames = "33_factor"
    elif p.role == "Priest": p.frames, p.tint = "33_clerk", (0.4, 0.4, 0.45)
    elif p.role == "Artisan": p.frames, p.tint = "32_lab", (0.95, 0.9, 0.85)
    else: p.frames = "32_voy" if french else "32_lab"
    if not p.inv:
        p.inv = "" if p.role == "Child" else "Capote, Knife" if p.female else "Capote, Knife, Bread:2"
        if p.role == "Farmer" and not p.female and p.age >= 18: p.inv = "Musket, Capote, Knife, Bread:2"

def interior(kind):
    w, d, door = SIZE[kind]
    cells = [(i, j) for j in range(1, d - 1) for i in range(1, w - 1) if (i, j) != (1, 1)]
    return cells or [(1, 1)]

# ---- the whole run -----------------------------------------------------------------------
spec = json.load(open(os.path.join(HERE, "towns.json"), encoding="utf-8"))
reserved = set()
for t in spec["towns"]:
    for n in [t.get("priest")] + list(t.get("stores", [])) + [x["name"] for x in t.get("notables", [])] + [(t.get("fort") or {}).get("commandant")]:
        if n and not n.startswith("the "): reserved.add(n); reserved.add(n.replace("Father ", "").replace("Reverend ", ""))
out_towns = []; markers = []; total = 0
# Every town's centre and garrison are set down before any town takes its
# lots, so Fort Niagara is not built over by Niagara across the river.
laid = {}
for t in spec["towns"]:
    laid[t["name"]] = lay_out(t, None)
    a = laid[t["name"]][0]
    taken.update(B.Rhombus(B.Cell(a, -3, -3), 7, 7))
# The order the towns take their ground in (towns.json "lay"): where two
# would want the same shore, the one that really stood there goes first -
# Niagara at the river's mouth, then Queenston up the river, then York.
# Two passes: every town's heart is laid out first, then the farms round all
# of them, so that a small town's core stays together beside its neighbour's
# instead of being scattered among the other's farms.
CORE = {}
names_before = set(used)
for PASS in ("cores", "all"):
  used.clear(); used.update(names_before)
  out_towns = []; markers = []; total = 0
  for t in sorted(spec["towns"], key=lambda t: t.get("lay", 99)):
        rng = random.Random("town:" + t["name"])
        nm = Namer(rng, t["names"], t["french"])
        anchor, fort, fort_blds, parade = laid[t["name"]]
        for c in B.Rhombus(B.Cell(anchor, -3, -3), 7, 7): taken.discard(c)
        town_name = t["name"]
        # What the town needs: the heart first, then the families.
        heart = []   # (kind, name, home, lots, residents)
        if t.get("church"): heart.append(("Church", t["church"], "", 2, []))
        if t.get("hall"): heart.append(("Hall", t["hall"], "", 2, []))
        heart.append(("Store", "the market house", "market", 2, []))
        households = []   # (kind, name, home, people)
        hk = 0
        def home_key():
            global hk
            hk += 1; return f"house {hk}"
        if t.get("priest"):
            catholic = t["priest"].startswith("Father")
            pp, sur = family(nm, rng, False, "Priest", t["priest"].replace("Father ", "").replace("Reverend ", ""), rng.randint(40, 60), alone=catholic)
            pp[0].name = t["priest"]; used.add(t["priest"])
            households.append(("Townhouse", "the presbytery" if catholic else "the rectory", home_key(), pp))
        for n in t.get("notables", []):
            if n["role"] == "Wife": continue
            pp, sur = family(nm, rng, False, n["role"], n["name"], n.get("age"))
            households.append(("Townhouse", n["house"], home_key(), pp))
        for m in t.get("stores", []):
            if m.startswith("the "):
                households.append(("Store", m, home_key(), []))   # the Indian Department's storehouse, the sutler's store: nobody lives over it
                continue
            fr = rng.random() < t["french"]
            pp, sur = family(nm, rng, fr, "Merchant", m or None, rng.randint(30, 58))
            households.append(("Store", f"{pp[0].name}'s store", home_key(), pp))
        for trade in t.get("trades", []):
            fr = rng.random() < t["french"]
            pp, sur = family(nm, rng, fr, "Artisan")
            households.append(("Workshop", f"the {trade}'s shop", home_key(), pp))
        # Laura Secord and the like: a wife named in the notables marries the named man.
        for n in t.get("notables", []):
            if n["role"] != "Wife": continue
            for _, _, _, pp in households:
                if pp and pp[0].name == n["of"]:
                    wife = next((q for q in pp if q.role == "Wife"), None)
                    if wife: used.discard(wife.name); wife.name = nm.fixed(n["name"]); wife.age = n.get("age", wife.age)
        civil = sum(len(pp) for *_, pp in households)
        farms = []
        while civil < t["people"]:
            farm = rng.random() < t["farm"]
            fr = rng.random() < t["french"]
            pp, sur = family(nm, rng, fr, "Farmer" if farm else "Labourer")
            (farms if farm else households).append(("Townhouse", f"the {sur} house", home_key(), pp))
            civil += len(pp)
        # Labouring families two to a house, the farms at the edge.
        shared, single_fams = [], []
        pending = None
        for h in households:
            kind, name, home, pp = h
            if kind == "Townhouse" and pp and pp[0].role == "Labourer":
                if pending is None: pending = h; continue
                shared.append(("Townhouse", f"{pending[1]} and {name.replace('the ', '')}", f"{pending[2]};{home}", pending[3] + pp, {pending[2]: pending[3], home: pp}))
                pending = None
            else: single_fams.append(h + ({home: pp},))
        if pending is not None: single_fams.append(pending + ({pending[2]: pending[3]},))
        households = single_fams[:]
        # The townsmen's houses nearer the heart than the shared ones; the farms
        # are out in the country (place_farms).
        households = households + shared
        farm_entries = [h + ({h[2]: h[3]},) for h in farms]
        # Lots: the heart's and the stores' double lots, then one each; then
        # the farms out in the country, and a house in the town for any farm
        # family that found no ground of its own.
        need = sum(h[3] for h in heart) + sum(2 if h[0] == "Store" else 1 for h in households) + 4
        if PASS == "cores":
            CORE[t["name"]] = town_lots(anchor, need) if need > 4 else []
            continue
        lots = list(CORE[t["name"]])
        homesteads = place_farms(anchor, len(farm_entries), rng)
        left = farm_entries[len(homesteads):]
        farm_entries = farm_entries[:len(homesteads)]
        if left:
            lots += town_lots(anchor, len(left))
            households = households + left
        free_lots = list(lots)
        blds = []
        def single():
            return free_lots.pop(0) if free_lots else None
        def double():
            for k, (a, b) in enumerate(free_lots):
                if (a + 1, b) in free_lots and a // CROSS_EVERY == (a + 1) // CROSS_EVERY:
                    free_lots.remove((a, b)); free_lots.remove((a + 1, b)); return (a, b)
            return single()
        for kind, name, home, nlots, _ in heart:
            lot = double() if nlots == 2 else single()
            if lot is None: break
            i, j = lot_origin(*lot); blds.append((kind, name, i, j, home))
        # Names of family houses are made unique within the town.
        seen = {}
        lot_of = {}
        for kind, name, home, pp, _ in households:
            if not free_lots: break
            lot = double() if kind == "Store" else single()
            if lot is None: break
            a, b = lot; i, j = lot_origin(a, b)
            k = seen.get(name, 0) + 1; seen[name] = k
            if k > 1: name = f"{name} ({k})"
            blds.append((kind, name, i, j, home)); lot_of[home] = (a, b, kind, i, j, name)
        for (kind, name, home, pp, _), (fi, fj) in zip(farm_entries, homesteads):
            k = seen.get(name, 0) + 1; seen[name] = k
            if k > 1: name = f"{name} ({k})"
            blds.append((kind, name, fi, fj, home)); lot_of[home] = (None, None, kind, fi, fj, name)
        households = households + farm_entries
        blds += fort_blds
        # Where each person sleeps and works.
        people_by_house = []
        church = next(((i, j) for kind, _, i, j, _ in blds if kind == "Church"), None)
        hall = next(((i, j) for kind, _, i, j, _ in blds if kind == "Hall"), None)
        for kind, name, home, pp, by_home in households:
          if not pp or home not in lot_of: continue
          a, b, kind, i, j, hname = lot_of[home]
          cells = interior(kind)
          w, d, door = SIZE[kind]
          threshold = (i + door, j + d - 2)
          for fam_home, fam in by_home.items():
            for k, p in enumerate(fam):
                    ci, cj = cells[k % len(cells)]
                    p.bed = (i + ci, j + cj)
                    if p.role == "Farmer" and a is None:
                        # A homestead: his fields beside the house.
                        p.work = (i + 6, j + 2) if dry(B.Cell(anchor, i + 6, j + 2)) else threshold
                    elif p.role == "Farmer":
                        # His fields: out beyond the lot, away from the town's heart.
                        da, db = a, b
                        norm = math.hypot(da, db) or 1
                        for step in range(8, 30, 2):
                            fi, fj = i + 2 + round(da / norm * step * 1.2), j + 2 + round(db / norm * step)
                            if dry(B.Cell(anchor, fi, fj)) and B.Cell(anchor, fi, fj) not in taken: p.work = (fi, fj); break
                        p.work = p.work or (i + door, j + LOT_J)   # the street in front, if the fields are under water
                    elif p.role == "Priest" and church: p.work = (church[0] + 3, church[1] + 2)
                    elif p.role == "Officer" and hall: p.work = (hall[0] + 3, hall[1] + 2)
                    elif p.role in ("Merchant", "Artisan"): p.work = (i + door, j + d - 2)
                    elif p.role == "Labourer": p.work = (i + door, j + LOT_J + 1)
                    else: p.work = threshold
                    look(p, p.name.split()[-1] in FR_SUR)
            people_by_house.append((hname, fam_home, fam))
        if fort:
            gp = garrison(t["fort"], t["polity"], rng, nm)
            bar = [(i, j) for kind, _, i, j, home in fort_blds if kind == "Barracks"]
            oq = next((i, j) for kind, _, i, j, home in fort_blds if kind == "FactorsHouse")
            bcells = interior("Barracks"); ocells = interior("FactorsHouse")
            gate_out = (fort["i"] + fort["gate"], fort["j"] + fort["depth"] + 1)
            for k, p in enumerate(gp):
                if p.role == "Officer" or (p.spouse and p.spouse.role == "Officer"):
                    ci, cj = ocells[k % len(ocells)]; p.bed = (oq[0] + ci, oq[1] + cj)
                    p.work = (parade[0], parade[1])
                else:
                    bi, bj = bar[k % len(bar)]; ci, cj = bcells[(k // len(bar)) % len(bcells)]
                    p.bed = (bi + ci, bj + cj)
                    # One man in eight on sentry at the gate, the rest at drill on the parade.
                    p.work = gate_out if p.role == "Soldier" and k % 8 == 0 else (parade[0] - 4 + k % 9, parade[1] - 1 + (k // 9) % 3)
                look(p, False)
            people_by_house.append(("the garrison", "garrison", gp))
        # Rebase everything on the town's own top corner.
        cells_ij = [(i, j) for _, _, i, j, _ in blds]
        if fort: cells_ij += [(fort["i"], fort["j"]), (fort["i"] + fort["width"], fort["j"] + fort["depth"])]
        for kind, _, i, j, _ in blds:
            w, d, _ = SIZE[kind]; cells_ij.append((i + w, j + d))
        i0 = min(i for i, _ in cells_ij); j0 = min(j for _, j in cells_ij)
        i1 = max(i for i, _ in cells_ij); j1 = max(j for _, j in cells_ij)
        top = B.Cell(anchor, i0, j0)
        entry = dict(name=town_name, polity=t["polity"], year=t["year"], note=t["note"], top=list(top), width=i1 - i0, depth=j1 - j0,
                     buildings=[dict(kind=k, name=n, i=i - i0, j=j - j0, home=h) for k, n, i, j, h in blds])
        if fort: entry["fort"] = dict(i=fort["i"] - i0, j=fort["j"] - j0, width=fort["width"], depth=fort["depth"], gate=fort["gate"], name=fort["name"])
        out_towns.append(entry)
        count = sum(len(pp) for *_, pp in people_by_house)
        total += count
        print(f"{town_name}: {count} people in {len(people_by_house)} households, {len(blds)} buildings, {entry['width']} x {entry['depth']} cells at {list(top)}")
        markers.append((town_name, t["polity"], anchor, people_by_house))

if SHORT:
    print("\n".join(SHORT))
if "--debug" in sys.argv:
    # The Lakes with every town's lots (one colour a town), the forts and the posts' and camps' ground in grey.
    dbg = img.copy(); dp = dbg.load()
    for c in taken:
        if 0 <= c[0] < W and 0 <= c[1] < H: dp[c[0], c[1]] = (90, 90, 90)
    cols = [(230, 60, 60), (60, 200, 60), (240, 200, 40), (200, 80, 220), (60, 200, 220), (255, 140, 0), (255, 255, 255)]
    by_anchor = {}
    for anchor, what, cells in DEBUG:
        k = by_anchor.setdefault(anchor, len(by_anchor))
        col = cols[k % len(cols)]
        if what == "farms": col = tuple((v + 255) // 2 for v in col)
        for c in cells:
            if 0 <= c[0] < W and 0 <= c[1] < H: dp[c[0], c[1]] = col
    X0, Y0 = 1060, 1880
    box = dbg.crop((X0, Y0, 1260, 2480)).resize((800, 1200), Image.NEAREST)
    from PIL import ImageDraw
    dr = ImageDraw.Draw(box)
    for t in spec["towns"]:
        c = B.world_cell(*B.world(t["lon"], t["lat"]))
        x, y = (c[0] - X0) * 4, (c[1] - Y0) * 2
        dr.ellipse((x - 5, y - 5, x + 5, y + 5), outline=(0, 0, 0), width=2)
        dr.text((x + 7, y - 6), t["name"], fill=(0, 0, 0))
    box.save(os.path.join(os.environ.get("TOWNS_DEBUG_DIR", HERE), "towns_debug.png"))
if SHORT and "--force" not in sys.argv:
    raise SystemExit("some towns did not fit (see above); --force writes them anyway")
L["towns"] = out_towns
raw = open(WORLD, encoding="utf-8").read()
open(WORLD, "w", encoding="utf-8", newline="\n").write(json.dumps(world, ensure_ascii=False, indent=1) + ("\n" if raw.endswith("\n") else ""))

# ---- the scene -----------------------------------------------------------------------------
s = scene_text
blocks = re.split(r"\n(?=\[)", s)
keep = [b for b in blocks if not re.match(r'\[node name="[^"]*" type="Node2D" parent="Cast/Towns', b)
        and not re.match(r'\[node name="Towns" type="Node2D" parent="Cast"\]', b)
        and not re.match(r'\[node name="Work" type="Marker2D" parent="Cast/Towns', b)]
s = "\n".join(keep).rstrip("\n") + "\n"
for rid, path in (("33_clerk", "ClerkFrames"), ("33_factor", "FactorFrames"), ("33_boy", "ChildBoyFrames"), ("33_girl", "ChildGirlFrames")):
    if f'id="{rid}"' not in s:
        s = s.replace('id="32_voy"]\n', f'id="32_voy"]\n[ext_resource type="SpriteFrames" path="res://Scenes/Npc/{path}.tres" id="{rid}"]\n', 1)
def W(c): return B.cell_world(*c)
gen = ['[node name="Towns" type="Node2D" parent="Cast"]\n']
for town_name, polity, anchor, houses in markers:
    tnode = node_name(town_name)
    gen.append(f'[node name="{tnode}" type="Node2D" parent="Cast/Towns"]\n')
    folders = set()
    for hname, home, pp in houses:
        folder = node_name(hname)[:60]
        while folder in folders: folder += " 2"
        folders.add(folder)
        gen.append(f'[node name="{folder}" type="Node2D" parent="Cast/Towns/{tnode}"]\n')
        nodes = {}
        for p in pp:
            nn = node_name(p.name)
            while nn in nodes.values(): nn += " 2"
            nodes[id(p)] = nn
        for p in pp:
            bx, by = W(B.Cell(anchor, *p.bed)); wx, wy = W(B.Cell(anchor, *p.work))
            lines = [f'[node name="{nodes[id(p)]}" type="Node2D" parent="Cast/Towns/{tnode}/{folder}"]', f"position = Vector2({round(bx)}, {round(by)})",
                     'script = ExtResource("30_npcspawn")', f'DisplayName = "{p.name}"', f"Role = {ROLE[p.role]}", f"Faction = {FACTION[polity]}",
                     f"Home = {HOME_TOWN}", f'PostName = "{town_name}|{home}"']
            if p.leader: lines.append("Leader = true")
            lines.append(f'Frames = ExtResource("{p.frames}")')
            if p.tint: lines.append("Tint = Color(%g, %g, %g, 1)" % p.tint)
            if p.role == "Child": lines.append("SpriteScale = %g" % round(0.6 + 0.2 * min(1, p.age / 16), 2))
            elif p.female: lines.append("SpriteScale = 0.72")
            if p.female: lines.append("Female = true")
            lines.append(f"Age = {max(1, p.age)}")
            if p.inv: lines.append(f'Inventory = "{p.inv}"')
            if p.spouse is not None and id(p.spouse) in nodes: lines.append(f'Spouse = NodePath("../{nodes[id(p.spouse)]}")')
            if p.father is not None and id(p.father) in nodes: lines.append(f'Father = NodePath("../{nodes[id(p.father)]}")')
            gen.append("\n".join(lines) + "\n")
            gen.append(f'[node name="Work" type="Marker2D" parent="Cast/Towns/{tnode}/{folder}/{nodes[id(p)]}"]\nposition = Vector2({round(wx - bx)}, {round(wy - by)})\n')
s = s.rstrip("\n") + "\n\n" + "\n".join(gen)
open(SCENE, "w", encoding="utf-8", newline="").write(s)
print(f"{total} people in {len(out_towns)} towns")
