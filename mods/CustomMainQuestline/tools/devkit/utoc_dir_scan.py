"""Read-only: rebuild asset paths from IoStore .utoc directory indexes and print those matching a pattern."""
import os, re, struct, sys

PAKS = r"D:\Conan Exiless\Conan Exiles Dedicated Server\ConanSandbox\Content\Paks"
PAT = re.compile(sys.argv[1] if len(sys.argv) > 1 else r"(?i)dregs|sewerabom|abysmal|abyssal|nahjef|a3c1", re.I)
INVALID = 0xFFFFFFFF


def fstring(b, o):
    n = struct.unpack_from("<i", b, o)[0]
    o += 4
    if n == 0:
        return "", o
    if n < 0:
        s = b[o:o - 2 * n].decode("utf-16-le").rstrip("\0")
        return s, o - 2 * n
    return b[o:o + n].decode("utf-8", "replace").rstrip("\0"), o + n


def parse_index(b, start):
    mount, o = fstring(b, start)
    nd = struct.unpack_from("<i", b, o)[0]; o += 4
    if not (0 < nd < 2_000_000):
        return None
    dirs = [struct.unpack_from("<4I", b, o + 16 * i) for i in range(nd)]; o += 16 * nd
    nf = struct.unpack_from("<i", b, o)[0]; o += 4
    if not (0 <= nf < 5_000_000):
        return None
    files = [struct.unpack_from("<3I", b, o + 12 * i) for i in range(nf)]; o += 12 * nf
    ns = struct.unpack_from("<i", b, o)[0]; o += 4
    if not (0 <= ns < 5_000_000):
        return None
    strings = []
    for _ in range(ns):
        s, o = fstring(b, o)
        strings.append(s)
    out = []

    def walk(di, prefix, depth=0):
        if di == INVALID or depth > 64:
            return
        name, first_child, next_sib, first_file = dirs[di]
        path = prefix + (strings[name] + "/" if name != INVALID else "")
        fi = first_file
        while fi != INVALID:
            fn, nxt, _ = files[fi]
            out.append(path + strings[fn])
            fi = nxt
        c = first_child
        while c != INVALID:
            walk(c, path, depth + 1)
            c = dirs[c][2]

    walk(0, mount)
    return out


hits = 0
for name in sorted(os.listdir(PAKS)):
    if not name.endswith(".utoc"):
        continue
    with open(os.path.join(PAKS, name), "rb") as f:
        b = f.read()
    paths = None
    for m in re.finditer(rb"\.\./\.\./\.\./", b):
        try:
            paths = parse_index(b, m.start() - 4)
        except Exception:
            paths = None
        if paths:
            break
    if not paths:
        continue
    for p in paths:
        if PAT.search(p):
            print(f"{name}\t{p}")
            hits += 1
print(f"# hits: {hits}", file=sys.stderr)
