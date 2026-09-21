# Interaction Pattern Library: Whisper Ward

> **Status**: Stable
> **Author**: UX Designer & Lead Programmer
> **Last Updated**: 2026-09-21
> **Version**: 1.0
> **Engine**: Unity 6 LTS (6000.3.17f1)
> **UI Framework**: Unity UI Toolkit (UXML/USS) cho Menu & Settings; UGUI Canvas cho Gameplay HUD & World Space Prompts
> **Related Documents**:
> - `design/accessibility-requirements.md` — Tiêu chuẩn trợ năng đã cam kết (Standard Tier)
> - `design/gdd/suspicion-meter-grade.md` — Quy cách thanh đo nghi ngờ & Grade
> - `design/gdd/audio-ui-feedback.md` — Quy cách phản hồi âm thanh & thị giác
> - `design/gdd/player-movement-hide.md` — Tương tác ẩn nấp HideSpot
> - `docs/architecture/architecture.md` — Kiến trúc Master

---

## 1. Overview & Mục đích

Tài liệu này là quy chuẩn duy nhất (Single Source of Truth) cho tất cả các mẫu tương tác giao diện (Interaction Patterns) trong Whisper Ward. Bất kỳ màn hình Menu, bảng Settings hoặc thành phần HUD nào cũng phải tái sử dụng trực tiếp các mẫu được định nghĩa tại đây, đảm bảo tính nhất quán về cảm giác điều khiển (Game Feel) và tuân thủ tuyệt đối chuẩn trợ năng **Standard Tier**.

---

## 2. Pattern Catalog Index

| Mã mẫu | Tên Pattern | Phân loại | Mục đích sử dụng | Framework |
|---|---|---|---|---|
| **PAT-NAV-01** | Primary Menu Button | Navigation | Nút bấm điều hướng chính (Start, Continue, Retry) | UI Toolkit |
| **PAT-NAV-02** | Secondary / Back Button | Navigation | Nút hủy bỏ, quay lại màn hình trước | UI Toolkit |
| **PAT-SET-01** | Option Slider | Input | Thanh trượt chỉnh âm lượng, độ nhạy chuột, HUD scale | UI Toolkit |
| **PAT-SET-02** | Toggle Switch | Input | Bật/tắt chế độ (Crouch Mode, Aim Mode, Motion Reduction) | UI Toolkit |
| **PAT-SET-03** | Keybind Remap Slot | Input | Ô bấm gán lại phím điều khiển (Keyboard, Mouse, Gamepad) | UI Toolkit |
| **PAT-HUD-01** | Directional Threat Chevron | Gameplay HUD | Mũi tên elip ($R_x=240, R_y=160$) chỉ hướng lính đang nghi ngờ | UGUI Canvas |
| **PAT-HUD-02** | Suspicion Meter & Sinking Notch | Gameplay HUD | Thanh đo nghi ngờ tích hợp vạch khấc ngưỡng phản xạ $T_{\text{entry}}$ | UGUI Canvas |
| **PAT-HUD-03** | Player Stance Indicator | Gameplay HUD | Icon hiển thị tư thế (Crouch, Walk, Sprint, Hidden) | UGUI Canvas |
| **PAT-WLD-01** | Contextual World Reticle | In-World | Ký hiệu nút nổi trên vật thể (Tủ trốn `[E]`, Nhặt Burst `[F]`) | UGUI World |

---

## 3. Standard Control Patterns (Menu & Settings)

### PAT-NAV-01: Primary Menu Button
* **Mô tả**: Nút hành động chính trên màn hình. Mỗi màn hình chỉ có tối đa một Primary Button nổi bật nhất.
* **Quy cách tương tác**:
  * **Default**: Nền màu xanh thép `#8FA3B8` (hoặc viền phát sáng), chữ trắng tương phản ≥ 4.5:1.
  * **Hover (Mouse)**: Tỉ lệ co giãn $1.03\times$, độ sáng tăng $+15\%$, đổi con trỏ chuột sang Pointer hand, phát âm thanh `UI_Hover_Click` ($80\text{ ms}$).
  * **Focus (Keyboard/Gamepad)**: Xuất hiện Focus Ring viền trắng $2\text{ px}$ bao quanh, tự động focus vào nút này khi mở màn hình.
  * **Pressed**: Thu nhỏ $0.97\times$, độ sáng $-10\%$. Kích hoạt sự kiện khi nhả phím (`OnPointerUp`), phát âm thanh `UI_Confirm_Primary`.
  * **Disabled**: Opacity $40\%$, không phản hồi chuột hay phím bấm.

### PAT-NAV-02: Secondary / Back Button
* **Mô tả**: Nút phụ hoặc quay lại ("Back", "Cancel"). Trọng số thị giác thấp hơn Primary Button để không gây tranh chấp chú ý.
* **Quy cách tương tác**:
  * **Default**: Nền trong suốt, chỉ có viền mảnh (Outline), chữ xám bạc.
  * **Hover / Focus**: Nền chuyển sang xám mờ $15\%$, viền sáng lên.
  * **Phím tắt mặc định**: Luôn tự động liên kết với phím `Escape` trên bàn phím và nút `B / Circle` trên Gamepad.

### PAT-SET-01: Option Slider
* **Mô tả**: Thanh trượt điều chỉnh giá trị liên tục trong khoảng xác định (Âm lượng $0\% - 100\%$, Tỉ lệ HUD $100\% - 130\%$).
* **Quy cách tương tác**:
  * **Hiển thị**: Thanh rãnh trượt (Track), thanh tô đầy (Fill), con trượt (Thumb), và **luôn hiển thị nhãn số giá trị bên cạnh** (VD: `80%`). Tuyệt đối không chỉ hiển thị vị trí vạch mà thiếu số.
  * **Điều khiển phím / Gamepad**: Bấm Mũi tên Trái / Phải hoặc D-pad bước nhảy $5\%$/lần bấm, phát âm thanh click nhẹ `UI_Slider_Step`.

### PAT-SET-02: Toggle Switch
* **Mô tả**: Công tắc bật/tắt nhị phân hai trạng thái cho các tính năng trợ năng và thiết lập lối chơi.
* **Quy cách tương tác**:
  * **Hiển thị**: Hộp trượt dạng viên thuốc. Bên cạnh luôn hiển thị văn bản rõ ràng: `"ON"` hoặc `"OFF"`, không chỉ dựa vào màu xanh/xám.
  * **Chuyển đổi**: Bấm chuột hoặc phím `Space`/`Enter` làm thumb trượt mượt mà ($150\text{ ms}$). Nếu kích hoạt Motion Reduction Mode, thumb chuyển vị trí ngay lập tức không cần trượt.

### PAT-SET-03: Keybind Remap Slot
* **Mô tả**: Ô chọn gán phím trong menu Cài đặt điều khiển (Settings > Controls).
* **Quy cách tương tác**:
  * **Trạng thái chờ**: Hiển thị phím hiện tại (VD: `[W]`, `[LShift]`, `[Gamepad Left Stick]`).
  * **Trạng thái lắng nghe (Listening)**: Khi bấm vào slot, slot nhấp nháy màu hổ phách `#F2A33C` kèm thông báo `"Press any key..."`.
  * **Xác nhận**: Nhận input tiếp theo từ Unity Input System, cập nhật override binding, kiểm tra trùng lặp (Conflict Warning). Bấm `Escape` để hủy gán.

---

## 4. Gameplay HUD Patterns (UGUI Canvas)

### PAT-HUD-01: Directional Threat Chevron (HUD Azimuth)
* **Mô tả**: Mũi tên chỉ hướng cảnh báo vị trí mối đe dọa từ lính gác (theo GDD `suspicion-meter-grade.md` và `audio-ui-feedback.md`).
* **Quy cách vị trí**:
  * Chiếu góc phương vị lính gác lên đường elip màn hình tâm tại ngực nhân vật:
    $$x = R_x \sin(\theta_{\text{rel}}), \quad y = R_y \cos(\theta_{\text{rel}}), \quad R_x = 240\text{ px}, R_y = 160\text{ px}$$
* **Quy cách thị giác & Dư thừa hình học**:
  * **Góc xoay**: Mũi tên xoay tự do hướng về phía lính gác.
  * **Màu sắc & Trạng thái**:
    * $r_{\text{threat}}^* < 0.5$: Màu Xanh thép mờ `#8FA3B8`, chevron đơn.
    * $0.5 \le r_{\text{threat}}^* < 1.0$: Màu Hổ phách `#F2A33C`, chevron kép, nhấp nháy nhịp $2\text{ Hz}$.
    * $r_{\text{threat}}^* \ge 1.0$ (Phát hiện): Màu Cam cảnh báo `#D98E2B`, nhấp nháy nhịp $4\text{ Hz}$ kèm âm thanh alert stinger.

### PAT-HUD-02: Suspicion Meter & Sinking Notch
* **Mô tả**: Thanh đo tích lũy nghi ngờ $A$ kèm vạch khấc phản xạ $T_{\text{entry}}(R)$ gắn trên đầu lính gác hoặc hiển thị trực quan.
* **Quy cách hiển thị**:
  * **Thanh Fill**: Thanh màu ngang chạy từ $0$ đến $1.0$ biểu thị giá trị $A$ hiện tại.
  * **Sinking Notch**: Một vạch trắng đứng mảnh ($2\text{ px}$) đánh dấu vị trí $T_{\text{entry}}(R)$. Vạch này từ từ chìm xuống ($1.0 \to 0.25$) khi lính gác nhìn liên tục người chơi.
  * **Hiệu ứng Wariness Residual**: Nếu $A$ giảm, để lại vệt màu mờ (Ghost trail) rút lui từ từ trong $0.5\text{ s}$ để người chơi cảm nhận được độ cảnh giác còn sót lại.

### PAT-HUD-03: Player Stance Indicator
* **Mô tả**: Cụm biểu tượng cố định góc dưới màn hình hiển thị tư thế di chuyển và khả năng bị phát hiện của người chơi.
* **Quy cách hiển thị**:
  * **Crouch**: Biểu tượng người ngồi thu mình, màu trung tính.
  * **Walk / Sprint**: Biểu tượng người đứng / chạy kèm vòng bán kính tiếng ồn tương ứng ($1.0\text{ m}$ hoặc $4.0\text{ m}$).
  * **Hidden (Trong tủ HideSpot)**: Biểu tượng con mắt nhắm (Eye Closed) có gạch chéo bảo đảm an toàn, toàn màn hình phủ một lớp Low-Pass Filter âm thanh ($800\text{ Hz}$).

---

## 5. In-World Interaction Patterns (UGUI World Space)

### PAT-WLD-01: Contextual World Reticle (Tương tác vật thể)
* **Mô tả**: Icon nút bấm ngữ cảnh nổi trực tiếp trong không gian 3D trên đầu vật thể khi người chơi tiến vào bán kính tương tác ($R \le 1.5\text{ m}$).
* **Hành vi**:
  * **Xuất hiện**: Khi $R \le 1.5\text{ m}$ và nhân vật nhìn về phía vật thể, reticle fade-in trong $120\text{ ms}$ và nổi lên cao $0.1\text{ m}$.
  * **Biểu tượng phím động**: Hiển thị phím theo thiết bị đang dùng: `[E]` (Keyboard) hoặc `(X)` (Xbox Gamepad) kèm hành động rõ ràng: `"Hide"` (với tủ Locker) hoặc `"Take Burst"` (với nhặt vật phẩm).
  * **Giữ phím (Hold)**: Nếu hành động yêu cầu giữ (Throw Aim), vòng tròn xung quanh reticle sẽ tự động xoay tròn tô đầy theo tiến trình.
  * **Biến mất**: Khi người chơi rời khỏi bán kính $> 1.5\text{ m}$, reticle fade-out trong $80\text{ ms}$.

---

## 6. Animation & Sound Standards

* **Animation Timings**:
  * Hover / Focus Transition: $80\text{ ms}$ (ease-out).
  * Press Scale Down: $60\text{ ms}$ (ease-in).
  * Release Scale Up: $80\text{ ms}$ (ease-out).
  * Modal Fade-in / Fade-out: $150\text{ ms}$ – $200\text{ ms}$.
* **Motion Reduction Override**:
  * Khi kích hoạt Motion Reduction trong Settings, mọi chuyển động scale và slide đều được thay thế bằng Instant Snap hoặc Fade nhẹ ($50\text{ ms}$), camera shake bị vô hiệu hóa hoàn toàn.
* **Audio Cues**:
  * Mỗi lần hover/focus đều kích hoạt âm thanh tick cực nhẹ ($< 40\text{ ms}$) không gây mệt tai.
  * Xác nhận lựa chọn kích hoạt âm thanh confirm rõ ràng, khác biệt hoàn toàn với âm thanh hủy bỏ/quay lại.
