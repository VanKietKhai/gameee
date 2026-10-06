"""Read-only IoStore reader: list files in a .utoc, decompress selected packages from the .ucas (Oodle via the
Dev Kit's oo2core.dll) and print their printable strings. Works on copies in the scratchpad only."""
import ctypes, re, struct, sys

OODLE = r"D:\epic\CEUE5Devkit\Engine\Binaries\DotNET\AutomationTool\AutomationScripts\BuildGraph\net10.0\runtimes\win-x64\native\oo2core.dll"
INVALID = 0xFFFFFFFF


def fstring(b, o):
    n = struct.unpack_from("<i", b, o)[0]; o += 4
    if n == 0:
        return "", o
    if n < 0:
        return b[o:o - 2 * n].decode("utf-16-le").rstrip("\0"), o - 2 * n
    return b[o:o + n].decode("utf-8", "replace").rstrip("\0"), o + n


def read_toc(path):
    b = open(path, "rb").read()
    assert b[:16] == b"-==--==--==--==-", "not a utoc"
    (version,) = struct.unpack_from("<B", b, 16)
    (hsize, entries, nblocks, blocksz, nmeth, methlen, cblock, dirsize, parts) = struct.unpack_from("<9I", b, 20)
    (container_id,) = struct.unpack_from("<Q", b, 56)
    flags = b[80]
    seeds, nophash = struct.unpack_from("<I", b, 84)[0], struct.unpack_from("<I", b, 96)[0]
    o = hsize
    o += 12 * entries                                   # chunk ids
    offlens = []
    for i in range(entries):
        raw = b[o + 10 * i:o + 10 * i + 10]
        offlens.append((int.from_bytes(raw[0:5], "big"), int.from_bytes(raw[5:10], "big")))
    o += 10 * entries
    o += 4 * seeds + 4 * nophash
    blocks = []
    for i in range(nblocks):
        raw = b[o + 12 * i:o + 12 * i + 12]
        blocks.append((int.from_bytes(raw[0:5], "little"), int.from_bytes(raw[5:8], "little"),
                       int.from_bytes(raw[8:11], "little"), raw[11]))
    o += blocksz * nblocks
    methods = ["None"] + [b[o + methlen * i:o + methlen * (i + 1)].split(b"\0")[0].decode() for i in range(nmeth)]
    o += nmeth * methlen
    if flags & 0x04:  # signed
        (hs,) = struct.unpack_from("<i", b, o); o += 4 + 2 * hs + 20 * nblocks
    files = {}
    if dirsize:
        d = b[o:o + dirsize]
        mount, p = fstring(d, 0)
        nd = struct.unpack_from("<i", d, p)[0]; p += 4
        dirs = [struct.unpack_from("<4I", d, p + 16 * i) for i in range(nd)]; p += 16 * nd
        nf = struct.unpack_from("<i", d, p)[0]; p += 4
        fents = [struct.unpack_from("<3I", d, p + 12 * i) for i in range(nf)]; p += 12 * nf
        ns = struct.unpack_from("<i", d, p)[0]; p += 4
        strs = []
        for _ in range(ns):
            s, p = fstring(d, p); strs.append(s)

        def walk(di, prefix):
            name, child, sib, ff = dirs[di]
            path_ = prefix + (strs[name] + "/" if name != INVALID else "")
            fi = ff
            while fi != INVALID:
                fn, nxt, ud = fents[fi]
                files[path_ + strs[fn]] = ud
                fi = nxt
            c = child
            while c != INVALID:
                walk(c, path_); c = dirs[c][2]
        walk(0, mount)
    return dict(version=version, flags=flags, cblock=cblock, offlens=offlens, blocks=blocks, methods=methods, files=files)


def read_chunk(toc, ucas, idx, oodle):
    off, ln = toc["offlens"][idx]
    cb = toc["cblock"]
    first, last = off // cb, (off + ln - 1) // cb
    out = bytearray()
    with open(ucas, "rb") as f:
        for bi in range(first, last + 1):
            boff, csz, usz, meth = toc["blocks"][bi]
            f.seek(boff)
            data = f.read((csz + 15) & ~15)[:csz]
            if toc["methods"][meth] == "None":
                out += data[:usz]
            else:
                dst = ctypes.create_string_buffer(usz)
                r = oodle.OodleLZ_Decompress(data, ctypes.c_int64(csz), dst, ctypes.c_int64(usz), 1, 0, 0, None, 0, None, None, None, 0, 3)
                if r != usz:
                    raise RuntimeError(f"oodle decompress failed block {bi}: {r} != {usz}")
                out += dst.raw
    start = off - first * cb
    return bytes(out[start:start + ln])


def main():
    utoc, filt = sys.argv[1], re.compile(sys.argv[2], re.I)
    strpat = re.compile(sys.argv[3], re.I) if len(sys.argv) > 3 else None
    toc = read_toc(utoc)
    print(f"# version={toc['version']} flags={toc['flags']:#x} methods={toc['methods']} files={len(toc['files'])}")
    if toc["flags"] & 0x02:
        print("# ENCRYPTED container; cannot read"); return
    oodle = ctypes.WinDLL(OODLE)
    oodle.OodleLZ_Decompress.restype = ctypes.c_int64
    ucas = utoc[:-5] + ".ucas"
    for path, idx in sorted(toc["files"].items()):
        if not filt.search(path):
            continue
        data = read_chunk(toc, ucas, idx, oodle)
        seen, strings = set(), []
        for m in re.finditer(rb"[\x20-\x7e]{2,}", data):
            s = m.group().decode()
            if s not in seen and (strpat is None or strpat.search(s)):
                seen.add(s); strings.append(s)
        for m in re.finditer(rb"(?:[\x20-\x7e]\x00){2,}", data):
            s = m.group().decode("utf-16-le")
            if s not in seen and (strpat is None or strpat.search(s)):
                seen.add(s); strings.append("[W]" + s)
        print(f"=== {path} ({len(data)} B)")
        print("   " + " | ".join(x[:80] for x in strings[:400]))


main()
