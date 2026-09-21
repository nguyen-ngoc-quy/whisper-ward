# ADR-0005: Input Action Asset & Stance Buffering Contract

## Status
Accepted

## Date
2026-09-21 (Accepted via Technical Setup & Master Architecture Baseline)

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Package** | Unity New Input System (`com.unity.inputsystem`, v1.7.0+) |
| **Domain** | Foundation / Core Layer (Input & Locomotion) |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md`, `design/gdd/input-system.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | EditMode tests cho bộ đệm Stance/Throw buffer timing; PlayMode tests cho đổi thiết bị (Hot-swap) và lưu JSON remapping |

## ADR Dependencies

| Field | Value |
|---|---|
| **Depends On** | `ADR-0001: Deterministic Event/Messaging Bus`, `ADR-0002: Physics Collision Contract` |
| **Enables** | `IPlayerController` locomotion, camera aiming, throw mechanics, UI navigation |
| **Blocks** | Triển khai mã nguồn `PlayerThirdPersonController.cs` và `WhisperWardInputService.cs` |
| **Ordering Note** | Hợp đồng này là tiền đề kỹ thuật bắt buộc trước khi lập trình bộ điều khiển di chuyển nhân vật |

---

## Context

### Problem Statement
Whisper Ward là game stealth góc nhìn thứ ba đòi hỏi phản xạ nhanh và sự chuẩn xác tuyệt đối trong việc né tránh nón tầm nhìn của lính gác. Trong các bản thử nghiệm prototype, hai vấn đề kỹ thuật thường xuyên gây ức chế cho người chơi:
1. **Nuốt nút khi thao tác gấp (Dropped Inputs / Frame-Mismatch)**: Khi người chơi đang chạy hoặc bò, nhả phím di chuyển và bấm ném đạn Burst hoặc bấm đứng dậy, nếu lệnh nhấn rơi vào khoảnh khắc nhân vật đang giảm tốc hoặc đang dưới gầm thấp, lệnh bấm bị mất hoàn toàn ("nuốt nút").
2. **Cấp phát bộ nhớ rác (GC Spikes)**: Việc gọi `InputActionAsset.FindAction("Move")` hoặc `action.ReadValue<T>()` qua boxing đối tượng tạo ra rác bộ nhớ (Garbage Collection) trong vòng lặp chính, gây tụt khung hình (Stutter) trên trình duyệt WebGL.

### Constraints & Requirements
- **Unity 6 LTS**, C# 9+, chạy mượt mà 60 fps trên cả PC và WebGL.
- **Zero-GC trên Hot-Path**: Tuyệt đối không phân bổ bộ nhớ trong các vòng lặp đọc input (`Update`, `FixedUpdate`).
- **Action Map Exclusivity**: Phân tách triệt để giữa `Player` (In-Game) và `UI` (Menu/Pause). Khi mở Pause, toàn bộ trạng thái input gameplay phải được flush sạch sẽ về zero để tránh kẹt phím.
- **Stance & Throw Buffering**: Cửa sổ đệm thời gian $150\text{ ms} - 200\text{ ms}$ cho các thao tác ném Burst và chuyển đổi tư thế (Crouch $\to$ Stand, Sprint).
- **Hỗ trợ Trợ năng (Standard Tier)**: Tự động phát hiện đổi thiết bị (Keyboard/Mouse vs Gamepad) để đổi icon HUD và hỗ trợ lưu JSON remapping phím (`PlayerPrefs` / `LocalStorage`).

---

## Decision

### 1. Kiến trúc C# Wrapper `WhisperWardInputActions`
Sử dụng công cụ sinh mã tự động của Unity Input System để tạo ra lớp C# wrapper mạnh kiểu (`WhisperWardInputActions.cs`) từ asset `WhisperWardInputActions.inputactions`.
* **Tuyệt đối cấm**: Tìm kiếm action qua chuỗi tên (VD: `asset.FindAction("Move")`) trong runtime.
* Toàn bộ các tham chiếu `InputAction` được khởi tạo và cache một lần duy nhất tại hàm khởi tạo của `WhisperWardInputService`.

### 2. Hợp đồng Bộ dịch vụ `IInputService`
```csharp
namespace WhisperWard.Core.Contracts
{
    public enum InputContextMode
    {
        Gameplay,
        UI,
        Disabled
    }

    public readonly struct PlayerInputSnapshot
    {
        public readonly Vector2 Move;
        public readonly Vector2 Look;
        public readonly bool IsSprintHeld;
        public readonly bool CrouchTriggered;
        public readonly bool ThrowTriggered;
        public readonly bool InteractTriggered;
        public readonly bool PauseTriggered;
    }

    public interface IInputService
    {
        InputContextMode CurrentMode { get; }
        string ActiveControlScheme { get; }
        PlayerInputSnapshot GetCurrentSnapshot();
        void SetContextMode(InputContextMode mode);
        void SaveUserBindingsJson(string key = "ww_input_bindings_v1");
        void LoadUserBindingsJson(string key = "ww_input_bindings_v1");
        void ResetToDefaultBindings();
    }
}
```

### 3. Hợp đồng Bộ đệm Tư thế & Đạn Ném (Stance & Throw Buffering)
* **Cửa sổ đệm (Buffer Window)**: Khi người chơi bấm phím chuyển đổi tư thế hoặc ném trong điều kiện chưa đủ điều kiện hình học:
  $$\Delta t_{\text{buffer}} \le 150\text{ ms} \quad (0.15\text{ s})$$
* **Quy tắc Kiểm tra Độ thoáng Trần (Clearance Check)**:
  * Khi yêu cầu đứng dậy (Uncrouch) hoặc tự động đứng dậy để chạy (Auto-Stand to Sprint): Hệ thống gọi `IPhysicsQueryService.CheckSphereClearance(headOrigin, radius = 0.2f, LayerMask.World)`.
  * Nếu trần $< 1.8\text{ m}$: Lệnh đứng dậy được lưu vào bộ đệm trong tối đa $150\text{ ms}$. Nếu trong $150\text{ ms}$ người chơi bò ra khỏi vùng trần thấp, lệnh đứng dậy lập tức được thi hành tự động. Hết $150\text{ ms}$ mà vẫn vướng trần, lệnh tự hủy an toàn.
* **Quy tắc Bẻ Vận tốc Dừng khi Ném (Throw Snap)**:
  * Nếu bấm ném khi nhân vật đang giảm tốc với vận tốc $V < 1.0\text{ m/s}$: Bộ điều khiển bẻ ngay vận tốc về $0.0\text{ m/s}$ trong cùng tick vật lý và giải phóng đạn ném, triệt tiêu việc ném trượt do đà di chuyển.

### 4. Lưu trữ Tùy biến Phím (Persistence Contract)
* Toàn bộ binding overrides được tuần tự hóa bằng `InputActionAsset.SaveBindingOverridesAsJson()`.
* Dữ liệu JSON được lưu trữ trong `PlayerPrefs` (trên Windows Standalone) hoặc đồng bộ sang `window.localStorage["ww_input_bindings_v1"]` (trên WebGL build).

---

## Consequences

### Tích cực
- **Zero-GC**: Đọc giá trị 2D qua `ReadValue<Vector2>()` của C# wrapper không sinh rác bộ nhớ, loại bỏ giật lag trên WebGL.
- **Trải nghiệm mượt mà**: Bộ đệm $150\text{ ms}$ giải quyết triệt để cảm giác nuốt nút, tạo cảm giác điều khiển đanh thép và chuẩn xác (Fair Mind-Challenge).
- **Tuân thủ Trợ năng**: Hỗ trợ đầy đủ Rebind phím và tự động đổi icon gợi ý phím khi cắm tay cầm.

### Hạn chế & Giảm thiểu
- **Phụ thuộc Physics Query**: Mỗi lần kiểm tra Uncrouch cần 1 tia SphereCast. Giảm thiểu bằng cách chỉ bắn tia khi có sự kiện nhấn phím C hoặc giữ Shift (Event-driven, không bắn tia mỗi frame).
