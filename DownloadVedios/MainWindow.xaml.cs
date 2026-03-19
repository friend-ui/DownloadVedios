using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace DownloadVedios
{
    public partial class MainWindow : Window
    {
        private string _ytDlpPath;
        private string _selectedFormat = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            _ytDlpPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp", "yt-dlp.exe");
            PathTextBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            QualityComboBox.SelectionChanged += QualityComboBox_SelectionChanged;
        }

        private void ConfirmUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                LoadVideoFormats();
            }
        }

        private string GetQualityLabel(string resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return "未知";

            var match = System.Text.RegularExpressions.Regex.Match(resolution, @"(\d+)x(\d+)");
            if (match.Success)
            {
                var width = int.Parse(match.Groups[1].Value);
                var height = int.Parse(match.Groups[2].Value);

                if (height >= 1080)
                    return "高";
                else if (height >= 720)
                    return "中";
                else
                    return "低";
            }

            if (resolution.Contains("1080") || resolution.Contains("4K") || resolution.Contains("2160"))
                return "高";
            else if (resolution.Contains("720") || resolution.Contains("480"))
                return "中";
            else
                return "低";
        }

        private async void LoadVideoFormats()
        {
            try
            {
                QualityComboBox.IsEnabled = false;
                DownloadButton.IsEnabled = false;
                QualityComboBox.Items.Clear();
                StatusTextBlock.Visibility = Visibility.Collapsed;

                if (!File.Exists(_ytDlpPath))
                {
                    ShowStatus("yt-dlp.exe 未找到！", "Red");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = $"--list-formats \"{UrlTextBox.Text}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    ShowStatus("无法启动 yt-dlp 进程", "Red");
                    return;
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("[") || trimmedLine.StartsWith("ID"))
                            continue;

                        var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            var formatId = parts[0];
                            var extension = parts[1];
                            var resolution = parts[2];
                            
                            if (int.TryParse(formatId, out int id))
                            {
                                if (id < 100000)
                                {
                                    if (!string.IsNullOrEmpty(resolution) && 
                                        resolution != "audio" && 
                                        resolution != "video" && 
                                        !resolution.Contains("only"))
                                    {
                                        var qualityLabel = GetQualityLabel(resolution);
                                        var quality = $"{resolution} - {qualityLabel} ({extension}, {formatId})";
                                        QualityComboBox.Items.Add(new ComboBoxItem { Content = quality, Tag = formatId });
                                    }
                                }
                            }
                        }
                    }

                    if (QualityComboBox.Items.Count > 0)
                    {
                        QualityComboBox.IsEnabled = true;
                        ShowStatus($"解析完成，共 {QualityComboBox.Items.Count} 种画质格式", "Green");
                    }
                    else
                    {
                        ShowStatus("未找到可用的视频格式", "Red");
                    }
                }
                else
                {
                    ShowStatus($"获取视频格式失败 (退出码: {process.ExitCode})", "Red");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"发生异常: {ex.Message}", "Red");
            }
        }

        private void ShowStatus(string message, string color)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            StatusTextBlock.Visibility = Visibility.Visible;
        }

        private void QualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedFormat = item.Tag?.ToString() ?? string.Empty;
                DownloadButton.IsEnabled = !string.IsNullOrEmpty(_selectedFormat);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择下载目录",
                InitialDirectory = PathTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                PathTextBox.Text = dialog.FolderName;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PathTextBox.Text) && Directory.Exists(PathTextBox.Text))
            {
                Process.Start("explorer.exe", PathTextBox.Text);
            }
            else
            {
                ShowStatus("下载目录不存在", "Red");
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text) || string.IsNullOrWhiteSpace(_selectedFormat) || string.IsNullOrWhiteSpace(PathTextBox.Text))
            {
                ShowStatus("请填写完整信息", "Red");
                return;
            }

            try
            {
                DownloadButton.IsEnabled = false;
                DownloadButton.Content = "下载中...";
                DownloadProgressBar.Visibility = Visibility.Visible;
                OutputTextBox.Clear();
                ShowStatus("开始下载...", "Blue");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = $"-f \"{_selectedFormat}+bestaudio[ext=m4a]/{_selectedFormat}+bestaudio\" --merge-output-format mp4 -o \"{System.IO.Path.Combine(PathTextBox.Text, "%(title)s.%(ext)s")}\" \"{UrlTextBox.Text}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    ShowStatus("无法启动下载进程", "Red");
                    return;
                }

                var outputTask = Task.Run(async () =>
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                OutputTextBox.AppendText(line + "\n");
                                OutputTextBox.ScrollToEnd();
                            });
                        }
                    }
                });

                var errorTask = Task.Run(async () =>
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                OutputTextBox.AppendText(line + "\n");
                                OutputTextBox.ScrollToEnd();
                            });
                        }
                    }
                });

                await process.WaitForExitAsync();
                await Task.WhenAll(outputTask, errorTask);

                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "下载";

                if (process.ExitCode == 0)
                {
                    ShowStatus("下载完成！", "Green");
                }
                else
                {
                    ShowStatus($"下载失败 (退出码: {process.ExitCode})", "Red");
                }
            }
            catch (Exception ex)
            {
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "下载";
                ShowStatus($"下载失败: {ex.Message}", "Red");
            }
        }
    }
}