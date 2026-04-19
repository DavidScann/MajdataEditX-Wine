using MajdataEdit.Utils;
using System.Windows;
using Un4seen.Bass;

namespace MajdataEdit;

// 文本控制部分
// 处理 FumenContent 中文本
// 和其与内部逻辑文本（RawFumen）的转换
// （内部完全用不含\r的文本来处理，牺牲一点性能换取牺牲一点可读性（bushi）

public partial class MainWindow : Window
{
    // 基础getter setter
    public string GetRawFumenText() => FumenContent.Text.Replace("\r", "");
    public void SetRawFumenText(string content) => 
        FumenContent.Text = content == null ? "" : content.Replace("\r", "");


    // 逻辑文本位置getter setter
    public int GetRawFumenPosition() => ToRawFumenPosition(FumenContent.CaretIndex);
    public void SetRawFumenPosition(int position) => FumenContent.Select(ToUiIndex(position), 0);
    public void SetRawFumenPosition(int positionX, int positionY)
    {
        if (positionX < 0 || positionY < 0)
        {
            FumenContent.CaretIndex = FumenContent.Text.Length - 1;
            return;
        }
        var text = FumenContent.Text;
        int currentLine = 0;
        int currentIndex = 0;
        int length = text.Length;

        while (currentLine < positionY && currentIndex <= length)
        {
            if (text[currentIndex] == '\n')
                currentLine++;
            currentIndex++;
        }

        int finalIndex = currentIndex + positionX;

        FumenContent.CaretIndex = finalIndex;
        ScrollToCaret();
    }

    public void ScrollToCaret()
    {
        Rect rect = FumenContent.GetRectFromCharacterIndex(FumenContent.CaretIndex, true);

        if (rect != Rect.Empty)
        {
            // 水平没有必要
            FumenContent.ScrollToVerticalOffset(rect.Top + FumenContent.VerticalOffset - 10);
        }
    }

    // 导航到当前位置
    public void SeekTextFromCurTime()
    {
        var time = Bass.BASS_ChannelBytes2Seconds(bgmStream, Bass.BASS_ChannelGetPosition(bgmStream));
        SeekTextFromTime(time);
    }

    // 依据时间挪光标
    public void SeekTextFromTime(double time)
    {
        var timingList = SimaiProcess.timingLists[selectedDifficulty];
        if (timingList.Count == 0) return;

        var theNote = timingList.MinBy(x => Math.Abs(time - x.Timing));
        SetRawFumenPosition(theNote?.RawTextPosition ?? 0);
    }

    public void SeekTextFromNoteOffset(int offset)
    {
        var timingList = SimaiProcess.noteLists[selectedDifficulty];
        if (timingList.Count == 0) return;

        var targetPos = GetRawFumenPosition();

        var indexed = timingList
            .Select((x, i) => (Value: x, Index: i))
            .MinBy(x => Math.Abs(x.Value.RawTextPosition - targetPos));

        if (indexed.Index + offset < timingList.Count)
        {
            var theNote = timingList[indexed.Index + offset];
            SetRawFumenPosition(theNote.RawTextPosition);
        }
    }

    public void ApplyMirror(Mirror.HandleType handleType)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.SelectedText, handleType);
        FumenContent.SelectedText = result;
    }

    public void ApplySubDevide(float multiplier)
    {
        var result = SubDivide.Subdivide(FumenContent.SelectedText, multiplier);
        FumenContent.SelectedText = result;
    }


    //////////////////// Helper Functions ////////////////////


    private int ToRawFumenPosition(int uiIndex)
    {
        string text = FumenContent.Text;

        if (string.IsNullOrEmpty(text)) return 0;
        if (uiIndex > text.Length) uiIndex = text.Length;
        if (uiIndex < 0) return 0;

        int rCount = 0;
        // 遍历到当前光标位置，统计有多少个 \r
        for (int i = 0; i < uiIndex; i++)
        {
            if (text[i] == '\r')
            {
                rCount++;
            }
        }

        return uiIndex - rCount;
    }

    private int ToUiIndex(int rawFumenPosition)
    {
        string text = FumenContent.Text;

        if (string.IsNullOrEmpty(text) || rawFumenPosition < 0) return 0;

        int currentRawCount = 0;
        int uiIndex = 0;

        while (uiIndex < text.Length && currentRawCount < rawFumenPosition)
        {
            // 如果遇到 \r，只增加 UI 索引（跳过它），逻辑计数不变
            if (text[uiIndex] == '\r')
                uiIndex++;
            else
            {
                // 普通字符（包括 \n）：UI 走一步，逻辑计数也走一步
                uiIndex++;
                currentRawCount++;
            }
        }

        return uiIndex;
    }
}
