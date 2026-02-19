using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WPFLocalizeExtension.Engine;

namespace MajdataEdit;

/// <summary>
///     BPMtap.xaml 的交互逻辑
/// </summary>
public partial class EditorSettingPanel : Window
{
    private readonly bool dialogMode;
    private readonly string[] langList = new string[3] { "zh-CN", "en-US", "ja" }; // 语言列表
    private bool saveFlag;

    public EditorSettingPanel(bool _dialogMode = false)
    {
        dialogMode = _dialogMode;
        InitializeComponent();

        if (dialogMode) Cancel_Button.IsEnabled = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var window = (MainWindow)Owner;

        var curLang = window.editorSetting!.Language;
        var boxIndex = -1;
        for (var i = 0; i < langList.Length; i++)
            if (curLang == langList[i])
            {
                boxIndex = i;
                break;
            }

        if (boxIndex == -1)
            // 如果没有语言设置 或者语言未知 就自动切换到English
            boxIndex = 1;

        LanguageComboBox.SelectedIndex = boxIndex;

        RenderModeComboBox.SelectedIndex = window.editorSetting.RenderMode;

        ViewerCover.Text = window.editorSetting.BackgroundCover.ToString();
        ViewerSpeed.Text = window.editorSetting.PlaySpeed.ToString("F1"); // 转化为形如"7.0", "9.5"这样的速度
        ViewerTouchSpeed.Text = window.editorSetting.TouchSpeed.ToString("F1");
        ComboDisplay.SelectedIndex = Array.IndexOf(
            Enum.GetValues(window.editorSetting.ComboStatusType.GetType()),
            window.editorSetting.ComboStatusType
        );
        if (ComboDisplay.SelectedIndex < 0)
            ComboDisplay.SelectedIndex = 0;

        PlayMethod.SelectedIndex = Array.IndexOf(
            Enum.GetValues(window.editorSetting.EditorPlayMethod.GetType()),
            window.editorSetting.EditorPlayMethod
        );
        if(PlayMethod.SelectedIndex < 0) 
            PlayMethod.SelectedIndex = 0;

        ChartRefreshDelay.Text = window.editorSetting.ChartRefreshDelay.ToString();
        AutoUpdate.IsChecked = window.editorSetting.AutoCheckUpdate;
        SmoothSlideAnime.IsChecked = window.editorSetting.SmoothSlideAnime;
        EditorFontSize.Text = window.editorSetting.FontSize.ToString();

        ShareUserName.Text = window.editorSetting.ShareUserName;
        ShareColorHex.Text = window.editorSetting.ShareColorHex;

        cbJudgeDisplayMode.SelectedIndex = (int)window.editorSetting.judgeDisplayMode;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //LanguageComboBox.SelectedIndex
        LocalizeDictionary.Instance.Culture = new CultureInfo(langList[LanguageComboBox.SelectedIndex]);
    }

    private void RenderModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderOptions.ProcessRenderMode =
            RenderModeComboBox.SelectedIndex == 0 ? RenderMode.Default : RenderMode.SoftwareOnly;
    }

    private void ViewerCover_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var offset = float.Parse(ViewerCover.Text);
        offset += e.Delta > 0 ? 0.1f : -0.1f;
        ViewerCover.Text = offset.ToString();
    }

    private void ViewerSpeed_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var offset = float.Parse(ViewerSpeed.Text);
        offset += e.Delta > 0 ? 0.5f : -0.5f;
        ViewerSpeed.Text = offset.ToString();
    }

    private void ViewerTouchSpeed_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var offset = float.Parse(ViewerTouchSpeed.Text);
        offset += e.Delta > 0 ? 0.5f : -0.5f;
        ViewerTouchSpeed.Text = offset.ToString();
    }

    private void Save_Button_Click(object sender, RoutedEventArgs e)
    {
        var window = (MainWindow)Owner;
        window.editorSetting!.Language = langList[LanguageComboBox.SelectedIndex];
        window.editorSetting!.RenderMode = RenderModeComboBox.SelectedIndex;
        window.editorSetting!.BackgroundCover = float.Parse(ViewerCover.Text);
        window.editorSetting!.PlaySpeed = float.Parse(ViewerSpeed.Text);
        window.editorSetting!.TouchSpeed = float.Parse(ViewerTouchSpeed.Text);
        window.editorSetting!.ChartRefreshDelay = int.Parse(ChartRefreshDelay.Text);
        window.editorSetting!.AutoCheckUpdate = (bool) AutoUpdate.IsChecked!;
        window.editorSetting!.FontSize = float.Parse(EditorFontSize.Text);
        window.editorSetting!.SmoothSlideAnime = (bool) SmoothSlideAnime.IsChecked!;
        window.editorSetting!.EditorPlayMethod = (EditorPlayMethod)PlayMethod.SelectedIndex;
        // window.editorSetting.isComboEnabled = (bool) ComboDisplay.IsChecked!;
        window.editorSetting!.ComboStatusType = (EditorComboIndicator)Enum.GetValues(
            window.editorSetting!.ComboStatusType.GetType()
        ).GetValue(ComboDisplay.SelectedIndex)!;
        window.editorSetting.ShareUserName = ShareUserName.Text;
        window.editorSetting.ShareColorHex = ShareColorHex.Text;
        window.editorSetting.judgeDisplayMode = (JudgeDisplayMode)cbJudgeDisplayMode.SelectedIndex;

        window.SaveEditorSetting();

        window.ViewerCover.Content = window.editorSetting.BackgroundCover.ToString();
        window.ViewerSpeed.Content = window.editorSetting.PlaySpeed.ToString("F1"); // 转化为形如"7.0", "9.5"这样的速度
        window.ViewerTouchSpeed.Content = window.editorSetting.TouchSpeed.ToString("F1");
        window.chartChangeTimer.Interval = window.editorSetting.ChartRefreshDelay;
        window.FumenContent.FontSize = window.editorSetting.FontSize;


        saveFlag = true;
        window.SyntaxCheck();
        Close();
    }

    private void Cancel_Button_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!saveFlag)
        {
            // 取消或直接关闭窗口
            if (dialogMode)
            {
                // 模态窗口状态下 则阻止关闭
                e.Cancel = true;
                MessageBox.Show(MainWindow.GetLocalizedString("NoEditorSetting"),
                    MainWindow.GetLocalizedString("Error"));
            }
            else
            {
                LocalizeDictionary.Instance.Culture = new CultureInfo(((MainWindow)Owner).editorSetting!.Language);
            }
        }
        else
        {
            if (dialogMode) DialogResult = true;
        }
    }
}