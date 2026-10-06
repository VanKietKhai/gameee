# PROBE C step 1: creates BP_MQ14ProbeCController (child of /Script/DreamworldMods.ModController) with NO logic,
# exactly like Probe B. The single BeginPlay log node is then added by hand in the Blueprint editor GUI.
# Refuses to run unless MQ14ProbeC is the active mod. The editor stays open.
import os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "probeC_status.txt")
MOD = "MQ14ProbeC"
PATH = "/Game/Mods/" + MOD
NAME = "BP_MQ14ProbeCController"


def log(msg):
    unreal.log("MQ14 probeC: " + msg)
    with open(STATUS, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


try:
    if not os.path.exists(os.path.join(unreal.Paths.project_content_dir(), "Mods", MOD, "active.txt")):
        raise RuntimeError(MOD + " is not the active mod; refusing")
    full = PATH + "/" + NAME
    if unreal.EditorAssetLibrary.does_asset_exist(full):
        log("exists " + full)
    else:
        f = unreal.BlueprintFactory()
        f.set_editor_property("parent_class", unreal.load_class(None, "/Script/DreamworldMods.ModController"))
        bp = unreal.AssetToolsHelpers.get_asset_tools().create_asset(NAME, PATH, None, f)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        if not unreal.EditorAssetLibrary.save_asset(full, only_if_is_dirty=False):
            raise RuntimeError("save failed")
        log("created " + full)
    log("DONE")
except Exception:
    log("FAILED: " + traceback.format_exc())
