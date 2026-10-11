"""Build the game's world map from the Pemmican Empire atlas data.

    python tools/world/build_world.py

Reads  tools/world/atlas_data.json  (the DATA block of pemmican_atlas.html:
       Natural Earth coasts, lakes and rivers, posts, ecozones)
       tools/world/extras.json      (hand additions: the Hayes, game places)
Writes Data/World/ground.png        (one pixel per map cell, colour = ground)
       Data/World/world.json        (scale, projection, size, places in cells)
       docs/world-ground-preview.png

ground.png is meant to be hand-editable: paint with the legend colours below
(the game matches each pixel to the nearest one).

Scale (Oct 2026, the old York-Oxford scale): 40 world px per km, Lambert
conformal conic so distances hold across the whole play area (design doc 6.1:
Pacific coast to Sault Ste. Marie and Moose Factory).
"""
import json, math, os, sys
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))

PX_PER_KM = 40.0
TILE_W, TILE_H = 128, 64          # IsoGrid: columns 128 px, rows 32 px
R_EARTH = 6371.0
LAT1, LAT2, LAT0, LON0 = 49.0, 59.0, 52.5, -102.75
WEST, EAST, SOUTH, NORTH = -127.0, -78.5, 44.5, 61.5
RES = 32                          # world px per raster pixel while drawing

# Ground classes, in the order the game's WorldMap.Ground enum uses.
LEGEND = [
    ("Ocean",          (36, 64, 110)),
    ("Lake",           (52, 92, 140)),
    ("River",          (64, 112, 160)),
    ("Ford",           (196, 176, 120)),
    ("Boreal",         (109, 138, 83)),
    ("Tundra",         (199, 203, 184)),
    ("Shield",         (169, 155, 171)),
    ("Parkland",       (179, 154, 69)),
    ("Grassland",      (214, 190, 90)),
    ("TallGrass",      (205, 173, 60)),
    ("LakesForest",    (111, 143, 82)),
    ("Cordillera",     (154, 132, 103)),
]
ECOZONE_CLASS = {
    "Tundra": "Tundra", "Boreal Forest": "Boreal", "Canadian Shield": "Shield", "Aspen Parkland": "Parkland",
    "Mixed Grassland": "Grassland", "Tall-Grass Prairie": "TallGrass", "Great Lakes Forest": "LakesForest", "Cordillera": "Cordillera",
}
COL = dict(LEGEND)
MAJOR_RIVERS = {"Nelson", "Saskatchewan", "North Saskatchewan", "South Saskatchewan", "Churchill", "Athabasca", "Peace",
                "Columbia", "Fraser", "Red River", "Assiniboine", "Winnipeg", "Albany", "Slave", "Missouri", "Hayes"}

# ---- projection -----------------------------------------------------------------
_r = math.radians
_n = math.log(math.cos(_r(LAT1)) / math.cos(_r(LAT2))) / math.log(math.tan(math.pi / 4 + _r(LAT2) / 2) / math.tan(math.pi / 4 + _r(LAT1) / 2))
_F = math.cos(_r(LAT1)) * math.tan(math.pi / 4 + _r(LAT1) / 2) ** _n / _n
def _rho(lat): return R_EARTH * _F / math.tan(math.pi / 4 + _r(lat) / 2) ** _n
_rho0 = _rho(LAT0)
def project_km(lon, lat):
    th = _n * _r(lon - LON0)
    rho = _rho(lat)
    return rho * math.sin(th), _rho0 - rho * math.cos(th)   # x east, y north (km)

X0 = project_km(WEST, LAT0)[0]
X1 = project_km(EAST, LAT0)[0]
YTOP = project_km(LON0, NORTH)[1]
YBOT = project_km(LON0, SOUTH)[1]
def world(lon, lat):
    x, y = project_km(lon, lat)
    return (x - X0) * PX_PER_KM, (YTOP - y) * PX_PER_KM

WORLD_W = (X1 - X0) * PX_PER_KM
WORLD_H = (YTOP - YBOT) * PX_PER_KM
COLS = int(WORLD_W // TILE_W)
ROWS = int(WORLD_H // (TILE_H / 2)) - 1

def cell_world(cx, cy): return (TILE_W / 2 + TILE_W * cx + (TILE_W / 2 if cy % 2 else 0), TILE_H / 2 + TILE_H / 2 * cy)
def world_cell(px, py):
    best, bd = None, 1e18
    y = round((py - TILE_H / 2) / (TILE_H / 2))
    for yy in (y - 1, y, y + 1):
        x = round((px - TILE_W / 2 - (TILE_W / 2 if yy % 2 else 0)) / TILE_W)
        for xx in (x - 1, x, x + 1):
            wx, wy = cell_world(xx, yy); d = (wx - px) ** 2 + (wy - py) ** 2
            if d < bd: bd, best = d, (xx, yy)
    return best

def rings(f):
    t, c = f["t"], f["c"]
    if t == "P": return [c[0]]
    if t == "MP": return [p[0] for p in c]
    if t == "L": return [c]
    if t == "ML": return c
    return []

def main():
    atlas = json.load(open(os.path.join(HERE, "atlas_data.json"), encoding="utf-8"))
    extras = json.load(open(os.path.join(HERE, "extras.json"), encoding="utf-8"))
    W, H = int(WORLD_W / RES) + 2, int(WORLD_H / RES) + 2
    def P(lon, lat):
        x, y = world(lon, lat); return (x / RES, y / RES)

    # Land (1) on sea (0); ecozones; lakes; rivers; fords - separate masks.
    land = Image.new("L", (W, H), 0); dl = ImageDraw.Draw(land)
    for f in atlas["base"]["land"]:
        for r in rings(f):
            if len(r) > 2: dl.polygon([P(*q) for q in r], fill=1)
    eco = Image.new("L", (W, H), 255); de = ImageDraw.Draw(eco)
    names = [n for n, _ in LEGEND]
    for zone, z in atlas["ecozones"].items():
        cls = ECOZONE_CLASS.get(zone)
        if cls and len(z["ring"]) > 2: de.polygon([P(*q) for q in z["ring"]], fill=names.index(cls))
    water = Image.new("L", (W, H), 0); dw = ImageDraw.Draw(water)
    for f in atlas["base"]["lakes"]:
        for r in rings(f):
            if len(r) > 2: dw.polygon([P(*q) for q in r], fill=1)
    rivers = [(f["n"], r) for f in atlas["base"]["rivers"] for r in rings(f)]
    widths = {x["name"]: x["width_px"] for x in extras.get("rivers", []) if "width_px" in x}
    rivers += [(x["name"], x["pts"]) for x in extras.get("rivers", [])]
    fords = []
    for name, r in rivers:
        if len(r) < 2: continue
        pts = [P(*q) for q in r]
        width = widths.get(name, 260 if name in MAJOR_RIVERS else 150) / RES
        dw.line(pts, fill=2, width=max(2, int(width)), joint="curve")
        # A ford every 40 km or so, so summer country isn't cut into islands.
        acc = 0.0
        for a, b in zip(pts, pts[1:]):
            seg = math.dist(a, b) * RES / PX_PER_KM
            acc += seg
            if acc > 110 and name not in widths: acc = 0; fords.append(b)
    dw2 = ImageDraw.Draw(water)
    for fx, fy in fords:
        rr = 170 / RES
        dw2.ellipse([fx - rr, fy - rr, fx + rr, fy + rr], fill=3)

    lp, ep, wp = land.load(), eco.load(), water.load()
    out = Image.new("RGB", (COLS, ROWS)); op = out.load()
    for cy in range(ROWS):
        for cx in range(COLS):
            wx, wy = cell_world(cx, cy)
            px, py = min(W - 1, int(wx / RES)), min(H - 1, int(wy / RES))
            w = wp[px, py]
            if not lp[px, py]: cls = "Ocean"
            elif w == 1: cls = "Lake"
            elif w == 3: cls = "Ford"
            elif w == 2: cls = "River"
            else: cls = names[ep[px, py]] if ep[px, py] != 255 else "Boreal"
            # A ford out in a lake or the sea is just water.
            op[cx, cy] = COL[cls]
    # Fords only mean something on rivers: re-check against the lake mask.
    for cy in range(ROWS):
        for cx in range(COLS):
            if op[cx, cy] == COL["Ford"]:
                wx, wy = cell_world(cx, cy)
                if not lp[min(W - 1, int(wx / RES)), min(H - 1, int(wy / RES))]: op[cx, cy] = COL["Ocean"]

    places = []
    def add_place(name, lon, lat, **kw):
        x, y = world(lon, lat)
        if not (0 <= x < WORLD_W and 0 <= y < WORLD_H): return
        cx, cy = world_cell(x, y)
        places.append(dict(name=name, lon=lon, lat=lat, cell=[cx, cy], **kw))
    for p in atlas["posts"]:
        add_place(p["name"], p["lon"], p["lat"], kind="post", company=p["company"], year=p["year"], note=p.get("note", ""))
    for p in extras.get("places", []):
        q = dict(p); n, lo, la = q.pop("name"), q.pop("lon"), q.pop("lat")
        add_place(n, lo, la, **q)

    rgb2cls = {c: n for n, c in LEGEND}
    G = [[rgb2cls[op[cx, cy]] for cx in range(COLS)] for cy in range(ROWS)]
    layout = lay_out(G, places, extras, atlas)

    os.makedirs(os.path.join(ROOT, "Data", "World"), exist_ok=True)
    out.save(os.path.join(ROOT, "Data", "World", "ground.png"))
    meta = dict(
        about="Generated by tools/world/build_world.py from the Pemmican Empire atlas data. Edit extras.json or ground.png, not this.",
        px_per_km=PX_PER_KM, columns=COLS, rows=ROWS,
        projection=dict(kind="lambert_conformal_conic", lat1=LAT1, lat2=LAT2, lat0=LAT0, lon0=LON0, x0_km=X0, ytop_km=YTOP),
        frame=dict(west=WEST, east=EAST, south=SOUTH, north=NORTH),
        legend=[dict(name=n, rgb=list(c)) for n, c in LEGEND],
        places=places,
        layout=layout,
    )
    json.dump(meta, open(os.path.join(ROOT, "Data", "World", "world.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    # Preview, three pixels a cell wide (cells are twice as wide as tall on screen).
    pv = out.resize((COLS * 4, ROWS * 1), Image.NEAREST)
    d = ImageDraw.Draw(pv)
    try: font = ImageFont.truetype("arial.ttf", 11)
    except OSError: font = None
    for p in places:
        if p.get("year", 0) > 1821: continue
        x, y = p["cell"][0] * 4, p["cell"][1]
        d.rectangle([x - 2, y - 2, x + 2, y + 2], fill=(170, 20, 20) if p.get("company") == "HBC" else (20, 40, 150))
        if p["name"] in ("York Factory", "Oxford House", "Fort William", "Moose Factory", "Cumberland House", "Fort Chipewyan", "Edmonton House", "Fort Astoria", "Fort Gibraltar", "Norway House", "Sault Ste. Marie"):
            d.text((x + 4, y - 6), p["name"], fill=(0, 0, 0), font=font)
    pv.save(os.path.join(ROOT, "docs", "world-ground-preview.png"))
    print("Now re-import so Godot sees the new ground.png:  godot --headless --path . --import")
    print(f"{COLS} x {ROWS} cells ({COLS * ROWS:,}), world {WORLD_W:.0f} x {WORLD_H:.0f} px, {len(places)} places, {len(fords)} fords")

NATION_OF = {
    "Blackfoot": "Blackfoot", "Blood (Kainai)": "Blackfoot", "Peigan (Piikani)": "Blackfoot", "Sarcee (Tsuut'ina)": "Blackfoot", "Gros Ventre": "Blackfoot",
    "Plains Cree": "Cree", "Woodland Cree": "Cree", "Swampy Cree": "Cree", "James Bay Cree (Eeyou)": "Cree", "Oji-Cree": "Cree",
    "Assiniboine": "Assiniboine", "Nakoda (Stoney)": "Assiniboine",
    "Plains Ojibwa (Saulteaux)": "Ojibwe", "Ojibwa (Anishinaabe)": "Ojibwe", "Odawa (Ottawa)": "Ojibwe", "Anishinaabe (Mississauga)": "Ojibwe",
    "Nipissing": "Ojibwe", "Menominee": "Ojibwe", "Potawatomi": "Ojibwe",
    "Dakota (Santee Sioux)": "Sioux", "Yankton (Nakota)": "Sioux", "Lakota (Teton Sioux)": "Sioux",
    "Mandan": "Missouri", "Hidatsa": "Missouri", "Arikara": "Missouri", "Crow": "Missouri", "Cheyenne": "Missouri", "Arapaho": "Missouri", "Shoshone": "Missouri", "Ho-Chunk (Winnebago)": "Missouri",
    "Chipewyan": "Dene", "Dene (Beaver)": "Dene", "Slavey (Dene)": "Dene", "Dakelh (Carrier)": "Dene", "Tsilhqot'in": "Dene", "Sekani": "Dene", "Tahltan": "Dene", "Kaska": "Dene",
    "Kootenay (Ktunaxa)": "Plateau", "Salish (Flathead)": "Plateau", "Secwépemc (Shuswap)": "Plateau", "Syilx (Okanagan)": "Plateau", "Nlaka'pamux": "Plateau",
    "Haida": "Coast", "Tsimshian": "Coast", "Nisga'a": "Coast", "Kwakwaka'wakw": "Coast", "Nuu-chah-nulth": "Coast", "Coast Salish": "Coast", "Tlingit": "Coast",
}

# ---- the York Factory country: where the hand-built places fit -------------------
# The game's posts are built from WorldLayout's shapes (a 22 x 18 fort with a
# wharf running up-right to the water, and so on). Each is put on the nearest
# spot to its real position where the shape lands on dry ground. Searches keep
# the row parity of the old anchor so every shape relative to it is unchanged.

WATER = {"Ocean", "Lake", "River", "Ford"}
def DR(c): x, y = c; return (x, y + 1) if y % 2 == 0 else (x + 1, y + 1)
def DL(c): x, y = c; return (x - 1, y + 1) if y % 2 == 0 else (x, y + 1)
def UL(c): x, y = c; return (x - 1, y - 1) if y % 2 == 0 else (x, y - 1)
def UR(c): x, y = c; return (x, y - 1) if y % 2 == 0 else (x + 1, y - 1)
def Cell(t, r, d):
    c = t
    for _ in range(abs(r)): c = DR(c) if r > 0 else UL(c)
    for _ in range(abs(d)): c = DL(c) if d > 0 else UR(c)
    return c
def Rhombus(t, w, d):
    for i in range(w):
        for j in range(d): yield Cell(t, i, j)

def lay_out(G, places, extras, atlas):
    rows, cols = len(G), len(G[0])
    def g(c):
        x, y = c
        return G[y][x] if 0 <= x < cols and 0 <= y < rows else "Ocean"
    def dry(c): return g(c) not in WATER
    def wet(c): return g(c) in ("Ocean", "Lake", "River")
    byname = {p["name"]: p for p in places}
    def near(name): return tuple(byname[name]["cell"])

    def search(target, old_anchor, ok, radius=110):
        best, bd = None, 1e18
        tx, ty = target
        for dy in range(-radius * 2, radius * 2 + 1):
            y = ty + dy
            if (y - old_anchor[1]) % 2: continue
            for dx in range(-radius, radius + 1):
                c = (tx + dx, y)
                d = (dx * 4) ** 2 + dy ** 2
                if d >= bd or not ok(c): continue
                best, bd = c, d
        if best is None: raise SystemExit(f"nowhere to put the place near {target}")
        return best

    # A fort's footprint, its top corner being the cell; centre it on the post.
    def centred(cell, w, d):
        x, y = cell
        return (x - w // 4, y - d // 2 - w // 2) if False else Cell(cell, -w // 2, -d // 2)

    def footprint_dry(top, w, d, margin=2):
        return all(dry(c) for c in Rhombus(Cell(top, -margin, -margin), w + 2 * margin, d + 2 * margin))

    # York Factory (G. Ray's plan): a 22 x 34 palisade on dry ground with room
    # behind it for Sloops Creek and the powder magazine, the boat shed dry
    # outside the river side, and the boardwalk running out of the river gate
    # (i = 21, j = 21) down-right until its last planks stand in the Hayes.
    def fort_ok(top):
        if not all(dry(c) for c in Rhombus(Cell(top, -11, -7), 22 + 11 + 2, 34 + 7 + 4)): return False
        if not all(dry(c) for c in Rhombus(Cell(top, 24, 2), 5, 6)): return False
        c = DR(Cell(top, 21, 21)); cells = []
        for _ in range(14): cells.append(c); c = DR(c)
        return dry(cells[0]) and dry(cells[1]) and all(wet(x) for x in cells[-3:]) and wet(DR(cells[-1]))
    fort = search(centred(near("York Factory"), 22, 34), (30, 64), fort_ok)
    far = lambda c, px: math.dist(cell_world(*c), cell_world(*fort)) >= px
    nwc = search(centred(near("Nor'Westers' house (York)"), 12, 10), (66, 150), lambda t: far(t, 3500) and footprint_dry(t, 12, 10))
    outpost = search(centred(near("Oxford House"), 12, 9), (36, 396), lambda t: footprint_dry(t, 12, 9))
    # Ground no camp may stand on: each post's footprint and a margin round
    # it (York's palisade is huge, and its creek, sheds and landing too).
    forbidden = set()
    def forbid(top, w, d, m): forbidden.update(Rhombus(Cell(top, -m, -m), w + 2 * m, d + 2 * m))
    forbid(fort, 22 + 14, 34, 9)      # the palisade, the boat shed beyond it, Sloops Creek behind
    forbid(nwc, 12, 10, 5); forbid(outpost, 12, 9, 5)
    def camp_ok(c):
        cells = list(Rhombus(Cell(c, -4, -4), 9, 9))
        return all(dry(x) and x not in forbidden for x in cells)
    placed = [fort, nwc, outpost]
    def clear_of_all(c, px=1500): return all(math.dist(cell_world(*c), cell_world(*o)) >= px for o in placed)
    def camp(name, old, extra=lambda c: True):
        c = search(near(name), old, lambda c: clear_of_all(c) and extra(c) and camp_ok(c))
        placed.append(c)
        return c
    winter = camp("Cree winter camp (York)", (14, 140), lambda c: far(c, 2400))
    summer = camp("Cree summer camp (York)", (23, 100))
    a_winter = camp("Assiniboine winter camp", (16, 424))
    a_summer = camp("Assiniboine summer camp", (22, 118))

    gate = Cell(fort, 21, 21); gate_out = DR(DR(gate))
    ogate = Cell(outpost, 5, 8); ogate_out = DL(DL(ogate))

    # Roads: the cheapest dry way, hugging the Hayes (the real route ran up it).
    import heapq
    hayes = [world(*q) for q in next(r for r in extras["rivers"] if r["name"] == "Hayes")["pts"]]
    def seg_d(p, a, b):
        ax, ay = a; bx, by = b; px, py = p
        vx, vy = bx - ax, by - ay
        t = max(0, min(1, ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy or 1)))
        return math.hypot(px - ax - t * vx, py - ay - t * vy)
    def hayes_d(c):
        p = cell_world(*c)
        return min(seg_d(p, a, b) for a, b in zip(hayes, hayes[1:]))
    def route(a, b, hug):
        nbrs = lambda c: [DR(c), DL(c), UL(c), UR(c)]
        dist = {a: 0}; prev = {}; pq = [(0, a)]
        bw = cell_world(*b)
        done = set()
        while pq:
            _, c = heapq.heappop(pq)
            if c == b: break
            if c in done: continue
            done.add(c); d = dist[c]
            for n in nbrs(c):
                k = g(n)
                if k in ("Ocean", "Lake"): continue
                cost = {"River": 30.0, "Ford": 4.0}.get(k, 1.0)
                if hug: cost *= 0.55 if 250 < hayes_d(n) < 900 else 1.4
                nd = d + cost
                if nd < dist.get(n, 1e18):
                    dist[n] = nd; prev[n] = c
                    w = cell_world(*n)
                    heapq.heappush(pq, (nd + 0.5 * math.dist(w, bw) / 72, n))
        if b not in prev: raise SystemExit(f"no dry way from {a} to {b}")
        path = [b]
        while path[-1] != a: path.append(prev[path[-1]])
        path.reverse()
        crossings.extend(list(c) for c in path if g(c) == "River")
        pts = [cell_world(*c) for c in path[::10]] + [cell_world(*b)]
        return [[round(x), round(y)] for x, y in pts]
    crossings = []
    road = route(gate_out, ogate_out, True)
    trail = route(gate_out, winter, False)

    sites = []
    for s in extras.get("sites", []):
        x, y = world(s["lon"], s["lat"])
        c = world_cell(x, y)
        if not dry(c): c = search(c, c, dry, 20)
        r = None
        if "route" in s:
            r = []
            for lon, lat in s["route"]:
                rc = world_cell(*world(lon, lat))
                if not dry(rc): rc = search(rc, rc, dry, 20)
                r.append(list(rc))
        sites.append(dict(name=s["name"], species=s["species"], cell=list(c), count=s["count"], capacity=s["capacity"], radius=s["radius"], route=r))

    # Portages as a fraction of the way along the road (nearest point to each).
    seglen = [math.dist(a, b) for a, b in zip(road, road[1:])]
    total = sum(seglen) or 1
    portages = []
    for pt in extras.get("portages", []):
        p = world(pt["lon"], pt["lat"]); best, bd, run = 0.0, 1e18, 0.0
        for (a, b), L in zip(zip(road, road[1:]), seglen):
            vx, vy = b[0] - a[0], b[1] - a[1]
            t = max(0, min(1, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / (vx * vx + vy * vy or 1)))
            dd = math.dist(p, (a[0] + t * vx, a[1] + t * vy))
            if dd < bd: bd, best = dd, (run + t * L) / total
            run += L
        portages.append(dict(name=pt["name"], fraction=round(best, 3), cell=list(world_cell(*p))))
    # Every other HBC and NWC post in the atlas, as a small palisaded post
    # (Oxford House's 12 x 9 shape) on dry ground near its real position.
    # Older posts choose first; each keeps clear of everything already set down.
    hand_built = {"York Factory", "Oxford House"}
    posts = []
    for p in sorted(places, key=lambda q: q.get("year", 0)):
        if p.get("kind") != "post" or p["name"] in hand_built or p.get("company") not in ("HBC", "NWC"): continue
        want = Cell(tuple(p["cell"]), -6, -4)
        def post_ok(t):
            c = cell_world(*Cell(t, 6, 4))
            if any(math.dist(c, cell_world(*o)) < 1300 for o in placed): return False
            return footprint_dry(t, 12, 9)
        try: top = search(want, want, post_ok, 30)
        except SystemExit: print(f"  no room for {p['name']}"); continue
        placed.append(Cell(top, 6, 4)); forbid(top, 12, 9, 3)
        posts.append(dict(name=p["name"], company=p["company"], year=p.get("year", 0), closed=extras.get("post_closures", {}).get(p["name"], 0), top=list(top)))
    # The nations (design doc 7.2): a band camp for each of the atlas's
    # peoples at its place in 1811, and a family of the nearest nation
    # camped outside each post (the "homeguard" who lived by the posts).
    peoples = []
    for pe in atlas["peoples"] + extras.get("peoples", []):
        nation = pe.get("nation") or NATION_OF.get(pe["name"])
        if nation is None: continue
        lon, lat = pe["lon"], pe["lat"]
        kf = [k for k in pe.get("kf", []) if k[0] <= 1811]
        if kf: _, lon, lat = kf[-1]
        if not (WEST <= lon <= EAST and SOUTH - 1 <= lat <= NORTH): continue
        peoples.append((pe["name"], nation, lon, lat, pe.get("maj", 0)))
    camps = []
    for name, nation, lon, lat, maj in peoples:
        c = world_cell(*world(lon, lat))
        if not (0 <= c[0] < cols and 0 <= c[1] < rows): continue
        try: centre = search(c, c, lambda x: clear_of_all(x, 1600) and camp_ok(x), 60)
        except SystemExit: print(f"  no room for {name}"); continue
        placed.append(centre)
        camps.append(dict(name=name, nation=nation, kind="band", centre=list(centre), people=12 if maj else 8))
    def nearest_people(cell):
        w = cell_world(*cell)
        return min(peoples, key=lambda q: math.dist(w, world(q[2], q[3])))
    def family_ok(x): return all(dry(y) and y not in forbidden for y in Rhombus(Cell(x, -2, -2), 5, 5))
    for post in posts:
        gate = Cell(tuple(post["top"]), 5, 8)
        out_of_gate = Cell(gate, -1, 6)
        try: centre = search(out_of_gate, out_of_gate, lambda x: clear_of_all(x, 700) and family_ok(x), 25)
        except SystemExit: continue
        placed.append(centre)
        nm, nation, *_ = nearest_people(centre)
        camps.append(dict(name=f"{nm} family at {post['name']}", nation=nation, kind="family", post=post["name"], centre=list(centre), people=4))
    km = lambda a, b: math.dist(cell_world(*a), cell_world(*b)) / PX_PER_KM
    print(f"{len(camps)} camps ({sum(1 for c in camps if c['kind'] == 'band')} bands); {len(posts)} other posts fitted; {len(crossings)} river cells forded; fort {fort}, nwc {nwc} ({km(fort, nwc):.0f} km), oxford {outpost} ({km(fort, outpost):.0f} km), road {len(road)} pts")
    return dict(fort_top=list(fort), nwc_top=list(nwc), outpost_top=list(outpost),
                cree_winter=list(winter), cree_summer=list(summer), assiniboine_winter=list(a_winter), assiniboine_summer=list(a_summer),
                road=road, trail=trail, crossings=crossings, portages=portages, sites=sites, posts=posts, camps=camps)

if __name__ == "__main__":
    main()
