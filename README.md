# Game Performance Overlay (Windows 11)

[![GitHub Release](https://img.shields.io/github/v/release/tiendung21111/GamePerfOverlay?color=10ff50&label=Download)](https://github.com/tiendung21111/GamePerfOverlay/releases)

> 🚀 **[TẢI VỀ BẢN CHẠY NGAY TẠI ĐÂY (GITHUB RELEASES)](https://github.com/tiendung21111/GamePerfOverlay/releases)**  
> *(Tải file `GamePerfOverlay-v1.0.0-win-x64.zip` về giải nén ra là chạy được ngay trên mọi máy tính Windows 10/11, không cần cài đặt .NET SDK hay clone mã nguồn).*

---

Ứng dụng đo nhiệt độ, xung nhịp và % hiệu năng CPU, GPU, VRAM, RAM và FPS siêu nhẹ, giao diện thuần hiển thị (100% trong suốt, chữ xanh neon Game Bar), tối ưu cho game thủ trên Windows 10/11.

---

## 1. Tính Năng Nổi Bật

- **Giao diện thuần hiển thị (Pure HUD):**
  - Nền hoàn toàn trong suốt 100%, không viền, không thanh tiêu đề, không biểu đồ sóng gây rối mắt.
  - Toàn bộ chữ và số hiển thị màu xanh neon Xbox (`#10ff50`), font monospace `Consolas` in đậm kèm hiệu ứng đổ bóng mờ đen (text-shadow) giúp đọc rõ trên mọi nền game (kể cả cảnh tuyết, trời sáng).
  - Cố định gọn gàng tại góc trên bên trái màn hình (`Top = 20px, Left = 20px`).
- **Chuột xuyên thấu 100% (Permanent Click-Through):**
  - Tích hợp cờ Win32 `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`: Mọi cú click chuột, ngắm bắn hay lia góc nhìn trong game đều xuyên thẳng qua cụm số, tuyệt đối không cướp focus hay gây khựng game.
- **Phím tắt toàn cục:**
  - Nhấn tổ hợp phím **`Ctrl + D + M`** để bật hoặc ẩn tức thì cụm thông số.
- **Đo FPS Zero-Hook (Anti-Cheat Safe 100%):**
  - Đo tốc độ khung hình tức thời (cập nhật 100ms) thông qua cơ chế Event Tracing for Windows (ETW) của Microsoft (tương tự Xbox Game Bar / Intel PresentMon).
  - Không hook hay inject DLL vào game, an toàn tuyệt đối với mọi hệ thống chống gian lận (EasyAntiCheat, BattlEye, Vanguard).
  - Tự động bỏ qua các tiến trình desktop (`explorer.exe`, `dwm.exe`) và hiển thị `-- FPS` khi không chơi game 3D.
- **Tương thích đa phần cứng:**
  - Hỗ trợ toàn diện CPU Intel (Core i3/i5/i7/i9) và AMD Ryzen.
  - Hỗ trợ GPU NVIDIA GeForce (RTX/GTX), AMD Radeon và Intel Arc/Iris Xe.
  - Tự động phân loại VRAM Dedicated (card rời) và VRAM Shared (card onboard).
- **Siêu nhẹ & Tiết kiệm tài nguyên:**
  - Viết bằng C# WPF native trên nền .NET 10.
  - Chu kỳ đo FPS 100ms siêu nhạy, cảm biến phần cứng 300ms, mức chiếm dụng CPU cực thấp (< 0.1%) và RAM < 30 MB.

- **Tự khởi động cùng Windows (Chạy ẩn - Không cần mở tay):**
  - Tích hợp sẵn tùy chọn trong menu chuột phải dưới khay hệ thống (System Tray).
  - Sử dụng cơ chế Windows Task Scheduler với quyền `HIGHEST`: Tự động khởi chạy ngầm khi bạn bật máy tính mà **không bị hiện bảng thông báo UAC làm phiền**.
  - Khi bật máy, app sẽ âm thầm chờ sẵn dưới nền; bạn vào game chỉ việc nhấn **`Ctrl + D + M`** là xem được ngay.

---

## 2. Hướng Dẫn Sử Dụng

### Cách 1: Thiết Lập Bật Máy Tự Chạy Ngầm (Khuyên Dùng)
1. Mở thư mục `publish/` $\rightarrow$ nhấp chuột phải vào **`GamePerfOverlay.exe`** $\rightarrow$ chọn **Run as administrator**.
2. Nhìn xuống góc phải màn hình (khay hệ thống cạnh đồng hồ), nhấp chuột phải vào biểu tượng ứng dụng $\rightarrow$ tích chọn **"Khởi động cùng Windows (chạy ẩn)"**.
3. Từ lần sau, mỗi khi bật máy:
   - Bạn **không cần click mở file `.exe` nữa**.
   - App đã tự chạy ngầm sẵn, bạn vào game chỉ việc ấn **`Ctrl + D + M`** để bật/tắt HUD thông số!

### Cách 2: Khởi Chạy Thủ Công Từng Lần
1. Mở file: `publish/GamePerfOverlay.exe` (Run as administrator).
2. Nhấn **`Ctrl + D + M`** để ẩn hoặc hiện bất cứ lúc nào.
3. Muốn tắt hẳn app: Chuột phải vào icon ở khay hệ thống $\rightarrow$ chọn **Thoát**.

### Mang Sang Máy Khác (Portable):
- Sao chép toàn bộ thư mục `publish/` (hoặc file `GamePerfOverlay.exe`) sang USB/Google Drive.
- Có thể chạy trực tiếp trên bất kỳ máy tính Windows 10/11 64-bit nào mà không cần cài đặt thêm .NET SDK.
- *Lưu ý về đo nhiệt độ trên Windows 11:* Ứng dụng sử dụng driver phần cứng chuẩn mới `PawnIO` (thay thế WinRing0 cũ hay bị Defender chặn). Nếu máy mới hiển thị `--°C`, chỉ cần mở PowerShell chạy `winget install namazso.PawnIO` là nhiệt độ sẽ nhận diện tức thì.

---

## 3. Cấu Trúc Dự Án

```
GamePerfOverlay/
├── App.xaml / App.xaml.cs            # Khởi tạo dịch vụ, khay hệ thống và điều phối vòng lặp 100ms
├── MainWindow.xaml / .cs             # Giao diện HUD trong suốt, thiết lập cờ Click-through Win32
├── app.manifest                      # Yêu cầu quyền Administrator để đọc cảm biến phần cứng
├── Models/
│   └── HardwareMetrics.cs            # Dữ liệu 5 dòng (FPS, CPU, GPU, VRAM, RAM)
├── ViewModels/
│   └── OverlayViewModel.cs          # Data-binding tối ưu tránh vẽ lại thừa
├── Services/
│   ├── SensorService.cs              # Đọc CPU, GPU, RAM, VRAM bằng LibreHardwareMonitorLib
│   ├── FpsTracker.cs                 # Lắng nghe sự kiện ETW DXGI Present (Zero-Hook)
│   ├── FpsCalculator.cs              # Tính toán tốc độ khung hình cửa sổ trượt
│   ├── HotkeyChordDetector.cs        # Nhận diện tổ hợp phím Ctrl + D + M đồng thời
│   └── HotkeyManager.cs              # Windows Hook bàn phím toàn cục (WH_KEYBOARD_LL)
└── Interop/
    └── NativeMethods.cs              # Định nghĩa các hàm Win32 API
```
