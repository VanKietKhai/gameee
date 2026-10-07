import struct, ctypes, re, sys, os, subprocess, glob
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad\modscan"
UP = r"D:\epic\CEUE5Devkit\Engine\Binaries\Win64\UnrealPak.exe"
oo = ctypes.WinDLL(r"D:\epic\CEUE5Devkit\Engine\Binaries\DotNET\UnrealBuildTool\runtimes\win-x64\native\oo2core.dll")
dec = oo.OodleLZ_Decompress; dec.restype = ctypes.c_int64
dec.argtypes = [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, ctypes.c_int64] + [ctypes.c_int]*3 + [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int64, ctypes.c_int]
def raw(base):
    t = open(base + ".utoc", "rb").read(); c = open(base + ".ucas", "rb").read()
    (hs, ec, cbc, cbs, cmc, cml, bsz, dis, pc) = struct.unpack_from("<9I", t, 20)
    seeds = struct.unpack_from("<I", t, 84)[0]; wo = struct.unpack_from("<I", t, 96)[0]
    off = hs + ec*22 + seeds*4 + wo*4
    out = bytearray()
    for i in range(cbc):
        b = t[off+i*12: off+i*12+12]
        o = int.from_bytes(b[0:5], "little"); cs = int.from_bytes(b[5:8], "little"); us = int.from_bytes(b[8:11], "little")
        src = c[o:o+cs]
        if b[11] == 0: out += src[:us]
        else:
            buf = ctypes.create_string_buffer(us)
            if dec(src, cs, buf, us, 1, 0, 0, None, 0, None, None, None, 0, 3) != us: raise Exception("dec")
            out += buf.raw
    return bytes(out)
def ftexts(d):
    res = {}
    for m in re.finditer(rb"[0-9A-F]{32}\x00", d):
        kp = m.start() - 4
        if kp < 0 or struct.unpack_from("<i", d, kp)[0] != 33: continue
        p = m.end(); n = struct.unpack_from("<i", d, p)[0]
        if 1 <= n <= 4000 and d[p+4+n-1] == 0:
            s = d[p+4:p+4+n-1]
            try: s = s.decode("utf-8")
            except: continue
        elif -4000 <= n <= -1:
            s = d[p+4:p+4-2*n-2].decode("utf-16le", "ignore")
        else: continue
        if re.search(r"[A-Za-z]{2}", s): res[m.group()[:-1].decode()] = s
    return res
mods = os.path.join(r"D:\steamnew\steamapps\common\Conan Exiles\ConanSandbox\Mods")
for pak in sorted(glob.glob(mods + r"\*.pak")):
    name = os.path.basename(pak)[:-4]; od = os.path.join(S, name)
    if not os.path.isdir(od): subprocess.run([UP, pak, "-Extract", od], capture_output=True)
    bases = [f[:-5] for f in glob.glob(od + r"\*-Windows.utoc")]
    if not bases: print(f"{name:32} (no IoStore container)"); continue
    try:
        d = raw(bases[0]); open(bases[0] + ".raw", "wb").write(d)
        ft = ftexts(d)
        print(f"{name:32} raw={len(d):>9}  texts={len(ft):>5}")
    except Exception as e: print(name, "ERR", e)
