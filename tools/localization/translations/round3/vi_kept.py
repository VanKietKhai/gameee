import json, ast
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
T = {
0:"Sơn Chiến Aquilonia",1:"Máy khách",26:"Internet",33:"Thẻ Chiến Trận",34:"Chợ",
35:"Aaaaaaaaah!",55:"[Tiếp tục]",56:"[Dâng một khối Obsidian]",57:"[Dâng một miếng Thịt Thượng Hạng]",58:"[Dâng một ít Dung Dịch Giả Kim]",59:"[Dâng một giọt Máu]",60:"[Dâng một bình Máu Hiến Tế]",61:"[Dâng một nắm Lưu Huỳnh]",
62:"Sói Đất",63:"Pháp Quan Acheron",64:"Tín Đồ Mitra",65:"Tín Đồ Set",66:"Tín Đồ Yog",67:"Khắc Tinh Của Aja",68:"Cú Cắn Cá Sấu",69:"Bàn Thờ Ymir",70:"Tri Thức Tổ Tiên: Sơn Chiến Trang Trí",71:"Lao Cổ",
72:"Gai Phẫn Nộ",73:"Kẻ Hủy Diệt",76:"Sơn Chiến Hóa Thân",77:"Vinh Quang Asura",78:"Chuồng Chim",80:"Đá Nam Châm Của Baal-pteor",81:"Dao Cạo Của Baal-pteor",82:"Nghiệp Xấu",84:"Nỏ Thần Công",85:"Xe Phá Thành",
86:"Mỏ Quạ",87:"Mùi Chó Cái",88:"Cú Cắn",89:"Cú Cắn Cay Đắng",90:"Máu Đen",91:"Cơn Thịnh Nộ Của Bruargh Đen",92:"Vuốt Đen",93:"Thành Lũy Sắt Đen",94:"Trang Phục Cướp Biển Đen",95:"Kẻ Trích Máu Được Ban Phước",
96:"Nụ Hôn Derketo Được Ban Phước",97:"Ankh Mitra Được Ban Phước",98:"Pha Lê Máu",99:"Kẻ Trích Máu",100:"Kẻ Uống Máu",101:"Móng Vuốt Nhuốm Máu",102:"Vuốt Đẫm Máu",103:"Pha Lê Xanh Dương",104:"Thợ Đóng Hộp",
105:"Nhát Chém Sáng",106:"Lưỡi Giòn",107:"Kẻ Đấm Bốc",110:"Đồ Lề Của Kẻ Ăn Thịt Người",111:"Tàn Sát",112:"Thợ Làm Nến",113:"Kitin",114:"Phẩm Son Rệp",115:"Tim Lạnh",116:"Kẻ Tha Hóa",117:"Nhát Kết Liễu",
118:"Búa Của Crom",119:"Tử Thần Của Cây",120:"Rìu Mang Tử Thần",121:"Kẻ Lừa Dối",122:"Sơn Chiến Trang Trí",123:"Người Bảo Vệ",124:"Nụ Hôn Của Derketo",125:"Tiếng Nói Của Derketo",126:"Kẻ Phá Hủy",127:"Gai Bệnh Dịch",
128:"Diệt Vong",129:"Miệng Rồng",130:"Ý Chí Cạn Kiệt",131:"Cầu Kéo",132:"Kho Vũ Khí Chết Đuối",133:"Kiếm Của Kẻ Say",134:"Trang Phục Thợ Săn Cồn Cát",135:"Xúc Xắc Tám Mặt",136:"Kẻ Uống Của El",
138:"Sao Hôm",139:"Đồ Sử Thi Lưu Đày",140:"Kẻ Lưu Đày",141:"Bịt Mắt",142:"Kẻ Đâm Mắt",143:"Nhiệt Huyết",144:"Cơn Sốt",145:"Rìu Chiến Xương Quỷ",146:"Đại Kiếm Xương Quỷ",147:"Trường Mâu Xương Quỷ",148:"Khiên Xương Quỷ",
149:"Búa Xương Quỷ",150:"Chùy Xương Quỷ",151:"Lưỡi Câu",152:"Nắm Đấm Của Kẻ Chết Đuối",153:"Kẻ Xé Thịt",154:"Phát Bắn Văng",155:"Kẻ Đập Tan Kẻ Thù",157:"Ánh Lò Rèn",158:"Mảnh Quyền Năng",159:"Y Phục Người Khổng Lồ Băng",
160:"Phát Bắn Băng",161:"Xương Ma Cà Rồng",163:"Vết Nứt Sông Băng",164:"Trăng Lấp Lánh",165:"Nanh Yêu Tinh",166:"Golem Đi Theo",167:"Đêm Duyên Dáng",168:"Khắc Tinh Mồ Mả",169:"Kẻ Đào Mộ",170:"Cơn Giận Của Sói Lớn",
171:"Pha Lê Xanh Lá",172:"Bi Thương",173:"Thánh Hóa",174:"Dây Trói Của Đao Phủ",175:"Chùy Gada Của Hanuman",176:"Tổn Hại",177:"Nhức Đầu",178:"Kẻ Bổ Đầu",179:"Kẻ Đóng Băng Tim",180:"Lưỡi Kiếm Lò Sưởi",181:"Kẻ Xuyên Tim",
182:"Tranh Cãi Nảy Lửa",183:"Gậy Hạng Nặng",184:"Người Thừa Kế",185:"Di Sản",187:"Dao Găm Nanh Chó Săn",188:"Lưỡi Đói Khát",189:"Nữ Thợ Săn",190:"Lõi Băng",191:"Tầm Với Lây Nhiễm",192:"Không Thể Thỏa Mãn",
193:"Trường Kiếm Sắt",194:"Khiên Tròn Sắt",195:"Linh Miêu Đảo",196:"Lưỡi Răng Cưa",197:"Bước Rình Mồi Của Jhebbal Sag",198:"Kho Vũ Khí Jhil",199:"Giáp Hầm Jhil",200:"Sức Bền Của Jhil",201:"Rìu Lưỡi Ngang Của Jin",
202:"Vuốt Rừng Rậm",203:"Kẻ Cướp Khari",204:"Lính Khari",205:"Thép Khari",206:"Dao Của Kẻ Bất Lương",207:"Hiệp Sĩ",208:"Gõ",209:"Lễ Phục Của Kurak",210:"Kẻ Chém Chân",211:"Lemuria",212:"???? Thấp Cấp",213:"Đền Ley",
214:"Kẻ Giết Sư Tử",215:"Phát Bắn Xa",216:"Cú Vỗ Yêu",217:"Kẻ Đoạt Dục Vọng",218:"Kiếm Lai Dối Trá",219:"Xoáy Nước Maelstrom",220:"Ánh Sáng Maelstrom",221:"Sọ Pháp Sư",222:"Thảm Sát",223:"Kẻ Giết Voi Ma Mút",224:"Lính Đánh Thuê",
225:"Kẻ Ăn Kim Loại",226:"Cú Cắn Của Misha",227:"Sương Tang Tóc",228:"Công Lý Của Mitra",231:"Mặt Trời Ban Mai",232:"Gò Người Chết",233:"Hắc Kiếm Của Musashi",234:"Tổ Của Zath",235:"Đèn Đêm",236:"Ngón Tay Nhanh Nhẹn",
238:"Mái Chèo",239:"Obsidian",240:"Ngày Xửa Ngày Xưa Trên Dây Cung",243:"Mũi Sỏi",244:"Kẻ Xuyên Thủng",245:"Biểu Tượng Phượng Hoàng",246:"Kiếm Khắc Phượng Hoàng",247:"Chốn Hoan Lạc Của Derketo",248:"Hút Sức Mạnh",249:"Bộ Giáp Người Nguyên Thủy",
250:"Váy Nguyên Thủy",251:"Cung Khari Hoàn Mỹ",252:"Dao Găm Khari Hoàn Mỹ",253:"Kiếm Khari Hoàn Mỹ",254:"Động Đất",255:"Mũi Tên Run Rẩy",256:"Búa Thịnh Nộ",257:"Rìu Cuồng Nộ",259:"Còi Của Kẻ Bắt Chuột",260:"Mảnh Dao Cạo",
261:"Rìu Lưỡi Ngang Nổi Loạn",262:"Pha Lê Đỏ",263:"Đồ Tể Đáng Tin",264:"Kẻ Xé Toạc",265:"Sự Thật Được Hé Lộ",266:"Sương Giá",267:"Xé Rách",268:"Sóng Dữ",269:"Mũi Đá",270:"Lưỡi Lao Nhanh",271:"Thần Gặt Cát",272:"Bùa Chống Bọ Cạp",
273:"Lưỡi Của Set",274:"Kẻ Cắt Đứt",275:"Cú Cắn Của Bóng Tối",276:"Nọc Của Bóng Tối",277:"Lưng Đá Phiến",278:"Cú Cắn Cá Mập",279:"Kiếm Sắt Sắc",280:"Sáng Như Bạc",281:"Cò Mỏ Giày",283:"Nhát Chém Lặng Lẽ",
285:"Lưỡi Của Kẻ Sát Nhân",286:"Rắn Cắn",288:"Bài Ca Sắt",289:"Đấng Tối Cao",290:"Lớp Phủ Bóng Ma",291:"Khắc Tinh Bóng Ma",292:"Ngọn Tháp",293:"Mảnh Vụn",294:"Lao Thép",295:"Mũi Khâu",296:"Rìu Quen Quen Lạ Lạ",297:"Cháy Nắng",
298:"Xiềng Xích Đứt Gãy",299:"Đòn Bất Ngờ",300:"Kẻ Sinh Tồn",301:"Đòn Nhanh",302:"Ngựa Con Nhanh Nhẹn",303:"Phiến Đá Quyền Năng",304:"Lời Than Của Telith",305:"Nỗi Buồn Của Telith",306:"Cái Mỏ",307:"Đội Phương Trận Đen",
308:"Thằng Khốn Giòn",309:"Sự Chia Cắt",310:"Kẻ Mưng Mủ",311:"Kẻ Nghiệt Ngã",312:"Kẻ Xiên Người",313:"Cái Vồ",314:"Cây Sồi",315:"Kiếm Giấy Cói",316:"Kẻ Không Thể Hiểu",318:"Móng Tay Của Titan",319:"Người Cầm Đuốc",
320:"Chuyển Hóa",321:"Khắc Tinh Quỷ Lùn",322:"Chàm Thật",323:"Lãnh Nguyên",324:"Thú Ngà",325:"Hai Lần Trên Một Mũi Tên",326:"Song Quang",327:"Song Tiễn",328:"Thành Phố Không Tên",329:"Kho Vũ Khí Hầm",330:"Sào Nhảy",
331:"Cắt Mạch",332:"Mắt Độc",333:"Song Liềm Chiến",334:"Ấm Áp Mạnh Mẽ",335:"Lốc Xoáy",336:"Lưỡi Ác Độc",337:"Bóng Ma",338:"Cú Vung Hoang Dã",339:"Uy Nghi Của Mùa Đông",340:"Mắt Sói",341:"Bão Sói",342:"Kẻ Phá Vỡ Thế Giới",
343:"Gai Yakith",344:"Sải Bước Của Ymir",345:"Dao Chặt Yog",346:"Cái Chạm Của Yog",
352:"Sơn Chiến Khitan",353:"Sơn Chiến Pict",354:"Sơn Chiến Derketo",355:"Sơn Chiến Neca",356:"Kẻ Tận Dụng",357:"Dâng Trào",358:"Dâng Trào Hoang Dã",
359:"Trượng Phép",360:"Tượng Phép Thuật",361:"Bộ Đồ Thương Nhân Đoàn Lữ Hành",363:"Ma Thuật Học Tái Hiện",365:"Vườn Thú Lớn",366:"Hổ Răng Kiếm Pict Lớn",367:"Kẻ Phá Hoại Gurnakhi",369:"Thợ Tạo Hình Người Hầu Sắt",370:"Kẻ Gọi Quỷ Khitan",
373:"Mài Dao Găm",374:"Yên Tê Giác Đạo Quân Câm Lặng",375:"Kẻ Lưu Đày Quý Tộc",380:"Xích Đu Vendhya",381:"Sơn Chiến Turan",
388:"AMD Anti-Lag 2",391:"Sói Đất Ăn Xác",406:"Thrall Tìm Kho Báu Săn Đe",409:"Người Được Asura Chọn",410:"Tầm Nhìn Asura",412:"Chó Hư!",427:"Giám đốc điều hành",428:"Giám đốc vận hành",433:"Thuyền trưởng",
455:"Quỷ Dơi Vô Địch",456:"Ác Quỷ Vô Địch",457:"Jhil Non Vô Địch",461:"Clan",462:"Nhạc Menu Cổ Điển",463:"Rắn Hổ Mang Đầu Đàn",469:"Kẻ Nghiền Nát",477:"Linh Cẩu Quỷ",482:"Kẻ Chạy Cồn Cát",485:"Voi",486:"Nghĩa Địa Voi",
488:"Email:",494:"FPS: 60",514:"Biên Giới",515:"Vệ Sĩ Người Khổng Lồ Băng",519:"Ma Cà Rồng",520:"Vua Khổng Lồ",524:"Khỉ Đột Lưng Bạc",525:"Đại Xà",526:"Sói Đất Ăn Xác Lớn",527:"Linh Cẩu Lớn",540:"Người Chăm Chỉ",545:"Linh Cẩu",
549:"Thời Đại Sắt",550:"Ruột Sắt",551:"Phổi Sắt",552:"Mũi Đá Sắt",577:"Komodo Non",596:"Ánh sáng",597:"Đèn",606:"Lò Đốt Sen",607:"Hóa Thân Rận",609:"Chế độ laptop cấu hình thấp",614:"Lumen - Chất lượng GI",
624:"Lợn Hung Dữ",627:"Mi-Go Bắt Cóc",628:"Thí Nghiệm Mi-Go",635:"Phạt di chuyển:",636:"Cú Đá Của La",653:"Cận Thị",654:"Nemedia",657:"Mẹ Tổ",663:"Kẻ Nhào Lộn Nhanh Nhẹn",686:"Ăn Tạp",
732:"Ping",734:"Cận Vệ",737:"Báo Thù Điên Loạn",740:"Komodo Bạch Tạng Cuồng Nộ",741:"Sức Mạnh Cuồng Nộ",742:"Lưng Dao",745:"Hồi Sinh",747:"Xương Trỗi Dậy",748:"Quái Vật Trỗi Dậy",749:"Chiến Binh Trỗi Dậy",750:"Người Làm Lễ",751:"Nghi Lễ",752:"Sông",
756:"Hài Cốt Lang Thang",763:"Kẻ Ăn Xác",771:"Người Rắn Vũ Phu",772:"Người Rắn Lâu La",773:"Người Rắn Tàn Bạo",774:"Người Rắn Trinh Sát",775:"Nữ Thợ Săn Shaggai",776:"Vỡ Vụn",781:"Khỉ Gân Guốc",785:"Bồ Nông Siptah",786:"Tê Giác Siptah",
789:"Thi Sĩ",790:"Chiến Binh Xương",791:"Người Mang Skelos I",792:"Người Mang Skelos II",793:"Người Mang Skelos III",794:"Nhà Vô Địch Giáo Phái Skelos",797:"Kinh Hoàng Bò Lổm Ngổm",802:"Kẻ Tàn Sát",804:"Bóng Ma",806:"Gân Thép",811:"Studio",812:"Bảng Chuyển",
819:"Kiên Cường",820:"Kẻ Chặt Gân",837:"Kẻ Thoái Hóa",840:"Kẻ Nuốt Chửng",844:"Đao Phủ",845:"Kẻ Đói Khát",852:"Người Gác Cổng",853:"Kẻ Đi Trên Mồ",855:"Sứ Giả Điềm Gở",859:"Cơn Đói",861:"Quan Tòa",864:"Tai Họa Đồng Tộc",
865:"Kẻ Lang Thang Cô Độc",868:"Kẻ Tạo Liệt Sĩ",869:"Người Hát Rong",870:"Con Lai",874:"Kẻ Đi Không Ai Biết",875:"Người Giữ Nghi Lễ",877:"Học Giả",882:"Kẻ Tàn Sát",896:"Người Canh Gác",897:"Người Canh Gác Bên Trên",899:"Người Thuần Thú",
904:"Nụ Cười Nanh",905:"Chết Đuối Hai Lần",906:"Nhà Vô Địch Chết Đuối Hai Lần",907:"Lâu La Chết Đuối Hai Lần",924:"Người Linh Cẩu",929:"Kim Loại Gia Công",940:"Yeti",
968:"Người Giữ Đe",973:"Kẻ Chết Đuối",974:"Yêu Tinh",975:"Tiểu Quỷ",976:"Nữ Quái Harpy",979:"Chết Đuối Hai Lần",980:"Hầm",981:"Anh Em Sói",982:"Người Sói",988:"Sơn Chiến Yamatai",
}
uniq = {}
for line in open(S + r"\kept_unique.txt", encoding="utf-8"):
    i, f, rep = line.rstrip("\n").split("\t", 2); uniq[int(i)] = (f, ast.literal_eval(rep))
kept = json.load(open(S + r"\scope_kept.json", encoding="utf-8"))
vi = {uniq[i][1]: v for i, v in T.items()}
out = []
for x in kept:
    if x["en"] in vi and vi[x["en"]] != x["en"]:
        out.append({"file": x["file"], "ns": x["ns"], "key": x["key"], "en": x["en"], "old": x["en"], "vi": vi[x["en"]]})
json.dump(out, open(S + r"\trov_kept_english.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("overrides", len(out), "unique translated", len(T))
