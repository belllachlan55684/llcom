using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace llcom.View.Controls
{
    public partial class TxRxColorBlocks : UserControl
    {
        public static readonly DependencyProperty InterfaceKeyProperty =
            DependencyProperty.Register(
                nameof(InterfaceKey),
                typeof(string),
                typeof(TxRxColorBlocks),
                new PropertyMetadata("Serial", (d, e) => ((TxRxColorBlocks)d).RefreshColors()));

        public string InterfaceKey
        {
            get => (string)GetValue(InterfaceKeyProperty);
            set => SetValue(InterfaceKeyProperty, value);
        }

        public TxRxColorBlocks()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshColors();
        }

        public void RefreshColors()
        {
            if (sendColorBlock == null || recvColorBlock == null) return;
            sendColorBlock.Background = Tools.Global.setting.GetSendDisplayBrushForInterface(InterfaceKey);
            recvColorBlock.Background = Tools.Global.setting.GetRecvDisplayBrushForInterface(InterfaceKey);
        }

        private void SendColorBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            using (var dlg = new WinForms.ColorDialog())
            {
                try { dlg.Color = ColorTranslator.FromHtml(Tools.Global.setting.GetSendDisplayColorForInterface(InterfaceKey)); }
                catch { }
                dlg.FullOpen = true;
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    Tools.Global.setting.SetSendDisplayColorForInterface(InterfaceKey, $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
                    RefreshColors();
                    (Application.Current.MainWindow as MainWindow)?.RefreshDataInterfaceStatus();
                }
            }
        }

        private void RecvColorBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            using (var dlg = new WinForms.ColorDialog())
            {
                try { dlg.Color = ColorTranslator.FromHtml(Tools.Global.setting.GetRecvDisplayColorForInterface(InterfaceKey)); }
                catch { }
                dlg.FullOpen = true;
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    Tools.Global.setting.SetRecvDisplayColorForInterface(InterfaceKey, $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
                    RefreshColors();
                    (Application.Current.MainWindow as MainWindow)?.RefreshDataInterfaceStatus();
                }
            }
        }
    }
}
