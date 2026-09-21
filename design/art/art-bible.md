# Art Bible: Whisper Ward

## Document Status
- **Version**: 0.4.0 (Visual Identity Foundation)
- **Last Updated**: 2026-09-21
- **Owned By**: art-director
- **Status**: Approved (Sections 1–4 Visual Identity Foundation)
- **Milestone Gate**: Technical Setup → Pre-Production (Sections 1–4 Foundation Satisfied)

---

## 1. Visual Identity Summary

> **Visual Anchor**: *"Every light is a verdict"* (Mỗi nguồn sáng là một phán quyết).

Whisper Ward mang phong cách thị giác **"Cold Watch"** — sự kết hợp giữa chủ nghĩa tối giản công nghiệp (Brutalist Architecture) và thẩm mỹ điện ảnh Neo-Noir tương phản cao (Chiaroscuro). Game đặt người chơi vào một cơ sở nghiên cứu kiên cố trong màn đêm cô lập, nơi ánh trăng xanh lạnh lẽo làm phông nền cho những luồng sáng sắc lẹm từ đèn pin tuần tra của lính gác và đèn pha an ninh.

Trong Whisper Ward, bóng tối không chỉ là bóng râm thẩm mỹ mà là một ranh giới sinh tử:
- **Bóng tối (Deep Shadow)**: Là đồng minh, nơi ẩn náu và là không gian của sự toan tính chiến thuật.
- **Ánh sáng (Direct Light)**: Là phán quyết. Bước vào luồng sáng đồng nghĩa với việc đối mặt với sự phán xét tức thì từ tầm nhìn AI. Mọi nguồn sáng đều có mục đích dẫn hướng lối chơi và cảnh báo hiểm họa.

---

## 2. Reference Board

| Tác phẩm tham chiếu | Phương tiện | Yếu tố nghệ thuật kế thừa | Ứng dụng trong Whisper Ward |
|---|---|---|---|
| **Splinter Cell: Blacklist / Chaos Theory** | Game | Kỹ thuật chiếu sáng Chiaroscuro tương phản gắt; luồng sáng đèn pin cắt ngang bóng tối; dáng di chuyển lén lút. | Xác định ranh giới vùng sáng/tối sắc nét để người chơi dễ dàng đọc vị tầm nhìn của AI. |
| **Dishonored** | Game | Phong cách kết xuất Stylized Realism; bề mặt vật liệu mang chất hội họa nhưng bảo toàn độ nhám PBR; silhouette sắc nét. | Giúp mô hình nhân vật và môi trường có tính nhận diện cao, không bị chìm vào phông nền dù trong bóng tối sâu. |
| **Alien: Isolation** | Game | Bầu không khí cơ sở khép kín nghẹt thở; hạt bụi thể tích (Volumetric Dust) làm lộ rõ hình nón ánh sáng của kẻ địch. | Sử dụng luồng sáng thể tích (Volumetric Light Shafts) để trực quan hóa góc nhìn $60^\circ$ của lính gác một cách tự nhiên. |
| **Neo-Noir Cinema (Blade Runner)** | Điện ảnh | Bảng màu bổ túc: nền xanh đêm lạnh đối lập với đèn hổ phách và cam báo động; Rim lighting tách lớp nhân vật. | Đèn viền mờ (Rim Light) trên trang phục nhân vật chính giúp người chơi luôn kiểm soát được vị trí cơ thể mà không bị lính phát hiện. |

---

## 3. Color Palette

### Primary Palette (Cold Watch)

| Tên màu | Mã Hex | Mục đích sử dụng chính | Tỷ lệ xuất hiện |
|---|---|---|---|
| **Moonlit Steel** | `#8FA3B8` | Ánh trăng xanh mờ chiếu qua cửa sổ trần, ánh sáng môi trường (Ambient) | 40% (Chủ đạo) |
| **Deep Shadow** | `#121820` | Vùng bóng râm sâu, nơi người chơi ẩn nấp an toàn | 35% (Nền tảng) |
| **Torch Amber** | `#F2A33C` | Luồng sáng đèn pin lính gác, ánh sáng nghi ngờ (Investigating) | 12% (Nguy hiểm) |
| **Alarm Orange** | `#D98E2B` | Đèn còi báo động khẩn cấp, trạng thái truy đuổi (Chase) | 5% (Báo động) |
| **Vault Gold** | `#E5B842` | Ánh sáng phát quang của Cổ vật mục tiêu tối thượng | 3% (Mục tiêu) |
| **Sanctuary Cyan**| `#38B2AC` | Đèn báo bảng điều khiển điện tử / tủ trốn an toàn (HideSpot) | 5% (Cứu cánh) |

### Emotional Color Mapping

| Trạng thái Gameplay | Bảng màu chủ đạo | Tâm lý & Cảm xúc của người chơi |
|---|---|---|
| **Ẩn nấp / Thám thính** | Moonlit Steel (`#8FA3B8`) + Deep Shadow (`#121820`) | Căng thẳng, tĩnh lặng, tự chủ và an toàn có toan tính. |
| **Bị nghi ngờ (Investigate)** | Torch Amber (`#F2A33C`) cắt ngang Deep Shadow | Giật mình, áp lực thời gian, tim đập nhanh khi nón sáng quét qua. |
| **Truy đuổi (Active Chase)** | Alarm Orange (`#D98E2B`) nhấp nháy + Strobe light | Báo động đỏ, hoảng sợ, buộc phải phản xạ tìm đường tháo chạy. |
| **Chiếm được Cổ vật (Victory)** | Vault Gold (`#E5B842`) tỏa sáng ấm áp | Thành tựu, giải tỏa áp lực, đắc thắng. |

---

## 4. Art Style & Visual Hierarchy

### Rendering Style
* **Engine & Pipeline**: Unity 6 LTS, Universal Render Pipeline (URP 17) với chế độ kết xuất **Forward+**.
* **Định hướng phong cách**: **Stylized Realism PBR** (Chân thực cách điệu). Bề mặt kim loại xỉn, bê tông thô, kính mờ phản chiếu ánh trăng nhẹ nhàng nhưng lược bỏ các chi tiết nhiễu (noise) vụn vặt để tối ưu độ rõ ràng thị giác.
* **Kỹ thuật ánh sáng**:
  * Đèn Directional Light mô phỏng ánh trăng góc nghiêng $45^\circ$, đổ bóng sắc nét (Sharp Contact Shadows).
  * Đèn Spotlights thời gian thực gắn trên lính gác có góc quét $60^\circ$, kết hợp hiệu ứng bụi thể tích (Volumetric Dust) để người chơi nhìn rõ biên độ hình nón ánh sáng.

### Proportions & Scale
* **Nhân vật**: Tỷ lệ cơ thể người thực tế ($1:7.5 - 1:8$), không cách điệu đầu to hoạt hình. Tư thế cúi thấp (Crouch) gập người sâu giúp giảm tiết diện đón sáng tới $50\%$.
* **Môi trường**: Trần cơ sở nghiên cứu cao $3.5\text{ m} - 5.0\text{ m}$ tạo cảm giác áp chế. Các chướng ngại vật (thùng hàng, vách ngăn) cao đúng $1.1\text{ m}$ (ngang ngực khi đứng, che kín đầu khi ngồi).

### Level of Detail (LOD) & Materiality
* **Vật liệu PBR tối giản**: Ưu tiên bản đồ Normal Maps và Roughness Maps rõ nét để ánh sáng lướt qua làm nổi gờ tường và nẹp kim loại. Tránh các họa tiết hoa văn sặc sỡ làm người chơi nhầm lẫn với bóng đổ.

### Visual Hierarchy & Lighting Rules
Hệ thống cấp bậc thị giác dẫn hướng mắt người chơi theo 4 tầng tương phản:
1. **Tầng 1 (Tối thượng - Hiểm họa tức thì)**: Nón ánh sáng đèn pin lính gác và đèn báo động. Độ sáng $100\%$, tương phản tuyệt đối, chiếm trọn sự chú ý.
2. **Tầng 2 (Mục tiêu & Tương tác)**: Ánh sáng hắt từ tủ trốn Locker, cửa mở, cổ vật mục tiêu. Độ sáng $70\%$, dùng ánh sáng điểm dịu mắt để dẫn đường.
3. **Tầng 3 (Người chơi & Địa hình)**: Silhouette nhân vật chính được viền một lớp ánh sáng Rim Light xanh mờ nhẹ ($15\%$), vừa đủ để người chơi nhận diện cơ thể mình trong bóng tối mà không bị AI phát hiện.
4. **Tầng 4 (Bóng tối an toàn - Background)**: Góc tường, gầm cầu thang, hốc tủ. Độ sáng $\le 10\%$, màu đen sâu thẳm, triệt tiêu chi tiết thừa để người chơi cảm nhận sự an toàn tuyệt đối.

---

## 5. Character Art Standards

[To be designed in Pre-Production]

---

## 6. Environment Art Standards

[To be designed in Pre-Production]

---

## 7. UI & VFX Standards

[To be designed in Pre-Production]

---

## 8. Asset Production Standards

[To be designed in Pre-Production]

---

## 9. Accessibility Alignment

[To be designed in Pre-Production]
