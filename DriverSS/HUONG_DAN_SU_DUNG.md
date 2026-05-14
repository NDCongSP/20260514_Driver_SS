# ScanAndScale.Core — Thư viện Driver Đa Nền Tảng

## Tổng quan

Thư viện `ScanAndScale.Core.dll` hỗ trợ đọc dữ liệu từ 3 loại thiết bị:

| Thiết bị | Driver | Giao thức | Ghi chú |
|----------|--------|-----------|---------|
| **Barcode** | `BarcodeDriver` | Zebra CoreScanner SDK | Cần cài Zebra SDK |
| **RFID** | `RfidDriver` | SerialPort (COM) | Tự tìm cổng COM |
| **Cân điện tử** | `ScaleDriver` | TCP/IP (Telnet) | Load DLL model cân |

---

## Cấu trúc thư mục

```
DriverSS/
├── ScanAndScale.Core/          ← Thư viện DLL (multi-target)
│   ├── Models/                 ← DriverStatus, DataValue, Config classes
│   ├── Drivers/                ← BarcodeDriver, RfidDriver, ScaleDriver
│   ├── Helpers/                ← DriverHelper (utility methods)
│   └── lib/                    ← Đặt Interop.CoreScanner.dll ở đây
│
└── WpfSample/                  ← Project WPF mẫu
    ├── ViewModels/             ← MainViewModel (MVVM)
    ├── Converters/             ← ValueConverters cho binding
    ├── MainWindow.xaml         ← Giao diện chính
    └── MainWindow.xaml.cs      ← Code-behind (rất ngắn)
```

---

## Multi-target Framework

Thư viện build cho **3 target** để tương thích tối đa:

```
net472             → .NET Framework 4.7.2 (WinForms cũ)
net6.0-windows     → .NET 6 WinForms hoặc WPF
net8.0-windows     → .NET 8 WinForms hoặc WPF
```

---

## Cách thêm thư viện vào project của bạn

### Cách 1: Dùng ProjectReference (khi có source code)
```xml
<ItemGroup>
  <ProjectReference Include="path\to\ScanAndScale.Core\ScanAndScale.Core.csproj" />
</ItemGroup>
```

### Cách 2: Dùng DLL trực tiếp (sau khi build)
```xml
<ItemGroup>
  <Reference Include="ScanAndScale.Core">
    <HintPath>path\to\ScanAndScale.Core.dll</HintPath>
  </Reference>
</ItemGroup>
```

---

## Hướng dẫn sử dụng từng Driver

### 1. Barcode Driver (Zebra Scanner)

```csharp
using ScanAndScale.Core.Drivers;
using ScanAndScale.Core.Models;

// BarcodeDriver là Singleton — dùng .Instance
var barcodeDriver = BarcodeDriver.Instance;

// Đăng ký lắng nghe sự kiện TRƯỚC khi Initialize
barcodeDriver.DataValueChanged += (sender, e) =>
{
    // ⚠️ Chạy trên background thread của Zebra SDK!
    // WPF: cần Dispatcher.Invoke
    // WinForms: cần control.Invoke

    if (e.NewValue.IsValid)
    {
        string barcode = e.NewValue.Value?.ToString() ?? "";
        Console.WriteLine($"Barcode: {barcode}");
    }
};

// Khởi tạo với config
bool ok = barcodeDriver.Initialize(new BarcodeConfig
{
    Enable = true,
    ReadOnly = false
});

// Khi thoát app:
barcodeDriver.Dispose();
```

**Yêu cầu:** Copy `Interop.CoreScanner.dll` vào thư mục `lib\` của project ScanAndScale.Core.

---

### 2. RFID Driver (SerialPort)

```csharp
using ScanAndScale.Core.Drivers;
using ScanAndScale.Core.Models;

// RfidDriver là Singleton
var rfidDriver = RfidDriver.Instance;

// Đăng ký event
rfidDriver.DataValueChanged += (sender, e) =>
{
    // ⚠️ Chạy trên ThreadPool thread (SerialPort.DataReceived)!

    if (e.NewValue.IsValid)
    {
        string rfidCode = e.NewValue.Value?.ToString() ?? "";
        Console.WriteLine($"RFID: {rfidCode}");
    }
};

// Khởi tạo — tự tìm COM port
bool ok = rfidDriver.Initialize(new RfidConfig
{
    Enable = true,
    AutoFindCom = true,            // Tự động tìm cổng COM
    DeviceCaption = "Pongee",      // Tên thiết bị trong Device Manager
    DeviceManufacturer = "Prolific", // Nhà sản xuất chip USB-Serial
    BaudRate = 9600
});

// Hoặc chỉ định cổng COM cụ thể:
// rfidDriver.Initialize(new RfidConfig {
//     Enable = true,
//     AutoFindCom = false,
//     ComPort = "COM3"
// });

// Kết nối lại nếu mất kết nối:
rfidDriver.Reconnect();

// Khi thoát:
rfidDriver.Dispose();
```

---

### 3. Scale Driver (TCP/IP)

```csharp
using ScanAndScale.Core.Drivers;
using ScanAndScale.Core.Models;

// ScaleDriver KHÔNG phải Singleton — tạo instance cho mỗi cân
var scaleDriver = new ScaleDriver();

// Đăng ký event
scaleDriver.DataValueChanged += (sender, e) =>
{
    // ⚠️ Chạy trên ThreadPool thread (Timer.Elapsed)!

    if (e.NewValue.DriverStatus == DriverStatus.Connected)
    {
        double weightKg = Convert.ToDouble(e.NewValue.Value ?? 0.0);
        bool isStable = scaleDriver.IsStable;
        string unit = scaleDriver.Unit; // "KG", "G", "TON"
        Console.WriteLine($"Cân: {weightKg:F3} {unit} (Stable={isStable})");
    }
};

// Khởi tạo
scaleDriver.Initialize(new ScaleConfig
{
    Enable = true,
    IP = "192.168.80.237",     // IP của cân
    Port = 23,                  // Cổng Telnet
    ModelName = "Scale_DIGI",  // DLL parser: Scale_DIGI.dll phải tồn tại!
    TimeScanMs = 400,           // Đọc mỗi 400ms
    CalibZero = 0.0,
    CalibGain = 1.0,
    DecimalNum = 3
});

// Khi thoát:
scaleDriver.Dispose();
```

**Yêu cầu:** File `Scale_DIGI.dll` (hoặc tên model tương ứng) phải nằm cùng thư mục với file `.exe`.

---

## Xử lý Thread trong WPF

Tất cả drivers đều bắn event từ **background thread**. Trong WPF, phải dispatch về UI thread:

```csharp
// Cách 1: Dùng Dispatcher.Invoke (đồng bộ)
driver.DataValueChanged += (sender, e) =>
{
    Application.Current?.Dispatcher.Invoke(() =>
    {
        // Cập nhật UI ở đây — an toàn
        myTextBox.Text = e.NewValue.Value?.ToString();
    });
};

// Cách 2: Dùng Dispatcher.BeginInvoke (bất đồng bộ)
driver.DataValueChanged += (sender, e) =>
{
    Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        myTextBox.Text = e.NewValue.Value?.ToString();
    });
};
```

## Xử lý Thread trong WinForms

```csharp
driver.DataValueChanged += (sender, e) =>
{
    if (myTextBox.InvokeRequired)
    {
        myTextBox.Invoke(() => myTextBox.Text = e.NewValue.Value?.ToString());
    }
    else
    {
        myTextBox.Text = e.NewValue.Value?.ToString();
    }
};
```

---

## Mô hình Models

```csharp
// DataValue — đóng gói giá trị + trạng thái
public class DataValue
{
    public DriverStatus DriverStatus { get; set; }  // Connected/Disconnected/...
    public object? Value { get; set; }               // Giá trị thực tế
    public bool IsValid { get; }                     // Connected && Value != null
}

// DriverStatus — trạng thái kết nối
public enum DriverStatus { Unknown, Connected, Disconnected, Reconnecting }

// DataValueChangedEventArgs — event args khi có thay đổi
public class DataValueChangedEventArgs : EventArgs
{
    public DataValue OldValue { get; }  // Giá trị trước
    public DataValue NewValue { get; }  // Giá trị mới
}
```

---

## WPF MVVM Pattern (xem WpfSample)

```
MainWindow.xaml          → Giao diện (XAML binding)
MainWindow.xaml.cs       → Code-behind (rất ngắn, chỉ tạo VM + Init/Dispose)
MainViewModel.cs         → Toàn bộ logic: khởi tạo drivers, xử lý events
BaseViewModel.cs         → INotifyPropertyChanged + RelayCommand
DriverStatusConverter.cs → Chuyển DriverStatus → màu sắc/visibility
```
