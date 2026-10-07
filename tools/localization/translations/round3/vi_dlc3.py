import json, ast
S = r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad"
GKS = " Dù thoạt nhìn hình dáng có vẻ thô, nhìn kỹ sẽ thấy những đường rãnh có hoa văn mang dụng ý. Những người có thể giải thích ý nghĩa ban đầu của chúng đã không còn từ lâu."
LIT = "Biết chữ là dấu hiệu quan trọng của văn minh - nhờ chữ viết mà con người và các dân tộc truyền lại tri thức, kinh nghiệm và góc nhìn của mình, rất lâu sau khi họ đã ra đi.\n\n"
FISH = "Nghề cá phát đạt ở các thành phố ven biển Tây Shem. Biển Shem dồi dào đến mức sóng ánh bạc vì vảy cá bơi lượn, và không khí sát bến cảng nồng mùi muối biển, rong rêu và cá.\n\n"
WOOL = "Đồng cỏ, bình nguyên và đồi thoai thoải của Shem rất hợp để chăn nuôi những đàn cừu và dê lớn - nhưng giữa chính những đồng cỏ ấy cũng có những vạt lanh, bông và gai dầu.\n\nDù tơ lụa tốt nhất Hyboria đến từ Khitai, thợ dệt Shem vẫn làm ra vải và chỉ chất lượng cao. Từ khung cửi và con suốt, họ làm len, mohair và cashmere từ đàn gia súc; vải lanh và bông từ đồng bằng."
KYR = "<LoreItalics>'Kẻ thù của ta đã giết ta cả trăm lần bằng tin đồn,' Conan càu nhàu. 'Vậy mà ta vẫn ngồi đây nốc rượu Kyros.' Và hắn làm đúng như lời.</>\n— The Hour of the Dragon\n\nRượu đỏ lẫn trắng của Shem thường đậm, nhiều vị trái cây, mạnh mẽ - nhờ nho trồng trên vùng đất có ngày dài ấm áp, đất màu mỡ và đồi thoai thoải."
MKT = "\n\nChúng được bày biện đẹp nhất có thể để thu hút người mua ở chợ."
STEALTH = ", hoàn hảo cho trinh sát, gián điệp và lính gác muốn thấy mọi thứ mà không bị ai thấy."
VEND_H = "<LoreItalics>Đi trên những con đường ở Vendhya có thể rất nguy hiểm, thương nhân đoàn lữ hành đã học được rằng của cải mời gọi hiểm nguy. Bộ giáp chế tác tinh xảo đảm bảo cả thương nhân lẫn hàng hóa sống sót qua vùng hoang dã khắc nghiệt</>Đỉnh cao của sự bảo vệ, giáp thép là bức tường phòng thủ vững chắc trước mọi loại tấn công, nhưng trọng lượng cực lớn khiến người mặc phải hy sinh sự cơ động. "
GKB = "Các công trình của Chư Hầu Vua Khổng Lồ được xây theo cùng phong cách kiến trúc Vua Khổng Lồ, nhưng ở tỉ lệ hợp với con người hơn. Thế nhưng hãy nhìn kỹ cách xây và so với tàn tích của nền văn minh Vua Khổng Lồ rải rác khắp Vùng Đất Lưu Đày, bạn sẽ thấy sự bắt chước của các Chư Hầu còn kém ở đâu. Dù họ cố hết sức mô phỏng vẻ ngoài, họ không thể bắt chước phương pháp mà các Vua Khổng Lồ dùng để dựng nên công trình.\n\n"
T = {
510:"Thiết bị hữu ích này ép và tách chất lỏng khỏi nguyên liệu - như dầu từ cá sống hay hạt.",
511:"Một bộ trang phục sang trọng mà học giả Shem thường mặc. Nhuộm màu tươi đậm và trang trí bằng vàng.",
512:"Bộ trang bị tiêu chuẩn cho những người ở Shem có nhiệm vụ chạy đến mỗi khi ai đó hét \"Lính đâu! Lính đâu!\"",
513:"Bình phong không át được tiếng thét của những gì diễn ra phía sau, nhưng chúng che được tầm nhìn.",
514:"Một băng đầu mềm trang trí bằng vàng. Món phụ kiện hoàn hảo giữ tóc khỏi rơi xuống mặt trong những giờ học dài.",
515:"Những món đồ có thể thấy trong các chòi và lán săn rải rác khắp vùng đất khắc nghiệt, thô ráp Cimmeria.",
516:"Lông thú để ngủ mang lại sự thoải mái và giúp giữ ấm trên nền lạnh cứng của hầm mộ, hang động hay lâu đài.",
517:"Thường chỉ thấy trong nhà người giàu hay quý tộc, ghế có tay vịn và lưng tựa được coi là dấu hiệu của sự tao nhã.",
518:"Những tấm chăn len thô này có thể không êm nhất, nhưng đủ giữ ấm trước thời tiết.",
519:"Dùng để cất giữ lâu dài hay vận chuyển hàng đường dài, chiếc thùng chắc chắn này có mặt trên xe ngựa, trong kho và quán rượu.",
520:"Ngay cả những tên man rợ cứng rắn nhất cũng cần chút êm ái trong đời. Tìm sự thoải mái với bộ đồ dùng gia đình Cimmeria này.",
521:"Ngay cả những người lính dày dạn nhất cũng cần chỗ để buông vũ khí và nghỉ ngơi. Một bộ nội thất giản dị mà thanh lịch, hợp với doanh trại. ",
522:"Một chiếc áo choàng cầu kỳ kiểu Shem. Làm từ vải Shem mềm mà bền, nhuộm màu tươi đậm, loại áo này phổ biến trong giới học giả Shem.",
523:"Bộ đồ sinh tồn nhẹ và kín đáo"+STEALTH,
524:"Trong tay nhạc công lành nghề, nhạc cụ này tạo ra những giai điệu và hòa âm tuyệt diệu nhất. Trong tay kẻ nghiệp dư - thì không hẳn.",
525:"Lớp vải mềm sang trọng phủ xuống chân, nhuộm màu tươi đậm. Bộ trang phục giúp học giả Shem dành hàng giờ học tập trong thoải mái hoàn toàn.",
526:"Nhà xí mỗi vùng mỗi khác. Các vương quốc tiên tiến có hệ thống vệ sinh phức tạp, còn nơi đơn sơ chỉ là một tấm ván trên cái máng.",
527:"Với chút sáng tạo, một giá, lò nướng hay lò sấy chuyên dụng có thể dùng để phơi khô một số vật liệu. Phơi thịt giúp bảo quản. Gỗ khô thì cháy tốt hơn nhiều.",
528:"Chiếc giường này chẳng hơn một bục gỗ cứng chất đầy lông thú và chăn len thô là mấy. Tưởng chừng không thể, nhưng nó lại thoải mái đến bất ngờ.",
529:"Ô liu sống chát và khó ăn. Tốt hơn là ép lấy dầu hoặc ngâm muối."+MKT.replace("Chúng được", "Những quả ô liu này được"),
530:"Một chiếc bình khắc hoa văn nghi lễ, được Chư Hầu Vua Khổng Lồ dùng trong các nghi thức báng bổ. Máu, thịt, tro, chất độc và thuốc thử... một vật đa dụng, nhưng không vì thế mà bớt huyền bí.",
531:"Một ngọn đuốc lớn cắm xuống đất để chiếu sáng một khu vực. Có người dùng chúng làm đèn hiệu, soi đường về nhà trong những đêm tối nhất.\n\nCó thể bật tắt những ngọn đuốc này mà không cần nhiên liệu.",
532:"Một bồn gỗ lớn truyền thống để đạp nho. Là bước quan trọng khi làm rượu, nho thu hoạch cẩn thận theo truyền thống được phụ nữ và trẻ em Shem đạp nát bằng chân.",
533:"Một hộp phô mai Shem, làm từ sữa của những đàn cừu và dê lớn mà Shem nổi tiếng."+MKT.replace("Chúng được", "Những miếng phô mai này được"),
534:"Trong khi người khác dùng tạm vũng nước hay mảnh obsidian lớn, những ai may mắn vừa giàu vừa sống ở thành thị có thể dùng tấm kim loại lớn đánh bóng để chải chuốt.",
535:"Nơi cất giữ vật quý và đồ hiếm, chiếc rương ấn tượng này làm từ vật liệu tốt và trang trí đẹp mắt. Ổ khóa tinh xảo sẽ khiến mọi tên trộm chùn bước, trừ kẻ quyết tâm nhất. Dù vậy, không hệ thống nào hoàn hảo.",
536:"Phần lớn những bài tập mưu trí đầu tiên của loài người đến từ việc nhìn mặt nước và nghĩ cách bắt cá dễ hơn. Bẫy và lưới được đan từ nhiều loại sợi dai, giúp con người đuổi theo những con cá ngon.",
537:"Những ai luyện giả kim rồi sẽ học được rằng các hỗn hợp dễ phản ứng phải được cất cẩn thận, kẻo gây hậu quả không mong muốn. \n\nGiá này giúp người dùng cầu trong chiến đấu cất chúng an toàn khi không dùng. Cầm cẩn thận.",
538:"Ở dạng đơn giản nhất, lồng là một không gian kín làm từ song thép tôi để nhốt người hay thú. Được giới buôn nô lệ, thợ bẫy và cai ngục khắp nơi sử dụng, lồng có đủ cỡ, hình dạng và mức độ di động.",
539:"Một tủ lớn để cất và sắp xếp vô số thuốc thử của dược sư và thầy thuốc thảo dược dùng trong chữa bệnh. Tủ thuốc được phù thủy ưa chuộng, họ dùng các ngăn nhỏ để sắp xếp nguyên liệu làm phép.",
540:"Chiếc lều được may để dựng và gấp nhanh"+STEALTH+"\n\nKhi đã dựng lều, chỉ cần cúi người vào trong là có chỗ trú.",
541:"Từ khi con người bắt đầu tụ họp thành nhóm, cờ và biểu ngữ đã được dùng để nhận diện bạn hay thù. Tường thành các vương quốc Hyboria treo đầy cờ như thế, mỗi lá mang biểu tượng của một phe cầm quyền.",
542:"Đá hiến tế mỗi nền văn hóa một khác. Pháp sư Pict dùng những phiến đá granite thô có chỗ trũng nông để đặt nạn nhân, còn phù thủy hắc ám của Acheron giết nạn nhân trên những bàn thờ cầu kỳ khắc biểu tượng báng bổ.",
543:"Phòng thủ một pháo đài trong trận đánh đêm dễ hơn rất nhiều khi thấy được kẻ địch đến từ đâu.\n\nGắn đuốc lên tường giúp rảnh tay cầm kiếm hay giáo và soi sáng hành động của kẻ thù.",
544:"Một chiếc giường giản dị theo phong cách Vua Khổng Lồ."+GKS,
545:"Chiến binh khoác lác và thợ thủ công đều thích trang trí thành trì bằng vũ khí của kẻ thù đã bị chinh phục.\n\nNhững anh hùng khiêm tốn hơn vẫn thấy giá trưng bày hữu ích, nhất là để cất vũ khí ở những chỗ mà bình thường chúng trông lạc lõng.",
546:"Một chiếc ghế dài chạm khắc chắc chắn theo phong cách Vua Khổng Lồ."+GKS,
547:"Một chiếc ghế đẩu chạm khắc chắc chắn theo phong cách Vua Khổng Lồ."+GKS,
548:"Vùng Đất Lưu Đày đầy những sinh vật đáng sợ và mỗi lần chạm trán là chuyện sống còn. Hộp sọ của món kỷ vật này dường như thuộc về một loài hươu hung dữ thường thấy ở Cimmeria - to lớn, với cặp gạc sắc nhọn đủ xiên cả người lẫn thú.",
549:"Một chiếc ghế cứng và oai vệ theo phong cách Vua Khổng Lồ."+GKS,
550:"Một chiếc bàn chạm khắc tinh xảo theo phong cách Vua Khổng Lồ."+GKS,
551:"Dù người Cimmeria nổi tiếng gắn bó với thép đắng, họ cũng trọng da và len - thép để che chắn trên chiến trường, len để chống chọi thời tiết. Có phương tiện và hiểu biết để vá đồ là kỹ năng sinh tồn thiết yếu.",
553:"Một tấm thảm trang trí ngoạn mục làm từ len Shem hảo hạng nhất, được nhuộm và se tỉ mỉ thành những sợi mảnh màu ngọc lam, đỏ son và kem. Quy trình làm ra tác phẩm như vậy vừa tốn kém vừa công phu, và những tấm thảm xa hoa cỡ này chỉ có trong những sảnh đường thật sự vĩ đại của Hyboria.",
554:"Khi con người khám phá bí mật nông nghiệp, thế giới đã thay đổi. Không còn là những bộ lạc lang thang đánh nhau, các làng mạc và thành phố đầu tiên hình thành quanh ý tưởng rằng đi săn kiếm ăn vất vả hơn nhiều so với trồng trọt an toàn sau tường thành.\n\nGieo hạt vào chậu này sẽ khiến chúng mọc nhanh theo thời gian.",
555:"Các thành phố dọc bờ biển phía tây Shem có hải sản tươi quanh năm. Trong số những món biển được ưa chuộng nhất là sò ốc - tôm hùm nhiều thịt, cua ngọt, tôm mọng và đủ loại nhuyễn thể đủ cỡ đủ vị."+MKT.replace("Chúng được", "Những con sò ốc này được"),
556:KYR,
557:LIT+"Người ta nói quân đội tạo vinh quang cho vương quốc, còn thương nhân tạo của cải. Nhưng chỉ người chép sử mới tạo nên ký ức của một vương quốc.",
558:VEND_H, 559:VEND_H + " ",
560:WOOL,
561:"Điệu múa dao Shem là màn trình diễn kỹ năng truyền thống - động tác có thể gợi cảm, nhưng vũ công phải có sự khéo léo, sức mạnh và lòng tin vào bản thân lẫn bạn diễn để múa mà không làm ai bị thương. Thường biểu diễn trong lễ hội hay dịp ăn mừng, các điệu múa này thường dâng lên các vị thần sinh sản của Shem như Ishtar, Pteor và Derketo.",
562:"Trong mùa đông đơn điệu, không việc gì quan trọng hơn giữ củi chất cao và lửa luôn cháy. Là những người dẻo dai của phương bắc lạnh giá, đá sỏi, người Cimmeria hiểu rằng có sẵn củi nhiều khi là ranh giới giữa sống và chết.\r\n\r\nNhững đống củi như thế này thường đặt ngay ngoài nhà, để khi lửa cần thêm củi chỉ phải thò tay ra ngoài một chút.",
563:KYR+MKT.replace("Chúng được", "Những chai rượu này được"),
564:"Túi ngủ được may để trải và gấp nhanh"+STEALTH+"\n\nKhi ai đó chết ở Vùng Đất Lưu Đày, họ tỉnh lại ở nơi ngủ lần cuối mà không biết vì sao. Đặt túi ngủ sẽ đặt nó làm điểm hồi sinh của bạn, cho tới khi bạn đặt cái mới hoặc nó bị phá hủy. Đặt túi ngủ mới sẽ phá hủy cái cũ.",
565:LIT+"Ai đó đã cẩn thận cất những cuộn giấy này để phân loại sau. Ai biết chúng chứa thông tin gì? Có lẽ là thơ, luận văn hay nhật ký cũ. Có khi chỉ là lá thư phàn nàn về dịch vụ kém.",
566:LIT+"Ai đó đã cẩn thận cất những phiến đá này để phân loại sau. Ai biết chúng chứa thông tin gì? Có lẽ là thơ, luận văn hay nhật ký cũ. Có khi chỉ là lá thư phàn nàn về dịch vụ kém.",
567:FISH+"Phần lớn những bài tập mưu trí đầu tiên của loài người đến từ việc nhìn mặt nước và nghĩ cách bắt cá dễ hơn. Lưới như thế này giúp vớt được nhiều cá cùng lúc.",
568:"Một ngọn đuốc lớn cắm xuống đất để chiếu sáng một khu vực. Có người dùng chúng làm đèn hiệu, soi đường về nhà trong những đêm tối nhất.\n\nChư Hầu Vua Khổng Lồ dùng những ngọn đuốc này trong nghi lễ. Ánh lửa chập chờn tạo không khí uy nghiêm cho nghi thức, lóe trên lưỡi dao nghi lễ khi chúng rạch qua da thịt hay cắm vào tim những vật tế đang gào thét.\n\nCó thể bật tắt những ngọn đuốc này mà không cần nhiên liệu.",
569:"<LoreItalics>Những kẻ lưu đày Vendhya thanh lịch không vì sinh tồn mà bỏ sự tao nhã. Giáp của họ được tạo hình từ vải quý và hoa văn tinh xảo, mang cái đẹp vào một thế giới chỉ biết tàn bạo.</> Bảo vệ tốt hơn hẳn giáp nhẹ, giáp vừa này cân bằng giữa phòng thủ và linh hoạt. Cách tiếp cận trung dung này có đánh đổi riêng, vì nó không chắc bằng giáp nặng cũng không linh hoạt bằng giáp nhẹ.",
570:"<LoreItalics>Người chết đã chết, chuyện đã qua là xong! Ta có con tàu, có đám thủy thủ thiện chiến và một cô nàng môi như rượu vang, chừng ấy là đủ rồi. Liếm vết thương đi, lũ khốn, và khui một thùng bia ra.</> - The Pool of the Black One\n\nCòn cách nào tốt hơn để chứng minh cho kẻ man rợ thấy văn minh là tốt bằng việc làm ướt cái lưỡi dữ dằn của chúng bằng rượu mạnh, rượu vang hay bia? Nghệ thuật lên men ban cho loài người món quà đồ uống. Những thùng gỗ này là trái tim của công việc thiêng liêng ấy.",
571:"<LoreItalics>Ta đã xin Totrasmek một liều bùa yêu, không ngờ hắn xảo trá và thù hận đến vậy. Hắn đưa ta một thứ thuốc để pha vào rượu của người tình, và thề rằng khi Alafdhal uống nó, chàng sẽ yêu ta say đắm hơn bao giờ hết và chiều theo mọi ước muốn của ta.</> - Shadows In Zamboula\n\nTừ một mụ phù thủy quê mùa lẩm bẩm bên lọ thuốc mỡ chữa thương, đến một Sát Thủ Bậc Thầy Katari pha chế loại độc không thể phát hiện, dụng cụ trên bàn giả kim giúp nhà giả kim biến nguyên liệu thô thành những bình thuốc kỳ diệu vô tận.",
572:"<LoreItalics> Hàm của một chiếc bẫy sắt đã sập vào chân hắn, răng cắm sâu và giữ chặt. Chỉ nhờ bắp chân cuồn cuộn cơ mà xương không bị vỡ vụn.</> - Red Nails\r\n\r\nNhững chiếc bẫy này ban đầu do quý tộc Zingara nghĩ ra để ngăn kẻ săn trộm xâm phạm điền trang. Hiệu quả của chúng nổi tiếng đến mức chẳng bao lâu thợ săn khắp các vương quốc phía tây đã dùng chúng để bắt thú.\r\n\r\nBẫy này chỉ có tác dụng với người chơi.",
573:"<LoreItalics>Với ánh lửa lấp lánh trên bộ giáp thép xanh, hắn như một pho tượng thép - sức mạnh dồn nén tạm lặng; không phải nghỉ ngơi, mà bất động trong khoảnh khắc, chờ hiệu lệnh để lại lao vào hành động khủng khiếp.</> - Black Colossus\n\nChế tạo giáp, nhất là giáp kim loại, cần rất nhiều dụng cụ đặc biệt. Bàn làm giáp này có thể làm mọi thứ, từ giáp da nhẹ nhất đến giáp thép tôi. Tất nhiên là nếu có đủ nguyên liệu.",
574:WOOL+MKT.replace("Chúng được", "Những cuộn chỉ này được"),
575:FISH+"Với tầm với vượt trội, giáo là vũ khí phổ biến trên chiến trường Hyboria. Cách dùng hiệu quả nhất là từ phía sau hàng chiến tuyến, đâm vào khe hở của tường khiên. Cây giáo này vừa là vũ khí cân bằng vừa là cọc để bêu đầu.",
576:GKB+"Mái dốc rất thực dụng trong bão cát vì mảnh vụn trôi khỏi mái chứ không đọng lại.",
577:"Sống ở thành phố là hòa mình vào văn minh và văn hóa - nhưng cũng có nghĩa là đông người sống sát nhau, chắc chắn sẽ sinh chuyện.\n\nỞ những thành phố lớn của Shem như Asgalun, Pelishtia và Kyros, vũ khí như thế này là trang bị tiêu chuẩn của vệ binh thành. Giáp tấm nặng đủ bảo vệ khi tình hình leo thang và cũng giúp cả dân thường lẫn tội phạm dễ nhận ra họ giữa đám đông.\n\nCây cung này hạ được cả kẻ thù là người lẫn không phải người, nhưng phụ thuộc nhiều vào tay nghề người dùng.",
578:"<LoreItalics>Họ là những chàng trai trẻ nhưng rắn rỏi, gân guốc, mang dáng vẻ chỉ có ở những kẻ bị nghịch cảnh dồn đến đường cùng. Họ mặc áo giáp xích và đồ da sờn cũ; kiếm đeo bên thắt lưng.</> - A Witch Shall be Born\r\n\r\nMùi hôi của xưởng thuộc da rất dễ nhận ra ở các thành phố lớn, và thợ thuộc da thường bị đẩy ra rìa thành phố hay làng mạc, tốt nhất là ở cuối dòng sông. \r\n\r\nVỏ cây là loại tanin phổ biến nhất để biến da sống thành da thuộc. Trong quá trình đó nó phân hủy thành Hắc ín, một chất xúc tác hữu ích cho các nhà giả kim.",
579:GKB+"Hàng rào thấp này không nhằm chặn kẻ thù mà để cung thủ phòng thủ có chỗ nấp giữa các phát bắn.",
}
uniq = {}
for line in open(S + r"\dlc_unique.txt", encoding="utf-8"):
    i, rep = line.rstrip("\n").split("\t", 1); uniq[int(i)] = ast.literal_eval(rep)
vi = {uniq[i]: v for i, v in T.items()}
json.dump(vi, open(S + r"\dlc_vi_3.json", "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print(len(vi))
