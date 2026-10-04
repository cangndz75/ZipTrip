"""Render neutral isolated views from the reproducible Golden Lv1 Blender sources."""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Builds/art-gate-02b/review/isolated'
OUT.mkdir(parents=True, exist_ok=True)
ITEMS = [('Passport',1,2),('Towel',1,4),('Shampoo',1,3),
         ('Sunglasses',2,1),('TravelPouch',2,3)]


def aim(camera, target):
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()


for name, width, depth in ITEMS:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / f'ArtSource/GoldenItems/{name}/{name}_Source.blend'))
    obj = bpy.data.objects[f'M_Item_{name}']
    world = bpy.data.worlds.new('Neutral review world')
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.34,.39,.41,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .7
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = 'ORTHO'
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type='AREA', location=(-2,2,7))
    key = bpy.context.object
    key.data.energy = 950
    key.data.shape = 'DISK'; key.data.size = 6
    aim(key,(width/2,-depth/2,0))
    bpy.ops.object.light_add(type='AREA', location=(3,-5,5))
    fill = bpy.context.object
    fill.data.energy = 450
    fill.data.size = 5
    aim(fill,(width/2,-depth/2,0))
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 760
    scene.render.resolution_y = 760
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'AgX'
    center = Vector((width/2,-depth/2,.2))
    camera.data.ortho_scale = max(width,depth)*1.25
    camera.location = center + Vector((0,0,10))
    aim(camera,center)
    scene.render.filepath = str(OUT / f'{name}-top.png')
    bpy.ops.render.render(write_still=True)
    camera.location = center + Vector((max(width,depth)*.80,-max(width,depth)*.70,
                                       max(width,depth)*1.35))
    aim(camera,center)
    camera.data.ortho_scale = max(width,depth)*1.45
    scene.render.filepath = str(OUT / f'{name}-three-quarter.png')
    bpy.ops.render.render(write_still=True)
    obj.rotation_euler.z = math.pi/2
    obj.location = Vector((width/2-depth/2, -(depth/2+width/2),0))
    camera.location = center + Vector((0,0,10))
    aim(camera,center)
    camera.data.ortho_scale = max(width,depth)*1.25
    scene.render.filepath = str(OUT / f'{name}-rotation-90.png')
    bpy.ops.render.render(write_still=True)
