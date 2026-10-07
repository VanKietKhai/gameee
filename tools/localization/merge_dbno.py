import sys, glob, os
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
exec(open(S + r"\locres_rw.py", encoding="utf-8-sig").read().split("src = glob.glob")[0])
TR = {  # key: (english source exactly as in the mod, vietnamese)
 "EEA63C7A48500948B774189E80598911": ("You have been downed! \nA clan member can save you before time runs out",
                                      "Bạn đã bị hạ gục! \nThành viên clan có thể cứu bạn trước khi hết giờ"),
 "F4166948460E54BDA995C789DCDD28FB": ("(Press Interact to give up)", "(Nhấn Tương tác để bỏ cuộc)"),
 "A815C3BC4B84BA1916C508BC20853800": ("Dying", "Đang hấp hối"),
}
src = glob.glob(S + r"\vihoa\**\Exiles_UI.locres", recursive=True)[0]
nss, _ = load(src)
root = [n for n in nss if n[0] == ""][0]
have = {k for k, h, s in root[1]}
for k, (en, vi) in TR.items():
    assert k not in have, k
    root[1].append([k, srchash(en), vi])
out = os.path.join(S, "vihoa_build", "Exiles_UI.locres"); os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, "wb").write(save(nss))
chk, _ = load(out)
r = [n for n in chk if n[0] == ""][0][1]
print("entries", sum(len(k) for _, k in chk), "added:", [x for x in r if x[0] in TR])
