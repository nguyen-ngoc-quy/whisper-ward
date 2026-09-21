# Suspicion Attribution Telemetry (FSM Trace)

> **Status**: Approved (2026-09-21)
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge), Pillar 3 (Cat-and-Mouse Suspense)

## Overview

Hệ thống **Suspicion Attribution Telemetry (FSM Trace)** (System `#10`) là lớp hạ tầng ghi vết chẩn đoán và quy kết nguyên nhân nghi ngờ (Diagnostic Trace & Attribution Infrastructure) hoạt động ở cấp độ MVP (chế độ dành riêng cho nhà phát triển — dev-only trong MVP), đóng vai trò là "chiếc hộp đen" ghi lại toàn bộ chuỗi nhận thức và quyết định của lính gác (*Guard AI FSM*). Được xây dựng để xác thực **Giả thuyết cốt lõi (Core Hypotheses Claims 1, 2, 3)** trong `game-concept.md`, hệ thống thu thập toàn bộ các sự kiện cảm giác thô (LOS Gain/Break, tiếng động bước chân, va chạm đạn nén Burst) và các quyết định leo thang trạng thái (Investigate Commit, Re-anchor, Chase Entry, Capture) vào một bộ đệm vòng tròn trong bộ nhớ (**In-Memory Ring Buffer** với dung lượng cố định $N_{\text{ring}} = 2048$ sự kiện, zero-GC allocation). Dữ liệu này được xả ra tệp JSON trên bản build PC hoặc sao chép vào Clipboard trên bản build WebGL tại các ranh giới lượt chơi (`Attempt Start / End`) hoặc ngay sau khi người chơi phát biểu nguyên nhân bị bắt giữ.

Về mặt triết lý thiết kế và giá trị vận hành, hệ thống tuân thủ 4 nguyên tắc bất biến:
1. **Minh bạch Tuyệt đối & Cung cấp Chứng cứ Sự thật (Truth Reporter of FSM Decision Inputs)**: Telemetry không suy diễn hay giả lập lại hành vi; hệ thống phản ánh trung thực $100\%$ các yếu tố đầu vào cấu thành quyết định của AI theo chuẩn **Lược đồ Sự kiện R12 (`trace_event_schema`)**. Mọi phát biểu của người chơi về lý do thất bại ("Lính gác nhìn thấy tôi qua cửa sổ phía Đông", "Tôi nán lại trong vùng sáng quá thời gian cửa sổ xác nhận") đều được đối chiếu trực tiếp với chuỗi sự kiện được ghi vết để chứng minh tính công bằng (Fairness & Learnability).
2. **Cách ly Kiểm thử & Không Làm Nhiễm Trải nghiệm (Strict Test-Isolation & Blind Run)**: Trong suốt quá trình người chơi thực hiện lượt thử (run), hệ thống tuyệt đối không hiển thị bất kỳ thông tin quy kết trực tiếp nào lên giao diện màn hình (No live attribution HUD). Thông tin nguyên nhân bắt giữ chỉ được giải mã và hiển thị sau khi người chơi đã tự phát biểu nguyên nhân của mình, ngăn ngừa hiện tượng thiên vị nhận thức (confirmation bias) làm sai lệch kết quả đánh giá Playtest.
3. **Đồng bộ Đồng hồ Ảo & Khả năng Tái hiện Tuyệt đối (Virtual Clock Synchronization & Determinism)**: Toàn bộ các bản ghi sự kiện sử dụng dấu thời gian nhịp ảo cố định (`virtual_timestamp` từ bộ đếm nhịp `H.0.9`), phong bì phiên duy nhất `[session_id, attempt_epoch]`, loại trừ hoàn toàn việc đọc `Time.time` của hệ thống. Điều này bảo đảm hai lần chạy lại (replay) cùng một kịch bản đầu vào sẽ cho ra chuỗi vết bit-identical chính xác $100\%$ (AC-P21).
4. **Không Rò rỉ Dữ liệu & An toàn Hiệu năng (Zero-Allocation & No-Disk Leak)**: Bộ đệm vết được cấp phát tĩnh một lần duy nhất khi khởi tạo màn chơi, ghi đè tuần hoàn theo cơ chế vòng tròn mà không gây giật lag (GC spikes) trên luồng chính. Trong các bản build thử nghiệm Playtest, hệ thống cấm hoàn toàn việc gọi `Debug.Log` nhằm ngăn ngừa việc rò rỉ vết chẩn đoán vào tệp `Player.log` trên ổ đĩa.

## Player Fantasy

Trong *Whisper Ward*, sự căng thẳng tột độ không bắt nguồn từ những yếu tố kinh dị ngẫu nhiên, khó đoán, mà đến từ sự chuẩn xác lạnh lùng của một **cỗ máy biết suy nghĩ** (The Transparent Thinking Machine). Người chơi đang đột nhập vào một viện điều dưỡng tâm thần biệt lập dưới ánh trăng, nơi những người lính gác tuần tra không hề gian lận, không dịch chuyển tức thời (rubber-band) và không suy đoán vu vơ — họ quan sát, tính toán ngưỡng nghi ngờ và hành động theo các quy luật nhận thức minh bạch tuyệt đối.

Khi lưới an ninh siết lại và người chơi bị bắt giữ, trải nghiệm tuyệt đối không bao giờ được phép mang lại cảm giác bất công, mù mờ hay rẻ tiền. Dù ở cấp độ MVP hệ thống Telemetry chỉ hoạt động ngầm (dev-only infrastructure), chuỗi ghi vết FSM bảo đảm luật chơi nền tảng của **Thử thách Trí tuệ Công bằng (Pillar 2 - Fair Mind-Challenge)**: *Mỗi lần bị bắt giữ luôn là hệ quả nhân quả trực tiếp từ chính hành động của người chơi*. Cho dù đó là một cú chạy nước rút bất cẩn trên sàn gạch men, sự chần chừ thêm một tích tắc trong nón ánh sáng đèn pin, hay việc không kịp cắt tầm nhìn (LOS break) trước khi cửa sổ xác nhận khép lại, người chơi luôn thấu hiểu cội nguồn thất bại.

Bằng việc bảo toàn một chuỗi chứng cứ sự thật bất biến phía sau mọi quyết định leo thang báo động, hệ thống Telemetry củng cố niềm tin tuyệt đối của người chơi vào tính trung thực của thế giới game. Thất bại không gây ra sự ức chế hay cảm giác ấm ức với hệ thống, mà biến thành khoảnh khắc thức tỉnh chẩn đoán đầy tính khích lệ — khơi dậy nỗi khao khát mãnh liệt muốn quay lại thử lại ngay lập tức với sự cẩn trọng và chuẩn xác cao hơn: *"Tôi biết chính xác điều gì đã khiến hắn quay đầu lại. Lần tới, tôi sẽ lướt qua khe cửa đó một cách hoàn hảo."*

## Detailed Design

### Core Rules

1. **CR1: Cấu trúc Bộ đệm Vòng tròn Cấp phát Tĩnh (Pre-allocated Static Struct Ring Buffer)**:
   - Hệ thống quản lý một bộ đệm vòng tròn cố định với dung lượng $N_{\text{ring}} = 2048$ phần tử (`FsmTraceRecord` blittable struct, kích thước $\le 128\text{ bytes/record}$).
   - Toàn bộ vùng nhớ bộ đệm ($\approx 256\text{ KiB}$) được cấp phát tĩnh một lần duy nhất (`ArrayPool` hoặc `NativeArray`) khi nạp màn chơi (`SceneLoaded`), cam kết **Zero-GC Allocation** trên luồng chính trong suốt quá trình gameplay.
   - Khi bộ đệm đầy ($N > 2048$), con trỏ ghi tự động xoay vòng tuần hoàn (FIFO overwrite), đè lên các bản ghi cũ nhất trong khi vẫn duy trì chỉ số nhịp toàn cục đơn điệu tăng (`global_sequence_id`).

2. **CR2: Hợp đồng Dữ liệu Lược đồ Sự kiện R12 Chuẩn hóa (R12 Schema Event Serialization)**:
   - Mỗi bản ghi trong bộ đệm mang phong bì nhận dạng chuẩn bất biến: `[session_id, attempt_epoch, virtual_timestamp, publisher]`.
   - Hệ thống hỗ trợ 10 nhóm sự kiện cốt lõi theo định dạng R12 của `entities.yaml`:
     - **Nhóm Cảm giác Thô (Raw Sensing Facts)**: `LOS gain`, `LOS break` (kèm $R$, `break_type`, `causal_class`), `step_event` (bước chân), `NoisePublished` (nguồn âm thanh thô).
     - **Nhóm Hàng đợi & Tiếp nhận (Perception Admission)**: `Noise heard` (kèm $R_{\text{eff}}$, `residual_at_hearing`, `entry_id`), `Threshold crossing`, `Confirm-window elapsed`.
     - **Nhóm Quyết định FSM (FSM Decisions)**: `Escalation` (Investigate-commit kèm nguyên nhân và vị trí neo), `Noise re-anchor`, `Chase-entry`, `Chase-end`, `Investigate-resolution`, `Capture`.
     - **Nhóm Vòng đời & Khám phá (Lifecycle & Discovery)**: `AttemptBoundary` (Start/End), `pickup-reached` (người chơi tiếp cận đạn nén Burst trong bán kính $R_{\text{reach}} = 1.20\text{ m}$ — neo chứng chỉ Claim 3).

3. **CR3: Giao thức Cách ly Thử nghiệm Kép (Strict Test-Isolation & Blind Post-Capture Protocol)**:
   - Trong suốt thời gian diễn ra lượt thử (active run), hệ thống ở trạng thái **Mù Tuyệt đối (Blind Mode)**: Không hiển thị bất kỳ HUD, Text hay Gizmo nào chỉ điểm nguyên nhân nghi ngờ trực tiếp cho người chơi.
   - Khi xảy ra sự kiện `Capture` (bắt giữ thành công):
     - Màn hình chuyển sang trạng thái tạm dừng kiểm thử (Tester Interview Prompt), khóa toàn bộ quyền truy xuất dữ liệu vết.
     - Người chơi được yêu cầu phát biểu nguyên nhân thất bại bằng lời ("Điều gì đã khiến lính gác phát hiện bạn?").
     - Chỉ sau khi người kiểm thử (Tester/Researcher) nhấn xác nhận đã ghi nhận câu trả lời (`SubmitPlayerStatement()`), hệ thống mới giải mã (Reveal) nguyên nhân thực sự từ sự kiện `Escalation` / `Capture` tương ứng để đối chiếu với câu trả lời.

4. **CR4: Đường ống Xả Vết Đa Nền tảng (Dual Flush Pipeline: JSON on PC / Clipboard on WebGL)**:
   - **Kích hoạt Xả vết (Flush Triggers)**:
     1. Ngay khi kết thúc một lượt thử (`Attempt End` — do Capture hoặc hoàn thành mục tiêu Escape).
     2. Ngay sau khi người chơi nộp phát biểu nguyên nhân bắt giữ (`Post-Statement Flush`).
     3. Khi người chơi chủ động nhấn tổ hợp phím nhà phát triển `F12` hoặc gõ lệnh console `fsm.trace.dump`.
   - **Xử lý theo Nền tảng**:
     - **Bản build PC (Standalone Windows)**: Chuỗi JSON được ghi bất đồng bộ (`FileStream` phi đồng bộ) vào đường dẫn cố định: `Application.persistentDataPath/Telemetry/fsm_trace_[session_id]_epoch[attempt_epoch]_[timestamp].json`.
     - **Bản build WebGL**: Do cơ chế bảo mật Sandbox trình duyệt không hỗ trợ I/O ổ đĩa, chuỗi JSON nén được sao chép trực tiếp vào bộ nhớ tạm hệ thống (`GUIUtility.systemCopyBuffer`), đồng thời phát ra tín hiệu WebGL JS Hook `window.onFsmTraceFlushed(jsonString)` để trang web lưu trữ tự động.
   - **Quy tắc An toàn**: Tuyệt đối không gọi `Debug.Log` trong tester build để ngăn ngừa rò rỉ vết vào tệp `Player.log` trên ổ đĩa.

### States and Transitions

Hệ thống Telemetry vận hành như một Máy trạng thái Vòng đời Ghi vết (Telemetry Lifecycle FSM):

```text
       [Init / Scene Load]
               │
               ▼
        ┌──────────────┐
   ┌───►│    Armed     │◄───────────────────────┐
   │    └──────┬───────┘                        │
   │           │ OnAttemptStart (epoch++)       │
   │           ▼                                │
   │    ┌──────────────┐                        │
   │    │  Recording   │ (Blind Run, Zero-GC)   │
   │    └──────┬───────┘                        │
   │           │                                │
   │           ├───────────────┬────────────────┤
   │           │ Capture       │ Escape / Abort │
   │           ▼               ▼                │
   │    ┌──────────────┐ ┌──────────────┐       │
   │    │ AwaitingStmt │ │ AutoFlushed  │───────┘
   │    └──────┬───────┘ └──────────────┘
   │           │ Statement Submitted
   │           ▼
   │    ┌──────────────┐
   │    │   Revealed   │ (Trace Flushed to Disk/Clipboard)
   │    └──────┬───────┘
   │           │ Reset / Next Attempt
   └───────────┴────────────────────────────────┘
```

- **1. Armed (Sẵn sàng)**: Đã cấp phát bộ đệm Ring Buffer $N_{\text{ring}} = 2048$, lắng nghe tín hiệu bắt đầu lượt chơi mới từ GameManager / Event Bus.
- **2. Recording (Đang ghi vết ngầm)**: Đang trong lượt chơi. Nhận các sự kiện từ Event Bus Pha 5, ghi tuần tự vào Ring Buffer. Không hiển thị Live Attribution ra UI.
- **3. AwaitingStatement (Chờ phát biểu nguyên nhân)**: Khi người chơi bị bắt, đóng băng tiếp nhận sự kiện thế giới, hiển thị giao diện khóa nhập liệu để tester phát biểu lý do.
- **4. Revealed (Giải mã & Xả vết)**: Mở khóa truy xuất vết, xả tệp JSON hoặc Clipboard, đối chiếu nguyên nhân thực tế với phát biểu của người chơi.
- **5. AutoFlushed (Xả tự động khi thắng/hủy)**: Đối với các lượt chơi hoàn thành sạch (Clean Escape) hoặc hủy bỏ (Abort), xả toàn bộ vết của attempt đó ngay lập tức để phục vụ nghiệm thu Claim 2 & 3.

### Interactions with Other Systems

| Hệ thống Tương tác | Hướng Giao tiếp | Hợp đồng Dữ liệu / Cơ chế Kết nối | Mục đích Phối hợp |
|---|---|---|---|
| **Event Bus (`#15`)** | Lắng nghe (Egress) | Đăng ký nhận tại `Phase 5: Presentation / Telemetry` | Thu thập sự kiện chuẩn hóa không gây ảnh hưởng đến luồng tính toán AI và Vật lý. |
| **Guard AI FSM (`#1`)** | Thu thập Sự kiện | Tiêu thụ bản ghi `FSM Decision Trace` & `LivenessFact` | Ghi nhận chính xác mốc thay đổi trạng thái và quyết định điều tra/truy đuổi của lính gác. |
| **Perception Systems (`#2`)** | Thu thập Sự kiện | Lắng nghe `LOS gain`, `LOS break`, `Noise heard` | Xác định thời điểm và cự ly mà lính gác bắt đầu nhận thức được người chơi. |
| **Suspicion Grade (`#7`)** | Cung cấp Dữ liệu | API `ITelemetryQueryService.GetEscalationCause(entry_id)` | Hỗ trợ hệ thống tính điểm phân rã nguyên nhân trừ điểm (Forensic Grade Breakdown). |
| **Player Controller (`#11`)** | Thu thập Sự kiện | Lắng nghe `step_event`, `MovementStateTransition` | Liên kết tốc độ, tư thế di chuyển của nhân vật với các thời điểm phát ra tiếng động. |
| **Input System (`#19`)** | Nhận Tín hiệu | Lắng nghe phím tắt `DeveloperKeyMap/DumpTrace` (F12) | Kích hoạt xả vết thủ công phục vụ kiểm thử và phân tích trực tiếp. |

## Formulas

Hệ thống Telemetry vận hành dựa trên 4 công thức toán học xác định, bao gồm thuật toán địa chỉ hóa bộ nhớ vòng tròn và các tiêu chuẩn kiểm định định lượng cho 3 Giả thuyết cốt lõi (Core Hypotheses Claims 1, 2, 3):

### D1: Phép Ánh xạ Địa chỉ Vòng tròn Không Phân mảnh (Ring Buffer Indexing & Sequence Mapping)

Bộ đệm vòng tròn sử dụng một biến đếm nhịp ghi tuần tự tăng đơn điệu $S_{\text{global}} \in \mathbb{N}$ (64-bit unsigned integer) để xác định chỉ số ô nhớ thực tế trong mảng tĩnh có kích thước lũy thừa của hai ($N_{\text{ring}} = 2048 = 2^{11}$):

1. **Công thức Ánh xạ Chỉ số Bộ nhớ (Memory Index Mapping)**:
   $$i_{\text{slot}} = S_{\text{global}} \pmod{N_{\text{ring}}} = S_{\text{global}} \ \& \ (N_{\text{ring}} - 1)$$
   - Sử dụng phép toán bitwise AND (`& 2047`) thay thế phép chia lấy dư modulo, tối ưu hóa triệt để thời gian thực thi ($< 1\text{ ns}$ mỗi thao tác ghi).

2. **Xác định Số lượng Bản ghi Hợp lệ trong Lượt chơi Hiện tại**:
   $$K_{\text{valid}} = \min\left(S_{\text{global}} - S_{\text{epoch\_start}}, N_{\text{ring}}\right)$$
   - Trong đó $S_{\text{epoch\_start}}$ là chỉ số nhịp tại thời điểm bắt đầu lượt chơi mới (`Attempt Start`).

- **Dải giá trị đầu ra (Output Range)**: $i_{\text{slot}} \in [0, 2047]$, $K_{\text{valid}} \in [0, 2048]$.
- **Ví dụ tính toán (Worked Example)**:
  - Lượt chơi bắt đầu tại $S_{\text{epoch\_start}} = 5000$.
  - Khi ghi sự kiện thứ $5100$, chỉ số slot được ghi là: $i_{\text{slot}} = 5100 \ \& \ 2047 = 1004$.
  - Số lượng bản ghi hợp lệ: $K_{\text{valid}} = \min(5100 - 5000, 2048) = 100$.

---

### D2: Tỷ lệ Thấu hiểu Nguyên nhân Bị Bắt (Claim 1: Forensic Detection Legibility Score)

Để chứng minh tính công bằng và dễ học hỏi (Fairness & Learnability), sau mỗi lần bị bắt giữ, người chơi phải phát biểu được ít nhất một **nguyên nhân thực sự cụ thể** (Specific True Cause) trùng khớp với chuỗi quyết định FSM:

1. **Hàm Phù hợp Tiêu chí Chẩn đoán (Rubric Matching Predicate)**:
   $$M(p) = \begin{cases}
   1 & \text{if } \text{Statement}(p) \cap \text{FsmCausalChain}(p) \neq \emptyset \quad (\text{per QA Rubric}) \\
   0 & \text{otherwise}
   \end{cases}$$
   - Chỉ tính điểm cho lần bắt giữ **đầu tiên** của mỗi người chơi ($N_{\text{testers}} \ge 5$ người) để tránh nhiễm nhận thức từ các lượt chơi sau.

2. **Công thức Tỷ lệ Đạt Tiêu chuẩn Claim 1**:
   $$\text{Score}_{\text{Claim1}} = \frac{\sum_{p=1}^{N_{\text{first\_captures}}} M(p)}{N_{\text{first\_captures}}} \ge 0.80 \quad (80\%)$$

- **Dải giá trị đầu ra (Output Range)**: $\text{Score}_{\text{Claim1}} \in [0.00, 1.00]$.
- **Ví dụ tính toán (Worked Example)**:
  - Thử nghiệm trên $N = 6$ người chơi lần đầu.
  - 5 người phát biểu chính xác ("Tôi chạy gây tiếng động", "Tôi nán lại trong đèn pin"), 1 người phát biểu mơ hồ ("Hắn tự nhiên thấy tôi").
  - $\text{Score}_{\text{Claim1}} = \frac{5}{6} \approx 83.3\% \ge 80\% \implies$ **PASS Claim 1**.

---

### D3: Chỉ số Thấu hiểu & Thực thi Lộ trình An toàn (Claim 2: Dual-Sub-Bar Patrol Comprehension & Execution)

Người chơi sau khi quan sát một chu kỳ tuần tra hoàn chỉnh của lính gác phải: (1) Trình bày được kế hoạch di chuyển đúng, và (2) Vượt qua căn phòng ngay trong lượt thử đầu tiên sau quan sát mà không bị phát hiện thị giác:

1. **Tiêu chuẩn Kép Hai Nhánh (Dual Sub-Bars)**:
   - **Nhánh A (Thấu hiểu kế hoạch - Comprehension)**: $C(p) \in \{0, 1\}$ (Đúng lộ trình và cửa sổ thời gian xuất phát an toàn).
   - **Nhánh B (Thực thi thành công - Execution)**: $E(p) \in \{0, 1\}$ (Hoàn thành phòng chơi mà không phát sinh bất kỳ sự kiện leo thang thị giác `Investigate` hoặc `Chase`, với điều kiện cảnh giác ban đầu $R_{\text{start}} \le \varepsilon_{\text{residual}} = 0.05$).

2. **Công thức Điểm Tổng hợp Claim 2**:
   $$\text{Pass}_{\text{Claim2}}(p) = C(p) \cdot E(p)$$
   $$\text{Score}_{\text{Claim2}} = \frac{\sum_{p=1}^{N_{\text{testers}}} \text{Pass}_{\text{Claim2}}(p)}{N_{\text{testers}}} \ge 0.60 \quad (60\% \text{ with } N \ge 5)$$

- **Dải giá trị đầu ra (Output Range)**: $\text{Score}_{\text{Claim2}} \in [0.00, 1.00]$.
- **Ví dụ tính toán (Worked Example)**:
  - Có $N = 5$ người chơi. 4 người hiểu đúng kế hoạch ($C=1$), trong đó 3 người thực hiện thành công ngay lượt đầu tiên mà không kích hoạt nghi ngờ ($E=1$).
  - $\text{Score}_{\text{Claim2}} = \frac{3}{5} = 60.0\% \ge 60\% \implies$ **PASS Claim 2**.

---

### D4: Tỷ lệ Sử dụng Công cụ Tự nguyện Không Hướng dẫn (Claim 3: Unprompted Tool Adoption Metric)

Khẳng định rằng người chơi tự nguyện sử dụng đạn nén Burst như một công cụ chiến thuật đánh lạc hướng mà không cần bất kỳ hướng dẫn hay gợi ý HUD nào:

1. **Mẫu số Đủ điều kiện Tiếp cận (Presented Denominator)**:
   - Người chơi chỉ được tính vào tập mẫu khi đã thực sự tiếp cận vật phẩm ném:
     $$D_{\text{reach}}(p) = \begin{cases}
     1 & \text{if đã phát sinh sự kiện } \texttt{pickup-reached} \ (d \le 1.20\text{ m}) \\
     0 & \text{if chưa từng tiếp cận vật phẩm}
     \end{cases}$$

2. **Tử số Sử dụng Tự nguyện (Unprompted Numerator)**:
   - $U(p) = 1$ nếu người chơi ném Burst ở các lượt thử sau khi đã nhặt vật phẩm mà không có sự nhắc nhở của người hướng dẫn.

3. **Công thức Tỷ lệ Chấp nhận Công cụ Claim 3**:
   $$\text{Score}_{\text{Claim3}} = \frac{\sum_{p \in \{D_{\text{reach}}=1\}} U(p)}{\sum_{p=1}^{N_{\text{testers}}} D_{\text{reach}}(p)} \ge 0.60 \quad (60\% \text{ with } N_{\text{presented}} \ge 5)$$

- **Dải giá trị đầu ra (Output Range)**: $\text{Score}_{\text{Claim3}} \in [0.00, 1.00]$.
- **Ví dụ tính toán (Worked Example)**:
  - Trong 6 người chơi, có 5 người tìm thấy vật phẩm ($N_{\text{presented}} = 5$).
  - Trong số đó, 4 người chủ động ném chai để dụ lính gác mở cổng an toàn.
  - $\text{Score}_{\text{Claim3}} = \frac{4}{5} = 80.0\% \ge 60\% \implies$ **PASS Claim 3**.

## Edge Cases

Hệ thống Telemetry xử lý minh bạch và dứt khoát 6 kịch bản biên bất thường, bảo đảm độ toàn vẹn của dữ liệu vết và ngăn ngừa sai lệch trong công tác đánh giá Playtest:

### EC1: Tràn Bộ đệm Vòng tròn khi Lượt chơi Kéo dài Bất thường (Ring Buffer Overflow via FIFO Overwrite)
- **Kịch bản**: Người chơi di chuyển vòng vo, nấp trong bóng tối hoặc kéo dài một lượt thử vượt quá 2048 sự kiện ghi vết ($S_{\text{global}} - S_{\text{epoch\_start}} > 2048$).
- **Hành vi xác định**:
  1. Hệ thống tiếp tục ghi đè tuần hoàn theo cơ chế FIFO lên các ô nhớ cũ nhất của lượt chơi, không dừng ghi và không ném ngoại lệ (no crash/no stall).
  2. Khi xả dữ liệu (`Flush`), hệ thống đánh dấu cờ cảnh báo `buffer_overflow_truncated: true` và ghi nhận số lượng sự kiện đã bị loại bỏ ($N_{\text{dropped}} = S_{\text{global}} - S_{\text{epoch\_start}} - N_{\text{ring}}$).
  3. Chuỗi sự kiện quan trọng nhất dẫn đến kết cục thất bại (chuỗi 2048 sự kiện gần nhất trước thời điểm Capture) luôn được bảo toàn $100\%$, bảo đảm chuỗi nhân quả của lần bị bắt giữ không bao giờ bị mất dấu.

### EC2: Cơ chế Dự phòng khi Clipboard WebGL Bị Từ chối Truy cập (WebGL Clipboard Permission Denial Fallback)
- **Kịch bản**: Người chơi trải nghiệm bản build WebGL trên một số trình duyệt kích hoạt chính sách bảo mật khắt khe (chặn quyền gọi `GUIUtility.systemCopyBuffer` hoặc `navigator.clipboard`).
- **Hành vi xác định**:
  1. Thao tác sao chép được bao bọc trong khối `try/catch`. Nếu phát sinh lỗi quyền truy cập, hệ thống bắt ngoại lệ fail-safe mà không làm treo game.
  2. Kích hoạt tức thì cơ chế dự phòng cấp 2: Lưu chuỗi JSON vết vào bộ nhớ cục bộ trình duyệt `window.localStorage.setItem("ww_fsm_trace_latest", jsonString)`.
  3. Kích hoạt hook cầu nối JavaScript `window.onFsmTraceFlushed(jsonString)` để trang web chủ (Host Web Page) có thể tự động tải xuống tệp JSON hoặc gửi về máy chủ phân tích.

### EC3: Chết Ngay Lập tức Trước khi Kịp Phát sinh Sự kiện (Instant Capture / Zero-Step Death)
- **Kịch bản**: Người chơi đứng yên ngay tại điểm xuất phát và bị lính gác bắt giữ ngay trong những tick đầu tiên do đứng sai vị trí hoặc lỗi nạp màn chơi ($K_{\text{valid}} < 5$).
- **Hành vi xác định**:
  1. Hệ thống vẫn xả toàn bộ dữ liệu vết bình thường, ghi nhận sự kiện mở đầu `AttemptBoundary: Start` và kết thúc bằng `Capture`.
  2. Bản ghi mang cờ cảnh báo `trivial_attempt_duration: true` (thời lượng $< 1.0\text{ s}$).
  3. Lượt thử này được đánh dấu loại khỏi tập mẫu tính toán của Claim 1 và Claim 2 để bảo đảm tính khách quan của dữ liệu thống kê kiểm thử.

### EC4: Trùng lặp Định danh Phiên chơi do Tải lại Màn chơi Cấp tốc (Rapid Scene Reload & Session Collision)
- **Kịch bản**: Người chơi nhấn nút Restart liên tục nhiều lần trong một phần nghìn giây hoặc xảy ra sự cố tải lại màn chơi cấp tốc.
- **Hành vi xác định**:
  1. Phong bì phiên `session_id` được sinh bằng hàm băm kết hợp `Guid.NewGuid()` và dấu thời gian thực hệ thống lúc khởi tạo (`EpochTicks`).
  2. Mỗi lần tải lại màn chơi luôn sinh một `session_id` mới hoàn toàn và đặt lại `attempt_epoch = 1`.
  3. Tên tệp xuất JSON hoặc khóa lưu trữ luôn mang tính duy nhất tuyệt đối, không bao giờ xảy ra tình trạng ghi đè tệp của lượt chơi trước đó.

### EC5: Mở Menu Tạm Dừng hoặc Mất Focus Cửa sổ Ứng dụng (Pause Menu & Window Focus Loss)
- **Kịch bản**: Người chơi nhấn `Escape` mở Menu Tạm dừng hoặc chuyển cửa sổ ứng dụng (Alt-Tab) trong khi đang trong lượt thử.
- **Hành vi xác định**:
  1. Khi `Time.timeScale = 0.0f`, đồng hồ nhịp ảo (`H.0.9 virtual_timestamp`) đóng băng tuyệt đối.
  2. Hệ thống Telemetry tạm dừng nhận các sự kiện chuyển động, chỉ ghi duy nhất 1 bản ghi sự kiện `AppFocusChanged` hoặc `SessionPaused` để giải thích khoảng trống thời gian.
  3. Khi tiếp tục chơi, nhịp ảo tiếp tục tăng từ giá trị đóng băng, bảo đảm các phép tính toán khoảng thời gian thực thi của lính gác không bị méo mó.

### EC6: Thất bại do Hết Giờ / Hủy Lượt Chơi không qua Bắt giữ (Non-Capture Attempt Termination)
- **Kịch bản**: Người chơi chủ động bấm nút "Thử lại phòng chơi" (Restart Attempt) từ menu tạm dừng, hoặc thoát game giữa chừng khi chưa bị bắt và chưa tới cổng thoát.
- **Hành vi xác định**:
  1. Ghi nhận sự kiện kết thúc `AttemptBoundary: Aborted` kèm theo lý do hủy bỏ `reason: "user_restart"`.
  2. Hệ thống tự động xả dữ liệu vết của attempt đó ngay lập tức (`AutoFlushed`) mà không kích hoạt giao diện phỏng vấn tester (không hiển thị `AwaitingStatement`).
  3. Lượt chơi này không được tính vào mẫu số của Claim 1 (do không có sự kiện Capture), nhưng được ghi nhận vào bộ đếm số lần thử của Claim 2 ($K \le 3$).

## Dependencies

Hệ thống Telemetry vận hành theo nguyên tắc phân tách trách nhiệm (Separation of Concerns), đóng vai trò là một điểm thu thập và cung cấp dịch vụ phân tích thông qua giao diện chuẩn `ITelemetryService`:

```csharp
public interface ITelemetryService {
    // Ingestion API (Egress consumer từ Event Bus Pha 5)
    void RecordEvent<T>(in T eventRecord) where T : struct, IFsmTraceEvent;
    
    // Lifecycle Management
    void StartAttempt(string sessionId, int attemptEpoch, ulong startTimestamp);
    void EndAttempt(AttemptEndReason reason, ulong endTimestamp);
    void SubmitPlayerStatement(string statementText);
    
    // Query & Export API
    ReadOnlySpan<FsmTraceRecord> GetCurrentAttemptTrace();
    bool TryGetEscalationCause(string entryId, out EscalationCauseInfo causeInfo);
    void FlushTraceToStorage();
}
```

### Upstream Dependencies (Hệ thống Cung cấp Dữ liệu Đầu vào)

1. **Event / Messaging Bus (`#15` - `design/gdd/event-messaging-bus.md`)**:
   - **Dữ liệu tiêu thụ**: Toàn bộ luồng sự kiện có kiểu phát ra trong ván chơi, nhận tại **Pha 5 (`Phase 5: Presentation / Telemetry / Grade`)**.
   - **Ràng buộc hai chiều**: Event Bus cam kết không giữ tham chiếu tới payload sau pha 5; Telemetry sao chép dữ liệu dạng blittable value-type vào Ring Buffer trong $\mathcal{O}(1)$.

2. **Guard AI FSM (`#1` - `design/gdd/guard-ai-fsm.md`)**:
   - **Dữ liệu tiêu thụ**: Các sự kiện quyết định mang tính bước ngoặt: `Escalation` (Investigate-commit), `Noise re-anchor`, `Chase-entry`, `Chase-end`, `Capture`, `LivenessFact`.
   - **Ràng buộc hai chiều**: Guard AI FSM sở hữu logic quyết định; Telemetry chỉ ghi nhận nguyên trạng các trường định danh (`entry_id`, `fact_id`, `cause`, `position`), tuyệt đối không làm biến đổi hay can thiệp ngược lại hành vi của FSM.

3. **Perception Systems (`#2` - `design/gdd/perception.md`)**:
   - **Dữ liệu tiêu thụ**: `LOS gain`, `LOS break`, `Threshold crossing`, `Confirm-window elapsed`, `Noise heard`.
   - **Ràng buộc hai chiều**: Perception tính toán và gắn nhãn $R_{\text{eff}}$, mức nghi ngờ $A$, độ cảnh giác $R$; Telemetry ghi nhận để phục vụ đối chiếu nguyên nhân bắt giữ.

4. **Player Controller (`#11` - `design/gdd/player-third-person-controller.md`) & Player Noise (`#3`)**:
   - **Dữ liệu tiêu thụ**: `step_event` (bước chân kèm sải chân), `MovementStateTransition` (Walk, Run, Crouch), `NoisePublished` (Burst landing).
   - **Ràng buộc hai chiều**: Cung cấp dấu thời gian nhịp ảo từ bộ đếm `H.0.9`, bảo đảm trục thời gian thống nhất trên toàn hệ thống.

---

### Downstream Dependents (Hệ thống Tiêu thụ Dữ liệu Đầu ra)

1. **Suspicion Meter / Grade Operator (`#7` - `design/gdd/suspicion-meter-grade.md`)**:
   - **Dữ liệu cung cấp**: Tra cứu nguyên nhân leo thang thông qua `ITelemetryService.TryGetEscalationCause(entry_id)`.
   - **Mục đích**: Phân rã chính xác các khoản trừ điểm trên bảng điểm kết thúc màn chơi (`Forensic Score Breakdown`).

2. **QA & Playtest Harness (`design/qa/prototype-playtest-plan.md`)**:
   - **Dữ liệu cung cấp**: Tệp xuất JSON hoàn chỉnh của các lượt thử và cơ chế đối chiếu phát biểu của người chơi để nghiệm thu 3 Giả thuyết cốt lõi (Claim 1, Claim 2, Claim 3).

3. **HUD / UI (`#14` - Vertical Slice)**:
   - **Dữ liệu cung cấp**: Chuỗi vết tóm tắt phục vụ hiển thị sơ đồ đường đi và các mốc chạm trán (Timeline Encounters) ở màn hình tổng kết nhiệm vụ sau này.

## Tuning Knobs

Tất cả các tham số vận hành của hệ thống Telemetry được quản lý theo mô hình điều khiển bằng cấu hình dữ liệu (`TelemetryConfig` ScriptableObject) và chia thành 3 nhóm chức năng rõ ràng:

### Nhóm 1: Dung lượng & Quản lý Bộ nhớ (Buffer Capacity & Memory Management)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Kỹ thuật & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `telemetry_ring_buffer_capacity` | `2048` | phần tử | `[1024, 8192]` | Dung lượng tối đa của Ring Buffer trong RAM. Giá trị $2048$ phần tử ($\approx 256\text{ KiB}$) là lũy thừa của 2, tối ưu hóa phép toán bitwise AND và đủ sức lưu trữ toàn bộ chuỗi sự kiện của một lượt chơi dài $5\text{ phút}$ ở tốc độ phát sinh trung bình $\approx 7\text{ sự kiện/s}$. | CR1, D1 |
| `telemetry_record_byte_size_max` | `128` | bytes | `[64, 256]` | Kích thước trần của struct blittable `FsmTraceRecord`. Giữ struct nhỏ gọn bảo đảm cache locality của CPU khi duyệt vết. | CR1 |
| `telemetry_burst_reach_distance` | `1.20` | mét (m) | `[0.80, 2.00]` | Bán kính hình cầu quanh vật phẩm nhặt Burst để kích hoạt sự kiện `pickup-reached`, neo chính xác mẫu số tiếp cận của Claim 3. | CR2, D4 |

### Nhóm 2: Ngưỡng Nghiệm thu Kiểm thử Cốt lõi (Playtest Claims Validation Thresholds)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Thiết kế & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `telemetry_claim1_pass_threshold` | `0.80` | tỷ lệ ($80\%$) | `[0.70, 0.90]` | Tỷ lệ người chơi lần đầu phát biểu chính xác nguyên nhân bị bắt giữ ngay sau lần bắt đầu tiên ($N_{\text{first\_captures}} \ge 5$). | D2 |
| `telemetry_claim2_pass_threshold` | `0.60` | tỷ lệ ($60\%$) | `[0.50, 0.80]` | Tỷ lệ người chơi vượt qua cả 2 nhánh (Thấu hiểu kế hoạch $C=1$ VÀ Thực thi thành công $E=1$ ngay lượt đầu sau quan sát). | D3 |
| `telemetry_claim3_pass_threshold` | `0.60` | tỷ lệ ($60\%$) | `[0.50, 0.80]` | Tỷ lệ người chơi tự nguyện sử dụng đạn nén Burst không cần nhắc nhở sau khi đã tiếp cận vật phẩm ($N_{\text{presented}} \ge 5$). | D4 |
| `telemetry_claim2_residual_epsilon` | `0.05` | điểm cảnh giác | `[0.01, 0.10]` | Trần cảnh giác dư thừa ban đầu tối đa cho phép để một lượt thử được coi là hợp lệ đánh giá Claim 2 ($R_{\text{start}} \le 0.05$). | D3 |
| `telemetry_min_tester_sample_size` | `5` | người | `[5, 20]` | Số lượng người chơi tối thiểu để kết quả kiểm định thống kê đạt giá trị kết luận (dưới 5 người thì kết quả là `INCONCLUSIVE`). | D2, D3, D4 |

### Nhóm 3: Giao thức Xuất file & Dự phòng WebGL (Export Pipeline & Platform Fallbacks)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Kỹ thuật & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `telemetry_flush_async_timeout_ms` | `500` | mili-giây (ms) | `[100, 2000]` | Thời gian chờ tối đa cho tác vụ ghi tệp JSON bất đồng bộ xuống ổ đĩa PC trước khi cảnh báo timeout. | CR4 |
| `telemetry_trivial_duration_threshold_s` | `1.00` | giây (s) | `[0.50, 2.00]` | Ngưỡng thời lượng tối thiểu của một attempt; dưới ngưỡng này bị đánh dấu `trivial_attempt_duration: true` (bị loại khỏi mẫu). | EC3 |
| `telemetry_webgl_storage_key` | `"ww_fsm_trace_latest"` | chuỗi ký tự | khóa duy nhất | Tên khóa định danh lưu trữ chuỗi JSON dự phòng vào `window.localStorage` của trình duyệt WebGL. | EC2 |
| `telemetry_flush_hotkey` | `"F12"` | phím điều khiển | phím hợp lệ | Phím nóng cưỡng bức xả vết sự kiện FSM hiện tại xuống tệp/bộ nhớ tạm trong bản build phát triển. | Section Audio |

## Visual/Audio Requirements

Dù là hệ thống chẩn đoán ngầm (dev-only trong MVP), Telemetry cung cấp công cụ trực quan hóa trực tiếp trên Unity Editor và các phản hồi xúc giác/âm thanh khi xả dữ liệu:

### Visual Requirements (Yêu cầu Thị giác & Công cụ Trực quan hóa Editor)

1. **Công cụ Trực quan hóa Gizmo Quyết định FSM (`FsmDecisionGizmoDrawer`)**:
   - Trong Unity Editor (Scene View) hoặc bản build Development (khi bật cờ `telemetry_debug_overlay = true`):
     - **Tia Tầm nhìn (LOS Rays)**: Vẽ đường tia màu vàng mảnh nối từ mắt lính gác tới ngực nhân vật khi có `LOS gain`, chuyển sang màu đỏ rực khi `Confirm-window elapsed` và biến mất (đứt đoạn màu xám) khi có `LOS break`.
     - **Vòng tròn Âm thanh (Noise Radii)**: Vẽ đường tròn đồng tâm trên mặt sàn XZ thể hiện phạm vi lan truyền âm thanh $R_{\text{eff}}$ của bước chân (xanh dương) và đạn nén Burst (vàng cam), mờ dần trong $0.5\text{ s}$.
     - **Điểm Neo Điều tra (Investigation Anchor Markers)**: Đặt một khối lập phương wireframe màu vàng tại vị trí neo nghi ngờ (`noise_anchor_position`) và vẽ đường mũi tên chỉ hướng di chuyển mục tiêu của lính gác.
     - **Điểm Bắt giữ (Capture Epicenter)**: Đánh dấu một hình cầu wireframe màu đỏ tại vị trí nhân vật bị bắt giữ kèm nhãn văn bản nổi (`GUI.Label`) thể hiện nguyên nhân gốc (`Root Cause: Sprint Footstep / East Window LOS`).

2. **Chế độ Màn hình Mù Khi Chơi Thử (Tester Blind Screen Policy)**:
   - Trong bản build dành cho người thử nghiệm Playtest, toàn bộ các Gizmo và nhãn văn bản trên đều bị vô hiệu hóa cưỡng bức (`#if !DEVELOPMENT_BUILD`), bảo đảm khung nhìn người chơi trong sạch $100\%$.

---

### Audio Requirements (Yêu cầu Âm thanh Chẩn đoán)

1. **Âm thanh Xác nhận Xả Dữ liệu (Flush Confirmation Chime)**:
   - Khi người kiểm thử nhấn phím `F12` hoặc hệ thống hoàn tất việc xả dữ liệu tệp JSON/Clipboard thành công:
     - Phát một âm thanh click kỹ thuật số tần số cao nhẹ nhàng (`sfx_telemetry_flush_confirm`, $1200\text{ Hz}$, âm lượng $-18.0\text{ dBFS}$, thời lượng $0.08\text{ s}$) trên kênh âm thanh UI.
     - Giúp người kiểm thử nhận biết chắc chắn dữ liệu ván chơi đã được sao lưu an toàn mà không cần thoát ra màn hình desktop để kiểm tra.

2. **Cấm Hoàn toàn Âm thanh Gợi ý Trong ván chơi (No Diegetic Hint Audio)**:
   - Hệ thống Telemetry tuyệt đối không phát ra bất kỳ âm thanh hay tiếng bíp cảnh báo nào khi ghi nhận các sự kiện nghi ngờ, tránh việc vô tình tạo ra gợi ý âm thanh hỗ trợ người chơi gian lận bài kiểm tra Claim 1.

---

## UI Requirements

### 1. Màn hình Khóa Phỏng vấn Người thử nghiệm (Tester Interview Lockout Modal)

Giao diện chuyên dụng xuất hiện ngay sau khi xảy ra sự kiện `Capture` trong phiên bản Playtest:

```text
┌─────────────────────────────────────────────────────────────┐
│                 WHISPER WARD - PLAYTEST AUDIT               │
│                  [Attempt #1 - Capture Event]               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  LƯỢT CHƠI ĐÃ KẾT THÚC BỞI LÍNH GÁC BẮT GIỮ.                │
│                                                             │
│  Hãy hỏi người chơi:                                        │
│  "Điều gì cụ thể đã khiến lính gác phát hiện bạn?"          │
│                                                             │
│  [ Ô nhập nội dung phát biểu của người chơi               ] │
│  [ (Ví dụ: Chạy gây tiếng động gần cửa sổ phía Đông)     ] │
│                                                             │
│  ┌───────────────────────────┐ ┌──────────────────────────┐ │
│  │ [Xác nhận & Giải mã Vết]  │ │ [Bỏ qua & Chơi lại ngay] │ │
│  └───────────────────────────┘ └──────────────────────────┘ │
│                                                             │
│  (Sau khi bấm Xác nhận, nguyên nhân thực tế từ FSM Trace    │
│   sẽ hiển thị bên dưới để đối chiếu tính chuẩn xác)         │
└─────────────────────────────────────────────────────────────┘
```

1. **Trạng thái Khóa Khung hình (Modal Lock)**:
   - Khi xuất hiện, giao diện khóa toàn bộ input điều khiển nhân vật và camera.
   - Ẩn toàn bộ thông tin phân tích nguyên nhân cho đến khi người kiểm thử bấm nút `[Xác nhận & Giải mã Vết]`.

2. **Bảng Đối chiếu Sau Giải mã (Post-Reveal Comparison Panel)**:
   - Khi nhấn Xác nhận:
     - Hiển thị nguyên nhân gốc từ FSM: `Nguyên nhân thực tế: Noise Event (Sprint, R = 6.0m) -> Investigate Commit -> Visual Confirmation`.
     - Cung cấp hai nút đánh giá nhanh cho chuyên viên QA: `[Khớp tiêu chí (Claim 1 PASS)]` hoặc `[Không khớp (Claim 1 FAIL)]`.
     - Tự động xả chuỗi JSON vết ra thư mục Telemetry kèm câu trả lời của người chơi và đánh giá của QA.

### 2. Chỉ báo Trạng thái WebGL Clipboard (WebGL Copy Toast Notification)

- Trên bản build WebGL, khi dữ liệu vết được sao chép vào bộ nhớ tạm, hiển thị một thông báo nhỏ (Toast Notification) ở góc dưới bên phải màn hình trong $2.5\text{ s}$:
  - Biểu tượng: `📋 FSM Trace copied to Clipboard (Ctrl+V to paste JSON)`.
  - Giúp người chơi hoặc chuyên viên QA có thể dán trực tiếp dữ liệu vết vào biểu mẫu Google Forms hoặc bảng tính báo cáo lỗi.

## Acceptance Criteria

Mọi tiêu chí nghiệm thu (AC) đều được thiết kế độc lập, có thể kiểm chứng khách quan bằng kiểm thử tự động (Unit/Integration Test) hoặc quy trình thử nghiệm thực tế (Playtest Protocol):

### Nhóm Kỹ thuật & Hiệu năng Bộ nhớ (Technical & In-Memory Performance)

- **AC1 — Không cấp phát bộ nhớ rác trên luồng nóng (Zero-GC Ingestion Hot-Path)**:
  - *Điều kiện & Thao tác*: Gọi `ITelemetryService.RecordEvent<T>(in T record)` liên tục $10,000$ lần trong vòng lặp `Update` với các struct `FsmTraceRecord` blittable.
  - *Kết quả mong đợi*: `GC.GetAllocatedBytesForCurrentThread()` ghi nhận độ lệch bằng chính xác $0\text{ byte}$. Không có bất kỳ boxing hay heap allocation nào diễn ra.

- **AC2 — Chỉ mục vòng tròn mặt nạ nhị phân chính xác (Bitwise Mask Ring Indexing)**:
  - *Điều kiện & Thao tác*: Tăng biến đếm tuần tự toàn cục $S_{\text{global}}$ vượt qua $N_{\text{ring}} = 2048$ (ví dụ: $S_{\text{global}} = 2048, 2049, 4096$).
  - *Kết quả mong đợi*: Chỉ mục ô ghi $i_{\text{slot}} = S_{\text{global}} \ \& \ 2047$ xoay vòng chính xác về các slot $0, 1, 0$. Ghi đè tuần tự mượt mà không gây ra ngoại lệ `IndexOutOfRangeException` hay làm sai lệch dữ liệu liền kề.

- **AC3 — Trích xuất lát cắt sự kiện ván chơi chính xác (Attempt Record Span Slice)**:
  - *Điều kiện & Thao tác*: Lượt chơi bắt đầu tại $S_{\text{epoch\_start}} = 1000$. 
    - Trường hợp A: Ghi nhận $500$ sự kiện ($S_{\text{global}} = 1500$), gọi `GetCurrentAttemptTrace()`.
    - Trường hợp B: Ghi nhận $2500$ sự kiện ($S_{\text{global}} = 3500$), gọi `GetCurrentAttemptTrace()`.
  - *Kết quả mong đợi*: 
    - Trường hợp A trả về lát cắt `ReadOnlySpan<FsmTraceRecord>` gồm đúng $500$ bản ghi.
    - Trường hợp B áp dụng công thức D1, trả về đúng $K_{\text{valid}} = \min(2500, 2048) = 2048$ bản ghi gần nhất, bảo toàn tính toàn vẹn của chuỗi sự kiện.

- **AC4 — Truy vấn chuỗi nguyên nhân leo thang chuẩn xác (Root-Cause Causal Attribution Query)**:
  - *Điều kiện & Thao tác*: Thiết lập kịch bản lính gác bắt giữ nhân vật sau chuỗi sự kiện: `step_event` (Sprint) $\rightarrow$ `NoisePublished` $\rightarrow$ `Investigate` $\rightarrow$ `LOS gain` (Visual confirmation) $\rightarrow$ `Capture`. Gọi `TryGetEscalationCause()`.
  - *Kết quả mong đợi*: Phương thức trả về `true` với `EscalationCauseInfo` định danh chính xác nguyên nhân gốc là `Sprint Footstep` và nguyên nhân xác nhận thị giác là `Visual Confirmation`. Thời gian truy vấn hoàn tất trong $\le 0.1\text{ ms}$.

### Nhóm Quy trình Thử nghiệm Mù & Giao diện (Blind Protocol & UI Lockout)

- **AC5 — Khóa giao diện phỏng vấn và bảo mật dữ liệu vết (Blind Protocol Lockout)**:
  - *Điều kiện & Thao tác*: Kích hoạt sự kiện `Capture` trong bản build Playtest.
  - *Kết quả mong đợi*: Input điều khiển nhân vật và camera bị khóa tức thì ($0\%$ phản hồi). Toàn bộ Gizmo và thông tin phân tích nguyên nhân FSM bị ẩn $100\%$. Chỉ sau khi người kiểm thử nhập phát biểu và bấm `SubmitPlayerStatement()`, bảng đối chiếu và dữ liệu FSM mới được hiển thị.

- **AC6 — Xả dữ liệu tệp JSON bất đồng bộ trên PC Standalone (PC Asynchronous File Flush)**:
  - *Điều kiện & Thao tác*: Kích hoạt `FlushTraceToStorage()` hoặc nhấn `F12` trên bản build Windows PC.
  - *Kết quả mong đợi*: Một tệp JSON chuẩn UTF-8 tuân thủ `trace_event_schema` được ghi vào thư mục `Application.persistentDataPath/Telemetry/{session_id}_{epoch}.json` trong vòng $\le 50\text{ ms}$ mà không gây tụt khung hình (chạy trên background thread qua `FileStream.WriteAsync`).

- **AC7 — Sao chép Clipboard và cơ chế dự phòng trên WebGL (WebGL Dual Flush & Fallback)**:
  - *Điều kiện & Thao tác*: Gọi `FlushTraceToStorage()` trên bản build WebGL.
  - *Kết quả mong đợi*: Dữ liệu chuỗi JSON được nạp vào `GUIUtility.systemCopyBuffer` và kích hoạt hàm JavaScript `window.onFsmTraceFlushed`. Nếu quyền truy cập Clipboard bị trình duyệt chặn, hệ thống tự động ghi chuỗi JSON vào `window.localStorage` dưới khóa `ww_fsm_trace_latest` mà không gây crash ứng dụng.

### Nhóm Thẩm định Giả thuyết Cốt lõi (Core Hypothesis Metrics Evaluation)

- **AC8 — Thẩm định Tính minh bạch Nguyên nhân Bị bắt (Claim 1 — Forensic Legibility)**:
  - *Điều kiện & Thao tác*: Chạy công cụ phân tích Telemetry trên tập dữ liệu gồm $N \ge 5$ người chơi trải nghiệm lần đầu bị bắt giữ.
  - *Kết quả mong đợi*: Áp dụng công thức D2, nếu $\ge 80\%$ người chơi phát biểu khớp chính xác nguyên nhân gốc được FSM Trace ghi nhận ($\text{Score}_{\text{Claim1}} \ge 0.80$), bài kiểm tra thẩm định Claim 1 được đánh dấu PASS.

- **AC9 — Thẩm định Nắm bắt & Thực thi Thanh Nghi ngờ Kép (Claim 2 — Dual Sub-Bar Execution)**:
  - *Điều kiện & Thao tác*: Người chơi vượt qua bài kiểm tra lý thuyết về cơ chế thanh kép ($C=1$) và thực hiện pha tẩu thoát không để phát sinh nghi ngờ thị giác tồn dư ($E=1$, $R_{\text{start}} \le 0.05$).
  - *Kết quả mong đợi*: Điểm số tích hợp $\text{Pass}_{\text{Claim2}} = C \cdot E = 1$. Tỷ lệ đạt trên toàn bộ nhóm người thử nghiệm $\ge 60\%$ (Công thức D3).

- **AC10 — Thẩm định Tự nguyện Ứng dụng Công cụ (Claim 3 — Unprompted Tool Adoption)**:
  - *Điều kiện & Thao tác*: Lọc các lượt chơi có người chơi tiếp cận bán kính nhặt vật phẩm nén khí Burst ($d \le 1.20\text{ m}$, sự kiện `pickup-reached`).
  - *Kết quả mong đợi*: Tính toán tỷ lệ kích hoạt nén khí chủ động trước khi bị truy đuổi $U / D_{\text{reach}} \ge 0.60$ theo Công thức D4, thẩm định mức độ tự nguyện sử dụng công cụ đạt chuẩn thiết kế.

---

## Open Questions

- **OQ1 — Có nên hỗ trợ truyền phát dữ liệu từ xa (Remote HTTP Streaming) trong tương lai không?**:
  - *Bối cảnh*: Trong giai đoạn thử nghiệm diện rộng sau MVP, việc thu thập tệp JSON thủ công hoặc qua clipboard có thể bất tiện cho người chơi từ xa.
  - *Nghị quyết hiện tại*: Trong phạm vi MVP 8 tuần, việc xuất tệp cục bộ trên PC và sao chép Clipboard/LocalStorage trên WebGL là hoàn toàn tối ưu, loại bỏ sự phụ thuộc vào hạ tầng máy chủ và bảo đảm an toàn dữ liệu. Tính năng gửi POST HTTP lên endpoint analytics sẽ được cân nhắc ở giai đoạn hậu MVP nếu có nhu cầu phát hành bản demo rộng rãi.

- **OQ2 — Có cần ghi nhận toàn bộ chuỗi tín hiệu Input của người chơi (WASD & Chuột theo từng frame) không?**:
  - *Bối cảnh*: Việc ghi lại input từng frame có thể hỗ trợ tính năng Replay (Xem lại ván chơi).
  - *Nghị quyết hiện tại*: Không ghi nhận input thô từng frame. Việc này làm phình to dung lượng bộ nhớ và vi phạm tiêu chí tĩnh $\le 256\text{ KiB}$. Chuỗi sự kiện FSM hướng sự kiện (vị trí tọa độ, phát tán âm thanh bước chân, góc nhìn LOS) đã cung cấp đầy đủ $100\%$ độ trung thực pháp y để phân tích nguyên nhân mà không cần tái tạo vật lý toàn vẹn.
