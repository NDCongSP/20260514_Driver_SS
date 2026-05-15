using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scale_Vibra_HAW30
{
    /// <summary>
    ///Vibra HAW-30
    ///ST,NT,-  0.597  g
    ///US,NT,-  0.593  g
    ///ST, NT,-  0.597  kg
    ///US, NT,-  0.593  kg
    ///ST, GS,+  0.582  kg
    ///US, GS,+  3.029  kg
    /// </summary>
    public class ScaleReading
    {
        public static string oldData = "";
        public static string pattern = @"(\bST\b|\bUS\b),(\bNT\b|\bGS\b),([+-]\s*\d+(\.\d+)?)\s*(g|kg)";

        /// <summary>
        /// Get scale weight from raw data.
        /// </summary>
        /// <param name="WeightValue"></param>
        /// <param name="Stable"></param>
        /// <param name="Tare"></param>
        /// <param name="Unit"></param>
        /// <param name="rawData">The string read from scale.</param>
        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            //Debug.WriteLine(rawData.ToString());

            WeightValue = 0;
            Stable = false;// CountError > 5;
            Tare = false;
            Unit = "KG";

            bool isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length >= 12;

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))
            {
                rawData = oldData;
            }

            try
            {
                isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length >= 12;
                if (isTrueFormat)
                {
                    //Debug.WriteLine(rawData);
                    //Match match = Regex.Match(rawData, pattern);
                    MatchCollection matches = Regex.Matches(rawData, pattern);
                    foreach (Match match in matches)
                    {
                        string stable = match.Groups[1].Value;    // ST hoặc US . "ST" (Stable)
                        string weightType = match.Groups[2].Value;    // NT hoặc GS. "GS" (Gross)
                        string weightWithSign = match.Groups[3].Value; // Trọng lượng
                        string unit = match.Groups[5].Value;                       // Đơn vị cố định

                        //Debug.WriteLine(weightWithSign);

                        //// Detect system's regional settings
                        //CultureInfo currentCulture = CultureInfo.CurrentCulture;

                        //double weight = 0;
                        //// Convert string weight to decimal
                        //if (double.TryParse(weightWithSign.Replace(" ", ""), NumberStyles.Number, currentCulture, out weight))
                        //{

                        //    Console.WriteLine($"Weight: {weight} {unit}");
                        //}
                        //else
                        //{
                        //    Console.WriteLine("Error: Could not parse weight.");
                        //}
                        double weight = ThisToDouble(weightWithSign.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                        //Debug.WriteLine($"ST/US: {stable}, NT/GS: {weightType}, Weight: {weight} {unit}");
                        //Đổi tất cả về đơn vị thống nhất về KG
                        if (unit.ToUpper() == "KG")
                            weight = 1 * weight;
                        else if (unit.ToUpper() == "G")
                            weight = 0.001 * weight;
                        else if (unit.ToUpper() == "TON")
                            weight = 1000 * weight;

                        WeightValue = weight;
                        Stable = stable == "ST";
                        Unit = unit.ToUpper();
                    }
                }

                oldData = rawData;
            }
            catch (Exception ex)
            {

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
