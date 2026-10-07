"""Rebuild the patch locres files from the ORIGINAL Viet hoa files.
- additions (tr_*.json, trdt_*.json, trbase_*.json): new keys -> Exiles_UI.locres; keys already present in any
  original locres are skipped (they would conflict across files).
- overrides (trov_*.json, field "file"): replace the value of an existing key inside that same file.
"""
import glob, json, os
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
orig = {os.path.basename(f): f for f in glob.glob(S + r"\vihoa\**\*.locres", recursive=True)}
loaded = {name: load(path)[0] for name, path in orig.items()}
existing = {(ns, k) for nss in loaded.values() for ns, keys in nss for k, h, s in keys}

# overrides
ov_n = 0; touched = set()
for f in glob.glob(S + r"\trov_*.json"):
    for x in json.load(open(f, encoding="utf-8")):
        for ns, keys in loaded[x["file"]]:
            if ns != x["ns"]: continue
            for e in keys:
                if e[0] == x["key"]: e[2] = x["vi"]; ov_n += 1; touched.add(x["file"])

# additions into Exiles_UI
ui = loaded["Exiles_UI.locres"]; byns = {ns: keys for ns, keys in ui}
add = skip = 0; seen = set()
files = sorted(glob.glob(S + r"\tr_*.json") + glob.glob(S + r"\trdt_*.json") + glob.glob(S + r"\trbase_*.json"))
for f in files:
    for x in json.load(open(f, encoding="utf-8")):
        if "vi" not in x: continue
        k = (x["ns"], x["key"])
        if k in existing or k in seen: skip += 1; continue
        seen.add(k)
        if x["ns"] not in byns: byns[x["ns"]] = []; ui.append([x["ns"], byns[x["ns"]]])
        byns[x["ns"]].append([x["key"], srchash(x["en"]), x["vi"]]); add += 1
touched.add("Exiles_UI.locres")
os.makedirs(S + r"\vihoa_build", exist_ok=True)
for name in touched:
    data = save(loaded[name]); open(S + "\\vihoa_build\\" + name, "wb").write(data)
    chk, _ = load(S + "\\vihoa_build\\" + name)
    print(name, "entries", sum(len(k) for _, k in chk))
print("added", add, "skipped (already in patch / duplicate)", skip, "overrides", ov_n, "files", sorted(touched))
