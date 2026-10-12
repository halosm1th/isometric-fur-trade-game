"""Register prop PNGs as TileSet sources in terrain_tileset.tres.

  python tileset.py NAME [NAME ...]      props/<NAME>.png -> a new source
  python tileset.py --list               what is registered, and at what id

A prop is only half wired when the file is cut: the painter addresses it by
the source id it has in the tileset, so it has to be a source. Each one is
added the way the hand-made ones are - one 128x256 atlas, one tile, the same
texture origin - and the id it gets is printed for the C# side.

Godot must have imported the PNG first (`--headless --path . --import`), since
the uid comes out of its .import file. Running it twice is harmless: a prop
already registered keeps the id it has.
"""
import io, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TRES = os.path.join(ROOT, "assets", "tiles", "landscape", "terrain_tileset.tres")
PROPS = "res://assets/tiles/props/"


def read():
    return io.open(TRES, encoding="utf-8", newline="").read()


def registered(text):
    """{prop name: source id} for everything already in the tileset."""
    tex = dict((m.group(2), m.group(1)) for m in
               re.finditer(r'\[ext_resource type="Texture2D" uid="[^"]+" path="res://assets/tiles/props/([^"]+)\.png" id="([^"]+)"\]', text))
    atlas = {}
    for m in re.finditer(r'\[sub_resource type="TileSetAtlasSource" id="([^"]+)"\]\ntexture = ExtResource\("([^"]+)"\)', text.replace("\r\n", "\n")):
        if m.group(2) in tex:
            atlas[m.group(1)] = tex[m.group(2)]
    out = {}
    for m in re.finditer(r'sources/(\d+) = SubResource\("([^"]+)"\)', text):
        if m.group(2) in atlas:
            out[atlas[m.group(2)]] = int(m.group(1))
    return out


def uid_of(name):
    p = os.path.join(ROOT, "assets", "tiles", "props", name + ".png.import")
    if not os.path.exists(p):
        raise SystemExit(f"{name}: no .import - run Godot with --headless --path . --import first")
    m = re.search(r'uid="(uid://[^"]+)"', io.open(p, encoding="utf-8").read())
    return m.group(1)


def add(names):
    text = read()
    nl = "\r\n" if "\r\n" in text else "\n"
    have = registered(text)
    free = max(int(m.group(1)) for m in re.finditer(r'sources/(\d+) = ', text)) + 1
    for name in names:
        if name in have:
            print(f"  {name}: already source {have[name]}")
            continue
        if not os.path.exists(os.path.join(ROOT, "assets", "tiles", "props", name + ".png")):
            raise SystemExit(f"{name}: no such prop")
        tid, aid = "tex_p" + str(free), "Atlas_p" + str(free)
        ext = f'[ext_resource type="Texture2D" uid="{uid_of(name)}" path="{PROPS}{name}.png" id="{tid}"]'
        last = text.rfind("[ext_resource")
        end = text.index("\n", last) + 1
        text = text[:end] + ext + nl + text[end:]
        atlas = (f'[sub_resource type="TileSetAtlasSource" id="{aid}"]{nl}'
                 f'texture = ExtResource("{tid}"){nl}'
                 f'texture_region_size = Vector2i(128, 256){nl}'
                 f'0:0/0 = 0{nl}'
                 f'0:0/0/texture_origin = Vector2i(0, 86){nl}{nl}')
        at = text.index("[resource]")
        text = text[:at] + atlas + text[at:]
        text = text.rstrip("\r\n") + nl + f'sources/{free} = SubResource("{aid}"){nl}'
        print(f"  {name}: source {free}")
        free += 1
    io.open(TRES, "w", encoding="utf-8", newline="").write(text)


if __name__ == "__main__":
    if "--list" in sys.argv:
        for k, v in sorted(registered(read()).items(), key=lambda kv: kv[1]):
            print(f"{v:4d}  {k}")
    else:
        add(sys.argv[1:])
