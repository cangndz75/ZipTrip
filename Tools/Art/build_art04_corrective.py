"""Rebuild only the ART-04 pouch and open sweater as constructed meshes.

Blender 5.2: blender -b -P Tools/Art/build_art04_corrective.py
The approved ART-03 atlases/materials and the accepted passport are untouched.
"""
from math import cos, pi, sin
from pathlib import Path

import bpy
import bmesh
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Art/Models/Items/Art04'
ATLAS = ROOT / 'Assets/Art/Materials/Items/Art03'
parts = []


def swatch(name, rgb):
    image = bpy.data.images.load(str(ATLAS / f'T_Item_{name}_BaseColor.png'))
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    sample = pixels.reshape(height, width, 4)[16:height-16:8, 16:width-16:8, :3]
    row, col = np.unravel_index(np.argmin(np.sum((sample - rgb) ** 2, axis=2)), sample.shape[:2])
    return ((16 + col * 8 + .5) / width, (16 + row * 8 + .5) / height)


def mesh(name, vertices, faces, uv):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    layer = data.uv_layers.new()
    for loop in layer.data:
        loop.uv = uv
    for polygon in data.polygons:
        polygon.use_smooth = True
    parts.append(obj)
    return obj


def rounded(width, length, radius, cx=0, cz=.5):
    result = []
    for x, z, start in ((width/2-radius, length/2-radius, 0),
                        (-width/2+radius, length/2-radius, 90),
                        (-width/2+radius, -length/2+radius, 180),
                        (width/2-radius, -length/2+radius, 270)):
        for i in range(9):
            a = (start + i * 11.25) * pi / 180
            result.append((cx + x + radius*cos(a), cz + z + radius*sin(a)))
    return result


def shell(name, levels, uv):
    """A closed, soft-sided volume; levels are y,w,length,radius,x,z."""
    n = 36
    vertices = [(x, y, z) for y, width, length, radius, cx, cz in levels
                for x, z in rounded(width, length, radius, cx, cz)]
    faces = [tuple(range(n-1, -1, -1))]
    for level in range(len(levels)-1):
        for i in range(n):
            a, b = level*n+i, level*n+(i+1)%n
            faces.append((a, b, b+n, a+n))
    faces.append(tuple((len(levels)-1)*n+i for i in range(n)))
    return mesh(name, vertices, faces, uv)


def cord(name, points, radius, uv, close=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for point, position in zip(spline.points, points):
        point.co = (*position, 1)
    spline.use_cyclic_u = close
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.object
    layer = obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in layer.data:
        loop.uv = uv
    parts.append(obj)
    return obj


def box(name, center, size, bevel, uv):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('soft manufactured edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    layer = obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in layer.data:
        loop.uv = uv
    parts.append(obj)
    return obj


def pouch():
    leather = (.42, .66)
    piping = (.15, .56)
    dark = (.49, .90)
    gold = (.96, .04)
    # The body is newly modeled: rounded side walls swell gently between
    # a structural back panel and the front shell. No original organizer mesh.
    shell('main body', [(-.125,.72,.94,.13,-.005,.50),
                        (-.102,.81,1.01,.17,0,.50),
                        (-.034,.84,1.03,.18,.004,.50),
                        (.053,.82,1.01,.19,.002,.50),
                        (.082,.77,.96,.18,-.002,.50)], leather)
    shell('lower structural binding', [(-.128,.70,.93,.13,0,.50),
                                       (-.111,.79,.99,.17,0,.50),
                                       (-.093,.81,1.01,.18,0,.50)], piping)
    # Main zipper is between the two actual compartment shells; the raised
    # band and perimeter piping follow precisely the body contour.
    shell('main closure tape', [(.055,.802,.992,.184,0,.50),
                                (.068,.797,.987,.184,0,.50),
                                (.077,.783,.973,.18,0,.50)], dark)
    outline = rounded(.79,.98,.183,0,.50)
    cord('main compartment edge binding', [(x,.082,z) for x,z in outline], .006, piping, True)
    # A genuinely projected front compartment: separate side gusset, rounded
    # outer panel and visible intervening binding rather than a flat overlay.
    shell('front pocket gusset', [(.075,.635,.59,.115,-.025,.55),
                                  (.106,.686,.64,.12,-.02,.55),
                                  (.163,.686,.64,.12,-.018,.55)], piping)
    shell('front pocket leather', [(.147,.665,.616,.12,-.02,.55),
                                   (.180,.653,.603,.12,-.018,.55),
                                   (.199,.605,.55,.12,-.016,.55)], leather)
    pocket_outline = rounded(.65,.603,.12,-.018,.55)
    cord('front pocket bound edge', [(x,.183,z) for x,z in pocket_outline], .007, dark, True)
    # Zips sit in their seam openings: rear of main body and upper front
    # pocket edge. Teeth remain large enough to read in the phone capture.
    for name, y, z, start, stop, step in (
        ('main', .083, .962, -.31, .31, .035),
        ('pocket', .202, .760, -.266, .244, .032)):
        box(name+' recessed zipper tape', ((start+stop)/2,y,z),
            (stop-start,.009,.023), .003, dark)
        count = round((stop-start)/step)
        for i in range(count):
            x = start + (i+.5)*(stop-start)/count
            box(name+' brass tooth', (x,y+.007,z), (.013,.010,.018), .002, gold)
        side = stop-.055 if name == 'pocket' else start+.055
        box(name+' slider', (side,y+.016,z), (.044,.018,.043), .006, gold)
        # Metal link joins slider and the thick leather grip.
        cord(name+' pull link', [(side,y+.021,z+.014),(side+.016,y+.027,z+.05)], .007, gold)
        box(name+' leather pull', (side+.023,y+.031,z+.081),
            (.045,.025,.078), .011, dark)
    # A narrow hand loop is anchored through shaped leather mounting tabs.
    for z in (.37,.67):
        box('side handle anchor',(-.406,-.005,z),(.055,.17,.092),.017,piping)
    cord('side handle', [(-.420,-.007,.37),(-.46,-.009,.39),
                         (-.475,-.009,.50),(-.46,-.009,.65),(-.420,-.007,.67)],
         .018,dark)
    return 'TravelPouch'


def sweater():
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Art/Models/Items/Art01/M_Item_SweaterOpen_Art01.fbx'))
    source=next(obj for obj in bpy.context.scene.objects if obj.type=='MESH')
    bpy.context.view_layer.objects.active=source
    bpy.ops.object.select_all(action='DESELECT')
    source.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    modifier=source.modifiers.new('retopologized textile surface','DECIMATE')
    modifier.ratio=.80
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Deform the source itself: panels keep their organic authored profile,
    # while the tubular sleeve cross-section is compressed more strongly.
    for vertex in source.data.vertices:
        x,y,z=vertex.co
        sleeve_weight=min(1,max(0,(abs(x)-.23)/.16))
        vertex.co.y=y*(.65-.26*sleeve_weight)
        vertex.co.x=x*(1-.055*sleeve_weight)
    source.data.update()
    parts.append(source)
    coral=swatch('SweaterOpen',(.62,.29,.23))
    for side in (-1,1):
        cord('set-in shoulder join',[(side*.245,.085,.33),
             (side*.308,.09,.40),(side*.338,.075,.49)],.0035,coral)
    cord('broad torso fold',[(-.24,.086,.48),(-.08,.092,.46),
                              (.22,.086,.49)],.0035,coral)
    return 'SweaterOpen'


for build in (pouch,sweater):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts.clear()
    name=build()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=f'M_Item_{name}_Art04'
    obj.data.name=obj.name
    if name == 'TravelPouch':
        for vertex in obj.data.vertices:
            vertex.co.y = -vertex.co.y
    if name == 'TravelPouch':
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
    obj.data.update()
    obj.data.calc_loop_triangles()
    print('[ART04 corrective]',name,'triangles',len(obj.data.loop_triangles),flush=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'M_Item_{name}_Art04.fbx'),
        use_selection=True,object_types={'MESH'},bake_anim=False,
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,
        use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,
        add_leaf_bones=False,path_mode='RELATIVE',embed_textures=False,
        use_custom_props=False)
