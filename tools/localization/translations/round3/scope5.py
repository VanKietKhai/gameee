import glob, os, json, re
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
INTERNAL = re.compile(r"^(XX_|[A-Za-z0-9]+(_[A-Za-z0-9]+)+$|[A-Z][a-z]+[A-Z][A-Za-z]*$)")
kept = []; dlc_missing = []
for bf in sorted(glob.glob(S + r"\baseen\**\*.locres", recursive=True)):
    name = os.path.basename(bf)
    vf = glob.glob(S + r"\vihoa\**\\" + name, recursive=True)
    base, _ = load(bf); vi, _ = load(vf[0])
    vmap = {(ns, k): (h, s) for ns, keys in vi for k, h, s in keys}
    for ns, keys in base:
        for k, h, s in keys:
            v = vmap.get((ns, k))
            if v is None:
                if name == "Exiles_Special_DLC.locres": dlc_missing.append({"file": name, "ns": ns, "key": k, "en": s})
            elif v[0] == h and v[1] == s and re.search(r"[A-Za-z]{3}", s) and not INTERNAL.match(s):
                kept.append({"file": name, "ns": ns, "key": k, "en": s})
def summary(lst, label):
    u = {}
    for x in lst: u.setdefault(x["en"], 0); u[x["en"]] += 1
    chars = sum(len(t) for t in u)
    print(label, "entries", len(lst), "unique", len(u), "chars", chars)
    return list(u)
ud = summary(dlc_missing, "DLC missing:")
uk = summary(kept, "kept English:")
by = {}
for x in kept: by[x["file"]] = by.get(x["file"], 0) + 1
print(by)
json.dump(dlc_missing, open(S + r"\scope_dlc.json", "w", encoding="utf-8"), ensure_ascii=False)
json.dump(kept, open(S + r"\scope_kept.json", "w", encoding="utf-8"), ensure_ascii=False)
print("sample kept:", uk[:60])
print("sample dlc:", [t[:60] for t in ud[:25]])
