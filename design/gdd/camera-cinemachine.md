# Camera (Cinemachine Rig)

> **Status**: Approved (2026-09-21)
> **Author**: Nguyen Ngoc Quy, Claude (Game Studio Agent Architecture)
> **Last Updated**: 2026-09-21
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge), Pillar 3 (Cat-and-Mouse Suspense)

## Overview

Hệ thống **Camera (Cinemachine Rig)** (System `#20`) là lớp hạ tầng thị giác và định hướng không gian cốt lõi (Core Spatial Orientation & Visual Rig) trong *Whisper Ward*, chịu trách nhiệm điều khiển góc nhìn thứ ba (Third-Person Orbit / Over-The-Shoulder), xử lý va chạm hình học môi trường và cung cấp hệ quy chiếu hướng nhìn chuẩn (`camera_yaw`) cho toàn bộ chuyển động điều khiển của nhân vật. Được xây dựng trên nền tảng gói **Cinemachine 3.x** của Unity 6 LTS kết hợp hệ thống **Input System mới (`com.unity.inputsystem`)**, hệ thống mang lại một trải nghiệm quan sát lén lút mượt mà, ổn định và đầy tính chiến thuật, bảo đảm người chơi luôn nắm quyền kiểm soát góc nhìn mà không bao giờ bị ống kính máy quay phản bội hay làm mất phương hướng.

Về mặt kỹ thuật và cơ chế vận hành, hệ thống tuân thủ 4 nguyên tắc thiết kế bất biến:
1. **Hợp đồng Hệ quy chiếu Góc nhìn (Camera-Relative Coordinate Contract)**: Trục xoay ngang của camera (`camera_yaw`) là nguồn chân lý duy nhất xác lập hướng di chuyển cho Player Controller (`#11`). Quy ước toán học chuẩn xác: góc phương vị dương $\theta$ quy đổi phím `W` tiến tới $(\sin \theta, \cos \theta)$ và phím `D` sang phải $(\cos \theta, -\sin \theta)$ trên mặt phẳng ngang XZ thế giới.
2. **Độc lập Tuyệt đối khi Đứng yên (Idle Facing Hold — `AC-P25`)**: Khi người chơi thả tay khỏi các phím di chuyển WASD, việc xoay tự do góc nhìn camera xung quanh nhân vật tuyệt đối không làm nhân vật tự động xoay hướng theo (No autonomous turn/tracking). Hướng nhìn của nhân vật (`facing`) giữ nguyên giá trị bit-identical cho đến khi có lệnh di chuyển mới, cho phép người chơi đứng nấp và tự do quan sát xung quanh góc khuất mà không vô tình lộ diện.
3. **Cơ chế Chống Xuyên Tường & Giảm Giật (Deocclusion Spherecast & Damped Recovery)**: Sử dụng thuật toán quét khối cầu (Spherecast với bán kính đệm $R_{\text{cam\_col}} = 0.20\text{ m}$) kiểm tra va chạm trên Layer `World` (tường E20 Solid). Khi gặp vật cản, camera tự động thu ngắn khoảng cách tức thì để chống lộ hình học xuyên tường (clip-through), và nhẹ nhàng nới rộng trở lại khoảng cách mặc định bằng hàm giảm chấn mượt mà (damping recovery) khi không gian thông thoáng trở lại.
4. **Định khung Chuyên biệt trong Điểm Nấp (HideSpot Framing Transition)**: Khi người chơi bước vào tủ đồ kín (`HideSpot.IsOccupied == true`), hệ thống chuyển đổi trạng thái mượt mà sang Virtual Camera nội thất cố định, định vị góc nhìn qua khe cửa/chấn song quan sát (peep slats) mà không bị kẹp giữa vách tủ và tường phòng, duy trì trọn vẹn cảm giác hồi hộp, ngột ngạt nhưng có thể kiểm soát.

## Player Fantasy

Hệ thống Camera hiện thực hóa ảo tưởng trải nghiệm của một **Kẻ Rình Rập Tàng Hình Khôn Ngoan và Thận Trọng** (The Cautious Stalker), nơi góc nhìn của người chơi chính là công cụ sinh tồn tối thượng trong bóng đêm của Whisper Ward:

1. **Cảm giác Hoàn toàn Làm chủ Góc nhìn (Absolute Spatial Agency — Phục vụ Pillar 2: Fair Mind-Challenge)**:
   - Người chơi không bao giờ phải "chiến đấu" với góc quay của camera. Mọi thao tác di chuột đều phản hồi ngay lập tức, mượt mà và chuẩn xác theo tỷ lệ 1:1, không có quán tính trễ (input lag) hay hiện tượng tự động xoay tâm (auto-recenter) gây ức chế.
   - Khi đứng nép sau gờ tường hay nấp dưới bóng tối, người chơi có thể tự do xoay camera 360 độ để quét sạch hành lang, quan sát tuyến tuần tra của lính gác mà nhân vật không hề cử động một sợi cơ nào. Sự tự do thị giác này mang lại cảm giác làm chủ không gian, tự tin lên kế hoạch từng bước đi mà không lo bị lộ vị trí oan uổng.

2. **Cảm giác Căng thẳng Nghẹt thở và Mong manh (Intimate Vulnerability — Phục vụ Pillar 3: Cat-and-Mouse Suspense)**:
   - Vị trí camera đặt ngang vai (Over-the-shoulder) với khoảng cách vừa đủ ($D_{\text{default}} = 2.80\text{ m}$) và độ cao cân đối ($H_{\text{default}} = 1.40\text{ m}$) tạo nên sự gần gũi với nhân vật. Người chơi nhìn thấy từng chuyển động cúi mình, từng bước rón rén, và đồng thời cảm nhận rõ ràng điểm mù ngay sau lưng mình.
   - Khi bị lính gác đuổi bắt trong trạng thái `Chase`, camera duy trì sự ổn định sắt đá, không rung lắc quá đà làm mất phương hướng nhưng nới nhẹ tiêu cự (FOV tăng từ $60^\circ \to 68^\circ$) tạo cảm giác không gian kéo dãn ra theo cơn hoảng loạn của nhân vật đang tháo chạy.

3. **Khoảnh khắc Điểm Nấp Đóng Kín (The Breathless Peep-hole Moment)**:
   - Khoảnh khắc chui vào tủ đồ kín, camera lướt êm ái vào vị trí quan sát nội thất phía sau khe cửa. Người chơi nhìn qua khe chấn song hẹp, chứng kiến bước chân nặng nề của lính gác lướt qua ngay trước mặt. Góc nhìn bị giới hạn nhưng sắc nét, biến chiếc tủ từ một khối hình học đơn thuần thành một nơi trú ẩn nghẹt thở, nơi sự sống và cái chết chỉ cách nhau một tấm gỗ mỏng.

## Detailed Design

### Core Rules

Hệ thống Camera vận hành dựa trên 8 quy tắc cốt lõi (CR1–CR8) bảo đảm tính chính xác toán học, ổn định cơ học và khả năng tương thích cao với nền tảng WebGL:

- **CR1: Kiến trúc Rig Cinemachine 3.x Đa Tầng (Multi-VirtualCamera Architecture)**:
  - Máy quay vật lý duy nhất trong Scene (`MainCamera`) gắn thành phần điều phối `CinemachineBrain` (thực thi trong pha `LateUpdate` của vòng đời Unity để bảo đảm bắt kịp vị trí nhân vật sau pha `FixedUpdate` vật lý).
  - Hệ thống sử dụng 2 Virtual Cameras chuyên biệt:
    1. `CM_FreeOrbit` (Priority = 10): Camera chính góc nhìn thứ ba tự do, sử dụng thuật toán Orbital Follow xoay quanh `CameraFollowTarget`.
    2. `CM_HideSpot` (Priority = 20): Camera nội thất gắn tại `aperture_portal_target` của tủ nấp, kích hoạt bằng cách nâng độ ưu tiên khi người chơi vào tủ đồ.
  - Quá trình chuyển đổi giữa các Virtual Camera được thực hiện qua bộ hòa trộn `CinemachineBlenderSettings` với đường cong mượt mà `EaseInOut` trong thời gian $T_{\text{blend}} = 0.35\text{ s}$.

- **CR2: Ánh xạ Đầu vào Chuột & Khóa Góc Chúi (Input Mapping & Pitch Clamping)**:
  - Tín hiệu đầu vào từ chuột `Look` $(\Delta x, \Delta y)$ được đọc từ Input System mới:
    - Trục xoay ngang ($\Delta x \to \text{Yaw}$): Xoay không giới hạn $360^\circ$ quanh trục thẳng đứng thế giới (Vector Y), tốc độ xoay góc tỷ lệ thuận với độ nhạy chuột `mouse_sensitivity`:
      $$\omega_{\text{yaw}} = \Delta x \cdot S_{\text{mouse}}$$
    - Trục ngước/chúi ($\Delta y \to \text{Pitch}$): Xoay quanh trục ngang cục bộ của camera, chịu sự kiểm soát của cờ đảo trục `invert_y` và bị khóa cứng (hard clamped) trong dải an toàn:
      $$\theta_{\text{pitch}} \in [\theta_{\text{pitch\_min}}, \theta_{\text{pitch\_max}}] = [-35.0^\circ, +65.0^\circ]$$
  - Khóa góc chúi ngăn chặn triệt để lỗi lộn ngược camera (Gimbal Lock) hoặc để camera chúi xuống quá sâu làm đâm xuyên mặt sàn.

- **CR3: Định vị Khung hình Lệch Vai Ngang (Right-Shoulder Framing & Target Offset)**:
  - Điểm neo theo dõi (`Follow Target`): Gắn tại xương ngực của nhân vật với độ cao $Y_{\text{target}} = 1.35\text{ m}$ so với mặt đất.
  - Khoảng cách camera danh định (Nominal Distance): $D_{\text{nom}} = 2.80\text{ m}$.
  - Độ lệch vai ngang (Shoulder Offset): Đặt lệch cố định sang vai phải $+0.35\text{ m}$ ($X_{\text{offset}} = +0.35\text{ m}$). Định dạng này đưa nhân vật về vị trí $1/3$ bên trái khung hình theo quy chuẩn điện ảnh, giải phóng toàn bộ tâm nhìn phía trước và bên phải để người chơi ngắm ném đạn nén Burst hoặc quan sát hành lang.

- **CR4: Khử Va chạm Xuyên Tường Bằng Khối Cầu (Spherecast Deocclusion Algorithm)**:
  - Để ngăn camera xuyên qua tường hoặc để lộ khoảng trống ngoài màn chơi (void), hệ thống thực hiện quét khối cầu (Spherecast) từ `CameraFollowTarget` tới vị trí lý tưởng của camera:
    - Bán kính khối cầu kiểm tra: $R_{\text{cam\_col}} = 0.20\text{ m}$.
    - Lớp mặt nạ va chạm (LayerMask): Chỉ tương tác với Layer `World` (tường E20 Solid và chướng ngại vật tĩnh).
  - Khi phát hiện điểm va chạm tại khoảng cách $d_{\text{hit}}$:
    - Khoảng cách thực tế của camera lập tức được thu ngắn:
      $$D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R_{\text{cam\_col}})$$
    - Với khoảng cách tối thiểu cho phép $D_{\text{min}} = 0.40\text{ m}$.
  - Để tối ưu hóa hiệu năng trên WebGL (giới hạn 16.6 ms), số lần lặp tính toán khử va chạm được giới hạn tối đa $\le 2$ iterations/frame.

- **CR5: Giảm Chấn Hồi Phục Khoảng Cách Mượt mà (Damped Distance Recovery)**:
  - Khi chướng ngại vật che khuất biến mất (ví dụ người chơi vừa đi qua góc tường hẹp ra đại sảnh):
    - Camera không bật giật cục ra khoảng cách danh định $D_{\text{nom}}$ mà nới rộng dần khoảng cách thông qua hàm giảm chấn số mũ (Exponential Damping):
      $$D(t + \Delta t) = D(t) + (D_{\text{nom}} - D(t)) \cdot \left(1 - e^{-\frac{\Delta t}{\tau_{\text{recover}}}}\right)$$
    - Với hằng số thời gian hồi phục $\tau_{\text{recover}} = 0.25\text{ s}$. Cơ chế này loại bỏ hoàn toàn hiện tượng camera bị giật nảy (camera stutter/jitter) khi di chuyển qua các bề mặt tường nhấp nhô.

- **CR6: Bất biến Khóa Hướng Nhân vật Khi Đứng Yên (`AC-P25` Compliance)**:
  - Khi người chơi giữ trạng thái đứng yên (vận tốc đầu vào WASD $= 0$), mọi chuyển động xoay tự do của camera bằng chuột chỉ cập nhật góc phương vị `camera_yaw`.
  - Hướng mặt (`facing`) của nhân vật hoàn toàn giữ nguyên giá trị bit-identical, không bị ảnh hưởng bởi camera yaw. Nhân vật không tự động xoay lưng theo camera (No Autonomous Turn / No Re-center). Người chơi có thể tự do ngắm nhìn tứ phía từ vị trí ẩn nấp an toàn.

- **CR7: Định Khung Góc Nhìn Điểm Nấp Tủ Kín (HideSpot Interior Framing)**:
  - Khi nhân vật chui vào tủ nấp (`HideSpot.IsOccupied == true`):
    - Virtual Camera `CM_HideSpot` được kích hoạt. Điểm đặt máy quay được chuyển về `aperture_portal_target` của tủ nấp ($h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height} / 2)$).
    - Hướng nhìn của camera được hướng xuyên qua khe quan sát (peep slats) ra không gian bên ngoài. Người chơi chỉ được phép xoay nhẹ góc nhìn trong phạm vi hình nón $60^\circ$ ($\pm 30^\circ$ quanh hướng cửa tủ).
    - Triệt tiêu hoàn toàn khả năng camera bị kẹp giữa lưng tủ và tường phòng gây lỗi thị giác.

- **CR8: Điều Biến Tiêu Cự Tăng Căng Thẳng Rượt Đuổi (Chase FOV Dynamic Scaling)**:
  - Tiêu cự góc mở camera (Field of View - FOV) mặc định trong trạng thái khám phá và rình rập là $\text{FOV}_{\text{base}} = 60.0^\circ$.
  - Khi nhận sự kiện lính gác chuyển sang trạng thái truy đuổi `Chase`:
    - FOV mở rộng mượt mà lên $\text{FOV}_{\text{chase}} = 68.0^\circ$ trong thời gian $0.6\text{ s}$. Góc nhìn rộng hơn hỗ trợ quan sát hai bên cánh và gia tăng cảm giác tốc độ tháo chạy.
  - Khi người chơi cắt đuôi thành công và trạng thái `Chase` kết thúc: FOV từ từ co lại về $60.0^\circ$ trong thời gian $1.5\text{ s}$.

---

### States and Transitions

Hệ thống Camera vận hành như một Máy trạng thái hữu hạn (Camera FSM) gồm 5 trạng thái trực giao:

```
                  +-----------------------------------+
                  |                                   |
                  v                                   |
           [1. FreeOrbit] <====================> [2. OccludedOrbit]
             |        ^     (Vật cản E20 chắn)        |
             |        |                               |
 (Vào tủ nấp)|        | (Ra khỏi tủ)                  | (Bị phát hiện)
             v        |                               v
     [3. HideSpotInterior]                    [4. ChaseTension]
             |                                        |
             +----------------+   +-------------------+
                              |   |
                              v   v
                        [5. CaptureFocus]
                         (GameOver Catch)
```

1. **`FreeOrbit` (Trạng thái Mặc định)**:
   - Camera quay quanh nhân vật ở khoảng cách danh định $D_{\text{nom}} = 2.80\text{ m}$, lệch vai phải $+0.35\text{ m}$, $\text{FOV} = 60.0^\circ$.
   - **Chuyển trạng thái**:
     - $\to$ `OccludedOrbit`: Khi tia Spherecast phát hiện va chạm với tường E20 ($d_{\text{hit}} < D_{\text{nom}} + R_{\text{cam\_col}}$).
     - $\to$ `HideSpotInterior`: Khi người chơi kích hoạt tương tác chui vào tủ nấp (`PlayerEnclosedStateChangedEvent(true)`).
     - $\to$ `ChaseTension`: Khi có ít nhất một lính gác chuyển sang trạng thái `Chase`.

2. **`OccludedOrbit` (Trạng thái Bị Tường Ép Sát)**:
   - Khoảng cách camera bị co ngắn lại $D_{\text{actual}} \in [0.40, 2.80)\text{ m}$ theo bề mặt va chạm. Nếu $D_{\text{actual}} \le 0.60\text{ m}$, kích hoạt vật liệu mờ dither trên mô hình nhân vật.
   - **Chuyển trạng thái**:
     - $\to$ `FreeOrbit`: Khi tia Spherecast không còn bị cản, phục hồi mượt mà bằng hằng số giảm chấn $\tau_{\text{recover}} = 0.25\text{ s}$.
     - $\to$ `HideSpotInterior`: Khi người chơi chui vào tủ nấp từ góc tường hẹp.

3. **`HideSpotInterior` (Trạng thái Góc Nhìn Nội Thất Tủ Nấp)**:
   - Kích hoạt `CM_HideSpot`, định vị camera sau khe quan sát của tủ nấp, khóa góc xoay trong phạm vi $\pm 30^\circ$, kết hợp mặt nạ thị giác khe hẹp Peep-hole.
   - **Chuyển trạng thái**:
     - $\to$ `FreeOrbit`: Khi người chơi đẩy cửa rời khỏi tủ nấp (`PlayerEnclosedStateChangedEvent(false)`), hòa trộn mượt mà trong $0.35\text{ s}$.

4. **`ChaseTension` (Trạng thái Rượt Đuổi Căng Thẳng)**:
   - Camera giữ nguyên cơ chế Orbit nhưng nới rộng góc mở $\text{FOV} = 68.0^\circ$ và kích hoạt hiệu ứng rung nhẹ (Micro-shake biên độ $0.02\text{ m}$) khi lính gác áp sát trong cự ly $\le 4.0\text{ m}$.
   - **Chuyển trạng thái**:
     - $\to$ `FreeOrbit`: Khi toàn bộ lính gác mất dấu người chơi và thoát khỏi trạng thái `Chase`.
     - $\to$ `HideSpotInterior`: Khi người chơi trốn thoát thành công vào tủ nấp trong lúc bị rượt đuổi.
     - $\to$ `CaptureFocus`: Khi lính gác bắt được người chơi.

5. **`CaptureFocus` (Trạng thái Bị Bắt Giữ)**:
   - Khóa cứng mọi thao tác điều khiển chuột của người chơi.
   - Ống kính camera thực hiện quay cận cảnh vào khoảnh khắc lính gác túm lấy nhân vật trong $1.0\text{ s}$, sau đó dập tắt màn hình về màu đen (Fade to Black).

---

### Interactions with Other Systems

- **Với Player Controller (`#11`)**:
  - Giao diện cung cấp: `float GetCameraYaw()` trả về góc xoay phương vị của camera trong mặt phẳng ngang thế giới để Controller tính toán vector vận tốc di chuyển WASD.
  - Giao diện tiêu thụ: Đọc `Transform` của nhân vật để làm Follow Target và kiểm tra trạng thái di chuyển (để thực thi quy tắc đứng yên `AC-P25`).
- **Với Input System (`#19`)**:
  - Đọc trực tiếp Action `Look` dạng `Vector2` từ New Input System mỗi frame. Tự động áp dụng hệ số nhạy chuột `mouse_sensitivity` và cờ đảo chiều trục `invert_y`.
- **Với Physics System (`#18`)**:
  - Sử dụng hàm `Physics.SphereCast` truy vấn LayerMask `World` để xác định cự ly va chạm của chướng ngại vật E20 Solid.
- **Với Event / Messaging Bus (`#15`)**:
  - Đăng ký nhận các sự kiện:
    - `GuardStateChangedEvent`: Phát hiện trạng thái `Chase` để mở rộng FOV.
    - `PlayerEnclosedStateChangedEvent`: Chuyển đổi giữa `CM_FreeOrbit` và `CM_HideSpot`.
    - `PlayerCapturedEvent`: Kích hoạt trạng thái `CaptureFocus`.
- **Với Suspicion Meter / Grade Operator (`#7`)**:
  - Cung cấp ma trận chiếu ViewMatrix của camera để hệ thống UI Grade chiếu các mũi tên cảnh báo đa hướng chính xác lên không gian màn hình 2D (Screen-Space Projection).

## Formulas

### D1: Phép Biến đổi Hệ quy chiếu WASD & Chuẩn hóa Mặt phẳng XZ (Camera-Relative Basis Transformation & Normalization)

Quy đổi vector tín hiệu đầu vào điều khiển từ thiết bị (bàn phím/gamepad) sang vector hướng di chuyển thực tế trong không gian thế giới dựa trên góc phương vị `camera_yaw` ($\theta$):

1. **Đầu vào từ Trục Thiết bị ($\vec{I}$)**:
   $$\vec{I} = \begin{pmatrix} I_x \\ I_y \end{pmatrix} = \begin{pmatrix} \text{Axis}_{\text{horizontal}} \\ \text{Axis}_{\text{vertical}} \end{pmatrix} \in [-1.0, 1.0]^2$$
   - Trong đó: Phím `W` $\to I_y = +1.0$, phím `S` $\to I_y = -1.0$, phím `D` $\to I_x = +1.0$, phím `A` $\to I_x = -1.0$.

2. **Chuyển đổi Hệ quy chiếu Ma trận Góc nhìn sang Không gian Thế giới XZ ($\vec{d}_{\text{raw}}$)**:
   $$\vec{d}_{\text{raw}} = \begin{pmatrix} d_X \\ d_Z \end{pmatrix} = \begin{pmatrix} I_y \sin\theta + I_x \cos\theta \\ I_y \cos\theta - I_x \sin\theta \end{pmatrix}$$
   - Khớp chính xác với hợp đồng cơ sở `H.0.5` trong `player-third-person-controller.md`:
     - Nhấn giữ `W` ($I_x = 0, I_y = 1$): $\vec{d}_{\text{raw}} = (\sin\theta, \cos\theta)$.
     - Nhấn giữ `D` ($I_x = 1, I_y = 0$): $\vec{d}_{\text{raw}} = (\cos\theta, -\sin\theta)$.

3. **Chuẩn hóa Hướng Di chuyển Mặt phẳng XZ ($\vec{d}_{\text{move}}$)**:
   $$\vec{d}_{\text{move}} = \begin{cases}
   \frac{\vec{d}_{\text{raw}}}{\|\vec{d}_{\text{raw}}\|} = \frac{1}{\sqrt{d_X^2 + d_Z^2}} \begin{pmatrix} d_X \\ d_Z \end{pmatrix} & \text{if } \|\vec{d}_{\text{raw}}\| > 0.01 \\
   \begin{pmatrix} 0 \\ 0 \end{pmatrix} & \text{if } \|\vec{d}_{\text{raw}}\| \le 0.01
   \end{cases}$$
   - **Bảo đảm Bất biến**: Độ dài $\|\vec{d}_{\text{move}}\|$ luôn luôn bằng $1.0$ (khi có input) hoặc $0.0$ (khi thả phím), bảo đảm di chuyển chéo `W+D` có độ lớn đúng $1.0$, loại trừ hoàn toàn lỗi tăng tốc $\sqrt{2} \approx 1.414\times$ (tuân thủ nguyên tắc `E1` của Controller).

- **Dải giá trị đầu ra (Output Range)**: $\|\vec{d}_{\text{move}}\| \in \{0.0, 1.0\}$, $d_X, d_Z \in [-1.0, 1.0]$.
- **Ví dụ tính toán (Worked Example)**:
  - Camera đang nhìn hướng Đông Bắc: $\theta = 45^\circ$ ($\sin 45^\circ = \cos 45^\circ = \frac{\sqrt{2}}{2} \approx 0.7071$).
  - Người chơi nhấn giữ phím `W` tiến tới ($I_x = 0, I_y = 1$):
    - $d_X = 1 \cdot \sin 45^\circ + 0 \cdot \cos 45^\circ = 0.7071$.
    - $d_Z = 1 \cdot \cos 45^\circ - 0 \cdot \sin 45^\circ = 0.7071$.
    - $\|\vec{d}_{\text{raw}}\| = \sqrt{0.7071^2 + 0.7071^2} = 1.0$.
    - $\vec{d}_{\text{move}} = (0.7071, 0.7071)$ (nhân vật tiến thẳng theo hướng nhìn camera về phía Đông Bắc).
  - Người chơi nhấn giữ phím chéo `W+D` ($I_x = 1, I_y = 1$):
    - $d_X = 1 \cdot 0.7071 + 1 \cdot 0.7071 = 1.4142$.
    - $d_Z = 1 \cdot 0.7071 - 1 \cdot 0.7071 = 0.0$.
    - $\|\vec{d}_{\text{raw}}\| = \sqrt{1.4142^2 + 0^2} = 1.4142$.
    - Sau khi chuẩn hóa: $\vec{d}_{\text{move}} = (1.4142 / 1.4142, 0) = (1.0, 0.0)$ (nhân vật di chuyển sang hướng Đông với độ lớn đúng $1.0$, vận tốc đạt chính xác $V_{\text{run}} = 6.25\text{ m/s}$, không bị gian lận tốc độ).

---

### D2: Khử Va chạm Khối cầu & Tính toán Khoảng cách Hiệu chỉnh (Spherecast Deocclusion Solver)

Xác định khoảng cách an toàn của ống kính máy quay nhằm ngăn ngừa việc xuyên qua các mặt phẳng hình học của môi trường E20 Solid:

1. **Vị trí Khảo sát Khối cầu**:
   - Điểm bắt đầu quét: $\vec{P}_{\text{origin}} = \vec{P}_{\text{target}}$ (tọa độ ngực nhân vật).
   - Hướng quét: $\vec{u}_{\text{cam}} = -\text{Forward}_{\text{cam}}$ (hướng từ ngực nhân vật lùi về phía sau theo góc xoay camera).
   - Bán kính khối cầu kiểm tra: $R_{\text{cam\_col}} = 0.20\text{ m}$.
   - Khoảng cách quét tối đa: $L_{\text{cast}} = D_{\text{nom}} = 2.80\text{ m}$.

2. **Công thức Tính Khoảng cách Mục tiêu ($D_{\text{target}}$)**:
   $$D_{\text{target}} = \begin{cases}
   \max\left(D_{\text{min}}, d_{\text{hit}} - R_{\text{cam\_col}}\right) & \text{if } \text{SpherecastHit}(\vec{P}_{\text{origin}}, R_{\text{cam\_col}}, \vec{u}_{\text{cam}}, L_{\text{cast}}, \text{Layer}_{\text{World}}) == \text{true} \\
   D_{\text{nom}} & \text{if không phát hiện va chạm}
   \end{cases}$$
   - Trong đó:
     - $d_{\text{hit}}$: Khoảng cách từ tâm khối cầu bắt đầu đến điểm va chạm thực tế ghi nhận từ `RaycastHit.distance`.
     - $D_{\text{min}} = 0.40\text{ m}$: Khoảng cách tối thiểu của máy quay so với nhân vật.
     - $D_{\text{nom}} = 2.80\text{ m}$: Khoảng cách mặc định tự do.

- **Dải giá trị đầu ra (Output Range)**: $D_{\text{target}} \in [0.40, 2.80]\text{ m}$.
- **Ví dụ tính toán (Worked Example)**:
  - Nhân vật đứng cách bức tường gạch E20 phía sau một đoạn $1.50\text{ m}$.
  - Tia Spherecast chạm mặt phẳng tường tại $d_{\text{hit}} = 1.50\text{ m}$.
  - $D_{\text{target}} = \max(0.40, 1.50 - 0.20) = \max(0.40, 1.30) = 1.30\text{ m}$.
  - Khoảng cách mục tiêu tự động rút ngắn từ $2.80\text{ m} \to 1.30\text{ m}$, cách mặt tường đúng $0.20\text{ m}$, bảo đảm hình học của tường không bao giờ chạm vào Near Clip Plane ($0.05\text{ m}$) của camera.

---

### D3: Hàm Giảm chấn Hồi phục Khoảng cách Bất đối xứng (Asymmetric Distance Damping)

Ngăn chặn giật nảy hình ảnh khi di chuyển qua các bờ tường nhấp nhô, đồng thời bảo đảm camera thu ngắn tức thì khi gặp chướng ngại:

$$D(t + \Delta t) = \begin{cases}
D_{\text{target}} & \text{if } D_{\text{target}} < D(t) \quad (\text{Thu ngắn tức thì — Instant Collapse}) \\
D(t) + (D_{\text{target}} - D(t)) \cdot \left(1 - e^{-\frac{\Delta t}{\tau_{\text{recover}}}}\right) & \text{if } D_{\text{target}} \ge D(t) \quad (\text{Nới rộng giảm chấn — Damped Recovery})
\end{cases}$$
- Với $\tau_{\text{recover}} = 0.25\text{ s}$ là hằng số thời gian hồi phục độ dài camera.

- **Dải giá trị đầu ra (Output Range)**: $D(t) \in [0.40, 2.80]\text{ m}$.
- **Ví dụ tính toán (Worked Example)**:
  - Frame 1: Người chơi vừa bước qua mép tường hẹp ra đại sảnh. Khoảng cách hiện tại $D(t) = 1.30\text{ m}$, khoảng cách mục tiêu mới $D_{\text{target}} = 2.80\text{ m}$.
  - Thời gian delta $\Delta t = 0.0166\text{ s}$ ($60\text{ fps}$):
    - Tỷ lệ giảm chấn: $\alpha = 1 - e^{-0.0166 / 0.25} = 1 - e^{-0.0664} \approx 1 - 0.9358 = 0.0642$.
    - $D(t + \Delta t) = 1.30 + (2.80 - 1.30) \times 0.0642 = 1.30 + 1.50 \times 0.0642 \approx 1.396\text{ m}$.
  - Sau $\sim 0.75\text{ s}$ ($3\tau$), camera phục hồi $95\%$ khoảng cách về $2.72\text{ m}$ một cách êm ái, người chơi không hề cảm thấy một cú giật hình nào.

---

### D4: Điều biến Tiêu cự Rượt đuổi Năng động (Dynamic Chase FOV Scaling)

Mở rộng trường nhìn quang học (FOV) theo trạng thái FSM nhằm gia tăng cảm giác tốc độ và mức độ nguy kịch:

1. **Xác định Tiêu cự Mục tiêu ($\text{FOV}_{\text{target}}$) và Hằng số Thời gian ($\tau_{\text{fov}}$)**:
   $$\begin{cases}
   \text{FOV}_{\text{target}} = 68.0^\circ, \quad \tau_{\text{fov}} = 0.18\text{ s} & \text{if có lính gác ở trạng thái Chase} \\
   \text{FOV}_{\text{target}} = 60.0^\circ, \quad \tau_{\text{fov}} = 0.45\text{ s} & \text{if không có lính gác ở trạng thái Chase}
   \end{cases}$$

2. **Công thức Làm mịn Tiêu cự Mũ**:
   $$\text{FOV}(t + \Delta t) = \text{FOV}(t) + (\text{FOV}_{\text{target}} - \text{FOV}(t)) \cdot \left(1 - e^{-\frac{\Delta t}{\tau_{\text{fov}}}}\right)$$

- **Dải giá trị đầu ra (Output Range)**: $\text{FOV} \in [60.0^\circ, 68.0^\circ]$.
- **Ví dụ tính toán (Worked Example)**:
  - Lính gác phát hiện và hú còi rượt đuổi (`Chase` bắt đầu): $\text{FOV}(0) = 60.0^\circ$.
  - Trong vòng $0.36\text{ s}$ ($2\tau$), FOV mở rộng mượt mà từ $60.0^\circ \to 66.9^\circ$, tạo hiệu ứng thị giác "giật lùi" (dolly-zoom feel) tinh tế, làm nổi bật cảm giác hoảng hốt của cuộc truy đuổi.

## Edge Cases

Hệ thống Camera Cinemachine Rig xử lý minh bạch và dứt khoát 7 kịch bản biên bất thường, bảo đảm độ ổn định cao nhất cho hệ thống góc nhìn và chống hiện tượng phá vỡ đồ họa (graphic glitches / geometry clipping):

### EC1: Kẹp Tường Góc Nhọn Cực Hạn & Làm Mờ Mô Hình Nhân Vật (Extreme Corner Wedging & Dither Fade)
- **Kịch bản**: Người chơi di chuyển lùi sát vào góc tường nhọn ($90^\circ$ hoặc $< 90^\circ$), cả hai mặt tường E20 Solid đều áp sát và ép khoảng cách camera xuống dưới $D_{\text{min}} = 0.40\text{ m}$.
- **Hành vi xác định**:
  1. Thuật toán Spherecast khóa cứng sàn khoảng cách tối thiểu tại $D_{\text{min}} = 0.40\text{ m}$, tuyệt đối không cho phép máy quay lùi xuyên qua mặt phẳng tường ra ngoài màn chơi.
  2. Khi khoảng cách camera thực tế $D_{\text{actual}} \le 0.60\text{ m}$, hệ thống kích hoạt hiệu ứng mờ đục dạng lưới dither (Screen-Door Dither Transparency) trên Shader của mô hình nhân vật Player. Mức độ mờ đục tỷ lệ nghịch với khoảng cách:
     $$\text{Opacity}_{\text{dither}} = \text{clamp}\left(\frac{D_{\text{actual}} - D_{\text{min}}}{0.60 - D_{\text{min}}}, 0.15, 1.0\right) = \text{clamp}\left(\frac{D_{\text{actual}} - 0.40}{0.20}, 0.15, 1.0\right)$$
  3. Mô hình nhân vật trở nên bán trong suốt (đạt tối đa $85\%$ độ trong suốt ở $D_{\text{min}}$), bảo đảm không bao giờ che khuất tầm nhìn phía trước của người chơi ngay cả khi bị ép sát góc tường.

### EC2: Rơi Tự Do Từ Trên Cao & Giới Hạn Dây Cương Cứng (Rapid Vertical Drop & Hard Leash Threshold)
- **Kịch bản**: Nhân vật rơi tự do xuống hố thang máy, nhảy qua gờ tường cao hoặc bị rớt thẳng đứng với vận tốc $V_y < -8.0\text{ m/s}$.
- **Hành vi xác định**:
  1. Theo phương thẳng đứng (Trục Y), Cinemachine Position Damping áp dụng độ trễ mềm $\tau_Y = 0.10\text{ s}$ để giữ chuyển động mắt nhìn không bị giật cục.
  2. Tuy nhiên, nếu khoảng cách thẳng đứng giữa camera và điểm neo ngực nhân vật vượt quá ngưỡng giới hạn dây cương cứng $\Delta Y_{\text{leash}} = 2.50\text{ m}$, hệ thống tức thì hủy bỏ giảm chấn (snap hard override) và buộc camera hạ độ cao ngay lập tức cùng tốc độ với nhân vật:
     $$Y_{\text{cam}} = \min(Y_{\text{cam}}, Y_{\text{target}} + \Delta Y_{\text{leash}})$$
  3. Người chơi không bao giờ bị mất dấu nhân vật ra khỏi cạnh dưới của khung hình khi tiếp đất.

### EC3: Gián Đoạn Tiến Trình Vào/Thoát Tủ Nấp Do Bị Bắt Giữ (HideSpot Transition Interruption by Capture)
- **Kịch bản**: Người chơi đang trong quá trình chuyển đổi camera $T_{\text{blend}} = 0.35\text{ s}$ giữa `CM_FreeOrbit` và `CM_HideSpot` thì lính gác áp sát và bắt giữ thành công (`PlayerCapturedEvent` phát ra).
- **Hành vi xác định**:
  1. Bộ điều phối `CinemachineBrain` lập tức hủy bỏ tiến trình hòa trộn đang diễn ra (`CancelActiveBlend()`).
  2. Hệ thống chuyển đổi ngay tức thì ($0\text{ ms}$ blend) sang Virtual Camera tử ẹo `CaptureFocus`.
  3. Trục ngắm của `CaptureFocus` khóa chặt vào vị trí lính gác đang tóm lấy nhân vật, vô hiệu hóa toàn bộ input chuột của người chơi và chuẩn bị cho hiệu ứng GameOver Fade to Black.

### EC4: Vẩy Chuột Dị Thường / Xung Đột Tốc Độ Góc Cực Đại (Extreme Mouse Flick & Angular Velocity Limiter)
- **Kịch bản**: Người chơi vẩy chuột cơ học cực mạnh hoặc xảy ra lỗi đột biến xung nhịp chuột (Mouse Hardware Delta Spike) trả về delta chuột $\Delta x > 5000\text{ px/frame}$.
- **Hành vi xác định**:
  1. Đầu vào chuột được kiểm soát thông qua bộ kẹp tốc độ góc cực đại:
     $$\Delta \theta_{\text{yaw\_applied}} = \text{sign}(\Delta x) \cdot \min\left(|\Delta x| \cdot S_{\text{mouse}}, \omega_{\text{max}} \cdot \Delta t\right)$$
     Với trần tốc độ góc $\omega_{\text{max}} = 720^\circ/\text{s}$ ($2$ vòng quay trọn vẹn mỗi giây).
  2. Loại trừ triệt để tình trạng camera xoay hàng chục vòng trong một frame duy nhất, ngăn ngừa say tàu xe (motion sickness) và triệt tiêu lỗi suy biến tính toán va chạm.

### EC5: Ổn Định Vector Thế Giới Tại Góc Chúi Cực Đại (Pitch Extrema Clamping & Gimbal Lock Prevention)
- **Kịch bản**: Người chơi lia chuột liên tục lên đỉnh trời hoặc chúi thẳng xuống chân nhân vật trong thời gian dài.
- **Hành vi xác định**:
  1. Góc chúi $\theta_{\text{pitch}}$ bị chặn cứng trong khoảng giới hạn an toàn $[-35.0^\circ, +65.0^\circ]$.
  2. Phép quay Orbit của Cinemachine luôn sử dụng trục thẳng đứng thế giới $\vec{u}_{\text{world}} = (0, 1, 0)$ làm vector quy chiếu chuẩn (World Up Vector), không bao giờ tính toán xoay tích lũy cục bộ.
  3. Khử hoàn toàn hiện tượng khóa trục xoay (Gimbal Lock) và không để camera chúi xuống dưới sàn gây lộ khoảng trống vô tận (ground void clipping).

### EC6: Thay Đổi Kích Thước Khung Hình WebGL Canvas (Aspect Ratio Resize 16:9 / 21:9 / 4:3)
- **Kịch bản**: Người chơi co giãn cửa sổ trình duyệt WebGL hoặc thay đổi độ phân giải màn hình đột ngột giữa Fullscreen và Windowed.
- **Hành vi xác định**:
  1. Thấu kính của tất cả Virtual Camera được khóa cứng ở chế độ `Field of View: Vertical` ($60.0^\circ$).
  2. Chiều cao khung nhìn hiển thị luôn được bảo toàn độc lập với tỷ lệ màn hình; chiều ngang tự động mở rộng theo tỷ lệ khung hình (Aspect Ratio) mà không làm méo mó mô hình nhân vật hay sai lệch cự ly va chạm $D_{\text{nom}}, D_{\text{min}}$.

### EC7: Đóng Băng Tuyệt Đối Khi Mở Menu Tạm Dừng (TimeScale = 0 & Input Freezing)
- **Kịch bản**: Người chơi nhấn phím `Escape` mở Menu Tạm dừng (`Time.timeScale = 0.0f`).
- **Hành vi xác định**:
  1. Bộ điều khiển Camera ngắt hoàn toàn việc tiếp nhận tín hiệu từ Action Map `Player/Look`.
  2. Máy quay đóng băng tuyệt đối vị trí và hướng quay tại frame cuối cùng trước khi tạm dừng, hiển thị khung cảnh tĩnh phía sau lớp phủ làm mờ (Pause Blur Overlay) của giao diện UI.
  3. Khi thoát tạm dừng (`Time.timeScale = 1.0f`), bộ đệm delta chuột được làm sạch (`ResetMouseDelta()`) để tránh cú giật hình khi tiếp tục chơi.

## Dependencies

Hệ thống Camera Cinemachine Rig vận hành theo nguyên tắc phân tách trách nhiệm (Separation of Concerns), tương tác với các hệ thống khác thông qua hợp đồng dữ liệu hai chiều rõ ràng và giao diện dịch vụ `ICameraService` gọn nhẹ, tránh coupling trực tiếp vào các component cụ thể của Cinemachine:

### Upstream Dependencies (Hệ thống Cung cấp Dữ liệu Đầu vào)

1. **Input System (`#19` - `design/gdd/input-system.md`)**:
   - **Dữ liệu tiêu thụ**:
     - Action `Player/Look` (`Vector2`): Vector delta di chuyển của chuột hoặc gạt cần analog joystick phải mỗi frame.
     - Cấu hình người dùng: Hệ số nhạy chuột `mouse_sensitivity` ($S_{\text{mouse}} \in [0.05, 1.00]$) và cờ đảo chiều trục Y `invert_y` (`bool`).
   - **Tần suất truy vấn**: Mỗi frame trong pha `Update()` trước khi Cinemachine tính toán vị trí.
   - **Ràng buộc hai chiều**: `input-system.md` sở hữu định nghĩa Action Map `Player/Look` và bảo đảm tính hợp lệ của tín hiệu đầu vào.

2. **Physics & Collision Config (`#18` - `design/gdd/physics-collision-config.md`)**:
   - **Dữ liệu tiêu thụ**:
     - Lớp va chạm `LayerMask.GetMask("World")` chứa các khối hình học tường E20 Solid, trụ cột kiến trúc và chướng ngại vật tĩnh của màn chơi.
     - Hàm truy vấn vật lý: `Physics.SphereCast(origin, radius, direction, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore)`.
   - **Ràng buộc hai chiều**: Bỏ qua hoàn toàn Layer `TriggerVolume`, `Enemy`, `Player` để camera không bị co giật khi lính gác hoặc đồ vật di động lướt qua ống kính.

3. **Event / Messaging Bus (`#15` - `design/gdd/event-bus.md`)**:
   - **Sự kiện đăng ký nhận (Subscribed Events)**:
     - `GuardStateChangedEvent`: Phát hiện sự kiện lính gác chuyển sang trạng thái `Chase` hoặc kết thúc `Chase` để điều biến tiêu cự $\text{FOV}$ (Formula D4).
     - `PlayerEnclosedStateChangedEvent`: Nhận thông báo người chơi vào tủ nấp (`is_enclosed == true`) hoặc rời tủ (`is_enclosed == false`) để kích hoạt chuyển đổi giữa `CM_FreeOrbit` và `CM_HideSpot`.
     - `PlayerCapturedEvent`: Nhận thông báo người chơi bị bắt để chuyển quyền điều khiển sang `CaptureFocus` và đóng băng tương tác.
   - **Ràng buộc hai chiều**: Đăng ký sự kiện an toàn trong `OnEnable()` và hủy đăng ký trong `OnDisable()` để chống rò rỉ bộ nhớ (Memory Leak).

4. **Player Movement & Hide (`#5` - `design/gdd/player-movement-hide.md`)**:
   - **Dữ liệu tiêu thụ**:
     - Vị trí `Transform aperture_portal_target` của tủ nấp (độ cao quan sát $h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height} / 2)$).
     - Góc định hướng của cửa tủ nấp để khóa nón nhìn trong biên độ $\pm 30^\circ$.

---

### Downstream Dependents (Hệ thống Tiêu thụ Dữ liệu Đầu ra)

1. **Player Third-Person Controller (`#11` - `design/gdd/player-third-person-controller.md`)**:
   - **Dịch vụ cung cấp**:
     ```csharp
     public interface ICameraService {
         float GetCameraYaw();              // Góc xoay phương vị quanh trục Y thế giới (đơn vị: radian hoặc degree)
         Vector3 GetCameraForwardPlanar();  // Vector hướng nhìn chiếu phẳng trên mặt đất XZ
         Vector3 GetCameraRightPlanar();    // Vector hướng sang phải chiếu phẳng trên mặt đất XZ
         Matrix4x4 GetViewMatrix();         // Ma trận góc nhìn camera phục vụ chiếu không gian
     }
     ```
   - **Dữ liệu cung cấp**:
     - `GetCameraYaw()`: Nguồn chân lý duy nhất để Controller thực hiện phép biến đổi hệ quy chiếu WASD sang vector di chuyển thế giới theo công thức D1 (`H.0.5`).
   - **Cam kết bất biến**: Tuân thủ triệt để nguyên tắc `AC-P25`: Khi vận tốc WASD $= 0$, `camera_yaw` biến thiên không kích hoạt xoay thân nhân vật; nhân vật duy trì `facing` bit-identical.

2. **Suspicion Meter & Grade UI (`#7` - `design/gdd/suspicion-meter-grade.md`)**:
   - **Dữ liệu cung cấp**: `GetViewMatrix()` và ma trận chiếu `Camera.main.projectionMatrix`.
   - **Mục đích**: Hệ thống UI Grade sử dụng ma trận này để chiếu tọa độ 3D của lính gác đang nghi ngờ lên mặt phẳng 2D màn hình (`Camera.WorldToViewportPoint`), tính toán vị trí hiển thị các mũi tên cảnh báo đa hướng (Off-Screen Threat Indicators) chính xác tại mép màn hình.

3. **Audio & UI Feedback (`#13` - `design/gdd/audio-ui-feedback.md`)**:
   - **Dữ liệu cung cấp**: Thực thể `MainCamera` mang thành phần `AudioListener`.
   - **Mục đích**: `AudioListener` di chuyển và xoay đồng bộ với ống kính camera, xác lập tâm điểm không gian cho toàn bộ các nguồn phát âm thanh 3D (3D Spatial Audio Emitters: tiếng bước chân lính gác, tiếng còi hú, tiếng thì thầm), bảo đảm âm trường nổi (Stereo Panning) và độ suy giảm âm lượng theo khoảng cách chuẩn xác theo góc nhìn của người chơi.

## Tuning Knobs

Tất cả các giá trị cấu hình của hệ thống Camera được thiết kế theo mô hình điều khiển bằng dữ liệu (Data-Driven Configuration via `CameraConfig` ScriptableObject), cho phép tinh chỉnh nhanh chóng trong Unity Editor mà không cần can thiệp mã nguồn:

### Nhóm 1: Định vị & Khung hình Orbit (Framing & Orbit Geometry)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Lối chơi & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `camera_nominal_distance` | `2.80` | mét (m) | `[1.50, 4.50]` | Khoảng cách camera danh định ở điều kiện tự do. Cự ly $2.80\text{ m}$ bảo đảm tầm nhìn bao quát không gian hẹp của nhà thương điên trong khi vẫn giữ nhân vật đủ gần để cảm nhận tính mong manh. | CR3, D2 |
| `camera_min_distance` | `0.40` | mét (m) | `[0.25, 0.80]` | Khoảng cách tối thiểu của camera khi bị ép sát tường. Không được nhỏ hơn $0.25\text{ m}$ để tránh clipping hình học đầu nhân vật. | CR4, D2 |
| `camera_shoulder_offset_x` | `+0.35` | mét (m) | `[0.00, 0.60]` | Độ lệch ngang sang vai phải. Đặt nhân vật ở vị trí $1/3$ bên trái khung hình, giải phóng tâm nhìn để ném đạn nén Burst hoặc soi đèn pin. | CR3 |
| `camera_target_height_y` | `1.35` | mét (m) | `[1.00, 1.80]` | Độ cao điểm neo ngực nhân vật so với mặt đất. Căn chuẩn tâm ngắm ngang tầm mắt trung bình của lính gác. | CR3 |

### Nhóm 2: Khử Va Chạm & Giảm Chấn (Deocclusion & Damping)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Lối chơi & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `camera_spherecast_radius` | `0.20` | mét (m) | `[0.10, 0.35]` | Bán kính khối cầu kiểm tra va chạm tường E20 Solid. Đệm an toàn ngăn ngừa góc thấu kính camera chạm vào Near Clip Plane ($0.05\text{ m}$). | CR4, D2 |
| `camera_distance_recover_tau`| `0.25` | giây (s) | `[0.10, 0.60]` | Hằng số thời gian nới rộng khoảng cách camera khi thoát chướng ngại vật. Ngăn giật hình khi lướt qua các bờ tường nhấp nhô. | CR5, D3 |
| `camera_vertical_leash_threshold`| `2.50` | mét (m) | `[1.50, 4.00]` | Ngưỡng giới hạn dây cương cứng theo trục Y khi rơi tự do. Vượt quá ngưỡng này camera hủy damping để bắt kịp nhân vật tiếp đất. | EC2 |
| `camera_dither_fade_start` | `0.60` | mét (m) | `[0.45, 0.90]` | Ngưỡng khoảng cách bắt đầu kích hoạt làm mờ dither mô hình nhân vật khi bị ép sát góc tường. | EC1 |

### Nhóm 3: Tiêu cự & Rượt đuổi Căng thẳng (FOV & Tension Dynamics)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Lối chơi & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `camera_base_fov` | `60.0` | độ ($^\circ$) | `[50.0, 75.0]` | Tiêu cự góc nhìn dọc (Vertical FOV) mặc định khi rình rập và khám phá. Tạo cảm giác điện ảnh trung tính, ít méo góc. | CR8, D4 |
| `camera_chase_fov` | `68.0` | độ ($^\circ$) | `[60.0, 85.0]` | Tiêu cự mở rộng khi bị lính gác rượt đuổi (`Chase`). Gia tăng trường nhìn cánh và cảm giác tốc độ hoảng hốt. | CR8, D4 |
| `camera_fov_expand_tau` | `0.18` | giây (s) | `[0.08, 0.40]` | Hằng số thời gian mở rộng FOV khi bắt đầu cuộc rượt đuổi. Đạt $95\%$ độ mở sau $\approx 0.54\text{ s}$. | D4 |
| `camera_fov_contract_tau` | `0.45` | giây (s) | `[0.20, 1.00]` | Hằng số thời gian thu hẹp FOV về cơ sở sau khi cắt đuôi thành công. Giúp nhịp thở thị giác lắng dịu mượt mà. | D4 |
| `camera_hidespot_blend_time` | `0.35` | giây (s) | `[0.20, 0.60]` | Thời gian hòa trộn Cinemachine Blend giữa `CM_FreeOrbit` và `CM_HideSpot` khi vào/ra tủ nấp. | CR1, CR7 |

### Nhóm 4: Điều khiển & Giới hạn Góc quay (Input & Pitch Extrema)

| Tên Biến (Knob Identifier) | Giá trị Mặc định | Đơn vị | Dải An toàn [Min, Max] | Tác động Lối chơi & Rationale | Dẫn chiếu |
|---|---|---|---|---|---|
| `camera_pitch_min` | `-35.0` | độ ($^\circ$) | `[-50.0, -20.0]` | Giới hạn góc chúi xuống đất. Ngăn ngừa camera đâm xuyên mặt sàn hoặc lộn ngược góc nhìn. | CR2, EC5 |
| `camera_pitch_max` | `+65.0` | độ ($^\circ$) | `[+45.0, +80.0]` | Giới hạn góc ngước lên trần nhà. Đủ để quan sát lỗ thông gió trên trần mà không bị Gimbal Lock. | CR2, EC5 |
| `camera_max_angular_velocity` | `720.0` | độ/giây ($^\circ/\text{s}$) | `[360.0, 1440.0]` | Trần tốc độ góc tối đa của trục xoay ngang, triệt tiêu đột biến xung chuột phần cứng (mouse delta spikes). | EC4 |
| `camera_mouse_sensitivity_default` | `0.30` | vô thứ nguyên | `[0.05, 1.00]` | Hệ số nhạy chuột mặc định, mang lại trải nghiệm lia chuột 1:1 chuẩn xác trên màn hình PC và WebGL. | CR2 |

## Visual/Audio Requirements

### Visual Requirements (Yêu cầu Thị giác & Đồ họa)

1. **URP Post-Processing Volumes Động (Dynamic URP Volume Blending)**:
   - Hệ thống camera quản lý 2 Volume Profile URP được hòa trộn mượt mà theo trạng thái FSM:
     - **Profile Khám phá / Rình rập (`FreeOrbit`)**: Tông màu lạnh (Cool Grade - White Balance: $-15$), Vignette nhẹ nhàng (Intensity: $0.20$, Smoothness: $0.40$), Depth of Field vô hiệu hóa để tối ưu hóa hiệu năng WebGL (giữ dưới $1000$ draw calls).
     - **Profile Rượt đuổi Căng thẳng (`ChaseTension`)**: Khi trạng thái `Chase` kích hoạt, hòa trộn sang profile căng thẳng trong $0.30\text{ s}$:
       - **Chromatic Aberration**: Cường độ $0.35$ (tạo vệt quang sai nhẹ ở mép màn hình mô phỏng thị giác adrenaline).
       - **Vignette Sẫm Đỏ**: Cường độ tăng lên $0.42$ với viền màu đỏ tía tối, mô phỏng cảm giác tầm nhìn đường hầm (Tunnel Vision).
       - **Motion Blur Nhẹ**: Giới hạn ở mức tối thiểu ($0.15$) hoặc tự động tắt trên WebGL để tránh giảm sụt khung hình.

2. **Rung Chấn Ống Kính Vi Mô (Camera Micro-Shake via Cinemachine Impulse)**:
   - Khi có lính gác ở trạng thái `Chase` áp sát người chơi trong cự ly $d \le 4.0\text{ m}$:
     - Kích hoạt nguồn phát chấn động `CinemachineImpulseSource` dạng ồn tần số cao 6D Perlin Noise:
       - Biên độ rung cực đại: $A_{\text{shake}} = 0.02\text{ m}$ (khoảng dịch chuyển vị trí) và $0.5^\circ$ (góc quay).
       - Tần số rung: $12.0\text{ Hz}$.
     - Rung chấn phản ánh nhịp tim hoảng loạn và bước chân dồn dập của lính gác, tuyệt đối không rung lắc giật mạnh gây mất phương hướng hoặc cản trở khả năng điều khiển WASD của người chơi.

3. **Mặt Nạ Quan Sát Điểm Nấp Tủ Kín (HideSpot Peep-Hole Slit Mask)**:
   - Khi Virtual Camera `CM_HideSpot` kích hoạt:
     - Hiển thị lớp phủ UI toàn màn hình (Fullscreen Overlay) mang texture dạng rãnh chấn song khe hẹp (Vertical Slit / Louver slats), che tối $65\%$ diện tích màn hình ở hai bên cánh và giữ sáng khe nhìn trung tâm.
     - Tạo trải nghiệm ngột ngạt, hồi hộp của một kẻ đang nín thở quan sát qua khe tủ.

4. **Vật Liệu Mờ Dither Nhân Vật (Screen-Door Dither Transparency Shader)**:
   - Sử dụng Shader Graph URP tùy biến cho vật liệu của Player:
     - Khi cự ly $D_{\text{actual}} \le 0.60\text{ m}$, áp dụng dither alpha pattern theo ma trận Bayer $4 \times 4$.
     - Bảo đảm nhân vật mờ dần mà không kích hoạt pass vẽ trong suốt (Transparent Render Queue), giữ nguyên chuẩn opaque depth write, tối ưu hóa $100\%$ cho WebGL.

---

### Audio Requirements (Yêu cầu Âm thanh & Không gian)

1. **Định Vị Tâm Điểm Âm Thanh Không Gian (AudioListener Spatial Orientation)**:
   - Thành phần `AudioListener` duy nhất của trò chơi được gắn cố định trên thực thể `MainCamera`.
   - Vị trí và góc quay của ống kính quyết định trực tiếp hướng nghe âm thanh nổi 3D (3D Spatial Panning), bảo đảm người chơi quay camera về hướng nào thì âm thanh tiếng bước chân hay tiếng thì thầm của lính gác sẽ định vị chính xác ở tai tương ứng.

2. **Âm Thanh Biến Đổi Tiêu Cự & Hòa Trộn (Lens Transition Audio Feedback)**:
   - **Âm Whoosh Co Giãn FOV**: Khi FOV mở rộng từ $60.0^\circ \to 68.0^\circ$ lúc bắt đầu cuộc rượt đuổi, kích hoạt một âm thanh lướt gió tần số thấp (`sfx_camera_chase_whoosh`, $80 - 180\text{ Hz}$, âm lượng $-14.0\text{ dBFS}$) trong $0.25\text{ s}$, nhấn mạnh cảm giác thị giác bị kéo dãn.
   - **Âm Đóng Cửa Tủ Điểm Nấp**: Khi chuyển đổi sang `CM_HideSpot`, âm thanh tiếng cọt kẹt và sập chốt cửa gỗ nhẹ (`sfx_locker_door_close`) phát ra đúng thời điểm hoàn tất hòa trộn $0.35\text{ s}$.

## UI Requirements

### 1. Tâm Ngắm Năng Động (Contextual Sub-Reticle)

- **Trạng thái Mặc định (Khám phá / Lén lút)**: Không hiển thị bất kỳ tâm ngắm nào trên màn hình. Toàn bộ khung nhìn sạch sẽ $100\%$, duy trì tính điện ảnh đắm chìm (Cinematic Immersion).
- **Trạng thái Chuẩn Bị Ném Đạn Nén (`PrepareBurstState`)**:
  - Khi người chơi nhấn giữ chuột phải để chuẩn bị ném chai đạn nén Burst:
    - Hiển thị một chấm tròn mờ màu trắng ngà (Subtle Dot Reticle, đường kính $6\text{ px}$, độ mờ $40\%$) tại tâm màn hình để hỗ trợ ngắm điểm va chạm.
    - Chấm tròn tự động phóng to nhẹ thành vòng tròn $12\text{ px}$ khi tâm ngắm quét qua một bề mặt tường gạch E20 hợp lệ có khả năng phản dội âm thanh.
  - Khi thả chuột hoặc hủy ném: Chấm tròn mờ dần và biến mất trong $0.15\text{ s}$.

### 2. Menu Cài Đặt Camera & Hỗ Trợ Tiếp Cận Toàn Diện (Camera Settings & Accessibility Suite)

Giao diện Menu Cài đặt (Options > Camera / Controls) cung cấp đầy đủ các thanh điều khiển tùy biến cao cấp:

1. **Độ Nhạy Chuột (Mouse Sensitivity)**:
   - Thanh trượt từ $0.05$ đến $1.00$ (bước nhảy $0.01$, mặc định $0.30$).
   - Cho phép người chơi điều chỉnh tốc độ xoay phù hợp với độ phân giải màn hình và DPI chuột.
2. **Đảo Chiều Trục Y (Invert Y-Axis)**:
   - Nút bật/tắt (Toggle: On / Off, mặc định Off).
   - Đảo ngược hướng ngước/chúi của camera khi di chuyển chuột theo trục dọc cho những người chơi quen thuộc phong cách mô phỏng bay.
3. **Cường Độ Rung Màn Hình (Screen Shake Intensity — Motion Sickness Accessibility)**:
   - Thanh trượt từ $0.0$ đến $1.0$ (mặc định $1.0$).
   - Ở mức $0.0$: Vô hiệu hóa $100\%$ các hiệu ứng rung chấn ống kính `CinemachineImpulse` khi bị rượt đuổi, bảo vệ tối đa người chơi có tiền sử say tàu xe hoặc rối loạn tiền đình.
4. **Tùy Chỉnh Tiêu Cự Cơ Sở (Base Field of View Slider)**:
   - Thanh trượt từ $55.0^\circ$ đến $75.0^\circ$ (mặc định $60.0^\circ$).
   - Cho phép người chơi màn hình góc rộng hoặc người dễ say góc hẹp mở rộng trường nhìn mà không làm phá vỡ các tỷ lệ khung hình.
5. **Tâm Điểm Cố Định Chống Say (Persistent Center Dot Accessibility)**:
   - Nút bật/tắt (Toggle: On / Off, mặc định Off).
   - Khi bật, một chấm trắng tĩnh nhỏ ($4\text{ px}$, opacity $25\%$) luôn hiển thị ở chính giữa màn hình làm điểm neo thị giác (Visual Anchor), giúp giảm thiểu hội chứng say chuyển động (Motion Sickness).

## Acceptance Criteria

Mọi tiêu chí dưới đây phải được kiểm thử tự động (Unit / Integration Tests) hoặc kiểm thử thủ công có tài liệu xác thực (QA Verification Checklist) với kết quả nhị phân (Pass / Fail):

### AC1: Tính Toán Quy Đổi Hệ Quy Chiếu WASD Chuẩn Xác (Formula D1 & H.0.5 Compliance)
- **Điều kiện kiểm thử**: Giả lập tín hiệu đầu vào bàn phím tại các góc quay camera khác nhau:
  1. Khi $\theta_{\text{yaw}} = 45^\circ$ ($\sin 45^\circ = \cos 45^\circ \approx 0.7071$), người chơi nhấn phím `W` ($I_x = 0, I_y = 1$).
  2. Người chơi nhấn giữ đồng thời hai phím chéo `W+D` ($I_x = 1, I_y = 1$).
- **Kết quả đạt (Pass)**:
  - Trường hợp 1: Vector hướng thế giới trả về đúng $(0.7071, 0.7071)$ với sai số $\|\vec{\epsilon}\| \le 10^{-4}$.
  - Trường hợp 2: Vector sau chuẩn hóa có độ lớn chính xác $\|\vec{d}_{\text{move}}\| = 1.0000$, vận tốc di chuyển đạt đúng $V_{\text{run}} = 6.25\text{ m/s}$ (sai số $< 0.01\text{ m/s}$), loại trừ hoàn toàn hiện tượng tăng tốc $\sqrt{2}\times$.

### AC2: Bất Biến Khóa Hướng Nhân Vật Khi Đứng Yên (`AC-P25` Compliance)
- **Điều kiện kiểm thử**: Nhân vật đứng yên tại chỗ (vận tốc đầu vào WASD $= 0$). Người chơi lia chuột xoay camera tự do một vòng $360^\circ$ quanh nhân vật.
- **Kết quả đạt (Pass)**:
  - Giá trị góc phương vị `camera_yaw` biến thiên liên tục từ $0^\circ \to 360^\circ$.
  - Góc xoay thân `facing` của `PlayerTransform.rotation` giữ nguyên giá trị bit-identical không thay đổi ($0.0^\circ$ sai lệch). Không xảy ra hiện tượng nhân vật tự động xoay mặt theo camera.

### AC3: Thu Ngắn Khoảng Cách Khử Va Chạm Khối Cầu Tức Thời (Formula D2 Compliance)
- **Điều kiện kiểm thử**: Đặt nhân vật đứng trước bức tường gạch E20 Solid và lùi camera về phía tường. Điểm va chạm Spherecast được ghi nhận tại khoảng cách $d_{\text{hit}} = 1.50\text{ m}$ (nhỏ hơn cự ly tự do $D_{\text{nom}} = 2.80\text{ m}$).
- **Kết quả đạt (Pass)**:
  - Khoảng cách camera $D_{\text{actual}}$ lập tức thu ngắn về $\max(0.40, 1.50 - 0.20) = 1.30\text{ m}$ trong thời gian $0\text{ ms}$ (ngay trong frame hiện tại).
  - Khối thấu kính camera không bao giờ đâm xuyên qua bề mặt tường E20 Solid (Zero Geometry Clipping).

### AC4: Nới Rộng Giảm Chấn Phục Hồi Khoảng Cách Mượt Mà (Formula D3 Compliance)
- **Điều kiện kiểm thử**: Cho nhân vật di chuyển từ khe hẹp sát tường ra sảnh lớn thông thoáng. Khoảng cách ban đầu $D(0) = 1.30\text{ m}$, khoảng cách mục tiêu mới $D_{\text{target}} = 2.80\text{ m}$.
- **Kết quả đạt (Pass)**:
  - Khoảng cách tăng dần theo hàm số mũ với hằng số thời gian $\tau_{\text{recover}} = 0.25\text{ s}$.
  - Tại thời điểm $t = 0.75\text{ s}$ ($3\tau$), khoảng cách đạt tối thiểu $\ge 95\%$ giá trị danh định ($D \ge 2.72\text{ m}$).
  - Đường cong dịch chuyển vị trí camera hoàn toàn trơn tru, không có hiện tượng nảy giật khung hình (Zero Camera Stutter/Jitter).

### AC5: Khóa Góc Chúi & Ổn Định Vector Thế Giới (CR2 & EC5 Compliance)
- **Điều kiện kiểm thử**: Người chơi lia chuột liên tục lên trần nhà và chúi thẳng xuống sàn đất.
- **Kết quả đạt (Pass)**:
  - Góc chúi $\theta_{\text{pitch}}$ bị chặn cứng chính xác trong dải $[-35.0^\circ, +65.0^\circ]$.
  - Vector quy chiếu chuẩn World Up luôn duy trì $(0, 1, 0)$, không bao giờ xảy ra lỗi khóa trục xoay (Gimbal Lock) hoặc để camera chúi xuống dưới sàn gây lộ khoảng trống vô tận.

### AC6: Mở Rộng & Thu Hẹp Tiêu Cự Rượt Đuổi Mượt Mà (Formula D4 Compliance)
- **Điều kiện kiểm thử**: Phát sự kiện `GuardStateChangedEvent` kích hoạt trạng thái `Chase`, sau đó phát sự kiện kết thúc `Chase`.
- **Kết quả đạt (Pass)**:
  - Khi bắt đầu `Chase`: FOV mở rộng từ $60.0^\circ \to 68.0^\circ$ với $\tau_{\text{fov}} = 0.18\text{ s}$ (đạt $\ge 67.6^\circ$ sau $0.54\text{ s}$).
  - Khi kết thúc `Chase`: FOV từ từ co hẹp về $60.0^\circ$ với $\tau_{\text{fov}} = 0.45\text{ s}$ (đạt $\le 60.4^\circ$ sau $1.35\text{ s}$).

### AC7: Hòa Trộn Virtual Camera Điểm Nấp & Ngắt Quãng Bắt Giữ (CR7 & EC3 Compliance)
- **Điều kiện kiểm thử**:
  1. Người chơi kích hoạt chui vào tủ nấp (`PlayerEnclosedStateChangedEvent(true)`).
  2. Lính gác bắt được người chơi trong khi camera đang hòa trộn vào tủ nấp.
- **Kết quả đạt (Pass)**:
  - Trường hợp 1: Cinemachine chuyển đổi êm ái sang `CM_HideSpot` trong đúng thời gian $0.35\text{ s}$ theo đường cong `EaseInOut`. Khi đã vào trong tủ, góc xoay nhìn bị khóa trong nón $\pm 30^\circ$.
  - Trường hợp 2: Tiến trình hòa trộn bị hủy bỏ ngay lập tức, camera chuyển đổi tức thì ($0\text{ ms}$) sang `CaptureFocus`, khóa góc nhìn vào lính gác và vô hiệu hóa mọi input chuột.

### AC8: Làm Mờ Dither Mô Hình Nhân Vật Khi Kẹp Góc Hẹp (EC1 Compliance)
- **Điều kiện kiểm thử**: Lùi nhân vật vào góc tường hẹp cực hạn khiến khoảng cách camera bị ép xuống $D_{\text{actual}} \le 0.60\text{ m}$.
- **Kết quả đạt (Pass)**:
  - Shader dither kích hoạt trên mô hình nhân vật Player.
  - Khi $D_{\text{actual}} = 0.40\text{ m}$, độ trong suốt dither đạt $85\%$ (độ mờ đục chỉ còn $15\%$), bảo đảm toàn bộ tâm nhìn phía trước của người chơi không bị cơ thể nhân vật che khuất.

### AC9: Bắt Kịp Tức Thì Khi Rơi Tự Do Quá Giới Hạn (EC2 Compliance)
- **Điều kiện kiểm thử**: Cho nhân vật rơi tự do xuống hố sâu với vận tốc $V_y < -8.0\text{ m/s}$ và độ lệch độ cao vượt quá $\Delta Y > 2.50\text{ m}$.
- **Kết quả đạt (Pass)**:
  - Camera tức thì hủy bỏ position damping theo trục Y và hạ độ cao cùng tốc độ với nhân vật, giữ nhân vật luôn hiển thị trong khung hình quan sát khi tiếp đất.

### AC10: Trần Tốc Độ Góc Chống Vẩy Chuột Dị Thường (EC4 Compliance)
- **Điều kiện kiểm thử**: Bơm xung nhịp chuột đột biến giả lập với delta $\Delta x = 10000\text{ px/frame}$.
- **Kết quả đạt (Pass)**:
  - Tốc độ xoay góc của camera trong frame đó bị kẹp chặt không vượt quá $\omega_{\text{max}} = 720.0^\circ/\text{s}$ ($\Delta \theta \le 12.0^\circ$ tại frame $60\text{ fps}$), không gây say tàu xe hay vỡ hình.

### AC11: Đóng Băng Tuyệt Đối Khi Mở Menu Tạm Dừng (EC7 Compliance)
- **Điều kiện kiểm thử**: Mở Menu Tạm dừng (`Time.timeScale = 0.0f`) và thực hiện di chuyển chuột liên tục.
- **Kết quả đạt (Pass)**:
  - Camera bỏ qua hoàn toàn Action `Player/Look`. Tọa độ vị trí và góc phương vị của camera đóng băng $100\%$ không suy dịch. Sau khi đóng menu, không xảy ra hiện tượng giật camera.

### AC12: Hiệu Năng Render & Ngân Sách Draw Calls Trên WebGL
- **Điều kiện kiểm thử**: Chạy Scene màn chơi hoàn chỉnh trên trình duyệt WebGL ở độ phân giải $1920 \times 1080$.
- **Kết quả đạt (Pass)**:
  - Thời gian tính toán CPU của `CinemachineBrain` và thuật toán Spherecast trong pha `LateUpdate` tiêu tốn $\le 1.0\text{ ms}$/frame.
  - Tổng số draw calls của toàn bộ hệ thống camera (bao gồm Dither Shader và URP Volume Blending) duy trì $< 1000$ draw calls, duy trì ổn định tốc độ $60\text{ fps}$ không bị drop frame.

---

## Open Questions

- **OQ1: Cơ chế Đổi Vai Nhìn (Shoulder Swap Feature)**:
  - *Câu hỏi*: Có nên hỗ trợ thêm nút chuyển đổi vai nhìn (Phím `Q` hoặc `E` chuyển đổi giữa vai phải $+0.35\text{ m}$ và vai trái $-0.35\text{ m}$) không?
  - *Đề xuất*: Giữ cố định vai phải $+0.35\text{ m}$ trong giai đoạn MVP để giữ phạm vi kiểm thử và hệ thống điều khiển tinh gọn, cân nhắc bổ sung trong Polish Phase nếu người chơi phản hồi nhu cầu ngắm góc tường trái.
- **OQ2: Hạ Độ Cao Điểm Neo Khi Cúi Ngồi (`Crouch Anchor Height`)**:
  - *Câu hỏi*: Khi nhân vật chuyển sang trạng thái cúi ngồi `Crouch`, điểm neo ngực camera có nên hạ thấp từ $1.35\text{ m} \to 1.05\text{ m}$ không?
  - *Đề xuất*: Chỉ hạ nhẹ $0.15\text{ m}$ ($1.35\text{ m} \to 1.20\text{ m}$) để duy trì góc nhìn thoải mái và tránh việc camera bị chúi sát xuống sàn gây va chạm với các đồ vật thấp trên mặt đất.
