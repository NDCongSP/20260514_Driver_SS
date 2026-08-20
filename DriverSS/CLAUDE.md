# CLAUDE.md — Project Intelligence File
> Đọc file này **trước tiên** mỗi khi bắt đầu làm việc với project.  
> Dành cho: Claude Code · Claude Cowork · Cursor · Copilot  
> Cập nhật lần cuối: xem `## CHANGELOG`

---

## 📌 MỤC LỤC

1. [Project Overview](#1-project-overview)
2. [Architecture Manual](#2-architecture-manual)
3. [Coding Standards & Comment Rules](#3-coding-standards--comment-rules)
4. [Session Memory & Context Linking](#4-session-memory--context-linking)
5. [Changelog — Edit Log](#5-changelog--edit-log)
6. [Unit Test Guidelines](#6-unit-test-guidelines)
7. [Performance Optimization Rules](#7-performance-optimization-rules)
8. [How to Use This File](#8-how-to-use-this-file)

---

## 1. PROJECT OVERVIEW

```yaml
project_name:     "<TÊN_PROJECT>"
version:          "0.1.0"
language:         "<TypeScript | Python | Go | ...>"
framework:        "<Next.js | FastAPI | ...>"
package_manager:  "<npm | pnpm | pip | ...>"
primary_author:   "<Tên>"
repo:             "<URL>"
env:              "development"   # development | staging | production
```

### Mục tiêu
> Mô tả ngắn gọn: project này giải quyết vấn đề gì, cho ai, theo cách nào.

### Ràng buộc quan trọng
- [ ] Không dùng thư viện X vì lý do Y
- [ ] Mọi API call phải qua `src/lib/api.ts`
- [ ] Không commit secret / key trực tiếp vào code

---

## 2. ARCHITECTURE MANUAL

### 2.1 Sơ đồ thư mục

```
project-root/
├── src/
│   ├── components/       # UI components (dumb, presentational)
│   ├── containers/       # Smart components / page-level logic
│   ├── hooks/            # Custom React hooks
│   ├── lib/              # Shared utilities, API clients
│   ├── services/         # Business logic, external integrations
│   ├── store/            # State management (Redux / Zustand / Jotai)
│   ├── types/            # Global TypeScript types / interfaces
│   └── utils/            # Pure helper functions
├── tests/
│   ├── unit/             # Unit tests (*.test.ts)
│   ├── integration/      # Integration tests
│   └── e2e/              # End-to-end tests (Playwright / Cypress)
├── docs/                 # Architecture diagrams, ADRs
├── scripts/              # Dev/build/deploy scripts
├── CLAUDE.md             # ← File này
└── ...
```

### 2.2 Luồng dữ liệu (Data Flow)

```
[UI Component]
     │ dispatch / call hook
     ▼
[Store / Hook]
     │ calls
     ▼
[Service Layer]   ←── chứa toàn bộ business logic
     │ calls
     ▼
[API Client / lib/api.ts]
     │ HTTP / SDK
     ▼
[External API / DB]
```

### 2.3 Quy tắc phân tầng (Layer Rules)

| Layer       | Được phép import              | Không được import   |
|-------------|-------------------------------|----------------------|
| components  | hooks, types, utils           | services, store trực tiếp |
| hooks       | store, services, utils        | components           |
| services    | lib, utils, types             | components, hooks    |
| lib/api     | types                         | mọi layer khác       |

### 2.4 Quyết định kiến trúc (ADR — Architecture Decision Records)

| ID    | Ngày       | Quyết định                        | Lý do                        | Trạng thái  |
|-------|------------|-----------------------------------|------------------------------|-------------|
| ADR-1 | YYYY-MM-DD | Dùng Zustand thay Redux           | Bundle nhỏ hơn, API đơn giản | Accepted    |
| ADR-2 | YYYY-MM-DD | Tất cả fetch qua `lib/api.ts`     | Centralize error handling    | Accepted    |
<!-- Thêm ADR mới vào đây -->

---

## 3. CODING STANDARDS & COMMENT RULES

### 3.1 Cấu trúc comment bắt buộc

#### File header (mọi file source)
```typescript
/**
 * @file        src/services/userService.ts
 * @description Xử lý nghiệp vụ liên quan đến User: CRUD, auth, profile.
 * @author      <Tên> <email>
 * @created     YYYY-MM-DD
 * @modified    YYYY-MM-DD — <mô tả thay đổi ngắn>
 * @see         docs/user-flow.md
 */
```

#### Function / Method
```typescript
/**
 * Lấy thông tin user theo ID từ database.
 *
 * @param   {string}          userId  - UUID của user cần tìm
 * @param   {FetchOptions}    opts    - Tuỳ chọn cache / force-refresh
 * @returns {Promise<User>}           - Object User hoặc throw nếu không tìm thấy
 * @throws  {NotFoundError}           - Khi userId không tồn tại
 *
 * @example
 *   const user = await getUser("abc-123");
 *   console.log(user.name);
 */
async function getUser(userId: string, opts?: FetchOptions): Promise<User> { ... }
```

#### Inline comment — chỉ dùng khi logic KHÔNG tự giải thích được
```typescript
// ✅ Đúng: giải thích tại sao, không phải cái gì
const delay = 350; // Debounce 350ms — dưới ngưỡng này user chưa dừng gõ

// ❌ Sai: lặp lại code
const delay = 350; // gán delay bằng 350
```

#### TODO / FIXME / HACK
```typescript
// TODO(username, YYYY-MM-DD): Migrate sang API v2 sau khi backend deploy
// FIXME(username, YYYY-MM-DD): Race condition khi 2 tab cùng gọi refresh
// HACK(username, YYYY-MM-DD): Workaround bug iOS Safari — xoá khi upgrade lib
// PERF(username, YYYY-MM-DD): Bottleneck ở đây — xem CHANGELOG#PERF-001
```

### 3.2 Naming conventions

| Loại            | Convention         | Ví dụ                        |
|-----------------|--------------------|------------------------------|
| Variable/Param  | camelCase          | `userId`, `fetchOptions`     |
| Function        | camelCase verb     | `getUser`, `handleSubmit`    |
| Class/Interface | PascalCase         | `UserService`, `FetchOptions`|
| Constant        | UPPER_SNAKE_CASE   | `MAX_RETRY`, `API_BASE_URL`  |
| File (TS/JS)    | kebab-case         | `user-service.ts`            |
| CSS class       | kebab-case / BEM   | `btn--primary`               |
| Test file       | `*.test.ts`        | `user-service.test.ts`       |

### 3.3 Code style tóm tắt

```typescript
// Max line length: 100 chars
// Indent: 2 spaces (không dùng tab)
// Semicolons: bắt buộc (TypeScript)
// Single quote cho strings
// Trailing comma trong multi-line objects/arrays
// Arrow function cho callbacks
// async/await — không dùng .then().catch() thuần (trừ khi chain phức tạp)
```

---

## 4. SESSION MEMORY & CONTEXT LINKING

> **Mục đích:** Giúp Claude duy trì ngữ cảnh giữa các phiên làm việc mà không cần đọc lại toàn bộ code.

### 4.1 Active Context — Việc đang làm

```yaml
# Cập nhật phần này MỖI KHI kết thúc session làm việc
active_context:
  current_task:     "Thêm kết nối trực tiếp qua cổng COM (RS232/USB-to-Serial) cho Scale Driver, song song với TCP/IP hiện có (qua bộ chuyển đổi RS232-to-TCP) — cấu hình được ConnectionType (Tcp/Com) lúc khởi tạo. Đã implement Ở TẦNG DÙNG CHUNG (ScaleDriver.cs/ScaleConfig.cs) nên áp dụng cho MỌI model cân cùng lúc (không cần sửa riêng từng Scale_*), theo đúng yêu cầu 'test Vibra_HAW30 trước, OK thì triển khai hàng loạt' — vì kiến trúc vốn đã dùng chung 1 driver cho tất cả model (model cân chỉ là DLL parser nạp qua reflection, không biết TCP hay COM). Build sạch (exit 0) qua MSBuild, nhưng CHƯA test với cân thật qua COM — user chưa xác nhận. Session sau đó (cùng ngày): thêm project ScaleSimulator (WinForms, độc lập) mô phỏng cân qua TCP VÀ qua COM để user có thể tự test luồng đọc dữ liệu/auto-reconnect/Stable-detection MÀ KHÔNG CẦN cân thật, trong lúc chờ có cân Vibra_HAW30 vật lý — xem ScaleSimulator/README.md."
  related_files:
    - "ScanAndScale.Core/Models/ScaleConfig.cs"           # ScaleConnectionType enum (Tcp/Com) + ConnectionType/ComPort/BaudRate
    - "ScanAndScale.Core/Drivers/ScaleDriver.cs"          # Connect/Read/Reconnect dispatcher theo ConnectionType; nhánh Com dùng SerialPort.BaseStream + StreamReader (leaveOpen), cùng chiến lược drain-backlog như nhánh Tcp (BytesToRead thay Socket.Available)
    - "WpfSample/ViewModels/MainViewModel.cs"             # ScaleConnectionType/ScaleComPort/ScaleBaudRate + IsScaleTcp/IsScaleCom, BuildScaleConfig() truyền qua
    - "WpfSample/MainWindow.xaml"                         # ComboBox "Kết nối" (Tcp/Com) + panel COM/Baud ẩn/hiện qua BooleanToVisibilityConverter
    - "ScanAndScale.Core/ScanAndScale.Core.csproj"        # (không liên quan COM) Fix NU1012 khi Pack — đổi TargetFrameworks bare net6.0-windows/net8.0-windows → net6.0-windows7.0/net8.0-windows7.0 (khớp pattern ScanAndScale.Driver/TestDriver/WinFormsApp1 đã dùng sẵn), phải xoá obj/ + Restore lại sau khi đổi TFM
    - "ScaleSimulator/"                                   # (project mới, độc lập) Mô phỏng cân qua TCP server + COM writer, build sẵn 5 format raw data khớp đúng regex từng Scale_*/ScaleReading.cs — xem CHANGELOG bên dưới và ScaleSimulator/README.md
  blocked_by:       "Chưa có cân Vibra_HAW30 thật cắm qua COM để test end-to-end (mở port, đọc dữ liệu, auto-reconnect khi rút cáp). Code compile sạch nhưng logic SerialPort.BaseStream + StreamReader.ReadLineAsync (đặc biệt phần drain-backlog dùng BytesToRead) mới chỉ verify tĩnh, chưa chạy với cân thật. ScaleSimulator giúp test được luồng đọc/parse/reconnect NHƯNG KHÔNG thay thế hoàn toàn việc test với cân vật lý thật (không mô phỏng được đặc tính điện/nhiễu RS232 thật, hay hành vi baud/parity thật của thiết bị)."
  next_step:        "User: mở WpfSample, ComboBox 'Driver'=Scale_Vibra_HAW30 (mặc định), đổi ComboBox 'Kết nối' sang COM, nhập đúng ComPort (vd COM3, xem Device Manager) + Baud rate (thường 9600 — xem tài liệu cân), bấm 'Kết nối tất cả', xác nhận: (1) badge trạng thái lên Connected, (2) giá trị cân hiển thị đúng + real-time trong Log Scale, (3) rút cáp COM thử auto-reconnect có hoạt động không. Nếu OK → không cần sửa gì thêm ở tầng driver (đã dùng chung), chỉ cần các Scale_* khác test tương tự bằng cách đổi ComboBox 'Driver' sang model tương ứng. Trong lúc chưa có cân thật: có thể dùng ScaleSimulator (cài com0com để có cặp COM ảo, hoặc dùng nhánh TCP không cần phần cứng gì) để test trước luồng driver/UI."
  last_session:     "2026-08-20"
  open_questions:
    - "Cân có hỗ trợ báo cờ ổn định (Stable) qua RS-232C không, hay phải luôn đọc raw liên tục như hiện tại? Raw data thật thu được chưa thấy cờ ST/US — đã chuyển sang tự suy luận Stable ở phần mềm (UpdateStability), nhưng chưa xác nhận ngưỡng StableToleranceG=0.02g có phù hợp với nhiễu thực tế của cân hay không."
    - "Scale_Vibra_SJ6200 và Scale_Vibra_HAW30 có cùng bug gán Unit=raw-unit dù WeightValue đã quy đổi KG (giống bug vừa fix ở Shimadzu) — có cần sửa luôn không?"
    - "Cân Vibra_HAW30 cắm COM trực tiếp dùng Parity/DataBits/StopBits gì? Hiện ScaleDriver hard-code None/8/One (giống RfidDriver) — nếu cân thật cần khác thì phải thêm field vào ScaleConfig."
```

### 4.2 Quyết định đã chốt (Decision Log)

| ID     | Ngày       | Quyết định                              | Ai quyết | File liên quan              |
|--------|------------|-----------------------------------------|----------|-----------------------------|
| DEC-001 | YYYY-MM-DD | Dùng JWT lưu trong httpOnly cookie      | Team     | `lib/auth.ts`               |
| DEC-002 | YYYY-MM-DD | Không dùng ORM, viết raw SQL qua pg     | Dev      | `lib/db.ts`                 |
<!-- Thêm quyết định mới vào đây -->

### 4.3 Hướng dẫn Claude đọc context

Khi bắt đầu session mới, Claude PHẢI:
1. Đọc `active_context` → biết đang làm gì
2. Đọc `CHANGELOG` gần nhất → biết đã thay đổi gì
3. Đọc `Decision Log` → tránh đề xuất lại phương án đã bác bỏ
4. **Không** hỏi lại những gì đã ghi trong file này

**Prompt mẫu để bắt đầu session:**
```
Đọc CLAUDE.md và tiếp tục từ active_context. 
Task hiện tại: [mô tả]. File cần làm việc: [list file].
```

---

## 5. CHANGELOG — EDIT LOG

> Ghi lại **mọi thay đổi đáng kể** theo thứ tự ngược (mới nhất lên đầu).  
> Format: `[YYYY-MM-DD] [TYPE] [File/Module] — Mô tả`  
> Types: `FEAT` · `FIX` · `REFACTOR` · `PERF` · `TEST` · `DOCS` · `CHORE` · `BREAK`

---

### [2026-08-20] — Session: Thêm ScaleSimulator — mô phỏng cân qua TCP & RS232 để test không cần cân thật

```
[FEAT]     ScaleSimulator/ScaleSimulator.csproj                — Project WinForms mới (net8.0-windows7.0), ĐỘC LẬP
                                                                    (không ProjectReference ScanAndScale.Core/Scale_*)
[FEAT]     ScaleSimulator/ScaleFormats/ScaleFormatDefinition.cs — 5 format raw data (DIGI, IND_KG, Vibra_SJ6200,
                                                                    Vibra_HAW30, Shimadzu_TX4202L) — build khớp ĐÚNG
                                                                    regex/điều kiện parse thật trong từng
                                                                    Scale_*/ScaleReading.cs (đối chiếu trực tiếp
                                                                    source, không đoán mò)
[FEAT]     ScaleSimulator/ScaleSimEngine.cs                     — Timer nền sinh dòng raw data theo chu kỳ, cộng
                                                                    nhiễu ngẫu nhiên quanh giá trị target
[FEAT]     ScaleSimulator/Servers/TcpScaleServer.cs             — TCP server, broadcast dữ liệu tới mọi client —
                                                                    giả lập bộ chuyển đổi Serial-to-Ethernet
[FEAT]     ScaleSimulator/Servers/SerialScaleServer.cs          — Ghi dữ liệu ra 1 đầu cổng COM (Parity/DataBits/
                                                                    StopBits None/8/One — khớp ScaleDriver thật)
[FEAT]     ScaleSimulator/MainForm.cs                           — UI: chọn model/giá trị/nhiễu/cờ trạng thái, bật/tắt
                                                                    TCP server + COM writer độc lập, nút mô phỏng
                                                                    "đặt vật lên cân" (ramp → ổn định), log
[DOCS]     ScaleSimulator/README.md                             — Hướng dẫn dùng (kèm hướng dẫn com0com cho COM)
[CHORE]    ScanAndScale.sln, ScanAndScaleDriver.sln             — Đăng ký project ScaleSimulator vào cả 2 solution
[DOCS]     CLAUDE.md                                             — Cập nhật active_context + CHANGELOG
```

**Yêu cầu:** Tạo project mô phỏng thiết bị cân qua TCP và qua RS232 để test — dùng để test
`ScanAndScale.Core/Drivers/ScaleDriver.cs` (cả 2 nhánh `ConnectionType.Tcp` và `.Com`) mà
không cần cân điện tử thật, trong lúc đang chờ cân Vibra_HAW30 thật để test end-to-end
(xem `blocked_by` ở Section 4.1).

**Thiết kế:** `ScaleSimulator` là 1 app WinForms độc lập, KHÔNG reference
`ScanAndScale.Core`/`Scale_*` — lý do: `ScanAndScale.Core.csproj` bật
`GeneratePackageOnBuild=true` và kéo theo build lồng 6 project `Scale_*` mỗi lần build (xem
CHANGELOG [2026-08-20] phía dưới, mục "Lưu ý build") — nếu simulator phụ thuộc vào đó, mỗi
lần build/chạy simulator để test sẽ ăn theo toàn bộ pipeline NuGet pack/push chậm đó, phản
tác dụng với mục đích "công cụ test nhanh". Thay vào đó, `ScaleFormats/ScaleFormatDefinition.cs`
tự định nghĩa lại (bằng string literal) đúng 5 format raw data, được viết bằng cách đọc trực
tiếp regex/điều kiện `rawData.Length == N` trong từng `Scale_*/ScaleReading.cs` để đảm bảo
dòng dữ liệu simulator phát ra LUÔN được driver thật parse đúng.

`ScaleSimEngine` chạy 1 `System.Threading.Timer` nền, mỗi tick tính giá trị hiệu dụng =
target ± nhiễu ngẫu nhiên rồi build raw line, bắn sự kiện cho CẢ `TcpScaleServer` (broadcast
tới mọi client TCP đang kết nối) và `SerialScaleServer` (ghi ra cổng COM đang mở) — 2 kênh
độc lập, bật/tắt riêng, dùng CHUNG 1 nguồn dữ liệu (đúng thực tế 1 cân chỉ có 1 luồng dữ liệu).

**COM cần cặp cổng ảo:** không như TCP có sẵn loopback `127.0.0.1`, test qua COM trên 1 máy
cần 1 cặp cổng COM ảo null-modem (khuyến nghị **com0com** — driver mã nguồn mở, miễn phí) hoặc
2 cổng COM thật nối cáp null-modem — mở simulator ở 1 đầu, trỏ `ScaleConfig.ComPort` của app
đang test sang đầu còn lại. Chi tiết trong `ScaleSimulator/README.md`.

**Lưu ý phát hiện được (không phải bug do session này gây ra):** `Scale_Vibra_SJ6200/ScaleReading.cs`
gán `Stable = (flag == "S")` nhưng comment ngay trong chính file đó lại ghi "U là ổn định" —
tức code và comment mâu thuẫn nhau, khả năng là bug có sẵn. Simulator KHÔNG tự sửa (ngoài
phạm vi yêu cầu), chỉ cho phép chọn trực tiếp cả 2 giá trị U/S để user tự kiểm chứng
`ScaleDriver.IsStable` thực tế trả về gì — xem ghi chú trong `ScaleFormatDefinition.cs` và
`README.md`.

**Build:** `ScaleSimulator.csproj` build độc lập qua MSBuild — sạch (exit 0), chỉ cần thêm
`PackageReference System.IO.Ports` (không tự có sẵn trong Windows Desktop shared framework,
đúng version 9.0.2 đã dùng ở `ScanAndScale.Core.csproj`). Build lại toàn bộ `ScanAndScale.sln`
(gồm cả `ScaleSimulator` mới) — sạch (exit 0). Restore lần đầu mất ~2.3 phút do NuGet cố thử
server nội bộ không reachable (`10.40.10.4:5860` — xem CHANGELOG [2026-08-20] phía dưới) trước
khi rơi về `nuget.org`; các lần build sau nhanh bình thường nhờ cache.

**Chưa test:** Chưa tự chạy thử simulator kết nối thật với `WpfSample` (cả nhánh TCP lẫn COM
qua com0com) trong session này — chỉ verify build sạch. Cần user tự chạy và xác nhận.

**CẬP NHẬT — user chạy Debug trong Visual Studio, crash ngay khi mở app; đã fix + tự verify
bằng cách chạy thật (không chỉ build):**

1. **Crash `ArgumentOutOfRangeException` trên `_cboModel.SelectedIndex = 0`
   (`MainForm.cs` constructor).** Root cause: `ComboBox.DataSource` không populate `Items`
   đồng bộ — chỉ điền dữ liệu khi control có `Handle` (thường chỉ có khi Form được realize,
   vd lúc `Show`/`Load`). Set `SelectedIndex=0` ngay trong constructor (lúc `Items.Count`
   vẫn = 0) ném exception. **Fix:** dời việc chọn model ban đầu (và `_engine.Start()`) sang
   sự kiện `Load` của Form thay vì gọi trong constructor.
2. **Layout vỡ (phát hiện qua ảnh chụp màn hình sau khi fix crash #1):** 2 panel TCP/COM
   trong "2 & 3. Kênh phát dữ liệu" đè chồng lên nhau ở góc (0,0) — do các control con
   (`tcpHint`/`tcpLayout`, `comHint`/`comLayout`) được thêm vào `GroupBox` mà KHÔNG set
   `Dock`, mặc định `Location=(0,0)`. Set `Dock=DockStyle.Top` cho cả 4 control thì lộ ra
   **bug thứ 2**: `tcpGrp`/`comGrp` dùng `AutoSize=true` + có con `Dock=Top` bên trong +
   đồng thời ép cứng `Width=460` — tạo vòng phụ thuộc kích thước khiến GroupBox co gần về 0,
   `Text` tự xuống dòng từng ký tự (thấy rõ trong ảnh chụp). **Fix:** bỏ `AutoSize=true` ở
   `tcpGrp`/`comGrp`, dùng `Size` cố định (`460x190` và `460x235`) thay vì để tự tính.
3. **Tự verify bằng cách CHẠY THẬT** (không chỉ build): build → chạy `ScaleSimulator.exe` →
   chụp màn hình xác nhận layout đúng → dùng UI Automation (`System.Windows.Automation`) bấm
   nút "Bật TCP server" → xác nhận cổng 23 thật sự listen (`Test-NetConnection`) → mở
   `TcpClient` thật kết nối tới `127.0.0.1:23`, đọc được đúng `"0000.00@="` (9 ký tự, khớp
   `Scale_DIGI`) → đổi ComboBox Model sang `Scale_Vibra_HAW30` qua UI Automation → đọc lại,
   nhận đúng `"ST,NT,+  0.000  g"` (khớp regex `Scale_Vibra_HAW30`) → log panel hiện đúng
   client connect/disconnect kèm số lượng. Tất cả PASS.

Bài học: build sạch (exit 0) KHÔNG đảm bảo app WinForms chạy được — lỗi `SelectedIndex`/layout
này không phải lỗi biên dịch nên MSBuild không bắt được. Từ nay với `ScaleSimulator`, sau mỗi
thay đổi UI đáng kể nên tự chạy + chụp màn hình xác nhận, không chỉ dừng ở build.

---

### [2026-08-20] — Session: Thêm kết nối COM trực tiếp cho Scale Driver (song song TCP/IP hiện có)

```
[FEAT]     ScanAndScale.Core/Models/ScaleConfig.cs           — Enum ScaleConnectionType (Tcp/Com);
                                                                 ConnectionType, ComPort, BaudRate (default Tcp — không đổi hành vi cũ)
[FEAT]     ScanAndScale.Core/Drivers/ScaleDriver.cs           — Connect/Read/Reconnect/IsConnected/Disconnect đều
                                                                 dispatch theo ConnectionType; nhánh Com dùng SerialPort
                                                                 (Parity.None/8/StopBits.One, giống RfidDriver) +
                                                                 SerialPort.BaseStream/StreamReader (leaveOpen:true),
                                                                 cùng chiến lược drain-backlog "luôn giữ dòng mới nhất"
                                                                 như nhánh Tcp (BytesToRead thay Socket.Available)
[FEAT]     WpfSample/ViewModels/MainViewModel.cs              — ScaleConnectionType/ScaleComPort/ScaleBaudRate,
                                                                 IsScaleTcp/IsScaleCom, BuildScaleConfig() truyền qua
[FEAT]     WpfSample/MainWindow.xaml                          — ComboBox "Kết nối" (TCP/IP | COM) + panel COM/Baud
                                                                 ẩn/hiện qua BooleanToVisibilityConverter dựng sẵn
[FIX]      ScanAndScale.Core/ScanAndScale.Core.csproj         — (không liên quan COM) Fix NU1012 khi Pack — xem chi tiết dưới
```

**Yêu cầu:** Cân hiện tại xuất RS232 qua bộ chuyển đổi Serial-to-Ethernet, driver đọc qua TCP (đã chạy ổn). Cần
thêm khả năng cắm cân TRỰC TIẾP qua cổng COM (không qua bộ chuyển đổi), chọn được TCP hay COM lúc khởi tạo. Thử
trước với Scale_Vibra_HAW30, sau khi test OK thật mới triển khai hàng loạt cho các driver cân khác.

**Thiết kế:** `ScaleDriver.cs` vốn đã là driver DÙNG CHUNG cho MỌI model cân — model cân (Scale_DIGI, Scale_Vibra_HAW30,
Scale_Shimadzu_TX4202L, ...) chỉ là DLL parser nạp qua reflection (`GetWeight()`), hoàn toàn không biết gì về TCP
hay COM. Vì vậy chỉ cần thêm `ScaleConnectionType` vào `ScaleConfig` và dispatch theo nó trong `ScaleDriver` LÀ ĐỦ
để áp dụng cho TẤT CẢ driver cân cùng lúc — không cần đụng vào từng `Scale_*.csproj`. Việc "test Vibra_HAW30 trước"
vì vậy là bước xác nhận bằng phần cứng thật (chỉ đổi ComboBox "Driver" trên UI sang model khác là dùng được ngay
cho các cân khác), không phải một bước code riêng.

Nhánh COM (`ConnectSerialAsync`/`ReadScaleDataFromSerialAsync`/`TryReconnectSerialAsync`) được viết song song 1-1
với nhánh TCP đã có sẵn — dùng `SerialPort.BaseStream` + `StreamReader` (thay `NetworkStream`), `SerialPort.IsOpen`
(thay `TcpClient.Connected`), `SerialPort.BytesToRead` (thay `Socket.Available`) — giữ nguyên toàn bộ các fix quan
trọng đã rút ra từ nhánh TCP trước đó (giữ persistent port suốt 1 kết nối thay vì mở/đóng mỗi tick, luôn await trọn
vẹn không bao giờ bỏ dở read, luôn giữ dòng MỚI NHẤT khi có backlog). Parity/DataBits/StopBits cố định None/8/One
(giống pattern SerialPort có sẵn trong `RfidDriver.OpenSerialPort`) — chưa expose ra `ScaleConfig` vì chưa rõ cân
Vibra_HAW30 thật cần cấu hình khác không (xem `open_questions`).

`ScaleConfig.ConnectionType` mặc định = `Tcp` → mọi cấu hình cũ (chỉ set `IP`/`Port`, không set `ConnectionType`)
tiếp tục chạy giống hệt trước, không cần sửa gì. `MainViewModel`/`MainWindow.xaml` thêm ComboBox "Kết nối" + panel
COM/Baud (ẩn/hiện qua `BooleanToVisibilityConverter` dựng sẵn của WPF) — mặc định vẫn TCP + Scale_Vibra_HAW30,
đổi ComboBox sang COM là cách test nhánh mới.

**Build (không liên quan COM) — fix kèm theo:** Trong lúc verify build, phát hiện commit ngay trước đó cùng ngày
(`2a3d35d Automate NuGet packaging...`, đã bật `GeneratePackageOnBuild=true` cho `ScanAndScale.Core.csproj`) khiến
`MSBuild ScanAndScale.sln` trả về exit code 1 do lỗi `NU1012: Some dependency group TFMs are missing a platform
version: net6.0-windows, net8.0-windows` ở bước Pack — dù DLL vẫn build ra đúng, gây hiểu nhầm "build fail". Root
cause: NuGet pack đòi TFM có hậu tố `-windows` phải ghi rõ platform version ngay trong moniker (vd.
`net6.0-windows7.0`) để đưa vào nuspec dependency group; `TargetFrameworks` khai bare `net6.0-windows`/`net8.0-windows`
(không số phiên bản) nên thiếu, dù MSBuild build bình thường vẫn ngầm hiểu = 7.0. Fix: đổi
`TargetFrameworks` sang `net472;net6.0-windows7.0;net8.0-windows7.0` — ĐÚNG pattern đã dùng sẵn ở
`ScanAndScale.Driver`/`TestDriver`/`WinFormsApp1` trong cùng solution (an toàn cho consumer vì MSBuild/NuGet coi
TFM bare và có hậu tố `7.0` là CÙNG 1 TFM chuẩn hoá). Đổi `TargetFrameworks` làm `project.assets.json` cũ không còn
khớp (lỗi `NETSDK1005`) nên phải xoá `obj/` của `ScanAndScale.Core`/`WpfSample` rồi `MSBuild -t:Restore` lại trước
khi build. Verify: `MSBuild ScanAndScale.sln -m:1 -p:Configuration=Debug -p:PushToNuget=false` → **exit code 0**,
chỉ còn warning vô hại (NuGet server nội bộ `10.40.10.4:5860` không reachable — không chặn build).

**Lưu ý build:** `PushToNuget=true` (mặc định) khiến MỖI project (kể cả 6 `Scale_*` build lồng trong
`BuildAndCopyScaleDlls`) đều thử `dotnet nuget push` tới server nội bộ hiện KHÔNG reachable từ máy dev này — mỗi
lần build tốn thời gian chờ timeout mạng nhiều lần (đã khiến 1 lần build bị user huỷ vì tưởng treo). Khi cần build
nhanh để verify (không cần publish gói), truyền `-p:PushToNuget=false`.

**Chưa test:** Chưa có cân Vibra_HAW30 thật cắm qua COM để xác nhận end-to-end (mở port, đọc đúng giá trị real-time,
auto-reconnect khi rút cáp).

---

### [2026-08-14] — Session: Badge "Stable" không lên True dù cân đã đứng yên (chưa xác định được root cause — thêm diagnostic)

```
[FEAT]     Scale_Shimadzu_TX4202L/ScaleReading.cs          — Thêm LastStabilityInfo (static string debug)
[FEAT]     ScanAndScale.Core/Drivers/ScaleDriver.cs         — Đọc LastStabilityInfo qua reflection best-effort
                                                                (StabilityDebugInfo), không phá signature GetWeight
[FEAT]     WpfSample/ViewModels/MainViewModel.cs            — Log Scale in kèm StabilityDebugInfo
[DOCS]     CLAUDE.md                                        — Cập nhật active_context
```

**Triệu chứng:** User xác nhận giá trị cân hiển thị ĐÚNG và real-time (fix session trước đã ổn), nhưng badge
"False (Stable)" không bao giờ chuyển sang True dù panel "Log Scale" cho thấy cùng một giá trị (vd "69.13g")
lặp lại liên tục rất nhiều dòng, mỗi ~400ms.

**Phân tích:** Đọc lại toàn bộ logic `UpdateStability()` (StableWindowSize=5, StableToleranceG=0.02g) — về mặt
thuật toán, nếu 5 lần đọc liên tiếp có cùng giá trị (range=0) thì PHẢI trả về Stable=true trong vòng ~2 giây
(5 × 400ms). Đã verify build KHÔNG bị stale: rebuild sạch qua MSBuild (exit 0), grep trực tiếp trong
`WpfSample/bin/Debug/net8.0-windows/ScanAndScale.Core.dll` xác nhận có chứa `UpdateStability`/`LastStabilityInfo`
(tức DLL đang chạy đã có code mới). Không tìm thấy bug rõ ràng nào trong code hiện tại qua đọc tĩnh — có thể do
(1) nhiễu raw thực tế giữa các lần đọc lớn hơn 0.02g dù giá trị hiển thị làm tròn giống hệt nhau (2 chữ số thập
phân đúng bằng độ phân giải d=0.01g của cân nên khó xảy ra, nhưng chưa loại trừ được 100% nếu tolerance quá sát),
hoặc (2) WpfSample.exe lúc user test là tiến trình CŨ chưa được đóng/mở lại sau lần build gần nhất.

**Fix tạm thời (chưa phải fix cuối — CHỈ thêm khả năng quan sát):** Thêm field debug `LastStabilityInfo` (static
string) vào `Scale_Shimadzu_TX4202L/ScaleReading.cs`, cập nhật mỗi lần `UpdateStability()` chạy với nội dung
`"win=x/5 range=...g (tol=0.02g) -> ON DINH/CHUA on dinh"`. `ScaleDriver.cs` đọc field này qua reflection
`GetField("LastStabilityInfo", Public|Static)` — **tuỳ chọn, best-effort**: nếu model cân khác (DIGI, IND_KG,
Vibra...) không có field này, `GetField` trả `null` và bị bỏ qua an toàn, không phá vỡ các driver khác — không
đổi signature cố định của `GetWeight()` mà `ScaleDriver` dùng reflection để gọi cho MỌI model. Kết quả được in
thêm vào dòng log "Log Scale" trong `MainViewModel.cs`, vd:
`-> Stable:False [win=5/5 range=0.030g (tol=0.02g) -> CHUA on dinh] -> Tare:False`
để user xem được CON SỐ THẬT thay vì đoán mò, theo đúng cách đã áp dụng thành công ở các bug trước (đứt gãy giữa
số, sai đơn vị...).

Rebuild `ScanAndScale.sln` qua MSBuild (Configuration=Debug, `-m:1` để tránh lỗi khoá file tạm thời của VBCSCompiler
khi build song song) — **sạch (exit 0)**. Chưa test lại với cân thật.

---

### [2026-08-14] — Session: Fix cân báo sai giá trị (hiển thị trễ so với cân thật)

```
[FIX]      ScanAndScale.Core/Drivers/ScaleDriver.cs       — Drain backlog TCP, luôn parse dòng MỚI NHẤT
[FIX]      Scale_Shimadzu_TX4202L/ScaleReading.cs          — Unit luôn = "KG" (trước đó ghi nhầm "G")
[FIX]      WpfSample/Converters/DriverStatusConverter.cs   — Thêm TareToVisibilityConverter (đúng chiều)
[FIX]      WpfSample/MainWindow.xaml                       — Badge TARE dùng converter đúng chiều
[FEAT]     WpfSample/ViewModels/MainViewModel.cs            — Log kèm RawData trong Log Scale để debug
```

**Triệu chứng:** User kết nối cân Shimadzu TX4202L thật (IP 192.168.80.237), cân vật lý hiện `111.40`
nhưng app hiện `0.013 G` kèm badge `TARE` luôn sáng dù không tare.

**Root cause (đã xác nhận bằng raw data thật từ Hercules — `113.40g   113.40g   113.39g ...`,
đúng định dạng parser mong đợi, nên KHÔNG phải lỗi regex/format):**
1. **Chính:** Shimadzu ở chế độ auto-print gửi dữ liệu liên tục NHANH HƠN chu kỳ đọc
   `TimeScanMs` (400ms). `ReadScaleDataAsync()` cũ chỉ đọc đúng 1 dòng/tick → dữ liệu tồn đọng
   trong buffer TCP và NGÀY CÀNG LỆCH XA thời điểm hiện tại (driver không bao giờ đuổi kịp),
   nên hiển thị mãi giá trị cũ (gần 0, từ lúc chưa đặt vật lên cân).
2. **Phụ:** `Scale_Shimadzu_TX4202L/ScaleReading.cs` gán `Unit = unit.ToUpper()` (đơn vị RAW,
   "G") dù `WeightValue` đã quy đổi sang KG ở bước trước đó → hiển thị sai nhãn (số là KG
   nhưng ghi "G"). Bug này copy từ `Scale_Vibra_SJ6200`/`Scale_Vibra_HAW30` (cùng pattern lỗi,
   chưa sửa ở 2 file đó vì ngoài phạm vi yêu cầu lần này).
3. **Phụ:** `MainWindow.xaml` bind badge "TARE" với `InverseBoolToVisibilityConverter`
   (dùng nhầm converter đảo ngược) → badge hiện khi **KHÔNG** tare, ẩn khi **đang** tare.
   Bug có sẵn từ trước, không riêng driver Shimadzu.

**Fix:**
1. `ScaleDriver.ReadScaleDataAsync()`: sau khi đọc dòng đầu tiên, drain hết các dòng ĐÃ CÓ SẴN
   trong buffer (timeout 20ms/lần — không đợi dữ liệu mới), chỉ giữ dòng CUỐI CÙNG để parse.
   Áp dụng chung cho MỌI driver cân (không riêng Shimadzu) — an toàn tuyệt đối vì luôn ưu tiên
   dữ liệu mới nhất, không có tác dụng phụ với cân gửi chậm hơn `TimeScanMs`.
2. `Scale_Shimadzu_TX4202L`: `Unit = "KG"` cố định.
3. Thêm `TareToVisibilityConverter` (true→Visible, false→Collapsed) đúng chiều, thay
   `InvBoolToVis` trên badge TARE.
4. `MainViewModel.OnScaleDataChanged`: log `raw='...' → {value} {unit}` vào panel "Log Scale"
   mỗi khi giá trị đổi — để đối chiếu ngay trên UI nếu còn sai lệch, không cần hỏi lại raw data.

Đã xoá `obj\` liên quan, restore + build lại `ScanAndScale.sln` qua MSBuild — **build sạch (exit 0)**.
Chưa test lại với cân vật lý thật sau fix (cần user tự chạy lại và xác nhận giá trị bắt kịp real-time).

**CẬP NHẬT — fix trên CHƯA đủ, tìm ra root cause thật sự:**
User test lại, log "Log Scale" cho thấy giá trị bị ĐỨT GÃY GIỮA SỐ — vd raw thật là
`   113.44g` nhưng driver nhận được `.44g` (mất phần nguyên "113"), bị hiểu nhầm thành
44g/1000 = 0.044 kg. Raw data xen kẽ: thỉnh thoảng đọc đủ ("113.44g"), phần lớn chỉ còn
phần thập phân (".44g", ".46g"...).

**Root cause thật:** `ReadScaleDataAsync()` tạo `NetworkStream`/`StreamReader` MỚI mỗi tick
(comment đầu file nói "giữ persistent" nhưng code thực tế lại tạo mới — 2 comment mâu thuẫn
nhau, khả năng là regression từ lần "fixing bug reconnect scale" trước đây). Bản drain-backlog
đầu tiên dùng `Task.WhenAny(read, Task.Delay(20))` rồi **bỏ dở** read nếu timeout thắng — nhưng
read bị bỏ dở đó VẪN chạy ngầm, có thể hoàn tất SAU KHI tick hiện tại đã dispose stream/reader,
đúng lúc tick KẾ TIẾP tạo `StreamReader` MỚI trên CÙNG 1 socket → **2 lần đọc chạy song song**,
mỗi bên "cướp" một phần byte của cùng 1 dòng dữ liệu từ kernel socket buffer → đứt gãy giữa số.

**Fix (2 lớp):**
1. `ScaleDriver.ReadScaleDataAsync()`: bỏ hẳn cơ chế timeout-race-rồi-bỏ-dở. Thay bằng kiểm
   tra `Socket.Available > 0` (đồng bộ, không I/O) trước khi đọc thêm, và LUÔN `await` trọn
   vẹn mỗi lần đọc (không bao giờ bỏ dở nữa) — loại bỏ hoàn toàn khả năng 2 read chạy song song.
2. `Scale_Shimadzu_TX4202L/ScaleReading.cs`: thêm lookbehind `(?:(?<=^)|(?<=\s))` vào đầu
   pattern — số PHẢI bắt đầu ngay sau khoảng trắng hoặc đầu chuỗi, chặn các fragment kiểu
   ".44g" bị hiểu nhầm thành giá trị hoàn chỉnh (lớp phòng vệ thêm, phòng trường hợp đứt gãy
   khác chưa lường hết).

Đã verify regex bằng PowerShell `[regex]::Matches` với cả case đầy đủ lẫn fragment — fragment
bị reject đúng như kỳ vọng. Rebuild `ScanAndScale.sln` — sạch (exit 0). **Vẫn chưa test lại
với cân thật sau fix lần 2 này** — cần user xác nhận.

**CẬP NHẬT — theo yêu cầu user:** bỏ hẳn phần ép quy đổi sang KG trong
`Scale_Shimadzu_TX4202L/ScaleReading.cs` — giờ `WeightValue`/`Unit` giữ NGUYÊN giá trị và đơn vị
raw đọc được từ cân (vd. cân gửi gram thì hiển thị đúng gram, không tự nhân 0.001 để quy về KG
như các driver Vibra_SJ6200/Vibra_HAW30 khác). `MainViewModel`/`ScaleDisplayText` vốn đã tổng
quát (chỉ ghép `{ScaleValue} {ScaleUnit}` bất kể đơn vị gì) nên không cần sửa gì ở WPF. Build lại
— sạch (exit 0).

**CẬP NHẬT — badge "False (Stable)" không lên dù màn hình LCD cân đã báo ổn định (mũi tên
sáng):** Root cause: format continuous-output hiện tại của máy KHÔNG gửi cờ ST/US qua RS232 —
mũi tên ổn định trên LCD là do CHÍNH CÂN tự tính nội bộ (bộ lọc riêng), không phát ra ngoài
serial. Raw data thật vẫn dao động nhẹ (66.26-66.28g) ngay cả khi LCD đã khoá 66.29g ổn định.
`Stable` bị hard-code `false` từ đầu vì "không có cờ để đọc".

**Fix:** `Scale_Shimadzu_TX4202L/ScaleReading.cs` tự suy luận Stable ở phía phần mềm — theo dõi
`StableWindowSize=5` giá trị đọc gần nhất trong `Queue<double>` static, coi là ổn định nếu biên
độ (max-min) của cửa sổ đó ≤ `StableToleranceG=0.02` (~2 lần độ phân giải d=0.01g in trên máy).
Lưu ý: state này static (giống `oldData`) — chỉ đúng khi có DUY NHẤT 1 cân Shimadzu TX4202L
kết nối cùng lúc trong 1 process (giới hạn có sẵn từ trước, không phải vấn đề mới).
Build lại — sạch (exit 0). Chưa test lại với cân thật (cần user xác nhận badge Stable lên đúng
khi giá trị đã ổn định thực tế trên LCD).

---

### [2026-08-14] — Session: UI chọn driver/IP cân + fix build NETSDK1005

```
[FEAT]     WpfSample/MainWindow.xaml                — Thêm ComboBox "Driver" + TextBox "IP"/"Port" ở nhóm Cân
[FEAT]     WpfSample/ViewModels/MainViewModel.cs     — AvailableScaleModels, SelectedScaleModel, ScaleIp, ScalePort,
                                                         CanEditScaleConfig, BuildScaleConfig()
[FIX]      ScanAndScale.Core/ScanAndScale.Core.csproj — Thêm SetTargetFramework=netstandard2.0 cho 6 ProjectReference
                                                         Scale_* → sửa lỗi NETSDK1005 khi build/restore
[DOCS]     CLAUDE.md                                 — Cập nhật CHANGELOG
```

**Chi tiết:**
- GroupBox "⚖️ Cân điện tử" trong WpfSample giờ cho chọn driver (ComboBox đổ từ `ScaleModelNames.All` — driver mới thêm sau này tự xuất hiện) và nhập IP/Port tay, thay vì hard-code `Scale_DIGI` / `192.168.80.237:23` trong XAML. Cả 3 control tự khóa (`CanEditScaleConfig = !IsInitialized`) khi đang kết nối, phải "■ Ngắt kết nối" mới sửa lại được. Giá trị mặc định giữ nguyên `Scale_Vibra_HAW30` / `192.168.80.237` / `23` như cấu hình cũ.
- **Root cause của lỗi `NETSDK1005`** (chặn cả `dotnet build`/`MSBuild` CLI lẫn WPF Designer báo "Some assembly references are missing"): `ScanAndScale.Core.csproj` multi-target (`net472;net6.0-windows;net8.0-windows`) reference 6 project `Scale_*` (chỉ target `netstandard2.0`) với `ReferenceOutputAssembly=false` + `SkipGetTargetFrameworkProperties=true`. Global property `TargetFramework` của Core (vd. `net8.0-windows`) vẫn bị flow xuống khi NuGet dựng restore graph cho các `Scale_*`, khiến NuGet đòi hỏi `Scale_DIGI\obj\project.assets.json` phải có target `net8.0-windows` — trong khi project đó chỉ restore cho `netstandard2.0`.
- **Fix:** thêm metadata `<SetTargetFramework>TargetFramework=netstandard2.0</SetTargetFramework>` vào cả 6 `ProjectReference` — ép P2P reference luôn dùng đúng TFM cố định của project con, không kế thừa TargetFramework đang build của Core. Đã xoá `obj\` của 7 project liên quan rồi restore + build lại `ScanAndScale.sln` qua MSBuild — **build sạch (exit 0)** cho cả 3 TFM của Core và `WpfSample.dll`.
- **Lưu ý:** `dotnet build`/`dotnet restore` (CLI) qua SDK 10.0.201 vẫn có thể lỗi tương tự trên các máy khác nếu global SDK version resolve về bản mới — nếu gặp lại, build qua Visual Studio/MSBuild.exe (đường dẫn: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe`) đã xác nhận hoạt động ổn định.

---

### [2026-08-13] — Session: Thêm driver cân Shimadzu TX4202L

```
[FEAT]     Scale_Shimadzu_TX4202L/Scale_Shimadzu_TX4202L.csproj  — Tạo project parser mới (netstandard2.0)
[FEAT]     Scale_Shimadzu_TX4202L/ScaleReading.cs                — Parser GetWeight() cho format "48.08g"
[FEAT]     ScanAndScale.Core/Models/ScaleConfig.cs               — Thêm ScaleModelNames.Shimadzu_TX4202L
[CHORE]    ScanAndScale.Core/ScanAndScale.Core.csproj            — ProjectReference + BuildAndCopyScaleDlls/EmbedScaleDlls
[CHORE]    ScanAndScale.Driver/ScanAndScale.Driver.csproj        — ProjectReference tới Scale_Shimadzu_TX4202L
[CHORE]    ScanAndScale.sln, ScanAndScaleDriver.sln              — Đăng ký project mới vào 2 solution
[DOCS]     CLAUDE.md                                             — Cập nhật active_context
```

**Chi tiết:**
- Cân Shimadzu TX4202L (dòng UniBloc, max 4200g) kết nối qua bộ chuyển đổi Serial-to-Ethernet, `ScaleConfig.Port = 23` giống các cân khác trong dự án.
- Raw data mẫu bắt được từ Hercules TCP Client (IP `192.168.80.237:23`): nhiều giá trị dạng `"48.08g"` (số thập phân + đơn vị `g`, không có dấu `+`, không có cờ ổn định ST/US) có thể dính liền nhau trong 1 lần đọc, cách nhau bởi khoảng trắng hoặc dấu `-`.
- `ScaleReading.GetWeight()` dùng `Regex.Matches` quét toàn bộ raw data, lấy **match cuối cùng** (giá trị mới nhất) — cùng chiến lược với `Scale_Vibra_SJ6200`. `Stable` luôn trả `false` vì format này không có cờ báo ổn định (giống `Scale_IND_KG`).
- Đã build riêng `Scale_Shimadzu_TX4202L.csproj` thành công (0 lỗi) và verify regex khớp đúng với mẫu dữ liệu thật qua PowerShell `[regex]::Matches`.
- Build toàn bộ `ScanAndScale.Core.csproj` qua `dotnet build` CLI bị lỗi `NETSDK1005` cho **tất cả** project `Scale_*` (kể cả các project cũ đã có từ trước) — xác nhận đây là vấn đề môi trường/SDK CLI có sẵn từ trước (không phải do thay đổi lần này), khả năng cao do project multi-target (`net472;net6.0-windows;net8.0-windows`) cần build bằng Visual Studio/MSBuild thay vì `dotnet build` CLI thuần với SDK 10.0.201 hiện tại.

---

### [YYYY-MM-DD] — Session N-1

```
[CHORE]    package.json    — Upgrade Zod từ 3.21 → 3.23
[REFACTOR] lib/api.ts      — Tách error handler thành hàm riêng handleApiError()
```

<!-- Thêm session mới lên ĐẦU, trên dòng này -->

---

## 6. UNIT TEST GUIDELINES

### 6.1 Cấu trúc test file

```typescript
/**
 * @file        tests/unit/userService.test.ts
 * @description Unit tests cho src/services/userService.ts
 * @covers      getUser, createUser, updateUser, deleteUser
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getUser } from '@/services/userService';
import { mockUser } from '../fixtures/user.fixture';

// ─── Mock external dependencies ───────────────────────────────────────────
vi.mock('@/lib/api', () => ({ fetchJson: vi.fn() }));

describe('userService', () => {

  // ─── getUser ────────────────────────────────────────────────────────────
  describe('getUser()', () => {

    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should return user when valid ID provided', async () => {
      // ARRANGE
      mockFetchJson.mockResolvedValue(mockUser);

      // ACT
      const result = await getUser('valid-id-123');

      // ASSERT
      expect(result).toEqual(mockUser);
      expect(mockFetchJson).toHaveBeenCalledWith('/users/valid-id-123');
    });

    it('should throw NotFoundError when user does not exist', async () => {
      // ARRANGE
      mockFetchJson.mockRejectedValue({ status: 404 });

      // ACT & ASSERT
      await expect(getUser('ghost-id')).rejects.toThrow('NotFoundError');
    });

    it('should return cached result on second call within staleTime', async () => { ... });

  });

});
```

### 6.2 Test Coverage Requirements

| Layer       | Minimum Coverage | Ghi chú                            |
|-------------|------------------|------------------------------------|
| services/   | 90%              | Mọi branch phải có test            |
| lib/        | 85%              | Đặc biệt error paths               |
| hooks/      | 80%              | Test với React Testing Library     |
| utils/      | 95%              | Pure functions — dễ test nhất      |
| components/ | 70%              | Tập trung vào interaction, không style |

### 6.3 Test Naming Convention

```
it('should <expected_behavior> when <condition>')
it('should throw <ErrorType> when <invalid_input>')
it('should NOT <behavior> when <constraint>')
```

### 6.4 Test Fixtures & Factories

```typescript
// tests/fixtures/user.fixture.ts
export const mockUser: User = {
  id:    'test-uuid-001',
  name:  'Test User',
  email: 'test@example.com',
  role:  'viewer',
};

// Factory cho biến thể
export const createMockUser = (overrides: Partial<User> = {}): User => ({
  ...mockUser,
  ...overrides,
});
```

### 6.5 Lệnh chạy test

```bash
# Chạy tất cả
pnpm test

# Watch mode (dev)
pnpm test:watch

# Coverage report
pnpm test:coverage

# Chỉ chạy 1 file
pnpm test src/services/userService
```

---

## 7. PERFORMANCE OPTIMIZATION RULES

### 7.1 Nguyên tắc chung

```
RULE-PERF-01: Đo trước khi tối ưu — dùng profiler, không đoán mò
RULE-PERF-02: Ghi PERF comment + ID trước khi thay đổi liên quan đến performance
RULE-PERF-03: Mỗi tối ưu phải có benchmark trước/sau trong CHANGELOG
RULE-PERF-04: Không dùng premature optimization gây giảm readability
```

### 7.2 Frontend Performance Checklist

```markdown
- [ ] Lazy load routes và heavy components (React.lazy / dynamic import)
- [ ] Memo hóa đúng chỗ: React.memo, useMemo, useCallback
      → CHỈ khi profiler xác nhận re-render thừa
- [ ] Image: dùng next/image hoặc lazy loading + srcset
- [ ] Bundle: kiểm tra với `pnpm build --analyze`
- [ ] Fonts: font-display: swap, preload critical fonts
- [ ] API calls: SWR / React Query staleTime hợp lý
- [ ] Long lists: virtualize với react-virtual nếu > 200 items
```

### 7.3 Backend / Node Performance Checklist

```markdown
- [ ] Database queries: có index trên cột WHERE / JOIN / ORDER BY
- [ ] N+1 queries: dùng DataLoader hoặc JOIN thay vì loop
- [ ] Caching: Redis cho hot data, TTL rõ ràng
- [ ] Pagination: KHÔNG dùng OFFSET lớn — dùng cursor-based
- [ ] Streams: dùng stream cho file lớn, không load vào RAM
- [ ] Connection pool: config đúng pool size theo load
```

### 7.4 Performance Budget

| Metric              | Target       | Critical Threshold |
|---------------------|--------------|--------------------|
| LCP                 | < 2.5s       | > 4s → reject PR   |
| FID / INP           | < 100ms      | > 300ms → reject   |
| CLS                 | < 0.1        | > 0.25 → reject    |
| JS Bundle (initial) | < 200KB gz   | > 400KB → review   |
| API p95 latency     | < 300ms      | > 1s → alert       |

---

## 8. HOW TO USE THIS FILE

### 8.1 Dành cho Claude (AI Assistant)

```
Khi đọc file này, Claude phải:

1. LUÔN đọc toàn bộ file trước khi viết bất kỳ dòng code nào
2. TUÂN THỦ naming convention, comment format đã định nghĩa
3. CẬP NHẬT active_context sau mỗi session
4. THÊM entry vào CHANGELOG mỗi khi sửa/thêm/xoá code đáng kể
5. THAM CHIẾU Decision Log trước khi đề xuất kiến trúc/công nghệ
6. VIẾT test cho mọi function mới theo Section 6
7. KIỂM TRA Performance Checklist khi code liên quan đến render/query
8. KHÔNG lặp lại câu hỏi đã có câu trả lời trong file này
```

### 8.2 Dành cho Developer

```bash
# Mỗi khi bắt đầu ngày làm việc
# 1. Pull code mới nhất
git pull origin main

# 2. Cập nhật active_context trong CLAUDE.md nếu task thay đổi
# 3. Chạy test để đảm bảo baseline xanh
pnpm test

# Trước khi commit
# 1. Thêm CHANGELOG entry
# 2. Chạy lint + test
pnpm lint && pnpm test

# Khi tạo PR
# 1. Đảm bảo CLAUDE.md được cập nhật
# 2. Coverage không giảm so với main
```

### 8.3 Template prompt để dùng với Claude Code / Cowork

```
# Bắt đầu task mới:
"Đọc CLAUDE.md. Task: [mô tả task]. 
Các file liên quan: [list files].
Sau khi xong, cập nhật active_context và CHANGELOG."

# Debug / fix:
"Đọc CLAUDE.md section 4 (context) và file [X].
Bug: [mô tả]. Expected: [hành vi đúng].
Ghi FIX vào CHANGELOG sau khi sửa xong."

# Code review:
"Đọc CLAUDE.md coding standards.
Review file [X] theo đúng conventions đã định nghĩa.
Liệt kê vi phạm theo format: [Line] [Rule] [Gợi ý sửa]."

# Viết test:
"Đọc CLAUDE.md section 6. Viết unit test cho [function/file].
Đảm bảo cover: happy path, error cases, edge cases."
```

### 8.4 Maintenance

| Việc cần làm                        | Tần suất      | Người chịu trách nhiệm |
|-------------------------------------|---------------|------------------------|
| Cập nhật `active_context`           | Mỗi session   | Dev đang làm việc      |
| Thêm entry `CHANGELOG`              | Mỗi commit    | Dev đang làm việc      |
| Review và dọn CHANGELOG cũ         | Mỗi sprint    | Tech Lead              |
| Cập nhật ADR khi có quyết định mới  | Khi phát sinh | Người quyết định       |
| Review Performance Budget           | Mỗi release   | Tech Lead              |
| Audit test coverage                 | Mỗi sprint    | QA / Dev               |

---

> **Lưu ý:** File này là nguồn sự thật duy nhất (*single source of truth*) cho AI assistant làm việc với project.  
> Khi có mâu thuẫn giữa code và CLAUDE.md → **ưu tiên CLAUDE.md**, sau đó sửa code cho nhất quán.

---
*CLAUDE.md · Generated by Claude Sonnet 4.6 · Phiên bản template: 1.0.0*
