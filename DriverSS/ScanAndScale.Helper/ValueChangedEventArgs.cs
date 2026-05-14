namespace ScanAndScale.Helper
{
    public class ValueChangedEventArgs : EventArgs
    {
        public object OldValue { get; }
        public object NewValue { get; }

        public ValueChangedEventArgs(object oldValue, object newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class DataValueChangedEventArgs : EventArgs
    {
        public DataValue OldValue { get; }
        public DataValue NewValue { get; }

        public DataValueChangedEventArgs(DataValue oldValue, DataValue newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
    public class DataValue
    {
        public DriverStatus DriverStatus { get; set; }
        public object Value { get; set; }

        public DataValue(DriverStatus statusIsGood, object value)
        {
            DriverStatus = statusIsGood;
            Value = value;
        }
    }
    public enum DriverStatus
    {
        Unknown = 0,
        Connected = 1,
        Disconnected = 2,
        Reconnecting = 3,

    }
    public class ValueChangedEventArgs<T> : EventArgs
    {
        private T _NewValue;

        private T _OldValue;

        public T NewValue => _NewValue;

        public T OldValue => _OldValue;

        public ValueChangedEventArgs(T oldValue, T newValue)
        {
            _OldValue = oldValue;
            _NewValue = newValue;
        }
    }
}
