using DiscordRPC.Logging;
using MajdataEdit.AutoSaveModule;
using MajdataEdit.ChartShare;
using MajdataEdit.MaiMuriDX;
using MajdataEdit.Utils;
using MajSimai;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using Python.Runtime;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Un4seen.Bass;
using Brush = System.Drawing.Brush;
using Color = System.Drawing.Color;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Font = System.Drawing.Font;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Timer = System.Timers.Timer;

namespace MajdataEdit;

/// <summary>
///     MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    public static MainWindow instance;

    /// 设置窗口状态
    /// 仅配置 程序逻辑无关的UI元素，如可用性、标题栏文字等
    /// 类似 FumenContent, OffsetTextBox 之类的
    /// UI元素 内容 与数据相关，不在此处设置
    public void set_empty()
    {
        IsLoading = false;
        IsShare = false;
        IsHost = false;
        // show cover animation
        if (Cover.Visibility != Visibility.Visible)
        {
            ((Storyboard)Resources["CoverShow"]).Begin();
        }

        // ready for play
        Op_Button.IsEnabled = true;
        PlayAndPauseButton.Content = "▶";

        // limit for menu
        MenuEdit.IsEnabled = false;
        VolumnSetting.IsEnabled = false;
        Menu_ExportRender.IsEnabled = false;
        SyntaxCheckButton.IsEnabled = false;
        MaiMuriDX.IsEnabled = false;
        Menu_ToggleChartShare.IsEnabled = false;

        // window title
        TheWindow.Title = GetWindowsTitleString();

        // focus
        Cover.Focus();
    }

    public void set_loading(bool value)
    {
        IsLoading = value;
        if (value)
        {
            Cover.Visibility = Visibility.Visible;
            MenuBar.IsEnabled = false;
        }
        else
        {
            // hide cover animation
            if (Cover.Visibility == Visibility.Visible)
                ((Storyboard)Resources["CoverHide"]).Begin();
            MenuBar.IsEnabled = true;

            // limit for menu
            MenuEdit.IsEnabled = true;
            VolumnSetting.IsEnabled = true;
            Menu_ExportRender.IsEnabled = true;
            SyntaxCheckButton.IsEnabled = true;
            MaiMuriDX.IsEnabled = true;
            MapInfo.IsEnabled = true;
            Menu_ToggleChartShare.IsEnabled = true;

            // limit for editor
            LevelSelector.IsEnabled = true;
            LevelTextBox.IsEnabled = true;
            OffsetTextBox.IsEnabled = true;

            // window title
            TheWindow.Title = GetWindowsTitleString(SimaiProcess.simaiFile.Title);
        }
    }

    public void set_share(bool value)
    {
        IsShare = value;
        if (value)
        {
            // limit for menu
            MapInfo.IsEnabled = false;
            if (!IsHost) Menu_ToggleChartShare.IsEnabled = false; //非房主不能套娃开房
            Menu_AutosaveRecover.IsEnabled = false;

            // limit for editor
            LevelSelector.IsEnabled = false;
            LevelTextBox.IsEnabled = false;
            OffsetTextBox.IsEnabled = false;

            Menu_ConnectChartShare.Header = GetLocalizedString("DisconnectChartShare");

            // window title
            TheWindow.Title = GetWindowsTitleString(SimaiProcess.simaiFile.Title + " Share");
        }
        else
        {
            // limit for menu
            MapInfo.IsEnabled = true;
            //if (!isHost)
            Menu_ToggleChartShare.IsEnabled = true; //非房主不能套娃开房-恢复
            Menu_AutosaveRecover.IsEnabled = true;

            // limit for editor
            LevelSelector.IsEnabled = true;
            LevelTextBox.IsEnabled = true;
            OffsetTextBox.IsEnabled = true;

            Menu_ConnectChartShare.Header = GetLocalizedString("ConnectChartShare");

            // window title
            TheWindow.Title = GetWindowsTitleString(SimaiProcess.simaiFile.Title);
        }
    }
    public void set_host(bool value)
    {
        IsHost = value;
        if (value)
        {
            TheWindow.Height += 20;
            Global_Grid.RowDefinitions[2].Height = new GridLength(20); //show status bar
            ShareStatus.Text = string.Format(GetLocalizedString("ShareModeServer"), GetLocalIPAddress());
            Menu_ToggleChartShare.Header = GetLocalizedString("StopChartShare");
            Menu_ConnectChartShare.IsEnabled = false; //房主不能自己断掉与自己的连接
        }
        else
        {
            TheWindow.Height -= 20;
            Global_Grid.RowDefinitions[2].Height = new GridLength(0); //hide status bar
            ShareStatus.DataContext = null;
            Menu_ToggleChartShare.Header = GetLocalizedString("StartChartShare");
            Menu_ConnectChartShare.IsEnabled = true;
        }
    }

    void set_err_count<T>(T eCount) => Dispatcher.Invoke(() => ErrCount.Content = $"{eCount}");


    // wave draw
    bool isDrawing;
    bool isDrawingFFT;
    private float deltatime = 4f;
    private void draw_fft()
    {
        // During playback, only draw FFT every 4th frame (~7.5fps) to save CPU
        if (isPlaying)
        {
            _fftFrameCounter++;
            if (_fftFrameCounter % 4 != 0) return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            if (isDrawingFFT) return;
            isDrawingFFT = true;

            //Scroll WaveView
            var currentTime = Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetPosition(bgmStream));
            //MusicWave.Margin = new Thickness(-currentTime / sampleTime * zoominPower, Margin.Left, MusicWave.Margin.Right, Margin.Bottom);
            //MusicWaveCusor.Margin = new Thickness(-currentTime / sampleTime * zoominPower, Margin.Left, MusicWave.Margin.Right, Margin.Bottom);

            var writableBitmap = new WriteableBitmap(255, 255, 72, 72, PixelFormats.Pbgra32, null);
            FFTImage.Source = writableBitmap;
            writableBitmap.Lock();
            var backBitmap = new Bitmap(255, 255, writableBitmap.BackBufferStride,
                PixelFormat.Format32bppArgb, writableBitmap.BackBuffer);

            var graphics = Graphics.FromImage(backBitmap);
            graphics.Clear(Color.Transparent);

            var fft = new float[1024];
            Bass.BASS_ChannelGetData(bgmStream, fft, (int)BASSData.BASS_DATA_FFT1024);
            var points = new PointF[1024];
            for (var i = 0; i < fft.Length; i++)
                points[i] = new PointF((float)Math.Log10(i + 1) * 100f, 240 - fft[i] * 256); //semilog

            _cachedFFTPen ??= new Pen(Color.LightSkyBlue, 1);
            graphics.DrawCurve(_cachedFFTPen, points);


            //no please
            /*
            var isSuccess = new Visuals().CreateSpectrumWave(bgmStream, graphics, new System.Drawing.Rectangle(0, 0, 255, 255),
                System.Drawing.Color.White, System.Drawing.Color.Red, System.Drawing.Color.Black, 1,
                false, false, false);
            Console.WriteLine(isSuccess);
            */
            graphics.Flush();
            graphics.Dispose();
            backBitmap.Dispose();

            writableBitmap.AddDirtyRect(new Int32Rect(0, 0, 255, 255));
            writableBitmap.Unlock();
            isDrawingFFT = false;
        });
    }

    private void init_wave()
    {
        var width = (int)Width - 2;
        var height = (int)MusicWave.Height;
        WaveBitmap = new WriteableBitmap(width, height, 72, 72, PixelFormats.Pbgra32, null);
        MusicWave.Source = WaveBitmap;
    }

    private void draw_wave()
    {
        if (isDrawing) return;
        if (WaveBitmap == null) return;

        Dispatcher.Invoke(() =>
        {
            isDrawing = true;
            var width = WaveBitmap.PixelWidth;
            var height = WaveBitmap.PixelHeight;

            if (waveRaws[0] == null)
            {
                isDrawing = false;
                return;
            }

            WaveBitmap.Lock();

            //the process starts
            var backBitmap = new Bitmap(width, height, WaveBitmap.BackBufferStride,
                PixelFormat.Format32bppArgb, WaveBitmap.BackBuffer);
            var graphics = Graphics.FromImage(backBitmap);
            var currentTime = Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetPosition(bgmStream));

            graphics.Clear(Color.FromArgb(100, 0, 0, 0));

            var resample = (int)deltatime - 1;
            if (resample > 1 && resample <= 3) resample = 1;
            if (resample > 3) resample = 2;
            var waveLevels = waveRaws[resample];

        var step = songLength / waveLevels.Length;
        var startindex = (int)((currentTime - deltatime) / step);
        var stopindex = (int)((currentTime + deltatime) / step);
        var linewidth = backBitmap.Width / (float)(stopindex - startindex);

        // Use cached wave pen
        _cachedWavePen ??= new Pen(Color.Green, linewidth);
        _cachedWavePen.Width = linewidth;

        _reusablePointList.Clear();
        for (var i = startindex; i < stopindex; i = i + 1)
        {
            if (i < 0) i = 0;
            if (i >= waveLevels.Length - 1) break;

            var x = (i - startindex) * linewidth;
            var y = waveLevels[i] / 65535f * height + height / 2;

            _reusablePointList.Add(new PointF(x, y));
        }

        if (_reusablePointList.Count > 1)
            graphics.DrawLines(_cachedWavePen, _reusablePointList.ToArray());

        // Use cached beat positions (recalculated only when chart changes)
        RebuildBeatCacheIfNeeded();
        var strongBeat = _cachedStrongBeats;
        var weakBeat = _cachedWeakBeats;

        var viewStart = currentTime - deltatime;
        var viewEnd = currentTime + deltatime;

        // Draw strong beat (use early exit since list is sorted)
        _cachedBeatPenStrong ??= new Pen(Color.Yellow, 1);
        foreach (var btime in strongBeat)
        {
            if (btime < viewStart) continue;
            if (btime > viewEnd) break;
            var x = ((float)(btime / step) - startindex) * linewidth;
            graphics.DrawLine(_cachedBeatPenStrong, x, 0, x, 75);
        }

        // Draw weak beat
        foreach (var btime in weakBeat)
        {
            if (btime < viewStart) continue;
            if (btime > viewEnd) break;
            var x = ((float)(btime / step) - startindex) * linewidth;
            graphics.DrawLine(_cachedBeatPenStrong, x, 0, x, 15);
        }

        // Draw timing lines (with early skip and early exit for sorted list)
        _cachedTimingPen ??= new Pen(Color.White, 1);
        foreach (var note in SimaiProcess.timingLists[selectedDifficulty] ?? new())
        {
            if (note == null) break;
            if (note.Timing < viewStart) continue;
            if (note.Timing > viewEnd) break;
            var x = ((float)(note.Timing / step) - startindex) * linewidth;
            graphics.DrawLine(_cachedTimingPen, x, 60, x, 75);
        }

        //Draw notes (with early skip and early exit for sorted list)
        foreach (var note in SimaiProcess.noteLists[selectedDifficulty] ?? new())
        {
            if (note == null) break;
            // Skip notes too far in the past (allow extra margin for holds/slides that extend)
            if (note.Timing < viewStart - 30) continue;
            // Stop iterating once past the visible window (list is sorted by time)
            if (note.Timing > viewEnd) break;
            var notes = note.Notes;
            var isEach = notes.Count(o => !o.IsSlideNoHead && !o.IsMine) > 1;

                var x = ((float)(note.Timing / step) - startindex) * linewidth;

            foreach (var noteD in notes)
            {
                var y = noteD.StartPosition * 6.875f + 8f; //与键位有关

                if (noteD.IsHanabi)
                {
                    var xDeltaHanabi = (float)(1f / step) * linewidth; //Hanabi is 1s due to frame analyze
                    var rectangleF = new RectangleF(x, 0, xDeltaHanabi, 75);
                    if (noteD.Type == SimaiNoteType.TouchHold)
                        rectangleF.X += (float)(noteD.HoldTime / step) * linewidth;
                    var gradientBrush = new LinearGradientBrush(
                        rectangleF,
                        Color.FromArgb(100, 255, 0, 0),
                        Color.FromArgb(0, 255, 0, 0),
                        LinearGradientMode.Horizontal
                    );
                    graphics.FillRectangle(gradientBrush, rectangleF);
                }

                if (noteD.Type == SimaiNoteType.Tap)
                {
                    _cachedNotePen ??= new Pen(Color.LightPink, 2);
                    if (noteD.IsForceStar)
                    {
                        _cachedNotePen.Width = 3;
                        if (noteD.IsBreak) _cachedNotePen.Color = Color.OrangeRed;
                        else if (isEach) _cachedNotePen.Color = Color.Gold;
                        else if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                        else _cachedNotePen.Color = Color.DeepSkyBlue;
                        _cachedStarFont ??= new Font("Consolas", 12, System.Drawing.FontStyle.Bold);
                        Brush brush = new SolidBrush(_cachedNotePen.Color);
                        graphics.DrawString("*", _cachedStarFont, brush, new PointF(x - 7f, y - 7f));
                    }
                    else
                    {
                        _cachedNotePen.Width = 2;
                        if (noteD.IsBreak) _cachedNotePen.Color = Color.OrangeRed;
                        else if (isEach) _cachedNotePen.Color = Color.Gold;
                        else if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                        else _cachedNotePen.Color = Color.LightPink;
                        graphics.DrawEllipse(_cachedNotePen, x - 2.5f, y - 2.5f, 5, 5);
                    }
                }

                if (noteD.Type == SimaiNoteType.Touch)
                {
                    _cachedNotePen ??= new Pen(Color.DeepSkyBlue, 2);
                    _cachedNotePen.Width = 2;
                    if (noteD.IsBreak) _cachedNotePen.Color = Color.OrangeRed;
                    else if (isEach) _cachedNotePen.Color = Color.Gold;
                    else if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                    else _cachedNotePen.Color = Color.DeepSkyBlue;
                    graphics.DrawRectangle(_cachedNotePen, x - 2.5f, y - 2.5f, 5, 5);
                }

                if (noteD.Type == SimaiNoteType.Hold)
                {
                    _cachedNotePen ??= new Pen(Color.LightPink, 3);
                    _cachedNotePen.Width = 3;
                    if (noteD.IsBreak) _cachedNotePen.Color = Color.OrangeRed;
                    else if (isEach) _cachedNotePen.Color = Color.Gold;
                    else if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                    else _cachedNotePen.Color = Color.LightPink;

                    var xRight = x + (float)(noteD.HoldTime / step) * linewidth;

                    //1h[0:1]
                    if (!float.IsNormal(xRight) || xRight > ushort.MaxValue) xRight = ushort.MaxValue;
                    if (xRight - x < 1f) xRight = x + 5;
                    graphics.DrawLine(_cachedNotePen, x, y, xRight, y);
                }

                if (noteD.Type == SimaiNoteType.TouchHold)
                {
                    _cachedNotePen ??= new Pen(Color.FromArgb(200, 255, 75, 0), 3);
                    _cachedNotePen.Width = 3;
                    var xDelta = (float)(noteD.HoldTime / step) * linewidth / 4f;
                    //Console.WriteLine("HoldPixel"+ xDelta);

                    _cachedNotePen.Color = Color.FromArgb(200, 255, 75, 0);
                    if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                    graphics.DrawLine(_cachedNotePen, x, y, x + xDelta * 4f, y);
                    _cachedNotePen.Color = Color.FromArgb(200, 255, 241, 0);
                    graphics.DrawLine(_cachedNotePen, x, y, x + xDelta * 3f, y);
                    _cachedNotePen.Color = Color.FromArgb(200, 2, 165, 89);
                    if (noteD.IsMine) _cachedNotePen.Color = Color.Gray;
                    graphics.DrawLine(_cachedNotePen, x, y, x + xDelta * 2f, y);
                    _cachedNotePen.Color = Color.FromArgb(200, 0, 140, 254);
                    graphics.DrawLine(_cachedNotePen, x, y, x + xDelta, y);
                }

                if (noteD.Type == SimaiNoteType.Slide)
                {
                    _cachedNotePen ??= new Pen(Color.SkyBlue, 3);
                    _cachedNotePen.Width = 3;
                    if (!noteD.IsSlideNoHead)
                    {
                        if (noteD.IsBreak) _cachedNotePen.Color = Color.OrangeRed;
                        else if (isEach) _cachedNotePen.Color = Color.Gold;
                        else if (noteD.IsMine) _cachedNotePen.Color = Color.LightGray;
                        else _cachedNotePen.Color = Color.DeepSkyBlue;
                        _cachedStarFont ??= new Font("Consolas", 12, System.Drawing.FontStyle.Bold);
                        Brush brush = new SolidBrush(_cachedNotePen.Color);
                        graphics.DrawString("*", _cachedStarFont, brush, new PointF(x - 7f, y - 7f));
                    }

                    if (noteD.IsSlideBreak)
                        _cachedNotePen.Color = Color.OrangeRed;
                    else if (notes.Count(o => o.Type == SimaiNoteType.Slide && !o.IsMineSlide) >= 2)
                        _cachedNotePen.Color = Color.Gold;
                    else if (noteD.IsMine)
                        _cachedNotePen.Color = Color.LightGray;
                    else
                        _cachedNotePen.Color = Color.SkyBlue;

                    _cachedNotePen.DashStyle = DashStyle.Dot;
                    var xSlide = (float)(noteD.SlideStartTime / step - startindex) * linewidth;
                    var xSlideRight = (float)(noteD.SlideTime / step) * linewidth + xSlide;

                    if (!float.IsNormal(xSlideRight) || xSlideRight > ushort.MaxValue) xSlideRight = ushort.MaxValue;
                    if (!float.IsNormal(xSlide)) xSlide = ushort.MaxValue;

                    graphics.DrawLine(_cachedNotePen, xSlide, y, xSlideRight, y);
                    _cachedNotePen.DashStyle = DashStyle.Solid;
                }
            }
        }

        if (playStartTime - currentTime <= deltatime)
        {
            //Draw play Start time
            _cachedPlayStartPen ??= new Pen(Color.Red, 5);
            var x1 = (float)(playStartTime / step - startindex) * linewidth;
            PointF[] tranglePoints = { new(x1 - 2, 0), new(x1 + 2, 0), new(x1, 3.46f) };
            graphics.DrawPolygon(_cachedPlayStartPen, tranglePoints);
        }

        if (CursorTime - currentTime <= deltatime)
        {
            //Draw ghost cursor
            _cachedGhostCursorPen ??= new Pen(Color.Orange, 5);
            var x2 = (float)(CursorTime / step - startindex) * linewidth;
            PointF[] tranglePoints2 = { new(x2 - 2, 0), new(x2 + 2, 0), new(x2, 3.46f) };
            graphics.DrawPolygon(_cachedGhostCursorPen, tranglePoints2);
        }

        graphics.Flush();
        graphics.Dispose();
        backBitmap.Dispose();

        //MusicWave.Width = waveLevels.Length * zoominPower;
        WaveBitmap.AddDirtyRect(new Int32Rect(0, 0, WaveBitmap.PixelWidth, WaveBitmap.PixelHeight));
        WaveBitmap.Unlock();
        isDrawing = false;
        });
    }


    // editor UI

    private void update_time_display(double time)
    {
        var minute = (int)time / 60;
        double second = (int)(time - 60 * minute);
        Dispatcher.Invoke(() => { TimeLabel.Content = string.Format("{0}:{1:00}", minute, second); });
    }

    public void toggle_find()
    {
        if (FindGrid.Visibility == Visibility.Collapsed)
        {
            FindGrid.Visibility = Visibility.Visible;
            InputText.Text = FumenContent.SelectedText;
            InputText.Focus();
        }
        else
        {
            FindGrid.Visibility = Visibility.Collapsed;
        }
    }

    public void report_fatal_error(Error? error)
    {
        Dispatcher.Invoke(() =>
        {
            if (error == null)
            {
                fatalError = null;
                FatalErrorLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                fatalError = error;
                FatalErrorLabel.Content = string.Format(
                    GetLocalizedString("FatalError"),
                    error.Message,
                    error.Position.y,
                    error.Position.x);
                FatalErrorLabel.Visibility = Visibility.Visible;
            }
        });
    }

    public MainWindow()
    {
        InitializeComponent();
        if (Environment.GetCommandLineArgs().Contains("--ForceSoftwareRender"))
        {
            MessageBox.Show("正在以软件渲染模式运行\nソフトウェア・レンダリング・モードで動作\nBooting as software rendering mode.");
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
        instance = this;

        editorCommands = new()
        {
            { "PlayAndPause", new(() => TogglePlayAndStop()) },
            { "SaveFile", new(() => { SaveFumen(true); SystemSounds.Beep.Play(); }) },
            { "StopPlaying", new(() => TogglePlayAndPause()) },
            { "SendToView", new(() => TogglePlayAndStop(PlayMethod.Op)) },
            { "IncreasePlaybackSpeed", new(() => SetPlaybackSpeedDiff(1)) },
            { "DecreasePlaybackSpeed", new(() => SetPlaybackSpeedDiff(-1)) },
            { "Find", new(toggle_find) },
            { "MirrorLR", new(() => ApplyMirror(Mirror.HandleType.LRMirror)) },
            { "MirrorUD", new(() => ApplyMirror(Mirror.HandleType.UDMirror)) },
            { "Mirror180", new(() => ApplyMirror(Mirror.HandleType.HalfRotation)) },
            { "Mirror45", new(() => ApplyMirror(Mirror.HandleType.Rotation45)) },
            { "MirrorCcw45", new(() => ApplyMirror(Mirror.HandleType.CcwRotation45)) },
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        CheckAndStartView();

        TheWindow.Title = GetWindowsTitleString();

        SetWindowGoldenPosition();

        discordRpcClient.Logger = new ConsoleLogger { Level = LogLevel.Warning };
        discordRpcClient.Initialize();

        var handle = new WindowInteropHelper(this).Handle;
        Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_CPSPEAKERS, handle);
        TryLoadBassOpusPlugin();
        init_wave();

        ReadSoundEffect();
        ReadEditorSetting();

        chartChangeTimer.Elapsed += ChartChangeTimer_Elapsed;
        chartChangeTimer.AutoReset = false;
        currentTimeRefreshTimer.Elapsed += CurrentTimeRefreshTimer_Elapsed;
        currentTimeRefreshTimer.Start();
        visualEffectRefreshTimer.Elapsed += VisualEffectRefreshTimer_Elapsed;
        waveStopMonitorTimer.Elapsed += WaveStopMonitorTimer_Elapsed;

        if (editorSetting!.AutoCheckUpdate) await CheckUpdate(true);

        //errorListWindow.ErrorListView.Items.Add(new Error(ErrorType.Info, new Position(3, 5), "666", "三个6"));
        errorListWindow.Owner = this;
        //errorListWindow.Show();

        #region 异常退出处理

        if (!SafeTerminationDetector.Of().IsLastTerminationSafe())
        {
            // 若上次异常退出，则询问打开恢复窗口
            var result = MessageBox.Show(GetLocalizedString("AbnormalTerminationInformation"),
                GetLocalizedString("Attention"), MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                var lastEditPath = File.ReadAllText(SafeTerminationDetector.Of().RecordPath).Trim();
                if (lastEditPath.Length != 0)
                    // 尝试打开上次未正常关闭的谱面 然后再打开恢复页面
                    try
                    {
                        await InitFromFile(lastEditPath);
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine(error.StackTrace);
                    }

                Menu_AutosaveRecover_Click(new object(), new RoutedEventArgs());
            }
        }

        SafeTerminationDetector.Of().RecordProgramClose();

        #endregion
    }


    //start the view and wait for boot, then set window pos
    private void SetWindowPosTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        var setWindowPosTimer = (Timer)sender!;
        Dispatcher.Invoke(() => { InternalSwitchWindow(); });
        setWindowPosTimer.Stop();
        setWindowPosTimer.Dispose();
    }

    // This update very freqently to Draw FFT wave.
    private void VisualEffectRefreshTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            draw_fft();
            draw_wave();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    // This update less frequently. set the time text.
    private void CurrentTimeRefreshTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        update_time_display(Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetPosition(bgmStream)));
    }

    // 谱面变更延迟解析
    private void ChartChangeTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Console.WriteLine("TextChanged");
        //SyntaxCheck(); //不要进行定期检查（疑似快速修改谱面内容时莫名其妙卡死原因）
        //太快=>异步=>在另外线程调用的原因。。被自己蠢笑啦
        Dispatcher.Invoke(async () =>
        {
            SyntaxCheck();
            await SimaiProcess.Serialize(GetRawFumenText());
            InvalidateBeatCache();
            draw_wave();
            if (!ErrCount.Content.ToString()!.EndsWith("?"))
                set_err_count(ErrCount.Content.ToString() + "?");
        });
    }

    //Window events
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!isSaved)
            if (!AskSaveFumen())
            {
                e.Cancel = true;
                return;
            }

        var process = Process.GetProcessesByName("MajdataView");
        if (process.Length > 0)
        {
            var result = MessageBox.Show(GetLocalizedString("AskCloseView"), GetLocalizedString("Attention"),
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
                process[0].Kill();
        }

        currentTimeRefreshTimer.Stop();
        visualEffectRefreshTimer.Stop();

        soundSetting.Close();
        //if (bpmtap != null) { bpmtap.Close(); }
        //if (muriCheck != null) { muriCheck.Close(); }
        //SaveSetting(); 
        SaveEditorSetting(); //改了字体大小的话

        Bass.BASS_ChannelStop(bgmStream);
        Bass.BASS_StreamFree(bgmStream);
        Bass.BASS_ChannelStop(answerStream);
        Bass.BASS_StreamFree(answerStream);
        Bass.BASS_ChannelStop(breakStream);
        Bass.BASS_StreamFree(breakStream);
        Bass.BASS_ChannelStop(judgeExStream);
        Bass.BASS_StreamFree(judgeExStream);
        Bass.BASS_ChannelStop(hanabiStream);
        Bass.BASS_StreamFree(hanabiStream);
        Bass.BASS_Stop();
        Bass.BASS_Free();

        // 正常退出
        SafeTerminationDetector.Of().RecordProgramClose();
    }

    //Window grid events
    private void Grid_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            if (e.Data.GetData(DataFormats.FileDrop).ToString() == "System.String[]")
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                if (path.ToLower().Contains("maidata.txt"))
                {
                    if (!isSaved) if (!AskSaveFumen()) return;
                    var fileInfo = new FileInfo(path);
                    await InitFromFile(fileInfo.DirectoryName!);
                }
            }
    }

    private void FindClose_MouseDown(object sender, MouseButtonEventArgs e)
    {
        FindGrid.Visibility = Visibility.Collapsed;
        FumenContent.Focus();
    }

    #region MENU BARS

    private async void Menu_New_Click(object sender, RoutedEventArgs e)
    {
        if (!isSaved) if (!AskSaveFumen()) return;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "track.mp3, track.ogg|track.mp3;track.ogg"
        };
        if ((bool)openFileDialog.ShowDialog()!)
        {
            var fileInfo = new FileInfo(openFileDialog.FileName);
            CreateNewFumen(fileInfo.DirectoryName!);
            await InitFromFile(fileInfo.DirectoryName!);
        }
    }

    private async void Menu_Open_Click(object sender, RoutedEventArgs e)
    {
        if (!isSaved) if (!AskSaveFumen()) return;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "maidata.txt|maidata.txt"
        };
        if ((bool)openFileDialog.ShowDialog()!)
        {
            var fileInfo = new FileInfo(openFileDialog.FileName);
            await InitFromFile(fileInfo.DirectoryName!);
        }
    }

    private void Menu_Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFumen(true);
        SystemSounds.Beep.Play();
    }

    private void Menu_ExportRender_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndPause(PlayMethod.Record);
    }

    private async void Menu_ToggleChartShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ToggleChartShare(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(GetLocalizedString("ToggleShareFail"), ex.Message + ex.InnerException?.Message), GetLocalizedString("Error"));
            _client = null;
            return;
        }
    }

    private async void Menu_ConnectChartShare_Click(object sender, RoutedEventArgs e)
    {
        if (IsShare)
        {
            await DisconnectToChartServer();
        }
        else
        {
            new ConnectShare(async (ip, port) => { if (!await ConnectToChartServer(ip, port)) return; })
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            }.ShowDialog();
        }
    }

    private void Menu_CloseChart_Click(object sender, RoutedEventArgs e)
    {
        if (!isSaved) if (!AskSaveFumen()) return;
        ClearWindow(true);
    }

    private void MirrorLeftRight_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.LRMirror);
    }

    private void MirrorUpDown_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.UDMirror);
    }

    private void Mirror180_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.HalfRotation);
    }

    private void Mirror45_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.Rotation45);
    }

    private void MirrorCcw45_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.CcwRotation45);
    }

    private void SubDivide1p5_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplySubDevide(1.5f);
    }

    private void SubDivide2_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ApplySubDevide(2f);
    }

    private void BPMtap_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        new BPMtap {
            Owner = this
        }.Show();
    }

    private void MenuItem_InfomationEdit_Click(object? sender, RoutedEventArgs e)
    {
        var infoWindow = new Infomation();
        SetSavedState(false);
        infoWindow.ShowDialog();
        TheWindow.Title = GetWindowsTitleString(SimaiProcess.simaiFile.Title);
    }

    private void MenuItem_Majnet_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo() { FileName = "https://majdata.net", UseShellExecute = true });
        //maidata.txtの譜面書式
    }

    private void MenuItem_GitHub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo() { FileName = "https://github.com/re-poem/MajdataViewX", UseShellExecute = true });
    }

    private void MenuItem_SoundSetting_Click(object? sender, RoutedEventArgs e)
    {
        soundSetting = new SoundSetting
        {
            Owner = this
        };
        soundSetting.ShowDialog();
    }

    private async void SyntaxCheckButton_Click(object sender, EventArgs e)
    {
        if (await SyntaxCheck())
        {
            await Dispatcher.Invoke(async () => { await ShowSyntaxErrorAsync(); });
        }
    }

    private void MaiMuriDXButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchMaiMuriDX window = new(new RunArg(GetRawFumenText(), float.Parse(OffsetTextBox.Text), audioDir, false));
        window.Owner = this;
        window.Show();
    }

    private void MenuItem_EditorSetting_Click(object? sender, RoutedEventArgs e)
    {
        var esp = new EditorSettingPanel
        {
            Owner = this
        };
        esp.ShowDialog();
    }

    private void Menu_ResetViewWindow(object? sender, RoutedEventArgs e)
    {
        if (CheckAndStartView()) return;
        InternalSwitchWindow();
    }

    private void MenuFind_Click(object? sender, RoutedEventArgs e)
    {
        toggle_find();
    }

    private async void CheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        await CheckUpdate();
    }

    private void Menu_AutosaveRecover_Click(object? sender, RoutedEventArgs e)
    {
        var asr = new AutoSaveRecover
        {
            Owner = this
        };
        asr.ShowDialog();
    }

    #endregion

    #region 快捷键

    private void PlayAndPause_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        TogglePlayAndStop();
    }

    private void StopPlaying_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        TogglePlayAndPause();
    }

    private void SaveFile_Command_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        SaveFumen(true);
        SystemSounds.Beep.Play();
    }

    private void SendToView_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        TogglePlayAndStop(PlayMethod.Op);
    }

    private void IncreasePlaybackSpeed_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        SetPlaybackSpeedDiff(1);
    }

    private void DecreasePlaybackSpeed_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        SetPlaybackSpeedDiff(-1);
    }

    private void FindCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        toggle_find();
    }

    private void MirrorLRCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.LRMirror);
    }

    private void MirrorUDCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.UDMirror);
    }

    private void Mirror180Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.HalfRotation);
    }

    private void Mirror45Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.Rotation45);
    }

    private void MirrorCcw45Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        ApplyMirror(Mirror.HandleType.CcwRotation45);
    }

    #endregion

    #region Left componients

    private void PlayAndPauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndPause();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        Stop();
    }

    private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        set_loading(true);

        var i = LevelSelector.SelectedIndex;
        SetRawFumenText(SimaiProcess.fumens[i]);
        selectedDifficulty = i;
        LevelTextBox.Text = SimaiProcess.levels[selectedDifficulty];
        SetSavedState(true);
        await SimaiProcess.Serialize(GetRawFumenText());
        InvalidateBeatCache();
        draw_wave();
        SyntaxCheck();

        set_loading(false);
    }

    private void LevelTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SetSavedState(false);
        if (selectedDifficulty == -1) return;
        SimaiProcess.levels[selectedDifficulty] = LevelTextBox.Text;
    }

    private async void OffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoading) return;
        SetSavedState(false);
        if (string.IsNullOrWhiteSpace(OffsetTextBox.Text))
            OffsetTextBox.Text = "0";
        try
        {
            SimaiProcess.simaiFile.Offset = float.Parse(OffsetTextBox.Text);
            await SimaiProcess.Serialize(GetRawFumenText());
            InvalidateBeatCache();
            draw_wave();
        }
        catch
        {
            SimaiProcess.simaiFile.Offset = 0f;
        }
    }

    private void OffsetTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var offset = float.Parse(OffsetTextBox.Text);
        offset += e.Delta > 0 ? 0.01f : -0.01f;
        OffsetTextBox.Text = offset.ToString();
    }

    private void FollowPlayCheck_Click(object sender, RoutedEventArgs e)
    {
        FumenContent.Focus();
    }

    private void Op_Button_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndStop(PlayMethod.Op);
    }

    private void SettingLabel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // 单击设置的时候也可以进入设置界面
        var esp = new EditorSettingPanel();
        esp.Owner = this;
        esp.ShowDialog();
    }
    #endregion

    #region Textbox events

    private async void FumenContent_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        NoteNowText.Content = 
            (FumenContent.Text[..FumenContent.CaretIndex] //.Replace("\r", "") //没区别
                                      .Count(o => o == '\n') + 1) + " 行";
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING && (bool)FollowPlayCheck.IsChecked!)
            return;

        await SimaiProcess.Serialize(GetRawFumenText());
        InvalidateBeatCache();

        var timings = SimaiProcess.timingLists[selectedDifficulty] ?? new();
        double time = 0d;
        foreach (var timing in timings)
        {
            if (timing.RawTextPosition >= GetRawFumenPosition())
            {
                time = timing.Timing;
                break;
            }
        }

        //按住Ctrl，同时按下鼠标左键/上下左右方向键时，才改变进度，其他包含Ctrl的组合键不影响进度。
        //从错误页导航时/查找替换时(needChangeTime)也改变进度
        if ((Keyboard.Modifiers == ModifierKeys.Control && (
                Mouse.LeftButton == MouseButtonState.Pressed ||
                Keyboard.IsKeyDown(Key.Left) ||
                Keyboard.IsKeyDown(Key.Right) ||
                Keyboard.IsKeyDown(Key.Up) ||
                Keyboard.IsKeyDown(Key.Down)
            )) || needChangeTime)
        {
            if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING) Pause();
            SetBgmPosition(time);
            needChangeTime = false;
        }

        //Console.WriteLine("SelectionChanged: " + GetRawFumenPosition());
        CursorTime = (float)time;
        if (!isPlaying) draw_wave();
        if (!isFinding)
        {
            findPosition = FumenContent.CaretIndex; //主动点击时刷新一下
            isFinding = false;
        }

        if (IsShare && !_isRemoteUpdate)
        {
            await _client!.InvokeAsync(nameof(ChartHub.Moving), GetRawFumenPosition());
        }
    }

    private async void FumenContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoading) return;
        SetSavedState(false);
        await SyncChartServer(); //立马同步，用了diff的原因，没那么卡

        //间隔太小了不用管 话说为什么是33。
        //if (chartChangeTimer.Interval < 33)
        //{
        //    SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());
        //    DrawWave();
        //    return;
        //}

        //私以为没必要 真的有人注意过铺面刷新延迟吗。
        chartChangeTimer.Stop();
        chartChangeTimer.Start();
    }

    private void FumenContent_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var size = FumenContent.FontSize + e.Delta / 100;
            if (size > 0)
            {
                editorSetting!.FontSize = (float)size;
                FumenContent.FontSize = size;
            }

            e.Handled = true;
        }
    }

    private void Find_icon_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        FindAndScroll();
    }

    private void Replace_icon_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        FindAndReplace();
    }

    private void FumenContent_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 按下Insert键，同时未按下任何组合键，切换覆盖模式
        if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
        {
            SwitchFumenOverwriteMode();
            e.Handled = true;
        }
    }

    private void FumenContent_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RenderAllCursors();
    }

    #endregion

    #region Wave displayer

    private void WaveViewZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (deltatime > 1)
            deltatime -= 1;
        draw_wave();
        FumenContent.Focus();
    }

    private void WaveViewZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (deltatime < 10)
            deltatime += 1;
        draw_wave();
        FumenContent.Focus();
    }

    private void MusicWave_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.LeftAlt))
        {
            var newDelta = deltatime + -e.Delta / 100;
            if (newDelta > 1 && newDelta < 10)
                deltatime = newDelta;
            draw_wave();
            return;
        }
        ScrollWave(-e.Delta);
    }

    private void MusicWave_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        lastMousePointX = e.GetPosition(this).X;
    }

    private void MusicWave_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = e.GetPosition(this).X - lastMousePointX;
            lastMousePointX = e.GetPosition(this).X;
            ScrollWave(-delta);
        }

        lastMousePointX = e.GetPosition(this).X;
    }

    private void MusicWave_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        init_wave();
        draw_wave();
    }

    #endregion

    private void FatalErrorLabel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SetRawFumenPosition(fatalError!.Position.x, fatalError.Position.y-1);
    }

    private void PlayBackSpeedSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        playbackSpeed = PlayBackSpeedSelector.SelectedIndex switch
        {
            0 => 0.1f,
            1 => 0.25f,
            2 => 0.5f,
            3 => 0.75f,
            4 => 1f,
            5 => 1.5f,
            6 => 2f,
            _ => 1f
        };
    }

    #region New Feature Event Handlers

    private void SwitchFullKeyboardMode_Click(object sender, RoutedEventArgs e)
    {
        editorSetting!.FullKeyboardMode = !editorSetting.FullKeyboardMode;
        MessageBox.Show(this, $"Full Keyboard Mode: {(editorSetting.FullKeyboardMode ? "ON" : "OFF")}", "Info");
    }

    private void ExtractMp3_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Extract MP3 feature not yet implemented", "Info");
    }

    private void MediaQuickProcess_Click(object sender, RoutedEventArgs e)
    {
        MediaQuickProcessPopup.IsOpen = true;
    }

    private void ConvertToFcpxml_Click(object sender, RoutedEventArgs e)
    {
        FcpxmlFpsPopup.IsOpen = true;
    }

    private void FcpxmlFpsPopup_Opened(object sender, EventArgs e)
    {
        FcpxmlFpsBox.Focus();
    }

    private void FcpxmlFpsBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmFps_Click(sender, e);
        }
    }

    private void ConfirmFps_Click(object sender, RoutedEventArgs e)
    {
        FcpxmlFpsPopup.IsOpen = false;
        MessageBox.Show(this, $"FCPXML export with FPS: {FcpxmlFpsBox.Text}", "Info");
    }

    private void MediaQuickProcessPopup_Opened(object sender, EventArgs e)
    {
        BeatsCountBox.Focus();
    }

    private void BeatsCountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Only allow numeric input
        if (e.Key < Key.D0 || e.Key > Key.D9)
        {
            if (e.Key < Key.NumPad0 || e.Key > Key.NumPad9)
            {
                if (e.Key != Key.Back && e.Key != Key.Delete)
                {
                    e.Handled = true;
                }
            }
        }
    }

    private void ConfilmMediaQuickProcess_Click(object sender, RoutedEventArgs e)
    {
        MediaQuickProcessPopup.IsOpen = false;
        MessageBox.Show(this, $"Media Quick Process with {BeatsCountBox.Text} beats", "Info");
    }

    #endregion
}
