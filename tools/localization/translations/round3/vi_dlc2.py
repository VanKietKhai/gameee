import json, ast
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
A = "theo phong cách cổ"
T = {
183:"Quầy Chợ Shem",
400:f"Một chiếc áo giáp {A}.",401:f"Một cánh cửa {A}.",402:f"Một bức tường {A}.",403:"Chân Tường Tượng Lớn Chư Hầu Vua Khổng Lồ",404:f"Cầu thang {A}.",405:f"Một thanh đại kiếm {A}.",
406:"Chỉ đặt được trên đất bạn đã chiếm",407:f"Một thanh đoản kiếm {A}.",409:f"Một chiếc ghế dài chạm khắc {A}.",410:f"Một chiếc ghế đẩu chạm khắc {A}.",411:f"Một chiếc bàn chạm khắc {A}.",
412:f"Một chiếc ghế chắc chắn {A}.",413:f"Một cây cột {A}.",414:f"Một ô cửa sổ {A}.",415:"Thân Tường Tượng Lớn Chư Hầu Vua Khổng Lồ",416:"Giáp ngực của vệ binh thành Shem.",417:"Một kệ sách kiểu Shem.",
418:"Một cột giữa kiểu Shem.",419:"Một cột góc kiểu Shem.",421:"Một chiếc bàn đầy dụng cụ để tạo hình kim loại.",422:f"Trần nhà {A}.",423:"Một chiếc yên chắc chắn được kỵ binh Shem ưa chuộng.",
424:"Một chiếc lồng thả chìm dùng để bắt sò ốc.",425:"Một xưởng thuộc da để biến da sống thành da thuộc.",426:"Một chiếc rương lớn trang trí để cất đồ.",427:f"Một ngọn đuốc đứng {A}.",
428:"Biểu tượng huy hiệu clan của Chư Hầu Vua Khổng Lồ.",429:"Huy hiệu clan của các đơn vị Kỵ Binh Shem.",431:f"Khung cửa {A}.",432:f"Một hàng rào thấp {A}.",433:f"Một cặp dao găm {A}.",
434:"Một con Tê Giác đồ sộ có nguồn gốc từ Shem.",435:"Bộ dụng cụ để se sợi và dệt vải.",436:"Một cánh cửa đôi kiểu Shem.",437:"Một đầu cột lớn kiểu Shem.",438:"Một nền móng nêm kiểu Shem.",
439:"Một loại cung của vệ binh thành Shem.",440:"Một đống lửa trại nhỏ để nấu ăn và sưởi ấm.",441:f"Nền móng {A}.",442:f"Một bức tường lớn {A}.",443:"Một chân cột lớn kiểu Shem.",
445:"Chiếc váy quyến rũ của Vũ Công Dao Shem.",446:"Tấm mạng quyến rũ của Vũ Công Dao Shem.",447:"Một chỗ ngồi để giải quyết nhu cầu vệ sinh.",448:"Một loại kiếm của vệ binh thành Shem.",
449:"Hộp sọ hươu gắn trên một tấm gỗ.",450:"Một chiếc bình dùng trong nghi lễ theo lối cổ xưa.",451:f"Một ô cửa sổ lớn {A}.",452:"Băng tay quyến rũ của Vũ Công Dao Shem.",453:"Một loại khiên của vệ binh thành Shem.",
454:"Một ngọn đuốc có giá đỡ, gắn được lên tường.",455:f"Trần nêm {A}.",456:"Băng chân quyến rũ của Vũ Công Dao Shem.",457:"Một lá cờ đứng hiển thị huy hiệu clan của bạn.",458:"Một chiếc lồng không có lối vào thông thường nào.",
459:f"Một bức tường trong {A}.",460:"Một đôi dép da, chắc chắn và thoải mái.",461:"Khung cửa đôi kiểu Shem.",462:f"Nền hàng rào {A}.",463:f"Nền móng nêm {A}.",464:"Áo giáp quyến rũ của Vũ Công Dao Shem.",
465:"Một chiếc bát tráng men xinh xắn, hơi sờn vì thời gian và sử dụng.",466:"Thân cột lớn kiểu Shem.",467:f"Một cặp dao găm đấm {A}.",468:"Những món đồ ngư dân Shem thường dùng.",469:"Một tấm ngăn đặt được để giữ chút kín đáo.",
470:"Đồ bảo vệ chân thích hợp khi cưỡi ngựa",471:"Một phiến đá dùng để giữ nạn nhân bị trói khi hiến tế.",472:"Bàn giả kim đầy dụng cụ chuyên dụng.",473:"Thực thi ý chí của Tam Hùng bằng sức mạnh chính xác. ",
474:"Tường răng cưa để đặt vạc công thành và cung thủ.",475:"Một băng đầu giữ tóc khỏi rơi xuống mặt.",476:"Thả mình trong bồn tắm sang trọng - một mình hay có bạn đồng hành.",
478:"Sơn chiến trang trí mô phỏng họa tiết nghi lễ cổ xưa.",479:"Túi ngủ gọn nhẹ, tiện dụng để ngủ trên mặt đất.",480:"Hòa mình vào đỉnh cao văn hóa Shem với bộ xây dựng này.",481:"Khi theo đuổi tri thức, ăn mặc cho ra dáng cũng có ích.",
482:"Bày quầy hàng của bạn với bộ sưu tập hàng hóa thú vị này.",483:"Một bồn dài để ngâm toàn thân khi tắm.",484:"Một bộ đồ nghề nghi lễ báng bổ, rung lên năng lượng hắc ám.",485:"Áp đảo kẻ thù và thrall với bộ sưu tập đáng sợ này.",
486:"Một chiếc bàn thiết kế cổ đầy dụng cụ để chế tạo giáp.",487:"Một chiếc lều gọn nhẹ, dựng và tháo nhanh.",488:"Với bộ giáp và vũ khí này, kẻ thù sẽ không bao giờ biết thứ gì đã hạ chúng.",
489:"Một con Tê Giác Shem Sơn Vẽ đáng gờm, kèm yên kỵ binh đồng bộ.",490:"Tôn vinh sự huy hoàng của một nền văn minh đã qua với bộ xây dựng này.",491:"Sẵn sàng cưỡi chiến với bộ tê giác Shem dũng mãnh và yên.",
492:"Một kệ sách gỗ lớn, xây theo phong cách Shem ấn tượng.",493:"Thêm chút sức sống và cây xanh cho công trình với bộ làm vườn này.",494:"Pha chế nọc độc mạnh như của Đại Xà với bộ dụng cụ giả kim này.",
495:"Bộ dụng cụ se sợi và dệt vải để trang trí xưởng của bạn.",496:"Một món trang trí hoa lá hay đồ cảnh để làm đẹp khu vườn hoặc sân hiên.",497:"Giáp nhẹ của Chư Hầu Vua Khổng Lồ, những kẻ ưa lén lút và mưu mẹo.",
498:"Một bộ giáp đáng gờm mà bắt mắt, thường được vệ binh thành Shem mặc.",499:"Một cách ghê rợn nhưng hiệu quả để cho kẻ thù thấy cái giá của sự chống đối.",500:"Một giỏ đầy nho Shem mới hái, sẵn sàng làm rượu.",
501:"Trang phục, đạo cụ, âm nhạc và bát hát rong - mọi thứ một nghệ sĩ cần để kiếm sống.",502:"Một xâu thú săn được, treo sẵn để lấy thịt, da và sản phẩm khác.",503:"Một bồn dài để ngâm toàn thân khi tắm. Bồn này đủ chỗ cho hai người.",
504:"Giáp vừa của Chư Hầu Vua Khổng Lồ, những kẻ ưa lén lút nhưng muốn được bảo vệ hơn chút.",505:"Một ngọn đèn dầu thanh lịch. Họa tiết tê giác trên tay cầm cho thấy nó là sản phẩm của Shem.",
506:"Bộ trang phục mỏng manh quyến rũ của vũ công biểu diễn Điệu Múa Dao truyền thống Shem.",507:"Chiến tranh là không thể tránh, và khi nó tới thì thật khủng khiếp. Hãy chuẩn bị với lò rèn Vua Khổng Lồ của riêng bạn.",
508:"Đồ nghề cho người làm rượu mới vào nghề lẫn lão luyện muốn tự làm rượu vang hảo hạng.",509:"Bộ dầu thơm, muối, thuốc mỡ và cồn thuốc dùng khi tắm.",
}
uniq = {}
for line in open(S + r"\dlc_unique.txt", encoding="utf-8"):
    i, rep = line.rstrip("\n").split("\t", 1); uniq[int(i)] = ast.literal_eval(rep)
vi = {uniq[i]: v for i, v in T.items()}
json.dump(vi, open(S + r"\dlc_vi_2.json", "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print(len(vi))
