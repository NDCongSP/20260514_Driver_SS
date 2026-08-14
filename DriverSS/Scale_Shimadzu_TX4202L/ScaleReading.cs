using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scale_Shimadzu_TX4202L
{
    /// <summary>
    /// Parser cho can dien tu Shimadzu TX4202L (dong UniBloc, max 4200g, d=0.01g).
    /// Ket noi qua bo chuyen doi Serial-to-Ethernet (TCP) toi cong RS-232C cua can.
    ///
    /// Mau du lieu tho thuc te (bat qua Hercules TCP Client, IP 192.168.80.237:23):
    ///     0.08g -    0.08g -    0.08g -    0.08g -    0.08g
    ///    53.22g   48.09g   48.06g   48.08g   48.08g
    ///    48.08g   48.05g   48.09g   48.08g   48.08g
    ///    48.09g   48.09g   48.07g   48.08g   48.08g
    ///    77.15g   88.31g   88.35g   88.37g   88.37g
    ///    88.36g   88.35g   88.37g   88.37g   88.36g
    ///
    /// => Moi gia tri la mot so thap phan (co the co dau +/-), theo sau ngay boi
    ///    don vi "g" (hoac "kg" voi cau hinh khac), nhieu gia tri co the dinh lien
    ///    nhau trong cung mot lan doc, cach nhau boi khoang trang hoac dau "-".
    ///    Format nay KHONG co co ST/US bao trang thai on dinh qua RS232 (khac Vibra
    ///    HAW30/SJ6200) - Stable duoc driver TU SUY LUAN o phia phan mem, xem UpdateStability().
    ///
    /// Chien luoc parse: quet toan bo rawData bang regex, lay MATCH CUOI CUNG
    /// (gia tri moi nhat trong goi du lieu vua doc duoc) lam ket qua hien tai —
    /// tuong tu cach Scale_Vibra_SJ6200 xu ly khi mot lan doc nhan duoc nhieu gia tri.
    /// </summary>
    public class ScaleReading
    {
        public static string oldData = "";

        // So thap phan (+/-), theo sau boi don vi g hoac kg (khong phan biet hoa/thuong).
        // (?<=^|\s): so PHAI bat dau ngay sau dau cach hoac dau chuoi - chan cac fragment
        // dang ".44g" (chi con phan thap phan do doc du lieu bi dut giua so tren TCP)
        // bi hieu nham la mot gia tri hoan chinh rieng (vd. ".44g" -> hieu la 44g/1000).
        public static string pattern = @"(?:(?<=^)|(?<=\s))([+-]?\d+(?:\.\d+)?)\s*(kg|g)\b";

        // ─── Suy luan trang thai on dinh (Stable) o phia phan mem ──────────────
        // Format continuous-output hien tai cua may KHONG gui co ST/US qua RS232
        // (khac Vibra HAW30/SJ6200) - mui ten on dinh tren man hinh LCD la do
        // CHINH CAN tu tinh toan noi bo (bo loc rieng cua no), khong duoc phat ra
        // ngoai serial. Du lieu raw van dao dong nho (vd 66.26-66.28g) ke ca khi
        // LCD da bao on dinh (66.29g).
        // => Driver tu suy ra on dinh bang cach theo doi StableWindowSize gia tri
        //    doc gan nhat: neu bien do (max-min) trong cua so do <= StableToleranceG
        //    (~2 lan do phan giai d=0.01g in tren may) thi coi la on dinh.
        // Luu y: _recentWeights la static (giong oldData o tren) - chi dung dung
        // khi co DUY NHAT 1 can Shimadzu TX4202L ket noi cung luc trong 1 process.
        private const int    StableWindowSize = 5;    // So gia tri gan nhat can xem xet
        private const double StableToleranceG = 0.02; // Bien do toi da (gram) de coi la on dinh
        private static readonly Queue<double> _recentWeights = new Queue<double>();

        // ─── Diagnostic: cho phep ScaleDriver/UI doc duoc so lieu tinh Stable thuc te ──
        // ScaleDriver.GetWeight() dung reflection voi signature CO DINH nen KHONG the
        // them out-param moi ma khong lam vo cac Scale_* khac. Thay vao do, expose them
        // 1 static field rieng (optional - ScaleDriver doc "best-effort" qua reflection,
        // Scale_* khac khong co field nay van hoat dong binh thuong) de debug xem thuc te
        // window/range dang la bao nhieu, thay vi doan mo khi Stable cu mai la False.
        public static string LastStabilityInfo = "";

        private static bool UpdateStability(double weight)
        {
            _recentWeights.Enqueue(weight);
            while (_recentWeights.Count > StableWindowSize)
                _recentWeights.Dequeue();

            if (_recentWeights.Count < StableWindowSize)
            {
                LastStabilityInfo = $"win={_recentWeights.Count}/{StableWindowSize} (chua du du lieu)";
                return false; // Chua du du lieu trong cua so - chua the ket luan
            }

            double min = double.MaxValue, max = double.MinValue;
            foreach (var w in _recentWeights)
            {
                if (w < min) min = w;
                if (w > max) max = w;
            }
            double range = max - min;
            bool stable = range <= StableToleranceG;
            LastStabilityInfo = $"win={_recentWeights.Count}/{StableWindowSize} range={range:F3}g (tol={StableToleranceG:F2}g) -> {(stable ? "ON DINH" : "CHUA on dinh")}";
            return stable;
        }

        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            WeightValue = 0;
            Stable = false; // Mac dinh - se duoc tinh lai qua UpdateStability() neu parse thanh cong
            Tare = false;
            Unit = "G"; // Don vi mac dinh cua can nay la gram (chua co gia tri hop le nao doc duoc)

            bool isTrueFormat = !string.IsNullOrEmpty(rawData) && Regex.IsMatch(rawData, pattern, RegexOptions.IgnoreCase);

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))
            {
                rawData = oldData;
            }

            try
            {
                isTrueFormat = !string.IsNullOrEmpty(rawData) && Regex.IsMatch(rawData, pattern, RegexOptions.IgnoreCase);
                if (isTrueFormat)
                {
                    Debug.WriteLine(rawData);

                    MatchCollection matches = Regex.Matches(rawData, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        string weightStr = match.Groups[1].Value; // Trong luong
                        string unit = match.Groups[2].Value;      // Don vi: g hoac kg

                        double weight = ThisToDouble(weightStr); // Chuyen doi trong luong sang double

                        // KHONG quy doi don vi - giu nguyen gia tri va don vi raw tu can
                        // (can Shimadzu TX4202L o day luon gui gram "g", khong tu y ep ve KG).
                        WeightValue = weight;
                        Unit = unit.ToUpper();
                        Stable = UpdateStability(weight);
                    }

                    oldData = rawData;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scale_Shimadzu_TX4202L] Parse error: {ex.Message}");
            }
        }

        private static double ThisToDouble(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (double.TryParse(value.ToString(), NumberStyles.Any, new CultureInfo("en-US"), out double result))
            {
                return result;
            }

            return 0;
        }
    }
}
