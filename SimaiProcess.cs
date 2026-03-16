using MajSimai;
using System.IO;
using System.Windows;

namespace MajdataEdit;

internal static class SimaiProcess
{
    public static SimaiFile simaiFile = SimaiFile.Empty("", "");

    // directly read/write from simaiFile
    //public static string? title;
    //public static string? artist;
    //public static string? other_commands;
    //public static float first;

    // read/write to vars and save to simaiFile later
    public static string[] designers = new string[7];
    public static string[] fumens = new string[7];
    public static string[] levels = new string[7];

    // the timing points that contains notedata
    public static List<SimaiTimingPoint>[] OriginNoteLists = new List<SimaiTimingPoint>[7];
    public static List<SimaiTimingPoint>[] noteLists = new List<SimaiTimingPoint>[7];


    // the timing points made by "," in maidata
    public static List<SimaiTimingPoint>[] OriginTimingLists = new List<SimaiTimingPoint>[7];
    public static List<SimaiTimingPoint>[] timingLists = new List<SimaiTimingPoint>[7];


    /// <summary>
    ///     Reset all the data in the static class.
    /// </summary>
    public static void Clear()
    {
        simaiFile = SimaiFile.Empty("", "");
    }

    /// <summary>
    ///     Read the maidata.txt into the static class, including the variables. Show up a messageBox when enconter any
    ///     exception.
    /// </summary>
    /// <param name="filePath">file path of maidata.txt</param>
    /// <returns>if the read process faced any error</returns>
    public static async Task ReadAll(string filePath)
    {
        try
        {
            using var fileStream = File.OpenRead(filePath);
            simaiFile = await SimaiParser.ParseAsync(fileStream);
            designers = simaiFile.Charts.Select(c => c.Designer ?? "").ToArray();
            fumens = simaiFile.Charts.Select(c => c.Fumen ?? "").ToArray();
            levels = simaiFile.Charts.Select(c => c.Level ?? "").ToArray();
            OriginNoteLists = simaiFile.Charts.Select(c => c.NoteTimings.ToArray().ToList()).ToArray();
            OriginTimingLists = simaiFile.Charts.Select(c => c.CommaTimings.ToArray().ToList()).ToArray();
        }
        catch (InvalidSimaiMarkupException e)
        {
            MessageBox.Show($"在maidata读取谱面时出现错误：(L{e.Line} C{e.Column})\n{e.Message}");
        }
    }

    /// <summary>
    ///     Save the static data to maidata.txt
    /// </summary>
    /// <param name="filename">file path of maidata.txt</param>
    public static async void SaveData(string filename)
    {
        for (int i = 0; i < 7; i++)
        {
            SimaiChart chart = new(levels[i], 
                                designers[i], 
                                fumens[i],
                                OriginNoteLists[i].ToArray(),
                                OriginTimingLists[i].ToArray());
            simaiFile.Charts[i] = chart;
        }
        using var fileStream = File.OpenWrite(filename);
        fileStream.SetLength(0);
        await SimaiParser.DeparseAsync(simaiFile, fileStream);
    }

    /// <summary>
    ///     This method serialize the fumen data and load it into the static class.
    /// </summary>
    /// <param name="text">fumen text</param>
    public static async Task Serialize(string text)
    {
        try
        {
            MainWindow.instance.report_fatal_error(null);
            var selectedDiff = MainWindow.selectedDifficulty;
            noteLists[selectedDiff] = new();
            timingLists[selectedDiff] = new();
            var chart = await SimaiParser.ParseChartAsync(text);
            fumens[selectedDiff] = text;
            OriginNoteLists[selectedDiff] = chart.NoteTimings.ToArray().ToList();
            OriginTimingLists[selectedDiff] = chart.CommaTimings.ToArray().ToList();
            noteLists[selectedDiff] = new(OriginNoteLists[selectedDiff]);
            timingLists[selectedDiff] = new(OriginTimingLists[selectedDiff]);
            foreach (var noteGroup in noteLists[selectedDiff])
            {
                noteGroup.Timing += simaiFile.Offset;
                foreach (var note in noteGroup.Notes)
                {
                    note.SlideStartTime += simaiFile.Offset;
                }
            }
            foreach (var timing in timingLists[selectedDiff])
            {
                timing.Timing += simaiFile.Offset;
            }
        }
        catch (InvalidSimaiMarkupException e)
        {
            MainWindow.instance.report_fatal_error(new Error(
                ErrorType.Serialize, 
                new Position(e.Column, e.Line),
                e.Message,
                e.StackTrace));
        }
    }

    public static string GetDifficultyText(int index)
    {
        if (index == 0) return "EASY";
        if (index == 1) return "BASIC";
        if (index == 2) return "ADVANCED";
        if (index == 3) return "EXPERT";
        if (index == 4) return "MASTER";
        if (index == 5) return "Re:MASTER";
        if (index == 6) return "ORIGINAL";
        return "DEFAULT";
    }
}