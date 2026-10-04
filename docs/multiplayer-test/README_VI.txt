GÓI MOD THỬ NGHIỆM 13 MOD (STAGING) - CONAN EXILES ENHANCED
===========================================================

!!! ĐÂY LÀ THẾ GIỚI THỬ NGHIỆM (TEST WORLD) !!!
Mọi tiến trình (nhân vật, nhà, đồ, thrall...) CÓ THỂ BỊ XÓA bất cứ lúc nào.
Đây KHÔNG phải server chính thức.

Gói này chỉ chứa file mod (.pak), file thứ tự mod (modlist.txt) và file kiểm tra.
Không có file game, không có .exe/.dll. Bạn cần game Conan Exiles bản quyền trên Steam.


1. CHÉP FILE MOD VÀO ĐÂU
------------------------
a) Trong Steam: chuột phải "Conan Exiles" -> Manage (Quản lý) -> Browse local files
   (Duyệt tệp cục bộ). Một thư mục sẽ mở ra, ví dụ:
       ...\steamapps\common\Conan Exiles
b) Vào thư mục ConanSandbox. Nếu chưa có thư mục "Mods" thì tạo mới:
       ...\steamapps\common\Conan Exiles\ConanSandbox\Mods
c) Nếu trong Mods đã có mod cũ: chép cả thư mục Mods ra chỗ khác để sao lưu,
   rồi xóa các file .pak cũ và modlist.txt cũ trong Mods.
d) Chép TẤT CẢ file trong thư mục "Mods" của gói này (13 file .pak + modlist.txt)
   vào ...\ConanSandbox\Mods
e) Nếu bạn đang Subscribe (đăng ký) các mod này trên Steam Workshop, hãy
   Unsubscribe trong thời gian test, để game không dùng bản Workshop khác phiên bản.


2. THỨ TỰ MOD (KHÔNG ĐƯỢC ĐỔI)
------------------------------
File modlist.txt trong gói đã đúng thứ tự, chỉ cần chép nguyên file:
    1. StackMe10K.pak
    2. SavageParagon.pak
    3. GritandGrease.pak
    4. ThrallReputation.pak
    5. ImprovedThrallsAndQoL.pak
    6. WO_RidingThralls.pak
    7. Ancient_Realms.pak
    8. Cannibal_Captivity.pak
    9. NightTerrors.pak
   10. PvEPlusAmbush.pak
   11. PlayerDBNO.pak
   12. Simple_Minimap.pak
   13. ChestLabels.pak


3. KIỂM TRA ĐÚNG GÓI MOD
-------------------------
Mở PowerShell (bấm Start, gõ "PowerShell"), rồi chạy 2 dòng sau.
Sửa đường dẫn trong ngoặc kép cho đúng máy của bạn:

    cd "D:\SteamLibrary\steamapps\common\Conan Exiles\ConanSandbox\Mods"
    Get-Content "D:\Downloads\GoiMod13\SHA256SUMS.txt" | % { $h,$f = $_ -split '\s+',2; if ((Get-FileHash $f).Hash -eq $h) { "OK   $f" } else { "SAI  $f" } }

Kết quả đúng: 13 dòng, dòng nào cũng bắt đầu bằng "OK".
Nếu có dòng "SAI" hoặc báo lỗi không tìm thấy file: chép lại file đó từ gói.
Danh sách đầy đủ (tên mod, mã Workshop, SHA-256) nằm trong TEST_PACK_MANIFEST.txt.


4. MỞ GAME
----------
Mở Conan Exiles bình thường qua Steam (đăng nhập Steam như mọi khi).
Trong menu Mods của game, bạn sẽ thấy 13 mod theo đúng thứ tự trên.


5. VÀO SERVER BẰNG DIRECT CONNECT
---------------------------------
a) Mở Radmin VPN và vào mạng (network) mà chủ server gửi riêng cho bạn.
   Tên mạng và mật khẩu Radmin chỉ gửi qua tin nhắn riêng, không ghi trong file này.
b) Trong game: Play Online (Chơi trực tuyến) -> nút Direct Connect (Kết nối trực tiếp).
c) Nhập:
       IP:   <IP Radmin của chủ server, ví dụ 26.x.x.x - hỏi chủ server>
       Port: 7777
       Mật khẩu: để trống (trừ khi chủ server báo khác)
d) Bấm Connect. Lần đầu vào có thể load hơi lâu.


6. NHẮC LẠI: ĐÂY LÀ THẾ GIỚI THỬ NGHIỆM
---------------------------------------
- Server này chỉ để thử mod chơi nhiều người.
- Tiến trình có thể bị xóa hoặc quay lại bản sao lưu cũ bất cứ lúc nào.
- Đừng đầu tư nhiều thời gian vào nhà/đồ ở đây.
- Server chính thức (production) sẽ là một thế giới MỚI, làm sau.


7. BÁO LỖI
----------
Khi gặp lỗi, chụp màn hình (hoặc quay video ngắn) rồi gửi cho chủ server theo mẫu:

Player (Người chơi):
Time (Thời gian):
Number of players online (Số người đang online):
Location (Vị trí trên bản đồ):
Mod/system being tested (Mod/hệ thống đang test):
What happened (Chuyện gì xảy ra):
Expected behavior (Lẽ ra phải thế nào):
Can reproduce (Làm lại có bị lại không - Có/Không):
Screenshot/video (Ảnh/video):
Server restart required (Có phải khởi động lại server không):
Additional notes (Ghi chú thêm):

Cảm ơn bạn đã giúp test!
