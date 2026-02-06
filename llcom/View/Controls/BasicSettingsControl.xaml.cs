using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace llcom.View.Controls
{
    public partial class BasicSettingsControl : UserControl
    {
        public BasicSettingsControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = Tools.Global.setting;

            dataBitsComboBox.SelectedIndex = Tools.Global.setting.dataBits - 5;
            stopBitComboBox.SelectedIndex = Tools.Global.setting.stopBit - 1;
            dataCheckComboBox.SelectedIndex = Tools.Global.setting.parity;
            showHexComboBox.DataContext = Tools.Global.setting;

            var el = Encoding.GetEncodings();
            var encodingList = new List<EncodingInfo>(el);
            encodingList.Sort((x, y) => x.CodePage - y.CodePage);
            foreach (var en in encodingList)
            {
                var c = new ComboBoxItem
                {
                    Content = $"[{en.CodePage}] {en.Name}",
                    Tag = en.CodePage
                };
                int index = encodingComboBox.Items.Add(c);
                if (Tools.Global.setting.encoding == en.CodePage)
                    encodingComboBox.SelectedIndex = index;
            }
        }

        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", Tools.Global.GetTrueProfilePath() + "logs");
            }
            catch
            {
                Tools.MessageBox.Show($"尝试打开文件夹失败，请自行打开该路径：{Tools.Global.GetTrueProfilePath()}logs");
            }
        }

        private void DataBitsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataBitsComboBox.SelectedItem != null)
                Tools.Global.setting.dataBits = dataBitsComboBox.SelectedIndex + 5;
        }

        private void StopBitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (stopBitComboBox.SelectedItem != null)
                Tools.Global.setting.stopBit = stopBitComboBox.SelectedIndex + 1;
        }

        private void DataCheckComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataCheckComboBox.SelectedItem != null)
                Tools.Global.setting.parity = dataCheckComboBox.SelectedIndex;
        }

        private void encodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var c = sender as ComboBox;
            if (c?.SelectedItem == null) return;
            if ((int)((ComboBoxItem)c.SelectedItem).Tag == Tools.Global.setting.encoding)
                return;
            Tools.Global.setting.encoding = (int)((ComboBoxItem)c.SelectedItem).Tag;
        }
    }
}
