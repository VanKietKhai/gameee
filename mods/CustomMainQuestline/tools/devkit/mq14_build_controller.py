# Mod #14 Quest 01 vertical slice: builds /Game/Mods/MQ14MainQuest/BP_MQ14MainQuestController from scratch
# with the Dev Kit editor's own Blueprint authoring API (BlueprintGraphEditor). Deterministic: the asset is
# deleted and rebuilt on every run. Refuses unless MQ14MainQuest is the active mod.
#
# Server (authority):
#   BeginPlay -> SetTimerByFunctionName("MQ14_Tick", 5 s, looping)
#   MQ14_Tick: bind SignalOnKilled on every BP_NPC_Wildlife_SewerAbomination_C (AddUnique semantics), and
#              publish the PlayerStates of connected players who completed MQ01 (replicated list for the panel)
#   MQ14_OnBossKilled(Character, Killer): only inside The Dregs (distance to the D_S_SewerBoss1 spawner);
#              credit every player character within the credit radius (no killing blow needed), once per
#              character StableId; SaveGame list; HUD banner; persistence SetDirty.
# Client: F7 (legacy key event on this actor; vanilla J/OpenJourney untouched) -> rich message box with the Main Quest status.
import json, os, traceback
import unreal

HERE = os.path.dirname(os.path.abspath(__file__))
STATUS = os.path.join(HERE, "mq14_build_status.txt")
DOC_SUFFIX = ""
DOC = os.path.join(HERE, "mq14_controller_definition.json")
MOD = "MQ14MainQuest"
PATH = "/Game/Mods/" + MOD
NAME = globals().get("MQ14_NAME_OVERRIDE") or "BP_MQ14MainQuestController"
SCRATCH = NAME != "BP_MQ14MainQuestController"   # scratch builds are never saved (and never packaged)
FULL = PATH + "/" + NAME

BOSS_CLASS = "/Game/Characters/NPCs/sewer_abomination/blueprints/BP_NPC_Wildlife_SewerAbomination.BP_NPC_Wildlife_SewerAbomination_C"
DREGS_SPAWNER = (-137111.734375, 375790.8125, -21557.427734)   # D_S_SewerBoss1_1 (Gameplay_Dungeon_Sewer)
DREGS_RADIUS = 12000.0     # boss must die within 120 m of its own spawner
CREDIT_RADIUS = 5000.0     # 50 m, data: completionCreditRadius
TICK_SECONDS = 5.0
MACROS = "/Engine/EditorBlueprintResources/StandardMacros.StandardMacros:"

T_BANNER = "NHIỆM VỤ CHÍNH HOÀN THÀNH: Xuống The Dregs. Tiếp theo: Tower of Bats"
T_TITLE = "NHIỆM VỤ CHÍNH"
T_PANEL_ACTIVE = ("Hồi I — Sinh Tồn\n"
                  "▶ ĐANG LÀM: Xuống The Dregs\n"
                  "   Mục tiêu: Hạ gục Abysmal Remnant\n"
                  "   Khu vực: The Dregs (bản đồ D4) · Khuyến nghị cấp 20–25 · 1–3 người\n"
                  "   Mang cung và tên. Vào dungeon: theo bóng ma và làm nghi lễ hiến máu.\n"
                  "   (Người chơi trong bán kính 50 m khi boss chết đều được tính)\n\n"
                  "🔒 Tiếp theo: Tower of Bats (mở khi xong The Dregs)")
T_PANEL_DONE = ("Hồi I — Sinh Tồn\n"
                "✔ HOÀN THÀNH: Xuống The Dregs (Abysmal Remnant)\n\n"
                "▶ TIẾP THEO: Tower of Bats (bản đồ F5) · cấp 25–30\n"
                "   Mang theo Staff of the Triumvirate; hạ Albino Bat Demon\n"
                "   (Nhiệm vụ này sẽ được bật trong bản cập nhật sau)")

BEL = unreal.BlueprintEditorLibrary
BGE = unreal.BlueprintGraphEditor
log_lines = []


def log(msg):
    unreal.log("MQ14 build: " + msg)
    log_lines.append(msg)


def cls_path(c):
    return c if isinstance(c, str) else c.static_class().get_path_name()


def fn(cls, name):
    return cls_path(cls) + ":" + name


def pin(node, name, out=False):
    p = node.find_output_pin(name) if out else node.find_input_pin(name)
    if p is None or not p.is_valid():
        # tolerate case differences in parameter names (e.g. Id/ID, Text/text)
        pins = node.list_output_pins() if out else node.list_input_pins()
        for x in pins:
            if str(x.get_pin_name()).lower() == name.lower():
                return x
    if p is None or not p.is_valid():
        names = [str(x.get_pin_name()) for x in node.list_all_pins()]
        raise RuntimeError("pin %s (%s) not found on %s; pins=%s" % (name, "out" if out else "in", node.get_node_title(), names))
    return p


def link(a, a_pin, b, b_pin):
    pa = a_pin if not isinstance(a_pin, str) else pin(a, a_pin, out=True)
    pb = b_pin if not isinstance(b_pin, str) else pin(b, b_pin)
    if not pa.try_create_connection(pb):
        raise RuntimeError("link failed %s.%s -> %s.%s" % (a.get_node_title(), pa.get_pin_name(), b.get_node_title(), pb.get_pin_name()))


def setv(node, name, value):
    if not pin(node, name).set_pin_value(str(value)):
        raise RuntimeError("set %s=%s failed on %s" % (name, value, node.get_node_title()))


def call(ge, path, x, y):
    n = ge.add_call_function_node(path)
    if n is None:
        raise RuntimeError("cannot create call node " + path)
    n.set_node_pos(unreal.IntPoint(x, y))
    return n


def macro(ge, name, x, y):
    n = ge.add_macro_node(MACROS + name)
    if n is None:
        raise RuntimeError("cannot create macro " + name)
    n.set_node_pos(unreal.IntPoint(x, y))
    return n


def getvar(ge, name, x, y, class_path=""):
    n = ge.add_get_member_variable_node(name, class_path) if class_path else ge.add_get_member_variable_node(name)
    if n is None:
        raise RuntimeError("cannot get var " + name)
    n.set_node_pos(unreal.IntPoint(x, y))
    return n


def literal(ge, node, pin_name, value, x, y):
    """Feeds a const-ref string/text parameter through MakeLiteralString/MakeLiteralText (literals are refused)."""
    target = pin(node, pin_name)
    kind = str(target.get_pin_type_display_string()).lower()
    fname = "MakeLiteralText" if "text" in kind and "string" not in kind else "MakeLiteralString"
    lit = call(ge, "/Script/Engine.KismetSystemLibrary:" + fname, x, y)
    setv(lit, "Value", value)
    link(lit, "ReturnValue", node, target)


def vec(node, name, xyz):
    setv(node, name, "%f,%f,%f" % xyz)


def save_flag(bp, var_name):
    """Marks a Blueprint member variable SaveGame (CPF_SaveGame) so the game's persistence stores it."""
    cpf_save_game = 0x0000000001000000
    try:
        vars_ = bp.get_editor_property("new_variables")
    except Exception as e:
        log("SaveGame flag not settable from Python (%s); set it in the variable Details panel" % type(e).__name__)
        return False
    for v in vars_:
        if str(v.get_editor_property("var_name")) == var_name:
            flags = int(v.get_editor_property("property_flags"))
            v.set_editor_property("property_flags", flags | cpf_save_game)
    bp.set_editor_property("new_variables", vars_)
    for v in bp.get_editor_property("new_variables"):
        if str(v.get_editor_property("var_name")) == var_name:
            return bool(int(v.get_editor_property("property_flags")) & cpf_save_game)
    return False


def build_panel(ge, GS, KAL, CPC):
    key_names = [n for n in ge.list_available_nodes([]) if n.endswith("|F7")]
    log("F7 key candidates: %s" % key_names[:8])
    kb = [n for n in key_names if "Keyboard" in n] or key_names
    if not kb:
        raise RuntimeError("no F7 key node available")
    jkey = ge.create_node_from_name(kb[0], unreal.Vector2D(0, 700), [])
    lpcs = call(ge, fn(GS, "GetAllActorsOfClass"), 300, 900)
    setv(lpcs, "ActorClass", cls_path(CPC))
    jloop = macro(ge, "ForEachLoop", 300, 700)
    link(jkey, "Pressed", lpcs, "execute")
    link(lpcs, "then", jloop, "Exec")
    link(lpcs, "OutActors", jloop, "Array")
    isloc = call(ge, fn(unreal.Controller, "IsLocalController"), 600, 900)
    link(jloop, "Array Element", isloc, "self")
    br_l = ge.add_branch_node(); br_l.set_node_pos(unreal.IntPoint(600, 700))
    link(jloop, "LoopBody", br_l, "execute")
    link(isloc, "ReturnValue", br_l, "Condition")
    ps3 = getvar(ge, "PlayerState", 850, 900, cls_path(unreal.Controller))
    link(jloop, "Array Element", ps3, "self")
    pl3 = getvar(ge, "MQ01_CompletedPlayers", 850, 1050)
    has3 = call(ge, fn(KAL, "Array_Contains"), 1100, 900)
    link(pl3, "MQ01_CompletedPlayers", has3, "TargetArray")
    link(ps3, "PlayerState", has3, "ItemToFind")
    br_d = ge.add_branch_node(); br_d.set_node_pos(unreal.IntPoint(1100, 700))
    link(br_l, "then", br_d, "execute")
    link(has3, "ReturnValue", br_d, "Condition")
    for branch_pin, text, y in (("then", T_PANEL_DONE, 600), ("else", T_PANEL_ACTIVE, 850)):
        box = call(ge, fn(CPC, "ClientShowRichMessageBox"), 1400, y)
        link(br_d, branch_pin, box, "execute")
        link(jloop, "Array Element", box, "self")
        ins = [p for p in box.list_input_pins() if str(p.get_pin_name()) not in ("execute", "self")]
        literal(ge, box, str(ins[0].get_pin_name()), T_TITLE, 1400, y + 150)
        literal(ge, box, str(ins[1].get_pin_name()), text, 1400, y + 220)



def finish(bp):
    ok = BEL.compile_blueprint(bp)
    errs = []
    for g in BEL.list_graphs(bp):
        e = BGE.get_graph_editor(g)
        errs += ["%s: %s %s" % (g.get_name(), n.get_node_title(), n.error_msg) for n in e.list_nodes_with_errors()]
        errs += ["WARN %s: %s %s" % (g.get_name(), n.get_node_title(), n.error_msg) for n in e.list_nodes_with_warnings()]
    log("compile=%s issues=%s" % (ok, errs))
    if not ok or [x for x in errs if not x.startswith("WARN")]:
        raise RuntimeError("compile errors; not saving")
    if SCRATCH:
        log("SCRATCH build, not saved: " + FULL)
    elif not EAL.save_asset(FULL, only_if_is_dirty=False):
        raise RuntimeError("save failed")
    doc = {"asset": FULL, "boss_class": BOSS_CLASS, "dregs_spawner": DREGS_SPAWNER, "dregs_radius": DREGS_RADIUS,
           "credit_radius": CREDIT_RADIUS, "tick_seconds": TICK_SECONDS, "issues": errs,
           "graphs": {}}
    for g in BEL.list_graphs(bp):
        e = BGE.get_graph_editor(g)
        doc["graphs"][g.get_name()] = [n.get_node_title() for n in e.list_all_nodes()]
    doc["variables"] = list(BEL.list_member_variable_names(bp, False))
    with open(DOC, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1, ensure_ascii=False)
    log("DONE")


try:
    if not os.path.exists(os.path.join(unreal.Paths.project_content_dir(), "Mods", MOD, "active.txt")):
        raise RuntimeError(MOD + " is not the active mod; refusing")
    EAL = unreal.EditorAssetLibrary
    if EAL.does_asset_exist(FULL):
        # Rebuilding in place crashed the editor (2026-10-07). Rebuild = close the editor, delete the
        # .uasset of this mod's controller on disk, reopen, run this script once.
        raise RuntimeError(FULL + " already exists; delete it with the editor closed, then rerun")
    f = unreal.BlueprintFactory()
    f.set_editor_property("parent_class", unreal.load_class(None, "/Script/DreamworldMods.ModController"))
    bp = unreal.AssetToolsHelpers.get_asset_tools().create_asset(NAME, PATH, None, f)
    if bp is None:
        raise RuntimeError("create failed")
    log("created " + FULL)

    GS, KSL, KML, KAL = "/Script/Engine.GameplayStatics", "/Script/Engine.KismetSystemLibrary", "/Script/Engine.KismetMathLibrary", "/Script/Engine.KismetArrayLibrary"
    CPC, CC = unreal.ConanPlayerController, unreal.ConanCharacter
    SID = unreal.StableIdFunctionLibrary

    # ---- variables
    t_str_arr = BEL.get_array_type(BEL.get_basic_type_by_name("string"))
    t_ps_arr = BEL.get_array_type(BEL.get_object_reference_type(unreal.PlayerState.static_class()))
    BEL.add_member_variable(bp, "MQ01_CompletedIds", t_str_arr)
    BEL.add_member_variable(bp, "MQ01_CompletedPlayers", t_ps_arr)
    BEL.set_blueprint_variable_replication(bp, "MQ01_CompletedPlayers", unreal.BlueprintVariableReplication.REPLICATED)
    log("SaveGame flag on MQ01_CompletedIds: %s" % save_flag(bp, "MQ01_CompletedIds"))
    BEL.compile_blueprint(bp)

    # ---- function MQ14_OnBossKilled(Character, Killer)
    t_cc = BEL.get_object_reference_type(CC.static_class())
    g_kill = BEL.add_function_graph(bp, "MQ14_OnBossKilled")
    gk = BGE.get_graph_editor(g_kill)
    gk.add_graph_input_parameter("Character", t_cc)
    gk.add_graph_input_parameter("Killer", t_cc)
    entry = [n for n in gk.list_all_nodes() if n.get_class().get_name() == "K2Node_FunctionEntry"][0]
    loc = call(gk, fn(unreal.Actor, "K2_GetActorLocation"), 250, 200)
    link(entry, "Character", loc, "self")
    dist = call(gk, fn(KML, "Vector_Distance"), 500, 200)
    link(loc, "ReturnValue", dist, "V1")
    vec(dist, "V2", DREGS_SPAWNER)
    inside = call(gk, fn(KML, "LessEqual_DoubleDouble"), 750, 200)
    link(dist, "ReturnValue", inside, "A")
    setv(inside, "B", DREGS_RADIUS)
    br = gk.add_branch_node(); br.set_node_pos(unreal.IntPoint(1000, 0))
    link(entry, "then", br, "execute")
    link(inside, "ReturnValue", br, "Condition")
    p_out = call(gk, fn(KSL, "PrintString"), 1250, 300)
    setv(p_out, "InString", "MQ14 MQ01 ignored: Remnant-class death outside The Dregs")
    setv(p_out, "bPrintToScreen", "false")
    link(br, "else", p_out, "execute")
    p_in = call(gk, fn(KSL, "PrintString"), 1250, 0)
    setv(p_in, "InString", "MQ14 MQ01 boss death in The Dregs; crediting nearby players")
    setv(p_in, "bPrintToScreen", "false")
    link(br, "then", p_in, "execute")
    pcs = call(gk, fn(GS, "GetAllActorsOfClass"), 1250, 150)
    setv(pcs, "ActorClass", cls_path(CPC))
    loop = macro(gk, "ForEachLoop", 1550, 0)
    link(p_in, "then", pcs, "execute")
    link(pcs, "then", loop, "Exec")
    link(pcs, "OutActors", loop, "Array")
    pawn = call(gk, fn(unreal.Controller, "K2_GetPawn"), 1800, 250)
    link(loop, "Array Element", pawn, "self")
    valid = macro(gk, "IsValid", 2050, 0)
    link(loop, "LoopBody", valid, "exec")
    link(pawn, "ReturnValue", valid, "InputObject")
    ploc = call(gk, fn(unreal.Actor, "K2_GetActorLocation"), 2050, 250)
    link(pawn, "ReturnValue", ploc, "self")
    pdist = call(gk, fn(KML, "Vector_Distance"), 2300, 250)
    link(ploc, "ReturnValue", pdist, "V1")
    link(loc, "ReturnValue", pdist, "V2")
    near = call(gk, fn(KML, "LessEqual_DoubleDouble"), 2550, 250)
    link(pdist, "ReturnValue", near, "A")
    setv(near, "B", CREDIT_RADIUS)
    br_near = gk.add_branch_node(); br_near.set_node_pos(unreal.IntPoint(2550, 0))
    link(valid, "Is Valid", br_near, "execute")
    link(near, "ReturnValue", br_near, "Condition")
    sid = call(gk, fn(SID, "GetActorStableId"), 2800, 250)
    link(pawn, "ReturnValue", sid, "Actor")
    sids = call(gk, fn(SID, "Conv_StableIdToString"), 3050, 250)
    link(sid, "ReturnValue", sids, "Id")
    ids_get = getvar(gk, "MQ01_CompletedIds", 3050, 400)
    has = call(gk, fn(KAL, "Array_Contains"), 3300, 250)
    link(ids_get, "MQ01_CompletedIds", has, "TargetArray")
    link(sids, "ReturnValue", has, "ItemToFind")
    br_new = gk.add_branch_node(); br_new.set_node_pos(unreal.IntPoint(3550, 0))
    link(br_near, "then", sid, "execute")
    link(sid, "then", br_new, "execute")
    link(has, "ReturnValue", br_new, "Condition")
    add_id = call(gk, fn(KAL, "Array_Add"), 3800, 0)
    link(br_new, "else", add_id, "execute")
    link(ids_get, "MQ01_CompletedIds", add_id, "TargetArray")
    link(sids, "ReturnValue", add_id, "NewItem")
    ps_get = getvar(gk, "PlayerState", 3800, 400, cls_path(unreal.Controller))
    link(loop, "Array Element", ps_get, "self")
    pl_get = getvar(gk, "MQ01_CompletedPlayers", 3800, 550)
    add_ps = call(gk, fn(KAL, "Array_AddUnique"), 4050, 0)
    link(add_id, "then", add_ps, "execute")
    link(pl_get, "MQ01_CompletedPlayers", add_ps, "TargetArray")
    link(ps_get, "PlayerState", add_ps, "NewItem")
    banner = call(gk, fn(CPC, "ClientHUDShowNotification"), 4300, 0)
    link(add_ps, "then", banner, "execute")
    link(loop, "Array Element", banner, "self")
    literal(gk, banner, "Text", T_BANNER, 4300, 250)
    p_done = call(gk, fn(KSL, "PrintString"), 4550, 0)
    link(banner, "then", p_done, "execute")
    setv(p_done, "bPrintToScreen", "false")
    cat = call(gk, fn("/Script/Engine.KismetStringLibrary", "Concat_StrStr"), 4550, 250)
    setv(cat, "A", "MQ14 MQ01 COMPLETE character=")
    link(sids, "ReturnValue", cat, "B")
    link(cat, "ReturnValue", p_done, "InString")
    pc_get = getvar(gk, "PersistenceComponent", 4800, 250)
    pc_out = pin(pc_get, "PersistenceComponent", out=True)
    pnames = [n for n in gk.list_available_nodes([pc_out]) if "dirty" in n.lower() or n.lower().endswith("|save")]
    log("persistence candidates: %s" % pnames[:10])
    # 2026-10-07 discovery: the ActorPersistenceComponent call is 'Dreamworld|Persistence|Setdirtyflag'
    pick = [n for n in pnames if "|persistence|" in n.lower() and "dirty" in n.lower()]
    if not pick:
        raise RuntimeError("no persistence save function found")
    dirty = gk.create_node_from_name(pick[0], unreal.Vector2D(4800, 0), [pc_out])
    log("persistence call: " + pick[0])
    link(p_done, "then", dirty, "execute")
    if not dirty.find_self_pin().list_connected_pins():
        link(pc_get, "PersistenceComponent", dirty, "self")
    log("persistence pins: %s" % [str(p.get_pin_name()) for p in dirty.list_input_pins()])
    log("MQ14_OnBossKilled built")

    # ---- function MQ14_Tick: bind the boss deaths + publish completed players
    g_tick = BEL.add_function_graph(bp, "MQ14_Tick")
    gt = BGE.get_graph_editor(g_tick)
    tentry = [n for n in gt.list_all_nodes() if n.get_class().get_name() == "K2Node_FunctionEntry"][0]
    bosses = call(gt, fn(GS, "GetAllActorsOfClass"), 250, 200)
    setv(bosses, "ActorClass", BOSS_CLASS)
    bloop = macro(gt, "ForEachLoop", 500, 0)
    link(tentry, "then", bosses, "execute")
    link(bosses, "then", bloop, "Exec")
    link(bosses, "OutActors", bloop, "Array")
    elem_pin = pin(bloop, "Array Element", out=True)
    names = gt.list_available_nodes([elem_pin])
    bind_names = [n for n in names if "bind" in n.lower() and "signal" in n.lower() and "killed" in n.lower()]
    log("bind candidates: %s" % bind_names[:5])
    if not bind_names:
        raise RuntimeError("no 'Bind Event to Signal On Killed' node; sample=%s" % [n for n in names if "Killed" in n][:20])
    bind = gt.create_node_from_name(bind_names[0], unreal.Vector2D(800, 0), [elem_pin])
    link(bloop, "LoopBody", bind, "execute")
    cd_names = [n for n in gt.list_available_nodes([pin(bind, "Delegate")]) if "create event" in n.lower() or "createevent" in n.lower()]
    if not cd_names:
        raise RuntimeError("no Create Event node offered for the delegate pin")
    log("create-event candidates: %s" % cd_names[:5])
    cdel = gt.create_node_from_name(cd_names[0], unreal.Vector2D(550, 300), [pin(bind, "Delegate")])
    if not [p for p in pin(bind, "Delegate").list_connected_pins()]:
        link(cdel, [p for p in cdel.list_output_pins()][0], bind, "Delegate")
    BEL.set_create_delegate_function(cdel, "MQ14_OnBossKilled")
    log("delegate function = %s" % BEL.get_create_delegate_function(cdel))
    # publish completed players (connected now) for the client panel
    pcs2 = call(gt, fn(GS, "GetAllActorsOfClass"), 1100, 300)
    setv(pcs2, "ActorClass", cls_path(CPC))
    ploop = macro(gt, "ForEachLoop", 1300, 0)
    link(bloop, "Completed", pcs2, "execute")
    link(pcs2, "then", ploop, "Exec")
    link(pcs2, "OutActors", ploop, "Array")
    pawn2 = call(gt, fn(unreal.Controller, "K2_GetPawn"), 1550, 300)
    link(ploop, "Array Element", pawn2, "self")
    v2 = macro(gt, "IsValid", 1550, 0)
    link(ploop, "LoopBody", v2, "exec")
    link(pawn2, "ReturnValue", v2, "InputObject")
    sid2 = call(gt, fn(SID, "GetActorStableId"), 1800, 300)
    link(pawn2, "ReturnValue", sid2, "Actor")
    sids2 = call(gt, fn(SID, "Conv_StableIdToString"), 2050, 300)
    link(sid2, "ReturnValue", sids2, "Id")
    ids2 = getvar(gt, "MQ01_CompletedIds", 2050, 450)
    has2 = call(gt, fn(KAL, "Array_Contains"), 2300, 300)
    link(ids2, "MQ01_CompletedIds", has2, "TargetArray")
    link(sids2, "ReturnValue", has2, "ItemToFind")
    br2 = gt.add_branch_node(); br2.set_node_pos(unreal.IntPoint(2300, 0))
    link(v2, "Is Valid", sid2, "execute")
    link(sid2, "then", br2, "execute")
    link(has2, "ReturnValue", br2, "Condition")
    ps2 = getvar(gt, "PlayerState", 2550, 300, cls_path(unreal.Controller))
    link(ploop, "Array Element", ps2, "self")
    pl2 = getvar(gt, "MQ01_CompletedPlayers", 2550, 450)
    addu = call(gt, fn(KAL, "Array_AddUnique"), 2800, 0)
    link(br2, "then", addu, "execute")
    link(pl2, "MQ01_CompletedPlayers", addu, "TargetArray")
    link(ps2, "PlayerState", addu, "NewItem")
    log("MQ14_Tick built")

    # ---- event graph
    ge = BGE.get_graph_editor(BEL.find_event_graph(bp))
    ev = ge.find_event_node("ReceiveBeginPlay") or BEL.add_event_override(bp, "ReceiveBeginPlay", unreal.Vector2D(0, 0))
    ge = BGE.get_graph_editor(BEL.find_event_graph(bp))
    ev = ge.find_event_node("ReceiveBeginPlay")
    auth = call(ge, fn(unreal.Actor, "HasAuthority"), 250, 200)
    br_a = ge.add_branch_node(); br_a.set_node_pos(unreal.IntPoint(300, 0))
    link(ev, "then", br_a, "execute")
    link(auth, "ReturnValue", br_a, "Condition")
    timer = call(ge, fn(KSL, "K2_SetTimer"), 600, 0)
    link(br_a, "then", timer, "execute")
    setv(timer, "FunctionName", "MQ14_Tick")
    setv(timer, "Time", TICK_SECONDS)
    setv(timer, "bLooping", "true")
    p_start = call(ge, fn(KSL, "PrintString"), 900, 0)
    link(timer, "then", p_start, "execute")
    setv(p_start, "InString", "MQ14 MainQuest controller started (server); tracking MQ01 Abysmal Remnant")
    setv(p_start, "bPrintToScreen", "false")
    # client: enable input on this actor for the local player after a short delay
    delay = call(ge, fn(KSL, "Delay"), 600, 300)
    link(br_a, "else", delay, "execute")
    setv(delay, "Duration", "5.0")
    gpc = call(ge, fn(GS, "GetPlayerController"), 600, 500)
    en = call(ge, fn(unreal.Actor, "EnableInput"), 900, 300)
    link(delay, "then", en, "execute")
    link(gpc, "ReturnValue", en, "PlayerController")
    # F7 key -> panel (optional: a failure here keeps the server logic, is logged, and is fixed in a later build)
    before = {n.get_name() for n in ge.list_all_nodes()}
    try:
        build_panel(ge, GS, KAL, CPC)
        log("F7 panel built")
    except Exception:
        log("F7 PANEL FAILED (server logic kept): " + traceback.format_exc())
        ge.remove_nodes([n for n in ge.list_all_nodes() if n.get_name() not in before])
    log("event graph built")
    finish(bp)
except Exception:
    log("FAILED: " + traceback.format_exc())
finally:
    with open(STATUS, "w", encoding="utf-8") as f:
        f.write("\n".join(log_lines) + "\n")
