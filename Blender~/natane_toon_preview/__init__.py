# SPDX-License-Identifier: MIT
"""Natane Toon Preview: preview the Natane Toon Shader (Unity) look in Blender EEVEE."""

bl_info = {
    "name": "Natane Toon Preview",
    "author": "Natane",
    "version": (1, 0, 0),
    "blender": (4, 2, 0),
    "location": "Properties > Material > Natane Toon",
    "description": "Preview the Natane Toon Shader (Unity) look in EEVEE and import Unity .mat files",
    "category": "Material",
}

if "bpy" in locals():
    import importlib
    for _module in (unity_mat, node_group, outline, props, importer, exporter, operators, ui):
        importlib.reload(_module)
else:
    from . import unity_mat, node_group, outline, props, importer, exporter, operators, ui

import bpy

_modules = (props, operators, ui)


def register():
    for module in _modules:
        module.register()


def unregister():
    for module in reversed(_modules):
        module.unregister()
