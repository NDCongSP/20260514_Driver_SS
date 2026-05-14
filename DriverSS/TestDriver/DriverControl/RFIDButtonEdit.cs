using DevExpress.Images;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ScanAndScale.Driver;
using ScanAndScale.Helper;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ScanAndScale.Driver
{
    public class RFIDButtonEdit : DevExpress.XtraEditors.ButtonEdit
    {

        private RfidConfig _Config;

        public RfidConfig Config
        {
            get { return _Config; }
            set
            {
                var oldValue = _Config;
                _Config = value;
                OnFulltagChanged(new ValueChangedEventArgs(oldValue, value));
            }
        }

        #region Constructor
        public RFIDButtonEdit() : base()
        {
            this.ButtonClick += BtnEdit1_ButtonClick;
            this.Properties.Buttons.Add(new EditorButton(ButtonPredefines.Search));
            this.Properties.Buttons[1].Visible = true;
            this.Properties.Buttons.Add(new EditorButton(ButtonPredefines.Delete));
            this.Properties.Buttons[2].Visible = true;

            this.Properties.NullValuePrompt = "Vui lòng quét thẻ nhân viên";

            if (Common.GetModeDesign(this))
            {
                return;
            }
        }
        protected override void OnLoaded()
        {
            if (Common.GetModeDesign(this)) return;
            // CreatMonitorTag();
            base.OnLoaded();
            base.FindForm().FormClosed += ButtonEditRFID_FormClosed;
        }
        private void BtnEdit1_ButtonClick(object sender, ButtonPressedEventArgs e)
        {
            if (Common.GetModeDesign(this)) return;
            if (_Config == null) return;



            ButtonEdit editor = sender as ButtonEdit;
            if (e.Button.Kind == ButtonPredefines.Search)
            {
                if (_innerMonitoredItem != null)
                {
                    //Reconnect
                    _innerMonitoredItem.SerialportOpen(_Config.Enable, _Config.Rfid_Com, _Config.Rfid_AutoFindCom, true);
                }
            }
            if (e.Button.Kind == ButtonPredefines.Delete)
            {
                if (_innerMonitoredItem != null)
                {
                    // _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
                    _innerMonitoredItem.Dispose(true);

                }
            }




        }
        #endregion

        #region Property
        private RFIDMonitoredItem _innerMonitoredItem;
        [Browsable(false)]
        internal RFIDMonitoredItem InnerMonitoredItem
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
            if (!Common.GetModeDesign(this))
            {
                // config.OperatorAccessChanged -= Common_OperatorAccessChanged;
                if (_innerMonitoredItem != null)
                {
                    _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
                    _innerMonitoredItem.Dispose();

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
                CreatMonitorTag((RfidConfig)e.NewValue, (RfidConfig)e.OldValue);
            }
            catch (Exception ex)
            {

                Common.LogMsgError(ex);
            }
        }
        /// <summary>
        /// khi tag thay đổi thì gọi sự kiện này
        /// </summary>
        /// <param name="sender" is  MonitoredItem></param>
        /// <param name="e"></param>
        private void MonitoredItem_DataValueChanged(object sender, DataValueChangedEventArgs e)
        {
            try
            {
                Common.InvokeIfRequired(this, () =>
                {
                    if (!Common.FindFormActive(this)) return;
                    if (this.Visible == false) return;

                    RefeshData(sender, e.NewValue);
                    DataValueChanged?.Invoke(this, e);
                });
            }
            catch (Exception ex)
            {

                Common.LogMsgError(ex);
            }

        }

        public void RefeshData(object sender, DataValue e)
        {
            // Debug.WriteLine($"RefeshData {e.Value}");

            if (InnerMonitoredItem == null || e == null) return;
            try
            {

                Common.InvokeIfRequired(this, () =>
                {
                    if (e.DriverStatus == DriverStatus.Connected)
                    {
                        EditValue = e.Value;
                    }
                    else
                    {
                        EditValue = null;
                        //Text = e.DriverStatus.ToString();
                    }

                });
            }
            catch (Exception ex)
            {

                Common.LogMsgError(ex);
            }
        }

        private void CreatMonitorTag(RfidConfig config, RfidConfig oldconfig)
        {
            if (Common.GetModeDesign(this)) return;

            if (config == null)
            {
                return;
            }
            if (_innerMonitoredItem != null)
            {
                _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
            }
            ReadOnly = config.ReadOnly == true;
            _innerMonitoredItem = RFIDMonitoredItem.Instance;
            _innerMonitoredItem.SerialportOpen(config.Enable, config.Rfid_Com, config.Rfid_AutoFindCom);

            if (_innerMonitoredItem != null)
            {
                _innerMonitoredItem.DataValueChanged += MonitoredItem_DataValueChanged;
                if (_innerMonitoredItem.DataValue != null)
                {
                    // RefeshData(_innerMonitoredItem, _innerMonitoredItem.DataValue);
                    base.ToolTip = _innerMonitoredItem.ComPort.ToString();
                }
            }
        }

        #endregion

        public event EventHandler<ValueChangedEventArgs> PropertyChanged;
        public event EventHandler<DataValueChangedEventArgs> DataValueChanged;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DataValueChanged?.Invoke(this, new DataValueChangedEventArgs(_innerMonitoredItem.DataValue, new DataValue(DriverStatus.Unknown, EditValue)));

            }

            base.OnKeyDown(e);
        }
    }
}
