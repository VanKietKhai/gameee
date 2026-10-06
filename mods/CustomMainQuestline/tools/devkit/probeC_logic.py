# PROBE C step 2: adds exactly ONE node to BP_MQ14ProbeCController: Event BeginPlay -> PrintString
# ("MQ14ProbeC BeginPlay", log only, not on screen). Compiles, saves, then documents the graph to
# probeC_definition.json. Refuses unless MQ14ProbeC is the active mod. Idempotent.
import json, os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "probeC_status.txt")
DOC = os.path.join(HERE, "probeC_definition.json")
MOD = "MQ14ProbeC"
FULL = "/Game/Mods/MQ14ProbeC/BP_MQ14ProbeCController"
MESSAGE = "MQ14ProbeC BeginPlay"
BGE = unreal.BlueprintGraphEditor
BEL = unreal.BlueprintEditorLibrary


def log(msg):
    unreal.log("MQ14 probeC: " + msg)
    with open(STATUS, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


try:
    if not os.path.exists(os.path.join(unreal.Paths.project_content_dir(), "Mods", MOD, "active.txt")):
        raise RuntimeError(MOD + " is not the active mod; refusing")
    bp = unreal.load_asset(FULL)
    ge = BGE.get_graph_editor(BEL.find_event_graph(bp))
    prints = [n for n in ge.list_all_nodes() if "Print String" in n.get_node_title()]
    if not prints:
        ev = ge.find_event_node("ReceiveBeginPlay") or BEL.add_event_override(bp, "ReceiveBeginPlay", unreal.Vector2D(0, 0))
        ge = BGE.get_graph_editor(BEL.find_event_graph(bp))
        ev = ge.find_event_node("ReceiveBeginPlay")
        pn = ge.add_call_function_node("/Script/Engine.KismetSystemLibrary:PrintString")
        pn.set_node_pos(unreal.IntPoint(400, 0))
        if not ev.find_then_pin().try_create_connection(pn.find_execute_pin()):
            raise RuntimeError("exec connection failed")
        for name, val in (("InString", MESSAGE), ("bPrintToScreen", "false"), ("bPrintToLog", "true")):
            if not pn.find_input_pin(name).set_pin_value(val):
                raise RuntimeError("set %s failed" % name)
        log("added BeginPlay -> PrintString")
    if not BEL.compile_blueprint(bp):
        log("compile returned false")
    if not unreal.EditorAssetLibrary.save_asset(FULL, only_if_is_dirty=False):
        raise RuntimeError("save failed")
    ge = BGE.get_graph_editor(BEL.find_event_graph(bp))
    nodes = []
    for n in ge.list_all_nodes():
        nodes.append({"title": n.get_node_title(), "class": n.get_class().get_name(),
                      "pins": {str(p.get_pin_name()): p.get_pin_value() for p in n.list_input_pins()},
                      "links": [str(p.get_pin_name()) + "->" + ",".join(str(c.get_owning_node().get_node_title()) for c in p.list_connected_pins())
                                for p in n.list_output_pins() if p.list_connected_pins()]})
    doc = {"asset": FULL, "parent_class": BEL.get_blueprint_parent_class(bp).get_path_name(),
           "event_graph_nodes": nodes,
           "errors": [n.get_node_title() for n in ge.list_nodes_with_errors()],
           "warnings": [n.get_node_title() for n in ge.list_nodes_with_warnings()],
           "member_variables": list(BEL.list_member_variable_names(bp, False))}
    with open(DOC, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1)
    log("documented -> " + DOC)
    log("DONE")
except Exception:
    log("FAILED: " + traceback.format_exc())
