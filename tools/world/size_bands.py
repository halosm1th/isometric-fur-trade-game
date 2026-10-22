"""Give the country's camps the size of their peoples' bands in 1811, shrunk
by about half so the corners of the map still have bands in them, and add
the people to the scene as families (Cast/Camps, NpcSpawn markers).

    python tools/world/size_bands.py

Everyone already in a camp keeps their name and place; only newcomers are
added, so a re-run after a size change adds the difference. Sizes are set in
SIZES below by people (a band of the plains is bigger than one of the woods;
the Mandan and Hidatsa live in towns of earth lodges); families by a post go
to six. The lodges are laid out as World/Camps.LodgeCells lays them, about
eight to a lodge.
"""
import json, os, random, re, sys, unicodedata
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import build_world as B

WORLD = os.path.join(ROOT, "Data", "World", "world.json")
SCENE = os.path.join(ROOT, "Scenes", "World", "YorkFactoryExterior.tscn")

# People in a band about 1811, about halved. Earlier sizes stay as a floor.
SIZES = [
    ("Mandan", 120), ("Hidatsa", 110), ("Arikara", 100),
    ("Six Nations", 100),
    ("Blackfoot", 90), ("Blood", 85), ("Peigan", 90), ("Gros Ventre", 70), ("Sarcee", 45),
    ("Assiniboine", 80), ("Nakoda (Stoney)", 50),
    ("Lakota", 90), ("Yankton", 70), ("Dakota", 70),
    ("Plains Cree", 75), ("Beaver Hills Cree", 70), ("Plains Ojibwa", 50),
    ("Crow", 85), ("Cheyenne", 70), ("Arapaho", 60), ("Shoshone", 60),
    ("Meskwaki", 65), ("Ho-Chunk", 50), ("Potawatomi", 50), ("Menominee", 45), ("Odawa", 45), ("Wyandot", 45),
    ("Haida", 70), ("Tlingit", 65), ("Tsimshian", 65), ("Nisga'a", 55), ("Kwakwaka'wakw", 65), ("Nuu-chah-nulth", 60), ("Coast Salish", 60),
    ("Kootenay", 45), ("Salish (Flathead)", 50), ("Nlaka'pamux", 45), ("Secw", 45), ("Syilx", 45),
    ("Métis", 45),
    ("Swampy Cree", 35), ("Woodland Cree", 35), ("James Bay Cree", 30), ("Oji-Cree", 30),
    ("Anishinaabe (Mississauga)", 40), ("Ojibwa (Anishinaabe)", 40), ("Nipissing", 30),
    ("Chipewyan", 35), ("Dene (Beaver)", 30), ("Sekani", 25), ("Dakelh", 35), ("Tahltan", 30), ("Tsilhqot'in", 35),
]
FAMILY = 6

def size_for(camp):
    if camp["kind"] == "family": return max(camp["people"], FAMILY)
    for key, n in SIZES:
        if camp["name"].startswith(key) or key in camp["name"]: return max(camp["people"], n)
    return max(camp["people"], 35)

FACTION = dict(Cree=1, Assiniboine=5, Ojibwe=6, Dene=7, Blackfoot=8, Sioux=9, Plateau=10, Coast=11, Missouri=12, Metis=13, Haudenosaunee=15, SaukFox=16)
PLAINS = {"Assiniboine", "Sioux", "Blackfoot", "Missouri"}
GUNS = {"Cree": 0.6, "Assiniboine": 0.5, "Ojibwe": 0.6, "Metis": 0.9, "Haudenosaunee": 0.7, "SaukFox": 0.4, "Sioux": 0.3, "Blackfoot": 0.3, "Missouri": 0.25, "Dene": 0.3, "Plateau": 0.15, "Coast": 0.3}
ROLE = dict(Hunter=6, BandWoman=7, BandDaughter=8, Child=19)

# ---- the lodges (World/Camps.LodgeCells) --------------------------------------------
def lodge_cells(centre, n):
    out = [B.Cell(centre, 3, 0), B.Cell(centre, 0, 3), B.Cell(centre, -3, 0), B.Cell(centre, 0, -3), B.Cell(centre, 3, 3)][:n]
    for i, j in ((-3, -3), (3, -3), (-3, 3)):
        if len(out) >= n: return out
        out.append(B.Cell(centre, i, j))
    r = 6
    while len(out) < n:
        for i, j in ((r, 0), (0, r), (-r, 0), (0, -r), (r, r), (-r, -r), (r, -r), (-r, r), (r, r // 2), (-r, -r // 2), (r // 2, -r), (-r // 2, r)):
            if len(out) >= n: break
            out.append(B.Cell(centre, i, j))
        r += 3
    return out

def lodges(camp, people):
    return 1 if camp["kind"] == "family" else max(5, (people + 7) // 8)

# ---- names ------------------------------------------------------------------------------
CREE_M = ["Amisk", "Wapos", "Moswa", "Nikik", "Sihkos", "Atim", "Kinosew", "Piyesiw", "Maskwa", "Mahihkan", "Kihiw", "Mistatim", "Wapistan", "Pisiskiw", "Okimaw", "Nipin", "Kisik", "Sakihk"]
CREE_F = ["Niska", "Sisip", "Pihew", "Wapan", "Pisim", "Nipiy", "Kona", "Mikisiw", "Wapikwan", "Askiy", "Kisikaw", "Mostos"]
OJIB_M = ["Makwa", "Maiingan", "Migizi", "Amik", "Waabooz", "Nigig", "Animikii", "Ajijaak", "Ogimaa", "Zhingwaak", "Bineshii", "Mishiikenh"]
OJIB_F = ["Waabigwan", "Giizis", "Nibi", "Ojiig", "Zhaawan", "Miskwaa", "Aniibiish", "Memengwaa", "Waaban"]
ADJ = ["White", "Black", "Red", "Spotted", "Grey", "Many", "Old", "Young", "Yellow", "Big", "Long", "Calling", "Two", "Three", "Little", "Lone", "Running", "Standing", "Sitting", "Curly", "Iron", "Low", "High", "Crooked", "Bald", "Feathered", "Painted", "Brave", "Bright", "Dark"]
ANIMAL = ["Bull", "Wolf", "Calf", "Swan", "Hawk", "Crane", "Otter", "Beaver", "Owl", "Fox", "Turtle", "Moose", "Raven", "Badger", "Lynx", "Marten", "Loon", "Eagle", "Sturgeon", "Bear", "Elk", "Horse", "Dog", "Crow", "Thunder", "Cloud", "Arrow", "Lance", "Robe", "Shield", "Pipe", "Buffalo", "Mink", "Weasel", "Coyote"]
WOMAN_THING = ["Swan", "Calf", "Otter", "Elk", "Grass", "Moon", "Morning", "Weasel", "Bird", "Fawn", "Berry", "Star", "Snow", "Willow", "Cloud", "Corn", "Sweetgrass", "Shell", "Rain", "Sky", "Water", "Flower", "Bead", "Quill"]
METIS_M = ["Baptiste", "Joseph", "Pierre", "Antoine", "Louis", "Francois", "Michel", "Jean-Baptiste", "Charles", "Alexis"]
METIS_F = ["Marie", "Josephte", "Angelique", "Marguerite", "Genevieve", "Isabelle", "Louise", "Madeleine"]
METIS_S = ["Falcon", "Grant", "Desjarlais", "Cardinal", "Lagimodiere", "Laroque", "Delorme", "Nolin", "Bruce", "Sayer", "Pangman", "Dumont", "Ouellette", "Berthelet", "Morin", "Parenteau"]

text = open(SCENE, encoding="utf-8", newline="").read()
used = set(re.findall(r'(?m)^DisplayName = "([^"]+)"', text))
def node_name(n): return re.sub(r"[^A-Za-z0-9 \-]", "", unicodedata.normalize("NFKD", n).encode("ascii", "ignore").decode()).strip() or "Someone"

def name_for(rng, nation, female):
    for _ in range(400):
        if nation == "Metis":
            n = f"{rng.choice(METIS_F if female else METIS_M)} {rng.choice(METIS_S)}"
        elif nation == "Cree" and rng.random() < 0.45:
            n = rng.choice(CREE_F if female else CREE_M)
        elif nation == "Ojibwe" and rng.random() < 0.45:
            n = rng.choice(OJIB_F if female else OJIB_M)
        elif female:
            n = f"{rng.choice(WOMAN_THING)} Woman" if rng.random() < 0.35 else f"{rng.choice(ADJ)} {rng.choice(WOMAN_THING)} Woman"
        else:
            n = f"{rng.choice(ADJ)} {rng.choice(ANIMAL)}"
        if n not in used: used.add(n); return n
    n = f"{rng.choice(ADJ)} {rng.choice(ANIMAL)} the Younger"
    while n in used: n += " II"
    used.add(n); return n

# ---- who is in each camp now ----------------------------------------------------------
# A camp's markers carry PostName = its name; their folder is the node's parent.
blocks = re.split(r"\n(?=\[)", text)
present = {}   # camp name -> (folder path, count, node names)
for b in blocks:
    m = re.match(r'\[node name="([^"]+)" type="Node2D" parent="(Cast/Camps/[^"]+)"', b)
    if not m or 'script = ExtResource("30_npcspawn")' not in b: continue
    pm = re.search(r'(?m)^PostName = "([^"]*)"', b)
    hm = re.search(r'(?m)^Home = (\d+)', b)
    if not pm or not hm or hm.group(1) != "6": continue
    folder, cnt, names = present.get(pm.group(1), (m.group(2), 0, set()))
    names.add(m.group(1))
    present[pm.group(1)] = (folder, cnt + 1, names)

world = json.load(open(WORLD, encoding="utf-8"))
gen = []; added = 0; total = 0
for camp in world["layout"]["camps"]:
    target = size_for(camp)
    camp["people"] = target
    total += target
    nation = camp["nation"]
    if camp["name"] not in present or nation not in FACTION: continue
    folder, have, nodes = present[camp["name"]]
    need = target - have
    if need <= 0: continue
    rng = random.Random("band:" + camp["name"])
    c = tuple(camp["centre"])
    beds = lodge_cells(c, lodges(camp, target))
    fac = FACTION[nation]
    menf, womf = ("32_assin" if nation in PLAINS else "32_cree"), "32_creew"
    k = have
    def marker(name, role, female, age, extra, bed, work, inv):
        nn = node_name(name)
        while nn in nodes: nn += " 2"
        nodes.add(nn)
        bx, by = B.cell_world(*bed); wx, wy = B.cell_world(*work)
        lines = [f'[node name="{nn}" type="Node2D" parent="{folder}"]', f"position = Vector2({round(bx)}, {round(by)})", 'script = ExtResource("30_npcspawn")',
                 f'DisplayName = "{name}"', f"Role = {ROLE[role]}", f"Faction = {fac}", "Home = 6", f'PostName = "{camp["name"]}"',
                 f'Frames = ExtResource("{womf if female else menf}")']
        if role == "Child": lines.append("SpriteScale = %g" % round(0.55 + 0.02 * age, 2))
        elif female: lines.append("SpriteScale = 0.76")
        if female: lines.append("Female = true")
        lines.append(f"Age = {age}")
        if inv: lines.append(f'Inventory = "{inv}"')
        lines += extra
        gen.append("\n".join(lines) + "\n")
        gen.append(f'[node name="Work" type="Marker2D" parent="{folder}/{nn}"]\nposition = Vector2({round(wx - bx)}, {round(wy - by)})\n')
        return nn
    while need > 0:
        lodge = beds[(k // 8) % len(beds)] if camp["kind"] != "family" else beds[0]
        def near(r): return B.Cell(c, rng.randint(-r, r), rng.randint(-r, r))
        gun = rng.random() < GUNS.get(nation, 0.3)
        man_inv = ("Musket, Powder, " if gun else "Bow, ") + "Knife, HideCoat, DriedMeat:2"
        if camp["kind"] == "family" or need < 2:
            # A child of the family already there.
            girl = rng.random() < 0.5
            age = rng.randint(2, 13)
            marker(name_for(rng, nation, girl), "Child", girl, age, [], lodge, near(3), "")
            need -= 1; k += 1; continue
        # A family: a man, his wife, and their children; now and then an old one with them.
        hage = rng.randint(19, 55)
        hn = marker(name_for(rng, nation, False), "Hunter", False, hage, [], lodge, near(5), man_inv)
        wn = marker(name_for(rng, nation, True), "BandWoman", True, max(16, hage - rng.randint(0, 8)), [], lodge, near(2), "HideCoat, Knife")
        # Link the wife to her husband (NodePaths are siblings in the camp's folder).
        gen[-2] = gen[-2].rstrip("\n") + f'\nSpouse = NodePath("../{hn}")\n'
        need -= 2; k += 2
        kids = min(need, max(0, int(rng.gauss(2.2, 1.3))))
        for _ in range(kids):
            girl = rng.random() < 0.5
            age = rng.randint(1, max(2, min(17, hage - 17)))
            role = "Child" if age < 14 else ("BandDaughter" if girl else "Hunter")
            inv = "" if role == "Child" else ("HideCoat, Knife" if girl else "Bow, Knife, HideCoat")
            marker(name_for(rng, nation, girl), role, girl, age, [f'Father = NodePath("../{hn}")'], lodge, near(3), inv)
            need -= 1; k += 1
        if need > 0 and rng.random() < 0.15:
            old_f = rng.random() < 0.6
            marker(name_for(rng, nation, old_f), "BandWoman" if old_f else "Hunter", old_f, rng.randint(56, 74), [], lodge, near(2), "HideCoat")
            need -= 1; k += 1
    added += target - have

raw = open(WORLD, encoding="utf-8").read()
open(WORLD, "w", encoding="utf-8", newline="\n").write(json.dumps(world, ensure_ascii=False, indent=1) + ("\n" if raw.endswith("\n") else ""))
text = text.rstrip("\n") + "\n\n" + "\n".join(gen)
open(SCENE, "w", encoding="utf-8", newline="").write(text)
print(f"{added} people added to the camps; the country's camps now hold {total}")
