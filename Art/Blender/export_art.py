"""
Chai Dhaba Tycoon: export the Blender art to the game's runtime packs.

Open Art/Blender/ChaiTycoon_Stage1.blend, then run this from Blender's Text Editor (Run Script) or with
  blender ChaiTycoon_Stage1.blend --background --python export_art.py

Writes Assets/ChaiTycoon/Resources/Art/art_core.bytes (stage 1 props, every character, the stage 1
environment and layout) and art_stage2..5.bytes (each later stage's new props, environment and layout,
including the crowd), plus the textures in Resources/Art/Tex. Unity loads them with ArtLib
(Scripts/Visual/ArtLib.cs); Editor/ArtMaterials.cs makes the standard URP Lit materials for the textures.

Scene conventions the game relies on:
  * every prop is a collection PROP_<Name> with its objects at the origin, front facing -Y, 1 unit = 1 m;
    a game_meta custom property (list of floats) on the collection is exported as the mesh's meta. Station props
    (Kit*Station) use it for the stock tray origin and the steam point: tray x, y, z, steam x, y, z in game axes
  * Kit* props come from the user's Tripo kits (built by kit_props.py) and keep the kits' own UVs and texture
    sets: a material with the custom property unity_mat is exported as that Unity material, with its base colour,
    normal and metallic/roughness images written to Resources/Art/Tex/<unity_mat>_Base / _Normal / _MetalSmooth
  * every other material is a plain colour: it goes into the shared palette texture (Tex/Palette.png, one texel
    per colour) and the mesh's UVs point at that texel, so props and ground also use the standard Lit shader
  * characters are collections CHR_<Look> with parts <Look>_Torso, _ArmL, _ArmR, _LegL, _LegR built in
    pivot space (legs pivot at hip height 0.45, arms at shoulder height 0.98), facing +Y; the CHR_<Look>
    empty carries custom properties bw (body width) and scale. They stay flat-coloured for the toon shader
  * objects with the custom property game_skip = True are not exported (decorative stock the game spawns)
  * SCENE_StageN collections hold instances of PROP_ collections plus loose environment meshes;
    stage N is laid out around x = 120 * (N - 1); SCENE_StageN_Crowd holds posed character copies
Format: see ArtLib.cs (magic "CDTM", version 4 (UVs as int16 with a per-mesh scale): each mesh is kind 0, flat vertex colours for the toon characters,
or kind 3, a textured mesh with normals, UVs and one submesh per Unity material).
"""
import bpy, struct, math, os, re, shutil, zlib
import numpy as np
from mathutils import Matrix

OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(bpy.data.filepath), '..', '..', 'Assets', 'ChaiTycoon', 'Resources', 'Art'))
TEX_DIR = os.path.join(OUT_DIR, 'Tex')
STAGE_OFFSETS = {1: (0, 0), 2: (120, 0), 3: (240, 0), 4: (360, 0), 5: (480, 0)}
# props the game code builds in stage 1 even when the stage 1 layout doesn't place them
FORCED_CORE = ['TruckPink', 'TruckTeal', 'TruckMarigold', 'CarRed', 'CarWhite', 'CarBlue', 'Car', 'Rickshaw', 'KitQueueBarrier',
               'KitCanopy', 'RickshawStand', 'KitDiningSet', 'KitCounter', 'KitTill', 'KitRickshawStand', 'KitCharpaiTable',
               'KitChaiStation', 'KitTawaStation', 'KitKarahiStation']
STAGE1_FENCES = ('FenceSideBackL', 'FenceSideBackR', 'FenceSideR_')
# keep Blender's bevel segments so props look exactly as modelled (False: one segment, smaller packs)
EXACT_BEVELS = True
# stages 2-5 aren't playable yet, so their packs and textures stay out of the build (smaller download on Poki).
# Their last export is kept in Art/Export/later_stages; set True to export them into Resources again.
EXPORT_LATER_STAGES = False
PAL = 32                    # palette texture is PAL x PAL texels, one per plain colour
MS_SIZE = 1024               # size of the generated metallic/smoothness maps


def srgb8(c):
    c = max(0.0, min(1.0, c))
    s = c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return int(round(s * 255))


def mat_rgb(m):
    if m is None:
        return (255, 255, 255)
    if m.use_nodes:
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if b:
            c = b.inputs['Base Color'].default_value
            return (srgb8(c[0]), srgb8(c[1]), srgb8(c[2]))
    c = m.diffuse_color
    return (srgb8(c[0]), srgb8(c[1]), srgb8(c[2]))


def pstr(s):
    b = s.encode('utf-8'); return struct.pack('<B', len(b)) + b


# ------------------------------------------------------------------ textures

palette = []                 # sRGB colours; index i is texel (i % PAL, i // PAL) counted from the bottom row
written = set()              # files written to TEX_DIR this run


def pal_uv(rgb):
    if rgb not in palette:
        palette.append(rgb)
        assert len(palette) <= PAL * PAL, 'palette texture is full'
    i = palette.index(rgb)
    return ((i % PAL) + 0.5) / PAL, ((i // PAL) + 0.5) / PAL


def write_png(path, rgba):
    """rgba: uint8 array (h, w, 3 or 4), first row at the top."""
    h, w, ch = rgba.shape
    raw = b''.join(b'\x00' + rgba[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    hdr = struct.pack('>IIBBBBB', w, h, 8, 6 if ch == 4 else 2, 0, 0, 0)
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', hdr) + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b'')
    if not os.path.exists(path) or open(path, 'rb').read() != data:
        with open(path, 'wb') as f:
            f.write(data)
    written.add(os.path.basename(path))


def copy_tex(src, dst_name):
    dst = os.path.join(TEX_DIR, dst_name)
    if not os.path.exists(dst) or os.path.getsize(dst) != os.path.getsize(src):
        shutil.copyfile(src, dst)
    written.add(dst_name)


def image_path(img):
    return bpy.path.abspath(img.filepath, library=img.library) if img and img.filepath else None


def pixels(path, size):
    """A texture file's pixels as float32 (size, size, 4), bottom row first (Blender's order)."""
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    px = px.reshape(h, w, 4)
    if w != size:
        f = w // size
        px = px.reshape(size, f, size, f, 4).mean((1, 3))
    return px


def export_material_textures(m, unity_mat, done=set()):
    """Base colour, normal and metallic/smoothness for a textured (kit) material, named after unity_mat."""
    if unity_mat in done:
        return
    done.add(unity_mat)
    imgs = {}
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    for n in m.node_tree.nodes:
        if n.type != 'TEX_IMAGE' or not n.image:
            continue
        name = n.image.name.lower()
        role = 'normal' if 'normal' in name else 'metal' if 'metal' in name else 'rough' if 'rough' in name else \
            'rm' if name.endswith('_rm') or '_rm.' in name else 'base'
        imgs.setdefault(role, n.image)
    base = image_path(imgs.get('base'))
    if base:
        copy_tex(base, unity_mat + '_Base' + os.path.splitext(base)[1].lower())
    nrm = image_path(imgs.get('normal'))
    if nrm:
        copy_tex(nrm, unity_mat + '_Normal' + os.path.splitext(nrm)[1].lower())
    metal, rough = image_path(imgs.get('metal')), image_path(imgs.get('rough'))
    if metal and rough:
        dst = os.path.join(TEX_DIR, unity_mat + '_MetalSmooth.png')
        newest = max(os.path.getmtime(metal), os.path.getmtime(rough))
        if os.path.exists(dst) and os.path.getmtime(dst) >= newest:
            written.add(os.path.basename(dst))
        else:
            mp = pixels(metal, MS_SIZE)[:, :, 0]; rp = pixels(rough, MS_SIZE)[:, :, 0]
            out = np.zeros((MS_SIZE, MS_SIZE, 4), np.uint8)
            out[:, :, 0] = out[:, :, 1] = out[:, :, 2] = np.clip(mp * 255 + 0.5, 0, 255)
            out[:, :, 3] = np.clip((1 - rp) * 255 + 0.5, 0, 255)          # URP Lit: smoothness in alpha
            write_png(dst, out[::-1])


def write_palette():
    px = np.zeros((PAL, PAL, 3), np.uint8)
    for i, rgb in enumerate(palette):
        px[i // PAL, i % PAL] = rgb
    write_png(os.path.join(TEX_DIR, 'Palette.png'), px[::-1])


# ------------------------------------------------------------------ meshes

class PbrMeshOut:
    """A prop or environment mesh for the standard Lit shader: Blender's corner normals, UVs, and one submesh per
    Unity material (a kit's texture set, or 'Palette' for plain colours). Kind 3 in the pack."""
    def __init__(self, name, meta=()):
        self.name = name; self.meta = list(meta); self.verts = []; self.subs = {}; self.index = {}

    def add_object(self, o, xf, dg):
        ev = o.evaluated_get(dg)
        try:
            me = ev.to_mesh()
        except RuntimeError:
            return
        if me is None:
            return
        me.calc_loop_triangles()
        mats = [s.material for s in ev.material_slots] if ev.material_slots else list(o.data.materials)
        nrm = xf.to_3x3().inverted_safe().transposed()
        cn = me.corner_normals
        uvl = me.uv_layers.active.data if me.uv_layers.active else None
        for lt in me.loop_triangles:
            m = mats[lt.material_index] if lt.material_index < len(mats) else None
            m = m.original if m is not None else None          # evaluated copies don't carry custom properties
            um = m.get('unity_mat') if m is not None else None
            if um and uvl:
                export_material_textures(m, um)
            else:
                um = 'Palette'; puv = pal_uv(mat_rgb(m))
            idx = []
            for li in lt.loops:
                vi = me.loops[li].vertex_index
                n = (nrm @ cn[li].vector).normalized()
                uv = tuple(uvl[li].uv) if um != 'Palette' else puv
                key = (o.name, vi, round(n.x, 2), round(n.y, 2), round(n.z, 2), round(uv[0], 5), round(uv[1], 5))
                if key not in self.index:
                    co = xf @ me.vertices[vi].co
                    self.index[key] = len(self.verts)
                    self.verts.append(((co.x, co.z, co.y), (n.x, n.z, n.y), uv))   # Blender -> Unity axes
                idx.append(self.index[key])
            self.subs.setdefault(um, []).append((idx[0], idx[2], idx[1]))   # the axis swap mirrors: flip the winding
        ev.to_mesh_clear()

    def pack(self):
        nb = self.name.encode('utf-8'); out = bytearray()
        out += struct.pack('<B', len(nb)) + nb
        out += struct.pack('<B', len(self.meta)) + b''.join(struct.pack('<f', m) for m in self.meta)
        out += struct.pack('<B', 3)
        mx = max([abs(c) for v in self.verts for c in v[0]] + [1e-3])
        scale = mx / 32000.0
        out += struct.pack('<f', scale)
        # UVs as int16 with their own scale, so tiling textures (the ground) can run past 0..1
        uvmax = max([abs(c) for v in self.verts for c in v[2]] + [1.0])
        uscale = uvmax / 32000.0
        out += struct.pack('<f', uscale)
        subs = sorted(self.subs.items(), key=lambda kv: kv[0] != 'Palette')
        out += struct.pack('<B', len(subs))
        for mat, tris in subs:
            out += pstr(mat) + struct.pack('<I', len(tris))
        out += struct.pack('<I', len(self.verts))
        for p, n, uv in self.verts:
            out += struct.pack('<hhh', *[int(round(c / scale)) for c in p])
            out += struct.pack('<bbb', *[max(-127, min(127, int(round(c * 127)))) for c in n])
            out += struct.pack('<hh', *[int(round(c / uscale)) for c in uv])
        wide = len(self.verts) > 65535
        for mat, tris in subs:
            for a, b, c in tris:
                out += struct.pack('<III' if wide else '<HHH', a, b, c)
        return bytes(out)


class MeshOut:
    """Flat vertex colours with one outline part per object, for the toon-shaded characters. Kind 0."""
    def __init__(self, name, meta=()):
        self.name = name; self.meta = list(meta); self.verts = []; self.tris = []; self.pal = []; self.parts = 0

    def add_object(self, o, xf, dg):
        ev = o.evaluated_get(dg)
        try:
            me = ev.to_mesh()
        except RuntimeError:
            return
        if me is None:
            return
        me.calc_loop_triangles()
        mats = [s.material for s in ev.material_slots] if ev.material_slots else list(o.data.materials)
        part = self.parts; self.parts += 1
        vmap = {}
        for lt in me.loop_triangles:
            mi = lt.material_index
            rgb = mat_rgb(mats[mi] if mi < len(mats) else None)
            if rgb not in self.pal:
                self.pal.append(rgb)
            ci = self.pal.index(rgb)
            idx = []
            for vi in lt.vertices:
                if vi not in vmap:
                    co = xf @ me.vertices[vi].co
                    vmap[vi] = len(self.verts); self.verts.append((co.x, co.z, co.y))   # Blender -> Unity axes
                idx.append(vmap[vi])
            self.tris.append((idx[0], idx[2], idx[1], ci, part))   # the axis swap mirrors, so flip the winding
        ev.to_mesh_clear()

    def pack(self):
        nb = self.name.encode('utf-8'); out = bytearray()
        out += struct.pack('<B', len(nb)) + nb
        out += struct.pack('<B', len(self.meta)) + b''.join(struct.pack('<f', m) for m in self.meta)
        out += struct.pack('<B', 0)   # kind 0: flat vertex colours
        mx = max([abs(c) for v in self.verts for c in v] + [1e-3])
        scale = mx / 32000.0
        out += struct.pack('<f', scale)
        out += struct.pack('<B', len(self.pal)) + b''.join(bytes(p) for p in self.pal)
        out += struct.pack('<I', len(self.verts))
        for v in self.verts:
            out += struct.pack('<hhh', *[int(round(c / scale)) for c in v])
        out += struct.pack('<I', len(self.tris))
        wide = len(self.verts) > 65535
        for a, b, c, ci, p in self.tris:
            out += struct.pack('<III' if wide else '<HHH', a, b, c) + struct.pack('<BH', ci, p)
        return bytes(out)


def write_file(path, meshes, layouts):
    out = bytearray(b'CDTM') + struct.pack('<H', 4) + struct.pack('<H', len(meshes))
    for m in meshes:
        out += m.pack()
    out += struct.pack('<H', len(layouts))
    for lname, entries in layouts:
        out += pstr(lname) + struct.pack('<H', len(entries))
        for e in entries:
            out += pstr(e['name']) + struct.pack('<5f', *e['pos'], e['ry'], e['s']) + pstr(e.get('extra', ''))
    with open(path, 'wb') as f:
        f.write(out)
    return len(out)


def exportable(o):
    return o.type in ('MESH', 'CURVE') and not o.get('game_skip')


def prop_mesh(name, dg):
    col = bpy.data.collections['PROP_' + name]
    m = PbrMeshOut(name, col.get('game_meta', ()))
    for o in sorted(col.objects, key=lambda o: o.name):
        if exportable(o):
            m.add_object(o, o.matrix_world, dg)
    return m


def char_meshes(cid, dg):
    root = bpy.data.objects['CHR_' + cid]
    torso = bpy.data.objects[cid + '_Torso']
    top = max(v.co.z for v in torso.data.vertices)
    out = []
    for part, oname in (('Torso', cid + '_Torso'), ('ArmL', cid + '_ArmL'), ('ArmR', cid + '_ArmR'), ('Leg', cid + '_LegL')):
        meta = (root.get('bw', 1.0), root.get('scale', 1.0), top + 0.08) if part == 'Torso' else ()
        m = MeshOut(f'Chr_{cid}_{part}', meta)
        m.add_object(bpy.data.objects[oname], Matrix.Identity(4), dg)
        out.append(m)
    return out


def env_mesh(name, objs, offset, dg):
    m = PbrMeshOut(name); T = Matrix.Translation((-offset[0], -offset[1], 0))
    for o in objs:
        if exportable(o):
            m.add_object(o, T @ o.matrix_world, dg)
    return m


def layout_entries(cname, offset, crowd=None):
    ents = []
    for o in bpy.data.collections[cname].objects:
        if o.instance_type == 'COLLECTION' and o.instance_collection:
            p = o.location
            ents.append(dict(name=o.instance_collection.name.replace('PROP_', ''), pos=(p.x - offset[0], p.z, p.y - offset[1]),
                             ry=-math.degrees(o.rotation_euler.z), s=o.scale.x))
    if crowd and crowd in bpy.data.collections:
        for r in bpy.data.collections[crowd].objects:
            if r.parent is not None or r.type != 'EMPTY':
                continue
            look = re.match(r'[A-Za-z]+', r.name).group(0)
            legl = next((ch for ch in r.children if 'LegL' in ch.name), None)
            armr = next((ch for ch in r.children if 'ArmR' in ch.name), None)
            pose = 'idle'
            if legl and legl.rotation_euler.x > 1.0:
                pose = 'sit'
            elif legl and abs(legl.rotation_euler.x) > 0.3:
                pose = 'walk'
            if armr and abs(armr.rotation_euler.y) > 1.0:
                pose = 'wave'
            carry = sum(1 for ch in r.children if '_cup' in ch.name)
            p = r.location
            ents.append(dict(name='chr', pos=(p.x - offset[0], p.z, p.y - offset[1]), ry=-math.degrees(r.rotation_euler.z),
                             s=r.scale.x, extra=f'{look}|{pose}|{carry}'))
    return ents


def used(cname):
    return sorted({o.instance_collection.name.replace('PROP_', '') for o in bpy.data.collections[cname].objects
                   if o.instance_type == 'COLLECTION' and o.instance_collection})


def main():
    os.makedirs(TEX_DIR, exist_ok=True)
    palette.clear(); written.clear(); export_material_textures.__defaults__[0].clear()
    saved = {}
    for o in bpy.data.objects if not EXACT_BEVELS else ():   # lighter export: one bevel segment
        for md in o.modifiers:
            if md.type == 'BEVEL':
                saved[(o.name, md.name)] = md.segments; md.segments = 1
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    try:
        core = sorted(set(used('SCENE_Stage1')) | set(FORCED_CORE))
        assigned = set(core)
        looks = sorted(c.name[4:] for c in bpy.data.collections['CHARS'].children)
        meshes = [prop_mesh(p, dg) for p in core]
        for cid in looks:
            meshes += char_meshes(cid, dg)
        # stage 1: the sand ground with the road and kerbs (ENV_Stage1); older files: grass, yard and fences
        if 'ENV_Stage1' in bpy.data.collections:
            env1 = list(bpy.data.collections['ENV_Stage1'].objects)
        else:
            env1 = list(bpy.data.collections['ENV_Ground'].objects) + \
                [o for o in bpy.data.collections['ENV_Fence'].objects if o.name.startswith(STAGE1_FENCES)]
        meshes.append(env_mesh('Env_Stage1', env1, STAGE_OFFSETS[1], dg))
        size = write_file(os.path.join(OUT_DIR, 'art_core.bytes'), meshes, [('Stage1', layout_entries('SCENE_Stage1', STAGE_OFFSETS[1]))])
        print('art_core.bytes', size, 'bytes,', len(meshes), 'meshes')
        for s in (2, 3, 4, 5):
            if not EXPORT_LATER_STAGES:
                stale = os.path.join(OUT_DIR, f'art_stage{s}.bytes')
                for f in (stale, stale + '.meta'):
                    if os.path.exists(f): os.remove(f); print('removed', os.path.basename(f))
                continue
            if f'SCENE_Stage{s}' not in bpy.data.collections:
                continue
            props = [p for p in used(f'SCENE_Stage{s}') if p not in assigned]
            assigned |= set(props)
            ms = [prop_mesh(p, dg) for p in props]
            envobjs = [o for o in bpy.data.collections[f'SCENE_Stage{s}'].objects
                       if o.type in ('MESH', 'CURVE') and not o.name.startswith('S5_Flake')]
            ms.append(env_mesh(f'Env_Stage{s}', envobjs, STAGE_OFFSETS[s], dg))
            lay = layout_entries(f'SCENE_Stage{s}', STAGE_OFFSETS[s], crowd=f'SCENE_Stage{s}_Crowd')
            size = write_file(os.path.join(OUT_DIR, f'art_stage{s}.bytes'), ms, [(f'Stage{s}', lay)])
            print(f'art_stage{s}.bytes', size, 'bytes,', len(ms), 'meshes,', len(lay), 'layout entries')
        write_palette()
        print('palette', len(palette), 'colours; textures', sorted(written))
        # textures no export uses any more (and their Unity .meta files) are removed from the game
        for f in os.listdir(TEX_DIR):
            base = f[:-5] if f.endswith('.meta') else f
            if base not in written:
                os.remove(os.path.join(TEX_DIR, f)); print('removed old', f)
    finally:
        for (on, mn), seg in saved.items():
            o = bpy.data.objects.get(on)
            if o and mn in o.modifiers:
                o.modifiers[mn].segments = seg


main()
