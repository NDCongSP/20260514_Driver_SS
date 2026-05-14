using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraReports.UI;
using ScanAndScale.Driver;
using ScanAndScale.Helper;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TestDriver
{
    public class BarcodeButtonEdit : DevExpress.XtraEditors.ButtonEdit
    {

        // public BarcodeConfig config { get; set; } = new BarcodeConfig();

        private BarcodeConfig _Config;

        public BarcodeConfig Config
        {
            get { return _Config; }
            set
            {
                var oldValue = _Config;
                _Config = value;
                OnFulltagChanged(new ValueChangedEventArgs(oldValue, value));
            }
        }


        public event EventHandler<ValueChangedEventArgs> PropertyChanged;
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged;

        #region Constructor
        public BarcodeButtonEdit() : base()
        {
            this.ButtonClick += BtnEdit1_ButtonClick;

            if (MonitoredItemHelper.GetModeDesign(this))
            {
                return;
            }
            this.Properties.Buttons[0].Kind = ButtonPredefines.Ellipsis;
            this.Properties.Buttons.Add(new EditorButton(ButtonPredefines.OK));
            this.Properties.Buttons[0].Visible = false;
            this.Properties.Buttons[1].Visible = false;

            this.Properties.NullValuePrompt = "Vui lòng quét QR/Barcode";

            //CreatMonitorTag();
        }

        protected override void OnLoaded()
        {
            if (MonitoredItemHelper.GetModeDesign(this)) return;
            // CreatMonitorTag();
            base.OnLoaded();
            base.FindForm().FormClosed += ButtonEditRFID_FormClosed;
        }

        private void BtnEdit1_ButtonClick(object sender, ButtonPressedEventArgs e)
        {
            ButtonEdit editor = sender as ButtonEdit;
            if (e.Button.Kind == ButtonPredefines.Ellipsis)
            {
                //...
            }
            if (e.Button.Kind == ButtonPredefines.OK)
            {
                //...
            }
        }

        #endregion

        #region Property
        private BarcodeMonitoredItem _innerMonitoredItem;
        [Browsable(false)]
        internal BarcodeMonitoredItem InnerMonitoredItem
        {
            get => _innerMonitoredItem;
            set
            {
                if (_innerMonitoredItem != value)
                {
                    _innerMonitoredItem = value;
                }
            }
        }

        #endregion

        #region Event
        /// <summary>
        /// Bỏ theo dõi tag khỏi subcription khi unload
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonEditRFID_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!MonitoredItemHelper.GetModeDesign(this))
            {
                if (_innerMonitoredItem != null)
                {
                    _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
                    //_innerMonitoredItem.Dispose();
                }
            }

        }

        /// <summary>
        /// Khi tag bị thay đổi thì xóa tag đó khỏi subcription và đăng kí mới
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnFulltagChanged(ValueChangedEventArgs e)
        {
            try
            {
                PropertyChanged?.Invoke(this, e);
                CreatMonitorTag((BarcodeConfig)e.NewValue, (BarcodeConfig)e.OldValue);
            }
            catch (Exception ex)
            {
                MonitoredItemHelper.LogMsgError(ex);
            }
        }
        /// <summary>
        /// khi tag thay đổi thì gọi sự kiện này
        /// </summary>
        /// <param name="sender" is  MonitoredItem></param>
        /// <param name="e"></param>
        private void MonitoredItem_DataValueChanged(object sender, DataValueChangedEventArgs e)
        {
            //var frm = this.FindForm();
            //if (frm.IsMdiChild && frm._ParentForm.ActiveMdiChild != frm)
            //    return;
            //if (!frm.IsMdiChild && frm != Form.ActiveForm)
            //    return;
            // Debug.WriteLine($"Form {this?.FindForm()?.Text}: {e?.NewValue.Value}");
            if (!MonitoredItemHelper.FindFormActive(this)) return;
            if (this.Visible == false) return;

            try
            {
                //  if (Focused) return;
                MonitoredItemHelper.InvokeIfRequired(this, () =>
                {
                    RefeshData(sender, e.NewValue);
                    DataValueChanged?.Invoke(this, e);

                });

            }
            catch (Exception ex)
            {

                MonitoredItemHelper.LogMsgError(ex);
            }

        }

        public void RefeshData(object sender, DataValue e)
        {
            // Debug.WriteLine($"RefeshData {e.Value}");

            if (InnerMonitoredItem == null || e == null) return;
            if (_Config == null) return;

            try
            {

                MonitoredItemHelper.InvokeIfRequired(this, () =>
                {
                    if (e.DriverStatus == DriverStatus.Connected)
                    {
                        EditValue = e.Value;
                    }
                    else
                    {
                        EditValue = null;
                        // Text = e.DriverStatus.ToString();
                    }

                });
            }
            catch (Exception ex)
            {

                MonitoredItemHelper.LogMsgError(ex);
            }
        }

        private void CreatMonitorTag(BarcodeConfig config, BarcodeConfig oldconfig)
        {
            if (MonitoredItemHelper.GetModeDesign(this)) return;
            if (config == null) return;

            if (_innerMonitoredItem != null)
            {
                _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
            }


            if (config.Enable != true)
                _innerMonitoredItem = null;
            else
                _innerMonitoredItem = BarcodeMonitoredItem.Instance;


            if (_innerMonitoredItem != null)
            {
                ReadOnly = config.ReadOnly == true;
                _innerMonitoredItem.DataValueChanged += MonitoredItem_DataValueChanged;
                if (_innerMonitoredItem.DataValue != null && _innerMonitoredItem.DataValue != null)
                {
                    //RefeshData(_innerMonitoredItem, _innerMonitoredItem.DataValue);
                    // base.ToolTip = _innerMonitoredItem.ComPort.ToString();
                }
            }

        }

        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                MonitoredItemHelper.InvokeIfRequired(this, () =>
                {
                    DataValueChanged?.Invoke(this, new DataValueChangedEventArgs(_innerMonitoredItem?.DataValue, new DataValue(DriverStatus.Unknown, EditValue)));
                });
            }

            base.OnKeyDown(e);
        }
        public void ReLoadDataValueChanged()
        {
            MonitoredItemHelper.InvokeIfRequired(this, () =>
            {
                DataValueChanged?.Invoke(this, new DataValueChangedEventArgs(_innerMonitoredItem?.DataValue, new DataValue(DriverStatus.Unknown, EditValue)));
            });

        }
    }
}
