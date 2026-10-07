import re, struct, json, glob, sys
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
mod = sys.argv[1]
d = open(glob.glob(S + "\\modscan\\" + mod + "\\*-Windows.raw")[0], "rb").read()
def fstr(p):
    n = struct.unpack_from("<i", d, p)[0]
    if n == 0: return "", p+4
    if 1 <= n <= 8000 and p+4+n <= len(d) and d[p+4+n-1] == 0:
        try: return d[p+4:p+4+n-1].decode("ascii"), p+4+n
        except: return None
    if -8000 <= n <= -1: return d[p+4:p+4-2*n-2].decode("utf-16le", "replace"), p+4-2*n
    return None
cur, _ = load(S + r"\vihoa_build\Exiles_UI.locres")
have = {(ns, k): s for ns, keys in cur for k, h, s in keys}
items = {}
for m in re.finditer(rb"[0-9A-F]{32}\x00", d):
    kp = m.start() - 4
    if struct.unpack_from("<i", d, kp)[0] != 33: continue
    ns = None
    for L in range(0, 300):
        p = kp - 4 - L
        if p < 0: break
        if struct.unpack_from("<i", d, p)[0] == L:
            r = fstr(p)
            if r and r[1] == kp: ns = r[0]; break
    src = fstr(m.end())
    if ns is None or not src or not re.search(r"[A-Za-z]{2}", src[0]): continue
    k = m.group()[:-1].decode()
    items[(ns, k)] = {"ns": ns, "key": k, "en": src[0], "already": have.get((ns, k))}
out = list(items.values())
json.dump(out, open(S + "\\tr_" + mod + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(out), "already translated:", sum(1 for x in out if x["already"]), "namespaces:", sorted({x["ns"] for x in out}))
for i, x in enumerate(out): print(i, "|", "DONE" if x["already"] else "", "|", x["en"].replace("\n", "\\n")[:230])
