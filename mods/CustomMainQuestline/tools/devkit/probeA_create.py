# PROBE A (data only): one inert engine asset in /Game/Mods/MQ14ProbeA. No controller, no Blueprint,
# no references to game content. Runs only when MQ14ProbeA is the active mod. Then quits the editor.
import os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "probeA_status.txt")
MOD = "MQ14ProbeA"
PATH = "/Game/Mods/" + MOD
NAME = "MQ14ProbeA_InertCurve"


def log(msg):
    unreal.log("MQ14 probeA: " + msg)
    with open(STATUS, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


try:
    if not os.path.exists(os.path.join(unreal.Paths.project_content_dir(), "Mods", MOD, "active.txt")):
        raise RuntimeError(MOD + " is not the active mod; refusing")
    full = PATH + "/" + NAME
    if not unreal.EditorAssetLibrary.does_asset_exist(full):
        curve = unreal.AssetToolsHelpers.get_asset_tools().create_asset(NAME, PATH, unreal.CurveFloat, unreal.CurveFloatFactory())
        if curve is None:
            raise RuntimeError("create_asset returned None")
        if not unreal.EditorAssetLibrary.save_asset(full, only_if_is_dirty=False):
            raise RuntimeError("save failed")
        log("created " + full)
    assets = unreal.EditorAssetLibrary.list_assets(PATH, recursive=True)
    log("assets in mod: " + ", ".join(assets))
    ar = unreal.AssetRegistryHelpers.get_asset_registry()
    deps = ar.get_dependencies(unreal.Name(full), unreal.AssetRegistryDependencyOptions()) or []
    log("dependencies: " + (", ".join(str(d) for d in deps) or "none"))
    log("DONE")
except Exception:
    log("FAILED: " + traceback.format_exc())
finally:
    pass  # editor stays open for the GUI Build mod step
