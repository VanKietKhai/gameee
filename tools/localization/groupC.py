import json, re
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
rows = json.load(open(S + r"\groupC_rows.json", encoding="utf-8"))
TERMS = [
 ("Blood Crystal", "Pha Lê Máu"), ("Red Crystal", "Pha Lê Đỏ"), ("Blue Crystal", "Pha Lê Xanh Dương"), ("Green Crystal", "Pha Lê Xanh Lá"),
 ("Black Blood", "Máu Đen"), ("Frost Lotus", "Sen Băng"), ("Yellow Lotus", "Sen Vàng"), ("Purple Lotus", "Sen Tím"),
 ("Black Lotus", "Sen Đen"), ("Golden Lotus", "Sen Hoàng Kim"), ("Grey Lotus", "Sen Xám"), ("Crimson Lotus", "Sen Đỏ Thẫm"),
 ("True Indigo", "Chàm Thật"), ("Midnight Blue", "Lam Nửa Đêm"), ("Asura's Glory", "Vinh Quang Asura"), ("Highland", "Cao Nguyên"),
]
FIX = {  # whole-name fixes where the patch kept English or mixed word order
 "Blood Crystal": "Pha Lê Máu", "Red Crystal": "Pha Lê Đỏ", "Blue Crystal": "Pha Lê Xanh Dương", "Green Crystal": "Pha Lê Xanh Lá",
 "Black Blood": "Máu Đen", "True Indigo": "Chàm Thật", "Asura's Glory": "Vinh Quang Asura",
 "Shadespiced Crystal": "Pha Lê Tẩm Gia Vị U Ám", "Purple Lotus Seeds": "Hạt Giống Sen Tím",
}
out = []
for ns, k, en, vi in rows:
    if not vi or en.startswith("XX_") or not k.startswith("ItemTable_"): continue
    new = FIX.get(en)
    if new is None:
        new = vi
        for a, b in TERMS: new = new.replace(a, b)
        new = new.replace("Lò Đốt Lotus", "Lò Đốt Sen").replace("Hộ Vệ Crystal", "Hộ Vệ Pha Lê")
    if new != vi: out.append({"file": "Exiles_Items.locres", "ns": ns, "key": k, "en": en, "old": vi, "vi": new})
json.dump(out, open(S + r"\trov_Exiles_Items_names.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(out))
for x in out: print(f"{x['en']:36} {x['old']:34} -> {x['vi']}")
