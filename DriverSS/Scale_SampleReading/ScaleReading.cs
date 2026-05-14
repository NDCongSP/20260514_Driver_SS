using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Scale_SampleReading
{

    /// <summary>
    ///Vibra HAW-30
    ///ST,NT,-  0.597  g
    ///US, NT,-  0.593  g
    ///ST, NT,-  0.597  kg
    ///US, NT,-  0.593  kg
    ///ST, GS,+  0.582  kg
    ///US, GS,+  3.029  kg
    /// </summary>
    public class ScaleReading
    {
        public static string oldData = "";
        // public static string pattern = @"(\bST\b|\bUS\b),(\bNT\b|\bGS\b),([+-]\s*\d+(\.\d+)?)\s*kg";
        public static string pattern = @"(\bST\b|\bUS\b),(\bNT\b|\bGS\b),([+-]\s*\d+(\.\d+)?)\s*(g|kg)";
        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            //Debug.WriteLine(rawData.ToString());

            WeightValue = 0;
            Stable = false;// CountError > 5;
            Tare = false;
            Unit = "KG";
            bool isTrueFormat = !string.IsNullOrEmpty(rawData);//&& rawData.Length == 7;

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))
            {
                rawData = oldData;
            }

            try
            {
                isTrueFormat = !string.IsNullOrEmpty(rawData);//&& rawData.Length == 7;
                if (isTrueFormat)
                {

                    //MatchCollection matches = Regex.Matches(rawData, pattern);
                    //foreach (Match match in matches)
                    //{
                    //    string stable = match.Groups[1].Value;    // ST hoặc US
                    //    string ntOrGs = match.Groups[2].Value;    // NT hoặc GS
                    //    string weightStr = match.Groups[3].Value; // Trọng lượng
                    //    double weight = double.Parse(weightStr.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                    //    string unit = match.Groups[4].Value;                       // Đơn vị cố định

                    //    // Debug.WriteLine($"ST/US: {stable}, NT/GS: {ntOrGs}, Weight: {weight} {unit}");
                    //    //Đổi tất cả về đơn vị thống nhất về KG
                    //    if (unit.ToUpper() == "KG")
                    //        weight = 1 * weight;
                    //    else if (unit.ToUpper() == "G")
                    //        weight = 0.001 * weight;
                    //    else if (unit.ToUpper() == "TON")
                    //        weight = 1000 * weight;

                    //    WeightValue = weight;
                    //    Stable = (stable == "ST");
                    //    Unit = unit.ToUpper();




                    //    oldData = rawData;

                    //}


                    string result = rawData.Substring(0, 7);
                    // Đảo ngược chuỗi
                    string reversed = new string(result.Reverse().ToArray());
                    double weight = double.Parse(reversed.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                }

            }
            catch (Exception ex)
            {
                ;
            }
        }
    }

}
