using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scale_IND_KG
{

    /// <summary>
    ///Vibra DIGI
    ///=0004.62(kg)
    /// </summary>
    public class ScaleReading
    {
        public static string oldData = "";
        public static string pattern = @"^=([\-0-9\.]+)\((kg)\)";// @"^=([0-9]{4}\.[0-9]{2})\((kg)\)$";
        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            //Debug.WriteLine(rawData.ToString());
            //rawData = "=0000.54(kg)";
            WeightValue = 0;
            Stable = false;// CountError > 5;
            Tare = false;
            Unit = "KG";

            bool isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length == 12;

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))
            {
                rawData = oldData;
            }

            try
            {
                isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length == 12;
                if (isTrueFormat)
                {
                    MatchCollection matches = Regex.Matches(rawData, pattern);
                    foreach (Match match in matches)
                    {
                        //Debug.WriteLine("VVVVVVVVVVVVVVVV");

                        //string stable = match.Groups[2]?.Value?.Replace(" ", "");   // A,B,@  A là điểm 0, B là ổn định, @ là đang cân
                        string weightStr = match.Groups[1].Value; // Trọng lượng

                        double weight = ThisToDouble(weightStr.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                        // string unit = match.Groups[4].Value;                       // Đơn vị cố định



                        //Đơn vị mặc định KG
                        WeightValue = weight;


                        //Stable = (stable?.ToUpper() == "B")
                        //    || (weight == 0 && (stable?.ToUpper() == "C"))
                        //     || (stable?.ToUpper() == "F") //Tare dương
                        //      || (weight < 0 && (stable?.ToUpper() == "G")) // Tare Âm
                        //    ;


                        //Tare = (stable?.ToUpper() == "F") //Tare dương
                        //    || (stable?.ToUpper() == "D")
                        //    || (stable?.ToUpper() == "E")
                        //      || (weight < 0 && (stable?.ToUpper() == "G"));

                        ////   Debug.WriteLine($"ST/US: {Stable}, Weight: {WeightValue} ");
                        ////Unit = "KG";
                        Unit = match.Groups[2].Value.ToUpper();
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


            if (double.TryParse(value.ToString(), NumberStyles.Any, new CultureInfo("en-US"), out double result) )
            {
                return result;
            }

            return 0;
        }

    }

}
