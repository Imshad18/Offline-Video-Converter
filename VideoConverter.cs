using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UniversalVideoConverter
{
    public class MainForm : Form
    {
        private readonly ListBox fileList = new ListBox();
        private readonly ComboBox resolutionBox = new ComboBox();
        private readonly RadioButton sameFolderRadio = new RadioButton();
        private readonly RadioButton customFolderRadio = new RadioButton();
        private readonly TextBox outputFolderBox = new TextBox();
        private readonly Button browseOutputButton = new Button();
        private readonly Button addButton = new Button();
        private readonly Button removeButton = new Button();
        private readonly Button clearButton = new Button();
        private readonly Button convertButton = new Button();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label statusLabel = new Label();
        private readonly Label engineLabel = new Label();

        private string appDir;
        private string toolsDir;
        private string ffmpegPath;
        private string ffprobePath;
        private bool busy;

        private static readonly string[] VideoExtensions = new[]
        {
            ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v",
            ".mpeg", ".mpg", ".ts", ".mts", ".m2ts", ".flv", ".3gp",
            ".3g2", ".vob", ".ogv", ".asf", ".rm", ".rmvb", ".mxf"
        };

        public MainForm()
        {
            Text = "Universal Video → MP4";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(760, 560);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(246, 247, 249);
            AllowDrop = true;
            Icon = SystemIcons.Application;

            appDir = AppDomain.CurrentDomain.BaseDirectory;
            toolsDir = Path.Combine(appDir, "tools");
            ffmpegPath = Path.Combine(toolsDir, "ffmpeg.exe");
            ffprobePath = Path.Combine(toolsDir, "ffprobe.exe");

            BuildUi();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += async (s, e) => await EnsureEngineAsync();
        }

        private void BuildUi()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 86,
                BackColor = Color.FromArgb(31, 35, 41)
            };
            Controls.Add(header);

            var title = new Label
            {
                Text = "Universal Video → MP4",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 20F),
                AutoSize = true,
                Location = new Point(24, 14)
            };
            header.Controls.Add(title);

            var subtitle = new Label
            {
                Text = "Convert common video formats to standard MP4 (H.264 + AAC)",
                ForeColor = Color.FromArgb(205, 210, 216),
                AutoSize = true,
                Location = new Point(27, 55)
            };
            header.Controls.Add(subtitle);

            var filesLabel = new Label
            {
                Text = "Videos",
                Font = new Font("Segoe UI Semibold", 10F),
                AutoSize = true,
                Location = new Point(24, 106)
            };
            Controls.Add(filesLabel);

            fileList.Location = new Point(24, 132);
            fileList.Size = new Size(712, 150);
            fileList.HorizontalScrollbar = true;
            fileList.SelectionMode = SelectionMode.MultiExtended;
            fileList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(fileList);

            addButton.Text = "Add videos";
            addButton.Location = new Point(24, 292);
            addButton.Size = new Size(105, 32);
            addButton.Click += (s, e) => AddVideosWithDialog();
            Controls.Add(addButton);

            removeButton.Text = "Remove";
            removeButton.Location = new Point(137, 292);
            removeButton.Size = new Size(90, 32);
            removeButton.Click += (s, e) => RemoveSelected();
            Controls.Add(removeButton);

            clearButton.Text = "Clear";
            clearButton.Location = new Point(235, 292);
            clearButton.Size = new Size(80, 32);
            clearButton.Click += (s, e) => fileList.Items.Clear();
            Controls.Add(clearButton);

            var dropHint = new Label
            {
                Text = "You can also drag video files into this window.",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(330, 300)
            };
            Controls.Add(dropHint);

            var resLabel = new Label
            {
                Text = "Output resolution",
                Font = new Font("Segoe UI Semibold", 10F),
                AutoSize = true,
                Location = new Point(24, 344)
            };
            Controls.Add(resLabel);

            resolutionBox.DropDownStyle = ComboBoxStyle.DropDownList;
            resolutionBox.Items.AddRange(new object[]
            {
                "Original (no resize)",
                "2160p / 4K max",
                "1440p max",
                "1080p max",
                "720p max",
                "480p max"
            });
            resolutionBox.SelectedIndex = 0;
            resolutionBox.Location = new Point(180, 341);
            resolutionBox.Size = new Size(205, 28);
            Controls.Add(resolutionBox);

            var outputLabel = new Label
            {
                Text = "Save converted files",
                Font = new Font("Segoe UI Semibold", 10F),
                AutoSize = true,
                Location = new Point(24, 390)
            };
            Controls.Add(outputLabel);

            sameFolderRadio.Text = "Beside each source video";
            sameFolderRadio.Location = new Point(180, 387);
            sameFolderRadio.AutoSize = true;
            sameFolderRadio.Checked = true;
            sameFolderRadio.CheckedChanged += (s, e) => UpdateOutputControls();
            Controls.Add(sameFolderRadio);

            customFolderRadio.Text = "Use folder:";
            customFolderRadio.Location = new Point(392, 387);
            customFolderRadio.AutoSize = true;
            customFolderRadio.CheckedChanged += (s, e) => UpdateOutputControls();
            Controls.Add(customFolderRadio);

            outputFolderBox.Location = new Point(180, 420);
            outputFolderBox.Size = new Size(455, 27);
            outputFolderBox.Enabled = false;
            outputFolderBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(outputFolderBox);

            browseOutputButton.Text = "Browse";
            browseOutputButton.Location = new Point(643, 418);
            browseOutputButton.Size = new Size(93, 30);
            browseOutputButton.Enabled = false;
            browseOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseOutputButton.Click += (s, e) => BrowseOutputFolder();
            Controls.Add(browseOutputButton);

            convertButton.Text = "Convert to MP4";
            convertButton.Font = new Font("Segoe UI Semibold", 11F);
            convertButton.Location = new Point(24, 468);
            convertButton.Size = new Size(190, 42);
            convertButton.Enabled = false;
            convertButton.Click += async (s, e) => await ConvertAllAsync();
            Controls.Add(convertButton);

            progressBar.Location = new Point(230, 474);
            progressBar.Size = new Size(506, 24);
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(progressBar);

            statusLabel.Text = "Preparing conversion engine...";
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Location = new Point(230, 505);
            statusLabel.Size = new Size(506, 21);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(statusLabel);

            engineLabel.Text = "";
            engineLabel.ForeColor = Color.DimGray;
            engineLabel.Location = new Point(24, 527);
            engineLabel.Size = new Size(712, 20);
            engineLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(engineLabel);
        }

        private void UpdateOutputControls()
        {
            bool custom = customFolderRadio.Checked;
            outputFolderBox.Enabled = custom && !busy;
            browseOutputButton.Enabled = custom && !busy;
        }

        private void BrowseOutputFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose where converted MP4 files will be saved";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    outputFolderBox.Text = dialog.SelectedPath;
            }
        }

        private void AddVideosWithDialog()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose video files";
                dialog.Multiselect = true;
                dialog.Filter = "Video files|*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.webm;*.m4v;*.mpeg;*.mpg;*.ts;*.mts;*.m2ts;*.flv;*.3gp;*.3g2;*.vob;*.ogv;*.asf;*.rm;*.rmvb;*.mxf|All files|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                if (!File.Exists(file)) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!VideoExtensions.Contains(ext) && ext.Length > 0)
                {
                    // FFmpeg supports many formats; allow unknown extensions too when manually dragged/selected.
                }
                if (!fileList.Items.Contains(file))
                    fileList.Items.Add(file);
            }
        }

        private void RemoveSelected()
        {
            var selected = fileList.SelectedItems.Cast<object>().ToArray();
            foreach (var item in selected)
                fileList.Items.Remove(item);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) AddFiles(files);
        }

        private async Task EnsureEngineAsync()
        {
            Directory.CreateDirectory(toolsDir);
            if (File.Exists(ffmpegPath) && File.Exists(ffprobePath))
            {
                EngineReady();
                return;
            }

            convertButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            statusLabel.Text = "Downloading FFmpeg engine (first launch only)...";

            string zipPath = Path.Combine(Path.GetTempPath(), "uvc_ffmpeg.zip");
            string extractDir = Path.Combine(Path.GetTempPath(), "uvc_ffmpeg_extract");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                if (File.Exists(zipPath)) File.Delete(zipPath);
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);

                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "UniversalVideoConverter/1.0");
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        try
                        {
                            progressBar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                            statusLabel.Text = "Downloading FFmpeg engine... " + e.ProgressPercentage + "%";
                        }
                        catch { }
                    };
                    await wc.DownloadFileTaskAsync(new Uri("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"), zipPath);
                }

                statusLabel.Text = "Installing conversion engine...";
                progressBar.Style = ProgressBarStyle.Marquee;
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir));

                string foundFfmpeg = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                string foundFfprobe = Directory.GetFiles(extractDir, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (foundFfmpeg == null || foundFfprobe == null)
                    throw new Exception("FFmpeg files were not found in the downloaded package.");

                File.Copy(foundFfmpeg, ffmpegPath, true);
                File.Copy(foundFfprobe, ffprobePath, true);
                EngineReady();
            }
            catch (Exception ex)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                statusLabel.Text = "FFmpeg setup failed.";
                engineLabel.Text = "Internet is needed once on first launch. " + ex.Message;
                MessageBox.Show(this,
                    "The app could not download FFmpeg.\n\n" + ex.Message +
                    "\n\nCheck your internet connection and reopen the app.",
                    "FFmpeg setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
            }
        }

        private void EngineReady()
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            statusLabel.Text = "Ready.";
            engineLabel.Text = "Input: common video formats  •  Output: MP4 / H.264 video / AAC audio";
            convertButton.Enabled = true;
        }

        private async Task ConvertAllAsync()
        {
            if (busy) return;
            if (!File.Exists(ffmpegPath))
            {
                await EnsureEngineAsync();
                if (!File.Exists(ffmpegPath)) return;
            }
            if (fileList.Items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one video first.", "No videos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (customFolderRadio.Checked)
            {
                if (string.IsNullOrWhiteSpace(outputFolderBox.Text))
                {
                    MessageBox.Show(this, "Choose an output folder.", "Output folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Directory.CreateDirectory(outputFolderBox.Text);
            }

            var files = fileList.Items.Cast<string>().ToList();
            busy = true;
            SetBusyUi(true);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;

            int success = 0;
            var failures = new List<string>();
            string scaleFilter = GetScaleFilter();

            for (int i = 0; i < files.Count; i++)
            {
                string input = files[i];
                string output = BuildOutputPath(input);
                statusLabel.Text = string.Format("Converting {0} of {1}: {2}", i + 1, files.Count, Path.GetFileName(input));

                try
                {
                    double duration = await Task.Run(() => ProbeDurationSeconds(input));
                    bool ok = await Task.Run(() => ConvertOne(input, output, duration, i, files.Count, scaleFilter));
                    if (ok) success++;
                    else failures.Add(Path.GetFileName(input));
                }
                catch
                {
                    failures.Add(Path.GetFileName(input));
                }
            }

            busy = false;
            SetBusyUi(false);
            progressBar.Value = 100;
            statusLabel.Text = failures.Count == 0
                ? "Done. " + success + " file(s) converted."
                : "Finished: " + success + " converted, " + failures.Count + " failed.";

            if (failures.Count == 0)
            {
                MessageBox.Show(this, "Conversion complete.\n\n" + success + " file(s) converted to MP4.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    "Finished with some failures.\n\nConverted: " + success + "\nFailed: " + failures.Count +
                    "\n\nFailed files:\n" + string.Join("\n", failures.Take(10).ToArray()),
                    "Conversion finished", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetBusyUi(bool value)
        {
            addButton.Enabled = !value;
            removeButton.Enabled = !value;
            clearButton.Enabled = !value;
            resolutionBox.Enabled = !value;
            sameFolderRadio.Enabled = !value;
            customFolderRadio.Enabled = !value;
            convertButton.Enabled = !value;
            UpdateOutputControls();
        }

        private string BuildOutputPath(string input)
        {
            string folder = sameFolderRadio.Checked ? Path.GetDirectoryName(input) : outputFolderBox.Text;
            string baseName = Path.GetFileNameWithoutExtension(input);
            string suffix;
            switch (resolutionBox.SelectedIndex)
            {
                case 1: suffix = "_mp4_2160p"; break;
                case 2: suffix = "_mp4_1440p"; break;
                case 3: suffix = "_mp4_1080p"; break;
                case 4: suffix = "_mp4_720p"; break;
                case 5: suffix = "_mp4_480p"; break;
                default: suffix = "_mp4"; break;
            }

            string candidate = Path.Combine(folder, baseName + suffix + ".mp4");
            int n = 2;
            while (File.Exists(candidate) || string.Equals(candidate, input, StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.Combine(folder, baseName + suffix + "_" + n + ".mp4");
                n++;
            }
            return candidate;
        }

        private string GetScaleFilter()
        {
            int w = 0, h = 0;
            switch (resolutionBox.SelectedIndex)
            {
                case 1: w = 3840; h = 2160; break;
                case 2: w = 2560; h = 1440; break;
                case 3: w = 1920; h = 1080; break;
                case 4: w = 1280; h = 720; break;
                case 5: w = 854; h = 480; break;
                default: return null;
            }
            return "scale=w=min(" + w + "\\,iw):h=min(" + h + "\\,ih):force_original_aspect_ratio=decrease:force_divisible_by=2";
        }

        private double ProbeDurationSeconds(string input)
        {
            if (!File.Exists(ffprobePath)) return 0;
            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 " + Q(input),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string text = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                double seconds;
                return double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out seconds) ? seconds : 0;
            }
        }

        private bool ConvertOne(string input, string output, double duration, int fileIndex, int totalFiles, string scale)
        {
            string args = "-hide_banner -y -i " + Q(input) + " ";
            if (!string.IsNullOrEmpty(scale))
                args += "-vf " + Q(scale) + " ";
            args += "-map_metadata 0 -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p " +
                    "-c:a aac -b:a 192k -movflags +faststart -progress pipe:1 -nostats " + Q(output);

            var errors = new List<string>();
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = appDir
            };

            using (var p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (errors)
                        {
                            errors.Add(e.Data);
                            if (errors.Count > 30) errors.RemoveAt(0);
                        }
                    }
                };
                p.Start();
                p.BeginErrorReadLine();

                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (duration > 0 && line.StartsWith("out_time_ms=", StringComparison.Ordinal))
                    {
                        long micros;
                        if (long.TryParse(line.Substring("out_time_ms=".Length), out micros))
                        {
                            double current = micros / 1000000.0;
                            double thisFile = Math.Max(0, Math.Min(1, current / duration));
                            int overall = (int)Math.Round(((fileIndex + thisFile) / totalFiles) * 100.0);
                            BeginInvoke((Action)(() => progressBar.Value = Math.Max(0, Math.Min(100, overall))));
                        }
                    }
                }
                p.WaitForExit();
                return p.ExitCode == 0 && File.Exists(output);
            }
        }

        private static string Q(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
