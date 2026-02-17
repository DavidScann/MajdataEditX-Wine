using DiscordRPC;
using MajdataEdit.SyntaxModule;
using MajdataEdit.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Semver;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Un4seen.Bass;
using WPFLocalizeExtension.Engine;
using Timer = System.Timers.Timer;

namespace MajdataEdit;

public partial class MainWindow : Window
{
    // edit
    public void ShowMuriDXError(LaunchMaiMuriDX lmmdWindow)
    {
        SyntaxCheck();
        for (int i = errorListWindow.ErrorListView.Items.Count - 1; i >= 0; i--)
        {
            if ((errorListWindow.ErrorListView.Items[i] as Error)!.Type is ErrorType.MuriDXS or ErrorType.MuriDXD)
            {
                errorListWindow.ErrorListView.Items.RemoveAt(i);
            }
        }
        var errList = lmmdWindow.ErrorList;
        errList.ForEach(e =>
        {
            errorListWindow.ErrorListView.Items.Add(e);
        });
        if (errorListWindow.IsVisible) errorListWindow.Activate();
        else errorListWindow.Show();
        if (errList.Count >= 100 && editorSetting!.Language == "zh-CN")
        {
            MessageBox.Show("我将删除你的Majdata。");
        }
    }
    public void ShowSyntaxError()
    {
        SyntaxCheck();
        for (int i = errorListWindow.ErrorListView.Items.Count - 1; i >= 0; i--)
        {
            if ((errorListWindow.ErrorListView.Items[i] as Error)!.Type == ErrorType.Syntax)
            {
                errorListWindow.ErrorListView.Items.RemoveAt(i);
            }
        }
        var errListCopy = SyntaxChecker.ErrorList.ToList();
        errListCopy.ForEach(e =>
        {
            errorListWindow.ErrorListView.Items.Add(e);
        });
        if (errorListWindow.IsVisible) errorListWindow.Activate();
        else errorListWindow.Show();
    }
    public async void ShowSerializeError()
    {
        await SimaiProcess.Serialize(GetRawFumenText());

    }
    public async void SyntaxCheck()
    {
        try
        {
            SyntaxChecker.Scan(GetRawFumenText());
            set_err_count(SyntaxChecker.ErrorList.Count);
        }
        catch
        {
            set_err_count(GetLocalizedString("InternalErr"));
        }
    }
    private void SwitchFumenOverwriteMode()
    {
        fumenOverwriteMode = !fumenOverwriteMode;

        // 修改覆盖模式启用状态
        // fetch TextEditor from FumenContent
        var textEditorProperty =
            typeof(TextBox).GetProperty("TextEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        var textEditor = textEditorProperty!.GetValue(FumenContent, null);

        // set _OvertypeMode on the TextEditor
        var overtypeModeProperty = textEditor!.GetType()
            .GetProperty("_OvertypeMode", BindingFlags.NonPublic | BindingFlags.Instance)!;
        overtypeModeProperty!.SetValue(textEditor, fumenOverwriteMode, null);

        //修改提示弹窗可见性
        OverrideModeTipsPopup.Visibility = fumenOverwriteMode ? Visibility.Visible : Visibility.Collapsed;
    }


    // editor setting

    public void CreateEditorSetting()
    {
        editorSetting = new EditorSetting
        {
            RenderMode =
            RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly ? 1 : 0 // 使用命令行指定强制软件渲染时，同步修改配置值
        };

        File.WriteAllText(editorSettingFilename, JsonConvert.SerializeObject(editorSetting, Formatting.Indented));

        var esp = new EditorSettingPanel(true)
        {
            Owner = this
        };
        esp.ShowDialog();
    }

    public void ReadEditorSetting()
    {
        if (!File.Exists(editorSettingFilename)) CreateEditorSetting();
        var json = File.ReadAllText(editorSettingFilename);
        editorSetting = JsonConvert.DeserializeObject<EditorSetting>(json)!;

        if (RenderOptions.ProcessRenderMode != RenderMode.SoftwareOnly)
            //如果没有通过命令行预先指定渲染模式，则使用设置项的渲染模式
            RenderOptions.ProcessRenderMode =
                editorSetting.RenderMode == 0 ? RenderMode.Default : RenderMode.SoftwareOnly;
        else
            //如果通过命令行指定了使用软件渲染模式，则覆盖设置项
            editorSetting.RenderMode = 1;

        LocalizeDictionary.Instance.Culture = new CultureInfo(editorSetting.Language);
        AddGesture(editorSetting.PlayPauseKey, "PlayAndPause");
        AddGesture(editorSetting.PlayStopKey, "StopPlaying");
        AddGesture(editorSetting.SaveKey, "SaveFile");
        AddGesture(editorSetting.SendViewerKey, "SendToView");
        AddGesture(editorSetting.IncreasePlaybackSpeedKey, "IncreasePlaybackSpeed");
        AddGesture(editorSetting.DecreasePlaybackSpeedKey, "DecreasePlaybackSpeed");
        AddGesture("Ctrl+f", "Find");
        AddGesture(editorSetting.MirrorLeftRightKey, "MirrorLR");
        AddGesture(editorSetting.MirrorUpDownKey, "MirrorUD");
        AddGesture(editorSetting.Mirror180Key, "Mirror180");
        AddGesture(editorSetting.Mirror45Key, "Mirror45");
        AddGesture(editorSetting.MirrorCcw45Key, "MirrorCcw45");
        FumenContent.FontSize = editorSetting.FontSize;

        ViewerCover.Content = editorSetting.backgroundCover.ToString();
        ViewerSpeed.Content = editorSetting.playSpeed.ToString("F1"); // 转化为形如"7.0", "9.5"这样的速度
        ViewerTouchSpeed.Content = editorSetting.touchSpeed.ToString("F1");

        chartChangeTimer.Interval = editorSetting.ChartRefreshDelay; // 设置更新延迟

        SaveEditorSetting(); // 覆盖旧版本setting
    }

    public void SaveEditorSetting()
    {
        File.WriteAllText(editorSettingFilename, JsonConvert.SerializeObject(editorSetting, Formatting.Indented));
    }


    // wave

    public void ScrollWave(double delta)
    {
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING)
            Pause();
        delta = delta * deltatime / (Width / 2);
        var time = Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetPosition(bgmStream));
        SetBgmPosition(time + delta);
        SeekTextFromTime();
        Task.Run(() => draw_wave());
    }


    // window management

    public bool CheckAndStartView()
    {
        if (Process.GetProcessesByName("MajdataView").Length == 0 && Process.GetProcessesByName("Unity").Length == 0)
        {
            try
            {
                var viewProcess = Process.Start("MajdataView.exe");
                var setWindowPosTimer = new Timer(2000)
                {
                    AutoReset = false
                };
                setWindowPosTimer.Elapsed += SetWindowPosTimer_Elapsed;
                setWindowPosTimer.Start();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        return false;
    }

    public void InternalSwitchWindow(bool moveToPlace = true)
    {
        var windowPtr = FindWindow(null, "MajdataView");
        ShowWindow(windowPtr, 5); //还原窗口
        SwitchToThisWindow(windowPtr, true);
        if (moveToPlace) InternalMoveWindow();
    }

    public void InternalMoveWindow()
    {
        var windowPtr = FindWindow(null, "MajdataView");
        var source = PresentationSource.FromVisual(this);

        double dpiX = 1, dpiY = 1;
        if (source != null)
        {
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }

        //Console.WriteLine(dpiX+" "+dpiY);
        dpiX /= 96d;
        dpiY /= 96d;

        var Height = this.Height * dpiY;
        var Left = this.Left * dpiX;
        var Top = this.Top * dpiY;
        MoveWindow(windowPtr,
            (int)(Left - Height + 20),
            (int)Top,
            (int)Height - 20,
            (int)Height, true);
    }

    private void SetWindowGoldenPosition()
    {
        // 属于你的独享黄金位置
        var ScreenWidth = SystemParameters.PrimaryScreenWidth;
        var ScreenHeight = SystemParameters.PrimaryScreenHeight;

        Left = (ScreenWidth - Width + Height) / 2 - 10;
        Top = (ScreenHeight - Height) / 2;
    }


    // program version management

    private async Task CheckUpdate(bool onStart = false)
    {
        if (UpdateCheckLock) return;
        UpdateCheckLock = true;

        // 检查是否需要更新软件
        var response = await WebControl.RequestGETAsync("http://api.github.com/repos/re-poem/MajdataViewX/releases/latest");

        try
        {
            UpdateCheckLock = false;
            if (response == "ERROR")
            {
                // 网络请求失败
                if (!onStart)
                    MessageBox.Show(GetLocalizedString("RequestFail"), GetLocalizedString("CheckUpdate"));
                return;
            }

            var resJson = JsonConvert.DeserializeObject<JObject>(response)!;
            var latestVersionString = resJson["tag_name"]!.ToString();
            var releaseUrl = resJson["html_url"]!.ToString();

            var latestVersion = SemVersion.Parse(latestVersionString, SemVersionStyles.Any);

            if (latestVersion.ComparePrecedenceTo(MAJDATA_VERSION) > 0)
            {
                // 版本不同，需要更新
                var msgboxText = string.Format(GetLocalizedString("NewVersionDetected"), latestVersionString,
                    MAJDATA_VERSION_STRING);
                if (onStart) msgboxText += "\n\n" + GetLocalizedString("AutoUpdateCheckTip");

                var result = MessageBox.Show(
                    msgboxText,
                    GetLocalizedString("CheckUpdate"),
                    MessageBoxButton.YesNo);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        var startInfo = new ProcessStartInfo(releaseUrl)
                        {
                            UseShellExecute = true
                        };
                        Process.Start(startInfo);
                        break;
                    case MessageBoxResult.No:
                        break;
                }
            }
            else
            {
                // 没有新版本，可以不用更新
                if (!onStart) MessageBox.Show(GetLocalizedString("NoNewVersion"), GetLocalizedString("CheckUpdate"));
            }
        }
        catch (Exception)
        {
            // 解析失败
            if (!onStart) MessageBox.Show(GetLocalizedString("RequestFail"), GetLocalizedString("CheckUpdate"));
        }
    }


    // title

    public string GetWindowsTitleString()
    {
        return $"MajdataEdit ({MAJDATA_VERSION_STRING})";
    }

    public string GetWindowsTitleString(string info)
    {
        try
        {
            var details = "Editing: " + SimaiProcess.simaiFile.Title;
            if (details.Length > 50)
                details = details[..50];
            discordRpcClient.SetPresence(new RichPresence
            {
                Details = details,
                State = "With note count of " + SimaiProcess.noteLists[selectedDifficulty]?.Count ?? "",
                Assets = new Assets
                {
                    LargeImageKey = "salt",
                    LargeImageText = "Majdata",
                    SmallImageKey = "None"
                }
            });
        }
        catch
        {
        }

        return GetWindowsTitleString() + " - " + info;
    }

    //////////////////// Helper Functions ////////////////////

    private void AddGesture(string keyGusture, string command)
    {
        var gesture = (InputGesture)new KeyGestureConverter().ConvertFromString(keyGusture)!;
        var inputBinding = new InputBinding((ICommand)FumenContent.Resources[command], gesture);
        FumenContent.InputBindings.Add(inputBinding);
    }


    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", EntryPoint = "MoveWindow")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
}
