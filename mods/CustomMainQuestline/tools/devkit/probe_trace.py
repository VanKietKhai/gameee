# Read-only Dev Kit trace for Quest 01 (DEVKIT_CHECKLIST Part B). Never saves or modifies any asset.
import json, os, re
import unreal

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
PAT = re.compile(r"SewerAbomination|LavaWurm|LarvalHorror|Nahjef|Remnant|Dregs", re.I)
res = {"engine": unreal.SystemLibrary.get_engine_version(), "datatables": {}, "classes": {}, "maps": {}, "errors": []}


def dt_rows(path, keep):
    dt = unreal.load_asset(path)
    if dt is None:
        res["errors"].append("cannot load " + path); return
    js = unreal.DataTableFunctionLibrary.export_data_table_to_json_string(dt)
    rows = json.loads(js)
    res["datatables"][path] = [r for r in rows if keep(r)]


try:
    dt_rows("/Game/Systems/SpawnTable/SpawnDataTable", lambda r: bool(PAT.search(json.dumps(r))))
except Exception as e:
    res["errors"].append("SpawnDataTable: %r" % e)
try:
    dt_rows("/Game/Systems/Progression/DT_ExilesJourney", lambda r: "Remnant" in json.dumps(r) or "Dregs" in json.dumps(r))
except Exception as e:
    res["errors"].append("DT_ExilesJourney: %r" % e)

for cpath in ["/Game/Characters/NPCs/sewer_abomination/Blueprints/BP_NPC_Wildlife_SewerAbomination",
              "/Game/Characters/NPCs/sewer_abomination/Blueprints/BP_NPC_Wildlife_LavaWurm",
              "/Game/Characters/NPCs/Humanoid/HumanoidNPCCharacter_20percentbigger_boss_Nahjef"]:
    try:
        bp = unreal.load_asset(cpath)
        chain, cls = [], bp.generated_class() if hasattr(bp, "generated_class") else None
        info = {"asset_class": bp.get_class().get_name()}
        if cls is not None:
            info["generated_class"] = cls.get_path_name()
            cdo = unreal.get_default_object(cls)
            props = {}
            for name in ("CharacterName", "DisplayName", "SpawnTableRowName", "bIsBoss", "BossName"):
                try:
                    props[name] = str(cdo.get_editor_property(name))
                except Exception:
                    pass
            info["cdo_props"] = props
        try:
            info["parent"] = str(unreal.BlueprintEditorLibrary.get_blueprint_parent_class(bp).get_path_name()) if hasattr(unreal, "BlueprintEditorLibrary") else None
        except Exception as e:
            info["parent_err"] = repr(e)
        res["classes"][cpath] = info
    except Exception as e:
        res["errors"].append("%s: %r" % (cpath, e))

# Placed spawners/controllers in the two Dregs gameplay sublevels.
for mpath in ["/Game/Maps/ConanSandbox/Gameplay/Gameplay_Dungeon_Sewer", "/Game/Maps/ConanSandbox/Gameplay/Gameplay_Dungeon_Sewer_Blackout"]:
    try:
        world = unreal.EditorLoadingAndSavingUtils.load_map(mpath)
        actors = unreal.GameplayStatics.get_all_actors_of_class(world, unreal.Actor)
        rows = []
        for a in actors:
            cname = a.get_class().get_name()
            label = a.get_actor_label() if hasattr(a, "get_actor_label") else a.get_name()
            if not re.search(r"Spawn|Controller|Boss|Territor|Dregs|Nahjef", cname + " " + label, re.I):
                continue
            entry = {"label": label, "class": a.get_class().get_path_name(), "loc": str(a.get_actor_location())}
            for prop in ("SpawnTable", "SpawnTableRowName", "SpawnTableRow", "NPCSpawnTableRow", "BossController", "SpawnRequests", "Spawnpoints"):
                try:
                    entry[prop] = str(a.get_editor_property(prop))
                except Exception:
                    pass
            for comp in a.get_components_by_class(unreal.ActorComponent):
                cn = comp.get_class().get_name()
                if re.search(r"Spawn", cn, re.I):
                    for prop in ("SpawnTable", "SpawnTableRowName", "NPCSpawnEntries", "SpawnEntries", "SpawnTableRow"):
                        try:
                            entry.setdefault("components", {}).setdefault(cn, {})[prop] = str(comp.get_editor_property(prop))[:600]
                        except Exception:
                            pass
            rows.append(entry)
        res["maps"][mpath] = rows
    except Exception as e:
        res["errors"].append("%s: %r" % (mpath, e))

with open(os.path.join(OUT_DIR, "trace.json"), "w", encoding="utf-8") as f:
    json.dump(res, f, indent=1, default=str)
unreal.log("MQ14 probe_trace wrote trace.json")
