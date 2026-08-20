// ============================================================
// File: ScaleFormats/ScaleFormatDefinition.cs
// Mục đích: Định nghĩa 1 "kiểu format" dữ liệu thô — tương ứng 1-1 với
//           1 model cân thật (Scale_DIGI, Scale_IND_KG, Scale_Vibra_SJ6200,
//           Scale_Vibra_HAW30, Scale_Shimadzu_TX4202L) — sao cho dòng dữ
//           liệu build ra LUÔN khớp đúng regex/điều kiện parse trong
//           Scale_*/ScaleReading.cs tương ứng (đã đọc trực tiếp source
//           code của từng parser để đảm bảo khớp, không đoán mò).
// ============================================================

using System.Globalization;

namespace ScaleSimulator.ScaleFormats
{
    /// <summary>Một lựa chọn "cờ trạng thái" (raw flag) mà cân thật gửi kèm giá trị cân.</summary>
    public sealed class ScaleFlagOption
    {
        public string Label { get; }
        public string RawFlag { get; }

        public ScaleFlagOption(string label, string rawFlag)
        {
            Label = label;
            RawFlag = rawFlag;
        }

        public override string ToString() => Label; // hiển thị trong ComboBox
    }

    /// <summary>Dữ liệu đầu vào để build 1 dòng raw — do MainForm cập nhật theo UI.</summary>
    public sealed class ScaleBuildContext
    {
        /// <summary>Giá trị cân hiện tại (đã cộng nhiễu nếu có) — đơn vị theo <see cref="Unit"/>.</summary>
        public double Weight { get; set; }

        /// <summary>Đơn vị đang chọn (vd "g", "kg") — null nếu model không cho chọn.</summary>
        public string? Unit { get; set; }

        /// <summary>Cờ trạng thái raw đang chọn (vd "ST", "B", "U"...) — null nếu model không có cờ.</summary>
        public string? RawFlag { get; set; }
    }

    /// <summary>Định nghĩa đầy đủ 1 format dữ liệu thô của 1 model cân.</summary>
    public sealed class ScaleFormatDefinition
    {
        /// <summary>Đúng bằng ScaleConfig.ModelName / ScaleModelNames.* bên ScanAndScale.Core.</summary>
        public required string ModelName { get; init; }

        /// <summary>Tên hiển thị trên UI.</summary>
        public required string DisplayName { get; init; }

        /// <summary>Mẫu dữ liệu thật tham khảo — hiển thị làm gợi ý trên UI.</summary>
        public required string SampleHint { get; init; }

        /// <summary>Danh sách đơn vị chọn được — rỗng nếu model cố định 1 đơn vị.</summary>
        public string[] Units { get; init; } = Array.Empty<string>();

        /// <summary>Danh sách cờ trạng thái chọn được — rỗng nếu model không có cờ.</summary>
        public ScaleFlagOption[] FlagOptions { get; init; } = Array.Empty<ScaleFlagOption>();

        /// <summary>Số chữ số thập phân hiển thị mặc định (chỉ để set NumericUpDown, không ép trong builder).</summary>
        public int DecimalPlaces { get; init; } = 2;

        /// <summary>Giá trị min/max hợp lý cho model (để giới hạn NumericUpDown cân).</summary>
        public double MinWeight { get; init; } = -9999.99;
        public double MaxWeight { get; init; } = 9999.99;

        /// <summary>Ghi chú ràng buộc đặc biệt (vd DIGI/IND_KG yêu cầu độ dài dòng cố định).</summary>
        public string? Note { get; init; }

        /// <summary>Build 1 dòng raw data từ context hiện tại. PHẢI khớp đúng parser thật.</summary>
        public required Func<ScaleBuildContext, string> BuildLine { get; init; }
    }

    /// <summary>
    /// Danh mục tất cả format cân hỗ trợ — mỗi entry đã được đối chiếu trực tiếp với
    /// regex/điều kiện trong Scale_*/ScaleReading.cs tương ứng trong repo này.
    /// </summary>
    public static class ScaleFormatCatalog
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly ScaleFormatDefinition Digi = new()
        {
            ModelName = "Scale_DIGI",
            DisplayName = "Scale_DIGI",
            SampleHint = "0003.08@=   (đủ đúng 9 ký tự/dòng, không hỗ trợ số âm)",
            DecimalPlaces = 2,
            MinWeight = 0,
            MaxWeight = 9999.99,
            Note = "Parser yêu cầu rawData.Length == 9 CHÍNH XÁC → simulator luôn pad 4 chữ số nguyên (0000.00) + 1 ký tự cờ + \"=\".",
            FlagOptions = new[]
            {
                new ScaleFlagOption("@  Đang cân (chưa ổn định)", "@"),
                new ScaleFlagOption("B  Ổn định (Stable)", "B"),
                new ScaleFlagOption("A  Điểm 0", "A"),
                new ScaleFlagOption("C  Lỗi / quá tải", "C"),
                new ScaleFlagOption("F  Tare dương (ổn định)", "F"),
            },
            BuildLine = ctx =>
            {
                double w = Math.Clamp(ctx.Weight, 0, 9999.99);
                string numPart = w.ToString("0000.00", Inv); // luôn 7 ký tự
                string flag = string.IsNullOrEmpty(ctx.RawFlag) ? "@" : ctx.RawFlag!;
                return $"{numPart}{flag}=";
            }
        };

        public static readonly ScaleFormatDefinition IndKg = new()
        {
            ModelName = "Scale_IND_KG",
            DisplayName = "Scale_IND_KG",
            SampleHint = "=0004.62(kg)   (đủ đúng 12 ký tự/dòng)",
            DecimalPlaces = 2,
            MinWeight = -999.99,
            MaxWeight = 9999.99,
            Note = "Parser yêu cầu rawData.Length == 12 CHÍNH XÁC — không có cờ Stable/Tare (parser luôn trả về false).",
            BuildLine = ctx =>
            {
                double w = Math.Clamp(ctx.Weight, -999.99, 9999.99);
                string numPart = w < 0
                    ? "-" + Math.Abs(w).ToString("000.00", Inv)  // 1 + 6 = 7 ký tự
                    : w.ToString("0000.00", Inv);                 // 7 ký tự
                return $"={numPart}(kg)";
            }
        };

        public static readonly ScaleFormatDefinition VibraSj6200 = new()
        {
            ModelName = "Scale_Vibra_SJ6200",
            DisplayName = "Scale_Vibra_SJ6200",
            SampleHint = "+0920.91 G U",
            DecimalPlaces = 2,
            MinWeight = -9999.99,
            MaxWeight = 9999.99,
            Units = new[] { "G", "KG" },
            Note = "⚠ Code Scale_Vibra_SJ6200/ScaleReading.cs gán Stable = (flag==\"S\") — NGƯỢC với comment trong " +
                   "chính file đó (\"U là ổn định\"). Có thể là bug có sẵn, ngoài phạm vi simulator này. Dùng 2 lựa " +
                   "chọn U/S bên dưới để tự kiểm chứng ScaleDriver.IsStable thực tế trả về gì.",
            FlagOptions = new[]
            {
                new ScaleFlagOption("U  (theo tài liệu máy: ổn định)", "U"),
                new ScaleFlagOption("S  (theo tài liệu máy: đang cân)", "S"),
            },
            BuildLine = ctx =>
            {
                double w = Math.Clamp(ctx.Weight, -9999.99, 9999.99);
                string sign = w < 0 ? "-" : "+";
                string numPart = Math.Abs(w).ToString("0000.00", Inv);
                string unit = string.IsNullOrEmpty(ctx.Unit) ? "G" : ctx.Unit!;
                string flag = string.IsNullOrEmpty(ctx.RawFlag) ? "U" : ctx.RawFlag!;
                return $"{sign}{numPart} {unit} {flag}";
            }
        };

        public static readonly ScaleFormatDefinition VibraHaw30 = new()
        {
            ModelName = "Scale_Vibra_HAW30",
            DisplayName = "Scale_Vibra_HAW30",
            SampleHint = "ST,NT,-  0.597  g",
            DecimalPlaces = 3,
            MinWeight = -999.999,
            MaxWeight = 999.999,
            Units = new[] { "g", "kg" },
            FlagOptions = new[]
            {
                new ScaleFlagOption("ST  Ổn định (Stable)", "ST"),
                new ScaleFlagOption("US  Đang cân", "US"),
            },
            BuildLine = ctx =>
            {
                double w = Math.Clamp(ctx.Weight, -999.999, 999.999);
                string sign = w < 0 ? "-" : "+";
                string numPart = Math.Abs(w).ToString("0.000", Inv);
                string unit = string.IsNullOrEmpty(ctx.Unit) ? "kg" : ctx.Unit!;
                string flag = string.IsNullOrEmpty(ctx.RawFlag) ? "ST" : ctx.RawFlag!;
                return $"{flag},NT,{sign}  {numPart}  {unit}";
            }
        };

        public static readonly ScaleFormatDefinition ShimadzuTx4202L = new()
        {
            ModelName = "Scale_Shimadzu_TX4202L",
            DisplayName = "Scale_Shimadzu_TX4202L",
            SampleHint = "48.08g   (không có cờ ST/US — ScaleDriver tự suy luận Stable từ độ dao động)",
            DecimalPlaces = 2,
            MinWeight = -4200,
            MaxWeight = 4200,
            Units = new[] { "g", "kg" },
            Note = "Không có ComboBox cờ trạng thái — driver thật tự tính Stable phần mềm bằng cửa sổ 5 giá trị " +
                   "gần nhất (biên độ ≤ 0.02g). Dùng ô \"Nhiễu (±)\" bên dưới: để 0 → driver sẽ báo Stable=true sau " +
                   "~5 lần đọc; đặt > 0.02 → driver sẽ luôn báo Stable=false, mô phỏng đúng cách test thực tế.",
            BuildLine = ctx =>
            {
                double w = Math.Clamp(ctx.Weight, -4200, 4200);
                string numPart = w.ToString("0.00", Inv);
                string unit = string.IsNullOrEmpty(ctx.Unit) ? "g" : ctx.Unit!;
                return $"{numPart}{unit}";
            }
        };

        public static readonly ScaleFormatDefinition[] All =
        {
            Digi, IndKg, VibraSj6200, VibraHaw30, ShimadzuTx4202L
        };
    }
}
