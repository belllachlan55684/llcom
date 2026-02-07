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
    public partial class SendScriptControl : UserControl
    {
        private static bool fileLoading;
        private static string lastLuaFile = "";

        public SendScriptControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = Tools.Global.setting;
            SearchPanel.Install(textEditor.TextArea);
            ApplyScriptEditorTheme(Tools.Global.setting.darkMode);
            Tools.Global.ThemeChanged += (_, dark) => Dispatcher.Invoke(() => ApplyScriptEditorTheme(dark));
            loadLuaFile(Tools.Global.setting.sendScript);
        }

        private void loadLuaFile(string fileName)
        {
            if (!File.Exists(Tools.Global.ProfilePath + $"user_script_send_convert/{fileName}.lua"))
            {
                Tools.Global.setting.sendScript = Tools.Global.GetDefaultScriptName();
                if (!File.Exists(Tools.Global.ProfilePath + $"user_script_send_convert/{Tools.Global.setting.sendScript}.lua"))
                    File.Create(Tools.Global.ProfilePath + $"user_script_send_convert/{Tools.Global.setting.sendScript}.lua").Close();
            }
            else
                Tools.Global.setting.sendScript = fileName;

            textEditor.Text = File.ReadAllText(Tools.Global.ProfilePath + $"user_script_send_convert/{Tools.Global.setting.sendScript}.lua");

            var luaFileDir = new DirectoryInfo(Tools.Global.ProfilePath + "user_script_send_convert/");
            var luaFiles = luaFileDir.GetFileSystemInfos();
            fileLoading = true;
            luaFileList.Items.Clear();
            for (int i = 0; i < luaFiles.Length; i++)
            {
                var file = luaFiles[i] as FileInfo;
                if (file != null && file.Name.EndsWith(".lua"))
                {
                    string name = file.Name.Substring(0, file.Name.Length - 4);
                    luaFileList.Items.Add(name);
                    if (name == Tools.Global.setting.sendScript)
                        luaFileList.SelectedIndex = luaFileList.Items.Count - 1;
                }
            }
            lastLuaFile = Tools.Global.setting.sendScript;
            fileLoading = false;
            LuaEnv.LuaLoader.ClearRun();
        }

        private void saveLuaFile(string fileName)
        {
            File.WriteAllText(Tools.Global.ProfilePath + $"user_script_send_convert/{fileName}.lua", textEditor.Text);
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
                        textEditor.SyntaxHighlighting = highlighting;
                    }
                }
            }
        }

        private void ApiDocumentButton_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(Tools.Global.apiDocumentUrl);

        private void OpenScriptFolderButton_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start("explorer.exe", Tools.Global.GetTrueProfilePath() + "user_script_send_convert");

        private void NewScriptButton_Click(object sender, RoutedEventArgs e)
        {
            luaTestWrapPanel.Visibility = Visibility.Collapsed;
            newLuaFileWrapPanel.Visibility = Visibility.Visible;
        }

        private void TestScriptButton_Click(object sender, RoutedEventArgs e)
        {
            newLuaFileWrapPanel.Visibility = Visibility.Collapsed;
            luaTestWrapPanel.Visibility = Visibility.Visible;
        }

        private void LuaFileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (luaFileList.SelectedItem != null && !fileLoading)
            {
                if (lastLuaFile != "")
                    saveLuaFile(lastLuaFile);
                loadLuaFile(luaFileList.SelectedItem as string);
            }
        }

        private void NewLuaFileCancelbutton_Click(object sender, RoutedEventArgs e) =>
            newLuaFileWrapPanel.Visibility = Visibility.Collapsed;

        private void NewLuaFilebutton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newLuaFileNameTextBox.Text))
            {
                Tools.MessageBox.Show(TryFindResource("LuaNoName") as string ?? "?!");
                return;
            }
            if (File.Exists(Tools.Global.ProfilePath + $"user_script_send_convert/{newLuaFileNameTextBox.Text}.lua"))
            {
                Tools.MessageBox.Show(TryFindResource("LuaExist") as string ?? "?!");
                return;
            }
            try
            {
                File.Create(Tools.Global.ProfilePath + $"user_script_send_convert/{newLuaFileNameTextBox.Text}.lua").Close();
                loadLuaFile(newLuaFileNameTextBox.Text);
            }
            catch
            {
                Tools.MessageBox.Show(TryFindResource("LuaCreateFail") as string ?? "?!");
                return;
            }
            newLuaFileWrapPanel.Visibility = Visibility.Collapsed;
        }

        private void LuaTestbutton_Click(object sender, RoutedEventArgs e)
        {
            if (luaFileList.SelectedItem == null || fileLoading) return;
            try
            {
                var r = LuaEnv.LuaLoader.Run($"{luaFileList.SelectedItem as string}.lua",
                    new System.Collections.ArrayList { "uartData",
                        (bool)luaTestHexCheck.IsChecked ? Tools.Global.Hex2Byte(luaTestTextBox.Text) :
                        Tools.Global.GetEncoding().GetBytes(luaTestTextBox.Text) });
                Tools.MessageBox.Show($"{TryFindResource("SettingLuaRunResult") as string ?? "?!"}\r\nHEX：" + Tools.Global.Byte2Hex(r) +
                    $"\r\n{TryFindResource("SettingLuaRawText") as string ?? "?!"}" + Tools.Global.Byte2Readable(r));
            }
            catch (Exception ex)
            {
                Tools.MessageBox.Show($"{TryFindResource("ErrorScript") as string ?? "?!"}\r\n" + ex.ToString());
            }
        }

        private void LuaTestCancelbutton_Click(object sender, RoutedEventArgs e) =>
            luaTestWrapPanel.Visibility = Visibility.Collapsed;

        private void TextEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (lastLuaFile != "")
                saveLuaFile(lastLuaFile);
        }

        internal void SaveOnUnload()
        {
            if (lastLuaFile != "")
                saveLuaFile(lastLuaFile);
        }
    }
}
