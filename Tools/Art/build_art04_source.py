"""Build ART-04 visual sources from the shipped meshes plus real construction.

The source UV/material remains the approved ART-03 palette. All added parts are
geometry and sample broad color areas of the same atlas, so each stays one
renderer and one material. Blender 5.2 headless entry point.
"""
from pathlib import Path
from math import pi, sin, cos
import bpy
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
MODELS=ROOT/'Assets/Art/Models/Items'
TEXTURES=ROOT/'Assets/Art/Materials/Items/Art03'
OUT=MODELS/'Art04'
OUT.mkdir(parents=True,exist_ok=True)

def color_uv(image, rgb):
    im=bpy.data.images.load(str(image))
    # Sample the original UV image away from island boundaries.
    w,h=im.size
    pixels=np.empty(w*h*4,dtype=np.float32)
    im.pixels.foreach_get(pixels)
    samples=pixels.reshape(h,w,4)[16:h-16:8,16:w-16:8,:3]
    delta=samples-np.array(rgb,dtype=np.float32)
    row,col=np.unravel_index(np.argmin(np.sum(delta*delta,axis=2)),samples.shape[:2])
    return ((16+col*8+.5)/w,(16+row*8+.5)/h)

parts=[]

def add(obj,uv):
    layer=obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in layer.data: loop.uv=uv
    parts.append(obj)
    return obj

def bevel_box(name,center,size,r,uv,segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    o=bpy.context.object; o.name=name; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if r:
        m=o.modifiers.new('shaped edge','BEVEL'); m.width=r; m.segments=segments
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.modifier_apply(modifier=m.name)
    for face in o.data.polygons: face.use_smooth=True
    return add(o,uv)

def rounded_panel(name,center,size,r,uv):
    width,depth,height=size
    points=[]
    for cx,cz,start in ((width/2-r,height/2-r,0),(-width/2+r,height/2-r,90),
                        (-width/2+r,-height/2+r,180),(width/2-r,-height/2+r,270)):
        for j in range(5):
            angle=(start+j*22.5)*pi/180
            points.append((cx+r*cos(angle),cz+r*sin(angle)))
    n=len(points)
    vertices=[(center[0]+x,center[1]+side*depth/2,center[2]+z)
              for side in (-1,1) for x,z in points]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    for i in range(n): faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    for face in mesh.polygons: face.use_smooth=True
    return add(obj,uv)

def cord(name,points,r,uv):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'; curve.resolution_u=2
    curve.bevel_depth=r; curve.bevel_resolution=1
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for p,v in zip(spline.points,points): p.co=(*v,1)
    ob=bpy.data.objects.new(name,curve); bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.convert(target='MESH')
    return add(bpy.context.object,uv)

def perimeter(hw,z0,z1,y,uv):
    for x in (-hw,hw): cord('bound vertical',[(x,y,z0),(x,y,z1)],.005,uv)
    for z in (z0,z1): cord('bound horizontal',[(-hw,y,z),(hw,y,z)],.005,uv)

def rounded_outline(hw,z0,z1,y,r,uv):
    path=[]
    for cx,cz,start in ((hw-r,z1-r,0),(-hw+r,z1-r,90),
                        (-hw+r,z0+r,180),(hw-r,z0+r,270)):
        for j in range(5):
            a=(start+j*22.5)*pi/180
            path.append((cx+r*cos(a),y,cz+r*sin(a)))
    path.append(path[0])
    cord('stitched pocket binding',path,.005,uv)

def augment_pouch(uv):
    leather,edge,dark,gold=uv
    for sign in (-1,1):
        face=sign*.119
        # Existing body stays intact. The front panel is separate and sits
        # proud on both sides, preserving rotation readability.
        rounded_panel('separate front-pocket gusset',(-.02,face+sign*.015,.61),(.68,.032,.49),.085,edge)
        rounded_panel('raised leather front pocket',(-.02,face+sign*.039,.62),(.64,.032,.45),.075,leather)
        rounded_outline(.32,.39,.84,face+sign*.057,.075,edge)
        bevel_box('front zip cloth channel',(-.02,face+sign*.063,.42),(.60,.011,.028),.003,dark,1)
        for i in range(17):
            x=-.30+i*.035
            bevel_box('front zipper tooth',(x,face+sign*.072,.42),(.014,.008,.022),.002,gold,1)
        bevel_box('brass zipper slide',(.20,face+sign*.08,.42),(.055,.015,.049),.006,gold,1)
        bevel_box('thick leather pull',(.22,face+sign*.087,.50),(.05,.022,.10),.012,dark)
        # Main-closure band sits behind the pocket and is legible in profile.
        bevel_box('main zip dark channel',(0,face+sign*.026,.17),(.79,.023,.042),.005,dark,1)
        for i in range(17):
            x=-.32+i*.04
            bevel_box('main zipper tooth',(x,face+sign*.041,.17),(.013,.009,.023),.001,gold,1)
        bevel_box('main zipper slide',(-.24,face+sign*.048,.17),(.054,.016,.05),.005,gold,1)
    # Side strap is attached to the casing, not a floating accent.
    bevel_box('carry tab',(-.43,0,.55),(.075,.31,.14),.019,dark)

def augment_sweater(uv):
    coral,cream,navy=uv
    # Reduce inflated source depth while retaining X/Z silhouette and UVs.
    # A broad, subtle fold supplies construction without covering the
    # approved coral/cream body or changing sleeve silhouette.
    for sign in (-1,1):
        face=sign*.078
        cord('broad torso fold',[(-.24,face+sign*.017,.48),(-.08,face+sign*.022,.46),(.22,face+sign*.017,.49)],.005,coral)

def augment_passport(uv):
    navy,page,gold=uv
    for sign in (-1,1):
        face=sign*.102
        # Page block protrudes slightly at fore-edge and top/bottom so that it
        # reads as bound paper from the gameplay camera and both rotations.
        bevel_box('page block',(0,face+sign*.014,.51),(.66,.026,.91),.015,page)
        bevel_box('rounded leather cover',(-.012,face+sign*.033,.51),(.73,.028,.97),.026,navy)
        bevel_box('reinforced spine',(-.338,face+sign*.049,.51),(.075,.036,.94),.015,navy)
        perimeter(.325,.10,.91,face+sign*.053,gold)
        bevel_box('restrained upper gold rule',(0,face+sign*.053,.65),(.24,.008,.013),.003,gold,1)
        bevel_box('restrained lower gold rule',(0,face+sign*.053,.35),(.18,.008,.009),.002,gold,1)
    for z in (.10,.91):
        bevel_box('page edge',(.02,0,z),(.62,.17,.014),.004,page,1)

for name,build,swatches in [
    ('TravelPouch',augment_pouch,[(.42,.66),(.15,.56),(.49,.90),(.96,.04)]),
    ('SweaterOpen',augment_sweater,[(.62,.29,.23),(.86,.77,.64),(.10,.16,.27)]),
    ('Passport',augment_passport,[(.08,.12,.22),(.78,.73,.61),(.58,.39,.16)])]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(MODELS/'Art01'/f'M_Item_{name}_Art01.fbx'))
    source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bpy.context.view_layer.objects.active=source
    bpy.ops.object.select_all(action='DESELECT'); source.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    reduction={'TravelPouch':.62,'SweaterOpen':.88,'Passport':.75}[name]
    modifier=source.modifiers.new('budgeted source retopology','DECIMATE')
    modifier.ratio=reduction
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    if name=='SweaterOpen':
        for vert in source.data.vertices: vert.co.y*=.72
    atlas=TEXTURES/f'T_Item_{name}_BaseColor.png'
    uv=swatches if name=='TravelPouch' else [color_uv(atlas,c) for c in swatches]
    parts.clear(); build(uv)
    bpy.ops.object.select_all(action='DESELECT')
    source.select_set(True)
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=source
    bpy.ops.object.join()
    source.name=f'M_Item_{name}_Art04'
    source.data.name=source.name
    source.data.calc_loop_triangles()
    print('[ART04]',name,'triangles',len(source.data.loop_triangles),flush=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'M_Item_{name}_Art04.fbx'),use_selection=True,
        object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',
        global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='OFF',
        use_tspace=False,add_leaf_bones=False,path_mode='RELATIVE',
        embed_textures=False,use_custom_props=False)
