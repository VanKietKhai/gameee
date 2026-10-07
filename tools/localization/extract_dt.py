"""Find DataTable-style FTexts: namespace "" (FString len 1), key = readable id, source = text."""
import re, struct, glob, json, os, sys
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
cur, _ = load(S + r"\vihoa_build\Exiles_UI.locres")
have = {(ns, k) for ns, keys in cur for k, h, s in keys}

def fstr(d, p):
    n = struct.unpack_from("<i", d, p)[0]
    if 1 <= n <= 6000 and p + 4 + n <= len(d) and d[p + 4 + n - 1] == 0:
        b = d[p + 4:p + 4 + n - 1]
        try: return b.decode("utf-8"), p + 4 + n
        except: return None
    if -6000 <= n <= -1 and p + 4 - 2 * n <= len(d):
        try: return d[p + 4:p + 4 - 2 * n - 2].decode("utf-16le"), p + 4 - 2 * n
        except: return None
    return None

def scan(mod):
    d = open(glob.glob(S + "\\modscan\\" + mod + "\\*-Windows.raw")[0], "rb").read()
    out = {}
    for m in re.finditer(rb"\x01\x00\x00\x00\x00", d):
        p = m.end()
        k = fstr(d, p)
        if not k or not re.fullmatch(r"[A-Za-z0-9_\-\.]{3,120}", k[0]) or re.fullmatch(r"[0-9A-F]{32}", k[0]): continue
        s = fstr(d, k[1])
        if not s or not re.search(r"[A-Za-z]{2}", s[0]): continue
        out[k[0]] = s[0]
    return out

mods = sys.argv[1:] or [os.path.basename(x) for x in glob.glob(S + r"\modscan\*")]
for mod in mods:
    try: r = scan(mod)
    except IndexError: continue
    items = [{"ns": "", "key": k, "en": v, "already": ("", k) in have} for k, v in r.items()]
    if not items: continue
    json.dump(items, open(S + "\\trdt_" + mod + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"{mod:30} dt-texts={len(items):5} already_in_patch={sum(1 for x in items if x['already'])}")
