# Creates the ONLY content of the MQ14CompatProbe mod: an empty Blueprint child of the Dev Kit's ModController.
# Touches nothing outside /Game/Mods/MQ14CompatProbe. Writes a status file next to this script.
import os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "create_probe_status.txt")
MOD_PATH = "/Game/Mods/MQ14CompatProbe"
NAME = "BP_MQ14CompatProbeController"


def log(msg):
    unreal.log("MQ14 create_probe: " + msg)
    with open(STATUS, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


try:
    active = os.path.join(unreal.Paths.project_content_dir(), "Mods", "MQ14CompatProbe", "active.txt")
    if not os.path.exists(active):
        raise RuntimeError("MQ14CompatProbe is not the active mod (no active.txt); refusing to create assets")
    cls = None
    for path in ("/Script/DreamworldMods.ModController",):
        cls = unreal.load_class(None, path)
        if cls:
            break
    if cls is None:
        raise RuntimeError("ModController class not found")
    log("parent class: " + cls.get_path_name())
    full = MOD_PATH + "/" + NAME
    if unreal.EditorAssetLibrary.does_asset_exist(full):
        log("asset already exists: " + full)
    else:
        factory = unreal.BlueprintFactory()
        factory.set_editor_property("parent_class", cls)
        asset = unreal.AssetToolsHelpers.get_asset_tools().create_asset(NAME, MOD_PATH, None, factory)
        if asset is None:
            raise RuntimeError("create_asset returned None")
        unreal.BlueprintEditorLibrary.compile_blueprint(asset)
        if not unreal.EditorAssetLibrary.save_asset(full, only_if_is_dirty=False):
            raise RuntimeError("save_asset failed")
        log("created and saved: " + full)
    log("assets in mod: " + ", ".join(unreal.EditorAssetLibrary.list_assets(MOD_PATH, recursive=True)))
    log("DONE")
except Exception:
    log("FAILED: " + traceback.format_exc())
