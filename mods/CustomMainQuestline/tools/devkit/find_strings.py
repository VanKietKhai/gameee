"""Read-only: search binaries for a regex in ASCII and UTF-16LE strings; print matches with context strings."""
import re, sys, os

pat = re.compile(sys.argv[1], re.I)
paths = sys.argv[2:]
a_re = re.compile(rb'[\x20-\x7e]{4,}')
w_re = re.compile(rb'(?:[\x20-\x7e]\x00){4,}')
for p in paths:
    files = [p] if os.path.isfile(p) else [os.path.join(r, f) for r, _, fs in os.walk(p) for f in fs]
    for f in files:
        try:
            b = open(f, 'rb').read()
        except OSError:
            continue
        found = []
        for m in a_re.finditer(b):
            s = m.group().decode('ascii')
            if pat.search(s): found.append(('A', m.start(), s))
        for m in w_re.finditer(b):
            s = m.group().decode('utf-16-le')
            if pat.search(s): found.append(('W', m.start(), s))
        if found:
            print(f'== {f} ({len(found)})')
            for kind, off, s in found[:60]:
                print(f'  {kind} {off:#x} {s[:200]}')
