using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace llcom.View.Controls
{
    public partial class ScriptSelectorControl : UserControl
    {
        public static readonly System.Windows.DependencyProperty InterfaceKeyProperty =
            System.Windows.DependencyProperty.Register(
                nameof(InterfaceKey),
                typeof(string),
                typeof(ScriptSelectorControl),
                new PropertyMetadata("Serial"));

        public static readonly System.Windows.DependencyProperty IsSendScriptProperty =
            System.Windows.DependencyProperty.Register(
                nameof(IsSendScript),
                typeof(bool),
                typeof(ScriptSelectorControl),
                new PropertyMetadata(false, (d, e) => ((ScriptSelectorControl)d).RefreshDisplay()));

        public string InterfaceKey
        {
            get => (string)GetValue(InterfaceKeyProperty);
            set => SetValue(InterfaceKeyProperty, value);
        }

        public bool IsSendScript
        {
            get => (bool)GetValue(IsSendScriptProperty);
            set => SetValue(IsSendScriptProperty, value);
        }

        private string ScriptFolder => IsSendScript ? "user_script_send_convert" : "user_script_recv_convert";
        private string DirPath => Tools.Global.ProfilePath + ScriptFolder + "/";

        public ScriptSelectorControl()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshDisplay();
        }

        internal void RefreshSelection()
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            scriptLabelText.Text = IsSendScript
                ? (Application.Current.TryFindResource("SendScriptLabel") as string ?? "Send script")
                : (Application.Current.TryFindResource("RecvScriptLabel") as string ?? "Receive script");
            var list = IsSendScript
                ? Tools.Global.setting.GetSendScriptListForInterface(InterfaceKey)
                : Tools.Global.setting.GetRecvScriptListForInterface(InterfaceKey);
            var summary = list == null || list.Count == 0
                ? (IsSendScript ? (Application.Current.TryFindResource("SendScriptTab") as string ?? "Send") : (Application.Current.TryFindResource("RecvScriptTab") as string ?? "Recv"))
                : string.Join(", ", list.Take(3)) + (list.Count > 3 ? "…" : "");
            scriptSummaryText.Text = $"({list?.Count ?? 0}) {summary}";
        }

        private void ScriptButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLists();
            scriptPopup.IsOpen = true;
        }

        private void LoadLists()
        {
            var selected = IsSendScript
                ? Tools.Global.setting.GetSendScriptListForInterface(InterfaceKey) ?? new List<string>()
                : Tools.Global.setting.GetRecvScriptListForInterface(InterfaceKey) ?? new List<string>();

            availableListBox.Items.Clear();
            selectedListBox.Items.Clear();

            if (!Directory.Exists(DirPath))
                Directory.CreateDirectory(DirPath);

            try
            {
                var dir = new DirectoryInfo(DirPath);
                var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
                foreach (var file in dir.GetFiles("*.lua"))
                {
                    var name = file.Name.Substring(0, file.Name.Length - 4);
                    if (selectedSet.Contains(name))
                        selectedListBox.Items.Add(name);
                    else
                        availableListBox.Items.Add(name);
                }
            }
            catch { }

            UpdateButtonStates();
        }

        private void SaveSelected()
        {
            var list = new List<string>();
            foreach (var item in selectedListBox.Items)
                list.Add(item as string ?? "");
            if (IsSendScript)
                Tools.Global.setting.SetSendScriptListForInterface(InterfaceKey, list);
            else
                Tools.Global.setting.SetRecvScriptListForInterface(InterfaceKey, list);
            RefreshDisplay();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (availableListBox.SelectedItem == null) return;
            var name = availableListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;
            availableListBox.Items.Remove(name);
            selectedListBox.Items.Add(name);
            SaveSelected();
            UpdateButtonStates();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedListBox.SelectedItem == null) return;
            var name = selectedListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;
            selectedListBox.Items.Remove(name);
            availableListBox.Items.Add(name);
            SaveSelected();
            UpdateButtonStates();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            var idx = selectedListBox.SelectedIndex;
            if (idx <= 0) return;
            var item = selectedListBox.Items[idx];
            selectedListBox.Items.RemoveAt(idx);
            selectedListBox.Items.Insert(idx - 1, item);
            selectedListBox.SelectedIndex = idx - 1;
            SaveSelected();
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            var idx = selectedListBox.SelectedIndex;
            if (idx < 0 || idx >= selectedListBox.Items.Count - 1) return;
            var item = selectedListBox.Items[idx];
            selectedListBox.Items.RemoveAt(idx);
            selectedListBox.Items.Insert(idx + 1, item);
            selectedListBox.SelectedIndex = idx + 1;
            SaveSelected();
        }

        private void AvailableListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtonStates();
        private void SelectedListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtonStates();

        private void UpdateButtonStates()
        {
            addButton.IsEnabled = availableListBox.SelectedItem != null;
            removeButton.IsEnabled = selectedListBox.SelectedItem != null;
            upButton.IsEnabled = selectedListBox.SelectedIndex > 0;
            downButton.IsEnabled = selectedListBox.SelectedIndex >= 0 && selectedListBox.SelectedIndex < selectedListBox.Items.Count - 1;
        }
    }
}
