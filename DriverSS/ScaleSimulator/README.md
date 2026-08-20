# ScaleSimulator

Mô phỏng cân điện tử — phát dữ liệu thô (raw data) **qua TCP** (giả lập bộ chuyển đổi
Serial-to-Ethernet) và **qua cổng COM** (giả lập cân cắm trực tiếp RS232/USB-to-Serial) —
để test `ScanAndScale.Core/Drivers/ScaleDriver.cs` mà không cần cân thật.

Đứng độc lập, không `ProjectReference` tới `ScanAndScale.Core`/`Scale_*` — build nhanh,
không bị kéo theo pipeline NuGet pack/push của các project đó.

## Chạy

```
dotnet run --project ScaleSimulator
```
hoặc mở `ScanAndScale.sln`/`ScanAndScaleDriver.sln` trong Visual Studio, chọn
`ScaleSimulator` làm Startup Project, F5.

## Dùng qua TCP (test `ScaleConfig.ConnectionType = Tcp`)

1. Chọn **Model cân** khớp với model driver đang test (vd `Scale_Vibra_HAW30`).
2. Đặt **Giá trị (target)**, tuỳ chọn **Nhiễu ngẫu nhiên (±)** để mô phỏng dao động thật.
3. Group "TCP": đặt **Port** (mặc định 23, có thể đổi tuỳ ý), bấm **▶ Bật TCP server**.
4. Trỏ app đang test: `ScaleConfig.IP = "127.0.0.1"`, `ScaleConfig.Port` = cổng vừa chọn.

## Dùng qua COM (test `ScaleConfig.ConnectionType = Com`)

Windows không có "COM loopback" như TCP 127.0.0.1 — cần **1 cặp cổng COM ảo null-modem**:

- **Cách 1 (khuyên dùng): [com0com](https://sourceforge.net/projects/com0com/)** — driver cổng
  COM ảo miễn phí, mã nguồn mở. Cài xong sẽ có sẵn 1 cặp cổng nối chéo nhau (mặc định
  `CNCA0` ↔ `CNCB0`), có thể đổi tên hiển thị thành `COM5`/`COM6` qua Setup Command Prompt
  đi kèm (`change CNCA0 PortName=COM5`, `change CNCB0 PortName=COM6`). Mọi byte ghi vào
  1 đầu sẽ đọc được ở đầu kia.
- **Cách 2:** 2 cổng COM thật trên máy, nối bằng cáp null-modem (chéo TX/RX/GND).

Sau khi có cặp cổng (vd `COM5` ↔ `COM6`):

1. Chọn **Model cân** + **Giá trị**/**Nhiễu** như trên.
2. Group "COM": chọn cổng **COM5** (đầu simulator giữ), **Baud rate** (mặc định 9600 —
   khớp `ScaleConfig.BaudRate` mặc định), bấm **▶ Bật COM writer**.
3. Trỏ app đang test: `ScaleConfig.ConnectionType = Com`, `ScaleConfig.ComPort = "COM6"`
   (đầu còn lại của cặp), `ScaleConfig.BaudRate` khớp giá trị đã chọn ở bước 2.

Parity/DataBits/StopBits cố định `None/8/One` ở cả simulator lẫn `ScaleDriver` thật — không
cần chỉnh.

## Mô phỏng "đặt vật lên cân"

Nút **▶ Mô phỏng đặt vật lên cân (ramp → ổn định)**: đưa giá trị về 0, giữ một nhịp, rồi
tăng dần lên giá trị target trong ~1.2s kèm dao động nhẹ, sau đó giữ ổn định đúng bằng
target — hữu ích để test luồng đọc dữ liệu real-time và logic tự suy luận `Stable`
(vd `Scale_Shimadzu_TX4202L` — không có cờ ST/US từ máy, `ScaleDriver` tự tính dựa trên
độ dao động của 5 lần đọc gần nhất).

## Ghi chú theo từng model

Mỗi format raw data trong `ScaleFormats/ScaleFormatDefinition.cs` được build **khớp đúng**
regex/điều kiện parse thật trong `Scale_*/ScaleReading.cs` tương ứng (đã đối chiếu trực
tiếp source code, không đoán mò):

| Model | Ràng buộc đặc biệt |
|---|---|
| `Scale_DIGI` | Dòng phải đúng **9 ký tự** — simulator tự pad `0000.00` + 1 ký tự cờ + `=`. Không hỗ trợ số âm. |
| `Scale_IND_KG` | Dòng phải đúng **12 ký tự** — không có cờ Stable/Tare (parser luôn trả `false`). |
| `Scale_Vibra_SJ6200` | ⚠ Code parser gán `Stable = (flag=="S")` — **ngược** với comment trong chính file đó ("U là ổn định"). Dùng ComboBox 2 lựa chọn U/S để tự kiểm chứng `ScaleDriver.IsStable` thật sự trả về gì — có thể là bug có sẵn, ngoài phạm vi sửa của simulator này. |
| `Scale_Vibra_HAW30` | Cờ `ST`/`US` gửi kèm mỗi dòng — driver dùng trực tiếp, không suy luận. |
| `Scale_Shimadzu_TX4202L` | Không có cờ ST/US — `ScaleDriver` tự suy luận `Stable` phần mềm (cửa sổ 5 giá trị, biên độ ≤ 0.02g). Đặt **Nhiễu = 0** để test driver báo `Stable=true`; đặt **Nhiễu > 0.02** để test luôn `Stable=false`. |
