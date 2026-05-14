using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scale_DIGI
{

    /// <summary>
    ///Vibra DIGI
    ///3.08@=   3.08@=   1.24@=   2.79@=   2.79@=   1.95@=   2.10@=   2.10@=   1.97@=   1.94@=   1.94@=   
    ///3.21@=   3.21@=   1.90@=   3.57@=   3.57@=   1.65@=   3.82@=   3.82@=   1.51@=   3.79@=   3.79@= 
    ///1.99@=   3.85@=   3.85@=   1.53@=   3.32@=   3.32@=   2.38@=   2.66@=   2.66@=   2.51@=   2.51@= 
    ///1.63@=   2.38@=   2.38@=   1.32@=   1.44@=   1.44@=   0.91@=   1.09@=   1.09@=   0.97@=   0.93@= 
    ///0.93A=   0.00A=   0.00A=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.53@=   0.53@= 
    ///1.23@=   0.88@=   0.88@=   0.92@=   0.92@=   0.97@=   0.92@=   0.92@=   0.86@=   0.85@=   0.85@= 
    ///0.81@=   0.78@=   0.78@=   0.75@=   0.35@=   0.35@=   0.39@=   0.36@=   0.36@=   0.35@=   0.35@= 
    ///0.35@=   0.56@=   0.56@=   0.63@=   0.61@=   0.61B=   0.61B=   0.97@=   0.97@=   0.94@=   0.88@= 
    ///0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=  
    ///0.88@=   1.57@=   1.55@=   1.55@=   1.13@=   0.87@=   0.87@=   0.88B=   0.88B=   0.88B=   0.88B= 
    ///0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B= 
    ///0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B= 
    ///0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B=   0.88B= 
    ///0.88B=   0.88B=   0.88B=   0.87B=   0.87B=   0.87B=   0.87B=   0.88B=   0.88B=   0.88B=   0.88B=  
    ///0.88B=   0.88B=   0.88B=   0.88B=   0.87A=   0.87A=   0.00A=   0.00A=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00B=   0.00B=   0.01B=   0.01B=   0.01B=   0.01B=  
    ///0.01B=   0.01B=   0.01B=   0.01B=   0.01C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=  
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=  
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00A=   0.00@=   0.44@=   0.42@=   0.42@=   0.39@=   0.39B= 
    ///0.39B=   0.39B=   0.39B=   0.39B=   0.38@=   0.32@=   0.32@=   0.30@=   0.30B=   0.30B=   0.25@= 
    ///0.25@=   0.23@=   0.20@=   0.20A=   0.00A=   0.00A=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C=   0.00C= 
    ///0.00C=   0.00C=   0.00C
    /// </summary>
    public class ScaleReading
    {
        public static string oldData = "";
        public static string pattern = @"=\s*([+-]?\s*[\d.]+)(\w|@)";//  @"=\s*([+-]\s*[\d.]+)(\w|@)";// @"=\s*([\d.]+)(\w)";
        public static void GetWeight(out double? WeightValue, out bool? Stable, out bool? Tare, out string Unit, string rawData)
        {
            //Debug.WriteLine(rawData.ToString());

            WeightValue = 0;
            Stable = false;// CountError > 5;
            Tare = false;
            Unit = "KG";

            bool isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length == 9;

            if (isTrueFormat == false && !string.IsNullOrEmpty(oldData))
            {
                rawData = oldData;
            }

            try
            {
                isTrueFormat = !string.IsNullOrEmpty(rawData) && rawData.Length == 9;
                if (isTrueFormat)
                {
                    MatchCollection matches = Regex.Matches(rawData, pattern);
                    foreach (Match match in matches)
                    {
                        //Debug.WriteLine("VVVVVVVVVVVVVVVV");

                        string stable = match.Groups[2]?.Value?.Replace(" ", "");   // A,B,@  A là điểm 0, B là ổn định, @ là đang cân
                        string weightStr = match.Groups[1].Value; // Trọng lượng
                        double weight = ThisToDouble(weightStr.Replace(" ", ""));  // Chuyển đổi trọng lượng sang double

                        // string unit = match.Groups[4].Value;                       // Đơn vị cố định



                        //Đơn vị mặc định KG
                        WeightValue = weight;
                        Stable = (stable?.ToUpper() == "B")
                            || (weight == 0 && (stable?.ToUpper() == "C"))
                             || (stable?.ToUpper() == "F") //Tare dương
                              || (weight < 0 && (stable?.ToUpper() == "G")) // Tare Âm
                            ;


                        Tare = (stable?.ToUpper() == "F") //Tare dương
                            || (stable?.ToUpper() == "D")
                            || (stable?.ToUpper() == "E")
                              || (weight < 0 && (stable?.ToUpper() == "G"));

                        //   Debug.WriteLine($"ST/US: {Stable}, Weight: {WeightValue} ");
                        Unit = "KG";

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


            if (double.TryParse(value.ToString(), NumberStyles.Any, new CultureInfo("en-US"), out double result))
            {
                return result;
            }

            return 0;
        }

    }

}
