using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WPFLocalizeExtension.Engine;

namespace MajdataEdit;

/// <summary>
///     App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    public App()
    {
        LocalizeDictionary.Instance.SetCurrentThreadCulture = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // // Auto-detect Wine and enable software rendering for compatibility
        // if (IsRunningUnderWine())
        // {
        //     RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        //     // Reduce WPF animation framerate to ease CPU load under Wine
        //     Timeline.DesiredFrameRateProperty.OverrideMetadata(
        //         typeof(Timeline),
        //         new FrameworkPropertyMetadata(30));
        // }
    }

    /// <summary>
    /// Detects whether the application is running under Wine/Proton.
    /// </summary>
    // private static bool IsRunningUnderWine()
    // {
    //     try
    //     {
    //         // Method 1: Check for ntdll.dll wine_get_version export
    //         var ntdll = GetModuleHandle("ntdll.dll");
    //         if (ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero)
    //             return true;

    //         // Method 2: Check Wine registry key
    //         using var key = Registry.CurrentUser.OpenSubKey(@"Software\Wine");
    //         if (key != null) return true;
    //     }
    //     catch { }
    //     return false;
    // }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception.GetType() == typeof(COMException) &&
            e.Exception.Message.IndexOf("UCEERR_RENDERTHREADFAILURE") != -1)
        {
            // 需要开启软件渲染
            MessageBox.Show(MajdataEdit.MainWindow.GetLocalizedString("SoftRenderError"),
                MajdataEdit.MainWindow.GetLocalizedString("Error"));
            Shutdown(114);
            return;
        }

        MessageBox.Show(e.Exception.Source + " At:\n" + e.Exception.Message + "\n" + e.Exception.StackTrace, "发生错误",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}