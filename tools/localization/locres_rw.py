import struct, glob, sys
sys.path.insert(0, r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad")
exec(open(r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad\uehash.py", encoding="utf-8-sig").read().split("print(\"srchash test\"")[0])
MAGIC = bytes.fromhex("0E147475674A03FC4A15909DC3377F1B")
def rstr(d, p):
    n = struct.unpack_from("<i", d, p)[0]; p += 4
    if n >= 0: return (d[p:p+n-1].decode("latin1") if n else ""), p+n
    return d[p:p-2*n-2].decode("utf-16le"), p-2*n
def load(path):
    d = open(path, "rb").read(); assert d[:16] == MAGIC and d[16] == 3
    so = struct.unpack_from("<q", d, 17)[0]; p = 17+8+4
    q = so; cnt = struct.unpack_from("<i", d, q)[0]; q += 4; strs = []
    for _ in range(cnt):
        s, q = rstr(d, q); q += 4; strs.append(s)
    nsc = struct.unpack_from("<I", d, p)[0]; p += 4; out = []
    for _ in range(nsc):
        p += 4; ns, p = rstr(d, p); kc = struct.unpack_from("<I", d, p)[0]; p += 4; keys = []
        for _ in range(kc):
            p += 4; k, p = rstr(d, p); h, idx = struct.unpack_from("<Ii", d, p); p += 8; keys.append([k, h, strs[idx]])
        out.append([ns, keys])
    return out, d
def wstr(s):
    if s == "": return struct.pack("<i", 0)
    if all(ord(c) < 128 for c in s): b = s.encode("ascii") + b"\0"; return struct.pack("<i", len(b)) + b
    b = s.encode("utf-16le") + b"\0\0"; return struct.pack("<i", -(len(b)//2)) + b
def save(nss):
    strs = []; idx = {}; refs = []
    body = bytearray(); total = 0
    body += struct.pack("<I", len(nss))
    for ns, keys in nss:
        body += struct.pack("<I", keyhash(ns) if ns else 0) + wstr(ns) + struct.pack("<I", len(keys))
        for k, h, s in keys:
            if s not in idx: idx[s] = len(strs); strs.append(s); refs.append(0)
            refs[idx[s]] += 1; total += 1
            body += struct.pack("<I", keyhash(k) if k else 0) + wstr(k) + struct.pack("<Ii", h, idx[s])
    head = MAGIC + bytes([3])
    so = len(head) + 8 + 4 + len(body)
    tab = bytearray(struct.pack("<i", len(strs)))
    for s, r in zip(strs, refs): tab += wstr(s) + struct.pack("<i", r)
    return bytes(head + struct.pack("<qI", so, total) + body + tab)
src = glob.glob(r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad\vihoa\**\Exiles_UI.locres", recursive=True)[0]
nss, orig = load(src)
rt = save(nss)
print("roundtrip identical:", rt == orig, len(rt), len(orig))

