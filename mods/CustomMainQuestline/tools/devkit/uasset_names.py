"""Read-only: list distinct printable strings (ASCII and UTF-16LE) in uncooked .uasset/.umap files matching a regex."""
import re, sys

pat = re.compile(sys.argv[1], re.I)
a_re = re.compile(rb'[\x20-\x7e]{5,}')
w_re = re.compile(rb'(?:[\x20-\x7e]\x00){5,}')
for f in sys.argv[2:]:
    b = open(f, 'rb').read()
    out = []
    seen = set()
    for m in a_re.finditer(b):
        s = m.group().decode('ascii')
        if pat.search(s) and s not in seen:
            seen.add(s); out.append(s)
    for m in w_re.finditer(b):
        s = m.group().decode('utf-16-le')
        if pat.search(s) and s not in seen:
            seen.add(s); out.append('[W] ' + s)
    print(f'=== {f.replace(chr(92), "/").split("/Content/")[-1]} ({len(out)})')
    for s in out:
        print('  ' + s[:220])
