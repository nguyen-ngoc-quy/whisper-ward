# Input System

> **Status**: In Review
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge), Anti-Pillar Scope Discipline

## Overview

Hệ thống **Input System** (System `#19`) là lớp hạ tầng kỹ thuật nền tảng (Foundation Layer) chịu trách nhiệm tiếp nhận, chuẩn hóa, lọc nhiễu và trừu tượng hóa toàn bộ tín hiệu điều khiển phần cứng (Bàn phím, Chuột, Tay cầm Gamepad) thành các hành động ngữ cảnh chuẩn hóa (Action Maps) phục vụ cho *Whisper Ward*. Được xây dựng trên nền tảng gói **Unity New Input System** (`com.unity.inputsystem`), hệ thống hoạt động như một lớp trung gian phi trạng thái độc lập (decoupled input layer), cách ly mã nguồn điều khiển nhân vật (`PlayerThirdPersonController`), camera (`CinemachineCameraRig`) và giao diện (`HUD/UI`) khỏi phần cứng vật lý cụ thể. Hệ thống quản lý hai Action Map chính: **`Player`** (các hành vi di chuyển mặt đất WASD, chốt tư thế ngồi toggle C, chạy nhanh Shift, ném Burst LMB/RT, nhặt đồ E) và **`UI`** (điều hướng menu, con trỏ chuột, xác nhận/hủy, tạm dừng Escape/Start).

Hệ thống bảo đảm kỷ luật phạm vi cốt lõi (Anti-Pillar Scope Discipline) bằng việc từ chối tiếp nhận mọi hành động nằm ngoài danh mục cam kết (cấm vĩnh viễn phím nhảy, leo trèo, núp tường tự động hay cận chiến). Đồng thời, hệ thống thực thi các cơ chế bảo vệ trải nghiệm lén lút nghiêm ngặt: cơ chế đệm lệnh ném $150\text{ ms}$ (`input_buffer_window_s`) kết hợp bẻ vận tốc dừng (`throw_velocity_snap_threshold = 1.0 m/s`) để triệt tiêu hiện tượng nuốt nút khi nhả phím di chuyển, thuật toán khử trôi cần analog (Radial Deadzone) cho Gamepad, và cam kết không phân bổ bộ nhớ rác (Zero GC Allocation) trong các vòng lặp đọc giá trị liên tục trên cả PC và WebGL.

## Player Fantasy

Hệ thống Input System không phải là một cơ chế hào nhoáng để người chơi chiêm ngưỡng, mà là **lớp vỏ vô hình biến ý chí của người chơi thành hành động chính xác tuyệt đối (Invisible, Frictionless Agency)**. Trong một tựa game lén lút căng thẳng dựa trên tư duy và trí tuệ như *Whisper Ward*, sự thất bại của hệ thống điều khiển đồng nghĩa với cái chết của sự nhập vai. Khi một người chơi bị lính gác bắt, họ phải tự nhủ: *"Mình đã tính toán sai thời điểm"*, chứ tuyệt đối không bao giờ là: *"Phím bị kẹt"*, *"Nhân vật bị trôi đà"* hay *"Game nuốt nút ném của mình"*.

Hệ thống phục vụ trực tiếp hai trụ cột cốt lõi:

1. **Thách thức Trí tuệ Công bằng (Pillar 2: Fair Mind-Challenge)**:
   - **Dừng lại là Dừng lại (Zero-Latency Halt)**: Khi người chơi nhả phím di chuyển (WASD) hoặc thả cần analog về vùng trung hòa, nhân vật dừng bước ngay lập tức, không trôi đà vô nghĩa dẫn đến việc vô tình bước chân vào vùng nón thị giác của lính gác.
   - **Phản hồi Ngay khi Cần (Buffered Throw Snap)**: Khoảnh khắc nghẹt thở khi người chơi đang di chuyển gấp, nhận ra lính chuẩn bị quay đầu, nhả cần điều khiển và bấm ném đạn Burst. Nhờ cửa sổ đệm $150\text{ ms}$ (`input_buffer_window_s`), lệnh ném không bị nuốt chửng do ngón tay thao tác nhanh hơn tick vật lý, mà tự động bẻ vận tốc về đứng yên và phóng chai chính xác vào góc phòng dự định.
   - **Chốt Tư thế Đanh gọn (Decisive Stance Latch)**: Phím **C** là một chốt lật cạnh (Toggle Latch). Nhấn một lần là ngồi thụp xuống — hạ thấp hình chiếu cơ thể và triệt tiêu tiếng bước chân; nhấn lần nữa là đứng thẳng. Không bao giờ xảy ra lỗi "mỏi tay trượt nút" như cơ chế giữ phím (Hold).

2. **Kỷ luật Phạm vi Chặt chẽ (Anti-Pillar Scope Discipline)**:
   - Người chơi không bao giờ bị rơi vào các tình huống "dính tường ngoài ý muốn" (Cover Magnetism) hay "vô tình nhảy bổ vào mặt lính" khi cố gắng luồn lách qua góc hành lang hẹp. Bằng cách loại bỏ hoàn toàn các động từ dư thừa (không nhảy, không leo trèo, không bám tường), hệ thống trả lại sự tự do và tính toán thuần túy cho đôi chân người chơi.
   - Khi nép mình trong tủ đồ (`HideSpot`), rê chuột hoặc đẩy cần xoay góc nhìn để quan sát qua khe chớp hoàn toàn độc lập, không làm xê dịch tọa độ chân và không phát ra bất kỳ rung động âm thanh nào ($0\text{ dB}$).

## Detailed Design

### Core Rules

- **C1: Phân cấp Action Map & Độc quyền Tuyệt đối (Strict Exclusive Hierarchy)**:
  - Hệ thống quản lý hai Action Map chính yếu: **`Player`** (Gameplay) và **`UI`** (Giao diện / Tạm dừng).
  - Hai Action Map hoạt động theo nguyên tắc loại trừ lẫn nhau tuyệt đối (Mutually Exclusive). Tại bất kỳ thời điểm nào, chỉ có tối đa một Action Map được phép kích hoạt:
    - Ở trạng thái bình thường (In-Game): `PlayerActions.Enable()`, `UIActions.Disable()`. Con trỏ chuột bị khóa tâm màn hình (`Cursor.lockState = CursorLockMode.Locked`, `Cursor.visible = false`).
    - Khi người chơi mở Menu / Tạm dừng (Pause), bị bắt (Busted / Game Over), hoặc trong hoạt cảnh: `PlayerActions.Disable()`, `UIActions.Enable()`. Con trỏ chuột được giải phóng (`Cursor.lockState = CursorLockMode.None`, `Cursor.visible = true`).
  - **Khử trôi đầu vào (Input State Flush)**: Khi chuyển từ `Player` sang `UI`, hệ thống thực hiện xóa sạch toàn bộ bộ đệm và biến trạng thái đầu vào (`_moveInput = Vector2.zero`, `_lookInput = Vector2.zero`, giải phóng cờ `isSprintHeld`). Điều này loại bỏ 100% hiện tượng kẹt phím, trôi nhân vật khi bấm Pause lúc đang chạy.

- **C2: Chuẩn hóa Tín hiệu Di chuyển & Vùng chết Cần xoay (Movement Normalization & Deadzone)**:
  - Tín hiệu di chuyển (`Move`) được đọc từ 2D Vector Composite (Bàn phím: W, A, S, D) hoặc Gamepad (Left Stick Analog).
  - Tín hiệu từ Gamepad được lọc qua thuật toán **Vùng chết Xuyên tâm (Radial Deadzone)** với ngưỡng sàn $r_{\text{dead\_inner}} = 0.15$ và ngưỡng trần $r_{\text{dead\_outer}} = 0.95$ (Công thức D1). Mọi độ lệch cần điều khiển $< 0.15$ được ép về $0.0$, triệt tiêu hiện tượng trôi cần analog vật lý (Stick Drift).
  - Vector di chuyển chuẩn hóa sau đó được chiếu sang hệ tọa độ không gian thế giới (World Space) dựa theo góc phương vị phẳng (Planar Azimuth / Yaw) của Camera hiện tại:
    $$\vec{d}_{\text{world}} = (\vec{u}_{\text{cam\_fwd}} \times \text{input}_y + \vec{u}_{\text{cam\_right}} \times \text{input}_x)$$
    Trong đó $\vec{u}_{\text{cam\_fwd}}$ và $\vec{u}_{\text{cam\_right}}$ đã được triệt tiêu thành phần trục $Y$ và chuẩn hóa về độ dài $1.0$.

- **C3: Chốt Lật Tư thế Ngồi (Stance Toggle Latch)**:
  - Phím **C** (Bàn phím) hoặc nút **B / Circle / R3** (Gamepad) hoạt động như một công tắc lật cạnh xung kích hoạt (Edge-triggered Toggle Latch).
  - Tư thế ngồi (Crouched) là một trạng thái cố định kéo dài (Persistent Posture), không phụ thuộc vào việc phím có tiếp tục được giữ hay không.
  - **Kiểm tra độ thoáng trần khi đứng dậy (Uncrouch Clearance Check)**: Khi người chơi bấm C để chuyển từ Crouched sang Standing, hệ thống thực hiện một tia quét kiểm tra trần (`Physics.SphereCast` bán kính $r = 0.2\text{ m}$ hướng thẳng lên trần nhà từ vị trí hông đến độ cao $1.8\text{ m}$, sử dụng `E20_layer_manifest` và `QueryTriggerInteraction.Ignore`). Nếu phát hiện vật cản (độ cao trần $< 1.8\text{ m}$), thao tác đứng dậy bị từ chối; nhân vật buộc phải duy trì tư thế ngồi để đảm bảo tính thực tế của hình học không gian.

- **C4: Kích hoạt Chạy nhanh & Tự động Đứng dậy (Sprint & Auto-Stand Clearance)**:
  - Hành động Chạy nhanh (`Sprint`) sử dụng cơ chế **Giữ phím (Hold to Sprint)** qua phím **Left Shift** (Bàn phím) hoặc **Left Stick Click / L3** (Gamepad).
  - Chạy nhanh chỉ có hiệu lực khi người chơi đồng thời giữ phím Sprint VÀ có độ lớn vector di chuyển $|\vec{v}_{\text{input}}| > 0.1$. Khi nhả phím Shift hoặc thả cần di chuyển về tâm, nhân vật ngay lập tức quay về trạng thái Đi bộ (Walk) hoặc Đứng yên (Idle), triệt tiêu nguy cơ vô tình rò rỉ tiếng bước chân chạy nhanh ($12\text{ dB}$).
  - **Tự động Đứng dậy để Chạy (Auto-Stand to Sprint)**: Nếu người chơi đang ở tư thế ngồi (Crouched) mà kích hoạt giữ Sprint kèm đẩy phím di chuyển:
    - Hệ thống thực hiện kiểm tra độ thoáng trần $\ge 1.8\text{ m}$.
    - Nếu trần thông thoáng: Lập tức tự động chốt tư thế sang Standing và bứt tốc chạy nhanh (chuyển đổi mượt mà giúp người chơi phản xạ bỏ chạy tức thì khi gặp nguy hiểm).
    - Nếu trần bị vướng vật cản ($< 1.8\text{ m}$): Lệnh Sprint bị triệt tiêu hoàn toàn, nhân vật tiếp tục duy trì tốc độ bò ngồi ($V_{\text{crouch}} = 1.8\text{ m/s}$) dưới gầm an toàn.

- **C5: Bộ đệm Lệnh Ném, Bẻ Vận tốc Dừng & Chốt Đứng (Throw Action Buffering & Snap)**:
  - Hành động ném Burst (`Throw`) được kích hoạt bởi sự kiện nhấn cạnh (Button Press) của chuột trái (**LMB**) hoặc cò phải tay cầm (**RT / R2**).
  - **Cửa sổ đệm đầu vào (Input Buffer Window)**: Khi nhận tín hiệu ném, nếu nhân vật đang giảm tốc do vừa nhả cần điều khiển nhưng vận tốc hiện tại $V < 1.0\text{ m/s}$ (`throw_velocity_snap_threshold`), hệ thống lưu lệnh vào bộ đệm thời gian $150\text{ ms}$ (`input_buffer_window_s`), lập tức bẻ vận tốc nhân vật về $0.0\text{ m/s}$ trong cùng tick vật lý và thi hành phóng đạn (Formula D2).
  - **Giải quyết Tư thế Ngồi khi Ném**: Đạn Burst yêu cầu tư thế đứng thẳng để đạt quỹ đạo ném đạo diễn. Nếu người chơi bấm ném khi đang ngồi:
    - Thực hiện kiểm tra trần $\ge 1.8\text{ m}$. Nếu thoáng: Tự động dựng đứng nhân vật lên Standing trong cùng frame và thực hiện ném.
    - Nếu trần $< 1.8\text{ m}$ hoặc vận tốc di chuyển đang lớn $\ge 1.0\text{ m/s}$: Thao tác ném bị từ chối và ghi nhận mã chẩn đoán `THROW_WHILE_MOVING_REJECTED` hoặc `THROW_INSUFFICIENT_CLEARANCE_REJECTED`, bảo toàn túi trang bị mà không tiêu hao vật phẩm.

- **C6: Tín hiệu Xoay Camera & Điểm Ẩn nấp (Look & HideSpot Pure Pivot)**:
  - Tín hiệu xoay góc nhìn (`Look`) tiếp nhận từ Mouse Delta (Chuột) hoặc Right Stick Analog (Gamepad), được đưa qua hàm tỉ lệ độ nhạy và đường cong gia tốc (Formula D3) trước khi cấp cho `CinemachineCameraRig`.
  - **Khóa Chân Tự do Xoay trong Điểm Nấp (Pure Pivot Isolation)**: Khi người chơi đã chui vào trong điểm ẩn nấp (`HideSpot`), tín hiệu `Look` vẫn được phép hoạt động độc lập để người chơi quan sát xung quanh qua khe chớp / rèm che tủ đồ. Tuy nhiên, logic điều khiển nhân vật khóa cứng chuyển vị tịnh tiến ($\Delta \vec{p} == \vec{0}$) và không phát ra bất kỳ âm thanh va chạm/bước chân nào ($0\text{ dB}$).
  - **Thoát khỏi Điểm Ẩn nấp**: Người chơi phải duy trì đẩy tín hiệu di chuyển về phía trước với độ lớn $> 0.6$ trong thời gian liên tục $> 0.1\text{ s}$ mới kích hoạt hành động bước ra ngoài, tránh việc vô tình bước ra ngoài do va quẹt nhẹ vào cần điều khiển.

- **C7: Tương tác Nhặt Đồ (Interact Action)**:
  - Hành động Tương tác (`Interact`) được gán cho phím **E** (Bàn phím) hoặc nút **X / Square / A** (Gamepad).
  - Là hành động dạng nút nhấn cạnh (Single-fire Edge Trigger), dùng để nhặt lại khối máy tạo tiếng ồn Burst đã ném trên sàn khi người chơi tiến vào trong phạm vi bán kính với tới $r_{\text{reach}} \le 1.6\text{ m}$ (`pickup_reach_radius`).

- **C8: Hợp đồng Bộ nhớ Phi Phân bổ Rác (Zero GC Allocation Hot-Path)**:
  - Trong vòng lặp thực thi của Game (`Update` / `FixedUpdate`), tuyệt đối cấm sử dụng các phương thức tìm kiếm chuỗi như `InputActionAsset.FindAction("Move")`.
  - Toàn bộ các tham chiếu `InputAction` (`_moveAction`, `_lookAction`, `_crouchAction`, `_sprintAction`, `_throwAction`, `_interactAction`, `_pauseAction`) được cache một lần duy nhất tại `Awake()` / `OnEnable()`.
  - Đọc giá trị 2D liên tục qua phương thức không cấp phát rác `_moveAction.ReadValue<Vector2>()`. Đăng ký và hủy đăng ký sự kiện (`.performed`, `.canceled`) chặt chẽ tại `OnEnable()` và `OnDisable()`.

---

### States and Transitions

Hệ thống quản lý trạng thái ngữ cảnh đầu vào (Input Context States) thông qua máy trạng thái sau:

```
                  ┌─────────────────────────────────┐
                  │         GameUnfocused           │
                  │ (Window lost focus / Alt+Tab)   │
                  └───────────────▲─────────────────┘
                                  │ Focus Gained / Lost
                                  ▼
┌─────────────────────────┐               ┌─────────────────────────┐
│     GameplayActive      │  Pause/Esc    │       MenuPaused        │
│   (Player ActionMap)    ├──────────────►│     (UI ActionMap)      │
│   Cursor: Locked/Hidden │◄──────────────┤   Cursor: Free/Visible  │
└────────────┬────────────┘   Resume/Esc  └─────────────────────────┘
             │
             │ Player Caught / Captured
             ▼
┌─────────────────────────┐
│      PlayerBusted       │
│     (UI ActionMap)      │
│   Input Flush & Locked  │
└─────────────────────────┘
```

#### Bảng Chuyển trạng thái Ngữ cảnh Đầu vào (Input Context State Transition Table)

| Trạng thái Hiện tại | Sự kiện / Điều kiện (Trigger) | Trạng thái Đích | Thao tác Xử lý Hệ thống (Actions) |
|---|---|---|---|
| **GameplayActive** | Người chơi bấm phím `Pause` (Escape / Start) | **MenuPaused** | `PlayerActions.Disable()`; `UIActions.Enable()`; Flush input buffer về $(0,0)$; Hiện và mở khóa con trỏ chuột (`CursorLockMode.None`); Gửi sự kiện `OnGamePaused`. |
| **MenuPaused** | Người chơi bấm `Resume` hoặc phím `Cancel` (Escape / B) | **GameplayActive** | `UIActions.Disable()`; `PlayerActions.Enable()`; Khóa và ẩn con trỏ chuột (`CursorLockMode.Locked`); Gửi sự kiện `OnGameResumed`. |
| **GameplayActive** | Lính gác bắt được người chơi (`OnPlayerCapturedEvent`) | **PlayerBusted** | `PlayerActions.Disable()`; `UIActions.Enable()`; Flush toàn bộ input; Kích hoạt hiển thị màn hình thất bại / tải lại điểm lưu. |
| **Bất kỳ trạng thái** | Ứng dụng mất tiêu điểm (Window Focus Lost / Alt+Tab) | **GameUnfocused** | Tự động kích hoạt chuyển sang **MenuPaused**; Flush toàn bộ trạng thái phím để chống kẹt phím ảo. |
| **GameUnfocused** | Ứng dụng nhận lại tiêu điểm (Window Focus Gained) | **MenuPaused** | Giữ nguyên ở MenuPaused để người chơi chủ động bấm tiếp tục chơi. |

---

### Interactions with Other Systems

| Hệ thống Tương tác | Dữ liệu Cung cấp từ Input System | Dữ liệu / Phản hồi Tiếp nhận |
|---|---|---|
| **Player Third-Person Controller (#11)** | Vector di chuyển thế giới $\vec{d}_{\text{world}}$, Cờ tư thế `isCrouchLatched`, Cờ chạy nhanh `isSprintHeld`. | Báo cáo vận tốc hiện tại $V_{\text{current}}$, Trạng thái trần va chạm để quyết định đứng dậy. |
| **Player Noise (`NoiseEmitter`) (#3)** | Sự kiện bấm ném `OnThrowTriggered`. | Trả về trạng thái chấp thuận/từ chối ném (`THROW_WHILE_MOVING_REJECTED`), tiêu hao túi đạn Burst. |
| **Player Movement & Hide (`HideSpot`) (#5)** | Vector di chuyển $\vec{v}_{\text{input}}$ (kiểm tra lực đẩy thoát nấp $> 0.6$ trong $> 0.1\text{ s}$), Tín hiệu xoay góc nhìn `Look`. | Trạng thái `isInsideHideSpot` để Input System khóa chuyển động tịnh tiến (Pure Pivot lock). |
| **Camera Cinemachine Rig (#20)** | Vector xoay góc nhìn chuẩn hóa (Pitch, Yaw Delta) đã xử lý độ nhạy và đường cong gia tốc. | Cung cấp góc phương vị phẳng (Planar Forward/Right) của Camera để biến đổi tọa độ di chuyển. |
| **Physics & Collision Config (#18)** | Thực hiện các lệnh Linecast/SphereCast kiểm tra độ thoáng trần $\ge 1.8\text{ m}$. | Trả về kết quả va chạm hình học theo đúng layer `E20_layer_manifest` và `QueryTriggerInteraction.Ignore`. |
| **HUD / UI (#14)** | Tín hiệu điều hướng menu (`Navigate`, `Submit`, `Cancel`, `Point`, `Click`). | Yêu cầu chuyển đổi Action Map khi mở các bảng cài đặt, bảng điểm hoặc menu tạm dừng. |

## Formulas

### D1: Chuẩn hóa Vùng chết Xuyên tâm Cần Analog (Radial Deadzone Normalization)

- **Mục đích**: Triệt tiêu hiện tượng trôi cần analog vật lý (Stick Drift) khi cần điều khiển ở vị trí trung hòa, đồng thời đảm bảo độ lớn vector chuyển động tăng mượt mà từ $0.0$ đến $1.0$ mà không bị nhảy bước đột ngột, và đạt cực đại $1.0$ trước khi chạm vào giới hạn cơ học ngoài cùng của tay cầm.
- **Biến số**:
  - $\vec{v}_{\text{raw}} = (x, y)$: Vector 2D thô đọc trực tiếp từ cần analog Gamepad ($x, y \in [-1.0, 1.0]$).
  - $r_{\text{raw}} = |\vec{v}_{\text{raw}}| = \sqrt{x^2 + y^2}$: Độ lệch xuyên tâm thô.
  - $r_{\text{dead\_inner}} = 0.15$ (`analog_deadzone_inner`): Ngưỡng sàn vùng chết trong.
  - $r_{\text{dead\_outer}} = 0.95$ (`analog_deadzone_outer`): Ngưỡng trần vùng chết ngoài.
  - $\vec{v}_{\text{norm}}$: Vector di chuyển chuẩn hóa đầu ra ($|\vec{v}_{\text{norm}}| \in [0.0, 1.0]$).
- **Công thức Toán học**:
  $$\vec{v}_{\text{norm}} = \begin{cases} 
  (0.0, 0.0) & \text{nếu } r_{\text{raw}} \le r_{\text{dead\_inner}} \\
  \frac{\vec{v}_{\text{raw}}}{r_{\text{raw}}} \times 1.0 & \text{nếu } r_{\text{raw}} \ge r_{\text{dead\_outer}} \\
  \frac{\vec{v}_{\text{raw}}}{r_{\text{raw}}} \times \left( \frac{r_{\text{raw}} - r_{\text{dead\_inner}}}{r_{\text{dead\_outer}} - r_{\text{dead\_inner}}} \right) & \text{nếu } r_{\text{dead\_inner}} < r_{\text{raw}} < r_{\text{dead\_outer}}
  \end{cases}$$
- **Ví dụ Tính toán**:
  - *Trường hợp 1 (Trôi cần nhẹ)*: $\vec{v}_{\text{raw}} = (0.08, 0.06) \implies r_{\text{raw}} = \sqrt{0.08^2 + 0.06^2} = 0.10$. Vì $0.10 \le 0.15$, kết quả $\vec{v}_{\text{norm}} = (0.0, 0.0)$ (Triệt tiêu 100% trôi cần).
  - *Trường hợp 2 (Đẩy nhẹ một nửa)*: $\vec{v}_{\text{raw}} = (0.0, 0.55) \implies r_{\text{raw}} = 0.55$. Độ lớn sau điều chỉnh:
    $$r_{\text{scaled}} = \frac{0.55 - 0.15}{0.95 - 0.15} = \frac{0.40}{0.80} = 0.50 \implies \vec{v}_{\text{norm}} = (0.0, 0.50)$$
  - *Trường hợp 3 (Gạt tối đa)*: $\vec{v}_{\text{raw}} = (0.70, 0.70) \implies r_{\text{raw}} \approx 0.9899$. Vì $0.9899 \ge 0.95$, kết quả $\vec{v}_{\text{norm}} = \frac{(0.70, 0.70)}{0.9899} \times 1.0 \approx (0.7071, 0.7071)$ với độ lớn đạt tuyệt đối $1.0$.

---

### D2: Vị từ Đệm Lệnh Ném & Bẻ Vận tốc Dừng (Throw Input Buffer & Velocity Snap Predicate)

- **Mục đích**: Khử triệt để hiện tượng "nuốt nút ném" khi người chơi đang di chuyển, quyết định ném đạn Burst, nhả tay khỏi phím WASD / cần analog và bấm ném ngay lập tức trong quá trình nhân vật đang trôi giảm tốc quán tính.
- **Biến số**:
  - $t_{\text{throw\_press}}$: Thời điểm người chơi nhấn nút ném (LMB hoặc Gamepad RT) tính theo đồng hồ thời gian thực của game ($s$).
  - $t_{\text{stick\_neutral}}$: Thời điểm gần nhất mà vector di chuyển $\vec{v}_{\text{norm}}$ rơi về $(0.0, 0.0)$ ($s$).
  - $\Delta t_{\text{release}} = t_{\text{throw\_press}} - t_{\text{stick\_neutral}}$: Khoảng thời gian từ lúc nhả cần đến lúc bấm ném ($s$).
  - $t_{\text{buffer\_window}} = 0.150\text{ s}$ (`input_buffer_window_s` từ `entities.yaml :1022`).
  - $V_{\text{ground}}$: Tốc độ mặt đất hiện tại của nhân vật ($\text{m/s}$).
  - $V_{\text{snap\_thresh}} = 1.0\text{ m/s}$ (`throw_velocity_snap_threshold` từ `entities.yaml :1029`).
  - $H_{\text{clearance}}$: Khoảng cách thẳng đứng từ mặt đất lên trần đo được qua SphereCast ($m$).
  - $H_{\text{req}} = 1.8\text{ m}$: Chiều cao trần tối thiểu yêu cầu cho tư thế ném đứng.
- **Công thức Vị từ**:
  $$\text{ThrowExecution}(t_{\text{throw\_press}}) = \begin{cases}
  \text{ADMIT\_AND\_SNAP} & \text{nếu } (\Delta t_{\text{release}} \le t_{\text{buffer\_window}}) \land (V_{\text{ground}} < V_{\text{snap\_thresh}}) \land (H_{\text{clearance}} \ge H_{\text{req}}) \\
  \text{ADMIT\_STATIONARY} & \text{nếu } (V_{\text{ground}} == 0.0) \land (H_{\text{clearance}} \ge H_{\text{req}}) \\
  \text{REJECT\_MOVING} & \text{nếu } (V_{\text{ground}} \ge V_{\text{snap\_thresh}}) \\
  \text{REJECT\_CLEARANCE} & \text{nếu } (H_{\text{clearance}} < H_{\text{req}})
  \end{cases}$$
- **Hành động Hệ thống**:
  - Khi $\text{ADMIT\_AND\_SNAP}$ kích hoạt: Đặt ngay lập tức vận tốc mặt đất của nhân vật về $0$:
    $$\vec{V}_{\text{player}} \leftarrow (0.0, 0.0, 0.0)$$
    Nếu nhân vật đang ngồi: Lập tức chuyển trạng thái sang Standing và bắt đầu phóng đạn Burst trong cùng tick $t_{\text{throw\_press}}$.
  - Khi $\text{REJECT\_MOVING}$: Hủy bỏ thao tác, ghi nhận mã `THROW_WHILE_MOVING_REJECTED`, túi đạn Burst không bị tiêu hao.

---

### D3: Tỉ lệ Độ nhạy Camera & Đường cong Phi tuyến Cần Xoay (Camera Look Sensitivity & Exponential Response Curve)

- **Mục đích**: Mang lại trải nghiệm xoay góc nhìn mượt mà: phản hồi 1:1 chính xác cho chuột trên PC, và đường cong lũy thừa phi tuyến cho cần analog tay cầm để hỗ trợ quan sát vi mô ở góc hẹp mà không hy sinh tốc độ quay nhanh $180^\circ$.
- **Biến số**:
  - $\Delta \vec{p}_{\text{mouse}} = (\Delta x, \Delta y)$: Tín hiệu dịch chuyển chuột theo pixel (Mouse Delta).
  - $S_{\text{mouse}} = 1.0$ (`mouse_sensitivity_multiplier` $\in [0.1, 3.0]$): Hệ số độ nhạy chuột do người chơi tùy chỉnh.
  - $\vec{v}_{\text{look\_stick}} = (x, y)$: Vector cần xoay phải của Gamepad sau khi qua hàm Deadzone $D1$.
  - $r_{\text{look}} = |\vec{v}_{\text{look\_stick}}| \in [0.0, 1.0]$: Độ lệch cần ngắm.
  - $\gamma = 1.5$ (`look_response_exponent` $\in [1.2, 2.0]$): Số mũ phản hồi phi tuyến của cần xoay.
  - $S_{\text{stick}} = 120.0^\circ/\text{s}$ (`gamepad_look_speed_deg_per_s`): Vận tốc xoay tối đa của camera.
  - $\Delta t$: Thời gian delta frame của game ($s$).
- **Công thức Toán học**:
  - **Với Chuột PC (Tuyến tính 1:1)**:
    $$\Delta \vec{\theta}_{\text{mouse}} = \Delta \vec{p}_{\text{mouse}} \times S_{\text{mouse}} \times 0.1^\circ$$
  - **Với Cần Analog Gamepad (Đường cong Lũy thừa Phi tuyến)**:
    $$M_{\text{curved}} = (r_{\text{look}})^\gamma$$
    $$\Delta \vec{\theta}_{\text{gamepad}} = \left( \frac{\vec{v}_{\text{look\_stick}}}{r_{\text{look}}} \times M_{\text{curved}} \right) \times S_{\text{stick}} \times \Delta t \quad (\text{với } r_{\text{look}} > 0)$$
- **Ví dụ Tính toán**:
  - *Quan sát vi mô ($r_{\text{look}} = 0.2$)*: $M_{\text{curved}} = (0.2)^{1.5} \approx 0.0894$. Tốc độ xoay chỉ bằng $8.9\%$ tốc độ tối đa $\approx 10.7^\circ/\text{s}$, cho phép người chơi căn chỉnh khe cửa rất mịn màng.
  - *Quan sát thông thường ($r_{\text{look}} = 0.6$)*: $M_{\text{curved}} = (0.6)^{1.5} \approx 0.4648$. Tốc độ xoay đạt $46.5\% \approx 55.8^\circ/\text{s}$.
  - *Gạt hết cần ($r_{\text{look}} = 1.0$)*: $M_{\text{curved}} = (1.0)^{1.5} = 1.0$. Đạt $100\%$ tốc độ xoay tối đa $120.0^\circ/\text{s}$, quay người $180^\circ$ trong đúng $1.5\text{ s}$.

---

### D4: Hệ số Co giãn Độ phân giải Con trỏ Chuột WebGL (WebGL DPI & Viewport Scaling Factor)

- **Mục đích**: Bù trừ sai lệch tọa độ chuột khi game chạy trong iframe hoặc canvas trình duyệt WebGL có tỷ lệ điểm ảnh vật lý (Device Pixel Ratio) khác $1.0$.
- **Biến số**:
  - $\Delta \vec{p}_{\text{raw}}$: Mouse Delta thô nhận từ trình duyệt.
  - $\text{DPR}$: `window.devicePixelRatio` (thường là $1.0$ trên màn tiêu chuẩn, $1.25$–$2.0$ trên màn hình Retina / High-DPI).
  - $W_{\text{canvas}}, H_{\text{canvas}}$: Độ phân giải pixel hiển thị thực tế của thẻ HTML `<canvas>`.
  - $W_{\text{render}}, H_{\text{render}}$: Độ phân giải nội bộ Unity WebGL được render ($1920 \times 1080$).
- **Công thức Điều chỉnh**:
  $$\Delta \vec{p}_{\text{calibrated}} = \Delta \vec{p}_{\text{raw}} \times \left( \frac{W_{\text{render}}}{W_{\text{canvas}} \times \text{DPR}} \right)$$
  Đảm bảo tốc độ rê chuột trên bản WebGL và bản PC Windows hoàn toàn tương đồng về mặt cảm nhận vật lý.

## Edge Cases

- **E1: Ngắt kết nối Tay cầm Đột ngột Giữa Trận (Gamepad Disconnection mid-gameplay)**:
  - *Tình huống*: Người chơi đang điều khiển bằng Gamepad thì bị tuột cáp USB hoặc tay cầm hết pin đột ngột khi đang di chuyển gần lính gác.
  - *Xử lý*:
    1. Unity Input System bắt sự kiện `InputSystem.onDeviceChange` với trạng thái `InputDeviceChange.Disconnected` hoặc `Removed`.
    2. Lập tức bẻ vector di chuyển $\vec{v}_{\text{norm}}$ về $(0, 0)$ và dừng nhân vật trong cùng frame.
    3. Tự động kích hoạt chuyển trạng thái sang **`MenuPaused`**, vô hiệu hóa Action Map `Player`, kích hoạt Action Map `UI`.
    4. Hiển thị bảng thông báo nổi bật trên màn hình: *"Đã mất kết nối tay cầm. Vui lòng kết nối lại hoặc nhấn phím bất kỳ trên Bàn phím/Chuột để tiếp tục"*.
    5. Đảm bảo nhân vật không bị trôi tự do vào vùng nhìn của lính gác khi người chơi đang xử lý sự cố phần cứng.

- **E2: Chuyển đổi Nóng Thiết bị Thời gian thực (Seamless Keyboard/Mouse $\leftrightarrow$ Gamepad Hot-Swapping)**:
  - *Tình huống*: Người chơi đang dùng bàn phím WASD bỗng chuyển sang cầm Gamepad điều khiển (hoặc ngược lại).
  - *Xử lý*:
    - Hệ thống lắng nghe tín hiệu đầu vào từ cơ chế `PlayerInput.onControlsChanged`.
    - Ngay khi nhận được tín hiệu hợp lệ (vượt qua ngưỡng Deadzone $0.15$ đối với Gamepad hoặc bất kỳ phím bấm bàn phím nào), hệ thống tự động cập nhật sơ đồ thiết bị đang hoạt động (`currentControlScheme`).
    - Gửi sự kiện `OnControlSchemeChanged` để toàn bộ hệ thống HUD lập tức chuyển đổi biểu tượng nút bấm hướng dẫn (Button Prompts) từ ký tự bàn phím (ví dụ: `[E] Nhặt đồ`, `[C] Ngồi`) sang biểu tượng nút bấm tay cầm (ví dụ: `[X] Nhặt đồ`, `[B] Ngồi`) mà không làm khựng game dù chỉ 1 frame.

- **E3: Mất Tiêu điểm Ứng dụng & Chuyển Tab Trình duyệt (Window Focus Lost / Alt+Tab / WebGL Blur)**:
  - *Tình huống*: Người chơi đang đè phím Shift + W để chạy nhanh thì nhấn Alt+Tab ra ngoài hoặc nhấp chuột sang màn hình thứ hai (trên PC), hoặc chuyển tab trình duyệt (trên WebGL).
  - *Xử lý*:
    - Bắt sự kiện `OnApplicationFocus(false)` hoặc `OnApplicationPause(true)`.
    - Thực hiện xóa sạch toàn bộ bộ đệm phím (`Input State Flush`): giải phóng toàn bộ phím đang giữ (`isSprintHeld = false`, `isCrouchLatched` giữ nguyên theo trạng thái chốt), ép vector di chuyển về $(0, 0)$.
    - Tự động chuyển game sang trạng thái **`MenuPaused`**.
    - Triệt tiêu 100% lỗi "kẹt phím ảo" (ghost input) thường gặp khi hệ điều hành không gửi sự kiện KeyUp lúc cửa sổ mất tiêu điểm. Khi người chơi Alt+Tab quay lại, nhân vật đứng yên an toàn thay vì tự động chạy đâm vào lính.

- **E4: Trình duyệt Mất Khóa Chuột Ngoài Ý muốn (WebGL Pointer Lock Lost)**:
  - *Tình huống*: Người chơi trên bản WebGL bấm phím thoát của trình duyệt hoặc nhấn tổ hợp phím hệ thống khiến trình duyệt tự động nhả quyền Pointer Lock của thẻ `<canvas>`.
  - *Xử lý*:
    - Bắt sự kiện DOM `pointerlockchange` khi `document.pointerLockElement == null`.
    - Hệ thống lập tức chuyển sang **`MenuPaused`**, giải phóng con trỏ chuột tự do hiển thị trên màn hình.
    - Hiển thị lớp phủ thông báo: *"Trò chơi đã tạm dừng do mất khóa chuột. Nhấp chuột vào màn hình để tiếp tục chơi"*.
    - Khi người chơi nhấp chuột lại vào khung hình canvas, gọi lại `RequestPointerLock()` và chuyển lại sang `GameplayActive`.

- **E5: Nhấn Ném Liên tục Siêu Nhanh & Hết Đạn (Rapid Throw Spam & Empty Ammo)**:
  - *Tình huống*: Người chơi hoảng loạn spam liên tục chuột trái hoặc nút RT khi đang chạy trốn lính.
  - *Xử lý*:
    - Mỗi hành động ném yêu cầu một khoảng hồi chiêu tối thiểu $t_{\text{throw\_cooldown}} = 0.5\text{ s}$ tương ứng với độ dài hoạt ảnh vung tay ném đạn.
    - Trong thời gian $0.5\text{ s}$ này, mọi thao tác bấm ném tiếp theo bị bộ đệm từ chối và ghi nhận mã `THROW_COOLDOWN_ACTIVE`.
    - Nếu số lượng đạn Burst trong túi đồ $= 0$: Thao tác ném bị từ chối ngay lập tức với mã `THROW_NO_AMMO_REJECTED`, đồng thời HUD kích hoạt hiệu ứng rung đỏ ô trang bị trống để phản hồi cho người chơi.

- **E6: Thao tác Ném khi đang Ẩn nấp trong Điểm Nấp (Throw Input while inside HideSpot)**:
  - *Tình huống*: Người chơi đang nấp trong tủ đồ kín (`HideSpot`) và bấm nút ném LMB/RT để tìm cách đánh lạc hướng lính bên ngoài.
  - *Xử lý*:
    - Hệ thống kiểm tra cờ `isInsideHideSpot == true` từ `PlayerMovementHideSystem`.
    - Vì quy tắc hình học không gian cấm ném xuyên qua cửa tủ đóng kín, thao tác ném bị từ chối triệt để với mã `THROW_INSIDE_HIDESPOT_REJECTED`.
    - Không tiêu hao đạn, không kích hoạt hoạt ảnh, và không làm hỏng trạng thái ẩn nấp hoàn hảo của người chơi.

- **E7: Nhấn Đồng thời các Phím Di chuyển Trái ngược (Opposing Direction Inputs: A+D, W+S)**:
  - *Tình huống*: Người chơi nhấn đồng thời cả 2 phím ngược chiều A và D, hoặc W và S.
  - *Xử lý*:
    - Bộ tổng hợp 2D Vector Composite của Unity New Input System tự động thực hiện phép cộng đại số đối xứng:
      $$\text{input}_x = (-1.0) + (1.0) = 0.0, \quad \text{input}_y = (1.0) + (-1.0) = 0.0$$
    - Vector di chuyển trả về đúng $(0, 0)$. Hệ thống không thiên vị bất kỳ phím nào được nhấn trước hay sau. Nhân vật đứng yên tuyệt đối, không có hiện tượng rung lắc hay giật hình (jitter).

- **E8: Cắm Nhiều Tay cầm Cùng Lúc (Multiple Gamepads Connected)**:
  - *Tình huống*: Máy tính cắm đồng thời 2 tay cầm Gamepad (hoặc có tay cầm ảo từ phần mềm vJoy/DS4Windows).
  - *Xử lý*:
    - Hệ thống cấu hình `PlayerInput` để ghép đôi cố định với thiết bị đầu tiên gửi tín hiệu hợp lệ (User Pairing).
    - Các tín hiệu nhiễu từ tay cầm thứ hai bị bỏ qua hoàn toàn, tránh hiện tượng xung đột vector di chuyển giữa hai thiết bị.

## Dependencies

### Upstream Dependencies (Phụ thuộc Cấp trên)

1. **Unity New Input System (`com.unity.inputsystem` v1.7.0+)**:
   - Gói nền tảng của engine cung cấp tầng cứng xử lý thiết bị (Keyboard, Mouse, Gamepad), tài nguyên `InputActionAsset`, các bộ xử lý xử lý composite và sự kiện `onDeviceChange`.
2. **Physics & Collision Config (#18)**:
   - Cung cấp quy tắc truy vấn hình học `E20_layer_manifest` và bắt buộc gắn cờ `QueryTriggerInteraction.Ignore` cho các phép đo độ thoáng trần đứng thẳng ($H_{\text{clearance}} \ge 1.8\text{ m}$) khi thực hiện đứng dậy và ném đạn.

---

### Downstream Dependents (Các Hệ thống Tiêu thụ)

1. **Player Third-Person Controller (#11)**:
   - Tiêu thụ `IPlayerInputProvider`: Nhận vector di chuyển phẳng thế giới $\vec{d}_{\text{world}}$, cờ chốt ngồi `isCrouchLatched`, cờ chạy nhanh `isSprintHeld`.
   - Cung cấp ngược lại vận tốc mặt đất hiện tại $V_{\text{ground}}$ để phục vụ cho vị từ đệm lệnh ném $D2$.
2. **Cinemachine Camera Rig (#20)**:
   - Tiêu thụ vector xoay góc nhìn $\Delta \vec{\theta}$ (Pitch, Yaw) đã chuẩn hóa và áp dụng đường cong phản hồi phi tuyến $D3$.
   - Cung cấp góc phương vị phẳng (Planar Forward/Right) của Camera để Input System tính toán vector di chuyển camera-relative.
3. **Player Noise (`NoiseEmitter`) (#3)**:
   - Tiếp nhận sự kiện bấm ném `OnThrowPressed` từ Input System.
   - Trả về mã chấp thuận hoặc từ chối (`THROW_WHILE_MOVING_REJECTED`) dựa trên cửa sổ đệm $150\text{ ms}$ (`input_buffer_window_s`) và ngưỡng bẻ vận tốc $1.0\text{ m/s}$ (`throw_velocity_snap_threshold`).
4. **Player Movement & Hide (`HideSpot`) (#5)**:
   - Input System cung cấp ngưỡng lực đẩy cần di chuyển ($> 0.6$ trong thời gian $> 0.1\text{ s}$) để kích hoạt thoát khỏi tủ đồ/gầm bàn.
   - Nhận cờ `isInsideHideSpot == true` để thực thi chế độ khóa chuyển động xoay thuần túy (Pure Pivot Isolation) $\Delta \vec{p} == \vec{0}$.
5. **HUD / UI (#14)**:
   - Tiếp nhận sự kiện `OnControlSchemeChanged` để tự động đổi biểu tượng gợi ý phím bấm trên màn hình (Keyboard Prompts $\leftrightarrow$ Gamepad Prompts).
   - Nhận tín hiệu điều hướng menu khi Action Map `UI` kích hoạt.
6. **Event / Messaging Bus (#15)**:
   - Đóng vai trò cầu nối phát tín hiệu: `OnGamePausedEvent`, `OnGameResumedEvent`, `OnControlSchemeChangedEvent`.

---

### Interface Contract: `IPlayerInputProvider`

Để đảm bảo nguyên tắc kiến trúc tách biệt (Decoupled Architecture) và phục vụ viết kiểm thử đơn vị tự động (Unit Test / Mocking) mà không phụ thuộc vào thiết bị phần cứng, Input System giao tiếp với Gameplay qua interface:

```csharp
public interface IPlayerInputProvider
{
    /// <summary>Vector di chuyển chuẩn hóa trong hệ tọa độ Camera [-1, 1]</summary>
    Vector2 MoveInput { get; }

    /// <summary>Góc xoay camera (Pitch, Yaw Delta) đã xử lý độ nhạy</summary>
    Vector2 LookDelta { get; }

    /// <summary>Trạng thái chốt lật tư thế ngồi</summary>
    bool IsCrouchLatched { get; }

    /// <summary>Trạng thái đè giữ nút chạy nhanh</summary>
    bool IsSprintHeld { get; }

    /// <summary>Sự kiện kích hoạt ném Burst (có qua bộ đệm D2)</summary>
    event Action OnThrowTriggered;

    /// <summary>Sự kiện kích hoạt nhặt đồ tương tác</summary>
    event Action OnInteractTriggered;

    /// <summary>Sự kiện chuyển đổi thiết bị điều khiển</summary>
    event Action<ControlSchemeType> OnControlSchemeChanged;

    /// <summary>Bật/tắt Action Map Player</summary>
    void SetPlayerInputActive(bool active);
}
```

---

### Bảng Đối chiếu Ma trận Phụ thuộc 2 Chiều (Bidirectional Dependency Matrix)

| Hệ thống A | Hệ thống B | Hướng Dữ liệu | Dữ liệu / Hợp đồng Trao đổi | Ràng buộc Không được Vi phạm |
|---|---|---|---|---|
| **Input System (#19)** | **Player Controller (#11)** | $19 \to 11$ | Vector di chuyển, Cờ Crouch/Sprint, Ném, Tương tác qua `IPlayerInputProvider`. | Controller không bao giờ đọc trực tiếp từ phần cứng Unity Input. |
| **Player Controller (#11)** | **Input System (#19)** | $11 \to 19$ | Vận tốc $V_{\text{ground}}$, Trạng thái va chạm trần $H_{\text{clearance}}$. | Input System không bao giờ can thiệp vật lý CharacterController trừ bẻ vận tốc dừng ném D2. |
| **Input System (#19)** | **Camera Rig (#20)** | $19 \to 20$ | Vector góc xoay $\Delta \vec{\theta}$ đã áp dụng Deadzone & Curve D3. | Camera không tự đọc chuột/cần analog. |
| **Camera Rig (#20)** | **Input System (#19)** | $20 \to 19$ | Vector phẳng Forward/Right của Camera. | Input System dùng góc Yaw phẳng, triệt tiêu trục Y để không bay lên trời. |
| **Input System (#19)** | **HideSpot (#5)** | $19 \to 5$ | Tín hiệu thoát nấp (đẩy hướng thoát $> 0.6$ trong $> 0.1\text{ s}$). | Không cho phép thoát nấp chỉ bằng cú gạt nhẹ cần điều khiển. |
| **HideSpot (#5)** | **Input System (#19)** | $5 \to 19$ | Cờ `isInsideHideSpot`. | Khóa tuyệt đối chuyển vị tịnh tiến ($\Delta \vec{p} == \vec{0}$) trong điểm nấp. |
| **Input System (#19)** | **Physics Config (#18)** | $19 \to 18$ | SphereCast kiểm tra trần $\ge 1.8\text{ m}$. | Bắt buộc sử dụng `E20_layer_manifest` và `QueryTriggerInteraction.Ignore`. |

## Tuning Knobs

Toàn bộ các thông số của Input System được quản lý theo mô hình phân cấp:
1. **Nhóm Thông số Logic Gameplay (Designer Knobs)**: Được đóng gói trong `InputConfigSO` (ScriptableObject) dành riêng cho nhóm thiết kế hệ thống cân chỉnh độ nhạy của các cơ chế trò chơi.
2. **Nhóm Thông số Cảm giác & Trợ năng (Player Settings)**: Được lưu trong `PlayerPrefs` và phơi ra giao diện Menu Cài đặt (Options Menu) để người chơi tự do tinh chỉnh theo thói quen phần cứng cá nhân.

### 1. Nhóm Thông số Logic Gameplay (`InputConfigSO`)

| Tên Thông số | Giá trị Mặc định | Miền An toàn | Đơn vị | Tác động Gameplay & Cơ sở Cân chỉnh |
|---|---|---|---|---|
| `input_buffer_window_s` | `0.150` | `[0.050, 0.300]` | giây | Thời gian đệm lệnh ném sau khi nhả cần di chuyển. Quá nhỏ ($< 0.05\text{ s}$) sẽ gây nuốt nút khi thao tác nhanh; quá lớn ($> 0.3\text{ s}$) tạo cảm giác trễ và mất kiểm soát. |
| `throw_velocity_snap_threshold` | `1.0` | `[0.5, 2.0]` | m/s | Ngưỡng vận tốc mặt đất tối đa cho phép bẻ dừng để thi hành ném. Bằng đúng khoảng giữa tốc độ bò ($1.8\text{ m/s}$) và đứng yên ($0.0\text{ m/s}$). |
| `throw_cooldown_s` | `0.50` | `[0.30, 1.00]` | giây | Thời gian hồi chiêu tối thiểu giữa 2 lần ném liên tiếp, khớp với độ dài hoạt ảnh vung tay của nhân vật để ngăn chặn spam đạn Burst. |
| `hidespot_exit_threshold` | `0.60` | `[0.40, 0.90]` | tỉ lệ | Độ lệch tối thiểu của cần di chuyển hướng ra cửa để kích hoạt bước ra khỏi điểm ẩn nấp, chống việc vô tình rời tủ khi va quẹt nhẹ vào cần. |
| `hidespot_exit_sustain_time` | `0.10` | `[0.05, 0.30]` | giây | Thời gian người chơi phải giữ liên tục lực đẩy về hướng thoát để xác nhận ý định rời khỏi điểm ẩn nấp. |
| `uncrouch_clearance_height` | `1.80` | `[1.60, 2.20]` | mét | Chiều cao tĩnh tối thiểu cần thiết để cho phép đứng dậy từ tư thế ngồi hoặc thi hành ném đạn Burst. |
| `uncrouch_sphere_radius` | `0.20` | `[0.10, 0.30]` | mét | Bán kính hình cầu SphereCast kiểm tra va chạm trần khi đứng dậy để tránh kẹt đỉnh đầu vào xà ngang. |

---

### 2. Nhóm Thông số Cảm giác & Trợ năng (Player Settings / Options Menu)

| Tên Thông số | Giá trị Mặc định | Miền An toàn | Đơn vị | Vị trí Giao diện & Tác động Trải nghiệm |
|---|---|---|---|---|
| `analog_deadzone_inner` | `0.15` | `[0.05, 0.35]` | tỉ lệ | Thanh trượt Slider trong Menu Cài đặt Tay cầm. Cho phép người chơi tăng vùng chết nếu tay cầm cá nhân bị trôi cần vật lý (Stick Drift). |
| `analog_deadzone_outer` | `0.95` | `[0.85, 0.99]` | tỉ lệ | Ngưỡng đạt tốc độ tối đa của cần analog. Hỗ trợ các tay cầm có vòng giới hạn cơ học bị mòn. |
| `mouse_sensitivity_multiplier` | `1.00` | `[0.10, 3.00]` | hệ số | Thanh trượt Slider trong Menu Cài đặt Chuột. Điều chỉnh tỉ lệ quay camera khi di chuột trên PC và WebGL. |
| `gamepad_look_speed_deg_per_s` | `120.0` | `[60.0, 240.0]` | độ/giây | Tốc độ xoay camera cực đại khi gạt hết cần analog phải. |
| `look_response_exponent` | `1.50` | `[1.00, 2.20]` | số mũ | Độ cong phản hồi của cần xoay. $1.0$ là tuyến tính hoàn toàn; $> 1.0$ tăng độ mịn cho góc ngắm hẹp quanh tâm. |
| `invert_mouse_y` | `false` | `[true, false]` | boolean | Tùy chọn đảo ngược trục Y khi rê chuột (Trợ năng / Thói quen người chơi). |
| `invert_gamepad_y` | `false` | `[true, false]` | boolean | Tùy chọn đảo ngược trục Y của cần ngắm tay cầm. |

## Visual/Audio Requirements

### 1. Yêu cầu Hình ảnh (Visual Requirements)

- **Biểu tượng Nút bấm Động (Dynamic Button Glyphs / Prompts)**:
  - Hệ thống HUD hiển thị các ký hiệu phím bấm trực quan (Glyphs) tương ứng chính xác với thiết bị đang hoạt động:
    - Khi dùng Bàn phím/Chuột: Hiển thị icon phím vuông bo góc với ký tự đậm nét (`[E]`, `[C]`, `[Shift]`, biểu tượng chuột trái `[LMB]`).
    - Khi dùng Gamepad: Tự động đổi sang biểu tượng tay cầm chuẩn Xbox/PlayStation (`[X]`, `[B]`, `[RT]`, `[L3]`).
  - Các biểu tượng phím đổi mượt mà ngay lập tức khi phát hiện sự kiện `OnControlSchemeChanged` mà không gây nhấp nháy giao diện.
- **Phản hồi Trực quan khi Từ chối Thao tác (Action Rejection Feedback)**:
  - Khi người chơi bấm ném đạn Burst nhưng hết đạn trong túi hoặc bị vướng trần ($< 1.8\text{ m}$): Ô trang bị đạn trên HUD chớp viền đỏ nhẹ ($0.2\text{ s}$) kèm rung lắc nhẹ (horizontal shake $4\text{ px}$) để người chơi nhận thức rõ lệnh bị từ chối do quy tắc không gian/tài nguyên chứ không phải game bị đơ nút.
- **Lớp phủ Khóa Chuột WebGL (WebGL Pointer Lock Overlay)**:
  - Khi chạy trên WebGL và trình duyệt chưa khóa chuột: Hiển thị một màn hình bán trong suốt mờ đen ($70\%$ opacity) với văn bản hướng dẫn nổi bật ở tâm: *"Nhấp chuột vào màn hình để bắt đầu chơi / Click to Play"*. Khi người chơi nhấp chuột thành công, lớp phủ mờ dần (fade-out $0.2\text{ s}$) và trao quyền điều khiển cho Action Map `Player`.

---

### 2. Yêu cầu Âm thanh (Audio Requirements)

- **Âm thanh Giao diện (UI Feedback SFX)**:
  - `sfx_ui_pause`: Âm thanh click cơ khí đanh gọn, không gian kín khi người chơi nhấn Escape mở menu tạm dừng ($45\text{ dB}$, non-diegetic).
  - `sfx_ui_resume`: Âm thanh lẫy khóa đóng lại khi tiếp tục trận đấu.
  - `sfx_action_denied`: Âm thanh trầm đục "thump" nhẹ khi bấm ném lúc hết đạn hoặc bị cản trần ($35\text{ dB}$, non-diegetic), tạo tín hiệu phản hồi xúc giác thính giác rõ ràng.
- **Quy tắc Tuyệt đối Không Âm thanh (Silence Constraint)**:
  - Toàn bộ quá trình tiếp nhận và xử lý tín hiệu di chuyển, xoay góc nhìn, chốt lật ngồi tuyệt đối không tự phát ra bất kỳ âm thanh giả lập nào từ tầng Input. Toàn bộ âm thanh bước chân ($6\text{ dB}$, $12\text{ dB}$) hay tiếng động sột soạt hoàn toàn do hệ thống `PlayerNoiseSystem` và hoạt ảnh nhân vật làm chủ.

---

## UI Requirements

### 1. Bảng Hiển thị Sơ đồ Phím Cố định (Fixed Keymap Reference - MVP)

Trong Menu Cài đặt (Options Menu) -> Tab **Điều khiển (Controls)**, cung cấp bảng đồ họa trực quan mô tả chi tiết chức năng của từng phím bấm cố định cho cả hai sơ đồ:

```
┌────────────────────────────────────────────────────────────────────────┐
│                      SƠ ĐỒ ĐIỀU KHIỂN (CONTROLS)                       │
├──────────────────────────────────┬─────────────────────────────────────┤
│ BÀN PHÍM & CHUỘT (PC / WEBGL)    │ TAY CẦM GAMEPAD (XBOX / PLAYSTATION)│
├──────────────────────────────────┼─────────────────────────────────────┤
│ W, A, S, D  : Di chuyển mặt đất   │ Cần trái (L-Stick) : Di chuyển      │
│ Chuột (Mouse): Xoay góc nhìn      │ Cần phải (R-Stick) : Xoay camera    │
│ C           : Chốt Ngồi / Đứng   │ Nút B / Circle     : Chốt Ngồi/Đứng │
│ Left Shift  : Giữ để Chạy nhanh  │ L3 (Nhấn cần trái) : Giữ để Chạy    │
│ Chuột Trái  : Ném đạn Burst      │ Cò phải (RT / R2)  : Ném đạn Burst  │
│ E           : Nhặt lại đạn Burst │ Nút X / Square     : Nhặt lại đạn   │
│ Escape      : Tạm dừng / Menu    │ Nút Start / Menu   : Tạm dừng / Menu│
└──────────────────────────────────┴─────────────────────────────────────┘
```

### 2. Bảng Tùy chọn Tinh chỉnh Điều khiển (Input Settings Menu)

Cung cấp các thành phần điều khiển giao diện chuẩn cho phép người chơi cá nhân hóa:
- **Thanh trượt Độ nhạy Chuột (Mouse Sensitivity Slider)**: Giá trị thực từ $0.1$ đến $3.0$, bước nhảy $0.05$, mặc định $1.0$.
- **Thanh trượt Tốc độ Xoay Tay cầm (Gamepad Look Speed Slider)**: Giá trị từ $60^\circ/\text{s}$ đến $240^\circ/\text{s}$, bước nhảy $5^\circ$, mặc định $120^\circ/\text{s}$.
- **Thanh trượt Vùng chết Tay cầm (Inner Deadzone Slider)**: Giá trị từ $0.05$ đến $0.35$, bước nhảy $0.01$, mặc định $0.15$. Kèm hình ảnh trực quan hiển thị vị trí thực của cần analog trong vòng tròn để người chơi kiểm tra độ trôi cần.
- **Nút bật/tắt Đảo trục Y Chuột (Invert Mouse Y Toggle)**: Checkbox `[x]`, mặc định `false`.
- **Nút bật/tắt Đảo trục Y Tay cầm (Invert Gamepad Y Toggle)**: Checkbox `[x]`, mặc định `false`.
- **Nút "Khôi phục Mặc định" (Reset to Defaults)**: Đặt lại toàn bộ các giá trị về mặc định ban đầu.

## Acceptance Criteria

Toàn bộ các tiêu chí nghiệm thu dưới đây phải được kiểm thử độc lập thông qua NUnit Test Framework (đối với logic dữ liệu, thuật toán chuẩn hóa, vị từ đệm lệnh) hoặc kịch bản kiểm thử tích hợp (đối với phần cứng, WebGL và UI):

### 1. Phân cấp Action Map & Chuyển trạng thái

- **AC-INP-01 (Tính Độc quyền Action Map)**:
  - *Điều kiện*: Game đang ở trạng thái `GameplayActive` với Action Map `Player` bật và `UI` tắt. Người chơi kích hoạt phím `Pause` (Escape / Gamepad Start).
  - *Kết quả bắt buộc*:
    1. Trong đúng $1$ frame, `PlayerActions.enabled == false` và `UIActions.enabled == true`.
    2. Vector di chuyển đọc từ `IPlayerInputProvider.MoveInput` trả về chính xác $(0.0, 0.0)$.
    3. Trạng thái con trỏ chuột chuyển sang `CursorLockMode.None` và `Cursor.visible == true`.
    4. Khi tiếp tục game (`Resume`), các trạng thái đảo ngược chính xác về ban đầu.

- **AC-INP-02 (Mất Tiêu điểm & Alt+Tab Input Flush)**:
  - *Điều kiện*: Người chơi đang đè phím Shift + W và chuyển tab hoặc click chuột ra ngoài cửa sổ game (`OnApplicationFocus(false)`).
  - *Kết quả bắt buộc*:
    1. Game tự động chuyển sang `MenuPaused`.
    2. Cờ `isSprintHeld` lập tức chuyển về `false`.
    3. Khi quay lại cửa sổ game (`OnApplicationFocus(true)`), nhân vật đứng yên tại chỗ, không xảy ra hiện tượng tự động chạy hay kẹt phím ảo.

---

### 2. Chuẩn hóa Tín hiệu & Vùng chết Cần Analog

- **AC-INP-03 (Khử Trôi cần Vùng chết D1)**:
  - *Điều kiện*: Cung cấp vector đầu vào thô $\vec{v}_{\text{raw}}$ với bán kính $r_{\text{raw}} = |\vec{v}_{\text{raw}}|$.
  - *Kết quả bắt buộc*:
    1. Với mọi $r_{\text{raw}} \le 0.15$: Hàm trả về vector $(0.0, 0.0)$.
    2. Với mọi $r_{\text{raw}} \ge 0.95$: Hàm trả về vector có độ lớn $|\vec{v}_{\text{norm}}| = 1.0000 \pm 10^{-4}$ cùng hướng với $\vec{v}_{\text{raw}}$.
    3. Với $r_{\text{raw}} = 0.55$: Hàm trả về vector có độ lớn bằng đúng $\frac{0.55 - 0.15}{0.95 - 0.15} = 0.5000 \pm 10^{-4}$.
    4. Đồ thị chuyển đổi là đơn điệu tăng liên tục, không có điểm gãy bước nhảy (discontinuity).

- **AC-INP-04 (Đối xứng Triệt tiêu Phím Đối lập E7)**:
  - *Điều kiện*: Kích hoạt đồng thời phím A và D, hoặc W và S trên bàn phím.
  - *Kết quả bắt buộc*: `MoveInput` trả về chính xác $(0.0, 0.0)$, không có bất kỳ frame nào bị lệch trục hay rung lắc.

---

### 3. Tư thế Ngồi & Kiểm tra Vật cản Trần

- **AC-INP-05 (Chốt Lật Tư thế Ngồi C3)**:
  - *Điều kiện*: Nhân vật đang đứng (`isCrouchLatched == false`) ở không gian thoáng ($H_{\text{clearance}} \ge 2.5\text{ m}$).
  - *Kết quả bắt buộc*:
    1. Nhấn phím C một lần $\implies$ `isCrouchLatched` chuyển thành `true`.
    2. Nhả phím C $\implies$ `isCrouchLatched` vẫn giữ nguyên `true` (Persistent Posture).
    3. Nhấn phím C lần hai $\implies$ `isCrouchLatched` chuyển thành `false`.

- **AC-INP-06 (Từ chối Đứng dậy khi Trần Thấp C3)**:
  - *Điều kiện*: Nhân vật đang ngồi trong đường ống hẹp hoặc dưới gầm bàn có trần $H_{\text{clearance}} = 1.2\text{ m} < 1.8\text{ m}$. Người chơi nhấn phím C.
  - *Kết quả bắt buộc*:
    1. Lệnh đứng dậy bị từ chối; `isCrouchLatched` vẫn duy trì `true`.
    2. Không có sự thay đổi chiều cao Capsule Collider của nhân vật.
    3. Gửi thông báo từ chối `UNCROUCH_INSUFFICIENT_CLEARANCE_REJECTED`.

---

### 4. Cơ chế Chạy nhanh (Sprint) & Tự động Đứng

- **AC-INP-07 (Cơ chế Giữ phím Chạy nhanh C4)**:
  - *Điều kiện*: Nhân vật đang đứng và di chuyển với $|\text{MoveInput}| > 0.1$.
  - *Kết quả bắt buộc*:
    1. Đè giữ phím Shift $\implies$ `IsSprintHeld == true`, nhân vật di chuyển với tốc độ chạy nhanh ($V_{\text{sprint}} = 4.0\text{ m/s}$).
    2. Nhả phím Shift $\implies$ `IsSprintHeld == false`, tốc độ nhân vật lập tức hạ về tốc độ đi bộ ($V_{\text{walk}} = 2.4\text{ m/s}$) trong frame kế tiếp.
    3. Đè Shift nhưng thả cần di chuyển về tâm ($|\text{MoveInput}| == 0$) $\implies$ Nhân vật đứng yên tại chỗ ($V = 0.0\text{ m/s}$).

- **AC-INP-08 (Tự động Đứng dậy để Bứt tốc Chạy C4)**:
  - *Điều kiện*: Nhân vật đang ngồi (`isCrouchLatched == true`) dưới trần cao $2.5\text{ m}$. Người chơi đè Shift và đẩy phím W.
  - *Kết quả bắt buộc*:
    1. SphereCast trần xác nhận $H_{\text{clearance}} \ge 1.8\text{ m}$.
    2. Trong cùng frame, `isCrouchLatched` tự động chuyển sang `false` và `IsSprintHeld == true`.
    3. Nhân vật đứng thẳng dậy và tăng tốc chạy nhanh mà không yêu cầu người chơi phải bấm phím C trước đó.
    4. Nếu thực hiện thao tác này dưới gầm bàn trần $1.2\text{ m}$: Lệnh Sprint bị triệt tiêu, nhân vật tiếp tục bò ngồi an toàn.

---

### 5. Bộ đệm Lệnh Ném & Bẻ Vận tốc Dừng

- **AC-INP-09 (Bẻ Vận tốc khi Nhả cần Ném D2)**:
  - *Điều kiện*: Người chơi thả cần di chuyển WASD, vận tốc nhân vật đang trượt giảm tốc còn $V_{\text{ground}} = 0.8\text{ m/s} < 1.0\text{ m/s}$. Người chơi nhấn nút ném (LMB) sau $0.10\text{ s} \le 0.15\text{ s}$ kể từ lúc nhả cần.
  - *Kết quả bắt buộc*:
    1. Vận tốc nhân vật lập tức bị bẻ về $\vec{V}_{\text{player}} = (0.0, 0.0, 0.0)$ trong cùng tick vật lý.
    2. Sự kiện `OnThrowTriggered` được phát ra thành công và đạn Burst được phóng đi.
    3. Không xảy ra hiện tượng nuốt nút ném.

- **AC-INP-10 (Từ chối Ném khi Đang Di chuyển Nhanh D2)**:
  - *Điều kiện*: Người chơi đang chạy với vận tốc $V_{\text{ground}} = 3.5\text{ m/s} \ge 1.0\text{ m/s}$ và bấm nút ném LMB.
  - *Kết quả bắt buộc*:
    1. Lệnh ném bị từ chối ngay lập tức; túi đạn Burst giữ nguyên số lượng.
    2. Nhân vật không bị khựng lại hay ngắt nhịp chạy.
    3. Ghi nhận mã chẩn đoán `THROW_WHILE_MOVING_REJECTED`.

- **AC-INP-11 (Ném khi đang Ngồi Tự động Đứng C5)**:
  - *Điều kiện*: Nhân vật đang ngồi yên ($V_{\text{ground}} = 0.0\text{ m/s}$) dưới trần cao $2.5\text{ m}$. Người chơi bấm ném LMB.
  - *Kết quả bắt buộc*:
    1. `isCrouchLatched` tự động chuyển sang `false` trong cùng frame.
    2. Nhân vật đứng dậy và phóng đạn Burst theo đúng quỹ đạo chuẩn.

---

### 6. Điểm Ẩn nấp (HideSpot) & Tương tác Nhặt đồ

- **AC-INP-12 (Khóa Tịnh tiến Pure Pivot trong HideSpot C6)**:
  - *Điều kiện*: Nhân vật ở trong điểm ẩn nấp (`isInsideHideSpot == true`). Người chơi rê chuột hoặc đẩy cần xoay camera phải.
  - *Kết quả bắt buộc*:
    1. Tín hiệu xoay góc nhìn `LookDelta` vẫn truyền đến Camera Rig bình thường.
    2. Chuyển vị tịnh tiến của nhân vật hoàn toàn bằng không ($\Delta \vec{p} == \vec{0}$).
    3. Cường độ âm thanh phát ra đo được là chính xác $0\text{ dB}$.
    4. Thao tác đẩy cần di chuyển chỉ kích hoạt bước ra ngoài khi đạt độ lớn $> 0.6$ và duy trì liên tục $> 0.1\text{ s}$.

- **AC-INP-13 (Phạm vi Tương tác Nhặt đồ C7)**:
  - *Điều kiện*: Đạn Burst đã ném nằm trên sàn nhà.
  - *Kết quả bắt buộc*:
    1. Khi khoảng cách từ chân người chơi đến điểm đặt $d \le 1.6\text{ m}$ và đường nhìn thông thoáng: Bấm phím E kích hoạt nhặt lại đạn thành công (`OnInteractTriggered`).
    2. Khi khoảng cách $d = 1.65\text{ m} > 1.6\text{ m}$: Bấm phím E không có tác dụng, đạn vẫn nằm trên sàn.

---

### 7. Phần cứng, Đổi thiết bị & Hiệu năng

- **AC-INP-14 (Chuyển đổi Thiết bị Nóng E2 & Tự động Pause khi Mất Tay cầm E1)**:
  - *Điều kiện*: Đang chơi bằng Gamepad, người chơi bấm một phím trên bàn phím.
  - *Kết quả bắt buộc*:
    1. Trong đúng $1$ frame, sự kiện `OnControlSchemeChanged(KeyboardMouse)` được phát ra; HUD đổi toàn bộ icon sang bàn phím.
    2. Ngắt kết nối cáp USB của Gamepad $\implies$ Game tự động chuyển sang `MenuPaused` và hiển thị cảnh báo ngắt kết nối.

- **AC-INP-15 (Hợp đồng Không Phân bổ Rác Bộ nhớ C8)**:
  - *Điều kiện*: Chạy kịch bản profiling đọc liên tục `MoveInput` và `LookDelta` trong $10.000$ frame liên tiếp ở tần số $60\text{ fps}$.
  - *Kết quả bắt buộc*:
    - Tổng lượng bộ nhớ rác cấp phát từ Input System (`GC.GetTotalMemory`) trong suốt $10.000$ frame đo được là **chính xác $0\text{ bytes}$**.

- **AC-INP-16 (Trình duyệt Thoát Khóa Chuột WebGL E4)**:
  - *Điều kiện*: Game đang chạy trên bản WebGL trong trình duyệt, sự kiện DOM `pointerlockchange` kích hoạt với con trỏ bị nhả.
  - *Kết quả bắt buộc*: Game lập tức chuyển sang `MenuPaused` và hiển thị lớp phủ yêu cầu nhấp chuột để tiếp tục.

---

## Open Questions

- **Q1: Có hỗ trợ rung tay cầm (Gamepad Vibration / Haptics) khi bị phát hiện hoặc khi ném đạn không?**
  - *Quyết định*: **Xếp vào Target Tier / Polish**. Giai đoạn MVP chỉ tập trung vào độ chính xác tuyệt đối của phản hồi điều khiển, tránh đưa vào các API rung tay cầm có thể gây lỗi tương thích trên nền tảng WebGL trình duyệt.
- **Q2: Có cho phép người chơi tùy biến gán lại phím (Custom Key Remapping) trong MVP không?**
  - *Quyết định*: **Không trong MVP (Xếp vào Target Tier)**. MVP sử dụng sơ đồ phím cố định chuẩn (Fixed Keymap) và hiển thị bảng đồ họa trực quan trong Menu Settings để tối ưu hóa phạm vi kiểm thử và ổn định code.
- **Q3: Có hỗ trợ điều khiển cảm ứng (Touch Controls) cho màn hình cảm ứng không?**
  - *Quyết định*: **Không (Forbidden per Technical Preferences)**. Game chỉ nhắm tới PC Windows và WebGL Demo chơi bằng Bàn phím/Chuột hoặc Gamepad.
