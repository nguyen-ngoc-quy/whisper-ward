# Physics & Collision Config

> **Status**: Approved (2026-09-21)
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 1 (Physical Believability), Pillar 2 (Absolute Spatial Fairness)

## Overview

Hệ thống **Physics & Collision Config** (System `#18`) là lớp hạ tầng kỹ thuật nền tảng (Foundation / Core Infrastructure) chịu trách nhiệm thiết lập và thực thi toàn bộ quy tắc phân tách layer va chạm, cấu hình truy vấn không gian (spatial query contracts) và bộ mô phỏng quỹ đạo ném tất định (deterministic kinematic trajectory simulation) cho *Whisper Ward*. Hệ thống đóng vai trò cơ quan thẩm quyền duy nhất (single source of authority) bảo đảm tính công bằng không gian tuyệt đối (Pillar 2: Absolute Spatial Fairness) bằng cách ngăn chặn triệt để các lỗi kinh điển của thể loại stealth như: lính nhìn/nghe xuyên tường do hở collider, tia cảm biến bị chặn nhầm bởi trigger thể tích, hoặc vật thể ném rơi sai lệch giữa quỹ đạo ngắm thử (preview) và thời gian thực (runtime).

Hệ thống quản lý nghiêm ngặt danh mục layer E20 (`E20_layer_manifest` bắt buộc chứa `World`, cấm tuyệt đối các bit `Player`, `Guard`, `trigger`), áp dụng chính sách `QueryTriggerInteraction.Ignore` cho toàn bộ các truy vấn cảm biến tầm nhìn, âm thanh, nhặt đồ và va chạm Burst. Hệ thống đồng thời cung cấp profile cách ly `HideSpotContainmentProfile` (`QueryTriggerInteraction.Collide`) phục vụ độc quyền cho việc kiểm tra capsule người chơi nằm trọn trong điểm nấp mà không làm suy yếu các rào chắn cảm biến. Hệ thống khóa cứng thiết lập `Physics.queriesHitBackfaces = false` cùng quy chuẩn cuốn lưới outward-facing nhằm loại trừ hiện tượng rò rỉ âm thanh qua vách mỏng, và điều phối chu trình mô phỏng quỹ đạo Burst kinematic ở tần số cố định $120\text{ Hz}$ ($\Delta t = 1/120\text{ s}$). Chi tiết kiến trúc triển khai tuân thủ ràng buộc của `docs/architecture/adr-0002-physics-collision-contract.md`.

## Player Fantasy

Hệ thống Physics & Collision Config không phải là cơ chế người chơi bấm nút để thi triển, mà là **nền tảng của sự tin cậy không gian tuyệt đối (Unshakable Spatial Trust)**. Trong thể loại lén lút (stealth), cảm xúc hồi hộp nghẹt thở chỉ tồn tại khi người chơi có niềm tin tuyệt đối vào bức tường bê tông trước mặt. Nếu một tia nhìn lén lút lọt qua khe hở vô hình giữa hai khối mesh, hay một tiếng bước chân bị lính nghe thấy qua vách ngăn do lỗi lật mặt phẳng (backface normal), ảo ảnh nhập vai sẽ lập tức sụp đổ thành sự ức chế kỹ thuật.

Hệ thống phục vụ ba trụ cột cốt lõi của *Whisper Ward*:

1. **Hiện thực vật lý hữu hình (Pillar 1: Physical Believability)**:
   - Khi người chơi ném chai phát âm thanh gây xao nhãng (Burst projectile), viên đạn bay theo một cung parabol hoàn toàn chân thực: bay vọt qua bậu cửa, lướt sát trần nhà và va đập giòn giã xuống mặt sàn gạch đúng điểm tiếp xúc dự kiến. Không có hiện tượng vật thể xuyên thấu qua mặt bàn, không có độ trễ do giật frame hay trôi lệch vị trí.

2. **Công bằng không gian tuyệt đối (Pillar 2: Absolute Spatial Fairness)**:
   - *"Nếu bạn không bị nhìn thấy, bạn an toàn."* Khi người chơi nép mình sau cột trụ hoặc ngồi thụp sau bức tường E20, họ biết chắc chắn $100\%$ rằng thế giới vật lý đang che chắn cho họ. Tia nhìn và tia nghe của lính tuần tra chạm vào bề mặt `World` sẽ bị triệt tiêu hoàn toàn. Không bao giờ xảy ra tình trạng "lính nhìn thấu tâm can", không có chuyện một trigger vô hình (như trigger mở cửa hay trigger nhặt đồ) vô tình biến thành "bức tường chắn âm thanh" làm hỏng tính toán của người chơi.

3. **Làm chủ tình thế thông qua môi trường (Pillar 3: You Create the Situation)**:
   - Người chơi tự tin dàn dựng các bẫy xao nhãng phức tạp vì họ biết cơ chế va chạm hoạt động với tính tất định toán học (mathematical determinism). Quỹ đạo hiển thị trước (trajectory preview) và quỹ đạo bay thực tế (runtime simulation) rơi trúng cùng một tọa độ tiếp xúc đến từng milimét ($\epsilon_{\text{contact}} = 0.001\text{ m}$), giúp mọi chiến thuật điều hướng kẻ địch diễn ra đúng như dự tính.

## Detailed Design

### Core Rules

#### C1.1 Phân tầng Layer và Ma trận Va chạm (Layer Manifest & Physics Collision Matrix)
1. **Danh mục Layer chuẩn hóa (Project Layer Allocation)**:
   - `Default` (Layer 0): Các thực thể phụ trợ, ánh sáng, máy quay kỹ thuật.
   - `World` (Layer 6): Toàn bộ hình học tĩnh kiên cố của môi trường (tường, sàn, trần, cột trụ, cầu thang, chướng ngại vật kiến trúc).
   - `Player` (Layer 8): Capsule thực thể người chơi.
   - `Guard` (Layer 9): Capsule thực thể lính gác.
   - `HideSpot` (Layer 10): Thể tích trigger của các điểm ẩn nấp (tủ, gầm bàn, lỗ thông hơi).
   - `Trigger` (Layer 11): Toàn bộ các trigger tương tác, cửa chuyển cảnh, vùng nhặt đồ.

2. **Ma trận Va chạm Động lực học PhysX (Physics Collision Matrix)**:
   - `World` $\leftrightarrow$ `Player`: **BẬT** (Player trượt trên mặt sàn và bị chặn bởi tường vững chắc).
   - `World` $\leftrightarrow$ `Guard`: **BẬT** (Guard di chuyển bám bề mặt sàn NavMesh và tránh tường).
   - `Player` $\leftrightarrow$ `Guard`: **TẮT** trong ma trận PhysX. Quyền phán xét bắt giữ (Catch-Gate) thuộc về máy trạng thái `Guard AI FSM` (F10 / Formula D2 của NavMesh), loại bỏ triệt để hiện tượng capsule đẩy giật (kinematic jitter) hay kẹt vật lý khi va chạm gần.
   - `Guard` $\leftrightarrow$ `Guard`: **TẮT** trong ma trận PhysX. Việc tránh nhau giữa các lính hoàn toàn do thuật toán RVO (Reciprocal Velocity Obstacles) của NavMeshAgent đảm nhiệm.
   - `HideSpot` & `Trigger`: **TẮT toàn bộ va chạm động lực học** với tất cả các layer khác trong ma trận PhysX. Các layer này chỉ được truy vấn có chủ đích thông qua các hàm query chuyên biệt (`PhysicsScene.OverlapCapsule`).

3. **Quy tắc Khởi tạo và Khóa Layer (`E20_layer_manifest`)**:
   - Khi nạp cảnh (Scene Init), dịch vụ `PhysicsQueryService` phân giải từng tên layer thông qua `LayerMask.NameToLayer(name)`.
   - Ràng buộc bất biến: Phải có ít nhất layer `World` ($\ge 0$). Nếu bất kỳ layer bắt buộc nào trả về `-1`, hệ thống lập tức từ chối nạp cảnh với mã lỗi `PHY_LAYER_MISSING`.
   - Lắp ráp `E20Mask`: Hệ thống hợp nhất các layer vững chắc và kiểm tra assertion: `E20Mask != 0` đồng thời **tuyệt đối không chứa bất kỳ bit nào** của `Player`, `Guard`, hoặc `Trigger`.
   - `E20Mask` được lưu vào bộ nhớ đệm (cached) bất biến cho toàn bộ phiên chơi; nghiêm cấm phân giải lại layer per-frame hoặc per-query.

#### C1.2 Hợp đồng Truy vấn Không gian Tập trung (Centralized Spatial Query Contracts)
Toàn bộ các truy vấn raycast, linecast, spherecast trong trò chơi bắt buộc phải tuân thủ bảng hợp đồng tham số tập trung sau, sử dụng cờ `QueryTriggerInteraction` tương ứng:

| STT | Loại Truy vấn | Điểm Gốc (Origin) | Điểm Đích (Target) / Hướng | Mask Sử Dụng | Trigger Policy | Hệ Thống Sở Hữu |
|---|---|---|---|---|---|---|
| **Q1** | **Hearing Occlusion (Movement)** | $\vec{p}_{\text{player\_feet}} + 0.25\text{ m} \cdot \vec{u}_y$ (`f12_noise_origin_offset`) | $\vec{p}_{\text{guard\_feet}} + 1.60\text{ m} \cdot \vec{u}_y$ (`guard_eye_height`) | `E20Mask` | `QueryTriggerInteraction.Ignore` | Perception (`#2`) / Player Noise (`#3`) |
| **Q2** | **Hearing Occlusion (Burst)** | $\vec{p}_{\text{landing\_contact}}$ (vị trí tiếp xúc va chạm thực tế) | $\vec{p}_{\text{guard\_feet}} + 1.60\text{ m} \cdot \vec{u}_y$ (`guard_eye_height`) | `E20Mask` | `QueryTriggerInteraction.Ignore` | Perception (`#2`) / Player Noise (`#3`) |
| **Q3** | **Vision Occlusion Linecast** | $\vec{p}_{\text{guard\_feet}} + 1.60\text{ m} \cdot \vec{u}_y$ (`guard_eye_height`) | $\vec{p}_{\text{player\_eye}}$ (Đứng $1.60\text{ m}$, Ngồi $0.80\text{ m}$) | `E20Mask` | `QueryTriggerInteraction.Ignore` | Perception (`#2`) |
| **Q4** | **Pickup Reach Linecast** | $\vec{p}_{\text{player\_feet}} + 1.20\text{ m} \cdot \vec{u}_y$ (`pickup_reach_origin_offset`) | $\vec{p}_{\text{pickup\_anchor}}$ | `E20Mask` | `QueryTriggerInteraction.Ignore` | Player Noise (`#3`) |
| **Q5** | **HideSpot Backstop Linecast** | $\vec{p}_{\text{guard\_feet}} + 1.60\text{ m} \cdot \vec{u}_y$ (`guard_eye_height`) | $\vec{p}_{\text{aperture\_portal\_target}}$ ($h_{\text{portal}} + 0.05\text{ m}$ dọc `hold_vector`) | `E20Mask` | `QueryTriggerInteraction.Ignore` | Guard AI FSM (`#1`) / Player Hide (`#5`) |
| **Q6** | **HideSpot Containment Check** | Capsule người chơi (tâm chân, đỉnh đầu, bán kính $0.35\text{ m}$) | Thể tích trigger của HideSpot mục tiêu | `HideSpotContainmentProfile` (`HideSpot` layer duy nhất) | `QueryTriggerInteraction.Collide` | Player Movement & Hide (`#5`) |
| **Q7** | **Thin-Wall NavMesh Verification** | Tọa độ điểm nguồn cần lấy mẫu | Tọa độ điểm tiếp xúc NavMesh (`hit.position`) | `E20Mask` | `QueryTriggerInteraction.Ignore` | NavMesh / Pathfinding (`#12`) |

*Nguyên tắc cốt lõi: Tuyệt đối không một truy vấn cảm biến (Q1–Q5, Q7) nào được phép dùng `QueryTriggerInteraction.Collide`. Ngoại lệ duy nhất là Q6 để phát hiện người chơi nằm gọn trong hộp nấp.*

#### C1.3 Mô phỏng Quỹ đạo Ném Tất định (Deterministic Kinematic Trajectory Simulation)
1. **Cơ chế Mô phỏng Kinematic**:
   - Đạn ném Burst không gắn `Rigidbody` động lực học của PhysX mà được tích phân từng bước thời gian rời rạc ở tần số cố định $120\text{ Hz}$ ($\Delta t_{\text{tick}} = 1/120\text{ s} \approx 0.0083333333\text{ s}$, `dt_max`).
   - Ở mỗi tick tích phân, vị trí mới được tính bằng phương trình đường đạn:
     $$\Delta \vec{p} = \vec{v}(t) \Delta t + \frac{1}{2} \vec{g} \Delta t^2, \quad \vec{v}(t + \Delta t) = \vec{v}(t) + \vec{g} \Delta t$$
   - Từng phân đoạn dịch chuyển được quét bằng `PhysicsScene.SphereCast` với bán kính cố định $r_{\text{proj}} = 0.05\text{ m}$ (`projectile_radius`) trên mặt nạ `E20Mask` (`QueryTriggerInteraction.Ignore`).

2. **Kiểm tra Chèn lấp Điểm phóng (Initial Launch Overlap Probe)**:
   - Trước khi tiêu tốn tài nguyên nhặt đồ và khởi động chuyến bay, hệ thống bắt buộc thực hiện một truy vấn quét ban đầu:
     ```csharp
     int hitCount = physicsScene.OverlapSphere(launchPosition, projectile_radius, overlapBuffer, E20Mask, QueryTriggerInteraction.Ignore);
     ```
   - Kích thước bộ đệm `overlapBuffer` được cấp phát bất biến một lần duy nhất với dung lượng $64$ phần tử (`initial_overlap_result_capacity`).
   - Nếu `hitCount > 0` và `< 64`: Hệ thống ghi nhận `INITIAL_OVERLAP_REJECTED`, hủy lệnh ném nhưng bảo toàn vật phẩm đang cầm, không phát sinh chuyến bay hay âm thanh.
   - Nếu `hitCount >= 64`: Bộ đệm bị bão hòa, hệ thống kích hoạt cơ chế an toàn đóng (fail-closed) với mã `INITIAL_OVERLAP_QUERY_INCOMPLETE` và hủy bỏ lệnh ném trước khi trừ đồ.

3. **Giải tỏa Va chạm và Đẩy lùi Bề mặt (Contact Push-Out Margin)**:
   - Khi `SphereCast` va chạm bề mặt vững chắc, điểm tiếp xúc bề mặt được ghi nhận tại `hit.point`.
   - Tọa độ tâm hình cầu tiếp xúc được công bố theo công thức chuẩn:
     $$\vec{p}_{\text{contact}} = \text{hit.point} + \text{hit.normal} \times (r_{\text{proj}} + \epsilon_{\text{contact}})$$
     với $\epsilon_{\text{contact}} = 0.001\text{ m}$ ($1\text{ mm}$), ngăn chặn hoàn toàn việc tâm hình cầu bị kẹt dính bên trong bề mặt collider ở bước tích phân kế tiếp.

4. **Khử Nghiệm Rời rạc Dưới Tick (Sub-Tick Analytical Root Cutoff)**:
   - Khi giải nghiệm giải tích đường đạn va chạm mặt sàn phẳng, các nghiệm $t \le \epsilon_t$ với $\epsilon_t = 1/120\text{ s}$ sẽ bị loại bỏ để tránh lỗi làm tròn số thực IEEE 754 khiến vật thể nổ tung tại tay người ném ở thời điểm phóng ($t \approx 10^{-16}\text{ s}$).

5. **Tính Nhất quán giữa Preview và Runtime**:
   - Bộ vẽ đường cong dẫn hướng (Arc Preview) và Bộ mô phỏng đường bay thực tế (Runtime Simulation) cùng chia sẻ một cấu trúc dữ liệu bất biến `ThrowSnapshot` và dùng chung phương thức tính toán va chạm. Điểm rơi thực tế cam kết trùng khớp điểm rơi hiển thị trước trong phạm vi dung sai tie-break $0.001\text{ m}$ (`contact_ambiguity_tolerance`).

#### C1.4 Quy chuẩn Mặt Pháp tuyến, Backface và Chiều Cuốn Lưới (Mesh Winding & Backfaces)
1. **Khóa Chặn Backface**: Khóa cứng `Physics.queriesHitBackfaces = false` trong toàn bộ thời gian chạy gameplay.
2. **Chuẩn Cuốn Lưới Môi trường (Outward Winding)**: Tất cả các mô hình 3D thuộc layer `World` bắt buộc phải có pháp tuyến hướng ra ngoài không gian chơi. Công cụ kiểm tra Level tự động (Content Validator) sẽ từ chối các mesh hở một mặt (single-sided mesh) mà mặt nhìn thấy lại là backface, ngăn ngừa triệt để lỗi tia nghe xuyên thấu qua tường mỏng.
3. **Kiểm tra Tính Đầy đủ Của Cảnh (Contentful Scene Check)**: Khi nạp cảnh, hệ thống assert rằng cảnh chơi phải có ít nhất một collider không phải trigger thuộc `E20Mask`. Một cảnh rỗng hoặc quên gắn layer `World` sẽ lập tức kích hoạt lỗi `PHY_EMPTY_E20_SCENE`.

#### C1.5 Đồng bộ Hóa Transform và Quy cách Capsule Nhân vật
1. **Đồng bộ Transform Theo Lô (Demand-Driven Batch Sync)**:
   - Thiết lập `Physics.autoSyncTransforms = false` để loại trừ chi phí CPU đồng bộ lặp lại vô ích trong quá trình di chuyển nhân vật.
   - Lệnh gọi tường minh `Physics.SyncTransforms()` được thực thi có kiểm soát tại hai thời điểm:
     1. Một lần duy nhất ngay trước khi bắt đầu một chuỗi tích phân mô phỏng Burst.
     2. Một lần duy nhất tại đầu mỗi chu kỳ tick cảm biến của AI Perception ($2\text{--}5\text{ Hz}$).

2. **Kích thước Capsule Nhân vật**:
   - **Người chơi (Player)**:
     - Tư thế Đứng: Chiều cao $h = 1.80\text{ m}$, Bán kính $r = 0.35\text{ m}$ (`r_player`).
     - Tư thế Ngồi: Chiều cao $h = 1.30\text{ m}$, Bán kính $r = 0.35\text{ m}$.
   - **Lính gác (Guard)**:
     - Chiều cao $h = 2.00\text{ m}$, Bán kính $r = 0.40\text{ m}$ (`r_guard`).
   - Cả Player và Guard đều là các thực thể Kinematic điều khiển thông qua mã lệnh và NavMeshAgent, không sử dụng Dynamic Rigidbody chịu lực vật lý tự do.

---

### States and Transitions

#### 1. Vòng đời Dịch vụ Truy vấn Vật lý (`PhysicsQueryService`)

```text
[ Uninitialized ]
        │
        ▼  (Scene Init / Awake)
[ Initializing ] ─── (Thiếu Layer / Scene Rỗng) ───► [ Initialization Error ] (Halt Load)
        │
        ▼  (Layer & E20 Mask Hợp lệ)
   [ Active ] ◄─────────────────────────────────────┐
        │                                           │
        ├─ (Truy vấn Cảm biến / Sensing Linecasts)  │
        ├─ (Đồng bộ Transform Chủ động Batch Sync)  │
        └─ (Tích phân Đường đạn Burst 120Hz) ───────┘
        │
        ▼  (Scene Unload / Shutdown)
  [ Disposed ]  (Phục hồi thiết lập autoSyncTransforms & queriesHitBackfaces ban đầu của Unity)
```

#### 2. Vòng đời Mô phỏng Đạn ném Burst (`BurstKinematicSimulation`)

```text
[ Idle / Carried ]
        │
        ▼  (Người chơi bấm Ném ở trạng thái Đứng tĩnh)
[ Initial Overlap Probe ] ─── (Vướng vật cản / Bão hòa) ──► [ Launch Rejected ] (Bảo toàn vật phẩm)
        │
        ▼  (Không gian phóng thông thoáng)
[ Substep Integration (120Hz) ] ◄─────────────────────────┐
        │                                                 │
        ├─ (Chưa chạm vật cản && t < t_max) ──────────────┘
        │
        ▼  (SphereCast chạm bề mặt E20)
[ Contact Resolution ]
        │  (Đẩy lùi tâm đạn hit.normal * (r + 0.001m))
        ▼
[ NoisePublished Event Emitted ] ──► [ Flight Terminated ]
```

---

### Interactions with Other Systems

| Hệ thống Tương tác | Dữ liệu/Hợp đồng Cung cấp | Dữ liệu Nhận lại từ Physics | Giao thức / Ràng buộc Đồng bộ |
|---|---|---|---|
| **Player Controller (`#11`)** | Tọa độ chân, tư thế (Đứng/Ngồi), lệnh ném | Cản trở di chuyển từ bề mặt `World`, kiểm tra trần cao đứng dậy $1.8\text{ m}$ | CharacterController trượt trên `World`; cấm va chạm vật lý trực tiếp với `Guard`. |
| **Perception (`#2`)** | Yêu cầu kiểm tra cản tia âm thanh (Q1/Q2) và tầm nhìn (Q3) | Kết quả boolean `Linecast` (bị cản hoặc thông suốt) | Tần số sensing $2\text{--}5\text{ Hz}$; bắt buộc gọi `Physics.SyncTransforms()` trước chuỗi quét. |
| **Player Noise (`#3`)** | Véc-tơ vận tốc ban đầu $\vec{v}_0$, góc $\theta$, điểm phóng | Điểm tiếp xúc va chạm thực tế $\vec{p}_{\text{landing\_contact}}$ | Chạy mô phỏng $120\text{ Hz}$ cố định; đồng bộ tuyệt đối giữa Preview Arc và Runtime. |
| **Player Movement & Hide (`#5`)** | Yêu cầu kiểm tra chứa đựng trong HideSpot (Q6), Backstop (Q5) | Kết quả `OverlapCapsule` của `HideSpotContainmentProfile` | Dùng độc quyền `QueryTriggerInteraction.Collide`; không chạm vào `E20Mask`. |
| **NavMesh / Pathfinding (`#12`)** | Hình học tĩnh để bake NavMesh, kiểm tra vách mỏng (Q7) | Kết quả `Linecast` xác minh vách mỏng $\le 0.40\text{ m}$ | Nguồn bake NavMesh lấy từ layer `World`; loại trừ hoàn toàn các layer động và trigger. |
| **Level / Content (`#8`)** | Cấu hình Layer collider trên các Prefab và khối kiến trúc | Báo cáo kiểm tra tính hợp lệ của pháp tuyến (Normals) và Layer | Validator cảnh báo nếu collider thiếu layer `World` hoặc có chiều dày $< 0.10\text{ m}$. |

## Formulas

### D1: Điểm Tâm Tiếp xúc Sau Va chạm (Contact Surface Push-Out Vector)

Nhằm ngăn chặn triệt để hiện tượng tâm khối cầu va chạm ($r = 0.05\text{ m}$) bị kẹt dính (penetration embedding) vào bên trong collider của bề mặt `World` ở tick kế tiếp, tọa độ công bố sau va chạm được đẩy lùi dọc theo véc-tơ pháp tuyến bề mặt:

$$\vec{p}_{\text{contact}} = \vec{p}_{\text{hit}} + \vec{n}_{\text{hit}} \times (r_{\text{proj}} + \epsilon_{\text{contact}})$$

**Biến số:**
| Biến số | Ký hiệu | Kiểu | Miền giá trị | Đơn vị | Mô tả |
|---|---|---|---|---|---|
| Điểm tiếp xúc bề mặt | $\vec{p}_{\text{hit}}$ | Vector3 | $\mathbb{R}^3$ | m | Tọa độ điểm va chạm chính xác trả về từ `RaycastHit.point` |
| Pháp tuyến bề mặt | $\vec{n}_{\text{hit}}$ | Vector3 | $\|\vec{n}\| = 1$ | — | Véc-tơ pháp tuyến đơn vị hướng ra ngoài tại điểm va chạm (`RaycastHit.normal`) |
| Bán kính đạn Burst | $r_{\text{proj}}$ | Float | $0.05$ (LOCKED) | m | Bán kính hình cầu va chạm (`entities.yaml` entry `projectile_radius`) |
| Khoảng đệm an toàn | $\epsilon_{\text{contact}}$ | Float | $0.001$ (LOCKED) | m | Biên độ đẩy lùi bề mặt tối thiểu ($1\text{ mm}$, `entities.yaml` entry `epsilon_contact`) |
| Điểm tâm công bố | $\vec{p}_{\text{contact}}$ | Vector3 | $\mathbb{R}^3$ | m | Tọa độ tâm hình cầu tiếp xúc công bố cho bộ mô phỏng và nguồn âm thanh |

**Ví dụ tính toán:**
- Đạn ném rơi xuống mặt sàn nằm ngang phẳng tại $\vec{p}_{\text{hit}} = (5.000, 0.000, 10.000)\text{ m}$.
- Pháp tuyến hướng thẳng đứng lên trời: $\vec{n}_{\text{hit}} = (0, 1, 0)$.
- Khoảng đẩy lùi: $r_{\text{proj}} + \epsilon_{\text{contact}} = 0.050 + 0.001 = 0.051\text{ m}$.
- Tọa độ tâm công bố: $\vec{p}_{\text{contact}} = (5.000, 0.051, 10.000)\text{ m}$.

---

### D2: Tích phân Bước đạn Rời rạc (Discrete Ballistic Substep Integration)

Chuyến bay của vật thể Burst được tích phân rời rạc theo thời gian thực ở tần số $120\text{ Hz}$ không phụ thuộc vào framerate hiển thị:

$$\Delta \vec{p}_k = \vec{v}_k \Delta t_{\text{tick}} + \frac{1}{2} \vec{g} \Delta t_{\text{tick}}^2, \quad \vec{v}_{k+1} = \vec{v}_k + \vec{g} \Delta t_{\text{tick}}$$

**Biến số:**
| Biến số | Ký hiệu | Kiểu | Miền giá trị | Đơn vị | Mô tả |
|---|---|---|---|---|---|
| Bước thời gian cố định | $\Delta t_{\text{tick}}$ | Float | $1/120 \approx 0.0083333333$ | s | Chu kỳ tích phân chuẩn (`entities.yaml` entry `dt_max`) |
| Gia tốc trọng trường | $\vec{g}$ | Vector3 | $(0, -9.81, 0)$ | m/s² | Trọng lực tiêu chuẩn của PhysX thế giới |
| Vận tốc tại bước $k$ | $\vec{v}_k$ | Vector3 | $\mathbb{R}^3$ | m/s | Véc-tơ vận tốc tức thời |
| Dịch chuyển tại bước $k$ | $\Delta \vec{p}_k$ | Vector3 | $\mathbb{R}^3$ | m | Quãng đường quét `SphereCast` trong bước tích phân thứ $k$ |

**Ví dụ tính toán:**
- Vận tốc phóng ban đầu $\vec{v}_0 = (0, 5.000, 8.660)\text{ m/s}$ ($v_0 = 10.0\text{ m/s}, \theta = 30^\circ$).
- Tại bước đầu tiên $k = 0$, $\Delta t = 1/120\text{ s}$:
  - $\Delta y_0 = 5.000 \times (1/120) - 0.5 \times 9.81 \times (1/120)^2 = 0.04167 - 0.00034 = 0.04133\text{ m}$.
  - $\Delta z_0 = 8.660 \times (1/120) = 0.07217\text{ m}$.
  - Độ dài phân đoạn quét SphereCast: $\|\Delta \vec{p}_0\|_2 = \sqrt{0.04133^2 + 0.07217^2} \approx 0.08318\text{ m}$ ($8.32\text{ cm}$).

---

### D3: Khử Nghiệm Thời gian Phóng Dưới Tick (Sub-Tick Analytical Root Cutoff)

Khi tính toán nghiệm tiếp xúc mặt sàn giải tích $y(t) = y_{\text{release}} + v_{0y} t - \frac{1}{2} g t^2 = y_{\text{landing}}$, nghiệm thời gian $t_{\text{flight}}$ phải thỏa mãn điều kiện lọc:

$$t_{\text{flight}} = \min \left\{ t \in t_{\text{roots}} \mid t > \epsilon_t \right\}$$

với $\epsilon_t = 1/120\text{ s} \approx 0.0083333333\text{ s}$ (`entities.yaml` entry `epsilon_t`).

**Biến số:**
| Biến số | Ký hiệu | Kiểu | Miền giá trị | Đơn vị | Mô tả |
|---|---|---|---|---|---|
| Tập nghiệm giải tích | $t_{\text{roots}}$ | Mảng Float | $\mathbb{R}$ | s | Các nghiệm thực của phương trình bậc hai quỹ đạo đạn |
| Ngưỡng lọc dưới tick | $\epsilon_t$ | Float | $1/120$ (LOCKED) | s | Thời gian 1 tick chuẩn để khử nghiệm ban đầu lúc phóng |
| Thời gian bay hợp lệ | $t_{\text{flight}}$ | Float | $(\epsilon_t, +\infty)$ | s | Thời gian bay chính thức được công nhận |

**Quy tắc phán quyết:** Mọi nghiệm $t \le \epsilon_t$ bị xem là điểm xuất phát hoặc sai số làm tròn số thực IEEE 754 và bị vứt bỏ. Nếu không có nghiệm nào $> \epsilon_t$, đạn không va chạm bề mặt trong khoảng giải tích.

---

### D4: Vị từ Chấp thuận Điểm Phóng Ban đầu (Initial Overlap Admission Predicate)

Trước khi kích hoạt lệnh ném và tiêu tốn vật phẩm, số lượng va chạm $N_{\text{hits}}$ từ truy vấn `OverlapSphere` được đánh giá theo vị từ:

$$\text{Admission}(N_{\text{hits}}) = \begin{cases} \text{ADMITTED} & \text{nếu } N_{\text{hits}} == 0 \\ \text{REJECTED\_OVERLAP} & \text{nếu } 0 < N_{\text{hits}} < C_{\text{buf}} \\ \text{FAIL\_CLOSED\_SATURATED} & \text{nếu } N_{\text{hits}} \ge C_{\text{buf}} \end{cases}$$

**Biến số:**
| Biến số | Ký hiệu | Kiểu | Miền giá trị | Đơn vị | Mô tả |
|---|---|---|---|---|---|
| Số va chạm phát hiện | $N_{\text{hits}}$ | Integer | $[0, +\infty)$ | vật thể | Số collider chạm phải khối cầu bán kính $0.05\text{ m}$ tại điểm phóng |
| Dung lượng bộ đệm | $C_{\text{buf}}$ | Integer | $64$ (LOCKED) | vật thể | Kích thước mảng tĩnh `initial_overlap_result_capacity` (`entities.yaml`) |
| Trạng thái tiếp nhận | $\text{Admission}$ | Enum | `{ADMITTED, REJECTED_OVERLAP, FAIL_CLOSED_SATURATED}` | — | Kết quả phán quyết cho phép hoặc hủy lệnh phóng |

---

### D5: Bất biến Dung sai Phân xử Va chạm (Collision Ambiguity Invariant)

Trong kiểm tra chất lượng màn chơi (Content Certification), nếu một tia quét ghi nhận hai bề mặt va chạm độc lập có khoảng cách chênh lệch nhỏ hơn dung sai tie-break:

$$\text{TieStatus}(d_1, d_2) = \begin{cases} \text{AMBIGUOUS\_COLLISION} & \text{nếu } |d_2 - d_1| < \delta_{\text{ambiguity}} \\ \text{DETERMINISTIC} & \text{nếu } |d_2 - d_1| \ge \delta_{\text{ambiguity}} \end{cases}$$

với $\delta_{\text{ambiguity}} = 0.001\text{ m}$ ($1\text{ mm}$, `entities.yaml` entry `contact_ambiguity_tolerance`).

**Quy tắc kiểm duyệt:** Cảnh chơi có vị trí hình học gây ra `AMBIGUOUS_COLLISION` sẽ bị từ chối kiểm duyệt (Fail Build Gate) để yêu cầu Level Designer hiệu chỉnh lại khe nẹp hình học, bảo đảm tính tất định tuyệt đối khi ném.

---

### D6: Bất biến Độ dày Tường Kiến trúc Tối thiểu (Minimum Wall Thickness Invariant)

Nhằm ngăn ngừa triệt để hiện tượng xuyên hầm (tunneling) của cả đạn ném lẫn tia cản âm thanh:

$$T_{\text{wall}} \ge \delta_{\text{wall\_min}} = 2 \times r_{\text{proj}} = 0.10\text{ m}$$

**Biến số:**
| Biến số | Ký hiệu | Kiểu | Miền giá trị | Đơn vị | Mô tả |
|---|---|---|---|---|---|
| Chiều dày tường thực tế | $T_{\text{wall}}$ | Float | $(0, +\infty)$ | m | Khoảng cách đo được giữa hai mặt đối diện của vách ngăn E20 |
| Bán kính đạn ném | $r_{\text{proj}}$ | Float | $0.05$ | m | Bán kính hình cầu đạn Burst ($5\text{ cm}$) |
| Độ dày tối thiểu bắt buộc | $\delta_{\text{wall\_min}}$ | Float | $0.10$ | m | Ngưỡng chặn hình học kiến trúc ($10\text{ cm}$) |

**Quy tắc kiểm duyệt:** Mọi vách ngăn có $T_{\text{wall}} < 0.10\text{ m}$ đều bị Level Validator gắn cờ `WALL_TOO_THIN_TUNNELING_RISK`.

## Edge Cases

### E1: Điểm Phóng Đạn Bị Chèn Lấp (Launch Overlap in Geometry)
- **Tình huống**: Người chơi đứng sát góc tường hoặc dưới gầm kệ và kích hoạt ném chai Burst, khiến hình cầu va chạm $r = 0.05\text{ m}$ tại điểm phóng bị chèn lấp bên trong một collider `World`.
- **Xử lý**: Hàm `OverlapSphere` ban đầu (Formula D4) phát hiện $N_{\text{hits}} > 0$. Lệnh ném lập tức bị hủy bỏ với chẩn đoán `INITIAL_OVERLAP_REJECTED`. Vật phẩm ném trong túi người chơi được bảo toàn nguyên vẹn, không sinh ra chuyến bay, không trừ tài nguyên và không phát tán sự kiện âm thanh.

### E2: Bão Hòa Bộ Đệm Quét Ban Đầu (Initial Overlap Buffer Saturation)
- **Tình huống**: Điểm phóng rơi vào một vùng có cấu trúc hình học cực kỳ dày đặc (ví dụ: lưới rào kim loại nhiều mắt) với số lượng collider chạm phải vượt quá dung lượng mảng tĩnh $64$ phần tử (`initial_overlap_result_capacity`).
- **Xử lý**: Hệ thống kích hoạt cơ chế an toàn đóng (Fail-Closed) với mã lỗi `INITIAL_OVERLAP_QUERY_INCOMPLETE`. Lệnh ném bị từ chối tuyệt đối để tránh việc Unity chỉ cắt gọn danh sách collider mà bỏ sót vật cản thực tế.

### E3: Thiếu Layer Khi Khởi Tạo Cảnh (Missing Required Layer on Scene Init)
- **Tình huống**: Nhà phát triển hoặc Level Designer nạp một cảnh thử nghiệm nhưng quên thiết lập Layer `World` trong cấu hình Tag Manager của dự án.
- **Xử lý**: Hàm `LayerMask.NameToLayer("World")` trả về `-1`. Hệ thống từ chối khởi tạo `PhysicsQueryService`, ngừng nạp cảnh và xuất log lỗi nghiêm trọng `PHY_LAYER_MISSING: Layer 'World' is undefined`. Hệ thống **tuyệt đối không âm thầm chuyển sang Layer Default**, tránh việc toàn bộ tia cản âm thanh và tầm nhìn bị vô hiệu hóa ngầm.

### E4: Cảnh Chơi Hoàn Toàn Rỗng Collider E20 (Contentless E20 Scene)
- **Tình huống**: Cảnh kiểm thử không có bất kỳ khối hình học tĩnh nào thuộc layer `World`.
- **Xử lý**: Khâu khởi tạo kiểm tra `Contentful Scene Check`. Nếu không tìm thấy ít nhất một collider không phải trigger trên `E20Mask`, hệ thống dừng nạp cảnh với lỗi `PHY_EMPTY_E20_SCENE`.

### E5: Rò Rỉ Tia Qua Mặt Lưới Đơn (Single-Sided Mesh Raycast Leakage)
- **Tình huống**: Một vách ngăn kiến trúc được dựng bằng mặt phẳng polygon đơn (single-sided quad). Khi lính gác hoặc người chơi ở phía mặt sau không có pháp tuyến, tia cản tầm nhìn/âm thanh đi từ mặt sau sẽ xuyên qua do thiết lập `Physics.queriesHitBackfaces = false`.
- **Xử lý**: 
  1. Level Validator chạy kiểm duyệt ngoại tuyến và tự động gắn cờ lỗi `SINGLE_SIDED_COLLIDER_DETECTED`.
  2. Quy chuẩn kiến trúc bắt buộc mọi vách ngăn phải có độ dày tối thiểu $T_{\text{wall}} \ge 0.10\text{ m}$ (Formula D6) hoặc sử dụng collider hộp (BoxCollider) có đầy đủ 6 mặt outward-facing.

### E6: Xuyên Tường Do Tốc Độ Cao (High-Velocity Tunneling Mitigation)
- **Tình huống**: Vật thể bay với vận tốc cực đại ($v_0 = 12.0\text{ m/s}$) hướng về một vách tường mỏng.
- **Xử lý**: Thay vì kiểm tra va chạm bằng phép thử điểm rời rạc (point sampling), mỗi tick tích phân $\Delta t = 1/120\text{ s}$ thực hiện quét thể tích liên tục `PhysicsScene.SphereCast`. Độ dịch chuyển mỗi tick tối đa chỉ khoảng $\approx 0.10\text{ m}$, kết hợp với độ dày tường tối thiểu $0.10\text{ m}$ (D6) bảo đảm khối cầu va chạm không bao giờ nhảy cóc qua bờ bên kia của tường.

### E7: Trigger Nằm Đè Lên Collider Kiên Cố (Trigger Clipping into Solid Geometry)
- **Tình huống**: Thể tích của một trigger nhặt đồ (Pickup Trigger) hoặc trigger cửa lấn vào bên trong tường bê tông `World`.
- **Xử lý**: Nhờ chính sách bắt buộc `QueryTriggerInteraction.Ignore` trên toàn bộ các truy vấn cảm biến Q1–Q5 và Q7, sự hiện diện của collider Trigger hoàn toàn vô hình đối với các tia kiểm tra âm thanh, tầm nhìn và va chạm Burst. Tia sẽ xuyên qua trigger và chỉ dừng lại khi chạm trúng bề mặt `World`.

### E8: Trần Thấp Chặn Tư Thế Ném Đứng (Low Ceiling Burst Stance Clearance)
- **Tình huống**: Người chơi đang ngồi trong ống thông gió hoặc gầm bàn (chiều cao trần $< 1.80\text{ m}$) và cố gắng nhấn phím ném Burst (cơ chế ném yêu cầu tư thế Đứng $1.80\text{ m}$).
- **Xử lý**: Trước khi thực hiện ném, hệ thống bắn một tia quét thẳng đứng từ chân người chơi lên độ cao $1.80\text{ m}$ trên `E20Mask`. Nếu bị cản trở bởi trần, lệnh ném bị từ chối ngay lập tức trước khi tiêu hao tài nguyên với mã lỗi `BURST_STAND_CLEARANCE_BLOCKED`.

### E9: Va Chạm Lưỡng Lự Cùng Khoảng Cách (Tie-Break Ambiguous Collision)
- **Tình huống**: Quả đạn Burst bay chui vào một khe nẹp góc nhọn và chạm trúng đồng thời hai bề mặt collider độc lập ở khoảng cách chênh lệch $< 0.001\text{ m}$.
- **Xử lý**: 
  - Trong kiểm duyệt màn chơi (Content Certification): Bị gắn cờ `AMBIGUOUS_COLLISION` và yêu cầu chỉnh sửa hình học level.
  - Trong runtime (Fallback an toàn): Hệ thống phân xử tất định bằng cách chọn collider có `collider.GetInstanceID()` nhỏ hơn, bảo đảm cả máy khách Preview và Runtime đều chọn ra cùng một điểm tiếp xúc duy nhất.

### E10: Gốc Tia Nằm Lọt Bên Trong Collider (Raycast Origin Embedded in Geometry)
- **Tình huống**: Do va chạm đẩy lùi hoặc góc hẹp, tọa độ mắt lính hoặc chân người chơi bị chìm vào bên trong một collider `World`.
- **Xử lý**: Do `queriesHitBackfaces = false`, tia phóng từ bên trong collider sẽ bay xuyên ra ngoài mà không ghi nhận va chạm, dẫn đến lỗi nhìn xuyên tường ngược. Để khắc phục: Dịch vụ `PhysicsQueryService` thực hiện một kiểm tra `CheckSphere` bán kính $0.05\text{ m}$ tại điểm gốc. Nếu điểm gốc bị chìm trong `E20Mask`, truy vấn tự động trả về `HIT_OCCLUDED` (coi như bị cản hoàn toàn, an toàn đóng).

### E11: Trôi Lệch Dấu Phẩy Động Trên WebGL (WebGL 32-bit Float Drift)
- **Tình huống**: Trình duyệt WebGL biên dịch mã sang WebAssembly (WASM) với các cơ chế tối ưu SIMD số thực khác biệt so với x86_64 trên PC.
- **Xử lý**: Mọi hằng số tích phân được khóa ở dạng số thực đơn chính xác (`float` C# với hậu tố `f`, ví dụ: `1.0f / 120.0f`). Nghiêm cấm ép kiểu ngầm định qua lại giữa `double` và `float` trong vòng lặp tích phân đạn ném.

### E12: Cửa Di Chuyển Khi Đạn Đang Bay (Dynamic Door Closing Mid-Flight)
- **Tình huống**: Một cánh cửa trượt đang đóng lại trong khi quả đạn Burst đang bay ngang qua ngưỡng cửa.
- **Xử lý**: Nhờ lệnh gọi đồng bộ `Physics.SyncTransforms()` trước mỗi chu kỳ mô phỏng Burst, collider của cánh cửa đang dịch chuyển sẽ được cập nhật tọa độ hình học tức thời trong `PhysicsScene`. Nếu cánh cửa chặn đường bay, SphereCast sẽ chạm trúng mặt cánh cửa và nổ ngay tại bề mặt cửa.

## Dependencies

Hệ thống Vật lý & Cấu hình Va chạm (System #18) thuộc tầng **Nền tảng (Foundation Layer)**, hoạt động như hạ tầng kỹ thuật trung tâm cung cấp dịch vụ truy vấn hình học tĩnh cho toàn bộ các hệ thống AI, Gameplay và Nhân vật.

### 1. Phụ thuộc Thượng nguồn (Upstream Dependencies)
- **Unity 6 LTS PhysX Engine**: Cung cấp các API truy vấn cấp thấp `PhysicsScene.Raycast`, `PhysicsScene.Linecast`, `PhysicsScene.SphereCast`, `PhysicsScene.OverlapSphere` và cơ chế quản lý ma trận va chạm tầng vật lý.
- **ADR-0002 (`docs/architecture/adr-0002-physics-collision-contract.md`)**: Tài liệu kiến trúc chuẩn hóa bắt buộc:
  - Đăng ký bộ quy tắc `E20_layer_manifest` cô lập.
  - Quy định phân tách `HideSpotContainmentProfile` khỏi `E20Mask`.
  - Thiết lập toàn cục `Physics.autoSyncTransforms = false` và `Physics.queriesHitBackfaces = false`.

---

### 2. Phụ thuộc Hạ nguồn (Downstream Consumers)
Mọi hệ thống tiêu thụ bên dưới đều có hợp đồng hai chiều rõ ràng với System #18:

| Hệ thống Tiêu thụ | GDD Tham chiếu | Dữ liệu & Giao diện Tiêu thụ từ System #18 | Hợp đồng Hai chiều & Bắt buộc Kỹ thuật |
|---|---|---|---|
| **Player Third-Person Controller** (#11) | `design/gdd/player-third-person-controller.md` | - Layer `Player`<br>- Thông số Capsule ($r=0.35\text{ m}$, $h=1.80\text{ m} / 1.30\text{ m}$)<br>- Truy vấn quét kiểm tra trần đứng dậy | Sử dụng `E20Mask` cho truy vấn cản trần; di chuyển nhân vật tương tác với `World` qua CharacterController. |
| **Perception Systems** (#2) | `design/gdd/perception.md` | - Truy vấn Linecast cản âm thanh ($0.5\text{ m}$)<br>- Truy vấn Raycast cản tầm nhìn (Mắt lính $\to$ 3 điểm kiểm tra người chơi)<br>- `E20Mask` | Bắt buộc truyền `QueryTriggerInteraction.Ignore` để không bị trigger đồ đạc/hidespot che khuất tầm nhìn hoặc chặn sóng âm. |
| **Player Noise (`NoiseEmitter`)** (#3) | `design/gdd/player-noise.md` | - Quét đạn Burst bằng `SphereCast` ($r=0.05\text{ m}$)<br>- Kiểm tra chèn lấp `OverlapSphere` ($64$ slot)<br>- Công thức đẩy điểm nổ D1 | Đồng bộ `Physics.SyncTransforms()` trước chuỗi ném; nhận diện điểm va đập và sinh sự kiện nổ trên bề mặt `World`. |
| **Player Movement & Hide** (#5) | `design/gdd/player-movement-hide.md` | - Layer `HideSpot`<br>- Profile `HideSpotContainmentProfile`<br>- Tia Backstop Linecast | Tách biệt hoàn toàn: Kiểm tra chui vào chỗ ẩn nấp dùng `QueryTriggerInteraction.Collide` trên layer `HideSpot`, còn tia phát hiện lính dùng `E20Mask` với `Ignore`. |
| **Guard AI (FSM Core)** (#1) | `design/gdd/guard-ai-fsm.md` | - Tia kiểm tra LOS bắt giữ (`catch_range`)<br>- Tia kiểm tra khả kiến điểm điều tra | Xác nhận đường ngắm bắt giữ không bị tường `World` cản trước khi chuyển sang trạng thái bắt giữ người chơi. |
| **NavMesh / Pathfinding** (#12) | `design/gdd/navmesh-pathfinding.md` | - Hình học va chạm tĩnh trên layer `World`<br>- Tia kiểm tra vách ngăn mỏng khi `SamplePosition` | Cung cấp hình học làm nguồn nướng NavMesh tĩnh; bảo đảm không snapping xuyên tường mỏng nhờ linecast cản trên `E20Mask`. |
| **Level / Content** (#8) | `design/gdd/level-content.md` | - Quy chuẩn độ dày tường ($T_{\text{wall}} \ge 0.10\text{ m}$)<br>- Bất biến mặt lưới hướng ra ngoài (Outward Winding)<br>- Bất biến dung sai phân xử va chạm | Kiểm duyệt hình học màn chơi trước khi build (Level Validator Gate), loại bỏ mesh 1 mặt không có collider kín. |

## Tuning Knobs

Toàn bộ các tham số điều khiển của hệ thống vật lý đều được định kiểu dữ liệu rõ ràng, xác định miền giá trị an toàn (Safe Range) và chỉ rõ tác động gameplay:

| Tên Tham số (`entities.yaml`) | Ký hiệu | Giá trị Mặc định | Miền An toàn | Đơn vị | Tác động Gameplay & Lý do Thiết kế |
|---|---|---|---|---|---|
| `contact_pushout_epsilon` | $\epsilon_{\text{contact}}$ | $0.001$ | $[0.0005, 0.005]$ | m | Khoảng đẩy tâm hình cầu khỏi mặt phẳng va chạm để chống kẹt/chìm collider trong các tick tiếp theo. |
| `trajectory_fixed_timestep` | $\Delta t$ | $0.0083333333$ ($1/120\text{ s}$) | $[1/240, 1/60]$ | s | Bước thời gian cố định tích phân cung bay của Burst. $120\text{ Hz}$ bảo đảm độ mịn và triệt tiêu sai số tunneling. |
| `initial_overlap_result_capacity` | $C_{\text{buf}}$ | $64$ | $[16, 128]$ | phần tử | Kích thước mảng tĩnh đệm collider khi kiểm tra chèn lấp tại điểm phóng đạn. Quá ngưỡng sẽ kích hoạt Fail-Closed an toàn. |
| `subtick_root_epsilon` | $\epsilon_t$ | $0.0083333333$ ($1/120\text{ s}$) | $[0.001, 0.02]$ | s | Ngưỡng thời gian loại bỏ nghiệm kỳ dị $t \approx 0$ khi ném ngang mặt đất để tránh phát nổ ngay tại tay người chơi. |
| `contact_ambiguity_tolerance` | $\delta_{\text{ambiguity}}$ | $0.001$ | $[0.0005, 0.005]$ | m | Dung sai so sánh khoảng cách va chạm giữa 2 mặt phẳng trong công cụ Level Validator để phát hiện va chạm nhập nhằng. |
| `min_wall_thickness` | $\delta_{\text{wall\_min}}$ | $0.10$ | $[0.08, 0.25]$ | m | Độ dày tối thiểu bắt buộc của mọi vách ngăn hình học E20 để ngăn đạn bay xuyên tường và rò rỉ âm thanh/tầm nhìn. |
| `burst_projectile_radius` | $r_{\text{proj}}$ | $0.05$ | $[0.03, 0.10]$ | m | Bán kính hình cầu SphereCast của đạn Burst. Quyết định kích thước va chạm hình học của chai gây nhiễu. |
| `sensing_sync_max_frequency` | $f_{\text{sync\_max}}$ | $5.0$ | $[2.0, 10.0]$ | Hz | Tần số tối đa thực hiện lệnh đồng bộ hóa `Physics.SyncTransforms()` trước mỗi chu kỳ cảm biến để tối ưu CPU trên WebGL/PC. |

## Visual/Audio Requirements

Là hạ tầng kỹ thuật vô hình (Invisible Infrastructure), Hệ thống Vật lý không trực tiếp hiển thị đồ họa thương mại hay tự phát âm thanh, nhưng có trách nhiệm cung cấp dữ liệu chẩn đoán hình ảnh và phân loại bề mặt âm thanh:

### 1. Yêu cầu Hiển thị & Debug Gizmos (Visual & Debug Rendering)
- **Tia cảm biến AI (Sensing Ray Visualizer)** (Chế độ Editor/Debug):
  - Tia tầm nhìn (Vision Raycast): Màu xanh lục (Clear LOS) khi không bị cản; chuyển sang màu đỏ (Occluded) tại điểm chạm bề mặt `World`.
  - Tia cản âm thanh (Hearing Linecast): Màu vàng sáng khi thông suốt; màu xám đậm khi xuyên qua vật cản âm học.
- **Đường bay đạn Burst (Trajectory Arc & Impact Marker)**:
  - Đường cong parabol dự đoán (Preview Arc): Hiển thị qua `LineRenderer` màu trắng đục bán trong suốt ($120$ điểm mẫu).
  - Điểm chạm dự đoán (Impact Decal / Marker): Hiển thị hình tròn hoặc decal chiếu phẳng áp sát bề mặt va chạm với pháp tuyến $\vec{n}_{\text{hit}}$.
  - Khối cầu va chạm (Collision Sphere Gizmo): Hình cầu dây thép (wireframe sphere) bán kính $r = 0.05\text{ m}$ màu cam tại điểm dừng cuối cùng.
- **Thể tích Trigger Ẩn nấp (HideSpot Volumes)**:
  - Khối hộp/hình trụ trigger của `HideSpot` hiển thị màu tím bán trong suốt (`alpha = 0.25`) khi bật Gizmos.
- **Cảnh báo Vi phạm Kiến trúc (Level Geometry Violations)**:
  - Trong công cụ kiểm duyệt màn chơi, các vách ngăn mỏng vi phạm $T_{\text{wall}} < 0.10\text{ m}$ hoặc lưới đơn single-sided sẽ được vẽ viền đỏ nhấp nháy (`GL_LINES`) để Level Designer dễ dàng phát hiện.

### 2. Ghép nối Âm thanh & Bề mặt Va chạm (Audio Material Coupling)
- Kết quả va chạm `RaycastHit` và `SphereCast` phải trả về thông tin `PhysicMaterial` hoặc thẻ nhận diện bề mặt (`SurfaceType`: Bê tông, Kim loại, Kính, Gỗ).
- Hệ thống truyền dữ liệu này đến `NoiseEmitter` và Audio Manager để phát đúng âm thanh va đập (Impact SFX) đặc trưng của chất liệu mà không cần hardcode.

## UI Requirements

### 1. Trải nghiệm Người chơi (Player-Facing UI)
- Không có bất kỳ giao diện vật lý nào hiển thị cho người chơi thông thường để bảo đảm tính chân thực và ngột ngạt của không khí lén lút (Zero Physics HUD clutter).

### 2. Bảng Theo dõi Kỹ thuật Dành cho Nhà phát triển (Developer Diagnostic Overlay - F3)
Khi kích hoạt bảng F3 Debug Console, giao diện hiển thị góc màn hình các thông số giám sát sức khỏe vật lý:
- `Physics Queries/sec`: Tổng số phép thử Raycast, Linecast, SphereCast thực hiện mỗi giây (Kỳ vọng: $< 250\text{ queries/s}$).
- `Transform Syncs/sec`: Số lần gọi `Physics.SyncTransforms()` trong một giây (Kỳ vọng: $\le 10\text{ calls/s}$).
- `Active Dynamic Rigidbodies`: Bắt buộc hiển thị $0$ (Bất biến cấm Rigidbody động).
- `Physics Allocation`: Cảnh báo màu vàng/đỏ nếu phát hiện phân bổ bộ nhớ GC rác (Garbage Collection alloc $> 0\text{ bytes}$) trong các hàm truy vấn vật lý chu kỳ.
- `Last Physics Diagnostic`: Hiển thị mã lỗi từ chối gần nhất (ví dụ: `INITIAL_OVERLAP_REJECTED`, `BURST_STAND_CLEARANCE_BLOCKED`, `INITIAL_OVERLAP_QUERY_INCOMPLETE`).

## Acceptance Criteria

Mọi tiêu chí chấp nhận đều được thiết kế độc lập, có điều kiện Pass/Fail định lượng rõ ràng để đội ngũ QA Lead và Kỹ sư Kiểm thử có thể tự động hóa hoặc kiểm tra thủ công:

- **AC1: Khởi tạo Layer Mask Tất định (Static Layer Mask Initialization)**
  - *Điều kiện thử*: Khởi động scene và gọi hàm khởi tạo `PhysicsQueryService`.
  - *Tiêu chuẩn Pass*: `LayerMask.NameToLayer("World")` trả về $\ge 0$. `E20Mask` chỉ chứa bit của layer `World`; tuyệt đối không chứa bit của `Player`, `Guard`, hay `HideSpot`. Nếu thiếu layer `World`, ném exception `PHY_LAYER_MISSING` và ngừng nạp cảnh.

- **AC2: Bất biến Không Rigidbody Động (Zero Dynamic Rigidbody Invariant)**
  - *Điều kiện thử*: Quét toàn bộ component `Rigidbody` trong Scene active khi bắt đầu vòng lặp.
  - *Tiêu chuẩn Pass*: $100\%$ Rigidbody đều có `isKinematic == true`. Số lượng Rigidbody động (`isKinematic == false`) bằng chính xác $0$.

- **AC3: Khóa Cấu hình Vật lý Toàn cục (Global Physics Configuration Assertions)**
  - *Điều kiện thử*: Kiểm tra cờ trạng thái `Physics` của Unity sau khi nạp cảnh.
  - *Tiêu chuẩn Pass*: `Physics.autoSyncTransforms == false` và `Physics.queriesHitBackfaces == false`.

- **AC4: Miễn nhiễm Trigger Tuyệt đối cho Tia Cảm biến (Sensing Query Trigger Immunity)**
  - *Điều kiện thử*: Đặt một thể tích Trigger (ví dụ: `HideSpot` hoặc Trigger vật phẩm) nằm chắn giữa Mắt lính và Người chơi, phía sau là một bức tường `World`. Bắn tia kiểm tra tầm nhìn và cản âm thanh.
  - *Tiêu chuẩn Pass*: Tia kiểm tra với `QueryTriggerInteraction.Ignore` xuyên qua hoàn toàn thể tích Trigger mà không sinh ra va chạm; chỉ ghi nhận cản trở khi chạm trúng bề mặt tường `World`.

- **AC5: Phân tách Độc lập Profile Chỗ Ẩn nấp (Dedicated HideSpot Profile Isolation)**
  - *Điều kiện thử*: Kiểm tra thể tích Capsule của người chơi chui vào chỗ ẩn nấp thông qua `HideSpotContainmentProfile`.
  - *Tiêu chuẩn Pass*: Phép thử sử dụng `QueryTriggerInteraction.Collide` và chỉ lọc duy nhất Layer `HideSpot`. Không bị ảnh hưởng hay xung đột với các collider kiên cố `World`.

- **AC6: Đồng bộ Hóa Transform Theo Nhu Cầu (Demand-Driven Transform Sync Batching)**
  - *Điều kiện thử*: Bắn chuỗi $10$ đạn Burst hoặc thực thi $20$ tia quét cảm biến AI trong cùng một khung hình.
  - *Tiêu chuẩn Pass*: `Physics.SyncTransforms()` được gọi tối đa $1$ lần trước mẻ mô phỏng Burst và $1$ lần ở đầu chu kỳ cảm biến AI ($2\text{--}5\text{ Hz}$). Tuyệt đối không gọi per-ray hoặc per-projectile.

- **AC7: Từ chối Phóng Đạn Khi Điểm Xuất Phát Bị Chèn Lấp (Initial Overlap Rejection)**
  - *Điều kiện thử*: Đặt người chơi đứng ép sát góc tường khiến khối cầu phóng $r = 0.05\text{ m}$ chạm phải collider `World` ($1 \le N_{\text{hits}} < 64$). Nhấn ném Burst.
  - *Tiêu chuẩn Pass*: Lệnh ném bị hủy ngay lập tức với mã lỗi `INITIAL_OVERLAP_REJECTED`. Chai Burst trong túi người chơi không bị trừ, không sinh ra đạn bay, không phát tán âm thanh.

- **AC8: An Toàn Đóng Khi Quá Tải Bộ Đệm Quét Ban Đầu (Fail-Closed Overlap Saturation)**
  - *Điều kiện thử*: Đặt điểm phóng vào vùng hình học cực dày đặc trả về $N_{\text{hits}} \ge 64$ collider.
  - *Tiêu chuẩn Pass*: Hệ thống từ chối lệnh ném với mã chẩn đoán `INITIAL_OVERLAP_QUERY_INCOMPLETE`. Không trừ tài nguyên đạn.

- **AC9: Đẩy Lùi Bề Mặt Tiếp Xúc Đạn Ném Đúng Định mức D1 (Contact Surface Push-Out Offset)**
  - *Điều kiện thử*: Phóng quả đạn Burst với vận tốc $10.0\text{ m/s}$ đập vuông góc vào một bức tường phẳng thẳng đứng.
  - *Tiêu chuẩn Pass*: Tọa độ tâm nổ $\vec{p}_{\text{contact}}$ cách điểm va chạm $\vec{p}_{\text{hit}}$ trên mặt phẳng đúng $r_{\text{proj}} + \epsilon_{\text{contact}} = 0.05\text{ m} + 0.001\text{ m} = 0.051\text{ m}$ theo hướng vector pháp tuyến $\vec{n}_{\text{hit}}$.

- **AC10: Triệt Tiêu Nghiệm Kỳ Dị Khi Ném Ngang Sàn D3 (Sub-Tick Root Cutoff)**
  - *Điều kiện thử*: Mô phỏng ném đạn với góc ném bằng $0$ và độ cao ban đầu sát mặt đất ($y_0 = y_{\text{floor}}$).
  - *Tiêu chuẩn Pass*: Hệ thống loại bỏ nghiệm $t \le 1/120\text{ s}$ và chỉ chấp nhận nghiệm $t > 1/120\text{ s}$. Đạn bay ra phía trước theo đúng cung ném thay vì phát nổ tức thời tại tay người chơi.

- **AC11: Chặn Đứng Dậy Ném Đạn Dưới Trần Thấp (Overhead Clearance Blocked)**
  - *Điều kiện thử*: Người chơi ở tư thế ngồi ($1.30\text{ m}$) trong đường ống có trần cao $1.40\text{ m}$ và nhấn phím ném Burst.
  - *Tiêu chuẩn Pass*: Tia kiểm tra đứng trên `E20Mask` phát hiện trần $< 1.80\text{ m}$. Lệnh ném bị từ chối với lỗi `BURST_STAND_CLEARANCE_BLOCKED`. Người chơi không bị ép đứng dậy và không mất tài nguyên.

- **AC12: Không Phân Bổ Rác Bộ Nhớ Trên Đường Truy Vấn Nóng (Zero GC Alloc Invariant)**
  - *Điều kiện thử*: Bật Unity Profiler và ghi nhận $1000$ frame liên tục khi $10$ lính gác đang quét cảm biến và người chơi liên tục nhắm ném Burst.
  - *Tiêu chuẩn Pass*: Bộ nhớ rác phân bổ `GC.Alloc` của toàn bộ các hàm truy vấn trong `PhysicsQueryService` bằng chính xác $0\text{ bytes}$ nhờ sử dụng mảng đệm tĩnh (`NonAlloc`).

- **AC13: Loại Trừ Tia Xuyên Mặt Sau & Kiểm Soát Hướng Mặt Lưới (Backface Rejection)**
  - *Điều kiện thử*: Bắn tia Raycast từ phía mặt sau không có pháp tuyến của một polygon đơn.
  - *Tiêu chuẩn Pass*: Tia xuyên qua không ghi nhận va chạm do `queriesHitBackfaces == false`. Công cụ Level Validator gắn cờ lỗi `SINGLE_SIDED_COLLIDER_DETECTED` với mọi mesh một mặt không kín.

- **AC14: Kiểm Chuẩn Độ Dày Vách Ngăn Tối Thiểu D6 (Minimum Wall Thickness Invariant)**
  - *Điều kiện thử*: Chạy Level Validator tự động trên toàn bộ khối hình học tĩnh của màn chơi.
  - *Tiêu chuẩn Pass*: $100\%$ các bức tường và vách ngăn trên layer `World` đều có độ dày $T_{\text{wall}} \ge 0.10\text{ m}$. Mọi vách mỏng hơn đều bị cảnh báo `WALL_TOO_THIN_TUNNELING_RISK`.

- **AC15: Tính Tất Định Khi Phân Xử Va Chạm Đồng Khoảng Cách D5 (Tie-Break Determinism)**
  - *Điều kiện thử*: Đạn Burst va chạm vào khe nẹp giữa hai collider có khoảng cách chênh lệch $< 0.001\text{ m}$.
  - *Tiêu chuẩn Pass*: Trong kiểm duyệt Editor: Cảnh báo lỗi `AMBIGUOUS_COLLISION`. Trong runtime: Chọn collider có `GetInstanceID()` nhỏ hơn, bảo đảm điểm tiếp xúc giữa Preview và Runtime hoàn toàn trùng khớp.

- **AC16: An Toàn Đóng Khi Gốc Tia Bị Nhúng Tường E10 (Embedded Origin Fail-Safe)**
  - *Điều kiện thử*: Đặt tọa độ gốc của tia tầm nhìn hoặc tia cản âm thanh lọt sâu $0.02\text{ m}$ bên trong một collider `World`.
  - *Tiêu chuẩn Pass*: `PhysicsQueryService` phát hiện gốc tia bị chìm thông qua phép thử `CheckSphere(0.05m)`, tự động trả về kết quả `HIT_OCCLUDED` (bị cản hoàn toàn). Tuyệt đối không để tia lọt ra ngoài tạo lỗi nhìn xuyên tường ngược.

## Open Questions

- **Hiện trạng**: Không còn câu hỏi thiết kế nào chưa được giải quyết ($0$ Open Questions). Toàn bộ các quy chuẩn phân tầng ma trận va chạm, quy tắc bất biến, công thức phân xử va chạm và cơ chế an toàn đóng đã được đồng bộ hóa hoàn toàn với `ADR-0002` và các GDD phụ thuộc (#1, #2, #3, #5, #11, #12).
