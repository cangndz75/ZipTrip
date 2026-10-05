"""ZT-ART-03: bake native Blender material corrections for the six shipped items.

No scene/post-process changes. All six existing UVs and runtime meshes are preserved.
Outputs are staged under Builds/art03/generated; Unity's Art03FamilyAssets imports them.
Run: blender -b -t 4 -P Tools/Art/build_art03_family.py
"""
from pathlib import Path
import bpy

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Builds/art03/generated'
OUTPUT.mkdir(parents=True, exist_ok=True)

def linear(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4

def color(values):
    return (*[linear(v) for v in values], 1)

def bake_item(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / f'Assets/Art/Models/Items/Art01/M_Item_{name}_Art01.fbx'))
    mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    material = bpy.data.materials.new('ART03 source material')
    material.use_nodes = True
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    source = nodes.new('ShaderNodeTexImage')
    source.image = bpy.data.images.load(str(ROOT / f'Assets/Art/Materials/Items/Art01/T_Item_{name}_BaseColor.jpg'))
    surface = source.outputs['Color']
    if name in ['SweaterOpen', 'TravelPouch']:
        split = nodes.new('ShaderNodeSeparateColor'); links.new(surface, split.inputs['Color'])
        def math(op, a, b):
            node = nodes.new('ShaderNodeMath'); node.operation = op
            if isinstance(a, (int, float)): node.inputs[0].default_value = a
            else: links.new(a, node.inputs[0])
            if isinstance(b, (int, float)): node.inputs[1].default_value = b
            else: links.new(b, node.inputs[1])
            return node.outputs[0]
        if name == 'SweaterOpen':
            mask = math('MULTIPLY', math('GREATER_THAN', split.outputs['Red'], math('MULTIPLY', split.outputs['Green'], 2)),
                       math('GREATER_THAN', split.outputs['Red'], math('MULTIPLY', split.outputs['Blue'], 1.7)))
        else:
            mask = math('MULTIPLY', math('GREATER_THAN', split.outputs['Green'], math('MULTIPLY', split.outputs['Red'], .85)),
                       math('GREATER_THAN', split.outputs['Green'], math('MULTIPLY', split.outputs['Blue'], 1.4)))
        ramp = nodes.new('ShaderNodeValToRGB')
        links.new(split.outputs['Red' if name == 'SweaterOpen' else 'Green'], ramp.inputs['Fac'])
        ramp.color_ramp.elements[0].color = color((.40, .16, .13) if name == 'SweaterOpen' else (.30, .12, .055))
        ramp.color_ramp.elements[1].color = color((.74, .38, .30) if name == 'SweaterOpen' else (.72, .45, .25))
        mix = nodes.new('ShaderNodeMixRGB')
        links.new(mask, mix.inputs[0]); links.new(surface, mix.inputs[1]); links.new(ramp.outputs['Color'], mix.inputs[2])
        surface = mix.outputs[0]
    saturation = {'SweaterOpen': .90, 'Passport': .82, 'Shampoo': .72, 'Towel': .78, 'Sunglasses': .65, 'TravelPouch': .85}[name]
    hue = nodes.new('ShaderNodeHueSaturation')
    hue.inputs['Saturation'].default_value = saturation
    links.new(surface, hue.inputs['Color'])
    emission = nodes.new('ShaderNodeEmission'); links.new(hue.outputs['Color'], emission.inputs['Color'])
    output = nodes.new('ShaderNodeOutputMaterial'); links.new(emission.outputs[0], output.inputs['Surface'])
    image = bpy.data.images.new('ART03 ' + name, 1024, 1024, alpha=False)
    target = nodes.new('ShaderNodeTexImage'); target.image = image
    nodes.active = target
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.context.scene.render.engine = 'CYCLES'
    bpy.context.scene.cycles.samples = 1
    bpy.context.scene.render.bake.margin = 8
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw = str(OUTPUT / f'T_Item_{name}_BaseColor.png')
    image.file_format = 'PNG'; image.save()
    print('[ART03] baked', name, flush=True)

for name in ['SweaterOpen', 'Passport', 'Shampoo', 'Towel', 'Sunglasses', 'TravelPouch']:
    bake_item(name)
