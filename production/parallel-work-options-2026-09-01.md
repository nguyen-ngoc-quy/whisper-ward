# Công việc song song — 2026-09-01

> Danh sách việc có thể làm song song trong lúc `/design-review design/gdd/player-noise.md`
> (full mode) đang chạy và trước khi fix pass bắt đầu. Tạo theo yêu cầu user.
> Snapshot lúc tạo file: **10/11 specialist đã trả lời**; 1 đang chạy (ai-programmer —
> stall 2 lần, đã resume 3 lần); verdict đang hướng tới **NEEDS REVISION**
> (9 blocker đã xác nhận qua 4 specialist, 9/9 đã verify trực tiếp vào text/code).
> Ước tính còn 1–2 vòng re-review nữa mới tới APPROVED.

## Đ phân nhóm đèn giao thông

| Nhóm | Làm ngay? | Lý do |
|---|---|---|
| **A. Code / môi trường** | ✅ Ngay (A1 chờ 3 specialist cuối trả về) | Không đụng file nào trong fix pass |
| **B. Quyết định của user** | ✅ Ngay | Chỉ quyết định, không ghi đè file đang review |
| **C. Lưu evidence review** | ✅ Ngay (cần user duyệt) | Chỉ ghi vào `production/qa/` |
| **D. Review GDD khác** | ✅ Nhưng **session riêng** | Read-only nhưng cần panel agent riêng |
| **E. Thiết kế hệ thống kề bên** | ⚠️ Tùy chọn | Finding vòng này vẫn là input cho nó |
| **F. Fix pass / fixture / registry** | ❌ Đợi | Trùng tuyến chính — sẽ va chạm |
| **G. Tracking records / consistency-check** | ❌ Đợi | Chỉ đổi khi APPROVED / sau fix pass |

---

## A. Code & môi trường — zero conflict

### A1. Sửa 3 code conformance debt trong `src/AI/` — scope S
Nguồn: performance-analyst (design đã normative — đây là lỗi phía code):
1. **Comparator thiếu khóa**: `src/AI/Perception/PerceptionHearingService.cs:972-989`
   sorts fact theo `(source_timestamp, fact_id, sequence)` và pair theo
   `(guard_eid, sequence)` — thiếu `source_event_class_rank` + `source_event_id`
   (khóa normative trong `entities.yaml:856-870`; AC5 bind service phải expose đủ).
2. **Guard comparator**: code sort theo `guard_eid` đơn thuần
   (`PerceptionHearingService.cs:992-997`) — khớp phương án 2 của performance-analyst
   F5, nhưng **phương án phải được design chốt trước** (drop position vs định nghĩa
   quantized lexicographic). → Gộp vào quyết định B3, chưa sửa code.
3. **`max_frame_delta_s` clamp thiếu**: `src/AI/Core/VirtualTickClock.cs:72-87`
   `Advance` không có clamp 0.0667 s + diagnostic `clock-delta-clamped`
   (`entities.yaml:1003-1009` yêu cầu). Mục này **không cần quyết định gì** — sửa được ngay.
- Verify bằng suite kiểm thử hiện có trong `src/AI/Testing/` nếu chạy được.
- ⚠️ Chỉ bắt đầu **sau khi 3 specialist cuối trả về** (ai-programmer có thể đang đọc `src/AI/`).

### A2. Bootstrap môi trường Unity 6 — scope M (phần lớn user-side)
- Cài Unity 6 LTS **6000.3.17f1** + module WebGL.
- Tạo project skeleton + packages: URP, Input System (new), Cinemachine,
  AI Navigation (NavMesh), ProBuilder, Unity Test Framework (NUnit).
- Zero conflict với design docs; **mở đường thực thi** cho OQ1/OQ2/OQ3/OQ6 —
  mọi gate implementation hiện đang chờ runtime này.
- Note: kỹ năng `docs/engine-reference/unity/VERSION.md` đã pin đúng version — LOW risk.

---

## B. Quyết định của user (không ghi file đang review)

### B1. Adjudicate OQ3 — chọn audio middleware — scope S (quyết định) → ADR mới
- **Câu hỏi**: Wwise (candidate duy nhất đã khảo sát) vs stock Unity no-middleware.
- **Vì sao ngay**: AC19 (`confirmed` onset) structurally không pass được cho đến khi
  OQ3 chốt một readback mechanism (qa-lead F-10: never waived, phải báo open nếu trượt).
  Đây là quyết định của user — không phụ thuộc review Player Noise.
- Deliverable: `docs/architecture/adr-0003-audio-middleware.md` (file mới, zero conflict)
  + checklist per-platform tolerance (kể cả WebGL decode-buffer latency — unity U6).

### B2. Chốt giá trị OQ6 bench protocol — scope S (quyết định TD)
- Qa-lead F-6: pin `reference_browser`, `reference_gpu_class`, `reference_resolution`,
  `reference_quality_tier` **trước** khi chấp nhận phép đo OQ6 đầu tiên.
- Quyết định giá trị làm ngay; ghi vào registry/AC21 thì **gộp vào fix pass** (tránh conflict).

### B3. Tư duy trước batch quyết định design của fix pass
Fix pass sẽ hỏi **một lần duy nhất** (multi-tab) các quyết định sau — nghĩ trước giúp trả nhanh:
- **I1** (root-selection): restrict Δy<0 ascending-face only vs `contact_mode` chọn root.
- **I2** (drain order): re-key theo `t_publish` vs giữ Burst-first + thêm AC leg.
- **B3-level** (siting rule): pickup ≥ `R_walk_eff` + margin vs crouch-mandatory approach beats.
- **A1/A2-level** (accessibility): kênh non-audio cho reception feedback (screen-edge tick + subtitle) và kênh non-color cho emission flash (glyph/shape).
- **B2-level** (gate): physical occluder ngoài patrol corridor vs objective-marker + vision case.
- **F5** (guard-selection key): drop position (dùng `guard_eid` ordinal) vs quantized lexicographic.

---

## C. Lưu evidence review vòng này — cần user duyệt — scope S
- 9 báo cáo specialist hiện **chỉ tồn tại trong conversation** — mất khi compact.
- Đề xuất: lưu từng báo cáo vào
  `production/qa/reviews/player-noise-2026-09-01/[tên-specialist].md` (thư mục mới).
- Lợi ích: input trực tiếp cho fix pass + re-review sau đối chiếu "prior items resolved";
  accessibility-specialist cũng đã tự đề xuất viết audit riêng.
- Không đụng file nào trong fix pass.

---

## D. Review GDD khác — session riêng, chạy song song được
- **`/design-review design/gdd/player-movement-hide.md`** — status In Review, GDD
  complete (Section D đã qua adversarial systems review, Section H đã reframe bởi qa-lead),
  đang chờ full review theo review-index.
- Cần **session Claude riêng** (panel riêng, ~10+ agent) — không chạy chung session
  này vì review Player Noise đang giữ fleet agent.
- Contract chung với Noise (CR6 noise-not-silenced, lifecycle table) đã ổn định qua 4 vòng.

---

## E. Tùy chọn — thiết kế hệ thống kề bên
- **Suspicion Meter / Grade (#6)** — F6/R_noise_share đã verify digit-exact qua nhiều
  vòng (stable), có thể bắt đầu skeleton `design/gdd/suspicion-grade.md`.
- ⚠️ Rủi ro rework trung bình: finding vòng này ảnh hưởng trực tiếp nó
  (qa-lead F-1 marker leg unowned; ux F13 residual invisibility in MVP no-attribution build).
- Game-designer IMP-2 cảnh báo acceptance surface đã vượt 8 tuần — mở hệ thống mới
  là scope call của producer/user. **Khuyến nghị: chỉ làm nếu user chủ động muốn.**

---

## F. PHẢI ĐỢI — tuyến chính (không song song)
- **Fix pass blocker** — file set đóng băng:
  `design/gdd/player-noise.md`, `design/gdd/guard-ai-fsm.md`,
  `design/gdd/sound_performance_audit.md`, `design/registry/entities.yaml`,
  `design/levels/mvp-burst-route-fixture.md`, `design/fixtures/noise-fixture-spec.md`
  (+ có thể `design/gdd/perception.md`, `docs/architecture/adr-0002-*.md` cho remedy U1–U4).
- **Fixture re-author B1–B5** (room nhất quán, patrol timing/dwell, vision case) —
  cần verdict creative-director + các quyết định B3 đã chốt.
- **Registry batch edits** (I3 root_product `:1254`, I4 band relabel, F7
  `required_budget_ms`→`diagnostic`, OQ6 pins) — gộp 1 lần vào fix pass.
- **Harness-spec amendments** (qa-lead F-3/F-4/F-5/F-7/F-8) — nằm trong
  `noise-fixture-spec.md` = fix-pass file set.

## G. KHÔNG ĐỤNG
- `design/gdd/systems-index.md` + `design/gdd/reviews/player-noise-review-log.md` —
  **chỉ đổi khi APPROVED thật** (rồi commit+push theo convention).
- `/consistency-check` — chỉ chạy sau fix pass, trước re-review.

---

## Checklist gợi ý hôm nay (pick nhiều càng tốt)

- [ ] B1 — chốt OQ3 (quyết định user, 15 phút suy nghĩ + ADR draft)
- [ ] B2 — chốt OQ6 reference hardware values
- [ ] C — duyệt lưu 9 báo cáo specialist làm evidence
- [ ] D — mở session Claude #2 chạy review player-movement-hide.md
- [ ] A2 — cài Unity 6 + packages (user-side, chạy nền)
- [ ] A1.3 — sửa `max_frame_delta_s` clamp trong VirtualTickClock.cs (sau khi 3 specialist cuối trả về)
- [ ] B3 — đọc trước 6 quyết định design sẽ được hỏi ở fix pass

*Cập nhật lần cuối: 2026-09-01 — tạo trong session review Player Noise (9/11 specialist xong).*
