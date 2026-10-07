import json, glob, os, sys
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
# DBNO as json (same content as merge_dbno.py)
dbno = [
 {"ns":"","key":"EEA63C7A48500948B774189E80598911","en":"You have been downed! \nA clan member can save you before time runs out","vi":"Bạn đã bị hạ gục! \nThành viên clan có thể cứu bạn trước khi hết giờ"},
 {"ns":"","key":"F4166948460E54BDA995C789DCDD28FB","en":"(Press Interact to give up)","vi":"(Nhấn Tương tác để bỏ cuộc)"},
 {"ns":"","key":"A815C3BC4B84BA1916C508BC20853800","en":"Dying","vi":"Đang hấp hối"}]
json.dump(dbno, open(S + r"\tr_PlayerDBNO.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
base = glob.glob(S + r"\vihoa\**\Exiles_UI.locres", recursive=True)[0]   # original Viet hoa
nss, _ = load(base)
byns = {ns: keys for ns, keys in nss}
orig_keys = {(ns, k) for ns, keys in nss for k, h, s in keys}
added = 0
for f in sys.argv[1:]:
    for x in json.load(open(S + "\\" + f, encoding="utf-8")):
        if "vi" not in x: continue
        if (x["ns"], x["key"]) in orig_keys: print("skip existing", x["key"]); continue
        if x["ns"] not in byns: byns[x["ns"]] = []; nss.append([x["ns"], byns[x["ns"]]])
        byns[x["ns"]].append([x["key"], srchash(x["en"]), x["vi"]]); added += 1
out = S + r"\vihoa_build\Exiles_UI.locres"
open(out, "wb").write(save(nss))
chk, _ = load(out)
print("added", added, "total entries", sum(len(k) for _, k in chk))
