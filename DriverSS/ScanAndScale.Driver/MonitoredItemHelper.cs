using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanAndScale.Driver
{
    public static class MonitoredItemHelper
    {
        public static bool GetModeDesign()
        {
            return GetModeDesign(null);
        }
        public static bool GetModeDesign(this Control c)
        {
            if (System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("DesignToolsServer"))
            {
                return true;
            }
            if (System.Diagnostics.Process.GetCurrentProcess().ProcessName == "devenv")
            {
                return true;
            }
            if (c != null && c.IsAncestorSiteInDesignMode)
            {
                return true;
            }
            if (c.Site != null && c.Parent.Site.DesignMode)
            {
                return true;
            }
            return false;

        }
        public static void InvokeIfRequired(Control control, Action action)
        {
            if (control.InvokeRequired)
            {
                control.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        public static bool FindFormActive(Control control)
        {
            var frm = control.FindForm();
            return FindFormActive(frm);

        }

        public static bool FindFormActive(Form frm)
        {
            if (frm == null) return false;

            if (frm == Form.ActiveForm)
                return true;
            if (!frm.IsMdiChild && frm == Form.ActiveForm)
                return true;

            var parentform = frm.ParentForm;
            if (frm.IsMdiChild && parentform != null
                && parentform?.ActiveMdiChild == frm && parentform == Form.ActiveForm)
            {
                // Debug.WriteLine("Form = true");
                return true;

            }
            //Debug.WriteLine("Form = false");
            return false;

        }

        internal static void LogMsgError(Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        public static double ToDouble(object value)
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
            try
            {
                if (value.ToString().Any(char.IsLetter) || string.IsNullOrEmpty(value?.ToString()))
                {
                    return 0;
                }
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0;
            }
            // return 0;
        }
    }
}
