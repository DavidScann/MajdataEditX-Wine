using DiscordRPC.Logging;
using MajdataEdit.AutoSaveModule;
using MajdataEdit.ChartShare;
using MajdataEdit.MaiMuriDX;
using MajdataEdit.SyntaxModule;
using MajdataEdit.Utils;
using MajSimai;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
    private void draw_wave()
    {
        if (isDrawing) return;
        if (WaveBitmap == null) return;

        Dispatcher.Invoke(() =>
        {
            isDrawing = true;
            try
            {
                var width = WaveBitmap.PixelWidth;
                var height = WaveBitmap.PixelHeight;

                if (waveRaws[0] == null)
                {
                    return;
                }

                WaveBitmap.Lock();
                try
                {
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
                    var pen = new Pen(Color.Green, linewidth);
                    var points = new List<PointF>();
                    for (var i = startindex; i < stopindex; i = i + 1)
                    {
                        if (i < 0) i = 0;
                        if (i >= waveLevels.Length - 1) break;

                        var x = (i - startindex) * linewidth;
                        var y = waveLevels[i] / 65535f * height + height / 2;

                        points.Add(new PointF(x, y));
                    }

                    graphics.DrawLines(pen, points.ToArray());

                    //Draw Bpm lines
                    var lastbpm = -1f;
                    var bpmChangeTimes = new List<double>(); //在什么时间变成什么值
                    var bpmChangeValues = new List<float>();
                    bpmChangeTimes.Clear();
                    bpmChangeValues.Clear();
                    foreach (var timing in SimaiProcess.timinglist)
                        if (timing.currentBpm != lastbpm)
                        {
                            bpmChangeTimes.Add(timing.time);
                            bpmChangeValues.Add(timing.currentBpm);
                            lastbpm = timing.currentBpm;
                        }

                    bpmChangeTimes.Add(Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetLength(bgmStream)));

                    // Optimized: calculate beats more efficiently to prevent lag
                    var visibleStart = currentTime - deltatime;
                    var visibleEnd = currentTime + deltatime;

                    // Find which BPM section contains or comes before the visible range
                    var startBpmIndex = 0;
                    for (var i = 0; i < bpmChangeTimes.Count - 1; i++)
                    {
                        if (bpmChangeTimes[i] <= visibleStart && visibleStart < bpmChangeTimes[i + 1])
                        {
                            startBpmIndex = i;
                            break;
                        }
                    }

                    // Calculate starting beat position
                    double time;
                    var signature = 4; //预留拍号
                    var currentBeat = 1;
                    var timePerBeat = 0d;

                    if (visibleStart > bpmChangeTimes[startBpmIndex] && bpmChangeValues.Count > startBpmIndex)
                    {
                        // Calculate how many beats have passed since the last BPM change
                        timePerBeat = 1d / (bpmChangeValues[startBpmIndex] / 60d);
                        var timeSinceLastBpm = visibleStart - bpmChangeTimes[startBpmIndex];
                        var beatsSinceLastBpm = (int)(timeSinceLastBpm / timePerBeat);
                        time = bpmChangeTimes[startBpmIndex] + beatsSinceLastBpm * timePerBeat;
                        currentBeat = (beatsSinceLastBpm % signature) + 1;
                    }
                    else
                    {
                        time = SimaiProcess.first;
                    }

                    pen = new Pen(Color.Yellow, 1);
                    var strongBeat = new List<double>();
                    var weakBeat = new List<double>();

                    // Start from the calculated BPM section
                    for (var i = Math.Max(1, startBpmIndex + 1); i < bpmChangeTimes.Count; i++)
                    {
                        while (time - bpmChangeTimes[i] < -0.05) //在那个时间之前都是之前的bpm
                        {
                            if (currentBeat > signature) currentBeat = 1;
                            timePerBeat = 1d / (bpmChangeValues[i - 1] / 60d);

                            // Only add beats within the visible range
                            if (time >= visibleStart && time <= visibleEnd)
                            {
                                if (currentBeat == 1)
                                    strongBeat.Add(time);
                                else
                                    weakBeat.Add(time);
                            }

                            currentBeat++;
                            time += timePerBeat;

                            // Early exit if past visible range
                            if (time > visibleEnd)
                                break;
                        }

                        if (time > visibleEnd)
                            break;

                        time = bpmChangeTimes[i];
                        currentBeat = 1;
                    }

                    foreach (var btime in strongBeat)
                    {
                        var x = ((float)(btime / step) - startindex) * linewidth;
                        graphics.DrawLine(pen, x, 0, x, 75);
                    }

                    foreach (var btime in weakBeat)
                    {
                        var x = ((float)(btime / step) - startindex) * linewidth;
                        graphics.DrawLine(pen, x, 0, x, 15);
                    }

                    //Draw timing lines
                    pen = new Pen(Color.White, 1);
                    foreach (var note in SimaiProcess.timinglist)
                    {
                        if (note == null) break;
                        if (note.time - currentTime < -deltatime) continue; // Skip timing lines before visible range
                        if (note.time - currentTime > deltatime) break; // Early exit when past visible range
                        var x = ((float)(note.time / step) - startindex) * linewidth;
                        graphics.DrawLine(pen, x, 60, x, 75);
                    }

                    //Draw notes                    
                    foreach (var note in SimaiProcess.notelist)
                    {
                        if (note == null) break;
                        if (note.time - currentTime < -deltatime) continue; // Skip notes before visible range
                        if (note.time - currentTime > deltatime) break; // Early exit when past visible range
                        var notes = note.getNotes();
                        var isEach = notes.Count(o => !o.isSlideNoHead) > 1;

                        var x = ((float)(note.time / step) - startindex) * linewidth;

                        foreach (var noteD in notes)
                        {
                            var y = noteD.startPosition * 6.875f + 8f; //与键位有关

                            if (noteD.isHanabi)
                            {
                                var xDeltaHanabi = (float)(1f / step) * linewidth; //Hanabi is 1s due to frame analyze
                                var rectangleF = new RectangleF(x, 0, xDeltaHanabi, 75);
                                if (noteD.noteType == SimaiNoteType.TouchHold)
                                    rectangleF.X += (float)(noteD.holdTime / step) * linewidth;
                                var gradientBrush = new LinearGradientBrush(
                                    rectangleF,
                                    Color.FromArgb(100, 255, 0, 0),
                                    Color.FromArgb(0, 255, 0, 0),
                                    LinearGradientMode.Horizontal
                                );
                                graphics.FillRectangle(gradientBrush, rectangleF);
                            }

                            if (noteD.noteType == SimaiNoteType.Tap)
                            {
                                if (noteD.isForceStar)
                                {
                                    pen.Width = 3;
                                    if (noteD.isBreak)
                                        pen.Color = Color.OrangeRed;
                                    else if (isEach)
                                        pen.Color = Color.Gold;
                                    else
                                        pen.Color = Color.DeepSkyBlue;
                                    Brush brush = new SolidBrush(pen.Color);
                                    graphics.DrawString("*", new Font("Consolas", 12, System.Drawing.FontStyle.Bold), brush,
                                        new PointF(x - 7f, y - 7f));
                                }
                                else
                                {
                                    pen.Width = 2;
                                    if (noteD.isBreak)
                                        pen.Color = Color.OrangeRed;
                                    else if (isEach)
                                        pen.Color = Color.Gold;
                                    else
                                        pen.Color = Color.LightPink;
                                    graphics.DrawEllipse(pen, x - 2.5f, y - 2.5f, 5, 5);
                                }
                            }

                            if (noteD.noteType == SimaiNoteType.Touch)
                            {
                                pen.Width = 2;
                                pen.Color = isEach ? Color.Gold : Color.DeepSkyBlue;
                                graphics.DrawRectangle(pen, x - 2.5f, y - 2.5f, 5, 5);
                            }

                            if (noteD.noteType == SimaiNoteType.Hold)
                            {
                                pen.Width = 3;
                                if (noteD.isBreak)
                                    pen.Color = Color.OrangeRed;
                                else if (isEach)
                                    pen.Color = Color.Gold;
                                else
                                    pen.Color = Color.LightPink;

                                var xRight = x + (float)(noteD.holdTime / step) * linewidth;

                                //1h[0:1]
                                if (!float.IsNormal(xRight)) xRight = ushort.MaxValue;
                                if (xRight - x < 1f) xRight = x + 5;
                                graphics.DrawLine(pen, x, y, xRight, y);

                            }

                            if (noteD.noteType == SimaiNoteType.TouchHold)
                            {
                                pen.Width = 3;
                                var xDelta = (float)(noteD.holdTime / step) * linewidth / 4f;
                                //Console.WriteLine("HoldPixel"+ xDelta);
                                if (!float.IsNormal(xDelta)) xDelta = ushort.MaxValue;
                                if (xDelta < 1f) xDelta = 1;

                                pen.Color = Color.FromArgb(200, 255, 75, 0);
                                graphics.DrawLine(pen, x, y, x + xDelta * 4f, y);
                                pen.Color = Color.FromArgb(200, 255, 241, 0);
                                graphics.DrawLine(pen, x, y, x + xDelta * 3f, y);
                                pen.Color = Color.FromArgb(200, 2, 165, 89);
                                graphics.DrawLine(pen, x, y, x + xDelta * 2f, y);
                                pen.Color = Color.FromArgb(200, 0, 140, 254);
                                graphics.DrawLine(pen, x, y, x + xDelta, y);
                            }

                            if (noteD.noteType == SimaiNoteType.Slide)
                            {
                                pen.Width = 3;
                                if (!noteD.isSlideNoHead)
                                {
                                    if (noteD.isBreak)
                                        pen.Color = Color.OrangeRed;
                                    else if (isEach)
                                        pen.Color = Color.Gold;
                                    else
                                        pen.Color = Color.DeepSkyBlue;
                                    Brush brush = new SolidBrush(pen.Color);
                                    graphics.DrawString("*", new Font("Consolas", 12, System.Drawing.FontStyle.Bold), brush,
                                        new PointF(x - 7f, y - 7f));
                                }

                                if (noteD.isSlideBreak)
                                    pen.Color = Color.OrangeRed;
                                else if (notes.Count(o => o.noteType == SimaiNoteType.Slide) >= 2)
                                    pen.Color = Color.Gold;
                                else
                                    pen.Color = Color.SkyBlue;
                                pen.DashStyle = DashStyle.Dot;
                                var xSlide = (float)(noteD.slideStartTime / step - startindex) * linewidth;
                                var xSlideRight = (float)(noteD.slideTime / step) * linewidth + xSlide;

                                if (!float.IsNormal(xSlideRight)) xSlideRight = ushort.MaxValue;
                                if (!float.IsNormal(xSlide)) xSlide = ushort.MaxValue;

                                graphics.DrawLine(pen, xSlide, y, xSlideRight, y);
                                pen.DashStyle = DashStyle.Solid;
                            }
                        }
                    }

                    if (playStartTime - currentTime <= deltatime)
                    {
                        //Draw play Start time
                        pen = new Pen(Color.Red, 5);
                        var x1 = (float)(playStartTime / step - startindex) * linewidth;
                        PointF[] tranglePoints = { new(x1 - 2, 0), new(x1 + 2, 0), new(x1, 3.46f) };
                        graphics.DrawPolygon(pen, tranglePoints);
                    }

                    if (ghostCusorPositionTime - currentTime <= deltatime)
                    {
                        //Draw ghost cusor
                        pen = new Pen(Color.Orange, 5);
                        var x2 = (float)(ghostCusorPositionTime / step - startindex) * linewidth;
                        PointF[] tranglePoints2 = { new(x2 - 2, 0), new(x2 + 2, 0), new(x2, 3.46f) };
                        graphics.DrawPolygon(pen, tranglePoints2);
                    }

                    graphics.Flush();
                    graphics.Dispose();
                    backBitmap.Dispose();

                    //MusicWave.Width = waveLevels.Length * zoominPower;
                    WaveBitmap.AddDirtyRect(new Int32Rect(0, 0, WaveBitmap.PixelWidth, WaveBitmap.PixelHeight));
                }
                finally
                {
                    WaveBitmap.Unlock();
                }
            }
            finally
            {
                isDrawing = false;
            }
        });
    }

            // 提取所有的节奏变更点（BPM 或 节拍记号 改变时）
            var bpmChanges = new List<(double Time, float Bpm, int Numerator, int Denominator)>();
            float lastBpm = -1f;
            int lastNum = -1;
            int lastDen = -1;

            foreach (var timing in SimaiProcess.timingLists[selectedDifficulty] ?? new())
            {
                if (timing == null) continue;
                if (timing.Bpm != lastBpm || timing.SignatureNumerator != lastNum || timing.SignatureDenominator != lastDen)
                {
                    bpmChanges.Add((timing.Timing, timing.Bpm, timing.SignatureNumerator, timing.SignatureDenominator));
                    lastBpm = timing.Bpm;
                    lastNum = timing.SignatureNumerator;
                    lastDen = timing.SignatureDenominator;
                }
            }

            // 添加音频结尾作为计算终点
            double audioEndTime = Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetLength(bgmStream));
            bpmChanges.Add((audioEndTime, lastBpm, lastNum, lastDen));

            double time = SimaiProcess.simaiFile.Offset;
            int currentBeat = 1;
            var strongBeat = new List<double>();
            var weakBeat = new List<double>();

            for (var i = 0; i < bpmChanges.Count - 1; i++)
            {
                var (Time, Bpm, Numerator, Denominator) = bpmChanges[i];
                var nextSegTime = bpmChanges[i + 1].Time;

                // 只要当前时间还没到下一个变更点，就按当前的节奏参数走
                while (time < nextSegTime - 0.05)
                {
                    // 如果超过了当前小节的分子，重置为第一拍
                    if (currentBeat > Numerator) currentBeat = 1;

                    // 计算当前 BPM 下一拍的时长： (60/BPM) * (4/分母)
                    double timePerBeat = (60d / Bpm) * (4.0 / Denominator);

                    if (currentBeat == 1)
                        strongBeat.Add(time);
                    else
                        weakBeat.Add(time);

                    currentBeat++;
                    time += timePerBeat;
                }

                time = nextSegTime;
                currentBeat = 1;
            }

            // Draw strong beat
            pen = new Pen(Color.Yellow, 1);
            foreach (var btime in strongBeat)
            {
                if (btime - currentTime > deltatime) continue;
                var x = ((float)(btime / step) - startindex) * linewidth;
                graphics.DrawLine(pen, x, 0, x, 75);
            }

            // Draw weak beat
            foreach (var btime in weakBeat)
            {
                if (btime - currentTime > deltatime) continue;
                var x = ((float)(btime / step) - startindex) * linewidth;
                graphics.DrawLine(pen, x, 0, x, 15);
            }

            // Draw timing lines
            pen = new Pen(Color.White, 1);
            foreach (var note in SimaiProcess.timingLists[selectedDifficulty] ?? new())
            {
                if (note == null) break;
                if (note.Timing - currentTime > deltatime) continue;
                var x = ((float)(note.Timing / step) - startindex) * linewidth;
                graphics.DrawLine(pen, x, 60, x, 75);
            }

            //Draw notes                    
            foreach (var note in SimaiProcess.noteLists[selectedDifficulty] ?? new())
            {
                if (note == null) break;
                if (note.Timing - currentTime > deltatime) continue;
                var notes = note.Notes;
                var isEach = notes.Count(o => !o.IsSlideNoHead) > 1;

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
                        if (noteD.IsForceStar)
                        {
                            pen.Width = 3;
                            if (noteD.IsBreak)
                                pen.Color = Color.OrangeRed;
                            else if (isEach)
                                pen.Color = Color.Gold;
                            else
                                pen.Color = Color.DeepSkyBlue;
                            Brush brush = new SolidBrush(pen.Color);
                            graphics.DrawString("*", new Font("Consolas", 12, System.Drawing.FontStyle.Bold), brush,
                                new PointF(x - 7f, y - 7f));
                        }
                        else
                        {
                            pen.Width = 2;
                            if (noteD.IsBreak)
                                pen.Color = Color.OrangeRed;
                            else if (isEach)
                                pen.Color = Color.Gold;
                            else
                                pen.Color = Color.LightPink;
                            graphics.DrawEllipse(pen, x - 2.5f, y - 2.5f, 5, 5);
                        }
                    }

                    if (noteD.Type == SimaiNoteType.Touch)
                    {
                        pen.Width = 2;
                        pen.Color = isEach ? Color.Gold : Color.DeepSkyBlue;
                        graphics.DrawRectangle(pen, x - 2.5f, y - 2.5f, 5, 5);
                    }

                    if (noteD.Type == SimaiNoteType.Hold)
                    {
                        pen.Width = 3;
                        if (noteD.IsBreak)
                            pen.Color = Color.OrangeRed;
                        else if (isEach)
                            pen.Color = Color.Gold;
                        else
                            pen.Color = Color.LightPink;

                        var xRight = x + (float)(noteD.HoldTime / step) * linewidth;

                        //1h[0:1]
                        if (!float.IsNormal(xRight)) xRight = ushort.MaxValue;
                        if (xRight - x < 1f) xRight = x + 5;
                        graphics.DrawLine(pen, x, y, xRight, y);

                    }

                    if (noteD.Type == SimaiNoteType.TouchHold)
                    {
                        pen.Width = 3;
                        var xDelta = (float)(noteD.HoldTime / step) * linewidth / 4f;
                        //Console.WriteLine("HoldPixel"+ xDelta);
                        if (!float.IsNormal(xDelta)) xDelta = ushort.MaxValue;
                        if (xDelta < 1f) xDelta = 1;

                        pen.Color = Color.FromArgb(200, 255, 75, 0);
                        graphics.DrawLine(pen, x, y, x + xDelta * 4f, y);
                        pen.Color = Color.FromArgb(200, 255, 241, 0);
                        graphics.DrawLine(pen, x, y, x + xDelta * 3f, y);
                        pen.Color = Color.FromArgb(200, 2, 165, 89);
                        graphics.DrawLine(pen, x, y, x + xDelta * 2f, y);
                        pen.Color = Color.FromArgb(200, 0, 140, 254);
                        graphics.DrawLine(pen, x, y, x + xDelta, y);
                    }

                    if (noteD.Type == SimaiNoteType.Slide)
                    {
                        pen.Width = 3;
                        if (!noteD.IsSlideNoHead)
                        {
                            if (noteD.IsBreak)
                                pen.Color = Color.OrangeRed;
                            else if (isEach)
                                pen.Color = Color.Gold;
                            else
                                pen.Color = Color.DeepSkyBlue;
                            Brush brush = new SolidBrush(pen.Color);
                            graphics.DrawString("*", new Font("Consolas", 12, System.Drawing.FontStyle.Bold), brush,
                                new PointF(x - 7f, y - 7f));
                        }

                        if (noteD.IsSlideBreak)
                            pen.Color = System.Drawing.Color.OrangeRed;
                        else if (notes.Count(o => o.Type == SimaiNoteType.Slide) >= 2)
                            pen.Color = Color.Gold;
                        else
                            pen.Color = Color.SkyBlue;
                        pen.DashStyle = DashStyle.Dot;
                        var xSlide = (float)(noteD.SlideStartTime / step - startindex) * linewidth;
                        var xSlideRight = (float)(noteD.SlideTime / step) * linewidth + xSlide;

                        if (!float.IsNormal(xSlideRight)) xSlideRight = ushort.MaxValue;
                        if (!float.IsNormal(xSlide)) xSlide = ushort.MaxValue;

                        graphics.DrawLine(pen, xSlide, y, xSlideRight, y);
                        pen.DashStyle = DashStyle.Solid;
                    }
                }
            }

            if (playStartTime - currentTime <= deltatime)
            {
                //Draw play Start time
                pen = new Pen(Color.Red, 5);
                var x1 = (float)(playStartTime / step - startindex) * linewidth;
                PointF[] tranglePoints = { new(x1 - 2, 0), new(x1 + 2, 0), new(x1, 3.46f) };
                graphics.DrawPolygon(pen, tranglePoints);
            }

            if (CursorTime - currentTime <= deltatime)
            {
                //Draw ghost cusor
                pen = new Pen(Color.Orange, 5);
                var x2 = (float)(CursorTime / step - startindex) * linewidth;
                PointF[] tranglePoints2 = { new(x2 - 2, 0), new(x2 + 2, 0), new(x2, 3.46f) };
                graphics.DrawPolygon(pen, tranglePoints2);
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

    private void SyntaxCheckButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SyntaxChecker.Scan(GetRawFumenText());
            set_err_count(SyntaxChecker.GetErrorCount());
            Dispatcher.Invoke(() => { ShowSyntaxError(); });
        }
        catch
        {
            set_err_count(GetLocalizedString("InternalErr"));
        }
    }

    private void MaiMuriDXButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchMaiMuriDX window = new(new RunArg(GetRawFumenText(), float.Parse(OffsetTextBox.Text), audioDir, false));
        window.Owner = this;
        window.Show();
    }

    private void SyntaxCheckButton_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            SyntaxChecker.Scan(GetRawFumenText());
            set_err_count(SyntaxChecker.GetErrorCount());
            Dispatcher.Invoke(() => { ShowSyntaxError(); });
        }
        catch
        {
            set_err_count(GetLocalizedString("InternalErr"));
        }
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

    #region RichTextbox events

    private async void FumenContent_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        NoteNowText.Content = 
            (FumenContent.Text[..FumenContent.CaretIndex] //.Replace("\r", "") //没区别
                                      .Count(o => o == '\n') + 1) + " 行";
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING && (bool)FollowPlayCheck.IsChecked!)
            return;

        await SimaiProcess.Serialize(GetRawFumenText());

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
        if (GetRawFumenText() == "" || IsLoading) return;
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
}