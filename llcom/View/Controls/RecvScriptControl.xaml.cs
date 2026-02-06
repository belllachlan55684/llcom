using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace llcom.View.Controls
{
    public partial class RecvScriptControl : UserControl
    {
        private static bool fileLoadingRev;
        private static string lastLuaFileRev = "";

        public RecvScriptControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = Tools.Global.setting;
            SearchPanel.Install(textEditorRev.TextArea);
            ApplyScriptEditorTheme(Tools.Global.setting.darkMode);
            Tools.Global.ThemeChanged += (_, dark) => Dispatcher.Invoke(() => ApplyScriptEditorTheme(dark));
            if (!string.IsNullOrEmpty(MainWindow.recvScriptBackup))
                loadLuaFileRev(MainWindow.recvScriptBackup);
            else
                loadLuaFileRev(Tools.Global.setting.recvScript);
        }

        private void loadLuaFileRev(string fileName)
        {
            if (!File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{fileName}.lua"))
            {
                Tools.Global.setting.recvScript = "default";
                if (!File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{Tools.Global.setting.recvScript}.lua"))
                    File.Create(Tools.Global.ProfilePath + $"user_script_recv_convert/{Tools.Global.setting.recvScript}.lua").Close();
            }
            else
                Tools.Global.setting.recvScript = fileName;

            textEditorRev.Text = File.ReadAllText(Tools.Global.ProfilePath + $"user_script_recv_convert/{Tools.Global.setting.recvScript}.lua");

            var luaFileDir = new DirectoryInfo(Tools.Global.ProfilePath + "user_script_recv_convert/");
            var luaFiles = luaFileDir.GetFileSystemInfos();
            fileLoadingRev = true;
            luaFileListRev.Items.Clear();
            for (int i = 0; i < luaFiles.Length; i++)
            {
                var file = luaFiles[i] as FileInfo;
                if (file != null && file.Name.EndsWith(".lua"))
                {
                    string name = file.Name.Substring(0, file.Name.Length - 4);
                    luaFileListRev.Items.Add(name);
                    if (name == Tools.Global.setting.recvScript)
                        luaFileListRev.SelectedIndex = luaFileListRev.Items.Count - 1;
                }
            }
            lastLuaFileRev = Tools.Global.setting.recvScript;
            fileLoadingRev = false;
            LuaEnv.LuaLoader.ClearRun();
        }

        private void saveLuaFileRev(string fileName)
        {
            File.WriteAllText(Tools.Global.ProfilePath + $"user_script_recv_convert/{fileName}.lua", textEditorRev.Text);
            LuaEnv.LuaLoader.ClearRun();
        }

        private void ApplyScriptEditorTheme(bool darkMode)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var name = asm.GetName().Name + (darkMode ? ".Lua-dark.xshd" : ".Lua.xshd");
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s != null)
                {
                    using (var reader = new XmlTextReader(s))
                    {
                        var xshd = HighlightingLoader.LoadXshd(reader);
                        var highlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                        textEditorRev.SyntaxHighlighting = highlighting;
                    }
                }
            }
        }

        private void ApiDocumentButton_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(Tools.Global.apiDocumentUrl);

        private void openScriptFolderButtonRev_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start("explorer.exe", Tools.Global.GetTrueProfilePath() + "user_script_recv_convert");

        private void luaFileListRev_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (luaFileListRev.SelectedItem != null && !fileLoadingRev)
            {
                if (lastLuaFileRev != "")
                    saveLuaFileRev(lastLuaFileRev);
                loadLuaFileRev(luaFileListRev.SelectedItem as string);
                MainWindow.recvScriptBackup = luaFileListRev.SelectedItem as string;
            }
        }

        private void newScriptButtonRev_Click(object sender, RoutedEventArgs e)
        {
            luaTestWrapPanelRev.Visibility = Visibility.Collapsed;
            newLuaFileWrapPanelRev.Visibility = Visibility.Visible;
        }

        private void testScriptButtonRev_Click(object sender, RoutedEventArgs e)
        {
            newLuaFileWrapPanelRev.Visibility = Visibility.Collapsed;
            luaTestWrapPanelRev.Visibility = Visibility.Visible;
        }

        private void newLuaFilebuttonRev_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newLuaFileNameTextBoxRev.Text))
            {
                Tools.MessageBox.Show(TryFindResource("LuaNoName") as string ?? "?!");
                return;
            }
            if (File.Exists(Tools.Global.ProfilePath + $"user_script_recv_convert/{newLuaFileNameTextBoxRev.Text}.lua"))
            {
                Tools.MessageBox.Show(TryFindResource("LuaExist") as string ?? "?!");
                return;
            }
            try
            {
                File.Create(Tools.Global.ProfilePath + $"user_script_recv_convert/{newLuaFileNameTextBoxRev.Text}.lua").Close();
                loadLuaFileRev(newLuaFileNameTextBoxRev.Text);
            }
            catch
            {
                Tools.MessageBox.Show(TryFindResource("LuaCreateFail") as string ?? "?!");
                return;
            }
            newLuaFileWrapPanelRev.Visibility = Visibility.Collapsed;
        }

        private void newLuaFileCancelbuttonRev_Click(object sender, RoutedEventArgs e) =>
            newLuaFileWrapPanelRev.Visibility = Visibility.Collapsed;

        private void luaTestbuttonRev_Click(object sender, RoutedEventArgs e)
        {
            if (luaFileListRev.SelectedItem == null || fileLoadingRev) return;
            try
            {
                var r = LuaEnv.LuaLoader.Run(
                    $"{luaFileListRev.SelectedItem as string}.lua",
                    new System.Collections.ArrayList {
                        "uartData",
                        (bool)(luaTestHexCheckRev?.IsChecked ?? false) ?
                            Tools.Global.Hex2Byte(luaTestTextBoxRev.Text) :
                            Tools.Global.GetEncoding().GetBytes(luaTestTextBoxRev.Text),
                        "uartPara",
                        Tools.Global.GetEncoding().GetBytes(luaTestParaTextBoxRev.Text)
                    },
                    "user_script_recv_convert/");
                Tools.MessageBox.Show($"{TryFindResource("SettingLuaRunResult") as string ?? "?!"}\r\nHEX：" + Tools.Global.Byte2Hex(r) +
                    $"\r\n{TryFindResource("SettingLuaRawText") as string ?? "?!"}" + Tools.Global.Byte2Readable(r));
            }
            catch (Exception ex)
            {
                Tools.MessageBox.Show($"{TryFindResource("ErrorScript") as string ?? "?!"}\r\n" + ex.ToString());
            }
        }

        private void luaTestCancelbuttonRev_Click(object sender, RoutedEventArgs e) =>
            luaTestWrapPanelRev.Visibility = Visibility.Collapsed;

        private void textEditorRev_LostFocus(object sender, RoutedEventArgs e)
        {
            if (lastLuaFileRev != "")
                saveLuaFileRev(lastLuaFileRev);
        }

        internal void SaveOnUnload()
        {
            if (lastLuaFileRev != "")
                saveLuaFileRev(lastLuaFileRev);
        }
    }
}
