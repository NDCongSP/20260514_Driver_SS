using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ScanAndScale.Helper;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace ScanAndScale.Driver
{

    public class ScaleButtonEdit : DevExpress.XtraEditors.ButtonEdit
    {
        //  public ScaleConfig config { get; set; } = new ScaleConfig();

        private ScaleConfig _Config;

        public ScaleConfig Config
        {
            get { return _Config; }
            set
            {
                var oldValue = _Config;
                _Config = value;

                OnFulltagChanged(new ValueChangedEventArgs(oldValue, value));
            }
        }

        private bool _enableReadScale = true;
        public bool EnableReadScale
        {
            get { return _enableReadScale; }
            set
            {
                if (_enableReadScale != value)
                    _enableReadScale = value;
            }
        }

        public event EventHandler<DataValueChangedEventArgs> DataValueChanged;
        public event EventHandler<ValueChangedEventArgs> PropertyChanged;
        public bool AutoDetectUnit { get; set; } = false;

        public bool Stable { get; set; } = true;
        public bool Tare { get; set; } = false;

        public double ValueTon { get; set; }
        public double ValueKg { get; set; }
        public double ValueGram { get; set; }

        #region Constructor
        public ScaleButtonEdit()
        {
            if (MonitoredItemHelper.GetModeDesign(this)) return;
            this.ButtonClick += BtnEdit1_ButtonClick;
            //CreatMonitorTag();

        }

        protected override void OnLoaded()
        {
            if (MonitoredItemHelper.GetModeDesign(this)) return;
            //CreatMonitorTag();

            base.OnLoaded();

            this.HandleCreated += (sender, e) =>
            {
                this.FindForm().FormClosed += ScaleButtonEdit_FormClosed;
            };
            this.Properties.Appearance.Options.UseForeColor = true;
        }

        private void BtnEdit1_ButtonClick(object sender, ButtonPressedEventArgs e)
        {
            ButtonEdit editor = sender as ButtonEdit;
            if (e.Button.Kind == ButtonPredefines.Ellipsis)
            {
                //frmScaleConfig frm = new frmScaleConfig();
                //frm.Show();
            }
            if (e.Button.Kind == ButtonPredefines.OK)
            {
                //...
            }
        }

        #endregion

        #region Property
        private ScaleMonitoredItem _innerMonitoredItem;
        [Browsable(false)]
        internal ScaleMonitoredItem InnerMonitoredItem
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

        #region DependencyProperty

        [Category("zWeightSetting")]
        private EmnumUnitType _UnitType = EmnumUnitType.Kg;
        public EmnumUnitType UnitType
        {
            get { return _UnitType; }
            set
            {

                if (AutoDetectUnit)
                {
                    if (_innerMonitoredItem?.Unit == "KG")
                    {
                        _UnitType = EmnumUnitType.Kg;
                    }
                    else if (_innerMonitoredItem?.Unit == "G")
                    {
                        _UnitType = EmnumUnitType.gr;
                    }
                    else if (_innerMonitoredItem?.Unit == "TON")
                    {
                        _UnitType = EmnumUnitType.Ton;
                    }
                    else
                    {
                        if (_UnitType != value)
                        {
                            _UnitType = value;
                        }
                    }
                }
                else
                {
                    if (_UnitType != value)
                    {
                        _UnitType = value;
                    }
                }
            }
        }

        private int _decimalNum = 4;
        public int DecimalNum
        {
            get { return _decimalNum; }
            set
            {
                if (_decimalNum != value)
                {
                    _decimalNum = value;
                    if (Config != null)
                    {
                        Config.DecimalNum = value;
                        OnFulltagChanged(new ValueChangedEventArgs(_Config, Config));
                    }
                }
            }
        }

        private double _bagWeight = 0;
        public double BagWeight
        {
            get { return _bagWeight; }
            set
            {
                if (_bagWeight != value)
                {
                    _bagWeight = value;
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
        private void ScaleButtonEdit_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!MonitoredItemHelper.GetModeDesign(this))
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
                CreatMonitorTag((ScaleConfig)e.NewValue, (ScaleConfig)e.OldValue);
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
            try
            {
                //  if (Focused) return;
                MonitoredItemHelper.InvokeIfRequired(this, () =>
                {
                    if (!MonitoredItemHelper.FindFormActive(this)) return;
                    if (this.Visible == false) return;

                    switch (_UnitType)
                    {
                        case EmnumUnitType.Kg:
                            e.NewValue.Value = e.NewValue.Value;
                            break;
                        case EmnumUnitType.gr:
                            e.NewValue.Value = (double?)e.NewValue.Value * 1000;
                            break;
                        case EmnumUnitType.Ton:
                            e.NewValue.Value = (double?)e.NewValue.Value / 1000;
                            break;
                        default:
                            e.NewValue.Value = e.NewValue.Value;
                            break;
                    }


                    if (_Config.CheckStable == true)
                        Stable = _innerMonitoredItem?.Stable == true;
                    else
                        Stable = true; //Nếu không kiểm tra thì nó bằng true luôn


                    if (_Config.CheckTare == true)
                        Tare = _innerMonitoredItem?.Tare == true;
                    else
                        Tare = false; //Nếu không kiểm tra thì nó bằng true luôn


                    this.Properties.Appearance.Options.UseForeColor = true;

                    if (Stable == true)
                        this.Properties.Appearance.ForeColor = System.Drawing.Color.Green;
                    else
                        this.Properties.Appearance.ForeColor = System.Drawing.Color.Red;


                    if (Tare == true)
                    {
                        this.Properties.Appearance.Options.UseBackColor = true;
                        this.Properties.Appearance.BackColor = System.Drawing.Color.LightPink;
                    }
                    else
                    {
                        this.Properties.Appearance.Options.UseBackColor = false;
                    }

                    if (_enableReadScale)
                    {
                        RefeshData(sender, e.NewValue);
                        DataValueChanged?.Invoke(this, e);
                    }
                });

            }
            catch (Exception ex)
            {

                MonitoredItemHelper.LogMsgError(ex);
            }

        }

        public void RefeshData(object sender, DataValue e)
        {
            Debug.WriteLine($"RefeshData {e.Value}");

            if (InnerMonitoredItem == null || e == null) return;
            try
            {

                MonitoredItemHelper.InvokeIfRequired(this, () =>
                {
                    if (e.DriverStatus == DriverStatus.Connected)
                    {
                        //EditValue = e.Value;
                        //EditValue = Math.Round(double.TryParse(((double)e.Value - _bagWeight).ToString(), out double value) ? value : 0, _decimalNum);

                        EditValue = Math.Round((double)e.Value - _bagWeight, _decimalNum);
                    }
                    else
                    {
                        EditValue = null;
                        //EditValue = e.DriverStatus.ToString();
                    }


                    switch (_UnitType)
                    {
                        case EmnumUnitType.Kg:
                            ValueKg = MonitoredItemHelper.ToDouble(e.Value);
                            ValueGram = ValueKg * 1000;
                            ValueTon = ValueKg * 0.001;
                            break;
                        case EmnumUnitType.gr:
                            ValueGram = MonitoredItemHelper.ToDouble(e.Value);
                            ValueKg = ValueGram * 0.001;
                            ValueTon = ValueKg * 0.001;
                            break;
                        case EmnumUnitType.Ton:
                            ValueTon = MonitoredItemHelper.ToDouble(e.Value);
                            ValueKg = ValueTon * 1000;
                            ValueGram = ValueKg * 1000;
                            break;
                        default:
                            ValueKg = MonitoredItemHelper.ToDouble(e.Value);
                            ValueGram = ValueKg * 1000;
                            ValueTon = ValueKg * 0.001;
                            break;
                    }

                });
            }
            catch (Exception ex)
            {

                MonitoredItemHelper.LogMsgError(ex);
            }
        }

        private void CreatMonitorTag(ScaleConfig config, ScaleConfig oldconfig)
        {
            if (MonitoredItemHelper.GetModeDesign(this)) return;
            if (config == null) return;

            if (_innerMonitoredItem != null)
            {
                _innerMonitoredItem.DataValueChanged -= MonitoredItem_DataValueChanged;
            }
            ReadOnly = config.ReadOnly == true;

            //if (!string.IsNullOrEmpty(config?.ToString()))
            {
                _innerMonitoredItem = null;
                _innerMonitoredItem = new ScaleMonitoredItem(
                    config.Enable
                    , config.IP
                    , config.Port
                    , config.TimeScan
                    , config.CalibZero
                    , config.CalibGain
                    , config.ModelName
                    , config.DecimalNum
                    );
                _innerMonitoredItem.CheckConnect();

                if (_innerMonitoredItem != null)
                {
                    _innerMonitoredItem.DataValueChanged += MonitoredItem_DataValueChanged;
                    if (_innerMonitoredItem.DataValue != null && _innerMonitoredItem.DataValue != null)
                    {
                        RefeshData(_innerMonitoredItem, _innerMonitoredItem.DataValue);
                        base.ToolTip = config.IP;
                    }
                }
            }
        }

        #endregion



        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DataValueChanged?.Invoke(this, new DataValueChangedEventArgs(_innerMonitoredItem.DataValue, new DataValue(DriverStatus.Unknown, EditValue)));
            }

            base.OnKeyDown(e);
        }

    }
    public enum EmnumUnitType
    {
        Kg,
        gr,
        Ton,
    }
}
