import json, ast, re
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
# style prefixes (Vietnamese puts the noun first: "<noun> <style>")
STYLE = [("Giant-king Vassal: ", "Chư Hầu Vua Khổng Lồ: "), ("Giant-king Vassal ", "Chư Hầu Vua Khổng Lồ"), ("Giant-king Cultist ", "Tín Đồ Vua Khổng Lồ"),
         ("Shemite City Guard ", "Vệ Binh Thành Shem"), ("Shemite Knife Dancer ", "Vũ Công Dao Shem"), ("Shemitish Knife Dancer ", "Vũ Công Dao Shem"),
         ("Shemite Scholar ", "Học Giả Shem"), ("Shemite Cavalry ", "Kỵ Binh Shem"), ("Shemite Spinster ", "Thợ Dệt Shem"), ("Shemitish Market ", "Chợ Shem"),
         ("Shemitish ", "Shem"), ("Shemite ", "Shem"), ("Cimmerian ", "Cimmeria"), ("Vendhyan ", "Vendhya"), ("Aesir ", "Aesir"), ("Turanian ", "Turan"),
         ("Forlorn Crypt ", "Hầm Mộ Hoang Vắng"), ("Pictish Perch ", "Chòi Pict"), ("Yamatai Tower ", "Tháp Yamatai")]
NOUN = {
 "Rug":"Thảm","Loom":"Khung Cửi","Lyre":"Đàn Lia","Door":"Cửa","Ramp":"Dốc","Wall":"Tường","Walls":"Tường","Chair":"Ghế","Fence":"Hàng Rào","Frame":"Khung","Wedge":"Nêm","Spear":"Giáo",
 "Barrel":"Thùng","Mirror":"Gương","Stairs":"Cầu Thang","Latrine":"Nhà Xí","Planter":"Chậu Cây","Ceiling":"Trần","Fountain":"Đài Phun Nước","Oil Lamp":"Đèn Dầu","Center Pillar":"Cột Giữa",
 "Corner Pillar":"Cột Góc","Fish Trap":"Bẫy Cá","Tub Small":"Bồn Tắm Nhỏ","Tub Medium":"Bồn Tắm Vừa","Wine Cask":"Thùng Rượu","Wine Rack":"Giá Rượu","Doorframe":"Khung Cửa","Gate Door":"Cửa Cổng",
 "Foundation":"Nền Móng","Gate Frame":"Khung Cổng","Spear Epic":"Giáo Sử Thi","Drying Rack":"Giá Phơi","Fluid Press":"Máy Ép","Double Door":"Cửa Đôi","Ramp Corner":"Dốc Góc","Fishing Nets":"Lưới Đánh Cá",
 "Potted Plant":"Chậu Cây Cảnh","Writing Desk":"Bàn Viết","Library Shelf":"Kệ Sách","Stairs Corner":"Cầu Thang Góc","Folding Screen":"Bình Phong","Shellfish Trap":"Bẫy Sò Ốc","Standing Torch":"Đuốc Đứng",
 "Basket of Cloth":"Giỏ Vải","Vineyard Grapes":"Nho Vườn","Doors and Gates":"Cửa và Cổng","Bathing Supplies":"Đồ Tắm","Large Pillar Cap":"Đầu Cột Lớn","Slopes and Ramps":"Dốc và Đường Dốc",
 "Wedge Foundation":"Nền Móng Nêm","Double Door Frame":"Khung Cửa Đôi","Large Pillar Base":"Chân Cột Lớn","Large Sloped Wall":"Tường Dốc Lớn","Grape Stomping Tub":"Bồn Đạp Nho",
 "Scroll Storage Box":"Hộp Đựng Cuộn Giấy","Tablet Storage Box":"Hộp Đựng Phiến Đá","Stairs and Pillars":"Cầu Thang và Cột","Fermentation Barrel":"Thùng Ủ","Large Pillar Middle":"Thân Cột Lớn",
 "Olive Display":"Quầy Ô Liu","Cheese Display":"Quầy Phô Mai","Thread Display":"Quầy Chỉ","Shellfish Display":"Quầy Sò Ốc","Ceilings and Foundations":"Trần và Nền Móng","Large Sloped Wall Corner":"Tường Dốc Lớn Góc",
 "Horticultural Club":"Câu Lạc Bộ Làm Vườn","Busker Bowl":"Bát Hát Rong","Scholar Robe":"Áo Choàng Học Giả","Spinster Set":"Bộ Thợ Dệt",
 "Bed":"Giường","Bow":"Cung","Cage":"Lồng","Maul":"Búa Tạ","Bench":"Ghế Dài","Chest":"Rương","Stool":"Ghế Đẩu","Table":"Bàn","Sword":"Kiếm","Emblem":"Huy Hiệu","Shield":"Khiên","Bedroll":"Túi Ngủ",
 "Daggers":"Dao Găm","Orb Rack":"Giá Cầu","Bow Epic":"Cung Sử Thi","Maul Epic":"Búa Tạ Sử Thi","Scout Helm":"Mũ Trinh Sát","Ritual Jar":"Bình Nghi Lễ","Scout Tent":"Lều Trinh Sát","Wall Torch":"Đuốc Tường",
 "Sword Epic":"Kiếm Sử Thi","Scout Armor":"Giáp Trinh Sát","Scout Boots":"Giày Trinh Sát","Great Sword":"Đại Kiếm","Shield Epic":"Khiên Sử Thi","Short Sword":"Đoản Kiếm","Daggers Epic":"Dao Găm Sử Thi",
 "Scout Bracers":"Giáp Tay Trinh Sát","Impaled Heads":"Đầu Bị Xiên","Wedge Ceiling":"Trần Nêm","Warrior Armor":"Giáp Chiến Binh","Sentinel Armor":"Giáp Lính Gác","Sentinel Boots":"Giày Lính Gác",
 "Scout Campfire":"Lửa Trại Trinh Sát","Scout Warpaint":"Sơn Chiến Trinh Sát","Scout Loincloth":"Khố Trinh Sát","Armorer's Bench":"Bàn Thợ Giáp","Hanging Brazier":"Lò Than Treo","Tall Window Cap":"Đỉnh Cửa Sổ Cao",
 "Scout Armor Epic":"Giáp Trinh Sát Sử Thi","Scout Chestpiece":"Áo Giáp Trinh Sát","Sentinel Bracers":"Giáp Tay Lính Gác","Crenelated Fence":"Hàng Rào Răng Cưa","Fence Foundation":"Nền Hàng Rào",
 "Tall Window Base":"Chân Cửa Sổ Cao","Great Sword Epic":"Đại Kiếm Sử Thi","Punching Daggers":"Dao Găm Đấm","Short Sword Epic":"Đoản Kiếm Sử Thi","Alchemist's Bench":"Bàn Giả Kim","Firebowl Cauldron":"Vạc Lửa",
 "Sacrificial Stone":"Đá Hiến Tế","Sentinel Warpaint":"Sơn Chiến Lính Gác","Sentinel Headdress":"Mũ Lính Gác","Sentinel Loincloth":"Khố Lính Gác","Apothecary Cabinet":"Tủ Thuốc","Blacksmith's Bench":"Bàn Thợ Rèn",
 "Tall Window Middle":"Thân Cửa Sổ Cao","Warrior Armor Epic":"Giáp Chiến Binh Sử Thi","Sentinel Armor Epic":"Giáp Lính Gác Sử Thi","Sentinel Chestguard":"Giáp Ngực Lính Gác","Weapons Display Rack":"Giá Trưng Vũ Khí",
 "Large Statue Wall Cap":"Đỉnh Tường Tượng Lớn","Punching Daggers Epic":"Dao Găm Đấm Sử Thi","Walls":"Tường","Stairs":"Cầu Thang","Fences ":"Hàng Rào ","Windows":"Cửa Sổ","Floors and Ceilings":"Sàn và Trần",
 "Helm":"Mũ","Armor":"Giáp","Tasset":"Giáp Hông","Bracers":"Giáp Tay","Sandals":"Dép","Breastplate":"Giáp Ngực","Armor Epic":"Giáp Sử Thi","Shield Epic":"Khiên Sử Thi","Sickle Sword":"Kiếm Liềm",
 "Sickle Sword Epic":"Kiếm Liềm Sử Thi","Bow Epic":"Cung Sử Thi","Veil":"Mạng Che Mặt","Skirt":"Váy","Outfit":"Trang Phục","Armbands":"Băng Tay","Footwraps":"Băng Chân","Chestpiece":"Áo Giáp","Outfit Epic":"Trang Phục Sử Thi",
 "Khanjar":"Dao Khanjar","Khanjar Epic":"Dao Khanjar Sử Thi","Headband":"Băng Đầu","Banner":"Cờ","Tools":"Dụng Cụ","Rhino Saddle":"Yên Tê Giác","Fur Cot":"Giường Lông Thú","Tannery":"Xưởng Thuộc Da",
 "Game Trap":"Bẫy Thú","Deer Trophy":"Chiến Lợi Phẩm Hươu","Hanging Game":"Thú Treo","Clay Cookware":"Nồi Đất","Wool Blankets":"Chăn Len","Firewood Stack":"Đống Củi","Trapper's Lodge":"Nhà Thợ Bẫy",
 "Furs":"Lông Thú","Spices":"Gia Vị","Cushion Set":"Bộ Đệm","Perfume Bottle Set":"Bộ Chai Nước Hoa","Short":"Ngắn",
}
def name_vi(en):
    for a, b in STYLE:
        if en.startswith(a):
            rest = en[len(a):]
            if a.endswith(": "): return b + NOUN.get(rest, None) if NOUN.get(rest) else None
            n = NOUN.get(rest)
            return f"{n} {b}" if n else None
    return None
M = {
 "Light":"Nhẹ","Mount":"Thú cưỡi","Speed":"Tốc độ","Medium":"Vừa","Insulated":"Cách nhiệt","Dragon Katana":"Katana Rồng","An iron trap.":"Một chiếc bẫy sắt.","Pelishtian Spa":"Nhà Tắm Pelishtia",
 "Vassal's Estate":"Điền Trang Chư Hầu","Savage Comforts":"Tiện Nghi Man Rợ","Asshuri Cavalry":"Kỵ Binh Asshuri","Bladedancer Set":"Bộ Vũ Công Kiếm","Breaker of Wills":"Kẻ Bẻ Gãy Ý Chí","Soldier's Refuge":"Chốn Nghỉ Của Lính",
 "Shemite Academia":"Học Viện Shem","Spyrian Trappings":"Đồ Lề Spyria","The Living Shadow":"Bóng Tối Sống","Warmaker's Armory":"Kho Vũ Khí Của Warmaker","An enameled bowl.":"Một chiếc bát tráng men.",
 "An ornate brazier.":"Một lò than chạm trổ.","Kyrosian Winemaker":"Người Làm Rượu Kyros","center pillar short":"cột giữa ngắn","corner pillar short":"cột góc ngắn","A hard, sturdy cot.":"Một chiếc giường cứng, chắc chắn.",
 "A basket of grapes.":"Một giỏ nho.","A basket of olives.":"Một giỏ ô liu.","A desk for writing.":"Một chiếc bàn để viết.","Shemite Scholar Robe":"Áo Choàng Học Giả Shem","Ziggurats of Asgalun":"Kim Tự Tháp Asgalun",
 "Priestking's Sacristy":"Phòng Thánh Của Vua Tư Tế","Anakim City Guard Kit":"Bộ Vệ Binh Thành Anakim","A warp-weighted loom.":"Một khung cửi có quả nặng.","A basket of spun wool.":"Một giỏ len đã se.","Leather thong sandals.":"Dép xỏ ngón bằng da.",
 "Special rack for drying":"Giá đặc biệt để phơi","A common wooden barrel.":"Một chiếc thùng gỗ thông thường.","A storage rack of wine.":"Một giá chứa rượu.","The Great Serpent's Gift":"Món Quà Của Đại Xà",
 "Basket of Cimmerian Wool":"Giỏ Len Cimmeria","A rack for storing orbs.":"Một giá để cất cầu.","A tub meant for washing.":"Một chiếc bồn để giặt rửa.","Special rack for drying.":"Giá đặc biệt để phơi.",
 "A luxurious leg covering.":"Một món che chân sang trọng.","Bounty of the Western Sea":"Lộc Biển Tây","A bucket of fishing nets.":"Một xô lưới đánh cá.","A cushioned wooden chair.":"Một chiếc ghế gỗ có đệm.",
 "A storage box of scrolls.":"Một hộp đựng cuộn giấy.","A storage box of tablets.":"Một hộp đựng phiến đá.","A tall, decorative torch.":"Một ngọn đuốc cao trang trí.","A woven trap for fishing.":"Một chiếc bẫy đan để bắt cá.",
 "A box of assorted cheeses.":"Một hộp phô mai các loại.","A manual pressing machine.":"Một máy ép thủ công.","Sentinel of the Triumverate":"Lính Gác Của Tam Hùng","A variety of captured game.":"Nhiều loại thú săn được.",
 "Framework for a large gate.":"Khung cho một cánh cổng lớn.","You are not in this faction!":"Bạn không thuộc phe này!","Painted Shemitish Rhinoceros":"Tê Giác Shem Sơn Vẽ","A box of assorted shellfish.":"Một hộp sò ốc các loại.",
 "A set of comfortable cushions":"Một bộ đệm êm ái","A basket with bolts of cloth.":"Một giỏ đựng các súc vải.","A humble, yet comfortable cot.":"Một chiếc giường đơn sơ nhưng êm ái.","A rack for displaying weapons.":"Một giá để trưng vũ khí.",
 "A stringed musical instrument.":"Một nhạc cụ dây.","A large tub for grape-treading.":"Một bồn lớn để đạp nho.","A large barrel filled with wine.":"Một thùng lớn đầy rượu.","A rug made in a Shemitish style.":"Một tấm thảm kiểu Shem.",
 "A small shelf display of thread.":"Một kệ nhỏ trưng chỉ.","A set of earthen cooking vessels.":"Một bộ nồi niêu bằng đất.","A bow emulating an ancient style.":"Một cây cung theo phong cách cổ.","A botanical or decorative object.":"Một cây cảnh hoặc đồ trang trí.",
 "A door made in a Shemitish style.":"Một cánh cửa kiểu Shem.","A ramp made in a Shemitish style.":"Một đường dốc kiểu Shem.","A wall made in a Shemitish style.":"Một bức tường kiểu Shem.","Stairs made in a Shemitish style.":"Cầu thang kiểu Shem.",
 "A neatly folded pile of soft furs.":"Một chồng lông thú mềm gấp gọn.","A neatly folded stack of blankets.":"Một chồng chăn gấp gọn.","A helm emulating an ancient style.":"Một chiếc mũ theo phong cách cổ.",
 "A cauldron for mixing concoctions.":"Một chiếc vạc để pha chế.","A row of skulls impaled on stakes.":"Một hàng sọ xiên trên cọc.","A maul emulating an ancient style.":"Một cây búa tạ theo phong cách cổ.",
 "Planter made in a Shemitish style.":"Chậu cây kiểu Shem.","A wedge made in a Shemitish style.":"Một khối nêm kiểu Shem.","A spear made in a Shemitish style.":"Một cây giáo kiểu Shem.","Daggers made in a Shemitish style.":"Dao găm kiểu Shem.",
 "Bracers emulating an ancient style.":"Giáp tay theo phong cách cổ.","A sword emulating an ancient style.":"Một thanh kiếm theo phong cách cổ.","A helm worn by Shemite city guards.":"Chiếc mũ của vệ binh thành Shem.",
 "A well that fills slowly over time.":"Một cái giếng đầy nước dần theo thời gian.","A wooden cask used in fermentation.":"Một thùng gỗ dùng để ủ.","A window made in a Shemitish style.":"Một ô cửa sổ kiểu Shem.",
 "You don't have a Bounty Hunter Skull":"Bạn không có Sọ Thợ Săn Tiền Thưởng","Sturdy doors to keep enemies at bay.":"Những cánh cửa chắc chắn để chặn kẻ thù.","A shield emulating an ancient style.":"Một chiếc khiên theo phong cách cổ.",
 "Bracers worn by Shemite city guards.":"Giáp tay của vệ binh thành Shem.","Sandals worn by Shemite city guards.":"Dép của vệ binh thành Shem.","A ceiling made in a Shemitish style.":"Trần nhà kiểu Shem.",
 "You need an unconscious Bounty Hunter":"Bạn cần một Thợ Săn Tiền Thưởng bất tỉnh","A tasset worn by Shemite city guards.":"Giáp hông của vệ binh thành Shem.","An oil lamp set atop an ornate stand.":"Một ngọn đèn dầu đặt trên chân đèn chạm trổ.",
 "A carefully piled stack of raw lumber.":"Một đống gỗ thô xếp cẩn thận.","A loincloth emulating an ancient style":"Một chiếc khố theo phong cách cổ","Boots made emulating an ancient style.":"Đôi giày theo phong cách cổ.",
 "An array of various ablution products.":"Đủ loại đồ dùng tắm rửa.","A low fence made in a Shemitish style.":"Một hàng rào thấp kiểu Shem.","A tall wall made in a Shemitish style.":"Một bức tường cao kiểu Shem.",
 "A fine selection of Vendhyan fragrances":"Tuyển tập hương liệu Vendhya hảo hạng","A headdress emualting an ancient style.":"Một chiếc mũ theo phong cách cổ.","A loincloth emulating an ancient style.":"Một chiếc khố theo phong cách cổ.",
 "A cabinet with many small compartments.":"Một chiếc tủ có nhiều ngăn nhỏ.","A robe made from layered, lavish cloth.":"Một chiếc áo choàng nhiều lớp vải sang trọng.","A mirror made of highly polished metal.":"Một chiếc gương bằng kim loại đánh bóng.",
 "A doorframe made in an Shemitish style.":"Khung cửa kiểu Shem.","A foundation made in a Shemitish style.":"Nền móng kiểu Shem.","A chestguard emulating an ancient style.":"Một chiếc giáp ngực theo phong cách cổ.",
 "Shemite Busker Bowl":"Bát Hát Rong Shem","Shemite Spinster Set":"Bộ Thợ Dệt Shem","Shemite Spinster Tools":"Dụng Cụ Thợ Dệt Shem","Shemite Scholar Outfit Epic":"Trang Phục Học Giả Shem Sử Thi",
 "Giant-king Cultist Scout Armor":"Giáp Trinh Sát Tín Đồ Vua Khổng Lồ","Giant-king Cultist Warrior Armor":"Giáp Chiến Binh Tín Đồ Vua Khổng Lồ",
 "Giant-king Cultist Scout Armor Epic":"Giáp Trinh Sát Tín Đồ Vua Khổng Lồ Sử Thi","Giant-king Cultist Warrior Armor Epic":"Giáp Chiến Binh Tín Đồ Vua Khổng Lồ Sử Thi",
}
uniq = {}
for line in open(S + r"\dlc_unique.txt", encoding="utf-8"):
    i, rep = line.rstrip("\n").split("\t", 1); uniq[int(i)] = ast.literal_eval(rep)
vi = {}; miss = []
for i in range(400):
    en = uniq[i]
    if en.startswith("XXX_") or en == "TBD": continue
    v = M.get(en) or name_vi(en)
    if v: vi[en] = v
    else: miss.append((i, en))
json.dump(vi, open(S + r"\dlc_vi_1.json", "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print("translated", len(vi), "missing", len(miss)); print(miss)
