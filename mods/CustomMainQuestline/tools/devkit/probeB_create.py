# PROBE B (empty official ModController): creates BP_MQ14ProbeBController (child of the Dev Kit's
# /Script/DreamworldMods.ModController) with NO logic, then documents its definition and compares with the
# official Funcom DLC controllers' parent classes (asset registry, read-only). Quits the editor at the end.
import json, os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "probeB_status.txt")
DOC = os.path.join(HERE, "probeB_definition.json")
MOD = "MQ14ProbeB"
PATH = "/Game/Mods/" + MOD
NAME = "BP_MQ14ProbeBController"
doc = {}


def log(msg):
    unreal.log("MQ14 probeB: " + msg)
    with open(STATUS, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


def prop(obj, name):
    try:
        v = obj.get_editor_property(name)
        return str(v)
    except Exception as e:
        return "n/a (%s)" % type(e).__name__


try:
    if not os.path.exists(os.path.join(unreal.Paths.project_content_dir(), "Mods", MOD, "active.txt")):
        raise RuntimeError(MOD + " is not the active mod; refusing")
    base = unreal.load_class(None, "/Script/DreamworldMods.ModController")
    full = PATH + "/" + NAME
    if not unreal.EditorAssetLibrary.does_asset_exist(full):
        f = unreal.BlueprintFactory()
        f.set_editor_property("parent_class", base)
        bp = unreal.AssetToolsHelpers.get_asset_tools().create_asset(NAME, PATH, None, f)
        unreal.BlueprintEditorLibrary.compile_blueprint(bp)
        if not unreal.EditorAssetLibrary.save_asset(full, only_if_is_dirty=False):
            raise RuntimeError("save failed")
        log("created " + full)
    bp = unreal.load_asset(full)
    gen = bp.generated_class()
    cdo = unreal.get_default_object(gen)
    doc["asset"] = full
    doc["generated_class"] = gen.get_path_name()
    doc["parent_class"] = str(unreal.BlueprintEditorLibrary.get_blueprint_parent_class(bp).get_path_name()) if hasattr(unreal.BlueprintEditorLibrary, "get_blueprint_parent_class") else base.get_path_name()
    doc["implemented_interfaces"] = prop(bp, "implemented_interfaces")
    for p in ("replicates", "always_relevant", "only_relevant_to_owner", "net_load_on_client", "net_dormancy",
              "replicate_movement", "can_be_damaged", "hidden", "initial_life_span", "auto_receive_input"):
        doc["cdo." + p] = prop(cdo, p)
    tick = cdo.get_editor_property("primary_actor_tick")
    doc["tick.can_ever_tick"] = prop(tick, "can_ever_tick")
    doc["tick.start_with_tick_enabled"] = prop(tick, "start_with_tick_enabled")
    doc["components_on_cdo"] = [c.get_class().get_name() + ":" + c.get_name() for c in cdo.get_components_by_class(unreal.ActorComponent)]
    scs = bp.get_editor_property("simple_construction_script")
    doc["scs_nodes"] = [str(n.get_editor_property("component_class").get_name()) + ":" + str(n.get_editor_property("internal_variable_name")) for n in (scs.get_all_nodes() if scs else [])]
    eg = unreal.BlueprintEditorLibrary.find_event_graph(bp)
    doc["event_graph"] = eg.get_name() if eg else None
    doc["base_cdo_defaults"] = {p: prop(unreal.get_default_object(base), p) for p in ("replicates", "always_relevant", "net_load_on_client")}
    # Official Funcom DLC controllers (registry tags only, read-only).
    ar = unreal.AssetRegistryHelpers.get_asset_registry()
    official = {}
    flt = unreal.ARFilter(class_paths=[unreal.TopLevelAssetPath("/Script/Engine", "Blueprint")], package_paths=["/Game/DLC", "/Game/Systems"], recursive_paths=True)
    for a in ar.get_assets(flt):
        n = str(a.asset_name)
        if "modcontroller" in n.lower():
            official[str(a.package_name)] = {"ParentClass": str(a.get_tag_value("ParentClass")), "NativeParentClass": str(a.get_tag_value("NativeParentClass"))}
    doc["official_dlc_controllers"] = official
    with open(DOC, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1)
    log("documented -> " + DOC)
    log("DONE")
except Exception:
    log("FAILED: " + traceback.format_exc())
finally:
    pass  # editor stays open for the GUI Build mod step
