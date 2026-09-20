# Event / Messaging Bus

> **Status**: Approved (2026-09-21)
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge), Pillar 3 (You Create the Situation)

## Overview

Hệ thống **Event / Messaging Bus** (System `#15`) là lớp hạ tầng kỹ thuật nền tảng (Foundation Layer) phi ghép nối (decoupled), chịu trách nhiệm vận chuyển, chuẩn hóa, sắp thứ tự thời gian và điều phối phân phối toàn bộ sự kiện gameplay giữa các hệ thống độc lập trong *Whisper Ward*. Hoạt động như một dịch vụ điều phối trung tâm dựa trên khung thời gian ảo xác định (Deterministic Virtual-Clock Phase Bus), hệ thống giải phóng hoàn toàn các phụ thuộc vòng tròn (circular dependencies) giữa bộ phát âm thanh người chơi (`NoiseEmitter`), hệ thống giác quan lính gác (`Perception`), máy trạng thái hành vi (`Guard AI FSM`), điểm ẩn nấp (`HideSpot`), cơ chế tính điểm (`Suspicion Meter / Grade`), và giao diện/telemetry.

Được hiện thực hóa theo hợp đồng kiến trúc **`ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts`**, hệ thống bao bọc mọi sự kiện trong một cấu trúc phong bì bất biến (`EventEnvelope`) mang đầy đủ định danh phiên chơi (`session_id`), kỷ nguyên thử thách (`attempt_epoch`), dấu thời gian ảo (`timestamp`), định danh nguồn phát (`publisher`), pha thời gian (`phase`), và định danh sở hữu bất biến (`identity`). Hệ thống bảo đảm tính công bằng tuyệt đối cho lối chơi lén lút thông qua 4 cơ chế bảo vệ cốt lõi: (1) Khử trùng lặp tại cổng vào (Ingress Deduplication) theo bộ khóa `(session_id, attempt_epoch, namespace, identity)` để loại bỏ triệt để việc xử lý trùng lặp tín hiệu âm thanh hay chuyển trạng thái tủ núp; (2) Hàng đợi có giới hạn nghiêm ngặt (`event_bus_pending_envelope_capacity = 256`) kết hợp chính sách từ chối sự kiện mới nhất (`bounded_queue_overflow_policy = reject-newest`) và cơ chế đệm thử lại xác định (`event_bus_retry_attempts = 3`) nhằm loại bỏ hiện tượng rơi rụng dữ liệu ngầm; (3) Rào cản vòng đời cấp nguyên tử (`BeginSession`, `BeginEpoch`) lập tức vô hiệu hóa các sự kiện cũ lỗi thời khi người chơi chết, bị bắt hoặc khởi động lại màn chơi; và (4) Quy trình bàn giao có kiểu 2 chiều (`Transactional Handoff`) bảo đảm ranh giới tải an toàn giữa Bus và hàng đợi xử lý thính giác của AI.

## Player Fantasy

Hệ thống Event / Messaging Bus là một **hạ tầng vô hình nhưng mang tính sống còn (The Invisible Bedrock of Causality)**. Người chơi không bao giờ trực tiếp nhìn thấy hay chạm vào Bus sự kiện, nhưng toàn bộ niềm tin của họ vào thế giới ngầm *Whisper Ward* đều dựa trên sự vận hành hoàn hảo của nó. Trong một tựa game lén lút dựa trên thử thách trí tuệ, nếu mối quan hệ nhân-quả bị phá vỡ (tiếng ồn đến trễ, sự kiện bị nuốt, hoặc AI phản ứng với những việc xảy ra từ... kiếp trước), ảo tưởng về một thế giới sống động và công bằng sẽ sụp đổ hoàn toàn.

Hệ thống phục vụ và củng cố trực tiếp hai trụ cột trải nghiệm cảm xúc:

1. **Thách thức Trí tuệ Công bằng (Pillar 2: Fair Mind-Challenge)**:
   - **Nhân - Quả Xác định Tuyệt đối (Flawless Deterministic Causality)**: Khi người chơi ném một quả đạn Burst đập vào bức tường gạch, âm thanh va chạm phải lập tức được ghi nhận và truyền đạt đến lính gác trong cùng pha thời gian ảo (`VirtualClockPhase`). Người chơi tin tưởng 100% rằng lính gác quay đầu là vì tiếng ném vừa phát ra, không bao giờ có độ trễ vô lý hay sự ngẫu nhiên của bộ đệm khung hình.
   - **Xóa Bỏ Hoàn Toàn Tình Trạng Bắt Oan Xuyên Cửa Tủ**: Khi người chơi lướt vào điểm ẩn nấp (`HideSpot`), sự kiện chuyển trạng thái sang `occupied` được chốt chặn trước khi pha hành vi của lính gác (`Guard AI FSM`) kiểm tra tầm tóm bắt. Người chơi cảm nhận được sự che chở thực sự của nơi ẩn nấp, không bao giờ bị "bắt oan" do sự kiện chui vào tủ đến sau tick kiểm tra va chạm của AI.
   - **Không Bị Phạt Bởi Dữ Liệu Ma (Zero Ghost Penalties)**: Sau khi bị lính tóm hoặc tự khởi động lại thử thách (Respawn / Epoch Increment), rào cản kỷ nguyên quét sạch toàn bộ hàng đợi. Người chơi hít một hơi thật sâu và làm lại từ đầu với một sân chơi hoàn toàn sạch sẽ — tuyệt đối không có hiện tượng lính gác chạy đến góc phòng vì nghe tiếng bước chân của lần chơi trước.

2. **Người Chơi Tạo Lập Tình Huống (Pillar 3: You Create the Situation)**:
   - **Sự Đáng Tin Cậy Khi Thao Túng**: Để người chơi dám mạo hiểm tạo ra tiếng ồn nhằm dụ lính gác rời khỏi chốt gác then chốt, họ phải có niềm tin tuyệt đối rằng tiếng động đó sẽ được phân phối đồng thời và công bằng đến mọi lính gác trong bán kính nghe. Không một lính gác nào bị "bỏ quên" do nghẽn hàng đợi (Backpressure) và không một sự kiện nào bị nhân bản gây báo động giả.

## Detailed Design

### Core Rules

1. **Quy tắc 1: Cấu trúc Phong bì Bất biến (Immutable EventEnvelope)**
   - Mọi thông điệp trao đổi qua Bus bắt buộc phải được đóng gói trong một cấu trúc phong bì bất biến `EventEnvelope`. Một khi phong bì đã được tạo tại nguồn phát (`publisher`), các trường dữ liệu tiêu đề (Header) và phần thân dữ liệu (`payload`) là chỉ đọc (readonly) và không bao giờ được phép sửa đổi:
     ```csharp
     public readonly struct EventEnvelope
     {
         public readonly SessionId SessionId;          // Định danh phiên chơi toàn cục
         public readonly AttemptEpoch AttemptEpoch;    // Thế hệ kỷ nguyên thử thách hiện tại
         public readonly VirtualTimestamp Timestamp;   // Dấu thời gian ảo xác định
         public readonly PublisherId Publisher;        // Định danh nguồn phát (System / Entity EID)
         public readonly VirtualClockPhase Phase;      // Pha xả sự kiện được chỉ định
         public readonly string OwnerNamespace;        // Không gian tên sở hữu định danh
         public readonly ulong EventIdentity;          // Định danh duy nhất do nguồn phát làm chủ
         public readonly IEvent Payload;               // Dữ liệu sự kiện có kiểu
     }
     ```
   - **Cam kết Định danh Bất biến**: Bus không bao giờ tự ý tạo mới, băm lại (rehash) hoặc cấp lại định danh sự kiện khi có yêu cầu gửi lại (retry). `EventIdentity` phải do chính hệ thống nguồn phát sở hữu và chịu trách nhiệm:
     - Đối với sự kiện âm thanh `NoisePublished`: `EventIdentity = fact_id` do bộ phát `NoiseEmitter` cấp đơn điệu.
     - Đối với sự kiện tủ núp `HideSpot occupied` / `HideSpot empty`: `EventIdentity = transition_id` do chính điểm ẩn nấp cấp đơn điệu khi có sự thay đổi ranh giới chiếm chỗ thực sự.

2. **Quy tắc 2: Cơ chế Khử trùng lặp tại Cổng vào (Ingress Deduplication Table)**
   - Bus duy trì một bảng tra cứu định danh sự kiện đang hoạt động:
     $$\text{DedupKey} = (\text{SessionId}, \text{AttemptEpoch}, \text{OwnerNamespace}, \text{EventIdentity})$$
   - Khi một phong bì đến cổng vào (`Publish(envelope)`):
     - Nếu $\text{DedupKey}$ đã tồn tại trong trạng thái `Admitted`, `HandoffPending`, hoặc `Delivered` của epoch hiện tại: Bus lập tức từ chối và trả về kết quả `event-bus-duplicate-identity`. Tuyệt đối không tạo phong bì thứ hai và không đưa vào hàng đợi.
     - Nếu $\text{DedupKey}$ chưa tồn tại: Sự kiện được coi là hợp lệ lần đầu và được chuyển sang bước kiểm tra dung lượng hàng đợi.

3. **Quy tắc 3: Đường ống 5 Pha Thời gian Ảo Xác định (Virtual-Clock 5-Phase Pipeline)**
   - Trong mỗi chu kỳ tính toán gameplay tick dựa trên đồng hồ ảo (`VirtualClock`), việc điều phối sự kiện được phân chia nghiêm ngặt thành 5 pha kế tiếp nhau. Không bao giờ xảy ra tình trạng xả xen kẽ lộn xộn:
     - **Pha 1 (`Phase 1: Environment & Props`)**: Xả các sự kiện thay đổi trạng thái môi trường, đặc biệt là việc chui vào / rời khỏi tủ núp (`HideSpot occupied` và `HideSpot empty`). Chốt chặn tính che chở trước khi AI tính toán quan sát.
     - **Pha 2 (`Phase 2: Sensing & Noise Publication`)**: Tiếp nhận và chuyển tiếp các sự kiện âm thanh phát sinh (`NoisePublished` từ bước chân di chuyển, va chạm đạn Burst).
     - **Pha 3 (`Phase 3: Perception Evaluation & Relays`)**: Hệ thống giác quan (`Perception`) tiếp nhận âm thanh, tính toán độ suy giảm qua tường/khoảng cách, và xuất bản các sự kiện nhận thức (`NoiseHeard`, `LOSGain`, `LOSLoss`).
     - **Pha 4 (`Phase 4: Guard AI FSM Decision & Navigation`)**: Máy trạng thái lính gác (`Guard AI FSM`) tiêu thụ các sự kiện nhận thức, tính toán chuyển trạng thái (Patrol $\to$ Investigate $\to$ Chase), và điều hướng truy đuổi.
     - **Pha 5 (`Phase 5: Presentation / Telemetry / Grade`)**: Hệ thống tính điểm (`Suspicion Meter / Grade Operator`), giao diện HUD, âm thanh phản hồi và bộ ghi vết Telemetry ghi nhận sự kiện cuối cùng.

4. **Quy tắc 4: Quản lý Hàng đợi Giới hạn & Xử lý Áp lực Ngược (Bounded Queue & Backpressure)**
   - Bus sở hữu duy nhất một hàng đợi phong bì chờ xử lý (`Pending Envelope Queue`) với dung lượng cố định được khóa từ Registry:
     $$C_{\text{bus\_queue}} = 256 \text{ phong bì (dải an toàn } [64, 1024])$$
   - **Xử lý Áp lực Ngược (Deterministic Backpressure)**:
     - Khi hàng đợi đạt ngưỡng tối đa 256 phong bì, phong bì mới chưa được ghi nhận vào bảng Dedup. Hệ thống kích hoạt cơ chế đệm thử lại xác định trong tối đa:
       $$N_{\text{retry\_max}} = 3 \text{ lượt (dải an toàn } [1, 8])$$
     - Phong bì tái sử dụng nguyên vẹn cấu trúc và định danh ban đầu, không làm tăng số thứ tự xuất bản.
   - **Chính sách Tràn hàng đợi (Overflow Policy)**:
     - Nếu sau 3 lượt thử lại mà hàng đợi vẫn không còn chỗ trống, hệ thống thực thi triệt để chính sách `bounded_queue_overflow_policy = reject-newest`:
       - Phong bì cũ trong hàng đợi được bảo toàn tuyệt đối (không bao giờ vứt bỏ sự kiện cũ FIFO).
       - Phong bì mới nhất bị từ chối và kích hoạt mã chẩn đoán nghiêm trọng: `event-bus-queue-overflow-rejected`.
       - Toàn bộ thông tin ngữ cảnh (`session_id`, `attempt_epoch`, `publisher`, `identity`, `queue_depth`) được tuần tự hóa ra log chẩn đoán. Tuyệt đối không bao giờ làm rơi rụng sự kiện ngầm (Zero Silent Drops).

5. **Quy tắc 5: Rào cản Kỷ nguyên và Phiên chơi Cấp nguyên tử (Epoch & Session Barriers)**
   - Khi người chơi chết, bị lính bắt, tải lại màn chơi, hoặc khởi động lại phòng chơi (Full-Room Restart), hệ thống phát lệnh rào cản vòng đời:
     - `BeginSession(new_session_id, initial_epoch)`: Dừng nạp sự kiện mới; vô hiệu hóa thế hệ cũ; thanh lý toàn bộ hàng đợi chờ và bảng tra cứu Dedup; đánh dấu các phong bì chưa kịp xả là `event-bus-stale-session`.
     - `BeginEpoch(session_id, new_epoch)`: Thực hiện tương tự cho bước chuyển kỷ nguyên thử thách mới trong cùng phiên; đánh dấu các phong bì chưa kịp xả là `event-bus-stale-epoch`.
   - Các sự kiện mang nhãn `stale` bị triệt tiêu ngay lập tức, không bao giờ được phép lọt sang pha sau để kích hoạt hành vi AI. Bất kỳ nỗ lực gửi lại (retry) mang `epoch` cũ sẽ bị từ chối ngay tại cổng vào.

6. **Quy tắc 6: Đăng ký Theo Thẻ An toàn & Không Phân bổ Rác (Token-based Subscription & Zero GC Allocation)**
   - Hệ thống áp dụng mẫu đăng ký dựa trên thẻ `SubscriptionToken`:
     ```csharp
     public readonly struct SubscriptionToken : IEquatable<SubscriptionToken>
     {
         public readonly int TokenId;
         public readonly Type EventType;
     }
     ```
   - Khi một hệ thống đăng ký (`Subscribe<T>(handler)`), Bus cấp phát một `SubscriptionToken` và đăng ký delegate vào danh sách lắng nghe tĩnh được định kích thước sẵn (Pre-allocated Array/List).
   - Khi hủy đăng ký (`Unsubscribe(token)`), đối tượng được gỡ bỏ an toàn, bảo đảm không gây memory leak khi `MonoBehaviour` bị tắt (`OnDisable`) hoặc hủy (`OnDestroy`).
   - **Snapshot Isolation**: Khi bắt đầu xả một pha, Bus tạo một bản chụp danh sách người nghe hợp lệ (`Active Listener Snapshot`). Mọi hành vi đăng ký mới hoặc hủy đăng ký diễn ra trong lúc đang gọi callback sẽ chỉ có hiệu lực ở chu kỳ xả kế tiếp, ngăn chặn lỗi sửa đổi tập hợp khi đang duyệt (`CollectionModifiedException`).
   - **Zero GC Allocation**: Các vòng lặp dispatch, đối tượng `EventEnvelope` (struct), và bảng tra cứu không tạo rác trong quá trình chạy liên tục trên cả PC và WebGL.

7. **Quy tắc 7: Giao thức Bàn giao Có kiểu với Perception (Transactional Handoff Contract)**
   - Để ngăn chặn việc hàng đợi thính giác của AI bị nghẽn làm sập hệ thống, Bus thực hiện cơ chế bắt tay 2 chiều khi phân phối sự kiện `NoisePublished`:
     ```text
     Perception.AcceptNoise(envelope) -> Accepted | Duplicate | Retry | Rejected(code)
     ```
     - `Accepted`: Perception đã tiếp nhận thành công vào hàng đợi raw-fact của mình; nghĩa vụ của phong bì đối với người nghe này hoàn tất.
     - `Duplicate`: Perception đã có bản ghi `fact_id` này; hoàn tất nghĩa vụ.
     - `Retry`: Hàng đợi của Perception đang tạm thời đầy; phong bì được giữ lại ở trạng thái `HandoffPending`, tiêu hao ngân sách thử lại của Bus.
     - `Rejected(code)`: Perception từ chối vĩnh viễn; phong bì kết thúc với mã lỗi `event-bus-downstream-handoff-retry-exhausted`. Phong bì chỉ được xóa khỏi hàng đợi khi mọi người nghe trong Snapshot đã hoàn tất xác nhận hoặc gặp lỗi kết thúc.

### States and Transitions

#### 1. Vòng đời Trạng thái Phong bì (EventEnvelope Lifecycle)

| Trạng thái | Điều kiện Kích hoạt | Hành vi Hệ thống | Trạng thái Kế tiếp |
|---|---|---|---|
| **Unvalidated** | Nguồn phát gọi `Publish(envelope)` | Kiểm tra tính hợp lệ của header, timestamp, session và epoch. | `Admitted`, `Rejected` |
| **Admitted** | Vượt qua kiểm tra hợp lệ, không trùng Dedup, hàng đợi còn chỗ | Đưa vào hàng đợi `PendingQueue`, lưu khóa vào bảng Dedup. | `HandoffPending`, `Delivered`, `StaleEpoch` |
| **HandoffPending** | Listener yêu cầu `Retry` (ví dụ: Perception bận) | Giữ phong bì trong hàng đợi, duy trì trạng thái Dedup, thử lại ở tick kế tiếp. | `Delivered`, `Rejected`, `StaleEpoch` |
| **Delivered** | Tất cả người nghe trong Snapshot đã xác nhận `Accepted` hoặc `Duplicate` | Hoàn thành nghĩa vụ phân phối; xóa phong bì khỏi hàng đợi; giữ khóa Dedup đến hết epoch. | *Trạng thái kết thúc* |
| **Rejected** | Hàng đợi tràn sau khi thử lại hoặc listener từ chối dứt điểm | Xuất mã chẩn đoán (`event-bus-queue-overflow-rejected` hoặc `handoff-retry-exhausted`). Xóa phong bì. | *Trạng thái kết thúc lỗi* |
| **StaleEpoch / StaleSession** | Rào cản `BeginEpoch` hoặc `BeginSession` kích hoạt | Vô hiệu hóa phong bì ngay lập tức; dọn sạch hàng đợi. Không gọi callback. | *Trạng thái triệt tiêu an toàn* |

#### 2. Trạng thái Dịch vụ Bus (EventBus Service States)

```text
[ Uninitialized ]
       │
       ▼  Initialize / BeginSession
[ ActiveSession: Running ] ──(BeginEpoch: Respawn/Checkpoint)──► [ EpochTransition: Flush Queue ]
       │                                                                  │
       │                                                                  ▼
       │                                                      [ ActiveSession: New Epoch ]
       ▼  Full-Room Restart / Scene Reload
[ SessionBarrier: Reset Entire Bus ] ──► [ ActiveSession: Running ]
```

### Interactions with Other Systems

| Hệ thống Tương tác | Hướng Giao tiếp | Kiểu Sự kiện Trao đổi | Bản chất Hợp đồng & Ràng buộc |
|---|---|---|---|
| **Player Noise (#3)** | Ingress (Gửi vào Bus) | `NoisePublished` | Bộ phát gửi âm thanh bước chân hoặc đạn Burst. Mang `fact_id` bất biến. Bus không thay đổi `timestamp` hay `fact_id`. |
| **Player Movement & Hide (#5)** | Ingress (Gửi vào Bus) | `HideSpot occupied`, `HideSpot empty` | Gửi khi người chơi chui vào / ra khỏi tủ núp. Mang `transition_id` duy nhất. Xả ở Pha 1 trước khi AI quét tìm kiếm. |
| **Perception Systems (#2)** | Egress & Ingress | Nhận `NoisePublished`; Gửi `NoiseHeard`, `LOSGain`, `LOSLoss` | Nhận âm thanh qua cơ chế `AcceptNoise` (Pha 2-3). Sau khi tính toán lan truyền, gửi lại các sự kiện tri giác đã lọc cho FSM. |
| **Guard AI FSM (#1)** | Egress & Ingress | Nhận `NoiseHeard`, `LOSGain`, `LOSLoss`; Gửi `GuardStateChanged`, `AlertEscalated` | Tiêu thụ sự kiện tri giác ở Pha 4 để chuyển trạng thái điều tra/truy đuổi. Gửi thông báo chuyển trạng thái cho Grade/HUD. |
| **Suspicion Meter / Grade (#7)** | Egress (Lắng nghe) | Nhận `NoiseHeard`, `GuardStateChanged`, `AlertEscalated`, `CheckpointPassed` | Lắng nghe ở Pha 5 để tính toán điểm tích lũy, thời gian phạt và xếp hạng màn chơi. |
| **HUD / UI (#14) & Telemetry (#10)** | Egress (Lắng nghe) | Nhận mọi sự kiện gameplay | Cập nhật thanh nghi ngờ trên màn hình, vẽ chỉ báo hướng âm thanh, và ghi vết chẩn đoán FSM Trace. |

## Formulas

### D1: Vị từ Tiếp nhận & Khử trùng lặp tại Cổng vào (Ingress Admission Predicate)

Vị từ tiếp nhận phong bì tại cổng vào Bus được định nghĩa như sau:

$$\text{AdmitPredicate}(E) = \begin{cases} 
\text{REJECT\_DUPLICATE} & \text{nếu } \text{DedupKey}(E) \in \mathcal{K}_{\text{epoch}} \\
\text{ADMIT\_PENDING} & \text{nếu } \text{DedupKey}(E) \notin \mathcal{K}_{\text{epoch}} \land |\mathcal{Q}_{\text{pending}}| < C_{\text{queue}} \\
\text{BACKPRESSURE\_RETRY} & \text{nếu } \text{DedupKey}(E) \notin \mathcal{K}_{\text{epoch}} \land |\mathcal{Q}_{\text{pending}}| \ge C_{\text{queue}} \land r < N_{\text{retry\_max}} \\
\text{REJECT\_OVERFLOW} & \text{nếu } \text{DedupKey}(E) \notin \mathcal{K}_{\text{epoch}} \land |\mathcal{Q}_{\text{pending}}| \ge C_{\text{queue}} \land r \ge N_{\text{retry\_max}}
\end{cases}$$

Trong đó khóa khử trùng lặp được xác định:
$$\text{DedupKey}(E) = (E.\text{SessionId}, E.\text{AttemptEpoch}, E.\text{OwnerNamespace}, E.\text{EventIdentity})$$

**Variables:**
| Biến số | Ký hiệu | Kiểu | Dải giá trị | Mô tả |
|---|---|---|---|---|
| Khóa định danh phong bì | $\text{DedupKey}(E)$ | 4-tuple | — | Bộ tứ định danh bất biến của phong bì $E$ |
| Bảng khóa kỷ nguyên | $\mathcal{K}_{\text{epoch}}$ | Set<Tuple> | $[0, 4096]$ | Tập hợp các khóa sự kiện đã tiếp nhận trong epoch hiện tại |
| Dung lượng hàng đợi hiện tại | $|\mathcal{Q}_{\text{pending}}|$ | int | $[0, 256]$ | Số lượng phong bì hiện đang chờ xả trong hàng đợi |
| Sức chứa tối đa hàng đợi | $C_{\text{queue}}$ | int | $256$ | Giới hạn dung lượng hàng đợi cố định (`event_bus_pending_envelope_capacity`) |
| Số lần đã thử lại | $r$ | int | $[0, 3]$ | Số lần phong bì đã thực hiện backpressure |
| Giới hạn thử lại tối đa | $N_{\text{retry\_max}}$ | int | $3$ | Ngưỡng thử lại tối đa cho phép (`event_bus_retry_attempts`) |

**Output Range:** Enum kết quả: `ADMIT_PENDING`, `REJECT_DUPLICATE`, `BACKPRESSURE_RETRY`, `REJECT_OVERFLOW`.  
**Ví dụ tính toán:**  
- Khi $E$ có $\text{DedupKey} = (1, 0, \text{"PlayerNoise"}, 1042)$, bảng $\mathcal{K}_{\text{epoch}}$ chưa có khóa này, $|\mathcal{Q}_{\text{pending}}| = 45 < 256$: $\implies \text{ADMIT\_PENDING}$ (Phong bì được nạp vào hàng đợi và thêm khóa vào $\mathcal{K}_{\text{epoch}}$).  
- Khi một listener khác gửi lại cùng phong bì đó trong epoch 0: $\text{DedupKey} \in \mathcal{K}_{\text{epoch}} \implies \text{REJECT\_DUPLICATE}$ (Mã `event-bus-duplicate-identity`, không nạp lại).  
- Khi $|\mathcal{Q}_{\text{pending}}| = 256$ và $r = 1 < 3$: $\implies \text{BACKPRESSURE\_RETRY}$ (Thử lại ở sub-tick tiếp theo).  
- Khi $|\mathcal{Q}_{\text{pending}}| = 256$ và $r = 3 \ge 3$: $\implies \text{REJECT\_OVERFLOW}$ (Mã `event-bus-queue-overflow-rejected`, xuất log chẩn đoán và loại bỏ).

---

### D2: Bộ Tứ Thứ tự Xác định & Phép So sánh Từ điển (Deterministic Ordering Tuple)

Để bảo đảm thứ tự phân phối sự kiện hoàn toàn xác định trên mọi máy chạy (không phụ thuộc vào thứ tự luồng hay thời điểm gọi hàm), thứ tự phân xử giữa hai phong bì $E_1$ và $E_2$ tuân theo phép so sánh từ điển nghiêm ngặt:

$$\text{OrderTuple}(E) = (\Phi, t_{\text{virtual}}, \text{Rank}_{\text{class}}, \text{PublisherSeq})$$
$$E_1 \prec E_2 \iff \text{OrderTuple}(E_1) < \text{OrderTuple}(E_2)$$

Biểu thức so sánh từ điển được khai triển:
$$\begin{aligned}
E_1 \prec E_2 \iff & (\Phi_1 < \Phi_2) \lor \\
& (\Phi_1 == \Phi_2 \land t_1 < t_2) \lor \\
& (\Phi_1 == \Phi_2 \land t_1 == t_2 \land \text{Rank}_1 < \text{Rank}_2) \lor \\
& (\Phi_1 == \Phi_2 \land t_1 == t_2 \land \text{Rank}_1 == \text{Rank}_2 \land \text{Seq}_1 < \text{Seq}_2)
\end{aligned}$$

**Variables:**
| Biến số | Ký hiệu | Kiểu | Dải giá trị | Mô tả |
|---|---|---|---|---|
| Chỉ số pha xả | $\Phi$ | int | $[1, 5]$ | Pha thời gian ảo (`Phase 1` đến `Phase 5`) |
| Dấu thời gian ảo | $t_{\text{virtual}}$ | float (s) | $\ge 0.0$ | Dấu thời gian ảo do đồng hồ `VirtualClock` cấp phát |
| Thứ bậc phân loại sự kiện | $\text{Rank}_{\text{class}}$ | int | $[1, 10]$ | Độ ưu tiên phân loại (ví dụ: `HideSpotEmpty` = 1, `HideSpotOccupied` = 2) |
| Thứ tự đơn điệu nội bộ Bus | $\text{PublisherSeq}$ | ulong | $[0, 2^{64}-1]$ | Bộ đếm tăng dần được gán ngay khi phong bì được Admit vào hàng đợi |

**Output Range:** `true` nếu $E_1$ được xả trước $E_2$; `false` nếu ngược lại.  
**Ví dụ tính toán:**  
- Phong bì $E_{\text{exit}}$ (`HideSpotEmpty`): $\Phi = 1$, $t = 12.400\text{ s}$, $\text{Rank} = 1$, $\text{Seq} = 88$.  
- Phong bì $E_{\text{enter}}$ (`HideSpotOccupied`): $\Phi = 1$, $t = 12.400\text{ s}$, $\text{Rank} = 2$, $\text{Seq} = 89$.  
- So sánh: Cùng $\Phi = 1$, cùng $t = 12.400\text{ s}$, nhưng $\text{Rank}_1 = 1 < \text{Rank}_2 = 2 \implies E_{\text{exit}} \prec E_{\text{enter}}$ (Sự kiện rời tủ được xả trước sự kiện vào tủ cùng tick).

---

### D3: Giải quyết Nghĩa vụ Bàn giao Bắt tay với Perception (Perception Handoff Resolution)

Trạng thái hoàn thành nghĩa vụ phân phối của một phong bì $E$ đối với tập người nghe hợp lệ trong bản chụp $\mathcal{L}_{\text{snapshot}}$ được xác định theo công thức:

$$\text{ObligationStatus}(E, \mathcal{L}_{\text{snapshot}}) = \begin{cases}
\text{COMPLETE\_DELIVERED} & \text{nếu } \forall L \in \mathcal{L}_{\text{snapshot}}, \text{Ack}(L, E) \in \{\text{Accepted}, \text{Duplicate}\} \\
\text{HOLD\_PENDING} & \text{nếu } \exists L \in \mathcal{L}_{\text{snapshot}}, \text{Ack}(L, E) == \text{Retry} \land r_L < N_{\text{retry\_max}} \\
\text{FAILED\_EXHAUSTED} & \text{nếu } \exists L \in \mathcal{L}_{\text{snapshot}}, \text{Ack}(L, E) == \text{Rejected} \lor (\text{Ack}(L, E) == \text{Retry} \land r_L \ge N_{\text{retry\_max}})
\end{cases}$$

**Variables:**
| Biến số | Ký hiệu | Kiểu | Dải giá trị | Mô tả |
|---|---|---|---|---|
| Tập người nghe bản chụp | $\mathcal{L}_{\text{snapshot}}$ | List<IListener> | $[1, 32]$ | Danh sách các listener hợp lệ tại thời điểm bắt đầu drain pha |
| Phản hồi bắt tay từ listener | $\text{Ack}(L, E)$ | Enum | 4 trạng thái | Kết quả trả về từ `AcceptNoise(E)` |
| Số lần thử lại với listener $L$ | $r_L$ | int | $[0, 3]$ | Bộ đếm số lần thử lại đối với listener cụ thể này |
| Giới hạn thử lại tối đa | $N_{\text{retry\_max}}$ | int | $3$ | Ngưỡng thử lại bàn giao tối đa (`event_bus_retry_attempts`) |

**Output Range:** Enum: `COMPLETE_DELIVERED`, `HOLD_PENDING`, `FAILED_EXHAUSTED`.  
**Ví dụ tính toán:**  
- Phong bì âm thanh $E_{\text{burst}}$ có 2 lính gác nghe trong $\mathcal{L}_{\text{snapshot}} = \{G_1, G_2\}$.  
- $G_1$ trả về `Accepted`. $G_2$ (hàng đợi thính giác đang bận) trả về `Retry` với $r_{G2} = 0 < 3$.  
- Kết quả: $\text{ObligationStatus} = \text{HOLD\_PENDING}$ (Phong bì giữ trạng thái chờ trong hàng đợi, sẽ thử lại với $G_2$ ở tick tiếp theo).

---

### D4: Vị từ Thanh lọc Rào cản Kỷ nguyên (Epoch Barrier Invalidation Predicate)

Quy tắc sàng lọc và vô hiệu hóa phong bì còn tồn đọng khi rào cản chuyển kỷ nguyên hoặc khởi động lại phiên chơi kích hoạt $(\Xi_{\text{active}} \to \Xi_{\text{new}})$:

$$\text{EpochFilter}(E, \Xi_{\text{new}}) = \begin{cases}
\text{RETAIN} & \text{nếu } E.\text{AttemptEpoch} == \Xi_{\text{new}} \\
\text{INVALIDATE\_STALE} & \text{nếu } E.\text{AttemptEpoch} < \Xi_{\text{new}} \implies \text{Purge}(E) \land \text{EmitDiagnostic}(\text{stale-epoch})
\end{cases}$$

**Variables:**
| Biến số | Ký hiệu | Kiểu | Dải giá trị | Mô tả |
|---|---|---|---|---|
| Thế hệ kỷ nguyên của phong bì | $E.\text{AttemptEpoch}$ | ulong | $\ge 0$ | Giá trị epoch được đóng dấu trên phong bì $E$ |
| Kỷ nguyên mới vừa kích hoạt | $\Xi_{\text{new}}$ | ulong | $\ge 0$ | Giá trị epoch hợp lệ hiện tại của phiên chơi |

**Output Range:** Enum: `RETAIN`, `INVALIDATE_STALE`.  
**Ví dụ tính toán:**  
- Người chơi bị bắt ở Epoch 2, màn chơi hồi sinh người chơi và gọi `BeginEpoch(session_1, 3)`.  
- Các phong bì $E$ còn nằm trong hàng đợi có $E.\text{AttemptEpoch} = 2 < 3$.  
- Bộ lọc $\text{EpochFilter} \implies \text{INVALIDATE\_STALE}$: Toàn bộ phong bì này bị dọn sạch (Purge) và xuất mã chẩn đoán `event-bus-stale-epoch`. Không một sự kiện cũ nào được phép gọi sang FSM của lính gác.

## Edge Cases

- **E1: Nếu một subscriber ném ra Exception trong quá trình thực thi callback**: Bus bắt giữ (`try-catch`) exception, ghi log lỗi nghiêm trọng (bao gồm đầy đủ metadata của phong bì: `session_id`, `epoch`, `timestamp`, `eventType`, `listenerTarget`), cô lập lỗi hoàn toàn và **tiếp tục phân phối sự kiện cho các listener còn lại** trong bản chụp snapshot. Điều này bảo đảm một lỗi cục bộ ở hệ thống âm thanh hoặc UI không bao giờ làm sập luồng điều phối AI hoặc làm đứng khung hình trò chơi.
- **E2: Nếu một subscriber gọi `Subscribe` hoặc `Unsubscribe` ngay bên trong callback đang chạy (Re-entrant Subscription)**: Nhờ cơ chế bản chụp `Active Listener Snapshot`, danh sách người nghe đang được duyệt qua trong pha hiện tại không bị xáo trộn (ngăn chặn hoàn toàn lỗi `InvalidOperationException: Collection was modified`). Thao tác đăng ký mới hoặc hủy đăng ký được áp dụng vào danh sách gốc và chỉ có hiệu lực bắt đầu từ chu kỳ xả pha kế tiếp.
- **E3: Nếu một subscriber gọi `Publish` một sự kiện mới ngay bên trong callback đang chạy (Re-entrant Publish)**: Phong bì mới được kiểm tra hợp lệ và đưa vào hàng đợi `PendingQueue` cho pha tương ứng của nó. Hệ thống **tuyệt đối không xả đệ quy ngay lập tức**, ngăn chặn nguy cơ tràn ngăn xếp (`StackOverflowException`) và bảo toàn trật tự tuyến tính của đường ống 5 pha thời gian ảo.
- **E4: Nếu rào cản Kỷ nguyên / Phiên chơi (`BeginEpoch` hoặc `BeginSession`) kích hoạt đúng lúc đang có phong bì ở trạng thái `HandoffPending`**: Toàn bộ nghĩa vụ bàn giao đang chờ xử lý lập tức bị hủy bỏ. Phong bì được chuyển trạng thái sang `StaleEpoch` hoặc `StaleSession`, xóa khỏi hàng đợi và xuất log chẩn đoán `event-bus-stale-epoch`. Phía `Perception` không còn nhận sự kiện này nữa, bảo đảm người chơi sau khi hồi sinh không bao giờ bị AI truy lùng bởi âm thanh của kiếp trước.
- **E5: Nếu dung lượng hàng đợi đạt ngưỡng tối đa ($|\mathcal{Q}_{\text{pending}}| \ge 256$) kéo dài qua 3 lượt thử lại (Queue Spike & Saturation)**: Hệ thống thực thi chính sách `bounded_queue_overflow_policy = reject-newest`: phong bì mới nhất bị từ chối dứt điểm, xuất mã lỗi nghiêm trọng `event-bus-queue-overflow-rejected` kèm toàn bộ thông tin ngữ cảnh ra log chẩn đoán, trong khi các phong bì cũ đã nạp trước đó được bảo vệ an toàn 100%. Không bao giờ có hiện tượng ghi đè ngẫu nhiên hay làm rơi rụng sự kiện ngầm (Zero Silent Drops).
- **E6: Nếu nguồn phát gửi một sự kiện có dấu thời gian ảo lùi về quá khứ ($t_{\text{event}} < t_{\text{virtual\_current}} - \epsilon$)**: Phong bì bị từ chối ngay tại cổng vào với mã lỗi `event-bus-retroactive-timestamp-rejected`. Mọi sự kiện gameplay bắt buộc phải tuân thủ tính nhân - quả thời gian đơn điệu không giảm.
- **E7: Nếu nguồn phát gửi lại (retry) một phong bì mà Bus đã tiếp nhận thành công trước đó**: Bảng tra cứu Ingress Dedup phát hiện $\text{DedupKey} \in \mathcal{K}_{\text{epoch}}$, lập tức trả về kết quả `event-bus-duplicate-identity` an toàn. Phong bì không bị đưa vào hàng đợi lần hai và không kích hoạt cảnh báo lỗi hệ thống bất thường.
- **E8: Nếu đối tượng nhận sự kiện (`MonoBehaviour`) bị hủy trong Scene mà quên gọi `Unsubscribe` (Dangling Listener)**: Khi duyệt snapshot để gọi callback, Bus thực hiện kiểm tra tham chiếu đối tượng sống (`target == null` trong Unity Object context). Nếu đối tượng đã bị tiêu hủy (`null`), Bus tự động bỏ qua việc gọi callback, đồng thời gỡ bỏ thẻ `SubscriptionToken` mồ côi này khỏi danh sách quản lý để thu hồi tài nguyên sạch sẽ.

## Dependencies

### 1. Phụ thuộc Ngược dòng (Upstream Dependencies)

Hệ thống Event / Messaging Bus thuộc **Tầng Nền tảng (Foundation Layer)**, tuyệt đối **không phụ thuộc vào bất kỳ hệ thống gameplay cụ thể nào** trong trò chơi. Để bảo đảm tính xác định và khả năng kiểm thử độc lập (Unit Testing), hệ thống chỉ dựa vào một giao diện dịch vụ kỹ thuật duy nhất được tiêm vào (Dependency Injection):
- **Dịch vụ Thời gian Ảo (`IVirtualClock`)**: Cung cấp dấu thời gian ảo đơn điệu không giảm (`VirtualTimestamp`) và nhịp kích hoạt các pha (`VirtualClockPhase`). Tuyệt đối không đọc trực tiếp `Time.time` hoặc `Time.deltaTime` của Unity.

### 2. Phụ thuộc Xuôi dòng (Downstream Dependents)

Hầu hết toàn bộ các hệ thống cốt lõi trong *Whisper Ward* đều phụ thuộc vào Bus để trao đổi dữ liệu phi ghép nối:

| Hệ thống Tiêu thụ | Vai trò Giao tiếp | Hợp đồng Dữ liệu Trao đổi | Ràng buộc Hai chiều |
|---|---|---|---|
| **Player Noise (#3)** | Publisher (Nguồn phát) | `NoisePublished` | Bộ phát chịu trách nhiệm cấp phát `fact_id` bất biến. Bus bảo toàn nguyên vẹn `fact_id` và `timestamp` khi chuyển tiếp cho Perception. |
| **Player Movement & Hide (#5)** | Publisher (Nguồn phát) | `HideSpot occupied`, `HideSpot empty` | Điểm ẩn nấp chịu trách nhiệm cấp phát `transition_id` duy nhất cho mỗi lần đổi trạng thái chiếm chỗ thực sự. Xả ở Pha 1 để chốt ranh giới trước khi AI quét tìm kiếm. |
| **Perception Systems (#2)** | Consumer & Publisher | `NoisePublished` $\to$ `NoiseHeard`, `LOSGain`, `LOSLoss` | Tiêu thụ âm thanh qua giao thức bắt tay hai chiều `AcceptNoise(envelope)`. Sau khi tính toán lan truyền vật lý, xuất bản các sự kiện tri giác vào Pha 3. |
| **Guard AI FSM (#1)** | Consumer & Publisher | `NoiseHeard`, `LOSGain`, `LOSLoss` $\to$ `GuardStateChanged`, `AlertEscalated` | Tiêu thụ sự kiện nhận thức ở Pha 4 để ra quyết định thay đổi trạng thái AI. Xuất bản biến động hành vi cho hệ thống tính điểm và giao diện. |
| **Suspicion Meter / Grade (#7)** | Consumer (Lắng nghe) | `NoiseHeard`, `GuardStateChanged`, `AlertEscalated`, `CheckpointPassed` | Lắng nghe ở Pha 5 để tính toán tích lũy nghi ngờ, trừ điểm phong độ và xác định xếp hạng hoàn thành màn chơi. |
| **Player Controller (#11)** | Consumer (Lắng nghe) | `GuardCatchTriggered`, `SessionReset`, `EpochBarrierTriggered` | Lắng nghe các sự kiện rào cản để khóa điều khiển khi bị tóm hoặc mở lại điều khiển khi hồi sinh. |
| **HUD / UI (#14) & Telemetry (#10)** | Consumer (Lắng nghe) | Tất cả sự kiện gameplay có kiểu | Cập nhật thanh nghi ngờ trên màn hình người chơi và ghi vết tuần tự hóa nhật ký chẩn đoán FSM Trace. |

### 3. Phá vỡ Phụ thuộc Vòng tròn (Breaking Circular Dependencies)

Bus sự kiện đóng vai trò then chốt giải quyết dứt điểm hai vòng lặp phụ thuộc chết người trong thiết kế AI lén lút:
1. **[Guard AI FSM] $\leftrightarrow$ [Alert Propagation]**: Lính gác kích hoạt báo động, và báo động thúc đẩy lính gác khác phản ứng. Nhờ Bus, lính gác chỉ xuất bản sự kiện `AlertEscalated`, và hệ thống lan truyền báo động lắng nghe sự kiện này rồi phát tán tiếp qua Bus; không có tham chiếu trực tiếp giữa hai lớp thực thể.
2. **[Guard AI FSM] $\leftrightarrow$ [Perception]**: FSM cần biết thông tin nhận thức, và Perception cần biết trạng thái cảnh giác của lính gác. Thông qua Bus, Perception đẩy sự kiện `NoiseHeard` / `LOSGain` vào Bus; FSM chỉ đọc dòng sự kiện ở Pha 4, hoàn toàn không gọi truy vấn đồng bộ (Synchronous Query) sang Perception.

## Tuning Knobs

Toàn bộ các tham số điều khiển của hệ thống Event / Messaging Bus là các hằng số kiến trúc hạ tầng được nạp một lần khi khởi động (Boot Configuration thông qua `EventBusSettings` ScriptableObject), hoàn toàn bị khóa (immutable) trong quá trình vận hành gameplay nhằm bảo toàn tính xác định tuyệt đối:

| Tên tham số | Giá trị chuẩn | Dải an toàn | Đơn vị | Tác động Gameplay & Lý do kỹ thuật | Hành vi khi quá cao / quá thấp |
|---|---|---|---|---|---|
| `event_bus_pending_envelope_capacity` | `256` | `[64, 1024]` | phong bì | Sức chứa tối đa của hàng đợi phong bì chờ xả. Đủ lớn để chứa đồng thời các âm thanh bước chân, va chạm Burst và sự kiện đổi trạng thái tủ núp trong 1 tick. | **Quá thấp**: Dễ gây tràn hàng đợi khi xảy ra nhiều tiếng ồn đồng thời.<br>**Quá cao**: Tốn bộ nhớ đệm tĩnh không cần thiết trên WebGL. |
| `event_bus_retry_attempts` | `3` | `[1, 8]` | lượt | Số lần thử lại tối đa khi hàng đợi Bus đầy hoặc khi phía Perception yêu cầu `Retry` trước khi từ chối dứt điểm. | **Quá thấp**: Phong bì dễ bị từ chối sớm khi có đột biến tải ngắn hạn.<br>**Quá cao**: Giữ phong bì chờ quá lâu, làm chậm phản ứng của AI. |
| `bounded_queue_overflow_policy` | `reject-newest` | `enum: reject-newest` | — | Chính sách xử lý khi hàng đợi tràn: luôn bảo vệ sự kiện cũ, chỉ từ chối sự kiện mới nhất và xuất log chẩn đoán đầy đủ. | **Cố định**: Bắt buộc tuân thủ tiêu chuẩn ADR-0001 để bảo đảm không rơi rụng sự kiện ngầm. |
| `max_active_subscribers_per_event` | `32` | `[8, 128]` | subscriber | Giới hạn số lượng người nghe đồng thời cho một kiểu sự kiện để cấp phát sẵn mảng snapshot mà không sinh GC rác. | **Quá thấp**: Không đủ chỗ cho nhiều lính gác cùng nghe.<br>**Quá cao**: Lãng phí bộ nhớ mảng cố định. |
| `ingress_dedup_capacity` | `4096` | `[1024, 16384]` | khóa | Sức chứa tối đa của bảng băm khử trùng lặp `DedupKey` trong suốt một epoch thử thách. | **Quá thấp**: Dẫn đến việc phải tái phân bổ kích thước bảng băm giữa chừng.<br>**Quá cao**: Chiếm dụng bộ nhớ không cần thiết. |
| `retroactive_timestamp_tolerance_s` | `0.001` | `[0.0, 0.010]` | giây | Ngưỡng dung sai thời gian nhỏ cho phép bù trừ sai số trôi dấu phẩy động (`float epsilon`) khi kiểm tra tính thời gian đơn điệu. | **Quá thấp**: Bác bỏ oan các sự kiện gửi cùng tick do sai số epsilon.<br>**Quá cao**: Có thể để lọt các sự kiện xảy ra lùi về quá khứ. |
| `phase_drain_budget_warning_ms` | `5.0` | `[2.0, 16.6]` | mili-giây | Ngưỡng cảnh báo hiệu năng khi tổng thời gian xả 5 pha trong một tick vượt quá ngân sách khung hình, phục vụ tối ưu hóa profiling. | **Quá thấp**: Gây nhiễu cảnh báo profiling giả.<br>**Quá cao**: Bỏ sót các đợt sụt giảm khung hình nghiêm trọng. |

## Visual/Audio Requirements

Hệ thống Event / Messaging Bus là **lớp hạ tầng dữ liệu kỹ thuật thuần túy (Pure Data Layer)**, không trực tiếp sở hữu hoặc hiển thị các tài nguyên đồ họa (mesh, shader, texture) hay tài nguyên âm thanh (audio clip):
- **Vai trò đối với Visual/VFX**: Bus chịu trách nhiệm vận chuyển chính xác dấu thời gian ảo (`timestamp`) và tọa độ phát sinh âm thanh/báo động (`Vector3 origin`) của các sự kiện gameplay (`NoisePublished`, `AlertEscalated`). Hệ thống hiệu ứng hình ảnh (System #21 `VFX / telegraph layer`) lắng nghe các sự kiện này qua Bus để kích hoạt vòng tròn gợn sóng âm thanh (Telegraph Ripples) và chuyển màu nón thị giác của lính gác mà không cần phụ thuộc trực tiếp vào nhân vật người chơi.
- **Vai trò đối với Audio**: Hệ thống âm thanh (System #13 `Audio & UI feedback`) lắng nghe sự kiện từ Bus để kích hoạt âm thanh diegetic (tiếng chân, tiếng vỡ chai đạn Burst) và stinger nhạc nền chuyển tiếp một cách đồng bộ và chính xác.

## UI Requirements

- **Giao diện Người chơi (Player-Facing UI)**: **Không có (None)**. Người chơi không bao giờ nhìn thấy hoặc tương tác trực tiếp với Bus sự kiện trong trải nghiệm gameplay tiêu chuẩn.
- **Bảng Theo dõi Gỡ lỗi Phát triển (Developer Debug Overlay)**:
  - Trong các bản build phát triển (Development / Debug Builds), Bus cung cấp giao diện hiển thị lớp phủ (Debug Overlay) có thể bật/tắt bằng phím tắt `F3` (hoặc lệnh trong bảng điều khiển nội bộ) với các chỉ số đo lường hiệu năng thời gian thực:
    - **Session & Epoch**: Hiển thị `session_id` hiện tại và bộ đếm thế hệ `attempt_epoch`.
    - **Queue Telemetry**: Độ sâu hàng đợi hiện tại $|\mathcal{Q}_{\text{pending}}| / 256$, vạch mức tải đỉnh cao nhất (High-Water Mark) kể từ khi khởi động phòng chơi.
    - **Sự kiện & Bắt tay**: Tổng số sự kiện đã tiếp nhận (`Admitted`), số sự kiện bị loại do trùng lặp (`Duplicate Rejected`), số lần thử lại (`Backpressure Retries`), và số sự kiện bị từ chối do quá tải (`Overflow Rejected`).
    - **Phase Timing**: Thời gian xả thực thi trung bình và tối đa (tính bằng mili-giây) của từng pha trong 5 pha thời gian ảo, cảnh báo màu đỏ nếu vượt quá ngưỡng `phase_drain_budget_warning_ms = 5.0 ms`.

## Acceptance Criteria

- **AC1: Bảo toàn Tính Bất biến của Phong bì (CR1)**  
  **GIVEN** phong bì `EventEnvelope` được khởi tạo tại nguồn phát mang đầy đủ các trường tiêu đề và payload dữ liệu có kiểu,  
  **WHEN** phong bì được đưa qua cổng `Publish(envelope)` và chuyển tiếp đến các listener trong hàm callback,  
  **THEN** toàn bộ các trường dữ liệu `SessionId`, `AttemptEpoch`, `Timestamp`, `Publisher`, `OwnerNamespace`, `EventIdentity` và `Payload` giữ nguyên vẹn 100% không bị đột biến dữ liệu (`bit-exact identity preservation`).

- **AC2: Khử Trùng lặp tại Cổng vào (CR2, D1)**  
  **GIVEN** một phong bì mang khóa $\text{DedupKey} = (S, \Xi, \Omega, I)$ đã được tiếp nhận (`Admitted`) vào hàng đợi trong epoch $\Xi$,  
  **WHEN** một nguồn phát gửi lại chính xác phong bì với cùng bộ tứ $\text{DedupKey}$ đó trong cùng epoch $\Xi$,  
  **THEN** Bus từ chối tiếp nhận, trả về mã `event-bus-duplicate-identity`, không tăng kích thước hàng đợi $|\mathcal{Q}_{\text{pending}}|$, và mỗi listener chỉ nhận được callback đúng 1 lần duy nhất (`Exactly-Once Delivery`).

- **AC3: Trật tự 5 Pha Thời gian Ảo Tuyệt đối (CR3, D2)**  
  **GIVEN** hai sự kiện $E_{\text{props}}$ (`Phase 1: Environment & Props`, ví dụ `HideSpotOccupied`) và $E_{\text{ai}}$ (`Phase 4: Guard AI FSM`) cùng được publish trong 1 gameplay tick,  
  **WHEN** hàm `Drain(virtualClock)` được thực thi theo thứ tự pha,  
  **THEN** toàn bộ listener của $E_{\text{props}}$ hoàn thành callback trước khi bất kỳ listener nào của $E_{\text{ai}}$ được kích hoạt, bất kể thứ tự gọi `Publish()` của hai sự kiện là trước hay sau.

- **AC4: Phân xử Hòa Cùng Pha theo Phép So sánh Từ điển (CR3, D2)**  
  **GIVEN** hai phong bì $E_1, E_2$ thuộc cùng một pha $\Phi = 1$ và cùng dấu thời gian ảo $t_1 = t_2$, trong đó $E_1$ có $\text{Rank}_{\text{class}} = 1$ (`HideSpotEmpty`) và $E_2$ có $\text{Rank}_{\text{class}} = 2$ (`HideSpotOccupied`),  
  **WHEN** tiến hành drain Pha 1,  
  **THEN** listener luôn nhận được $E_1$ trước $E_2$ theo đúng quy tắc so sánh từ điển D2.

- **AC5: Xử lý Áp lực Ngược khi Hàng đợi Đầy (CR4, D1)**  
  **GIVEN** hàng đợi đang chứa đủ 256 phong bì ($|\mathcal{Q}_{\text{pending}}| = 256$),  
  **WHEN** một phong bì mới chưa có trong Dedup được gửi đến Bus,  
  **THEN** Bus giữ phong bì ở trạng thái chờ thử lại (`BACKPRESSURE_RETRY`) trong tối đa $N_{\text{retry\_max}} = 3$ sub-ticks tiếp theo mà không làm tăng dung lượng hàng đợi vượt quá 256.

- **AC6: Tràn Hàng đợi Thực thi Chính sách Reject-Newest (CR4, D1)**  
  **GIVEN** hàng đợi vẫn đạt tối đa 256 phong bì sau 3 lượt thử lại liên tiếp ($r = 3$),  
  **WHEN** nỗ lực thử lại thứ 4 diễn ra,  
  **THEN** Bus từ chối dứt điểm phong bì mới nhất với mã `event-bus-queue-overflow-rejected`, bảo toàn nguyên vẹn 256 phong bì cũ trong hàng đợi, và tuần tự hóa đầy đủ thông tin sự kiện ra log chẩn đoán (Zero Silent Drops).

- **AC7: Thanh lọc Rào cản Kỷ nguyên (CR5, D4)**  
  **GIVEN** có 10 phong bì của Epoch 1 đang nằm trong hàng đợi chờ xả,  
  **WHEN** hàm `BeginEpoch(session_1, 2)` được gọi (do người chơi bị bắt hoặc respawn sang epoch mới),  
  **THEN** toàn bộ 10 phong bì của Epoch 1 lập tức bị xóa sạch khỏi hàng đợi, đánh dấu mã `event-bus-stale-epoch`, bảng Dedup được làm rỗng, và không một callback nào của 10 phong bì này được kích hoạt ở Epoch 2.

- **AC8: Tái thiết Toàn diện qua Rào cản Phiên chơi (CR5)**  
  **GIVEN** Bus đang trong phiên chơi Session 1 với hàng đợi chứa các phong bì dở dang,  
  **WHEN** hàm `BeginSession(session_2, 0)` được gọi (do khởi động lại phòng chơi Full-Room Restart hoặc nạp Scene mới),  
  **THEN** toàn bộ hàng đợi, bảng Dedup, bộ đếm `PublisherSeq` và danh sách listener được đặt lại trạng thái khởi đầu sạch sẽ (`event-bus-stale-session`), không rò rỉ bất kỳ trạng thái nào từ Session 1 sang Session 2.

- **AC9: Hủy Đăng ký Theo Thẻ An toàn (CR6)**  
  **GIVEN** một listener đăng ký lắng nghe sự kiện qua `Subscribe<T>(handler)` và nhận được `SubscriptionToken`,  
  **WHEN** listener gọi `Unsubscribe(token)` trước khi sự kiện $T$ được publish,  
  **THEN** listener đó tuyệt đối không nhận được callback khi sự kiện $T$ được xả.

- **AC10: Bản chụp Phân lập Chống Lỗi Sửa đổi Tập hợp (CR6, E2)**  
  **GIVEN** một listener đang trong hàm callback của Pha 2 gọi lệnh `Subscribe<T>` hoặc `Unsubscribe(token)`,  
  **WHEN** vòng lặp dispatch của Pha 2 đang thực thi,  
  **THEN** Bus hoàn thành toàn bộ vòng lặp mà không ném ngoại lệ `InvalidOperationException: Collection was modified`, và listener mới/hủy chỉ có hiệu lực từ Pha 2 của tick kế tiếp.

- **AC11: Bàn giao Có kiểu với Perception Lưu Giữ HandoffPending (CR7, D3)**  
  **GIVEN** một sự kiện `NoisePublished` được gửi đến 2 lính gác, trong đó Lính 1 trả về `Accepted` và Lính 2 trả về `Retry` (hàng đợi thính giác tạm đầy),  
  **WHEN** kết thúc chu kỳ xả Pha 2,  
  **THEN** phong bì giữ nguyên trạng thái `HandoffPending` trong hàng đợi, không bị xóa, và được kích hoạt thử lại với Lính 2 ở tick kế tiếp mà không gọi lại callback của Lính 1.

- **AC12: Cạn Kiệt Lượt Thử Bàn giao Ghi nhận Thất bại (CR7, D3)**  
  **GIVEN** một phong bì đang `HandoffPending` với Lính 2 tiếp tục nhận phản hồi `Retry` sau 3 lần thử lại ($r = 3$),  
  **WHEN** kết thúc lần thử thứ 3,  
  **THEN** phong bì được kết thúc với mã lỗi `event-bus-downstream-handoff-retry-exhausted`, xóa khỏi hàng đợi và xuất log chẩn đoán lỗi bàn giao.

- **AC13: Cô lập Ngoại lệ trong Callback Người nghe (E1)**  
  **GIVEN** Listener A và Listener B cùng đăng ký sự kiện $T$, trong đó Listener A cố tình ném ngoại lệ `NullReferenceException` trong hàm xử lý,  
  **WHEN** sự kiện $T$ được dispatch,  
  **THEN** Bus bắt giữ ngoại lệ của A, ghi log lỗi chi tiết, và Listener B vẫn nhận được callback hoàn toàn bình thường và chính xác.

- **AC14: Kiểm chứng Không Phân bổ Rác trong Hot-Path (CR6, Tuning Knobs)**  
  **GIVEN** hệ thống vận hành liên tục 1,000 gameplay ticks với lưu lượng 50 sự kiện/tick được Publish, Drain và phân phối đến các listener,  
  **WHEN** tiến hành đo đạc bằng Unity Profiler (`GC.Alloc` tracker),  
  **THEN** tổng dung lượng bộ nhớ rác phát sinh từ các phương thức `Publish`, `Drain`, `AcceptNoise` và bảng Dedup là chính xác **0 bytes** (Zero GC Allocation).

## Open Questions

- **OQ1: Cơ chế Ghi vết Telemetry khi chạy trên WebGL Demo**: Trong bản build WebGL, liệu việc ghi nhật ký chẩn đoán khi có sự kiện lỗi (`event-bus-queue-overflow-rejected`) nên gửi về in-memory circular buffer hay đẩy qua JS bridge `window.console.warn`?  
  *Trạng thái*: Chờ đội ngũ WebGL thống nhất trong Sprint 1; mặc định lưu vào `CircularBuffer<DiagnosticEvent>` dung lượng 128 bản ghi để phục vụ Dev Overlay.
- **OQ2: Mở rộng Đa luồng (Multi-threading / Unity Job System) trong Giai đoạn Mở rộng**: Hiện tại Bus chạy đơn luồng trên Unity Main-Thread để tương thích 100% với WebGL. Khi mở rộng lên quy mô nhiều phòng (>10 lính gác), liệu có cần áp dụng `NativeQueue` trong C# Job System không?  
  *Trạng thái*: Đã phân tích trong ADR-0001; MVP giới hạn ở 1 phòng 1-2 lính gác nên đơn luồng Main-Thread với Zero GC là tối ưu nhất; xem xét đánh giá lại ở cột mốc Target Slice.
