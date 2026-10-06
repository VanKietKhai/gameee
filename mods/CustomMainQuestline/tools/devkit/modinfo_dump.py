"""Read-only: print the embedded modinfo JSON objects (without the long description) of .pak files."""
import json, mmap, os, re, sys

for path in sys.argv[1:]:
    with open(path, 'rb') as f, mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as m:
        seen = set()
        for mt in re.finditer(rb'\{\s*"[A-Za-z]+"\s*:', m):
            start = mt.start()
            chunk = m[start:start + 200_000]
            depth, end, in_str, esc = 0, None, False, False
            for i, c in enumerate(chunk):
                ch = chr(c)
                if in_str:
                    if esc: esc = False
                    elif ch == '\\': esc = True
                    elif ch == '"': in_str = False
                    continue
                if ch == '"': in_str = True
                elif ch == '{': depth += 1
                elif ch == '}':
                    depth -= 1
                    if depth == 0:
                        end = i + 1
                        break
            if end is None:
                continue
            try:
                obj = json.loads(chunk[:end].decode('utf-8'))
            except Exception:
                continue
            if not isinstance(obj, dict) or not any(k.lower().startswith('devkit') or k.lower() == 'minimumversion' for k in obj):
                continue
            slim = {k: v for k, v in obj.items() if k.lower() not in ('description',)}
            key = json.dumps(slim, sort_keys=True)
            if key in seen:
                continue
            seen.add(key)
            print(f'== {os.path.basename(path)} @ {start:#x}')
            print(json.dumps(slim, ensure_ascii=False))
