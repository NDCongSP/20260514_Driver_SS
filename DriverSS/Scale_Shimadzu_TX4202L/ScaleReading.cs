using System;
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
    ///    Format nay KHONG co co ST/US bao trang thai on dinh (khac Vibra HAW30/SJ6200).
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

        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            WeightValue = 0;
            Stable = false; // Format nay khong co co ST/US -> khong xac dinh duoc trang thai on dinh
            Tare = false;
            Unit = "KG";

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

                        // Doi tat ca ve don vi thong nhat la KG
                        if (unit.ToUpper() == "KG")
                            weight = 1 * weight;
                        else if (unit.ToUpper() == "G")
                            weight = 0.001 * weight;

                        // Vong lap ghi de -> ket thuc se la MATCH CUOI CUNG (moi nhat)
                        WeightValue = weight;
                        // QUAN TRONG: WeightValue da duoc quy doi ve KG o tren (dong 66-69),
                        // nen Unit phai luon la "KG" - KHONG duoc gan lai theo don vi raw (g/kg)
                        // vi se gay nham lan: so hien thi la KG nhung nhan lai ghi "G".
                        Unit = "KG";
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
