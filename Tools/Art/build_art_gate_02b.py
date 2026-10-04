"""Rebuild the five Golden Lv1 physical items from deterministic Blender geometry."""
import bpy
import math
from pathlib import Path
from mathutils import Matrix

bpy.context.preferences.filepaths.save_version = 0

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "ArtSource/GoldenItems"
MODELS = ROOT / "Assets/Art/Models/Items/Final"
TEXTURES = ROOT / "Assets/Art/Materials/Items/GoldenLv1Final"
REVIEWS = ROOT / "Builds/art-gate-02b/blender"
for folder in (SOURCES, MODELS, REVIEWS):
    folder.mkdir(parents=True, exist_ok=True)

PARTS = []


def reset(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    PARTS.clear()
    image = bpy.data.images.load(str(TEXTURES / f"T_Item_{name}_BaseColor.png"), check_existing=True)
    material = bpy.data.materials.new(f"M_Item_{name}_Preview")
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    tex = material.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = image
    material.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = .73 if name in ('Towel', 'TravelPouch') else .55
    return material


def swatch(obj, mat, index):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
    verts = obj.data.vertices
    xs = [v.co.x for v in verts]; ys = [v.co.y for v in verts]
    low_x, low_y = min(xs), min(ys)
    span_x, span_y = max(xs)-low_x, max(ys)-low_y
    col, row = index % 4, index // 4
    for poly in obj.data.polygons:
        for loop_index in poly.loop_indices:
            vertex = verts[obj.data.loops[loop_index].vertex_index].co
            u = (vertex.x-low_x)/span_x if span_x > 1e-5 else .5
            v = (vertex.y-low_y)/span_y if span_y > 1e-5 else .5
            uv.data[loop_index].uv = ((col + .08 + .84*u)/4, 1-(row + .08 + .84*v)/4)
    PARTS.append(obj)
    return obj


def box(name, loc, size, bevel, mat, colour, segments=4):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new('Soft sewn edge', 'BEVEL')
    modifier.width = bevel
    modifier.segments = segments
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    return swatch(obj, mat, colour)


def tube(name, points, radius, mat, colour, resolution=5):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'; curve.resolution_u = 16
    curve.bevel_depth = radius; curve.bevel_resolution = resolution
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for point, xyz in zip(spline.points, points):
        point.co = (*xyz, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    return swatch(bpy.context.object, mat, colour)


def ring(name, center, rx, ry, z, radius, mat, colour, count=44):
    points = [(center[0]+rx*math.cos(2*math.pi*i/count),
               center[1]+ry*math.sin(2*math.pi*i/count), z)
              for i in range(count+1)]
    return tube(name, points, radius, mat, colour)


def uv_sphere(name, loc, scale, mat, colour, segments=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for face in obj.data.polygons:
        face.use_smooth = True
    return swatch(obj, mat, colour)


def passport(m):
    box('cream page block', (.51,-1.01,.11), (.77,1.70,.16), .075,m,3)
    box('navy cover', (.5,-1,.225), (.88,1.88,.17), .09,m,0)
    box('spine fold', (.14,-1,.24), (.10,1.78,.18), .045,m,1)
    tube('spine highlight',[(.20,-.14,.315),(.20,-1.86,.315)],.009,m,4)
    ring('gold travel globe',(.55,-.82),.22,.22,.321,.023,m,2)
    tube('gold meridian',[(.55,-.61,.326),(.48,-.71,.326),(.46,-.82,.326),
                            (.48,-.94,.326),(.55,-1.03,.326)],.015,m,2)
    tube('gold meridian opposite',[(.55,-.61,.326),(.62,-.71,.326),(.64,-.82,.326),
                                     (.62,-.94,.326),(.55,-1.03,.326)],.015,m,2)
    tube('gold equator',[(.34,-.82,.326),(.76,-.82,.326)],.014,m,2)
    box('passport gold underline',(.55,-1.31,.32),(.32,.025,.012),.008,m,2)


def towel(m):
    # Constant-width cloth roll with compressed, slightly uneven ends.
    sections = [(-.10,.12),(-.20,.31),(-.34,.37),(-.70,.39),(-1.4,.39),
                (-2.1,.385),(-2.8,.39),(-3.52,.38),(-3.80,.33),(-3.90,.15)]
    count = 20
    vertices = []
    for y, radius in sections:
        for i in range(count):
            t = 2*math.pi*i/count
            wobble = 1+.025*math.sin(5*t+y*3)
            vertices.append((.5+radius*math.cos(t)*wobble,y,
                             max(.025,.27+radius*.56*math.sin(t))))
    faces = []
    for j in range(len(sections)-1):
        for i in range(count):
            n = (i+1)%count
            faces.append((j*count+i,j*count+n,(j+1)*count+n,(j+1)*count+i))
    faces.extend([tuple(reversed(range(count))),
                  tuple((len(sections)-1)*count+i for i in range(count))])
    mesh = bpy.data.meshes.new('soft rolled cotton')
    mesh.from_pydata(vertices,[],faces); mesh.update()
    body = bpy.data.objects.new('rolled cotton body',mesh)
    bpy.context.collection.objects.link(body)
    for face in mesh.polygons: face.use_smooth = True
    swatch(body,m,0)
    # A loose overlapping fabric flap prevents the perfect hard-case silhouette.
    box('loose cloth flap',(.51,-2.00,.475),(.61,3.14,.065),.032,m,1,3)
    tube('flap edge',[(.19,-.44,.475),(.21,-1.30,.49),(.20,-2.30,.49),
                       (.24,-3.54,.47)],.018,m,5,3)
    for y in (-.76,-3.24):
        # Wide woven stripe is modelled as a curved band across the visible crown.
        points = [(.5+.385*math.cos(t),y,.225+.205*math.sin(t))
                  for t in [math.pi*i/22 for i in range(23)]]
        tube('teal woven stripe',points,.054,m,2)
        points = [(.5+.36*math.cos(t),y-.12,.23+.20*math.sin(t))
                  for t in [math.pi*i/22 for i in range(23)]]
        tube('coral narrow stripe',points,.025,m,3)
        box('wide woven teal band',(.5,y,.517),(.72,.18,.020),.025,m,2,3)
        box('narrow coral band',(.5,y-.14,.525),(.72,.06,.018),.015,m,3,3)
    # Exposed roll and curl are made broad enough to survive the overhead camera.
    ring('visible rolled end',(.5,-.23),.27,.11,.485,.032,m,1)
    spiral = []
    for i in range(50):
        t = i/49*math.pi*4
        r = .23*(1-i/51)
        spiral.append((.5+r*math.cos(t),-.23+r*.40*math.sin(t),.495))
    tube('rolled cloth spiral',spiral,.021,m,5,3)


def shampoo(m):
    box('coral bottle',(.5,-1.75,.16),(.71,2.17,.29),.145,m,0,6)
    box('bottle shoulder',(.5,-.53,.16),(.57,.40,.27),.12,m,1)
    box('neck',(.5,-.29,.14),(.32,.25,.22),.06,m,4)
    box('dark teal cap',(.5,-.16,.18),(.47,.31,.33),.085,m,2,5)
    for x in (.32,.39,.46,.53,.60,.67):
        tube('cap grip',[(x,-.04,.353),(x,-.25,.353)],.007,m,6,2)
    box('warm cream bottle label',(.5,-1.82,.314),(.58,.88,.025),.07,m,3)
    tube('mountain travel cue',[(.31,-1.9,.334),(.43,-1.69,.334),(.51,-1.83,.334),
                                (.61,-1.64,.334),(.70,-1.90,.334)],.022,m,2)
    box('label footer',(.5,-2.12,.338),(.30,.023,.01),.007,m,5)


def sunglasses(m):
    # Solid lenses sit under thick rounded frames; the bridge and folded arms read from above.
    for cx in (.53,1.47):
        box('dark lens', (cx,-.48,.135),(.69,.62,.11),.18,m,2,6)
        ring('tortoise lens frame',(cx,-.48),.397,.36,.205,.075,m,0)
        tube('upper brow',[(cx-.36,-.25,.244),(cx,-.15,.248),(cx+.36,-.25,.244)],.050,m,1)
        box('tiny light reflection',(cx-.12,-.34,.195),(.18,.035,.009),.015,m,5)
    tube('heavy bridge',[(.91,-.29,.22),(1,-.23,.25),(1.09,-.29,.22)],.068,m,0)
    for x, sign in ((.17,-1),(1.83,1)):
        tube('folded temple',[(x,-.34,.20),(x+sign*.055,-.65,.18),
                               (x-sign*.16,-.88,.15)],.055,m,4)
    box('left hinge',(.16,-.34,.205),(.12,.13,.11),.03,m,6)
    box('right hinge',(1.84,-.34,.205),(.12,.13,.11),.03,m,6)


def travel_pouch(m):
    box('padded mustard body',(1,-1.5,.20),(1.82,2.82,.38),.28,m,0,6)
    box('raised front textile panel',(1,-1.5,.397),(1.58,2.50,.045),.24,m,3)
    tube('stitched inset',[(.40,-.37,.427),(1.60,-.37,.427),
                          (1.76,-.53,.427),(1.76,-2.47,.427),
                          (1.60,-2.63,.427),(.40,-2.63,.427),
                          (.24,-2.47,.427),(.24,-.53,.427),
                          (.40,-.37,.427)],.012,m,1,3)
    tube('zipper track',[(.30,-.34,.426),(.55,-.24,.435),(1.46,-.24,.435),
                         (1.70,-.34,.426)],.052,m,2)
    for i in range(17):
        x = .40 + i*.075
        tube('zipper tooth',[(x,-.20,.445),(x,-.28,.445)],.012,m,5,2)
    box('zipper slider',(1.38,-.23,.47),(.16,.16,.055),.035,m,6)
    tube('zipper pull',[(1.38,-.18,.49),(1.48,-.07,.48),(1.54,-.13,.45)],.034,m,6)
    tube('front compartment seam',[(.32,-1.88,.436),(.48,-1.96,.44),
                                   (1.52,-1.96,.44),(1.68,-1.88,.436)],.032,m,1)
    box('front pocket zipper',(1,-1.91,.457),(.35,.055,.025),.018,m,6)
    tube('side carry tab',[(.12,-1.46,.24),(-.015,-1.46,.24),
                            (-.015,-1.75,.24),(.12,-1.75,.24)],.06,m,4)


def combined_mesh(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = f'M_Item_{name}'
    # Mesh vertices are in world coordinates, with canonical pivot at 0,0,0.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    triangles = sum(len(poly.vertices)-2 for poly in obj.data.polygons)
    print(f'{name}: {triangles} triangles, {len(obj.data.vertices)} vertices')
    return obj


def export(name, obj):
    source_dir = SOURCES / name
    source_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source_dir / f'{name}_Source.blend'))
    export_obj = obj.copy()
    export_obj.data = obj.data.copy()
    bpy.context.collection.objects.link(export_obj)
    # Blender XY is the item face. Unity's board lies in XZ with Y as height.
    axis_map = Matrix(((-1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    export_obj.data.transform(axis_map)
    bpy.ops.object.select_all(action='DESELECT')
    export_obj.select_set(True)
    bpy.context.view_layer.objects.active = export_obj
    bpy.ops.export_scene.fbx(filepath=str(MODELS / f'M_Item_{name}.fbx'),
                             use_selection=True, object_types={'MESH'},
                             bake_anim=False, axis_forward='Y', axis_up='Z',
                             global_scale=1, path_mode='RELATIVE', embed_textures=False)
    bpy.data.objects.remove(export_obj, do_unlink=True)


for name, builder in [('Passport',passport),('Towel',towel),('Shampoo',shampoo),
                      ('Sunglasses',sunglasses),('TravelPouch',travel_pouch)]:
    material = reset(name)
    builder(material)
    obj = combined_mesh(name)
    export(name,obj)
