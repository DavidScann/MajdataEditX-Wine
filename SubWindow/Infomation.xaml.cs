using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MajSimai;
using Microsoft.Win32;

namespace MajdataEdit;

/// <summary>
///     Infomation.xaml 的交互逻辑
/// </summary>
public partial class Infomation : Window
{
    public Infomation()
    {
        InitializeComponent();
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        SimaiProcess.simaiFile.Title = TitleTextbox.Text;
        SimaiProcess.simaiFile.Artist = ArtistTextbox.Text;
        SimaiProcess.simaiFile.Commands.Clear();
        foreach (var str in OtherTextbox.Text.Split('\n'))
        {
            if (SimaiCommand.TryParse(str, out var cmd))
            {
                //SimaiProcess.simaiFile.Commands.Clear(); //这肯定不是我干的qwqqqqq
                SimaiProcess.simaiFile.Commands.Add(cmd);
            }
        }

        SimaiProcess.designers[MainWindow.selectedDifficulty] = DesignTextbox.Text;
        Close();
    }

    private void InfomationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TitleTextbox.Text = SimaiProcess.simaiFile.Title;
        ArtistTextbox.Text = SimaiProcess.simaiFile.Artist;
        DesignTextbox.Text = SimaiProcess.designers[MainWindow.selectedDifficulty];

        StringBuilder sb = new();
        foreach (var cmd in SimaiProcess.simaiFile.Commands)
            sb.Append("&" + cmd.Prefix + "=" + cmd.Value + "\n");
        OtherTextbox.Text = sb.ToString();

        LoadImageFromDefault();
        RenderOptions.SetBitmapScalingMode(SaltImage, BitmapScalingMode.HighQuality);
    }

    private void LoadImageFromDefault()
    {
        if (File.Exists(MainWindow.maidataDir + "/bg.png"))
            LoadImageFromByte(File.ReadAllBytes(MainWindow.maidataDir + "/bg.png"));
        else if (File.Exists(MainWindow.maidataDir + "/bg.jpg"))
            LoadImageFromByte(File.ReadAllBytes(MainWindow.maidataDir + "/bg.jpg"));
        else
            SaltImage.Source =
                new BitmapImage(new Uri("pack://application:,,,/MajdataEdit;component/Resources/bg_dummy.jpg"));
    }

    private void LoadImageFromByte(byte[] data)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(data);
        image.EndInit();
        SaltImage.Source = image;
    }

    private void ReadMetadata(string path)
    {
        var formattedPath = path;

        if (!path.EndsWith(".ogg") && !path.EndsWith(".mp3"))
        {
            var ext = ".mp3";

            if (File.Exists(path + ".ogg")) ext = ".ogg";

            formattedPath += ext;
        }

        var file = TagLib.File.Create(formattedPath);
        TitleTextbox.Text = file.Tag.Title;
        ArtistTextbox.Text = file.Tag.JoinedPerformers;
        if (file.Tag.Pictures.Length > 0)
        {
            var pic = file.Tag.Pictures[0];
            LoadImageFromByte(pic.Data.Data);
            var result = MessageBox.Show(this, MainWindow.GetLocalizedString("IsOverridePicture"),
                MainWindow.GetLocalizedString("Info"), MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes)
            {
                LoadImageFromDefault();
                return;
            }

            if (pic.MimeType.Contains("jpeg"))
            {
                DelBGs();
                File.WriteAllBytes(MainWindow.maidataDir + "/bg.jpg", pic.Data.Data);
            }

            if (pic.MimeType.Contains("png"))
            {
                DelBGs();
                File.WriteAllBytes(MainWindow.maidataDir + "/bg.png", pic.Data.Data);
            }

            Console.WriteLine(pic.MimeType);
        }
    }

    private void ReadTrackButton_Click(object sender, RoutedEventArgs e)
    {
        ReadMetadata(MainWindow.maidataDir + "/track");
    }

    private void ReadFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "*.mp3, *.ogg|*.mp3;*.ogg"
        };
        if (openFileDialog.ShowDialog(this) == true) ReadMetadata(openFileDialog.FileName);
    }

    private void SaltImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            InitialDirectory = "",
            Filter = "图片|*.png;*.jpg"
        };
        if (openFileDialog.ShowDialog(this) == true)
        {
            var data = File.ReadAllBytes(openFileDialog.FileName);
            var info = new FileInfo(openFileDialog.FileName);
            LoadImageFromByte(data);
            DelBGs();
            File.Copy(openFileDialog.FileName, MainWindow.maidataDir + "/bg" + info.Extension);
        }
    }

    private void DelBGs()
    {
        if (File.Exists(MainWindow.maidataDir + "/bg.png")) File.Delete(MainWindow.maidataDir + "/bg.png");
        if (File.Exists(MainWindow.maidataDir + "/bg.jpg")) File.Delete(MainWindow.maidataDir + "/bg.jpg");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}