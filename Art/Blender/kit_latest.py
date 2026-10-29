"""
Chai Dhaba Tycoon: the second set of the user's Tripo kits ("Latest Props"), used for the first dhaba so it
matches the reference picture. Run in Blender with ChaiTycoon_Stage1.blend open (after kit_props.py's helpers):
    exec(open(r'D:/ChaiTycoon/Art/Blender/kit_props.py').read())
    exec(open(r'D:/ChaiTycoon/Art/Blender/kit_latest.py').read())
    build_latest()
Each kit is a 3 x 2 grid of items. Every connected part goes to the nearest grid cell; each item is levelled,
squared up, turned to face -Y, scaled to real size (metres), reduced where it is heavy, and becomes a
PROP_<Name> collection. Each kit's material keeps its own texture set (unity_mat names below).
"""
LATEST_ROOT = r'D:\ChaiTycoon\Art\Source\LatestProps'
# folder: (unity material, [six items, back row left to right, then front row])
LATEST = {
    'assorted_kitchen_props_3d_model': ('KitAssorted', ['ItemParatha', 'ItemSamosa', 'KitRegister', 'ItemCoins', 'KitBucket', 'KitGasCylinder']),
    'colorful_kitchen_props_3d_model': ('KitColorful', ['KitTukTuk', 'KitChaiCart', 'KitFridge', 'KitBench', 'KitWaterDrum', 'KitCupSign']),
    'miniature_kitchen_counter': ('KitKitchen', ['KitStoveChai', 'KitStoveTawa', 'KitStoveFryer', 'KitServeCounter', 'KitCharpaiTeal', 'KitSink']),
    'stone_wall_props_3d_model': ('KitStone', ['KitWallLong', 'KitWallMid', 'KitWallCorner', 'KitRugPost', 'KitRugRack', 'KitStoneLow']),
    'stylized_nature_assets_3d_model': ('KitNature', ['KitTree', 'KitBush', 'KitPottedPlant', 'KitGrass', 'KitRock', 'KitRailRug']),
}
# real size: (axis, metres) the item is scaled to; axis x = width, y = depth, z = height
SIZE = {
    'ItemParatha': ('x', 0.36), 'ItemSamosa': ('x', 0.22), 'KitRegister': ('x', 0.45), 'ItemCoins': ('x', 0.3),
    'KitBucket': ('z', 0.36), 'KitGasCylinder': ('z', 0.65),
    'KitTukTuk': ('y', 2.6), 'KitChaiCart': ('x', 1.3), 'KitFridge': ('z', 1.7), 'KitBench': ('x', 1.5),
    'KitWaterDrum': ('z', 0.75), 'KitCupSign': ('x', 1.2),
    'KitStoveChai': ('x', 1.3), 'KitStoveTawa': ('x', 1.3), 'KitStoveFryer': ('x', 1.3), 'KitServeCounter': ('xyz', (3.0, 0.8, 0.9)),
    'KitCharpaiTeal': ('xyz', (2.0, 1.1, 0.62)), 'KitSink': ('x', 0.9),
    'KitWallLong': ('z', 1.1), 'KitWallMid': ('z', 1.1), 'KitWallCorner': ('z', 1.1), 'KitRugPost': ('z', 1.5),
    'KitRugRack': ('z', 1.5), 'KitStoneLow': ('z', 0.4),
    'KitTree': ('z', 4.2), 'KitBush': ('z', 1.0), 'KitPottedPlant': ('z', 0.85), 'KitGrass': ('z', 0.45),
    'KitRock': ('z', 0.8), 'KitRailRug': ('z', 1.1),
}
YAW = {}          # extra turn (degrees) per item so its front faces -Y, filled in after looking at them
MAX_TRIS = 4000   # heavier items are reduced to this, unless they have their own budget below
TRIS_LATEST = {'KitStoveChai': 18000, 'KitStoveTawa': 18000, 'KitStoveFryer': 18000, 'KitServeCounter': 12000,
               'KitTukTuk': 7780, 'KitSink': 9000, 'KitRegister': 3148, 'KitBucket': 5324, 'KitTree': 5646,
               'KitWallCorner': 4170, 'KitWallLong': 4558}


def grid_items(o, k=6, iters=20):
    me = o.data; co = co_of(me)
    uniq, inv = np.unique(components(me), return_inverse=True)
    cen = np.array([co[inv == i].mean(0) for i in range(len(uniq))]); w = np.bincount(inv).astype(float)
    mn, mx = co.min(0), co.max(0)
    C = np.array([[mn[0] + (mx[0] - mn[0]) * (cx + 0.5) / 3, mn[1] + (mx[1] - mn[1]) * (cy + 0.5) / 2] for cy in (1, 0) for cx in range(3)])
    for _ in range(iters):
        lab = ((cen[:, None, :2] - C[None]) ** 2).sum(-1).argmin(1)
        for j in range(k):
            m = lab == j
            if m.any(): C[j] = (cen[m, :2] * w[m, None]).sum(0) / w[m].sum()
    return lab[inv]


def import_latest():
    src = coll('LATEST')
    for folder, (umat, names) in LATEST.items():
        node = 'L_' + folder
        if bpy.data.objects.get(node): continue
        d = os.path.join(LATEST_ROOT, folder)
        fbx = [f for f in os.listdir(d) if f.endswith('.fbx')][0]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(d, fbx))
        for o in [o for o in bpy.data.objects if o not in before]:
            for uc in list(o.users_collection): uc.objects.unlink(o)
            src.objects.link(o)
            if o.type == 'MESH':
                o.data.transform(o.matrix_basis.to_3x3().to_4x4()); o.rotation_euler = (0, 0, 0); o.scale = (1, 1, 1)
                o.location = (1000, 0, 0); o.name = node


def tag_material(m, umat, folder):
    """Name the kit material for Unity and point its images at copies next to the .blend."""
    m['unity_mat'] = umat
    d = os.path.join(os.path.dirname(bpy.data.filepath), 'Textures', umat); os.makedirs(d, exist_ok=True)
    for n in m.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and not n.image.filepath.startswith('//Textures'):
            src = bpy.path.abspath(n.image.filepath); dst = os.path.join(d, os.path.basename(src))
            if not os.path.exists(dst): shutil.copyfile(src, dst)
            n.image.filepath = '//Textures/' + umat + '/' + os.path.basename(src); n.image.reload()


def build_latest_items():
    work = coll('LATEST_ITEMS', coll('LATEST'))
    for folder, (umat, names) in LATEST.items():
        o = bpy.data.objects['L_' + folder]
        tag_material(o.data.materials[0], umat, folder)
        lab = grid_items(o)
        for j, name in enumerate(names):
            it = make_item('LI_' + name, o, lab == j, work)
            me = it.data
            align(me)
            if YAW.get(name): rotz(me, YAW[name])
            axis, metres = SIZE[name]
            co = co_of(me)
            if axis == 'xyz':                     # exact box (stretches the item)
                s = [metres[k] / max(1e-6, np.ptp(co[:, k])) for k in range(3)]
                me.transform(Matrix.Diagonal((s[0], s[1], s[2], 1)))
            else:
                i = 'xyz'.index(axis); s = metres / max(1e-6, np.ptp(co[:, i]))
                me.transform(Matrix.Diagonal((s, s, s, 1)))
            recenter(me)
            it.location = (0, 0, 0)


def reduce(name):
    me = bpy.data.objects['LI_' + name].data
    budget = TRIS_LATEST.get(name, MAX_TRIS)
    if len(me.polygons) <= budget:
        out = me.copy(); smooth_by_angle(out); return out
    tmp = bpy.data.objects.new('tmp_dec', me); bpy.context.scene.collection.objects.link(tmp)
    md = tmp.modifiers.new('dec', 'DECIMATE'); md.decimate_type = 'COLLAPSE'; md.use_collapse_triangulate = True
    md.ratio = budget / len(me.polygons)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    bpy.data.objects.remove(tmp)
    smooth_by_angle(out)
    return out


def steam_point(me, kind):
    """Where steam rises, in game axes (x, height, z): over the kettle, or over the pan / fryer basket."""
    co = co_of(me)
    if kind == 'kettle':
        top = co[co[:, 2] > co[:, 2].max() - 0.12]
        return [float(top[:, 0].mean()), float(co[:, 2].max()), float(top[:, 1].mean())]
    mid = co[(co[:, 2] > 0.7)]
    return [float(mid[:, 0].mean()), 1.15, float(mid[:, 1].mean())]


def decimated(me, n):
    """A reduced copy of a mesh (keeps UVs and materials)."""
    if len(me.polygons) <= n: return me.copy()
    tmp = bpy.data.objects.new('tmp_dec', me); bpy.context.scene.collection.objects.link(tmp)
    md = tmp.modifiers.new('dec', 'DECIMATE'); md.decimate_type = 'COLLAPSE'; md.use_collapse_triangulate = True
    md.ratio = n / len(me.polygons)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    bpy.data.objects.remove(tmp); smooth_by_angle(out)
    return out


def build_latest_props():
    LI = lambda n: bpy.data.objects['LI_' + n].data
    for n in ('KitWallLong', 'KitWallMid', 'KitWallCorner', 'KitStoneLow', 'KitRugRack', 'KitRugPost', 'KitTree', 'KitBush',
              'KitPottedPlant', 'KitGrass', 'KitRock', 'KitRailRug', 'KitFridge', 'KitBench', 'KitWaterDrum', 'KitCupSign',
              'KitSink', 'KitBucket', 'KitGasCylinder', 'KitChaiCart', 'KitRegister', 'ItemParatha', 'ItemSamosa', 'ItemCoins'):
        set_prop(n, reduce(n))
    counter = reduce('KitServeCounter')
    set_prop('KitCounter', counter.copy())                                   # 3.0 x 0.8, top at 0.9
    piece = decimated(counter, 3000)                                         # side pieces and the till
    # stations: the stove on the left, a small painted counter piece on the right for the cooked stock
    for sname, stove, kind in (('KitChaiStation', 'KitStoveChai', 'kettle'), ('KitTawaStation', 'KitStoveTawa', 'pan'),
                               ('KitKarahiStation', 'KitStoveFryer', 'pan')):
        st = reduce(stove); mn, mx = bbox(st); dx = 0.33 - mx.x
        side = (piece, fit(piece, (Vector((0.38, -0.45, 0)), Vector((1.1, 0.45, 0.88)))))
        me = join([(st, Matrix.Translation((dx, 0, 0))), side], sname + '_new')
        sp = steam_point(st, kind); sp[0] += dx
        set_prop(sname, me, [0.74, 0.89, -0.2, sp[0], sp[1], sp[2]])
        bpy.data.meshes.remove(st)
    # till: a short counter piece with the cash register on it, its screen facing the cashier (+Y)
    reg = reduce('KitRegister')
    till = join([(piece, fit(piece, (Vector((-0.55, -0.4, 0)), Vector((0.55, 0.4, 0.9))))),
                 (reg, Matrix.Translation((0.18, 0.05, 0.9)) @ Matrix.Rotation(math.pi, 4, 'Z'))], 'KitTill_new')
    set_prop('KitTill', till)
    # dine-in: the charpai is table and seat at once (guests sit on its long edges, food goes on the weave)
    # (the teal charpai falls apart when reduced: keep the clean bench painted with its texture once that exists)
    old = bpy.data.collections.get('PROP_KitCharpaiTable')
    if not (old and old.objects and any(m.name == 'TX_KitCharpaiTeal' for m in old.objects[0].data.materials)):
        set_prop('KitCharpaiTable', reduce('KitCharpaiTeal'))
    # the tuk-tuk replaces the rickshaw everywhere (customers, traffic, decor); like the old one, front along +Y
    tt = reduce('KitTukTuk'); tt.transform(Matrix.Rotation(math.pi, 4, 'Z')); recenter(tt)
    s = 2.4 / bbox(tt)[1].y / 2 if False else 2.4 / (bbox(tt)[1].y - bbox(tt)[0].y)
    tt.transform(Matrix.Diagonal((s, s, s, 1))); recenter(tt)
    set_prop('Rickshaw', tt)
    bpy.data.meshes.remove(counter); bpy.data.meshes.remove(piece); bpy.data.meshes.remove(reg)


def build_latest():
    import_latest(); build_latest_items(); build_latest_props()
