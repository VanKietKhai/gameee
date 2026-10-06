# PROBE B documentation (read-only): describes BP_MQ14ProbeBController before packaging and compares with official
# Funcom DLC controllers. Never modifies or saves assets.
import json, os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
DOC = os.path.join(HERE, "probeB_definition.json")
FULL = "/Game/Mods/MQ14ProbeB/BP_MQ14ProbeBController"
doc = {"asset": FULL, "errors": []}


def attempt(key, fn):
    try:
        doc[key] = fn()
    except Exception as e:
        doc["errors"].append("%s: %s" % (key, e))


def prop(obj, name):
    try:
        return str(obj.get_editor_property(name))
    except Exception as e:
        return "n/a"


bp = unreal.load_asset(FULL)
gen = bp.generated_class()
cdo = unreal.get_default_object(gen)
attempt("generated_class", lambda: gen.get_path_name())
attempt("parent_class", lambda: unreal.BlueprintEditorLibrary.get_blueprint_parent_class(bp).get_path_name())
attempt("parent_class_tag", lambda: str(unreal.AssetRegistryHelpers.get_asset_registry().get_asset_by_object_path(FULL + ".BP_MQ14ProbeBController").get_tag_value("ParentClass")))
attempt("implemented_interfaces", lambda: [str(i) for i in (bp.get_editor_property("implemented_interfaces") or [])])
for p in ("replicates", "always_relevant", "only_relevant_to_owner", "net_load_on_client", "net_dormancy",
          "replicate_movement", "hidden", "initial_life_span", "auto_receive_input"):
    doc["cdo." + p] = prop(cdo, p)
attempt("tick", lambda: {k: prop(cdo.get_editor_property("primary_actor_tick"), k) for k in ("can_ever_tick", "start_with_tick_enabled", "tick_interval")})
attempt("components_on_cdo", lambda: [c.get_class().get_path_name() + " : " + c.get_name() for c in cdo.get_components_by_class(unreal.ActorComponent)])


def subobjects():
    sub = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    out = []
    for h in sub.k2_gather_subobject_data_for_blueprint(bp):
        d = unreal.SubobjectDataBlueprintFunctionLibrary.get_data(h)
        obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(d)
        out.append("%s : %s (inherited=%s)" % (obj.get_class().get_name() if obj else "?", d.get_variable_name() if hasattr(d, "get_variable_name") else "?",
                                              unreal.SubobjectDataBlueprintFunctionLibrary.is_inherited_component(d)))
    return out


attempt("subobjects", subobjects)
attempt("event_graph_nodes", lambda: [n.get_name() for n in unreal.BlueprintEditorLibrary.find_event_graph(bp).get_editor_property("nodes")])
attempt("base_cdo", lambda: {p: prop(unreal.get_default_object(unreal.load_class(None, "/Script/DreamworldMods.ModController")), p)
                             for p in ("replicates", "always_relevant", "net_load_on_client")})


def official():
    ar = unreal.AssetRegistryHelpers.get_asset_registry()
    out = {}
    for a in ar.get_assets_by_class(unreal.TopLevelAssetPath("/Script/Engine", "Blueprint"), True):
        if "modcontroller" in str(a.asset_name).lower() and not str(a.package_name).startswith("/Game/Mods/"):
            out[str(a.package_name)] = str(a.get_tag_value("ParentClass"))
    return out


attempt("official_dlc_controllers", official)
with open(DOC, "w", encoding="utf-8") as fh:
    json.dump(doc, fh, indent=1)
unreal.log("MQ14 probeB_doc written: " + DOC)
