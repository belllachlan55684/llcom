using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace llcom.Model
{
    /// <summary>
    /// 各数据接口页的显示配置代理，将 Get/Set*ForInterface 暴露为可绑定属性
    /// </summary>
    public class InterfaceConfigProxy : INotifyPropertyChanged
    {
        internal string InterfaceKey => _interfaceKey;

        private readonly string _interfaceKey;

        public InterfaceConfigProxy(string interfaceKey)
        {
            _interfaceKey = interfaceKey ?? "Serial";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public bool ShowSend
        {
            get => Tools.Global.setting.GetShowSendForInterface(_interfaceKey);
            set { Tools.Global.setting.SetShowSendForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public bool ShowSendRaw
        {
            get => Tools.Global.setting.GetShowSendRawForInterface(_interfaceKey);
            set { Tools.Global.setting.SetShowSendRawForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public string DataToSend
        {
            get => Tools.Global.setting.GetDataToSendForInterface(_interfaceKey);
            set { Tools.Global.setting.SetDataToSendForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public int ShowHexFormat
        {
            get => Tools.Global.setting.GetShowHexFormatForInterface(_interfaceKey);
            set { Tools.Global.setting.SetShowHexFormatForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public bool EnableSymbol
        {
            get => Tools.Global.setting.GetEnableSymbolForInterface(_interfaceKey);
            set { Tools.Global.setting.SetEnableSymbolForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public bool DisableLog
        {
            get => Tools.Global.setting.GetDisableLogForInterface(_interfaceKey);
            set { Tools.Global.setting.SetDisableLogForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        public bool HexMode
        {
            get => Tools.Global.setting.GetHexForInterface(_interfaceKey);
            set { Tools.Global.setting.SetHexForInterface(_interfaceKey, value); OnPropertyChanged(); }
        }

        /// <summary>
        /// 通知界面刷新（切换接口后调用）
        /// </summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(ShowSend));
            OnPropertyChanged(nameof(ShowSendRaw));
            OnPropertyChanged(nameof(DataToSend));
            OnPropertyChanged(nameof(ShowHexFormat));
            OnPropertyChanged(nameof(EnableSymbol));
            OnPropertyChanged(nameof(DisableLog));
            OnPropertyChanged(nameof(HexMode));
        }
    }
}
