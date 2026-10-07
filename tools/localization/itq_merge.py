import json, glob, ast
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
uniq = {}
for line in open(S + "\\itq_unique.txt", encoding="utf-8"):
    i, rep = line.rstrip("\n").split("\t", 1)
    uniq[int(i)] = ast.literal_eval(rep)
vi = {}
for f in glob.glob(S + "\\itq_vi_*.json"):
    for k, v in json.load(open(f, encoding="utf-8")).items():
        vi[uniq[int(k)]] = v
items = json.load(open(S + "\\tr_ImprovedThrallsAndQoL.json", encoding="utf-8"))
n = 0
for x in items:
    if x["en"] in vi:
        x["vi"] = vi[x["en"]]; n += 1
json.dump(items, open(S + "\\tr_ImprovedThrallsAndQoL.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("unique translated", len(vi), "/", len(uniq), "entries", n, "/", len(items))
