# Whisper Ward — Kế Hoạch Phát Triển 8 Tuần
### Portfolio Project: Stealth/Adventure 3D với hệ thống AI tự viết (Unity)

---

## 1. Tổng Quan Dự Án

**Tên project:** Whisper Ward
**Thể loại:** Stealth / Adventure 3D, góc nhìn thứ ba
**Thời gian:** 8 tuần
**Engine:** Unity (URP)
**Mục tiêu portfolio:** Thể hiện khả năng thiết kế và tự viết hệ thống AI (Behavior Tree, Perception, Coordinated Alert) — điểm khác biệt so với các portfolio game dev thông thường, tận dụng background học AI của bạn.

### Concept cốt lõi
Người chơi xâm nhập một khu vực được canh gác (nhà kho / làng nhỏ / cơ sở bị bỏ hoang), phải lẻn qua hệ thống NPC lính canh có AI thông minh để lấy 1 vật phẩm mục tiêu và thoát ra ngoài. NPC không chỉ tuần tra theo đường cố định mà còn:
- Nhìn và nghe theo vùng thực tế (raycast-based vision cone, sound event system)
- Điều tra khi nghi ngờ (đi tới vị trí phát ra tiếng động / nơi nhìn thấy player lần cuối)
- Truy đuổi có dự đoán vị trí thay vì bám tọa độ hiện tại
- Báo động và phối hợp với NPC khác gần đó khi phát hiện player

### Vòng lặp gameplay (Core Loop)
```
Quan sát môi trường → Lên kế hoạch di chuyển → Lẻn / Ẩn nấp → 
(Nếu bị phát hiện) Chạy trốn / Đánh lạc hướng → Lấy vật phẩm → Thoát ra
```

### Phạm vi (Scope) — Giới hạn cứng để tránh trễ deadline
- 1 level chơi được hoàn chỉnh (không làm nhiều màn)
- Tối đa 3 trạng thái AI chính: Patrol, Investigate, Chase (không thêm state 4-5 dù có ý tưởng hay)
- 2-3 NPC lính canh trong level, không cần nhiều loại kẻ địch khác nhau
- Asset môi trường dùng free asset pack có sẵn (Synty, Kenney, Unity Asset Store free section) — không tự model 3D
- Tính năng LLM dialogue (nếu có) chỉ là optional bonus ở tuần 6, có thể cắt bỏ không ảnh hưởng core

---

## 2. Timeline Chi Tiết Theo Tuần

---

### 🗓️ Tuần 1 — Pre-production & Core Movement

**Mục tiêu tuần:** Nhân vật di chuyển mượt trong scene blockout, project setup sạch sẽ.

**Công việc:**
1. **Setup project**
   - Tạo Unity project mới với URP template
   - Khởi tạo Git repository, viết `.gitignore` chuẩn cho Unity
   - Cấu trúc folder: `Scripts/`, `Prefabs/`, `Scenes/`, `Art/`, `Audio/`, `ScriptableObjects/`
   - Setup Input System (Unity Input System package, không dùng Input Manager cũ)

2. **Game Design Document (GDD) ngắn gọn**
   - 1-2 trang: mục tiêu game, cơ chế chính, đối tượng người chơi, phạm vi
   - Sơ đồ luồng gameplay (core loop) đơn giản

3. **Player Controller**
   - Di chuyển cơ bản (WASD + chuột xoay camera third-person)
   - 3 trạng thái di chuyển: Đi bộ (walk), Chạy (run), Rón rén (crouch/sneak) — mỗi trạng thái có tốc độ và "mức độ ồn" khác nhau
   - Camera third-person đơn giản (Cinemachine Free Look hoặc tự viết script theo player)

4. **Blockout level**
   - Dùng ProBuilder dựng khung level thô: tường, vật cản, vài route di chuyển
   - Chưa cần đẹp, chỉ cần đúng layout để test gameplay sau này

**Deliverable cuối tuần:** Video ngắn quay nhân vật di chuyển mượt trong scene blockout với 3 trạng thái di chuyển.

---

### 🗓️ Tuần 2 — Hệ Thống Perception (Giác quan AI)

**Mục tiêu tuần:** NPC "biết" được khi nào phát hiện player, có thể verify qua debug log/gizmo.

**Công việc:**
1. **Module `AIPerception`**
   - **Vision system:** Raycast kiểm tra trong vùng góc nhìn (field of view angle) + khoảng cách (detection range) + line-of-sight (raycast check vật cản)
   - **Hearing system:** Event-based, lắng nghe các sự kiện âm thanh (tiếng bước chân, va chạm vật thể) trong bán kính nghe được
   - Tính điểm "mức độ nghi ngờ" (suspicion meter) tăng dần theo thời gian nhìn thấy thay vì phát hiện tức thì (cảm giác tự nhiên hơn)

2. **Player Noise System**
   - `NoiseEmitter` component gắn trên player, phát ra "noise event" với cường độ khác nhau tùy trạng thái (đi/chạy/rón rén)
   - NPC nghe được nếu noise event nằm trong bán kính nghe + không bị vật cản chặn hoàn toàn

3. **Debug Visualization**
   - Vẽ Gizmos cho vùng nhìn (hình quạt), bán kính nghe (hình tròn)
   - UI debug tạm thời hiển thị trạng thái nghi ngờ của NPC gần nhất

**Deliverable cuối tuần:** NPC đứng yên nhưng phát hiện được player khi đi vào vùng nhìn/nghe, log console rõ ràng.

---

### 🗓️ Tuần 3 — Behavior Tree Framework (Tự Viết)

**Mục tiêu tuần:** 1 NPC tuần tra theo waypoint, chuyển trạng thái Idle ⇄ Patrol qua Behavior Tree tự viết.

**Công việc:**
1. **Kiến trúc Behavior Tree cơ bản**
   - Base class `BTNode` với return type `Success / Failure / Running`
   - **Composite nodes:** `Selector` (OR logic — thử node con đến khi 1 cái thành công), `Sequence` (AND logic — chạy tuần tự, dừng nếu 1 cái fail)
   - **Leaf nodes (actions):** class kế thừa `BTNode`, override hàm `Evaluate()`
   - **Blackboard:** dictionary lưu trạng thái chia sẻ giữa các node (vị trí player nghi ngờ, thời gian phát hiện gần nhất, v.v.)

   > Lý do tự viết thay vì dùng asset có sẵn: chứng minh hiểu bản chất kiến trúc BT, đây là điểm nhấn kỹ thuật quan trọng nhất của portfolio.

2. **Các node cụ thể tuần này**
   - `PatrolAction`: di chuyển qua danh sách waypoint theo thứ tự/ngẫu nhiên
   - `IdleAction`: đứng yên, xoay đầu quan sát theo chu kỳ
   - `CheckPerceptionCondition`: node điều kiện kiểm tra blackboard xem có phát hiện player không

3. **Tích hợp NavMesh**
   - Bake NavMesh cho level blockout
   - `NavMeshAgent` cho NPC, kết nối với action node `PatrolAction` để di chuyển tới waypoint

**Deliverable cuối tuần:** 1 NPC chạy được Behavior Tree, tuần tra qua các điểm waypoint, đứng lại Idle ngẫu nhiên, có thể xem sơ đồ cây trong Inspector (dạng đơn giản, chưa cần visual editor).

---

### 🗓️ Tuần 4 — AI Logic Nâng Cao

**Mục tiêu tuần:** 2-3 NPC phối hợp phát hiện & truy lùng player một cách "thông minh".

**Công việc:**
1. **State Investigate**
   - Khi suspicion meter đạt ngưỡng nhưng chưa đủ để Chase: NPC di chuyển tới vị trí nghi ngờ (last heard/seen position)
   - Tới nơi, thực hiện hành vi "nhìn quanh" (xoay đầu/thân theo góc ngẫu nhiên trong X giây)
   - Nếu không tìm thấy gì → quay lại Patrol; nếu phát hiện thêm → chuyển Chase

2. **State Chase với dự đoán vị trí**
   - Thay vì đuổi theo tọa độ hiện tại của player (dễ đoán, kém tự nhiên), NPC tính toán **last known position + hướng di chuyển** để dự đoán điểm chặn
   - Có "giving-up timer": nếu mất dấu player quá X giây → chuyển về Investigate rồi Patrol

3. **Hệ thống Alert lan truyền**
   - Khi 1 NPC chuyển sang Chase, phát ra "alert event" trong bán kính nhất định
   - NPC khác trong bán kính nhận event → chuyển thẳng sang Investigate tại vị trí báo động (giả lập hành vi phối hợp nhóm mà không cần AI phức tạp)

**Deliverable cuối tuần:** Video demo 2-3 NPC phối hợp: 1 NPC phát hiện player, NPC còn lại chạy tới hỗ trợ hợp lý.

---

### 🗓️ Tuần 5 — Level Design & Gameplay Loop

**Mục tiêu tuần:** Chơi được từ đầu đến cuối, có điều kiện thắng/thua rõ ràng.

**Công việc:**
1. **Hoàn thiện level layout thật**
   - Thiết kế lại level (không còn blockout thô) với cover points, đường đi tắt, khu vực an toàn/nguy hiểm rõ ràng
   - Đặt vị trí vật phẩm mục tiêu và điểm thoát (extraction point)
   - Thiết kế patrol route cho từng NPC sao cho có "khe hở" hợp lý để người chơi lên chiến thuật

2. **Cơ chế ẩn nấp**
   - `HideSpot` trigger zone (tủ, bụi cây, gầm bàn...): khi player đứng trong → giảm mạnh detection range của NPC hoặc miễn nhiễm hoàn toàn với vision check
   - Feedback UI/visual khi player đang trong trạng thái ẩn nấp

3. **Win/Lose Condition & Flow**
   - Win: lấy vật phẩm + tới điểm thoát
   - Lose: bị NPC bắt được (chạm/ở trong Chase quá lâu ở khoảng cách gần)
   - Restart flow: quay lại điểm bắt đầu hoặc load lại scene, không cần hệ thống save phức tạp

**Deliverable cuối tuần:** Playable build hoàn chỉnh từ start đến win/lose, có thể gửi bạn bè test thử.

---

### 🗓️ Tuần 6 — Polish AI + (Optional) LLM Dialogue

**Mục tiêu tuần:** AI cảm giác "thông minh" tự nhiên, cân bằng độ khó; cân nhắc thêm điểm nhấn LLM nếu còn dư thời gian.

**Công việc:**
1. **Tinh chỉnh AI**
   - Cân bằng field of view (góc + khoảng cách), tốc độ tăng suspicion meter
   - Điều chỉnh giving-up timer, tốc độ patrol/chase cho cảm giác fair nhưng có thử thách
   - Test nhiều lần, ghi chú lại các trường hợp AI hành xử vô lý (bug behavior) để fix

2. **[OPTIONAL — chỉ làm nếu core AI đã ổn định] Tích hợp LLM Dialogue**
   - 1 NPC "quản lý/thường dân" đứng yên trong level, người chơi có thể tương tác hỏi đáp ngắn
   - Gọi API LLM (Claude API hoặc tương tự) qua HTTP request trong Unity, trả về câu trả lời ngắn dựa trên context được thiết kế sẵn (gợi ý đường đi, lore nhỏ)
   - Đây là điểm nhấn thể hiện khả năng kết hợp AI ứng dụng thực tế vào game — nhưng **ưu tiên thấp hơn core AI**, sẵn sàng cắt bỏ nếu trễ tiến độ

3. **Sound Design cơ bản**
   - Footstep sound theo trạng thái di chuyển (đi/chạy/rón rén khác âm lượng)
   - Alert sound khi NPC chuyển sang Investigate/Chase
   - Ambient background phù hợp không khí

**Deliverable cuối tuần:** AI chơi thử "cảm giác thật", không máy móc; ghi chú rõ có/không có tính năng LLM.

---

### 🗓️ Tuần 7 — Visual Polish & UX

**Mục tiêu tuần:** Game trông chuyên nghiệp, không còn cảm giác prototype.

**Công việc:**
1. **Lighting & Post-processing**
   - Setup lighting phù hợp không khí stealth (tương phản sáng/tối rõ ràng để hỗ trợ gameplay ẩn nấp)
   - URP Volume: color grading, vignette, bloom nhẹ
   - Bake lightmap nếu cần tối ưu performance

2. **Vật liệu & môi trường**
   - Áp material pass cho asset môi trường (đã dùng free asset pack)
   - Thêm chi tiết trang trí nhỏ để level không trống trải

3. **UI/UX**
   - HUD: detection meter (thanh hiển thị mức độ bị phát hiện), icon trạng thái ẩn nấp
   - Main menu đơn giản, màn hình pause, màn hình win/lose
   - Transition mượt giữa các màn hình

4. **Animation Polish**
   - Blend tree cho di chuyển (walk/run/crouch blend mượt theo tốc độ)
   - Animation cảnh báo của NPC (dấu chấm than, animation nhìn quanh khi Investigate)

**Deliverable cuối tuần:** Build có UI hoàn chỉnh, hình ảnh trình bày được cho screenshot/trailer.

---

### 🗓️ Tuần 8 — Bug Fix, Build & Portfolio Packaging

**Mục tiêu tuần:** Sản phẩm hoàn chỉnh sẵn sàng đưa vào portfolio.

**Công việc:**
1. **Playtesting & Bug Fixing**
   - Test kỹ toàn bộ flow, ưu tiên fix bug ảnh hưởng gameplay core (AI kẹt, player rơi ra ngoài map, v.v.)
   - Cân bằng lại độ khó lần cuối dựa trên feedback người test

2. **Build**
   - Build WebGL (dễ chia sẻ, nhúng vào portfolio website) hoặc Windows executable
   - Test build trên máy khác để đảm bảo không lỗi thiếu dependency

3. **Video Demo**
   - Quay video 1-2 phút: gameplay footage + voice-over ngắn giải thích hệ thống AI (perception, behavior tree, coordinated alert)
   - Ưu tiên quay cảnh thể hiện rõ AI "thông minh" (NPC phối hợp, dự đoán vị trí)

4. **Technical Writeup**
   - Viết tài liệu kỹ thuật mô tả:
     - Kiến trúc Behavior Tree tự viết (sơ đồ class, cách Selector/Sequence hoạt động)
     - Sơ đồ hệ thống Perception (vision cone + hearing)
     - Quyết định thiết kế quan trọng (vì sao chọn dự đoán vị trí thay vì đuổi trực tiếp, vì sao giới hạn 3 state...)
   - Đây là phần **quan trọng nhất** đối với portfolio định hướng AI — nhà tuyển dụng đọc phần này để hiểu tư duy hệ thống của bạn, không chỉ nhìn gameplay

5. **Đóng gói lên GitHub/Portfolio**
   - README rõ ràng: mô tả game, công nghệ dùng, link video demo, link build chơi thử
   - Source code sạch, comment hợp lý ở các module AI chính
   - Screenshot chất lượng cao

**Deliverable cuối tuần:** Repository GitHub hoàn chỉnh + video demo + writeup kỹ thuật + build chơi được, sẵn sàng đưa vào CV/portfolio.

---

## 3. Rủi Ro & Cách Giảm Thiểu

| Rủi ro | Mức độ | Cách xử lý |
|---|---|---|
| Scope creep ở AI (muốn làm AI "hoàn hảo") | Cao | Giới hạn cứng 3 state (Patrol/Investigate/Chase), không thêm state mới dù có ý tưởng hay |
| Tự làm asset 3D tốn thời gian | Trung bình | Dùng free asset pack (Synty, Kenney) ngay từ đầu, dồn thời gian cho code |
| Tính năng LLM dialogue làm trễ tiến độ | Trung bình | Đánh dấu rõ là optional ở tuần 6, sẵn sàng cắt bỏ nếu core AI chưa ổn định |
| Level design quá lớn/phức tạp | Trung bình | Chỉ làm 1 level duy nhất, ưu tiên chiều sâu thiết kế hơn số lượng |
| Bug AI phát sinh muộn (tuần 7-8) | Cao | Playtest liên tục từ tuần 4 trở đi, không để dồn hết vào tuần 8 |

---

## 4. Kỹ Năng Được Thể Hiện Trong Portfolio

- **AI/Game AI:** Tự thiết kế & implement Behavior Tree framework từ đầu, hệ thống Perception (vision + hearing), thuật toán dự đoán vị trí, hành vi phối hợp nhóm (alert propagation)
- **Gameplay Programming:** Character controller, state-based movement, trigger-based mechanics (ẩn nấp)
- **System Design:** Kiến trúc modular (Blackboard pattern, component-based AI), khả năng giới hạn scope hợp lý
- **(Optional) AI Ứng dụng:** Tích hợp LLM API vào gameplay thực tế
- **Kỹ năng mềm:** Quản lý thời gian dự án 8 tuần, viết tài liệu kỹ thuật rõ ràng

---

## 5. Checklist Nhanh Theo Tuần

- [ ] Tuần 1: Project setup + Player movement + Blockout level
- [ ] Tuần 2: Perception system (vision + hearing) hoạt động
- [ ] Tuần 3: Behavior Tree framework + Patrol/Idle
- [ ] Tuần 4: Investigate + Chase (dự đoán vị trí) + Alert lan truyền
- [ ] Tuần 5: Level design hoàn chỉnh + Hide mechanic + Win/Lose
- [ ] Tuần 6: Cân bằng AI + (optional) LLM dialogue + Sound
- [ ] Tuần 7: Lighting/Post-processing + UI/UX + Animation polish
- [ ] Tuần 8: Bug fix + Build + Video demo + Writeup + Đăng portfolio
