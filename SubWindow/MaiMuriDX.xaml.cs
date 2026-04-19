using MajdataEdit.MaiMuriDX;
using Python.Runtime;
using System.IO;
using System.Windows;

namespace MajdataEdit;

public partial class LaunchMaiMuriDX : Window
{
    RunArg RunArg { get; set; }
    public List<Error> ErrorList { get; set; } = new();


    private static bool _isRunning = false;
    public LaunchMaiMuriDX(RunArg runArg)
    {
        InitializeComponent();
        RunArg = runArg;
    }

    private async void StartCheck_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            MessageBox.Show("已经在运行一个实例了！", MainWindow.GetLocalizedString("Error"));
        }
        else
        {
            LaunchCheck();
            await ((MainWindow)Owner).ShowMuriDXErrorAsync(this);
            Close();
        }
    }

    private void LaunchCheck()
    {
        _isRunning = true;

        try
        {
            RunArg.render = RenderEnable_Checkbox.IsChecked == true;

            string home = Path.Combine(Directory.GetCurrentDirectory(), "MaiMuriDX");
            string py_home = Path.Combine(home, "python312-embed");

            Runtime.PythonDLL = $"{py_home}\\python312.dll";
            PythonEngine.PythonHome = py_home;
            PythonEngine.ProgramName = "MaiMuriDX";
            PythonEngine.Initialize();

            dynamic s, t; //s:静态检查结果，t:动态检查结果

            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                sys.path.insert(0, home);
                dynamic main = Py.Import("main");
                PyObject pyArg = RunArg.ToPython();
                dynamic result = main.c_run(pyArg);
                s = result[0];
                t = result[1];

                var I18N = MainWindow.GetLocalizedString;

                foreach (var item in s)
                {
                    Error error;
                    if (item["type"] == "Overlap")
                    {
                        error = new Error(ErrorType.MuriDXS,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                                string.Format(
                                    I18N("MuriDXSErrorOverlap"),
                                    item["affected"]["note"],
                                    item["cause"]["note"]
                                ),

                            string.Format(
                                I18N("MuriDXSErrorOverlapDetail"),
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["cause"]["combo"],
                                item["cause"]["note"],
                                item["cause"]["line"],
                                item["cause"]["col"]
                            ));
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "SlideHeadTap")
                    {
                        error = new Error(ErrorType.MuriDXS,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                                string.Format(
                                    I18N("MuriDXSErrorSlideHeadTap"),
                                    item["affected"]["note"],
                                    item["cause"]["note"]
                                ),

                            string.Format(
                                I18N("MuriDXSErrorSlideHeadTapDetail"),
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["cause"]["combo"],
                                item["cause"]["note"],
                                item["cause"]["line"],
                                item["cause"]["col"],
                                item["delta"] * 1000 / 180
                            ));
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "TapOnSlide")
                    {
                        error = new Error(ErrorType.MuriDXS,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                                string.Format(
                                    I18N("MuriDXSErrorTapOnSlide"),
                                    item["affected"]["note"],
                                    item["cause"]["note"]
                                ),

                            string.Format(
                                I18N("MuriDXSErrorTapOnSlideDetail"),
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["cause"]["combo"],
                                item["cause"]["note"],
                                item["cause"]["line"],
                                item["cause"]["col"],
                                item["delta"] * 1000 / 180
                            ));
                        ErrorList.Add(error);
                    }
                }



                foreach (var item in t)
                {
                    Error error;
                    float otime = item["time"];
                    int frame, sec = Math.DivRem((int)(otime * 60), 60, out frame);
                    int min = Math.DivRem(sec, 60, out sec);
                    string time = string.Format("[{0:D2}:{1:D2}F{2:00.00}]\n", min, sec, frame);
                    if (item["type"] == "MultiTouch")
                    {
                        string msg_notes = "";
                        foreach (var note in item["cause"])
                        {
                            msg_notes += string.Format(
                                "\"{2}\"(L{0},C{1})",
                                note["line"],
                                note["col"],
                                note["note"]) + "  ";
                        }
                        error = new Error(ErrorType.MuriDXD,
                            new Position((int)item["cause"][0]["col"], (int)item["cause"][0]["line"]),
                            string.Format(I18N("MuriDXDErrorMultiTouch"), item["hand_count"]),

                            string.Format(I18N("MuriDXDErrorMultiTouchDetail"),
                                time,
                                item["hand_count"],
                                msg_notes
                            )
                            );
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "SlideTooFast")
                    {
                        error = new Error(ErrorType.MuriDXD,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                            string.Format(I18N("MuriDXDErrorSlideTooFast"), item["affected"]["note"]),

                            string.Format(
                                I18N("MuriDXDErrorSlideTooFastDetail"),
                                time,
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["--critical_delta"] * 1000.0 / 180,
                                item["--msg_areas"]
                            )
                            );
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "Overlap")
                    {
                        error = new Error(ErrorType.MuriDXD,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                            string.Format(I18N("MuriDXDErrorOverlap"), item["affected"]["note"], ""),

                            string.Format(
                                I18N("MuriDXDErrorOverlapDetail"),
                                time,
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["delta"] * 1000 / 180
                            ));
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "SlideHeadTap")
                    {
                        error = new Error(ErrorType.MuriDXD,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                            string.Format(I18N("MuriDXDErrorSlideHeadTap"), item["affected"]["note"], item["cause"]["note"]),

                            string.Format(
                                I18N("MuriDXDErrorSlideHeadTapDetail"),
                                time,
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["cause"]["combo"],
                                item["cause"]["note"],
                                item["cause"]["line"],
                                item["cause"]["col"],
                                item["delta"] * 1000 / 180
                            ));
                        ErrorList.Add(error);
                    }
                    else if (item["type"] == "TapOnSlide")
                    {
                        error = new Error(ErrorType.MuriDXD,
                            new Position((int)item["affected"]["col"], (int)item["affected"]["line"]),
                            string.Format(I18N("MuriDXDErrorTapOnSlide"), item["affected"]["note"], item["cause"]["note"]),

                            string.Format(
                                I18N("MuriDXDErrorTapOnSlideDetail"),
                                time,
                                item["affected"]["combo"],
                                item["affected"]["note"],
                                item["affected"]["line"],
                                item["affected"]["col"],
                                item["cause"]["combo"],
                                item["cause"]["note"],
                                item["cause"]["line"],
                                item["cause"]["col"],
                                item["delta"] * 1000 / 180
                            ));
                        ErrorList.Add(error);
                    }
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message + "\n" + e.StackTrace, MainWindow.GetLocalizedString("Error"));
        }
        finally
        {
            PythonEngine.Shutdown();
        }

        _isRunning = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void Window_Initialized(object sender, EventArgs e)
    {
    }
}