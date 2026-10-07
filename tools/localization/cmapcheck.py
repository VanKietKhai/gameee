import struct, glob, os
VI = "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ"

def cmap(data):
    num = struct.unpack_from(">H", data, 4)[0]
    tables = {}
    for i in range(num):
        tag, _, off, ln = struct.unpack_from(">4sIII", data, 12 + 16 * i)
        tables[tag] = off
    c = tables[b"cmap"]
    n = struct.unpack_from(">H", data, c + 2)[0]
    cps = set()
    for i in range(n):
        pid, eid, off = struct.unpack_from(">HHI", data, c + 4 + 8 * i)
        o = c + off
        fmt = struct.unpack_from(">H", data, o)[0]
        if fmt == 4:
            segx2 = struct.unpack_from(">H", data, o + 6)[0]; seg = segx2 // 2
            ends = struct.unpack_from(">%dH" % seg, data, o + 14)
            starts = struct.unpack_from(">%dH" % seg, data, o + 16 + segx2)
            deltas = struct.unpack_from(">%dh" % seg, data, o + 16 + 2 * segx2)
            ro_pos = o + 16 + 3 * segx2
            ros = struct.unpack_from(">%dH" % seg, data, ro_pos)
            for k in range(seg):
                for ch in range(starts[k], ends[k] + 1):
                    if ch == 0xFFFF: continue
                    if ros[k] == 0: g = (ch + deltas[k]) & 0xFFFF
                    else:
                        gp = ro_pos + 2 * k + ros[k] + 2 * (ch - starts[k])
                        g = struct.unpack_from(">H", data, gp)[0]
                        if g: g = (g + deltas[k]) & 0xFFFF
                    if g: cps.add(ch)
        elif fmt == 12:
            ng = struct.unpack_from(">I", data, o + 12)[0]
            for k in range(ng):
                s, e, g = struct.unpack_from(">III", data, o + 16 + 12 * k)
                cps.update(range(s, e + 1))
    return cps

for f in sorted(glob.glob(r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad\fonts\**\*.ufont", recursive=True)):
    raw = open(f, "rb").read(); n0 = struct.unpack_from("<I", raw, 0)[0]; d = raw[4:4+n0]; tail = len(raw) - 4 - n0
    if d[:4] not in (b"\x00\x01\x00\x00", b"OTTO", b"true"):
        print(os.path.basename(f), "not a raw TTF, header", d[:8]); continue
    try:
        cps = cmap(d)
    except Exception as e:
        print(os.path.basename(f), "ERR", e); continue
    miss = [ch for ch in VI if ord(ch) not in cps]
    print(f"{os.path.basename(f):42} tail={tail} glyphs={len(cps):5} vietnamese_missing={len(miss)} {''.join(miss[:20])}")
