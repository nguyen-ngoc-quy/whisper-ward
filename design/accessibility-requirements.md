# Accessibility Requirements: Whisper Ward

> **Status**: Committed
> **Author**: UX Designer & Producer
> **Last Updated**: 2026-09-21
> **Accessibility Tier Target**: Standard Tier
> **Platform(s)**: PC (Windows - Primary), Web (WebGL Demo - Secondary/Portfolio)
> **External Standards Targeted**:
> - WCAG 2.1 Level AA (Contrast, Text Scaling, Keyboard Operability)
> - AbleGamers CVAA Guidelines (Partial - Non-communications single-player)
> - Game Accessibility Guidelines (Intermediate Level)
> **Linked Documents**: `design/gdd/systems-index.md`, `design/ux/interaction-patterns.md`, `.claude/docs/technical-preferences.md`, `docs/architecture/architecture.md`

---

## Accessibility Tier Definition

### Tier Definitions Summary

| Tier | Core Commitment | Effort in Whisper Ward |
|---|---|---|
| **Basic** | Critical text readable, no color-only signals, independent audio volume sliders, no photosensitivity flash. | Baseline design constraint |
| **Standard** | All of Basic + Full input remapping, subtitle speaker ID, adjustable text/HUD scale, colorblind mode with shape redundancy, toggle alternatives for hold actions, visual sound cues. | **Committed Target** |
| **Comprehensive** | All of Standard + Screen reader menu narration, mono audio fold-down, fine-grained assist sliders, custom HUD repositioning. | Post-MVP Consideration |
| **Exemplary** | All of Comprehensive + Full subtitle customization, tactile/haptic cues, external accessibility audit. | Out of Scope for 8-week MVP |

### This Project's Commitment

**Target Tier**: **Standard Tier**

**Rationale**:  
Whisper Ward là tựa game stealth góc nhìn thứ ba tập trung vào trí tuệ nhân tạo (Behavior Tree, Perception, Coordinated Alert) trong thời lượng phát triển 8 tuần. Độ căng thẳng của gameplay đến từ việc quan sát tầm nhìn lính gác và quản lý tiếng ồn. Nếu chỉ dừng ở mức Basic, người khiếm thính sẽ mất hoàn toàn cơ hội nhận biết bán kính phát tán tiếng ồn, còn người có khiếm khuyết vận động nhẹ sẽ gặp khó khăn khi phải giữ phím ngồi (Hold-to-crouch). 
Cam kết **Standard Tier** giải quyết trực tiếp các rào cản này:
1. **Âm thanh thành hình ảnh**: Biểu thị trực quan vòng sóng âm thanh (concentric ripples) trên mặt đất khi bước đi, chạy sprint ($4.0\text{ m}$) và ném Burst ($10.5\text{ m}$).
2. **Dư thừa hình học (Shape Redundancy)**: Tuyệt đối không dùng màu sắc đơn thuần để biểu thị nghi ngờ. Thanh đo Suspicion kết hợp vạch khấc ngưỡng chìm ($T_{\text{entry}}$) và mũi tên chỉ hướng chevron góc phương vị HUD ($R_x=240, R_y=160$).
3. **Giảm tải thao tác vận động**: Hỗ trợ đầy đủ Toggle cho Crouch và Aim Burst; hỗ trợ đổi phím toàn bộ (Full Key Remapping) trên Unity Input System.

---

## Visual Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---|---|---|---|---|
| Minimum text size — UI | Standard | Menus & Settings | Committed | 24px tối thiểu ở 1080p. Co giãn theo tỷ lệ màn hình (UI Toolkit). |
| Minimum text size — HUD | Standard | In-game HUD | Committed | 20px tối thiểu cho chữ số đo đạc nghi ngờ và telemetry overlay. |
| Minimum text size — Subtitles | Standard | Guard Vocalizations | Committed | 28px tối thiểu ở 1080p, viền bóng đổ hoặc nền hộp mờ 70% đen. |
| Text contrast | Standard | All UI Text | Committed | Đạt tối thiểu 4.5:1 (WCAG AA). Subtitle đạt 7:1 với nền hộp đen mờ. |
| Cold Watch Palette & Colorblind | Standard | Suspicion HUD | Committed | Bảng màu Cold Watch: Xanh thép `#8FA3B8` (Unaware), Hổ phách `#F2A33C` (Suspicious), Cam cảnh báo `#D98E2B` (Alerted). Đã kiểm chứng an toàn cho Protanopia, Deuteranopia, Tritanopia. |
| Shape Redundancy | Standard | Threat Indicators | Committed | Bắt buộc: Chevron định hướng thay đổi kích thước và nhịp đập theo $r_{\text{threat}}^*$; thanh đo nghi ngờ có vạch khấc ngưỡng $T_{\text{entry}}$. |
| UI Scaling | Standard | In-game HUD | Committed | Tùy chọn tỉ lệ HUD từ 100% đến 130% trong Settings. |
| Motion Reduction Mode | Standard | Camera & VFX | Committed | Tắt hoàn toàn Camera Shake, Head Bob, và hiệu ứng rung camera khi bị bắt. Chuyển cảnh fade mượt mà. |
| Photosensitivity Protection | Basic | Rendering / Lighting | Committed | Không có hiệu ứng chớp trắng toàn màn hình vượt quá 3 lần/giây (chuẩn Harding FPA). Đèn báo động xoay hoặc đổi màu từ từ. |

---

## Motor Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---|---|---|---|---|
| Full Input Remapping | Standard | Keyboard/Mouse & Gamepad | Committed | Hỗ trợ gán lại toàn bộ các phím: WASD, Crouch, Sprint, Throw Burst, Interact, Pause. Lưu vào profile người chơi (PlayerPrefs / LocalStorage). |
| Input Device Hot-Swapping | Standard | Gameplay & Menus | Committed | Tự động chuyển đổi icon gợi ý phím (Keyboard vs Gamepad) ngay khi phát hiện input mới từ Unity Input System. |
| Toggle vs Hold: Crouch | Standard | Player Locomotion | Committed | Cung cấp tùy chọn trong Settings: "Crouch Mode: Hold / Toggle" (Mặc định: Toggle). |
| Toggle vs Hold: Throw Aim | Standard | Burst Projectile | Committed | Cung cấp tùy chọn: Giữ chuột phải để ngắm hoặc bấm chuột phải để vào trạng thái ngắm. |
| No Rapid Button Mashing | Standard | Core Loop | Committed | Mọi hành động tương tác (trốn tủ HideSpot, nhặt Burst) đều kích hoạt bằng 1 lần bấm (`Press`), không yêu cầu bấm nhanh liên tục (QTE). |
| Generous Interaction Buffers | Standard | World Interactors | Committed | Vùng phát hiện tủ trốn HideSpot bán kính $1.5\text{ m}$, tương tác không yêu cầu ngắm chuẩn xác tuyệt đối đến từng pixel. |

---

## Cognitive Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---|---|---|---|---|
| Pause Anywhere | Basic | Single-player Gameplay | Committed | Bấm `Esc` đóng băng đồng hồ ảo `VirtualClock` ngay lập tức, không mất mát trạng thái FSM hay sự kiện đang bay. |
| Clear Threat Feedback | Standard | HUD / Awareness | Committed | Mũi tên chỉ hướng định vị chính xác góc phương vị của lính gác ngoài khung nhìn, loại bỏ sự mơ hồ vị trí. |
| In-Game Controls Overlay | Standard | Pause Menu | Committed | Màn hình Pause luôn hiển thị sơ đồ phím điều khiển rút gọn để người chơi dễ tra cứu lại. |
| Reading Time & No Timeout | Standard | Tutorial / Prompts | Committed | Các chỉ dẫn phím ngữ cảnh (`[E] Hide`) hiển thị liên tục khi ở trong vùng, không tự động biến mất theo thời gian ngắn. |

---

## Auditory Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---|---|---|---|---|
| Visual Sound Ripples | Standard | Stealth Locomotion | Committed | Khi phát ra tiếng ồn (Bước chân Sprint $4.0\text{ m}$, Burst rơi $10.5\text{ m}$), xuất hiện vòng sóng tròn đồng tâm mờ dần trên mặt đất. |
| Guard Voice Subtitles | Standard | Guard AI FSM | Committed | Phụ đề kèm định danh trạng thái: `[Guard 1 - Nghi ngờ]: "Có tiếng gì đằng kia?"`, `[Guard 2 - Báo động]: "Phát hiện mục tiêu!"`. |
| Independent Audio Sliders | Basic | Audio Mixer | Committed | 4 thanh trượt độc lập: Master Volume, SFX (tiếng động stealth), Ambience (môi trường), UI Feedback. |
| Directional Audio Indication | Standard | Spatial Hearing | Committed | Mũi tên trên HUD nhấp nháy khi lính gác phát ra âm thanh bước chân hoặc lời nói ở ngoài tầm nhìn màn hình. |

---

## Per-Feature Accessibility Matrix

| GDD / System | Visual | Motor | Cognitive | Auditory | Status |
|---|---|---|---|---|---|
| **Guard AI FSM** | Đèn trạng thái & tư thế | Không rào cản | Trạng thái rõ ràng (Unaware/Suspicious/Alert) | Phụ đề hội thoại cảnh báo | Đạt Standard |
| **Perception Pipeline** | Hình nón tầm nhìn mờ/rõ | Không rào cản | Vạch ngưỡng $T_{\text{entry}}$ cố định | Báo động khi lính phát hiện | Đạt Standard |
| **Player Noise & Burst** | Vòng sóng đồng tâm | Aim Toggle/Hold | Bán kính âm thanh trực quan | Hiệu ứng âm trầm đập đất | Đạt Standard |
| **Player Movement & Hide** | HUD hiển thị tư thế (Icon) | Crouch Toggle/Hold | Camera chuyển mượt khi ẩn nấp | LPF cắt âm khi trốn ($800\text{ Hz}$) | Đạt Standard |
| **Suspicion Meter & Grade** | Bảng màu Cold Watch | Không rào cản | Thanh đo chuẩn hóa $[0, 1]$ | Tiếng click tăng nhịp đập | Đạt Standard |
| **Input System** | Icon phím động | Full Remap phím | Bảng tra cứu phím Pause | Không rào cản | Đạt Standard |

---

## Known Intentional Limitations

| Feature | Tier Required | Lý do chưa hỗ trợ | Biện pháp giảm thiểu |
|---|---|---|---|
| **Screen Reader trong Game World** | Exemplary | Vượt quá phạm vi dự án indie 8 tuần chạy WebGL/PC. | Toàn bộ thông tin được trực quan hóa trên HUD và Text tương phản cao. |
| **Tùy chỉnh phông chữ Subtitle** | Comprehensive | Giới hạn font asset pipeline hiện tại. | Cung cấp kích thước chữ lớn mặc định kèm nền hộp đen bảo đảm độ tương phản 7:1. |
| **Haptic Feedback phức tạp** | Exemplary | WebGL không hỗ trợ rung gamepad đồng nhất trên mọi trình duyệt. | Bù đắp bằng rung màn hình nhẹ (có thể tắt) và hiệu ứng UI trực quan. |
