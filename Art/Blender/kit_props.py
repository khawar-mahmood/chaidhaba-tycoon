"""
Chai Dhaba Tycoon: the kit props, built from the user's three Tripo FBX kits.

Open Art/Blender/ChaiTycoon_Stage1.blend, then in Blender's Python:
    exec(open(r'D:/ChaiTycoon/Art/Blender/kit_props.py').read())
    build_all()          # import the kits, cut and place the items, decimate, rebuild the PROP_Kit* collections
    cleanup()            # drop the high-poly sources again (keeps the .blend small)

What it does, item by item (same steps as when the props were first placed, so every prop keeps its spot):
  * imports each kit (one mesh, one material with base colour / normal / metallic / roughness maps)
  * splits it into items by connected parts, levels each item on its base, squares it to its footprint,
    scales it to real metres and puts it on the ground at the origin, front facing -Y  (HI_<Name>)
  * decimates each item to the game's triangle budget, keeping the kit's own UVs and material (LO_<Name>),
    so in the game the props show the kits' actual textures
  * station props are the stall plus a plain side table for the stock; the dining set is the truck-art table
    between two charpai benches; the counter (two per lane), the till and the menu boards (the sign board) are
    kit pieces too
The kit materials carry a custom property unity_mat (KitStalls / KitStreet / KitWood); export_art.py writes
those materials' textures to the game and names the Unity material after it.
"""
import bpy, bmesh, math, os, shutil
import numpy as np
from mathutils import Matrix, Vector

KIT_ROOT = r'D:\Downloads_from_C\DDD\Props\P'
KITS = {   # Unity material: (folder, imported object)
    'KitStalls': ('modular+food+stalls+3d+model', 'tripo_node_61f1da3e'),
    'KitStreet': ('street+furniture+3d+model', 'tripo_node_19b3dc83'),
    'KitWood': ('wooden+furniture+3d+model', 'tripo_node_e544d3c3'),
}
ITEMS = {  # imported object -> items left to right (None: chair and picnic table share a part group)
    'tripo_node_61f1da3e': ['KitChaiStall', 'KitTawaStall', 'KitKarahiStall', 'KitMatkaStall', 'KitDegStall'],
    'tripo_node_19b3dc83': ['KitStreetLamp', 'KitBarrier', 'KitTrashBin', 'KitSignBoard', 'KitPlanter'],
    'tripo_node_e544d3c3': ['KitCharpai', 'KitTableStools', None, 'KitTruckArtTable'],
}
SKIP = set()            # (the sign board's second line of lettering is garbled; it is the menu board)
SCALE = {'KitChaiStall': 8.7, 'KitTawaStall': 8.7, 'KitKarahiStall': 8.7, 'KitMatkaStall': 8.7, 'KitDegStall': 8.7,
         'KitStreetLamp': 3.4 / 0.385, 'KitBarrier': 7.0, 'KitTrashBin': 1.0 / 0.191, 'KitPlanter': 1.3 / 0.211,
         'KitCharpai': 0.42 / (0.68 * 0.0658), 'KitTruckArtTable': 0.73 / 0.1103, 'KitTableStools': 0.75 / 0.0972,
         'KitPlasticChair': 0.88 / 0.149, 'KitPicnicUmbrella': 2.5 / 0.23, 'KitSignBoard': 2.6 / 0.286}
TRIS = {'KitChaiStall': 4000, 'KitTawaStall': 3600, 'KitKarahiStall': 3600, 'KitMatkaStall': 3600, 'KitDegStall': 3600,
        'KitStreetLamp': 700, 'KitBarrier': 900, 'KitTrashBin': 800, 'KitPlanter': 2000, 'KitCharpai': 4000,
        'KitTruckArtTable': 1800, 'KitTableStools': 1800, 'KitPlasticChair': 1200, 'KitPicnicUmbrella': 2000, 'KitSignBoard': 1400}
STATIONS = {'KitChaiStation': ('KitChaiStall', False, None), 'KitTawaStation': ('KitTawaStall', True, None),
            'KitKarahiStation': ('KitKarahiStall', True, None), 'KitMatkaStation': ('KitMatkaStall', False, '#265D7F'),
            'KitDegStation': ('KitDegStall', True, None)}


# ------------------------------------------------------------------ helpers

def coll(name, parent=None):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    p = parent or bpy.context.scene.collection
    if c.name not in [x.name for x in p.children]:
        p.children.link(c)
    return c


def co_of(me):
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co); return co.reshape(-1, 3)


def recenter(me):
    co = co_of(me); mn, mx = co.min(0), co.max(0)
    me.transform(Matrix.Translation((-(mn[0] + mx[0]) / 2, -(mn[1] + mx[1]) / 2, -mn[2]))); me.update()


def rotz(me, deg):
    me.transform(Matrix.Rotation(math.radians(deg), 4, 'Z')); me.update(); recenter(me)


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def flat_mat(hexc):
    """A plain colour material (the side tables). Exported through the shared palette texture."""
    hexc = hexc.lstrip('#').upper(); name = 'C_' + hexc
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    rgb = [srgb_to_lin(int(hexc[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = rgb + [1]; b.inputs['Roughness'].default_value = 0.8
    m.diffuse_color = rgb + [1]
    return m


# ------------------------------------------------------------------ import and cut

def import_kits():
    src = coll('SOURCE_KITS')
    for unity_mat, (folder, node) in KITS.items():
        if bpy.data.objects.get(node): continue
        d = os.path.join(KIT_ROOT, folder)
        fbx = [f for f in os.listdir(d) if f.lower().endswith('.fbx')][0]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(d, fbx))
        for o in [o for o in bpy.data.objects if o not in before]:
            for uc in list(o.users_collection): uc.objects.unlink(o)
            src.objects.link(o)
        m = bpy.data.objects[node].data.materials[0]
        m['unity_mat'] = unity_mat
        m['tex_dir'] = os.path.join(d, fbx[:-4] + '.fbm')


def components(me):
    ed = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1, 2)
    lab = np.arange(len(me.vertices))
    for _ in range(2000):
        a, b = ed[:, 0], ed[:, 1]
        m = np.minimum(lab[a], lab[b]); old = lab.copy()
        np.minimum.at(lab, a, m); np.minimum.at(lab, b, m)
        lab = lab[lab]
        if np.array_equal(lab, old): break
    return lab


def groups_for(o, margin=0.003):
    """Connected parts whose footprints overlap belong to the same item."""
    me = o.data; co = co_of(me)
    uniq, inv = np.unique(components(me), return_inverse=True)
    B = []
    for i in range(len(uniq)):
        pts = co[inv == i]
        B.append([pts[:, 0].min(), pts[:, 0].max(), pts[:, 1].min(), pts[:, 1].max()])
    B = np.array(B)
    grp = list(range(len(B)))
    def find(i):
        while grp[i] != i: grp[i] = grp[grp[i]]; i = grp[i]
        return i
    for i in range(len(B)):
        ov = (B[:, 0] <= B[i, 1] + margin) & (B[:, 1] >= B[i, 0] - margin) & (B[:, 2] <= B[i, 3] + margin) & (B[:, 3] >= B[i, 2] - margin)
        for j in np.nonzero(ov)[0]:
            ri, rj = find(i), find(int(j))
            if ri != rj: grp[rj] = ri
    vgroup = np.array([find(i) for i in range(len(B))])[inv]
    out = []
    for r in sorted(set(vgroup.tolist())):
        sel = vgroup == r; out.append((r, co[sel].min(0)))
    out.sort(key=lambda g: g[1][0])
    return vgroup, [g[0] for g in out], co


def make_item(name, o, vmask, target):
    me0 = o.data
    tri = np.empty(len(me0.polygons) * 3, np.int64); me0.polygons.foreach_get('vertices', tri); tri = tri.reshape(-1, 3)
    uv = np.empty(len(me0.loops) * 2, np.float32); me0.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 3, 2)
    co = co_of(me0)
    fmask = vmask[tri].all(1); t = tri[fmask]; u = uv[fmask]
    used = np.unique(t); remap = -np.ones(len(co), np.int64); remap[used] = np.arange(len(used))
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(used)); me.vertices.foreach_set('co', co[used].ravel())
    nt = remap[t]
    me.loops.add(nt.size); me.loops.foreach_set('vertex_index', nt.ravel())
    me.polygons.add(len(nt)); me.polygons.foreach_set('loop_start', np.arange(0, nt.size, 3)); me.polygons.foreach_set('loop_total', np.full(len(nt), 3))
    me.update(calc_edges=True)
    me.uv_layers.new(name='UVMap').data.foreach_set('uv', u.ravel())
    me.materials.append(me0.materials[0])
    ob = bpy.data.objects.new(name, me); target.objects.link(ob)
    return ob


def align(me):
    """Level on the base (plane through the lowest 3 %), square up to the smallest footprint, centre on the ground."""
    co = co_of(me)
    base = co[co[:, 2] <= np.quantile(co[:, 2], 0.03)]
    a, b, c = np.linalg.lstsq(np.c_[base[:, 0], base[:, 1], np.ones(len(base))], base[:, 2], rcond=None)[0]
    R1 = Vector((-a, -b, 1.0)).normalized().rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
    co1 = np.array([(R1 @ Vector(p))[:] for p in co[::max(1, len(co) // 20000)]])
    best = (1e9, 0)
    for deg in np.arange(-45, 45.01, 0.5):
        r = math.radians(deg); cs, sn = math.cos(r), math.sin(r)
        x = co1[:, 0] * cs - co1[:, 1] * sn; y = co1[:, 0] * sn + co1[:, 1] * cs
        area = (x.max() - x.min()) * (y.max() - y.min())
        if area < best[0]: best = (area, deg)
    me.transform(Matrix.Rotation(math.radians(best[1]), 4, 'Z') @ R1); me.update()
    recenter(me)


def yaw_from_top(me, frac=0.85):
    co = co_of(me); top = co[co[:, 2] > frac * co[:, 2].max()]
    best = (1e9, 0)
    for deg in np.arange(-45, 45.01, 0.5):
        r = math.radians(deg); cs, sn = math.cos(r), math.sin(r)
        x = top[:, 0] * cs - top[:, 1] * sn; y = top[:, 0] * sn + top[:, 1] * cs
        a = (x.max() - x.min()) * (y.max() - y.min())
        if a < best[0]: best = (a, deg)
    return best[1]


def build_hi():
    """HI_<Name>: every kit item at full detail, placed exactly like the game props."""
    hi = coll('HI_ITEMS', coll('SOURCE_KITS'))
    for node, labels in ITEMS.items():
        o = bpy.data.objects[node]
        vgroup, roots, co = groups_for(o)
        for r, label in zip(roots, labels):
            if label in SKIP: continue
            vm = vgroup == r
            parts = [('KitPlasticChair', vm & (co[:, 0] < 0.03)), ('KitPicnicUmbrella', vm & (co[:, 0] >= 0.03))] if label is None else [(label, vm)]
            for lab, m in parts:
                align(make_item('HI_' + lab, o, m, hi).data)
    H = lambda n: bpy.data.objects['HI_' + n].data
    rotz(H('KitTableStools'), yaw_from_top(H('KitTableStools')))
    rotz(H('KitCharpai'), 90); rotz(H('KitTruckArtTable'), 90)
    H('KitPlasticChair').transform(Matrix.Rotation(math.radians(-90), 4, 'Z'))
    for n, s in SCALE.items():
        H(n).transform(Matrix.Diagonal((s, s, s, 1))); recenter(H(n))
    # the picnic table's group caught a piece of the chair: drop parts entirely left of x = -0.95
    _drop_parts(H('KitPicnicUmbrella'), lambda xs: max(xs) < -0.95); recenter(H('KitPicnicUmbrella'))
    # the chair is missing its right side: mirror the complete left half
    me = H('KitPlasticChair'); top = [v.co for v in me.vertices if v.co.z > 0.75]
    cx = (min(v.x for v in top) + max(v.x for v in top)) / 2
    bm = bmesh.new(); bm.from_mesh(me)
    for v in bm.verts: v.co.x -= cx
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
    bmesh.ops.mirror(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], axis='X', merge_dist=0.002)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free(); recenter(me)
    # table + stools: straighten the fourth stool, then the street tables' footprint
    me = H('KitTableStools')
    for v in me.vertices:
        if v.co.x > 0.42 and v.co.z < 0.52: v.co.x -= 0.04; v.co.y += 0.3
    me.transform(Matrix.Diagonal((1.25, 1.25, 1, 1))); me.update()
    # lamp: pole base at the origin, arm along +x
    me = H('KitStreetLamp'); base = [v.co.copy() for v in me.vertices if v.co.z < 0.3]
    bx = sum(v.x for v in base) / len(base); by = sum(v.y for v in base) / len(base)
    me.transform(Matrix.Rotation(math.radians(90), 4, 'Z') @ Matrix.Translation((-bx, -by, 0))); me.update()
    for o in hi.objects: o.location = (0, 0, 0)


def _drop_parts(me, pred):
    bm = bmesh.new(); bm.from_mesh(me); seen = set(); kill = []
    for f in bm.faces:
        if f.index in seen: continue
        stack = [f]; comp = []; seen.add(f.index)
        while stack:
            g = stack.pop(); comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        if pred([v.co.x for g in comp for v in g.verts]): kill += comp
    bmesh.ops.delete(bm, geom=kill, context='FACES'); bm.to_mesh(me); bm.free()


# ------------------------------------------------------------------ game meshes

def decimate(name, tris):
    """LO_<Name>: the item cut down to the game's budget, keeping the kit's UVs and material."""
    src = bpy.data.objects['HI_' + name]
    tmp = bpy.data.objects.new('tmp_dec', src.data); bpy.context.scene.collection.objects.link(tmp)
    md = tmp.modifiers.new('dec', 'DECIMATE'); md.decimate_type = 'COLLAPSE'; md.use_collapse_triangulate = True
    md.ratio = min(1.0, tris / max(1, len(src.data.polygons)))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    bpy.data.objects.remove(tmp)
    old = bpy.data.meshes.get('LO_' + name)
    if old: old.name = 'LO_' + name + '_old'
    me.name = 'LO_' + name
    smooth_by_angle(me)
    if old and old.users == 0: bpy.data.meshes.remove(old)
    return me


def smooth_by_angle(me, deg=40):
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces: f.smooth = True
    for e in bm.edges:
        e.smooth = e.is_manifold and not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(deg))
    bm.to_mesh(me); bm.free()


def side_table(steel, legcol=None):
    """The plain table beside each stall where the cooked stock waits (x 0.38..1.1)."""
    top, leg, shelf = ('#A39896', '#7C7474', '#8F8584') if steel else ('#8B4C22', '#5E3015', '#764120')
    if legcol: leg = legcol
    bm = bmesh.new(); mats = []
    def box(c, s, hexc):
        m = flat_mat(hexc)
        if m not in mats: mats.append(m)
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r['verts']: v.co = Vector((c[0] + v.co.x * s[0], c[1] + v.co.y * s[1], c[2] + v.co.z * s[2]))
        for f in {f for v in r['verts'] for f in v.link_faces}: f.material_index = mats.index(m)
    x0, x1, y0, y1, h = 0.38, 1.1, -0.48, 0.42, 0.9
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    box((cx, cy, h - 0.025), (x1 - x0, y1 - y0, 0.05), top)
    if not steel:
        for k in range(3): box((cx, y0 + (y1 - y0) * (k + 0.5) / 3, h + 0.002), (x1 - x0 - 0.02, 0.012, 0.004), '#6A3818')
    for sx in (x0 + 0.04, x1 - 0.04):
        for sy in (y0 + 0.04, y1 - 0.04):
            box((sx, sy, (h - 0.05) / 2), (0.05, 0.05, h - 0.05), leg)
    box((cx, cy, 0.22), (x1 - x0 - 0.06, y1 - y0 - 0.06, 0.03), shelf)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    me = bpy.data.meshes.new('side_table'); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth = False
    return me


def join(parts, name):
    """One mesh from (mesh, matrix) parts, keeping every part's materials and UVs."""
    bm = bmesh.new(); mats = []
    for me, mtx in parts:
        tmp = me.copy(); tmp.transform(mtx)
        remap = []
        for m in tmp.materials:
            if m not in mats: mats.append(m)
            remap.append(mats.index(m))
        for p in tmp.polygons: p.material_index = remap[p.material_index] if remap else 0
        bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    out = bpy.data.meshes.get(name)
    if out: out.name = name + '_old'
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    return me


def bbox(me):
    co = co_of(me); return Vector(co.min(0)), Vector(co.max(0))


def fit(src, dst_box):
    smn, smx = bbox(src); dmn, dmx = dst_box
    s = [(dmx[i] - dmn[i]) / max(1e-6, smx[i] - smn[i]) for i in range(3)]
    return Matrix.Translation(dmn) @ Matrix.Diagonal((s[0], s[1], s[2], 1)) @ Matrix.Translation(-smn)


def set_prop(name, me, meta=None):
    """Put a mesh in PROP_<name> (one object at the origin); scenes instance the collection."""
    props = bpy.data.collections['PROPS']
    col = bpy.data.collections.get('PROP_' + name) or bpy.data.collections.new('PROP_' + name)
    if col.name not in [c.name for c in props.children]: props.children.link(col)
    old = [o.data for o in col.objects]
    for o in list(col.objects): bpy.data.objects.remove(o)
    for m in old:
        if m.users == 0: bpy.data.meshes.remove(m)
    stale = bpy.data.meshes.get(name)
    if stale and stale.users == 0: bpy.data.meshes.remove(stale)
    me.name = name
    col.objects.link(bpy.data.objects.new(name, me))
    if meta is not None: col['game_meta'] = meta
    if 'textured' in col: del col['textured']


MENU_PANEL = os.path.join(os.path.dirname(bpy.data.filepath), 'Textures', 'KitMenuBoard.png')


def painted_panel(sign):
    """The menu board: the sign board (0.6 scale) with its raised board face and lettering inside the frame
    replaced by a flat panel showing Textures/KitMenuBoard.png, a straight-on capture of the board face with
    the garbled second line of lettering painted over with the board's own wood. Frame, brackets, legs and
    back keep the kit's texture."""
    X0, X1, Z0, Z1, Y = -0.56, 0.56, 0.47, 1.415, 0.045
    bm = bmesh.new(); bm.from_mesh(sign); bm.faces.ensure_lookup_table()
    kill = [f for f in bm.faces if abs(f.calc_center_median().x) < 0.53 and 0.49 < f.calc_center_median().z < 1.40
            and f.calc_center_median().y < 0.07 and f.normal.y < 0.3]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    img = bpy.data.images.get('KitMenuBoard') or bpy.data.images.load(MENU_PANEL)
    img.name = 'KitMenuBoard'; img.filepath = MENU_PANEL; img.reload()
    m = bpy.data.materials.get('TX_KitMenuBoard') or bpy.data.materials.new('TX_KitMenuBoard')
    m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = img
    bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); bs.inputs['Roughness'].default_value = 0.7
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(tx.outputs['Color'], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], out.inputs[0])
    m['unity_mat'] = 'KitMenuBoard'
    mats = list(sign.materials)
    if m not in mats: sign.materials.append(m); mats.append(m)
    uvl = bm.loops.layers.uv.active or bm.loops.layers.uv.new('UVMap')
    vs = [bm.verts.new(p) for p in ((X0, Y, Z0), (X1, Y, Z0), (X1, Y, Z1), (X0, Y, Z1))]
    f = bm.faces.new(vs); f.material_index = mats.index(m); f.smooth = False
    for l, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))): l[uvl].uv = uv
    bm.normal_update()
    if f.normal.y > 0: f.normal_flip()
    bmesh.ops.triangulate(bm, faces=[f])
    bm.to_mesh(sign); bm.free()
    return sign


def build_props(charpai_mesh=None):
    """Rebuild every PROP_Kit* from the LO meshes. charpai_mesh: the bench to use (default the kit's own)."""
    for n, t in TRIS.items():
        decimate(n, t)
    LO = lambda n: bpy.data.meshes['LO_' + n]
    for n in ('KitStreetLamp', 'KitBarrier', 'KitTrashBin', 'KitPlanter', 'KitPlasticChair', 'KitPicnicUmbrella', 'KitTableStools'):
        set_prop(n, LO(n).copy())
    bench = charpai_mesh or LO('KitCharpai')
    set_prop('KitCharpai', bench.copy())
    for sname, (stall, steel, legcol) in STATIONS.items():
        st = LO(stall); mn, mx = bbox(st)
        dx = 0.35 - mx.x                                     # stall on the left, side table on the right
        me = join([(st, Matrix.Translation((dx, 0, 0))), (side_table(steel, legcol), Matrix.Identity(4))], sname + '_new')
        meta = list(bpy.data.collections['PROP_' + sname].get('game_meta', [])) if bpy.data.collections.get('PROP_' + sname) else None
        set_prop(sname, me, meta)
    # dining set: table 2.2 x 0.8, top at 0.735; benches 2.2 x 0.56 at y = +-0.98, seat 0.42
    table = LO('KitTruckArtTable')
    parts = [(table, fit(table, (Vector((-1.1, -0.4, 0)), Vector((1.1, 0.4, 0.735)))))]
    for side in (-1, 1):
        parts.append((bench, fit(bench, (Vector((-1.1, side * 0.98 - 0.28, 0)), Vector((1.1, side * 0.98 + 0.28, bbox(bench)[1].z))))))
    set_prop('KitDiningSet', join(parts, 'KitDiningSet_new'))
    set_prop('KitTruckArtTable', table.copy())
    # the counter: two truck-art tables end to end per lane, top at 0.85; the till: one, 1.1 x 0.8
    set_prop('KitCounter', join([(table, fit(table, (Vector((x0 + 0.01, -0.45, 0)), Vector((x0 + 1.49, 0.45, 0.85))))) for x0 in (-1.5, 0.0)], 'KitCounter_new'))
    till = table.copy(); till.transform(fit(till, (Vector((-0.55, -0.4, 0)), Vector((0.55, 0.4, 0.85)))))
    set_prop('KitTill', till)
    # menu boards: the street kit's sign board at 0.6 scale (1.27 x 1.56 m)
    sign = LO('KitSignBoard').copy(); sign.transform(Matrix.Diagonal((0.6, 0.6, 0.6, 1))); painted_panel(sign)
    set_prop('KitMenuBoard', sign)


URBAN_FBX = r'D:\Downloads_from_C\DDD\GTA\urban waste items 3d model\urban+waste+items+3d+model.fbx'


def build_queue_barrier():
    """KitQueueBarrier: the metal crowd barrier from the user's urban waste items set (base colour only, Unity
    material KitUrban). The set is laid out tilted on an upright sheet and the barrier is skewed, so it is
    levelled on its feet, sheared flat onto one plane, scaled to 1.3 m long (two sections per lane side, 1.07 m
    tall) and its feet are centred under the panel."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=URBAN_FBX)
    o = next(x for x in bpy.data.objects if x not in before and x.type == 'MESH')
    o.data.transform(o.rotation_euler.to_matrix().to_4x4()); o.rotation_euler = (0, 0, 0)
    me0 = o.data; co = co_of(me0)
    uniq, inv = np.unique(components(me0), return_inverse=True)
    cen = np.array([[co[inv == i][:, 0].mean(), co[inv == i][:, 2].mean()] for i in range(len(uniq))])
    # the barrier sits left of centre, second row from the top of the sheet; its parts lie within this box
    pick = np.zeros(len(co), bool)
    for i, (x, z) in enumerate(cen):
        if -0.5 < x < -0.21 and 0.39 < z < 0.64: pick |= inv == i
    tmp = coll('SOURCE_KITS')
    it = make_item('HI_KitQueueBarrier', o, pick, tmp)
    me = it.data
    align(me)
    co = co_of(me); h = co[:, 2].max(); panel = co[co[:, 2] > 0.3 * h]
    a, c, d = np.linalg.lstsq(np.c_[panel[:, 0], panel[:, 2], np.ones(len(panel))], panel[:, 1], rcond=None)[0]
    me.transform(Matrix(((1, 0, 0, 0), (-a, 1, -c, -d), (0, 0, 1, 0), (0, 0, 0, 1)))); recenter(me)
    co = co_of(me); s = 1.3 / np.ptp(co[:, 0])
    me.transform(Matrix.Diagonal((s, s, s, 1))); recenter(me)
    co = co_of(me); h = co[:, 2].max(); py = np.median(co[co[:, 2] > 0.4 * h][:, 1])
    for side in (-1, 1):
        foot = (co[:, 2] < 0.16 * h) & (np.sign(co[:, 0]) == side)
        fy = (co[foot, 1].min() + co[foot, 1].max()) / 2
        for i in range(len(co)):
            if np.sign(co[i, 0]) != side or co[i, 2] >= 0.32 * h: continue
            t = 0.0 if co[i, 2] < 0.16 * h else (co[i, 2] - 0.16 * h) / (0.16 * h)
            me.vertices[i].co.y -= (fy - py) * (1 - t)
    me.update(); recenter(me); smooth_by_angle(me)
    m = me.materials[0]; m['unity_mat'] = 'KitUrban'
    img = next(n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
    d = os.path.join(os.path.dirname(bpy.data.filepath), 'Textures', 'KitUrban'); os.makedirs(d, exist_ok=True)
    src = bpy.path.abspath(img.filepath); dst = os.path.join(d, os.path.basename(src))
    if not os.path.exists(dst): shutil.copyfile(src, dst)
    img.filepath = '//Textures/KitUrban/' + os.path.basename(src); img.reload()
    set_prop('KitQueueBarrier', me.copy())
    bpy.data.objects.remove(o)


def build_canopy():
    """KitCanopy: five of the picnic set's parasols (canopy and pole, without the table) at 1.2x scale in a
    row 3 m apart, placed behind the stations. High enough that the game camera still sees the stations."""
    src = bpy.data.collections['PROP_KitPicnicUmbrella'].objects[0].data
    co = co_of(src); mid = co[(co[:, 2] > 1.0) & (co[:, 2] < 1.5)]
    px, py = np.median(mid[:, 0]), np.median(mid[:, 1])
    bm = bmesh.new(); bm.from_mesh(src)
    drop = [f for f in bm.faces if not (f.calc_center_median().z > 1.0 or
            (f.calc_center_median().x - px) ** 2 + (f.calc_center_median().y - py) ** 2 < 0.07 ** 2)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    par = bpy.data.meshes.new('parasol'); bm.to_mesh(par); bm.free()
    for m in src.materials: par.materials.append(m)
    par.transform(Matrix.Translation((-px, -py, 0)))
    parts = [(par, Matrix.Translation((x, 0, 0)) @ Matrix.Rotation(math.radians(i * 67), 4, 'Z') @ Matrix.Diagonal((1.2, 1.2, 1.2, 1)))
             for i, x in enumerate((-6.0, -3.0, 0.0, 3.0, 6.0))]
    set_prop('KitCanopy', join(parts, 'KitCanopy_new'))
    bpy.data.meshes.remove(par)


def build_all(charpai_mesh=None):
    import_kits(); build_hi(); build_props(charpai_mesh); build_canopy(); build_queue_barrier()


def cleanup():
    """Remove the imported kits and the HI items (the PROP_ collections keep the game meshes)."""
    c = bpy.data.collections.get('SOURCE_KITS')
    if c:
        for o in list(c.all_objects): bpy.data.objects.remove(o)
        for ch in list(c.children_recursive): bpy.data.collections.remove(ch)
        bpy.data.collections.remove(c)
    for me in list(bpy.data.meshes):
        if me.users == 0: bpy.data.meshes.remove(me)
