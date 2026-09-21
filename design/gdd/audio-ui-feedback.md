# Audio & UI Feedback

> **Status**: Approved (2026-09-21)
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 1 (Thinking Enemies), Pillar 2 (Fair Mind-Challenge), Pillar 4 (Visible Intelligence)

## Overview

Hệ thống **Audio & UI Feedback** (System `#13`) là lớp hạ tầng trình diễn giác quan (Sensory Presentation Infrastructure) chịu trách nhiệm tổng hợp, định tuyến, xử lý âm học và phân phối toàn bộ phản hồi âm thanh (audio cues), xúc giác (haptics) và hiệu ứng thị giác tức thì (screen feedback) trong *Whisper Ward*. Hệ thống kết nối trực tiếp giữa các sự kiện gameplay trừu tượng (như cam kết bước chân `step_event`, va chạm đạn `Burst`, góc nhìn lính gác `LOSGain/LOSLoss`, và sự tích lũy nghi ngờ `SuspicionAccumulated`) với các giác quan của người chơi, biến thế giới ngầm moonlit thành một môi trường sống động, nghẹt thở và có thể "đọc vị" được hoàn toàn bằng thính giác.

Được xây dựng tuân thủ nghiêm ngặt hợp đồng kiến trúc **`ADR-0003: Audio Virtual Timestamp Boundary`**, hệ thống phân tách rạch ròi giữa thời gian cam kết gameplay ảo (`virtual_cue_request`) và thời điểm thiết bị âm thanh thực sự phát mẫu sóng (`virtual_dsp_onset`), loại bỏ hoàn toàn hiện tượng lệch pha thời gian trong các bản phát lại xác định (deterministic replay). Hệ thống thực thi 4 nguyên tắc kỹ thuật và thiết kế nền tảng:
1. **Phân tách Tuyệt đối Diegetic vs. Non-Diegetic**: Tách biệt rõ ràng giữa âm thanh thế giới (diegetic SFX — tiếng bước chân, tiếng đạn ném va chạm, tiếng lính hô hoán, tiếng động trong tủ núp mà AI có thể nghe hoặc phản ứng) và âm thanh nhận thức người chơi (non-diegetic HUD cues — tiếng tim đập cảnh báo, tiếng vo ve nghi ngờ, stinger phát hiện, tiếng click menu chỉ vang lên trong tâm lý người chơi).
2. **Hợp đồng Âm thanh Bước chân Kép (Dual-Voice Footstep Container)**: Kết hợp hai voice độc lập (`body` đại diện cho trọng lượng cơ thể + `accent` đại diện cho bề mặt tiếp xúc) được hòa trộn mượt mà theo vận tốc di chuyển và chất liệu sàn (`PhysicMaterial`), kích hoạt chính xác theo frame cam kết của sổ cái chuyển động thay vì bám vào hoạt ảnh lỏng lẻo.
3. **Báo động Trước Sải chân (`rustle_event`)**: Phát tín hiệu âm thanh cọ xát vải vóc tại ngưỡng $70\%$ sải chân để người chơi nhận biết thời điểm sắp phát ra tiếng động thực sự, củng cố trải nghiệm "Thách thức Trí tuệ Công bằng" (Pillar 2).
4. **Bảo vệ Mức Âm Lượng & Triệt Tiêu Âm Thanh Lỗi Thời**: Đáp ứng tiêu chuẩn bộ giới hạn đầu ra (`limiter_stock_desktop` $\le 0\text{ dBFS}$ với dung sai khởi phát $\le 22\text{ ms}$ trên Desktop; `limiter_stock_webgl` nén mềm trên WebGL), đồng thời huỷ bỏ tức thì (`cancel-before-presentation`) toàn bộ âm thanh của kỷ nguyên cũ khi người chơi chết hoặc reset màn chơi.

## Player Fantasy

Hệ thống Audio & UI Feedback kiến tạo một không gian âm thanh mang sắc thái **"Rình Rập Trong Yên Lặng Lạnh Lùng" (The Cold Watch)**. Trong cơ sở tuần tra ngập tràn ánh trăng của *Whisper Ward*, âm thanh không phải là những tiếng giật gân (jump-scare) rẻ tiền, mà là nhịp thở của chính tòa nhà và là chiếc la bàn sinh tồn tối thượng của người chơi. Mọi cử động, mọi rung chấn và mọi sự chú ý của kẻ địch đều được truyền tải qua các giác quan với sự chân thực, sắc nét và công bằng tuyệt đối.

Hệ thống hiện thực hóa trực tiếp 3 trụ cột trải nghiệm cảm xúc:

1. **Nghe Thấy Trí Tuệ Của Kẻ Địch (Pillar 1: Thinking Enemies)**:
   - **Âm thanh Chứng minh Kẻ địch Đang Suy Nghĩ**: Người chơi không cần nhìn thấy lính gác mà vẫn có thể "nhìn bằng tai" (acoustical mapping). Tiếng đế giày nện đều đặn trên sàn bê tông đột ngột khựng lại; tiếng xoay người sột soạt của áo giáp; tiếng lẩm bẩm ngờ vực khi lính chuyển sang trạng thái điều tra; và tiếng hô đanh thép khi phát hiện mục tiêu. Mỗi biến chuyển tâm lý của AI đều có âm thanh tương ứng, khẳng định người chơi đang đối đầu với những thực thể có tư duy sống động.
   - **Sự Bóp Nghẹt Âm Học Trong Điểm Nấp**: Khi chui vào tủ đồ hay gầm bàn (`HideSpot`), âm thanh môi trường xung quanh bị bóp nghẹt qua dải lọc tần số thấp (Low-Pass Filter). Tiếng bước chân nặng nề của lính gác tiến sát bên ngoài cánh cửa tủ, kèm tiếng kim loại kêu lách cách và ánh đèn pin quét qua khe thông gió, biến không gian ẩn náu thành một chiếc lồng nghẹt thở.

2. **Cảnh Báo Công Bằng & Nhịp Điệu Sinh Tồn (Pillar 2: Fair Mind-Challenge)**:
   - **Tín hiệu Cảnh báo Trước Sải Chân (`rustle_event`)**: Người chơi không bao giờ bị "bất ngờ" bởi tiếng động do chính mình tạo ra. Tại ngưỡng $70\%$ sải bước, tiếng cọ xát vải vóc vang lên êm dịu, báo hiệu rằng: *"Nếu bạn không hạ tốc độ hoặc chuyển sang ngồi cúi, frame tiếp theo sẽ là một tiếng bước chân vang vọng"*. Âm thanh đóng vai trò là một người thầy hướng dẫn công bằng, trao quyền làm chủ nhịp điệu di chuyển cho người chơi.
   - **Tiếng Nhịp Tim Trong Khoảng Xác Nhận (Confirm Window)**: Khi người chơi rơi vào tầm nhìn của lính, tiếng tim đập trầm đục dồn dập (Heartbeat thump) vang lên trong tai với nhịp độ tăng dần. Nó không che lấp âm thanh bước chân mà hòa quyện thành một đồng hồ đếm ngược bằng âm thanh, thúc giục người chơi phải lướt nhanh vào góc khuất trước khi quá muộn.

3. **Minh Bạch Giác Quan Giữa Thế Giới & Nhận Thức (Pillar 4: Visible & Audible Intelligence)**:
   - **Niềm Tin Vào Luật Chơi**: Người chơi hoàn toàn tin tưởng vào sự phân định rạch ròi: tiếng động nào là vật lý (diegetic — lính nghe được và sẽ phản ứng) và tín hiệu nào là nhận thức hỗ trợ (non-diegetic — chỉ vang lên trong tai người chơi để hỗ trợ ra quyết định). Không có sự mơ hồ, không có những cái chết oan ức vì âm thanh không rõ nguồn gốc.

## Detailed Design

### Core Rules

1. **Kiến trúc Audio Mixer & Định tuyến Bus (4-Bus Hierarchy)**:
   - Toàn bộ tín hiệu âm thanh trong trò chơi được định tuyến qua cây bus phân cấp nghiêm ngặt trong `AudioMixer`, đứng đầu là `Master Bus`:
     - **`Master Bus`**: Kiểm soát âm lượng tổng thể; gắn bộ giới hạn đầu ra phần cứng (Output Limiter) tuân thủ tiêu chuẩn `limiter_stock_desktop` (trần đỉnh $\le 0\text{ dBFS}$, không méo tiếng) trên Windows Desktop và `limiter_stock_webgl` (bộ nén mềm soft-knee browser limiter) trên trình duyệt WebGL.
     - **`SFX_Diegetic` (3D Spatial Audio Bus)**: Tuyến âm thanh phát sinh trong thế giới vật lý của game (lính gác và thế giới có thể tương tác/phản ứng):
       - Sub-bus `PlayerLocomotion`: Chứa âm thanh bước chân kép (`body` + `accent`) và âm thanh cọ xát quần áo `rustle_event`.
       - Sub-bus `Projectiles`: Chứa âm thanh vung tay ném đạn Burst, tiếng rít xé gió khi bay (in-flight projectile hiss), và tiếng va chạm nổ bục khi tiếp đất (`landing impact`).
       - Sub-bus `GuardAudio`: Chứa tiếng đế giày tuần tra của lính, tiếng xoay người áo giáp, tiếng lẩm bẩm ngờ vực, và tiếng hô hoán khi phát hiện mục tiêu.
     - **`Feedback_NonDiegetic` (2D Screen Audio Bus)**: Tuyến âm thanh tâm lý và tín hiệu chỉ báo (chỉ vang lên trong nhận thức người chơi, tuyệt đối không tạo ra rung chấn vật lý trong thế giới game):
       - Sub-bus `SuspicionCues`: Tiếng vo ve điện từ trầm (low-frequency drone), nhịp tim dồn dập trong Confirm Window (heartbeat), tiếng chuông stinger kim loại sắc nhọn khi bị Chase, và tiếng xả hơi nhẹ (air release whoosh) khi cắt đuôi tầm nhìn thành công.
       - Sub-bus `UI_Haptics`: Tiếng lách cách khi tương tác menu/HUD, tiếng buzzer trầm báo hiệu thao tác bị cấm (như ném đạn khi đang chạy hoặc trần quá thấp), và các xung vi chấn màn hình.
     - **`Ambience` (Environmental Tone Bus)**: Âm thanh nền của cơ sở nghiên cứu moonlit (tiếng gió rít qua khe cửa sổ, tiếng rù rì của hệ thống thông gió). Tự động giảm âm lượng (Ducking $-6\text{ dB}$) trong khoảng thời gian Chase Stinger kích hoạt.
     - **`Music` (Dynamic Tension Stems Bus)**: Lớp nhạc nền thích ứng (Adaptive Music Stems) chuyển tầng theo trạng thái FSM của lính gác (Patrol $\to$ Investigate $\to$ Chase).

2. **Hợp đồng Ranh giới Dấu thời gian Ảo (`ADR-0003: Audio Virtual Timestamp Boundary`)**:
   - Mọi yêu cầu phát âm thanh (Audio Cue Request) từ gameplay đều mang đầy đủ các trường dữ liệu bắt buộc:
     - `virtual_cue_request`: Dấu thời gian ảo của sự kiện gameplay (`VirtualTimestamp`).
     - `voice_instance_id`: Định danh duy nhất của thể hiện voice đang phát từ audio pool.
     - `dsp_start_sample`: Chỉ số mẫu tuyệt đối tại tầng DSP phần cứng do bộ điều hợp âm thanh ghi nhận.
   - Thời điểm khởi phát DSP được quy đổi về miền thời gian ảo theo công thức:
     $$\text{virtual\_dsp\_onset} = \left(\frac{\text{dsp\_start\_sample}}{\text{sample\_rate}}\right) + \text{epoch\_offset}$$
   - Một cue chỉ được coi là hợp lệ (`confirmed`) khi dung sai thời gian thỏa mãn:
     $$|\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| \le \text{audio\_cue\_onset\_tolerance\_ms} \quad (22\text{ ms})$$
   - Tuyệt đối cấm sử dụng `AudioSource.timeSamples` tương đối làm bằng chứng khởi phát; nếu không có thiết bị DSP báo cáo, trạng thái được ghi nhận là `unsupported` thay vì gán giá trị giả lập.

3. **Hợp đồng Âm thanh Bước chân Kép (Dual-Voice Footstep Container)**:
   - Âm thanh bước chân được kích hoạt frame-exact tại thời điểm sự kiện `step_event` được cam kết vào sổ cái di chuyển của `PlayerController`, loại bỏ hoàn toàn sự phụ thuộc vào sự kiện hoạt ảnh (animation events).
   - Mỗi bước chân kích hoạt một cấu trúc bộ chứa âm thanh kép (Logical Blend Container) gồm 2 voice đồng thời:
     - **Voice `body`**: Dải tần số thấp (60–250 Hz) phản ánh trọng lượng và lực dẫm của cơ thể (nhỏ nhẹ khi Crouch, chắc nịch khi Walk, mạnh bạo khi Run).
     - **Voice `accent`**: Dải tần số trung-cao (500–4000 Hz) phản ánh độ ma sát và tính chất của bề mặt sàn (`PhysicMaterial`: Bê tông nhám `Concrete`, Sàn gỗ rỗng `Wood`, Tấm kim loại `Metal`, Thảm cách âm `Carpet`).
   - Hai voice được hòa trộn mượt mà theo vận tốc thực tế $v$ và điều chỉnh âm lượng tổng thể theo công thức chuẩn hóa, bảo đảm mức đỉnh không bao giờ vượt quá ngưỡng bộ giới hạn $\le 0\text{ dBFS}$.

4. **Tín hiệu Cảnh báo Trước Sải chân (`rustle_event`)**:
   - Khi người chơi di chuyển ở chế độ Walk hoặc Run, quãng đường sải chân liên tục tích lũy trong sổ cái chuyển động.
   - Khi sải chân đạt tỷ lệ `rustle_threshold = 0.70 [0.60, 0.80]` của sải chân đã chốt, hệ thống phát một âm thanh cọ xát vải vóc êm dịu (`rustle_event`).
   - `rustle_event` là âm thanh cục bộ người chơi (player-local, audio-only), hoàn toàn không tạo ra sự kiện `NoisePublished`, đóng vai trò là "đồng hồ đếm nhịp bằng âm thanh" báo trước cho người chơi thời điểm sắp phát ra tiếng động bước chân thực sự.

5. **Quản lý Pool Kênh Âm thanh & Chính sách Thu hồi (Voice Pooling & Priority Stealing)**:
   - Hệ thống duy trì một bộ đệm tĩnh gồm $N_{\text{voices}} = 24$ kênh `AudioSource` được cấp phát trước ngay khi khởi tạo (Zero Allocations trong quá trình chơi game).
   - Phân cấp 3 tầng ưu tiên thu hồi kênh âm thanh (Voice Stealing Hierarchy):
     - **Tier 1 (Critical - Priority 0)**: Chase Stinger, Nhịp tim trong Confirm Window, Va chạm nổ đạn Burst, Tín hiệu Menu khẩn cấp. **Bất khả xâm phạm** (tuyệt đối không bị ngắt quãng).
     - **Tier 2 (Important - Priority 1)**: Bước chân người chơi, tiếng bước chân lính gác trong bán kính nguy hiểm $\le 8\text{ m}$, tiếng hô cảnh báo của lính.
     - **Tier 3 (Ambient/Incidental - Priority 2)**: Tiếng cọ xát sột soạt `rustle_event`, tiếng bước chân lính ở xa $> 8\text{ m}$, tiếng máy móc môi trường xa xôi.
   - Khi toàn bộ 24 kênh đều đang bận và có yêu cầu phát âm thanh mới:
     - Hệ thống tìm kiếm và thu hồi (steal) kênh âm thanh ở **Tier 3** có âm lượng cảm nhận nhỏ nhất hoặc có thời gian đã phát lâu nhất (`Oldest/Quietest Voice Stealing`).
     - Nếu không có kênh Tier 3 khả dụng, hệ thống mới xét đến Tier 2 có âm lượng nhỏ hơn âm thanh mới. Tuyệt đối không bao giờ ngắt kênh Tier 1.

6. **Âm học Môi trường & Bộ Lọc Tần số Thích ứng (Acoustic Occlusion & Enclosure LPF)**:
   - **Xử lý Khi Ẩn náu Trong Tủ/Gầm bàn (`HideSpot.IsOccupied == true`)**:
     - Kích hoạt bộ lọc thông thấp (Audio Low-Pass Filter) trên toàn bộ bus `SFX_Diegetic` với tần số cắt $f_{\text{cutoff}} = 800\text{ Hz}$ và cộng hưởng nhẹ $Q = 1.2$.
     - Toàn bộ âm thanh bước chân lính, tiếng gõ cửa ngoài tủ bị bóp nghẹt trầm đục, kết hợp tiếng thở dồn nén nội tâm (internal breathing) đưa vào bus `Feedback_NonDiegetic`.
   - **Xử lý Khi Âm thanh Bị Chắn bởi Tường/Vật cản E20 Solid**:
     - Thực hiện kiểm tra tia vật lý `Physics.Linecast(listener_pos, sound_pos, LayerMask_E20_World, QueryTriggerInteraction.Ignore)`.
     - Nếu tia bị cản bởi vật thể E20 có bề dày $T_{\text{wall}} \ge 0.10\text{ m}$: áp dụng bộ lọc thông thấp $f_{\text{cutoff}} = 1200\text{ Hz}$ và giảm âm lượng $-4\text{ dB}$ trên AudioSource tương ứng, mô phỏng chân thực sự suy hao qua tường gạch bê tông.

7. **Phản hồi Xúc giác Màn hình & Tín hiệu Thị giác Tức thì (Screen Feedback & Haptics)**:
   - **Chase Vignette**: Khi bước vào trạng thái Chase, bốn góc màn hình nhấp nháy lớp viền mờ màu hổ phách/đỏ cam với chu kỳ xung nhịp $0.3\text{ s}$, tăng độ cảnh báo không gian.
   - **Detection Flash**: Chớp sáng viền nhẹ màu vàng hổ phách trong $0.15\text{ s}$ khi lính bắt đầu khóa tầm nhìn trong Confirm Window ($A^* \ge T_{\text{entry}}^*$).
   - **Camera Micro-Shake (Rung chấn Nhẹ)**: Khi đạn Burst phát nổ va chạm mặt sàn trong bán kính $6\text{ m}$, kích hoạt xung rung máy ảo biên độ $0.05\text{ m}$, thời lượng $0.15\text{ s}$ suy giảm theo hàm mũ, tạo lực va đập cơ học rõ rệt.

8. **Rào cản Vòng đời Kỷ nguyên & Đóng băng khi Tạm dừng (Lifecycle & Pause Contract)**:
   - **Kỷ nguyên Mới (`BeginEpoch` / Respawn / Restart)**:
     - Toàn bộ các cue âm thanh đang chờ phát hoặc đang hoãn trong hàng đợi lập tức bị hủy bỏ (`cancel-before-presentation`).
     - Tắt ngay lập tức các tiếng còi Chase stinger hoặc nhịp tim đang dở dang; thiết lập lại bộ trộn âm thanh (AudioMixer Snapshot) về trạng thái mặc định tĩnh lặng `Normal_Stealth`.
   - **Tạm dừng Trò chơi (`Game Pause`)**:
     - Đóng băng thời gian ảo; tạm dừng (Pause) toàn bộ các kênh `AudioSource` đang phát trên thế giới.
     - Thời gian ảo không trôi qua trong lúc tạm dừng; các deadline âm thanh không bị cạn kiệt; khi Resume, trò chơi tiếp tục mượt mà mà không xảy ra tình trạng "xả ào ạt" (burst dump) âm thanh tồn đọng.

### States and Transitions

Trình quản lý trạng thái âm thanh môi trường (`AudioManagerPresenter`) vận hành theo một Máy trạng thái hữu hạn (FSM) gồm 5 trạng thái cấp cao:

```text
       ┌───────────────────────────────┐
       │      1. Normal_Stealth        │◄────────────────────────┐
       │ (Ambience 100%, Drone 0%)     │                         │
       └──────────────┬────────────────┘                         │
                      │ A* > 0                                   │
                      ▼                                          │
       ┌───────────────────────────────┐                         │
       │     2. Suspicion_Elevated     │                         │
       │ (Drone dâng dần, Heartbeat)   │                         │
       └───────┬───────────────┬───────┘                         │
               │               │                                 │
     A* == 0   │               │ FSM Chase Trigger               │ LOS Lost &
  (Meter Clean)│               ▼                                 │ Escaped
               │       ┌───────────────────────────────┐         │
               │       │        3. Chase_Active        │─────────┘
               │       │ (Ducking -6dB, Stinger, Stem) │
               │       └───────────────┬───────────────┘
               │                       │
               ▼                       │
       ┌───────────────────────────────┴─────────────────────────┐
       │                   4. Enclosed_Hiding                    │
       │       (HideSpot Occupied: LPF 800Hz, Muffled Breath)     │
       └───────────────────────────────┬─────────────────────────┘
                                       │
                      Player Death / Room Restart
                                       ▼
       ┌─────────────────────────────────────────────────────────┐
       │                    5. Epoch_Reset                       │
       │         (Queue Purge, Snap to Normal_Stealth)           │
       └─────────────────────────────────────────────────────────┘
```

1. **`Normal_Stealth`**: Trạng thái tuần tra êm đềm bình thường. Ambience phát $100\%$, Suspicion Drone tắt ($0\%$), âm thanh bước chân và tiếng gió rõ nét.
2. **`Suspicion_Elevated`**: Khi có lính nhìn thấy người chơi ($A^* > 0$). Kích hoạt tiếng vo ve điện từ `Suspicion Drone` với âm lượng và cao độ tăng tuyến tính theo $A^* / T_{\text{chase}}$. Nếu $A^* \ge T_{\text{entry}}^*$ (Confirm Window), kích hoạt thêm tiếng nhịp tim đập `Heartbeat` ($120\text{ BPM}$).
3. **`Chase_Active`**: Khi lính phát hiện hoàn toàn và chuyển sang rượt đuổi. Kích hoạt tiếng còi đanh thép `Chase Stinger` ($1$ lần duy nhất), giảm Ambience xuống $-6\text{ dB}$, hòa trộn lớp nhạc dồn dập `Chase Stem`, và bật hiệu ứng viền màn hình `Chase Vignette`.
4. **`Enclosed_Hiding`**: Kích hoạt khi người chơi chui vào `HideSpot` (`Occupied`). Áp dụng bộ lọc Low-Pass Filter $800\text{ Hz}$ lên toàn bộ SFX thế giới bên ngoài tủ; kích hoạt tiếng thở nhẹ dồn nén nội tâm.
5. **`Epoch_Reset`**: Kích hoạt tức thì khi người chơi chết, bị bắt hoặc khởi động lại màn chơi (`attempt_epoch` tăng). Xóa sạch toàn bộ hàng đợi âm thanh, ngắt mọi loop âm thanh cảnh báo, và đưa AudioMixer trở về `Normal_Stealth`.

### Interactions with Other Systems

1. **Tương tác với Event / Messaging Bus (`#15`)**:
   - Hệ thống đăng ký nhận các sự kiện gameplay bất biến thông qua `SubscriptionToken` trong pha thích hợp của đồng hồ thời gian ảo (`VirtualClockPhase`):
     - Pha 1 (`Environment & Props`): Nhận sự kiện `HideSpotChanged` để bật/tắt bộ lọc LPF $800\text{ Hz}$.
     - Pha 2 (`Sensing & Noise`): Nhận `NoisePublished` và `step_event` để kích hoạt âm thanh bước chân và đạn ném.
     - Pha 3 & 4 (`Perception & Guard FSM`): Nhận `LOSGain`, `LOSLoss`, `StateTransition` (Patrol $\to$ Investigate $\to$ Chase) để điều phối Stinger và tiếng hô hoán của lính.
     - Pha 5 (`Presentation / Grade`): Nhận `SuspicionUpdated` và `GradeScoreAccrued` để điều tiết âm thanh nhịp tim và tiếng chuông thành tích.

2. **Tương tác với Player Third-Person Controller (`#11`) & Input System (`#19`)**:
   - Nhận tín hiệu cam kết sải bước (`step_event`) và tín hiệu báo trước sải bước (`rustle_event`) từ controller.
   - Nhận trạng thái di chuyển (Crouch / Walk / Run) và góc xoay camera để tính toán vị trí tai nghe định hướng (Audio Listener).
   - Nhận tín hiệu thao tác bị chặn từ Input System (như cấm ném khi đang chạy hoặc trần quá thấp) để phát âm thanh buzzer cảnh báo.

3. **Tương tác với Player Noise (`#3`) & Physics Config (`#18`)**:
   - Nhận sự kiện ném đạn `Burst`: phát âm thanh vung tay khi đạn rời tay; phát tiếng rít đạn bay theo tọa độ đạn; phát tiếng nổ va chạm khi nhận sự kiện va chạm World hợp lệ.
   - Sử dụng lớp vật lý `LayerMask_E20_World` và chính sách `QueryTriggerInteraction.Ignore` của Physics GDD để thực hiện Linecast kiểm tra che khuất âm học (Occlusion) xuyên tường.

4. **Tương tác với Suspicion Meter & Grade Operator (`#7`)**:
   - Nhận giá trị chuẩn hóa $A^* / T_{\text{chase}}$ để điều biến âm lượng và tần số của `Suspicion Drone`.
   - Nhận cờ kích hoạt `ConfirmWindowActive` để bật tiếng nhịp tim `Heartbeat` đồng bộ với mũi tên chỉ hướng `DirectionalChevron`.
   - Nhận sự kiện kết thúc màn chơi (`GradeCertified`) để phát tiếng chuông tổng kết thứ hạng (S/A/B rank stingers).

## Formulas

### D1: Tăng ích Hòa trộn Bước chân Kép (Dual-Voice Footstep Gain & Velocity Crossfade)

Hệ thống tính toán hệ số tăng ích độc lập cho voice `body` và voice `accent` dựa trên vận tốc di chuyển thực tế của người chơi $v$:

1. **Chuẩn hóa Vận tốc Di chuyển ($\bar{v}$)**:
   $$\bar{v} = \text{clamp}\left(\frac{v - V_{\text{crouch}}}{V_{\text{run}} - V_{\text{crouch}}}, 0.0, 1.0\right)$$
   - Với $V_{\text{crouch}} = 1.80\text{ m/s}$, $V_{\text{run}} = 6.25\text{ m/s}$ (lấy từ sổ bộ `entities.yaml`).

2. **Hệ số Tăng ích Voice Body ($G_{\text{body}}$)** (Tuyến tính - Linear):
   $$G_{\text{body}} = G_{\text{body\_min}} + \bar{v} \cdot (G_{\text{body\_max}} - G_{\text{body\_min}})$$
   - Khởi tạo mặc định: $G_{\text{body\_min}} = 0.20$, $G_{\text{body\_max}} = 0.85$.

3. **Hệ số Tăng ích Voice Accent ($G_{\text{accent}}$)** (Phi tuyến bình phương - Quadratic):
   $$G_{\text{accent}} = K_{\text{surface}} \cdot \left[G_{\text{accent\_min}} + \bar{v}^2 \cdot (G_{\text{accent\_max}} - G_{\text{accent\_min}})\right]$$
   - Khởi tạo mặc định: $G_{\text{accent\_min}} = 0.15$, $G_{\text{accent\_max}} = 0.90$.
   - $K_{\text{surface}}$ là hệ số phản xạ âm của chất liệu mặt sàn (`PhysicMaterial`):
     - `Carpet` (Thảm nhung/nỉ): $K_{\text{surface}} = 0.35$ (tiêu âm, giảm ma sát cao tần).
     - `Wood` (Sàn gỗ rỗng): $K_{\text{surface}} = 0.80$ (cộng hưởng ấm).
     - `Concrete` (Bê tông nhám): $K_{\text{surface}} = 1.00$ (chuẩn mặc định cơ sở).
     - `Metal` (Tấm tôn/lưới sắt): $K_{\text{surface}} = 1.25$ (đanh sắc, ma sát cao tần mạnh).

4. **Kiểm soát Ngưỡng Đỉnh (Peak Normalization)**:
   $$\text{Gain}_{\text{master\_footstep}} = \frac{1}{\max(1.0, G_{\text{body}} + G_{\text{accent}})}$$
   - Bảo đảm tổng biên độ âm thanh không bao giờ vượt quá ngưỡng méo tiếng $0\text{ dBFS}$ trước khi qua Output Limiter.

- **Dải giá trị đầu ra (Output Range)**: $G_{\text{body}} \in [0.20, 0.85]$, $G_{\text{accent}} \in [0.05, 1.125]$.
- **Ví dụ tính toán (Worked Example)**:
  - Trường hợp 1: Người chơi đi bộ ($v = 3.60\text{ m/s}$) trên sàn bê tông ($K_{\text{surface}} = 1.00$):
    - $\bar{v} = (3.60 - 1.80) / (6.25 - 1.80) = 1.80 / 4.45 \approx 0.4045$.
    - $G_{\text{body}} = 0.20 + 0.4045 \times (0.85 - 0.20) = 0.20 + 0.2629 = 0.4629$.
    - $G_{\text{accent}} = 1.00 \times [0.15 + 0.4045^2 \times (0.90 - 0.15)] = 0.15 + 0.1636 \times 0.75 = 0.2727$.
    - Tổng biên độ $= 0.4629 + 0.2727 = 0.7356 < 1.0$ (không bị clamp, âm thanh bước đi đầm chắc, tiếng đế giày vừa phải).
  - Trường hợp 2: Người chơi chạy nhanh ($v = 6.25\text{ m/s}$) trên sàn kim loại ($K_{\text{surface}} = 1.25$):
    - $\bar{v} = 1.00$.
    - $G_{\text{body}} = 0.85$.
    - $G_{\text{accent}} = 1.25 \times 0.90 = 1.125$.
    - Tổng biên độ $= 0.85 + 1.125 = 1.975 > 1.0 \implies \text{Gain}_{\text{master\_footstep}} = 1 / 1.975 \approx 0.506$. Cân bằng đỉnh an toàn tại $0\text{ dBFS}$.

---

### D2: Ranh giới Dấu thời gian DSP & Vị từ Kiểm định Khởi phát (`ADR-0003`)

Quy đổi thời điểm khởi phát mẫu sóng vật lý tại tầng phần cứng âm thanh về trục thời gian ảo của gameplay:

1. **Công thức Quy đổi Thời điểm Khởi phát DSP ($\text{virtual\_dsp\_onset}$)**:
   $$\text{virtual\_dsp\_onset} = \left(\frac{\text{dsp\_start\_sample}}{\text{sample\_rate}}\right) + \text{epoch\_offset}$$
   - Trong đó:
     - `dsp_start_sample`: Vị trí mẫu âm thanh tuyệt đối do Audio Adapter ghi nhận ($\text{samples}$).
     - `sample_rate`: Tần số lấy mẫu âm thanh phần cứng ($44100\text{ Hz}$ hoặc $48000\text{ Hz}$).
     - `epoch_offset`: Mốc thời gian ảo tương ứng với mẫu số 0 của phiên chơi ($\text{giây}$).

2. **Vị từ Kiểm định Khởi phát Âm thanh ($\text{OnsetOutcome}$)**:
   $$\text{OnsetOutcome}(C) = \begin{cases}
   \text{CONFIRMED} & \text{if } |\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| \le \tau_{\text{onset\_tol}} \land \text{dsp\_start\_sample} \ne \text{null} \\
   \text{REJECTED\_JITTER} & \text{if } |\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| > \tau_{\text{onset\_tol}} \land \text{dsp\_start\_sample} \ne \text{null} \\
   \text{UNSUPPORTED} & \text{if } \text{dsp\_start\_sample} == \text{null} \lor \text{sample\_rate} == 0
   \end{cases}$$
   - Trong đó $\tau_{\text{onset\_tol}} = \text{audio\_cue\_onset\_tolerance\_ms} = 0.022\text{ s}$ ($22\text{ ms}$, đã chốt trong `entities.yaml`).

- **Dải giá trị đầu ra (Output Range)**: `OnsetOutcome` $\in \{\text{CONFIRMED}, \text{REJECTED\_JITTER}, \text{UNSUPPORTED}\}$.
- **Ví dụ tính toán (Worked Example)**:
  - Tại frame cam kết `step_event`, gameplay ghi nhận `virtual_cue_request = 14.5200 s`.
  - Bộ điều hợp âm thanh báo cáo: `dsp_start_sample = 697824`, `sample_rate = 48000 Hz`, `epoch_offset = 0.0000 s`.
  - $\text{virtual\_dsp\_onset} = (697824 / 48000) + 0.0 = 14.5380\text{ s}$.
  - Sai lệch: $|\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| = |14.5380 - 14.5200| = 0.0180\text{ s} = 18.0\text{ ms}$.
  - Vì $18.0\text{ ms} \le 22.0\text{ ms}$, kết quả thẩm định là **`CONFIRMED`** (đạt chứng chỉ âm thanh đồng bộ xác định).

---

### D3: Mô hình Suy giảm Âm học Không gian 3D & Che khuất Vật lý (Spatial Attenuation & Occlusion)

Tính toán biên độ âm lượng cảm nhận của một nguồn âm thanh thế giới (bước chân lính, tiếng va chạm đạn Burst) tại tai người nghe (Audio Listener):

1. **Suy giảm Khoảng cách Hình học ($A_{\text{distance}}$)**:
   $$A_{\text{distance}}(d) = \begin{cases}
   1.0 & \text{if } d \le d_{\text{min}} \\
   \frac{d_{\text{min}}}{d_{\text{min}} + (d - d_{\text{min}})} = \frac{d_{\text{min}}}{d} & \text{if } d_{\text{min}} < d \le d_{\text{max}} \\
   0.0 & \text{if } d > d_{\text{max}}
   \end{cases}$$
   - Mặc định: $d_{\text{min}} = 2.0\text{ m}$, $d_{\text{max}} = 25.0\text{ m}$.

2. **Hệ số Suy giảm Che khuất Vật lý ($M_{\text{occlusion}}$)**:
   $$M_{\text{occlusion}} = \begin{cases}
   10^{\frac{-4.0}{20}} \approx 0.6310 & \text{if tia Linecast bị chặn bởi tường E20 Solid} \\
   1.0 & \text{if tia Linecast thông thoáng (Direct LOS)}
   \end{cases}$$

3. **Hệ số Suy giảm Khi Đang Nấp Trong Tủ ($M_{\text{enclosed}}$)**:
   $$M_{\text{enclosed}} = \begin{cases}
   10^{\frac{-8.0}{20}} \approx 0.3981 & \text{if } \text{HideSpot.IsOccupied} == \text{true} \\
   1.0 & \text{if } \text{HideSpot.IsOccupied} == \text{false}
   \end{cases}$$

4. **Tổng Biên độ Âm lượng Cuối cùng ($A_{\text{final}}$)**:
   $$A_{\text{final}}(d) = A_{\text{distance}}(d) \cdot M_{\text{occlusion}} \cdot M_{\text{enclosed}}$$

- **Dải giá trị đầu ra (Output Range)**: $A_{\text{final}} \in [0.0, 1.0]$.
- **Ví dụ tính toán (Worked Example)**:
  - Lính gác đang nện bước ở khoảng cách $d = 6.0\text{ m}$ ($d_{\text{min}} = 2.0\text{ m}$).
  - Tia Linecast bị tường gạch E20 chắn ngang ($M_{\text{occlusion}} = 0.6310$).
  - Người chơi đang trốn bên trong tủ đồ kín ($M_{\text{enclosed}} = 0.3981$).
  - $A_{\text{distance}}(6.0) = 2.0 / 6.0 \approx 0.3333$.
  - $A_{\text{final}} = 0.3333 \times 0.6310 \times 0.3981 \approx 0.0837$ (Âm thanh lính gác bên ngoài tủ bị triệt giảm ~$-21.5\text{ dB}$, trầm đục, nghẹt thở nhưng vẫn đủ để người chơi lắng nghe định hướng).

---

### D4: Điều biến Âm thanh Nghi ngờ Suspicion Drone (Pitch & Volume Modulation)

Âm thanh vo ve điện từ phi thế giới (Non-diegetic Suspicion Drone) phản ánh trực tiếp mức độ nguy hiểm qua cả âm lượng và cao độ:

1. **Chuẩn hóa Tỷ lệ Nghi ngờ Tích lũy ($\bar{A}$)**:
   $$\bar{A} = \text{clamp}\left(\frac{A^*}{T_{\text{chase}}}, 0.0, 1.0\right)$$
   - Với $T_{\text{chase}} = 1.00$ (ngưỡng chuyển trạng thái Chase).

2. **Điều biến Âm lượng Tuyến tính ($V_{\text{drone}}$)**:
   $$V_{\text{drone}}(\bar{A}) = V_{\text{min}} + \bar{A} \cdot (V_{\text{max}} - V_{\text{min}})$$
   - Mặc định: $V_{\text{min}} = 0.0$ (im lặng tuyệt đối khi $A^* = 0$), $V_{\text{max}} = 0.75$.

3. **Điều biến Cao độ Âm thanh theo Hàm mũ Âm nhạc ($f_{\text{pitch}}$)**:
   $$f_{\text{pitch}}(\bar{A}) = f_{\text{base}} \cdot 2^{\left(\bar{A} \cdot \Delta_{\text{octave}}\right)}$$
   - Mặc định: $f_{\text{base}} = 60.0\text{ Hz}$ (tiếng ù siêu trầm), $\Delta_{\text{octave}} = 1.0$ (tăng đúng 1 quãng tám lên $120.0\text{ Hz}$ khi $A^* \to 1.0$).

- **Dải giá trị đầu ra (Output Range)**: $V_{\text{drone}} \in [0.0, 0.75]$, $f_{\text{pitch}} \in [60.0, 120.0]\text{ Hz}$.
- **Ví dụ tính toán (Worked Example)**:
  - Lính phát hiện người chơi, thanh nghi ngờ dâng lên $A^* = 0.50$:
    - $\bar{A} = 0.50 / 1.00 = 0.50$.
    - $V_{\text{drone}} = 0.0 + 0.50 \times (0.75 - 0.0) = 0.375$ (âm lượng dâng lên 50% mức trần).
    - $f_{\text{pitch}} = 60.0 \times 2^{0.50 \times 1.0} = 60.0 \times \sqrt{2} \approx 84.85\text{ Hz}$.
    - Cao độ dịch chuyển từ tiếng rền $60\text{ Hz}$ lên nốt trầm căng thẳng $84.85\text{ Hz}$, tạo cảm giác áp lực đè nặng trong tâm lý người chơi.

## Edge Cases

Hệ thống Audio & UI Feedback xử lý dứt điểm các tình huống biên kỹ thuật phần cứng âm thanh và logic gameplay, bảo đảm không bao giờ xảy ra lỗi nổ âm thanh (audio blast/pop), lệch pha âm học hoặc cạn kiệt tài nguyên:

### EC1: Mất Thiết bị Phần cứng Âm thanh / Đổi Tần số DSP (Audio Device Unplug / Sample Rate Drift / DSP Clock Re-anchor)
- **Tình huống**: Người chơi cắm hoặc rút tai nghe, đổi cổng xuất âm thanh trong hệ điều hành hoặc trình duyệt WebGL chuyển đổi ngữ cảnh âm thanh làm xung nhịp DSP bị gián đoạn, tần số lấy mẫu phần cứng thay đổi đột ngột (ví dụ từ $48000\text{ Hz} \to 44100\text{ Hz}$).
- **Xử lý**: 
  - Hệ thống lắng nghe sự kiện `AudioSettings.OnAudioConfigurationChanged`.
  - Audio Adapter ngay lập tức tạm dừng việc phát các cue mới, đọc lại `AudioSettings.outputSampleRate` và thiết lập lại mốc thời gian cơ sở `epoch_offset` tương ứng với mẫu số 0 mới của phần cứng.
  - Lập tức hủy bỏ toàn bộ các yêu cầu âm thanh đang chờ trong hàng đợi có thời hạn chót bị trễ hoặc vi phạm dung sai ($|\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| > 22\text{ ms}$) nhằm triệt tiêu hoàn toàn hiện tượng xả dồn âm thanh gây nổ màng loa (audio blast/distortion).
  - Các âm thanh nền dạng vòng lặp (Suspicion Drone, Heartbeat) được tái kích hoạt bằng hiệu ứng Fade-In êm ái trong $100\text{ ms}$ thay vì giật cục.

### EC2: Tạm dừng Trò chơi / Chuyển Tab WebGL / Mất Tiêu điểm Cửa sổ (Game Pause / Tab Blur / Background Focus Loss)
- **Tình huống**: Người chơi bấm phím Pause (mở menu tùy chọn), hoặc chuyển sang tab trình duyệt khác khiến WebGL rơi vào trạng thái nền (`Application.focusChanged(false)`).
- **Xử lý**:
  - Trục thời gian ảo của hệ thống âm thanh lập tức đóng băng (`virtual_time` ngừng trôi). Toàn bộ mixer bus `SFX_Diegetic` và `Music` hạ mức tăng ích xuống $-80\text{ dB}$ (hoặc áp dụng `AudioListener.pause = true` trên các kênh gameplay).
  - Khi trò chơi được tiếp tục (`Resume`), mọi yêu cầu âm thanh phát sinh trong lúc pause hoặc có hạn chót thuộc về quá khứ đều bị thanh trừng tức khắc.
  - Hệ thống không thực hiện phát đuổi (catch-up playback), loại bỏ hiện tượng âm thanh nổ dồn dập sau khi mở lại game.

### EC3: Xung đột Đa Cảnh báo Nghi ngờ (Multi-Guard Suspicion Contention & Acoustic Beating Prevention)
- **Tình huống**: Có từ hai lính gác trở lên đồng thời phát hiện hoặc tích lũy chỉ số nghi ngờ ($A^*$) đối với người chơi tại các vị trí và tốc độ tích lũy khác nhau.
- **Xử lý**:
  - Âm thanh phi thế giới `Suspicion Drone` chỉ lấy giá trị nghi ngờ cực đại trong tất cả các lính gác đang kích hoạt:
    $$A^*_{\text{dominant}} = \max_{i \in \text{Guards}}(A_i^*)$$
  - Cao độ ($f_{\text{pitch}}$) và âm lượng ($V_{\text{drone}}$) của Drone phi thế giới duy nhất chỉ phản ánh mối đe dọa nguy hiểm nhất, loại trừ hoàn toàn hiện tượng phát 2 dải tần số gần nhau gây ra giao thoa sóng âm lệch pha (acoustic beating/interferometry) gây nhức đầu cho người chơi.
  - Về mặt thị giác trên UI HUD, các vòng cung cảnh báo hướng (Threat Directional Arcs) vẫn hiển thị độc lập vị trí góc tương đối và mức độ lấp đầy cho từng lính gác để người chơi xác định rõ ràng môi trường xung quanh.

### EC4: Cạn kiệt Kênh Âm thanh khi Phát sinh Đột biến (Voice Pool Saturation & Priority Stealing Protocol)
- **Tình huống**: Trong một khung hình logic, có hơn 24 yêu cầu phát âm thanh diễn ra đồng thời (người chơi chạy nhanh, súng nổ, lính la hét, chuông báo động, mảnh kính vỡ).
- **Xử lý**:
  - Hệ thống áp dụng quy tắc thu hồi kênh (Voice Stealing) nghiêm ngặt dựa trên 3 phân tầng ưu tiên (Tier 1 > Tier 2 > Tier 3):
    - **Tier 1 (Bất khả xâm phạm - Protected)**: Âm thanh rượt đuổi Chase Stinger, nhịp tim nguy kịch Heartbeat, tác động đạn nén Burst. Các âm này không bao giờ bị drop hay bị cướp kênh.
    - **Tier 2 (Quan trọng - Important)**: Bước chân người chơi, tiếng bước chân lính trong cự ly nguy hiểm ($d \le 8.0\text{ m}$).
    - **Tier 3 (Phụ trợ - Ambient/Incidental)**: Tiếng cọ xát quần áo (`rustle`), tiếng động cơ môi trường xa, tiếng lính gác ở cự ly xa ($d > 8.0\text{ m}$).
  - Khi pool 24 kênh đã đầy: Hệ thống tìm và ngắt kênh Tier 3 có biên độ cảm nhận $A_{\text{final}}$ nhỏ nhất hoặc thời gian tồn tại lâu nhất. Nếu không còn kênh Tier 3 nào, hệ thống mới xem xét thu hồi kênh Tier 2 cũ nhất. Tuyệt đối không bao giờ từ chối hoặc làm gián đoạn voice Tier 1.

### EC5: Chuyển đổi Che khuất & Vào/Ra Điểm nấp Tốc độ cao (Rapid Hide Spot In/Out & LPF Cutoff Smoothing)
- **Tình huống**: Người chơi liên tục chui ra chui vào tủ nấp trong tích tắc, hoặc camera lia qua lại các mép cột/tường gạch dày khiến điều kiện che khuất (Occlusion) lật trạng thái liên tục giữa các frame.
- **Xử lý**:
  - Tần số cắt của bộ lọc Low-Pass Filter không bao giờ gán cứng tức thời theo dạng bước nhảy nhị phân ($20000\text{ Hz} \leftrightarrow 800\text{ Hz}$) mà được làm mịn liên tục qua hàm số mũ:
    $$f_{\text{cutoff}}(t + \Delta t) = f_{\text{cutoff}}(t) + (f_{\text{target}} - f_{\text{cutoff}}(t)) \cdot \left(1 - e^{-\frac{\Delta t}{\tau_{\text{cutoff\_smooth}}}}\right)$$
  - Với hằng số thời gian làm mịn $\tau_{\text{cutoff\_smooth}} = 0.15\text{ s}$.
  - Cơ chế này loại bỏ hoàn toàn các xung âm sắc nhọn hoặc tiếng lách cách kỹ thuật số (audio pop/click) tại ranh giới chuyển dịch dải tần của DSP filter.

### EC6: Vi dịch chuyển và Tì đè Tường Không Sinh Bước chân Giả (Zero-Displacement Micro-Stutter & Wall Pushing)
- **Tình huống**: Người chơi đè phím di chuyển vào góc tường bị cản lại (vận tốc đầu vào input $> 0$ nhưng độ dời vật lý thực tế $\Delta x_{XZ} \approx 0$), hoặc xoay chuột liên tục tại chỗ bên trong tủ nấp.
- **Xử lý**:
  - Tiến trình sải chân $s$ chỉ được phép tích lũy khi độ dời thực tế trên mặt phẳng ngang trong frame đạt ngưỡng tối thiểu: $\Delta \text{pos}_{XZ} \ge 0.01\text{ m}$.
  - Nếu độ dời nhỏ hơn ngưỡng (bị cản bởi va chạm vật lý E20 hoặc xoay tại chỗ trong vùng Pure Pivot Zone của tủ nấp), tiến trình sải chân $s$ giữ nguyên giá trị hoặc tự động xả về $0$.
  - Tiếng cọ xát quần áo (`rustle_event` tại $s = 0.70$) và tiếng bước chân (`step_event` tại $s = 1.0$) tuyệt đối không bị kích hoạt lặp đi lặp lại dạng súng máy (machine-gun footstep audio glitch).

### EC7: Người chơi Bị bắt Khi Đang Phát Âm sắc Cao trào (Player Capture Transition Sound Damping)
- **Tình huống**: Lính gác bắt được người chơi ngay tại thời điểm âm thanh Suspicion Drone đang ở đỉnh cao độ $120\text{ Hz}$ hoặc tiếng bước chân dồn dập trong pha rượt đuổi.
- **Xử lý**:
  - Khi nhận sự kiện `PlayerCapturedEvent` từ Event Bus, bộ điều phối âm thanh lập tức kích hoạt bộ giảm âm khẩn cấp (Hard Ducking): toàn bộ các bus `SFX_Diegetic`, `Ambience` và `Feedback_NonDiegetic` bị dìm âm lượng xuống $-30\text{ dB}$ trong vòng $50\text{ ms}$.
  - Toàn bộ không gian âm thanh được dọn sạch để nhường chỗ độc quyền cho âm thanh bắt giữ (Capture Stinger) và tiếng thở dốc thất bại của người chơi, đồng bộ với hiệu ứng thị giác màn hình chuyển dần sang màu đen (Fade to Black), ngăn ngừa sự hỗn loạn âm thanh khi thua trận.

## Dependencies

Hệ thống Audio & UI Feedback đóng vai trò là tầng biểu diễn đa giác quan (Presentation & Feedback Layer), kết nối trực tiếp với hạ tầng thông điệp và các hệ thống mô phỏng thế giới thông qua mô hình hỗn hợp: **Đăng ký nhận sự kiện định thời qua Event Bus** kết hợp **Phơi bày Dịch vụ Chỉ đọc (`IAudioFeedbackService`)** cho giao diện người dùng:

### Upstream Dependencies (Phụ thuộc Đầu vào)

1. **Event / Messaging Bus (`design/gdd/event-messaging-bus.md`)**:
   - **Bản chất phụ thuộc**: Kênh truyền thông điệp cốt lõi cho mọi tín hiệu kích hoạt âm thanh.
   - **Giao diện tiếp nhận**: Nhận các gói `EventEnvelope` mang dấu thời gian ảo `virtual_time`:
     - `PlayerStepCommittedEvent`: Kích hoạt bước chân kép (Dual-voice footstep).
     - `PlayerRustleCommittedEvent`: Kích hoạt âm thanh cọ xát quần áo (`rustle tell`).
     - `NoisePublishedEvent`: Kích hoạt âm thanh thế giới (SFX Diegetic) tương ứng với tiếng động vật lý.
     - `BurstDischargedEvent`: Kích hoạt âm thanh xả đạn nén và va chạm mục tiêu.
     - `SuspicionAccumulationUpdatedEvent`: Cung cấp điểm số nghi ngờ chuẩn hóa $A^*$.
     - `GuardStateChangedEvent`: Báo hiệu chuyển trạng thái FSM (Patrol $\to$ Investigate $\to$ Chase) để kích hoạt Stinger và stem âm nhạc.
     - `PlayerEnclosedStateChangedEvent`: Báo hiệu trạng thái vào/ra điểm nấp để áp dụng bộ lọc LPF.
     - `PlayerCapturedEvent`: Báo hiệu kết thúc ván chơi để dập tắt âm thanh nền và phát Capture Stinger.
   - **Ràng buộc hai chiều**: Event Bus đã chỉ định Audio System là một Subscriber ưu tiên trong Phase Dispatch `AudioPresentation`.

2. **Player Movement & Hide (`design/gdd/player-movement-hide.md`)**:
   - **Bản chất phụ thuộc**: Nguồn phát sinh tiến trình sải chân, vận tốc di chuyển và trạng thái che giấu.
   - **Dữ liệu tiêu thụ**:
     - Vận tốc tức thời $v$ (dùng cho công thức tăng ích bước chân D1).
     - Tiến trình sải chân $s$ (ngưỡng $0.70$ kích hoạt rustle, ngưỡng $1.00$ kích hoạt footstep).
     - Trạng thái `HideSpot.IsOccupied` (áp dụng suy giảm che khuất $M_{\text{enclosed}} = 0.3981$ và LPF $800\text{ Hz}$).
     - Chất liệu bề mặt tiếp xúc `PhysicMaterial` (xác định hệ số $K_{\text{surface}}$).
   - **Ràng buộc hai chiều**: `player-movement-hide.md` cam kết phát sinh sự kiện logic không phụ thuộc vào trạng thái audio hardware.

3. **Suspicion Meter & Grade Operator (`design/gdd/suspicion-meter-grade.md`)**:
   - **Bản chất phụ thuộc**: Đo lường mức độ nguy hiểm tâm lý của người chơi.
   - **Dữ liệu tiêu thụ**:
     - Điểm nghi ngờ chuẩn hóa tích lũy $A^* \in [0.0, 1.0]$ của lính gác nguy hiểm nhất.
     - Trạng thái tích lũy/suy giảm để điều biến cao độ $f_{\text{pitch}}$ và âm lượng $V_{\text{drone}}$ theo công thức D4.
   - **Ràng buộc hai chiều**: `suspicion-meter-grade.md` quy định Audio Feedback phản ánh trung thực nhịp độ căng thẳng mà không làm trễ tín hiệu gameplay.

4. **Guard AI & Perception (`design/gdd/guard-ai-fsm.md`)**:
   - **Bản chất phụ thuộc**: Nguồn phát sinh các sự kiện trạng thái tâm lý và hành vi của đối phương.
   - **Dữ liệu tiêu thụ**:
     - Chuyển đổi trạng thái FSM: `Patrol`, `Investigate`, `Chase`.
     - Vị trí không gian 3D của lính gác (tính toán suy giảm khoảng cách và hướng âm thanh theo D3).
   - **Ràng buộc hai chiều**: Guard AI phát thanh các tiếng rên rỉ/gầm gừ (vocal tells) và tiếng nện bước chân thông qua Audio System.

5. **Physics & Collision Config (`design/gdd/physics-collision-config.md`)**:
   - **Bản chất phụ thuộc**: Cung cấp cấu hình va chạm và kiểm tra đường truyền âm thanh.
   - **Dữ liệu tiêu thụ**:
     - Layer Matrix: Bắn tia `Linecast` trên Layer `World` (vật liệu E20 Solid) để kiểm tra che khuất âm học $M_{\text{occlusion}}$.
     - Cấu hình ma sát và phản xạ âm của các `PhysicMaterial`: Concrete, Wood, Metal, Carpet.
   - **Ràng buộc hai chiều**: Quy định bề dày vật lý $\ge 0.10\text{ m}$ là điều kiện xác định che khuất âm học.

---

### Downstream Dependents (Hệ thống Tiêu thụ Đầu ra)

1. **UI HUD & Visual Feedback Layer (`ui-hud-display`)**:
   - **Bản chất phụ thuộc**: Tiêu thụ dữ liệu phân tích âm học và đe dọa để hiển thị trực quan cho người chơi.
   - **Giao diện cung cấp (`IAudioFeedbackService`)**:
     - `float GetNormalizedSuspicionDroneLevel()`: Trả về âm lượng drone hiện tại để làm sáng hiệu ứng viền màn hình (Vignette Pulse).
     - `AudioThreatInfo[] GetActiveThreatDirections()`: Cung cấp vector hướng tương đối của các nguồn âm thanh nguy hiểm để vẽ các vòng cung cảnh báo hướng (Directional Threat Arcs).
     - `bool IsAudioDeviceOperational()`: Kiểm tra trạng thái hoạt động của phần cứng âm thanh (để hiển thị biểu tượng cảnh báo mất âm thanh nếu thiết bị lỗi).

2. **Game Feel & Screen FX (`gameplay-feel`)**:
   - **Bản chất phụ thuộc**: Đồng bộ hóa rung chấn màn hình (Camera Shake) và lực phản hồi haptic với các sự kiện âm thanh chấn động.
   - **Giao diện cung cấp**: Nhận tín hiệu thời điểm phát âm thanh Tier 1 (Stinger Chase, Burst Impact) để kích hoạt rung camera đồng bộ chính xác đến mức khung hình (Zero perceived lag).

3. **Level Blockout & Acoustic Design (`level-layout`)**:
   - **Bản chất phụ thuộc**: Bố trí không gian phù hợp với đặc tính truyền âm.
   - **Ràng buộc**: Mọi bề mặt sàn trong màn chơi bắt buộc phải gắn `PhysicMaterial` hợp lệ (không để null) để tránh lỗi âm thanh bước chân mặc định.

## Tuning Knobs

Mọi tham số âm học và đồng bộ giao diện trong hệ thống Audio & UI Feedback đều được điều khiển bằng dữ liệu (data-driven), tập trung trong `entities.yaml` và các ScriptableObject cấu hình âm thanh, tuyệt đối không bị hardcode trong mã nguồn:

### Nhóm 1: Hạ tầng Phần cứng & Đồng bộ DSP (Hardware & DSP Timing)

| Tên Tham số | Giá trị Mặc định | Dải An toàn | Đơn vị | Liên kết / Rationale |
|---|---|---|---|---|
| `audio_cue_onset_tolerance_ms` | `22.0` | `[10.0, 40.0]` | `ms` | ADR-0003 & Công thức D2. Dung sai tối đa chấp nhận giữa thời gian ảo gameplay và onset phần cứng DSP. |
| `voice_pool_max_capacity` | `24` | `[16, 32]` | `voices` | Core Rule CR5. Giới hạn số lượng AudioSource hoạt động đồng thời, tối ưu hóa cho trình duyệt WebGL. |
| `epoch_reanchor_fade_in_s` | `0.10` | `[0.05, 0.25]` | `s` | Edge Case EC1. Thời gian fade-in làm mịn các âm thanh lặp khi thiết bị âm thanh được cắm lại. |
| `game_pause_ducking_volume_db` | `-80.0` | `[-80.0, -40.0]` | `dB` | Edge Case EC2. Mức triệt tiêu âm lượng mixer bus khi trò chơi chuyển sang trạng thái Pause. |

### Nhóm 2: Bước chân & Cọ xát Di chuyển (Locomotion & Movement Audio)

| Tên Tham số | Giá trị Mặc định | Dải An toàn | Đơn vị | Liên kết / Rationale |
|---|---|---|---|---|
| `rustle_stride_threshold` | `0.70` | `[0.60, 0.80]` | `tỷ lệ` | Core Rule CR3 & Pillar 2. Ngưỡng sải chân phát âm sột soạt báo hiệu sắp nện bước chân. |
| `footstep_stride_threshold` | `1.00` | `[0.95, 1.05]` | `tỷ lệ` | Core Rule CR2. Ngưỡng sải chân cam kết phát thanh bước chân kép (Dual-Voice). |
| `footstep_gain_body_min` | `0.20` | `[0.05, 0.40]` | `tỷ lệ` | Công thức D1. Âm lượng tối thiểu của voice Body tần số thấp khi người chơi rón rén. |
| `footstep_gain_body_max` | `0.85` | `[0.70, 1.00]` | `tỷ lệ` | Công thức D1. Âm lượng tối đa của voice Body khi người chơi chạy hết tốc lực. |
| `footstep_gain_accent_min` | `0.15` | `[0.05, 0.35]` | `tỷ lệ` | Công thức D1. Âm lượng tối thiểu của voice Accent ma sát giày khi rón rén. |
| `footstep_gain_accent_max` | `0.90` | `[0.70, 1.20]` | `tỷ lệ` | Công thức D1. Âm lượng tối đa của voice Accent khi chạy nước rút. |
| `micro_stutter_pos_threshold_m` | `0.01` | `[0.005, 0.03]` | `m` | Edge Case EC6. Độ dời tối thiểu trên mặt phẳng ngang để công nhận tiến trình bước chân. |

### Nhóm 3: Không gian & Che khuất Vật lý (Spatial Acoustics & Occlusion)

| Tên Tham số | Giá trị Mặc định | Dải An toàn | Đơn vị | Liên kết / Rationale |
|---|---|---|---|---|
| `spatial_min_distance_m` | `2.0` | `[1.0, 4.0]` | `m` | Công thức D3. Khoảng cách bắt đầu suy giảm âm lượng hình học của nguồn âm 3D. |
| `spatial_max_distance_m` | `25.0` | `[15.0, 40.0]` | `m` | Công thức D3. Khoảng cách triệt tiêu hoàn toàn âm thanh thế giới (bán kính thính giác). |
| `occlusion_lpf_cutoff_hz` | `1200.0` | `[800.0, 2500.0]` | `Hz` | Core Rule CR7. Tần số cắt của bộ lọc Low-Pass Filter khi bị tường gạch E20 che khuất. |
| `occlusion_gain_loss_db` | `-4.0` | `[-8.0, -2.0]` | `dB` | Core Rule CR7 & Công thức D3. Suy giảm biên độ âm lượng khi âm thanh truyền xuyên vật cản. |
| `hidespot_lpf_cutoff_hz` | `800.0` | `[400.0, 1500.0]` | `Hz` | Core Rule CR6. Tần số cắt LPF khi người chơi trốn trong tủ kín (`HideSpot.IsOccupied`). |
| `hidespot_gain_loss_db` | `-8.0` | `[-14.0, -4.0]` | `dB` | Core Rule CR6 & Công thức D3. Suy giảm biên độ âm lượng thế giới khi ẩn náu trong tủ đồ. |
| `lpf_cutoff_smoothing_tau_s` | `0.15` | `[0.05, 0.30]` | `s` | Edge Case EC5. Hằng số thời gian làm mịn tần số cắt LPF tránh xung nhiễu kỹ thuật số. |

### Nhóm 4: Cảnh báo Tâm lý & Âm nhạc (Threat, Feedback & Music)

| Tên Tham số | Giá trị Mặc định | Dải An toàn | Đơn vị | Liên kết / Rationale |
|---|---|---|---|---|
| `suspicion_drone_base_pitch_hz` | `60.0` | `[45.0, 80.0]` | `Hz` | Công thức D4. Tần số cơ sở siêu trầm của âm thanh vo ve cảnh báo Suspicion Drone. |
| `suspicion_drone_octave_range` | `1.0` | `[0.5, 2.0]` | `quãng tám` | Công thức D4. Biên độ dịch chuyển cao độ (từ $60\text{ Hz} \to 120\text{ Hz}$) khi $A^* \to 1.0$. |
| `suspicion_drone_max_volume` | `0.75` | `[0.40, 0.90]` | `tỷ lệ` | Công thức D4. Giới hạn âm lượng trần của Suspicion Drone nhằm không che lấp tiếng bước chân. |
| `chase_stinger_ducking_db` | `-6.0` | `[-12.0, -3.0]` | `dB` | Core Rule CR4. Mức dìm âm lượng môi trường Ambience khi phát âm sắc rượt đuổi Chase. |
| `player_capture_ducking_db` | `-30.0` | `[-40.0, -20.0]` | `dB` | Edge Case EC7. Mức dập tắt âm lượng gameplay khi lính bắt được người chơi. |
| `vignette_threat_pulse_rate_hz` | `1.5` | `[0.8, 3.0]` | `Hz` | Core Rule CR8. Tần số nhấp nháy của hiệu ứng viền màn hình đỏ khi bị rượt đuổi. |

## Visual/Audio Requirements

### 1. Quy chuẩn Kỹ thuật Tài nguyên Âm thanh (Audio Asset Specifications)
- **Định dạng Tệp Gốc (Source Format)**: WAV 16-bit / 24-bit PCM, tần số lấy mẫu $44100\text{ Hz}$ hoặc $48000\text{ Hz}$.
- **Cấu hình Kênh (Channel Configuration)**:
  - **Mono (Đơn kênh)**: Bắt buộc cho toàn bộ âm thanh thế giới 3D Diegetic (Bước chân người chơi, tiếng di chuyển của lính gác, tiếng va chạm đạn nén Burst, tiếng mở chốt tủ nấp, tiếng xé gió). Âm thanh Mono bảo đảm tính toán không gian hóa (Spatial Panning & 3D Attenuation) chính xác tuyệt đối mà không bị lệch pha stereo.
  - **Stereo (Đa kênh)**: Chỉ sử dụng cho âm thanh phi thế giới 2D Non-Diegetic (Suspicion Drone, Chase Stinger, Heartbeat, UI Haptic Clicks, Music Stems).
- **Chuẩn hóa Biên độ Đỉnh (Peak Normalization)**: Toàn bộ sample âm thanh gốc được chuẩn hóa đỉnh tại $-1.5\text{ dBFS}$ nhằm chừa khoảng headroom an toàn, ngăn chặn hiện tượng méo tiếng liên mẫu (inter-sample clipping) trước khi đưa vào AudioMixer.
- **Dung lượng Bộ nhớ Âm thanh (Audio Memory Budget)**:
  - Tổng dung lượng bộ nhớ RAM thường trú cho toàn bộ sound bank không vượt quá $15.0\text{ MB}$ (đáp ứng nghiêm ngặt giới hạn hiệu năng của WebGL build).
  - Sử dụng nén `Vorbis` (chất lượng 70%) cho WebGL và `ADPCM` cho các hiệu ứng ngắn lặp lại nhanh.

### 2. Yêu cầu Hiệu ứng Hình ảnh Đồng bộ Âm học (Visual FX Specifications)
- **Hiệu ứng Viền Đe dọa (Threat Vignette Pulse - URP Post-Processing)**:
  - Khi Suspicion tích lũy $A^* \ge 0.60$, viền màn hình xuất hiện quầng tối mờ dần.
  - Khi chuyển sang trạng thái rượt đuổi `Chase`, viền màn hình đổi sang sắc đỏ máu (#8B0000) và nhấp nháy đồng nhịp với tần số tim đập $1.5\text{ Hz}$.
- **Mặt nạ Thị giác Điểm nấp (Peep Hole Slat Stencil Overlay)**:
  - Khi người chơi trốn trong tủ đồ kín (`HideSpot.IsOccupied == true`), màn hình kích hoạt lớp phủ khe quan sát (peep slats stencil) che 70% tầm nhìn ngoại vi, kết hợp hiệu ứng khử bão hòa màu sắc (Desaturation -30%) và viền mờ quang học (Chromatic Aberration 0.005) để phản ánh trạng thái nghẹt thở, căng thẳng.

---

## UI Requirements

### 1. Chỉ báo Đe dọa Đa hướng (Directional Threat Arcs)
- **Vị trí**: Nằm trên một vòng tròn la bàn vô hình bao quanh tâm ngắm (reticle) tại bán kính $120\text{ px}$.
- **Hành vi**:
  - Khi một lính gác nghi ngờ người chơi từ góc khuất ngoại vi, một vòng cung cung cấp hướng (Arc) xuất hiện hướng về góc phương vị của lính gác đó so với hướng nhìn camera.
  - Chiều dài và độ dày của vòng cung tỷ lệ thuận với điểm số nghi ngờ $A_i^*$.
  - Màu sắc chuyển đổi theo trạng thái: Vàng chanh (#E6C229) khi $A^* < 0.60$ (Investigate) $\to$ Cam đậm (#F17105) khi $A^* \ge 0.60 \to$ Đỏ chớp nháy (#D11149) khi vào Chase.
  - Khi lính gác hoàn toàn nằm trong nón nhìn trực diện camera ($|\Delta \theta| \le 30^\circ$), vòng cung tự động mờ dần để tránh che khuất tầm nhìn của người chơi.

### 2. Phụ đề Âm thanh Tiếp cận (Directional Closed Captions / Sound Subtitles)
- **Mục đích**: Bảo đảm người chơi khiếm thính hoặc chơi trong môi trường không bật âm thanh vẫn nắm bắt trọn vẹn 100% thông tin chiến thuật (Accessibility Compliance).
- **Định dạng hiển thị**: Nằm ở phần dưới màn hình (cách cạnh đáy $80\text{ px}$), nền đen mờ mờ $70\%$ độ mờ đục (opacity), chữ trắng tương phản cao (#FFFFFF), hỗ trợ phóng to từ $100\% \to 150\%$.
- **Cú pháp định hướng**: Kèm theo mũi tên chỉ hướng tương đối của âm thanh:
  - `[◄ Bước chân nặng - Trái]` (tiếng bước chân lính gác bên trái).
  - `[▲ Tiếng kim loại va chạm - Phía trước]` (tiếng đạn Burst nảy phía trước).
  - `[► Tiếng thở dồn dập - Phải]` (lính gác đang sục sạo bên phải).
- **Thời gian tồn tại**: Mỗi phụ đề tồn tại trong $1.5\text{ s}$ rồi tự động tan biến mượt mà (fade-out trong $0.3\text{ s}$).

### 3. Trình đơn Cài đặt Âm thanh (Audio Options Menu)
- Cung cấp 4 thanh trượt chỉnh âm lượng độc lập từ $0\%$ đến $100\%$:
  1. `Master Volume` (Âm lượng tổng thể).
  2. `SFX Volume` (Hiệu ứng âm thanh thế giới 3D).
  3. `Music Volume` (Âm nhạc nền và Stinger).
  4. `UI & Psychological Feedback Volume` (Âm lượng Drone nghi ngờ, nhịp tim và tiếng phản hồi giao diện).
- Tùy chọn Bật/Tắt:
  - `Audio Captions` (Bật/Tắt phụ đề âm thanh định hướng — mặc định: Tắt).
  - `Mono Audio Downmix` (Chuyển đổi toàn bộ âm thanh vòm sang Mono — hỗ trợ người chơi chỉ nghe được một bên tai).

## Acceptance Criteria

Hệ thống Audio & UI Feedback được công nhận đạt chuẩn khi vượt qua toàn bộ 12 tiêu chí nghiệm thu kiểm thử độc lập (AC1–AC12) dưới đây:

1. **AC1 — Kích hoạt Bước chân Kép Đồng bộ (Dual-Voice Footstep Gain Calculation)**:
   - **Điều kiện**: Người chơi di chuyển trên sàn với vận tốc $v \in [1.80, 6.25]\text{ m/s}$ trên các bề mặt vật lý khác nhau (`PhysicMaterial`).
   - **Kỳ vọng**: Tại thời điểm cam kết bước chân $s \ge 1.0$, Audio System kích hoạt đồng thời 2 voice `body` và `accent`. Voice `body` có âm lượng biến thiên tuyến tính $G_{\text{body}} \in [0.20, 0.85]$; voice `accent` biến thiên phi tuyến bình phương và nhân đúng hệ số bề mặt $K_{\text{surface}}$ (Bê tông = 1.0, Gỗ = 0.80, Kim loại = 1.25, Thảm = 0.35). Tổng tăng ích đỉnh được chuẩn hóa không vượt quá $0\text{ dBFS}$ ($\text{Gain}_{\text{master\_footstep}} \le 1.0$).

2. **AC2 — Tín hiệu Cọ xát Báo trước Không Phát tán Tiếng ồn (Pre-Commit Movement Tell - Rustle)**:
   - **Điều kiện**: Người chơi di chuyển đạt tiến trình sải chân $s = 0.70$ ($[0.60, 0.80]$).
   - **Kỳ vọng**: Âm thanh sột soạt quần áo (`rustle_event`) được phát ra ở tai người chơi. Kiểm tra xác nhận không có bất kỳ sự kiện `NoisePublishedEvent` nào được gửi vào Event Bus và bán kính thính giác của lính gác đối với sự kiện này bằng $0.0\text{ m}$.

3. **AC3 — Thẩm định Khởi phát Phần cứng DSP Chuẩn xác (`ADR-0003`)**:
   - **Điều kiện**: Thực thi kiểm thử kích hoạt âm thanh với vị trí mẫu DSP thực tế so với thời gian ảo gameplay.
   - **Kỳ vọng**: Vị từ `OnsetOutcome` trả về `CONFIRMED` khi và chỉ khi $|\text{virtual\_dsp\_onset} - \text{virtual\_cue\_request}| \le 22.0\text{ ms}$. Nếu sai lệch $> 22.0\text{ ms}$, trả về `REJECTED_JITTER`. Nếu không có thiết bị phần cứng hoặc tần số lấy mẫu $= 0$, trả về `UNSUPPORTED`.

4. **AC4 — Điều biến Âm học Suspicion Drone Chuẩn xác (Formula D4)**:
   - **Điều kiện**: Điểm nghi ngờ chuẩn hóa dâng dần từ $A^* = 0.0 \to 1.0$.
   - **Kỳ vọng**: Tần số cơ sở của Suspicion Drone dịch chuyển liên tục từ $60.0\text{ Hz} \to 120.0\text{ Hz}$ theo hàm mũ (đúng 1 quãng tám $2^{\bar{A}}$), và âm lượng tăng tuyến tính từ $0.0 \to 0.75$. Khi $A^* = 0.0$, âm thanh Drone tắt hoàn toàn ($V_{\text{drone}} = 0.0$).

5. **AC5 — Xử lý Đa Cảnh báo Nghi ngờ và Chống Triệt tiêu Lệch pha (Dominant Drone Contention)**:
   - **Điều kiện**: Hai hoặc nhiều lính gác đồng thời tích lũy điểm nghi ngờ đối với người chơi ở các mức khác nhau (ví dụ: Guard A có $A^* = 0.4$, Guard B có $A^* = 0.8$).
   - **Kỳ vọng**: Suspicion Drone Non-diegetic duy nhất chỉ điều biến theo giá trị nguy hiểm cực đại $A^* = \max(0.4, 0.8) = 0.8$ ($f_{\text{pitch}} \approx 104.46\text{ Hz}$). Không phát sinh hai luồng drone độc lập, loại bỏ hoàn toàn hiện tượng giao thoa sóng âm nhịp đập (acoustic beating).

6. **AC6 — Bộ lọc Âm học Không gian Kín Điểm Nấp (HideSpot Enclosure Acoustic Filtering)**:
   - **Điều kiện**: Người chơi bước vào và chốt cửa tủ nấp (`HideSpot.IsOccupied == true`).
   - **Kỳ vọng**: Toàn bộ mixer bus `SFX_Diegetic` bị áp dụng bộ lọc Low-Pass Filter với tần số cắt $800.0\text{ Hz}$ ($Q = 1.2$) và biên độ âm lượng bị suy giảm $-8.0\text{ dB}$ ($M_{\text{enclosed}} \approx 0.3981$). Tiếng thở dốc của nhân vật được kích hoạt trong bus `Feedback_NonDiegetic`.

7. **AC7 — Che khuất Âm thanh Vật lý Qua Tường E20 (Wall Acoustic Occlusion Linecast)**:
   - **Điều kiện**: Nguồn âm thanh 3D của lính gác bị ngăn cách với tai người chơi bởi tường gạch E20 Solid (độ dày $\ge 0.10\text{ m}$).
   - **Kỳ vọng**: Tia Linecast trên layer `World` xác nhận bị cản; nguồn âm bị suy giảm biên độ $-4.0\text{ dB}$ ($M_{\text{occlusion}} \approx 0.6310$) và áp dụng bộ lọc LPF tại tần số cắt $1200.0\text{ Hz}$.

8. **AC8 — Giao thức Thu hồi Kênh Ưu tiên 3 Phân tầng (Voice Stealing Protocol)**:
   - **Điều kiện**: Hệ thống nhận yêu cầu phát âm thanh vượt quá giới hạn 24 kênh của Voice Pool.
   - **Kỳ vọng**: Âm thanh Tier 1 (Chase Stinger, Heartbeat, Burst Impact) không bao giờ bị từ chối hoặc cướp kênh. Hệ thống tự động thu hồi voice Tier 3 có âm lượng nhỏ nhất hoặc thời gian tồn tại lâu nhất. Nếu không có Tier 3, thu hồi voice Tier 2 cũ nhất.

9. **AC9 — Làm mịn Chuyển tiếp Tần số Cắt Bộ lọc (LPF Cutoff Smoothing)**:
   - **Điều kiện**: Người chơi liên tục chui ra/vào tủ nấp hoặc góc khuất.
   - **Kỳ vọng**: Tần số cắt của LPF chuyển dịch mượt mà theo hàm số mũ với hằng số thời gian $\tau_{\text{cutoff\_smooth}} = 0.15\text{ s}$. Phân tích phổ tín hiệu âm thanh xác nhận không xuất hiện xung nhiễu kỹ thuật số (audio clicks/pops) tại các thời điểm chuyển tiếp.

10. **AC10 — Triệt tiêu Bước chân Giả khi Tì đè Tường (Zero-Displacement Wall Stutter Prevention)**:
    - **Điều kiện**: Người chơi đè phím di chuyển vào góc tường vật lý, vị trí tọa độ thực tế tịnh tiến $\Delta \text{pos}_{XZ} < 0.01\text{ m}$.
    - **Kỳ vọng**: Tiến trình sải chân $s$ giữ nguyên hoặc xả về $0$. Tuyệt đối không phát sinh âm thanh bước chân (`step_event`) hay âm thanh sột soạt (`rustle_event`) liên tục dạng súng máy.

11. **AC11 — Vòng cung Chỉ báo Đe dọa Đa hướng trên UI HUD (Directional Threat Arcs)**:
    - **Điều kiện**: Lính gác ở góc khuất ngoại vi camera ($|\Delta \theta| > 30^\circ$) tích lũy điểm nghi ngờ $A^* > 0$.
    - **Kỳ vọng**: UI HUD hiển thị vòng cung chỉ báo hướng chính xác tại bán kính $120\text{ px}$ quanh tâm ngắm, độ dài tỷ lệ thuận với $A^*$, đổi màu sắc vàng $\to$ cam $\to$ đỏ nhấp nháy đồng bộ với FSM của lính. Khi lính gác bước vào tầm nhìn trực diện ($|\Delta \theta| \le 30^\circ$), vòng cung tự động mờ dần.

12. **AC12 — Phụ đề Âm thanh Định hướng Hỗ trợ Tiếp cận (Audio Accessibility Captions)**:
    - **Điều kiện**: Tùy chọn `Audio Captions` được bật trong Menu Cài đặt.
    - **Kỳ vọng**: Khi phát sinh các âm thanh chiến thuật (bước chân lính, tiếng lên đạn, tiếng thở, tiếng mở cửa), phụ đề hiển thị đúng cú pháp với mũi tên định hướng tương đối (ví dụ `[◄ Bước chân nặng - Trái]`), tương phản cao trên nền đen mờ $70\%$, tồn tại $1.5\text{ s}$ và fade-out trong $0.3\text{ s}$.

---

## Open Questions

1. **Q1: Tải trước (Preload) toàn bộ soundbank vào RAM có gây nguy cơ vượt ngưỡng bộ nhớ trên WebGL không?**
   - **Trạng thái**: Đã giải quyết (Resolved).
   - **Giải pháp**: Phân chia Audio Clips thành các Addressable Asset Groups theo màn chơi, áp dụng nén Vorbis chất lượng 70%, bảo đảm tổng kích thước bộ nhớ âm thanh thường trú $\le 15.0\text{ MB}$, phù hợp với cấu hình tối thiểu của trình duyệt WebGL.

2. **Q2: Âm thanh nhịp tim (Heartbeat) khi bị rượt đuổi có nên tăng tốc theo vận tốc chạy của người chơi không?**
   - **Trạng thái**: Đã giải quyết (Resolved).
   - **Giải pháp**: Cố định tần số nhịp tim tại $1.5\text{ Hz}$ ($90\text{ BPM}$) kết hợp nhấp nháy đồng bộ với hiệu ứng viền màn hình URP Threat Vignette. Điều này duy trì nhịp điệu sinh học căng thẳng nhưng ổn định, không gây hoảng loạn mất kiểm soát hoặc rối loạn nhịp nghe của người chơi.
