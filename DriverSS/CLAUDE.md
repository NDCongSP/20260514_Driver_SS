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
  current_task:     "Đã thêm driver cân Shimadzu TX4202L + UI chọn driver/IP trong WpfSample + fix build NETSDK1005"
  related_files:
    - "Scale_Shimadzu_TX4202L/Scale_Shimadzu_TX4202L.csproj"
    - "Scale_Shimadzu_TX4202L/ScaleReading.cs"
    - "ScanAndScale.Core/Models/ScaleConfig.cs"          # ScaleModelNames.Shimadzu_TX4202L
    - "ScanAndScale.Core/ScanAndScale.Core.csproj"        # ProjectReference + SetTargetFramework fix
    - "ScanAndScale.Driver/ScanAndScale.Driver.csproj"    # ProjectReference
    - "ScanAndScale.sln / ScanAndScaleDriver.sln"         # đăng ký project mới
    - "WpfSample/MainWindow.xaml"                         # ComboBox Driver + TextBox IP/Port
    - "WpfSample/ViewModels/MainViewModel.cs"             # BuildScaleConfig(), CanEditScaleConfig
  blocked_by:       "Đã fix root cause đọc đứt gãy giữa số (2 read chạy song song trên cùng socket khi backlog-drain bỏ dở 1 read) — CHƯA test lại lần 2 với cân vật lý thật"
  next_step:        "User chạy lại WpfSample (VS: F5) với driver Scale_Shimadzu_TX4202L, IP 192.168.80.237, xác nhận giá trị hiển thị KHÔNG còn đứt gãy (không còn kiểu '0.044 KG' sai khi cân thật là 113.xx) và bắt kịp real-time; xem panel 'Log Scale' (log kèm RawData) để đối chiếu nếu còn sai"
  last_session:     "2026-08-14"
  open_questions:
    - "Cân có hỗ trợ báo cờ ổn định (Stable) qua RS-232C không, hay phải luôn đọc raw liên tục như hiện tại (Stable luôn = false)? Raw data thật thu được chưa thấy cờ ST/US."
    - "Scale_Vibra_SJ6200 và Scale_Vibra_HAW30 có cùng bug gán Unit=raw-unit dù WeightValue đã quy đổi KG (giống bug vừa fix ở Shimadzu) — có cần sửa luôn không?"
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
