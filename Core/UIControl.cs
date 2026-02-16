using System.Windows;
using System.Windows.Media.Imaging;

namespace MajdataEdit;

public partial class MainWindow : Window
{
    /// <summary>
    /// Clear/reset UI elements when closing a chart
    /// </summary>
    private void set_empty()
    {
        Dispatcher.Invoke(() =>
        {
            // Disable edit menu when no chart is loaded
            MenuEdit.IsEnabled = false;
            Menu_ToggleChartShare.IsEnabled = false;
            
            // Clear error count display
            ErrCount.Content = "0";
            
            // Clear time display
            TimeLabel.Content = "0:00";
            
            // Reset chart selector
            LevelSelector.SelectedIndex = -1;
        });
    }

    /// <summary>
    /// Update UI to reflect host status in chart sharing mode
    /// </summary>
    private void set_host(bool isHost)
    {
        Dispatcher.Invoke(() =>
        {
            if (isHost)
            {
                // Update menu text to show we're hosting
                Menu_ToggleChartShare.Header = GetLocalizedString("StopChartShare");
            }
            else
            {
                // Update menu text to show we can start hosting
                Menu_ToggleChartShare.Header = GetLocalizedString("StartChartShare");
            }
        });
    }

    /// <summary>
    /// Update UI to reflect chart sharing connection status
    /// </summary>
    private void set_share(bool isSharing)
    {
        Dispatcher.Invoke(() =>
        {
            if (isSharing)
            {
                // Disable connect menu when already connected
                Menu_ConnectChartShare.IsEnabled = false;
            }
            else
            {
                // Enable connect menu when disconnected
                Menu_ConnectChartShare.IsEnabled = true;
            }
        });
    }

    /// <summary>
    /// Initialize the waveform visualization bitmap
    /// </summary>
    private void init_wave()
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var width = (int)MusicWave.ActualWidth;
                var height = (int)MusicWave.ActualHeight;
                
                if (width > 0 && height > 0)
                {
                    WaveBitmap = new WriteableBitmap(width, height, 96, 96, 
                        System.Windows.Media.PixelFormats.Bgr32, null);
                    MusicWave.Source = WaveBitmap;
                }
            }
            catch
            {
                // Ignore initialization errors
            }
        });
    }

    /// <summary>
    /// Draw FFT (Fast Fourier Transform) visualization
    /// </summary>
    private void draw_fft()
    {
        // FFT visualization is currently a placeholder
        // The actual implementation would require FFT data from the audio stream
        // For now, this prevents the build error
    }

    /// <summary>
    /// Update the error count display
    /// </summary>
    private void set_err_count(object count)
    {
        Dispatcher.Invoke(() =>
        {
            ErrCount.Content = count?.ToString() ?? "0";
        });
    }

    /// <summary>
    /// Show or hide the loading indicator
    /// </summary>
    private void set_loading(bool loading)
    {
        Dispatcher.Invoke(() =>
        {
            Menu_ProcessStatus.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        });
    }
}
