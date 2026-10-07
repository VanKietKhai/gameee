import glob, os, json, re
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
report = {}
for bf in sorted(glob.glob(S + r"\baseen\**\*.locres", recursive=True)):
    name = os.path.basename(bf)
    vf = glob.glob(S + r"\vihoa\**\\" + name, recursive=True)
    base, _ = load(bf)
    vi, _ = load(vf[0]) if vf else ([], None)
    vmap = {(ns, k): (h, s) for ns, keys in vi for k, h, s in keys}
    missing, stale, same = [], [], []
    for ns, keys in base:
        for k, h, s in keys:
            v = vmap.get((ns, k))
            if v is None: missing.append((ns, k, s))
            elif v[0] != h: stale.append((ns, k, s, v[1]))
            elif v[1] == s and re.search(r"[A-Za-z]{3}", s): same.append((ns, k, s))
    total = sum(len(k) for _, k in base)
    report[name] = {"missing": missing, "stale": stale}
    print(f"{name:36} base={total:6} missing={len(missing):5} source_changed={len(stale):5} kept_english={len(same):5}")
    for x in missing[:4]: print("     missing:", x[2][:90].replace("\n", " "))
json.dump({k: {"missing": v["missing"], "stale": v["stale"]} for k, v in report.items()}, open(S + r"\base_diff.json", "w", encoding="utf-8"), ensure_ascii=False)
