using System;

using System.Diagnostics;

using System.Text.RegularExpressions;

namespace Scale_Vibra_SJ6200

{

    public class ScaleReading

    {

        public static string oldData = "";

        public static string pattern = @"([-+]?\d+\.\d+)\s*(\w+)\s*(\w+)";

        //+0920.91 G U

        //+0933.84 G U

        //+0951.34 G U

        //+0928.53 G U

        //+0909.27 G U

        //+1056.10 G U

        //+1625.40 G U

        //+1306.65 G U

        //+1042.51 G U

        //+0397.38 G U

        //+0143.35 G U

        //+0246.02 G U

        //+0331.14 G U

        //+0385.38 G U

        //+0425.91 G U

        //+0429.53 G U

        //+0425.78 G U

        //+0426.71 G U

        //+0422.12 G U

        //+0419.49 G U

        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)

        {

            WeightValue = 0;

            Stable = false;// CountError > 5;

            Tare = false;

            Unit = "KG";


            bool isTrueFormat = !string.IsNullOrEmpty(rawData) &&

                ((rawData.StartsWith("+") || rawData.StartsWith("-"))
&& (rawData.ToUpper().EndsWith("U") || rawData.ToUpper().EndsWith("S")));

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))

            {

                rawData = oldData;

            }

            try

            {

                isTrueFormat = !string.IsNullOrEmpty(rawData) &&

                    ((rawData.StartsWith("+") || rawData.StartsWith("-"))
&& (rawData.ToUpper().EndsWith("U") || rawData.ToUpper().EndsWith("S")));

                if (isTrueFormat)

                {

                    Debug.WriteLine(rawData);

                    MatchCollection matches = Regex.Matches(rawData, pattern);

                    foreach (Match match in matches)

                    {

                        string stable = match.Groups[3].Value;    // U hoặc S U là ổn định S là đang cân

                        string ntOrGs = match.Groups[2].Value;    // cái này là đơn vị thôi

                        string weightStr = match.Groups[1].Value; // Trọng lượng

                        double weight = ThisToDouble(weightStr.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                        string unit = match.Groups[2].Value;                       // Đơn vị cố định

                        Debug.WriteLine($"Ổn Định: {stable}, Đơn vị: {ntOrGs}, Weight: {weight}");

                        // Đổi tất cả về đơn vị thống nhất về KG

                        if (unit.ToUpper() == "KG")

                            weight = 1 * weight;

                        else if (unit.ToUpper() == "G")

                            weight = 0.001 * weight;

                        WeightValue = weight;

                        Stable = stable == "S";

                        Unit = unit.ToUpper();

                    }

                    oldData = rawData;

                }

            }

            catch (Exception ex)

            {

                ;

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

            if (double.TryParse(value.ToString(), out double result))

            {

                return result;

            }

            return 0;

        }

    }

}

