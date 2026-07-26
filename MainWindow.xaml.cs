using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarkDynastyHelper
{
    public class SaveData
    {
        public int TotalContracts { get; set; }
        public int TodayContracts { get; set; }
        public string LastDate { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public string BoundHwid { get; set; } = "";
        public Rectangle CustomRoi { get; set; } = Rectangle.Empty;
        public string TgChatId { get; set; } = "";
        public bool EnableTelegram { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowOverlay { get; set; } = false;
        public string CustomSoundPath { get; set; } = "";
        public int SuccessfulScans { get; set; } = 0;
        public int TotalScans { get; set; } = 0;
        public double FoodRemainingSeconds { get; set; } = 0;
        public double TuningRemainingSeconds { get; set; } = 0;
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll")] private static extern bool IsDebuggerPresent();

        private bool isScanning = false;
        private bool isMuted = false;
        private CancellationTokenSource? scanCts;

        private bool isFoodActive = false;
        private bool isTuningActive = false;

        private DateTime? foodStartTime;
        private DateTime? tuningStartTime;
        private DateTime? foodAvailableTime;
        private DateTime? tuningAvailableTime;

        private int totalContracts = 0;
        private int todayContracts = 0;
        private int successfulScans = 0;
        private int totalScans = 0;

        private Rectangle customRoi = Rectangle.Empty;
        private string activeLicenseKey = "";
        private string boundHwid = "";
        private string tgChatId = "";
        private bool enableTelegram = false;
        private bool minimizeToTray = true;
        private bool showOverlay = false;
        private string customSoundPath = "";

        private NotifyIcon? notifyIcon;
        private OverlayWindow? overlayWin;
        private string newsTitleCache = "Загрузка новостей...";
        private string newsTextCache = "Пожалуйста, подождите...";

        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StarkHelper");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "stark_config.json");
        private const string LOGS_DIR = "logs";

        public MainWindow()
        {
            InitializeComponent();
            RunAutoInstaller();
            RunSecurityChecks();
            CleanOldLogs();
            InitTrayIcon();
            LoadSavedData();

            if (string.IsNullOrWhiteSpace(activeLicenseKey))
            {
                KeyInputWindow keyWin = new KeyInputWindow();
                if (keyWin.ShowDialog() == true)
                {
                    activeLicenseKey = keyWin.EnteredKey;
                    boundHwid = GetHWID();
                    SaveDataToFile();
                }
                else { Environment.Exit(0); }
            }

            LoadingWindow loadWin = new LoadingWindow(activeLicenseKey);
            loadWin.ShowDialog();

            if (!loadWin.IsSuccess)
            {
                System.Windows.MessageBox.Show("Ошибка авторизации! Программа будет закрыта.", "Stark Security", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }

            // Открываем вкладку новостей сразу при старте и заполняем их
            ShowView(ViewNews);
            NewsTitleView.Text = newsTitleCache;
            NewsTextView.Text = newsTextCache;

            if (showOverlay)
            {
                overlayWin = new OverlayWindow();
                overlayWin.Show();
            }

            AddLog($"HWID: {GetHWID()}");
            AddLog("Запуск STARK HELPER [Stark Dynasty v3.0]...");

            _ = StartAppPipelineAsync();
            StartCooldownTimerLoop();
            RegisterGlobalHotkeys();
            StartMidnightResetTimer();
        }

        private void RunAutoInstaller()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                if (!Directory.Exists(LOGS_DIR)) Directory.CreateDirectory(LOGS_DIR);
            }
            catch { }
        }

        private void ShowView(UIElement targetView)
        {
            ViewContracts.Visibility = Visibility.Collapsed;
            ViewNews.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewProfile.Visibility = Visibility.Collapsed;

            targetView.Visibility = Visibility.Visible;
        }

        private void NavContracts_Click(object sender, RoutedEventArgs e) => ShowView(ViewContracts);
        private void NavNews_Click(object sender, RoutedEventArgs e)
        {
            NewsTitleView.Text = newsTitleCache;
            NewsTextView.Text = newsTextCache;
            ShowView(ViewNews);
        }
        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            EnableTgChk.IsChecked = enableTelegram;
            TgChatIdTxt.Text = tgChatId;
            TrayChk.IsChecked = minimizeToTray;
            OverlayChk.IsChecked = showOverlay;
            ShowView(ViewSettings);
        }
        private void NavProfile_Click(object sender, RoutedEventArgs e)
        {
            ProfileKeyTxt.Text = $"Ключ: {activeLicenseKey}";
            ProfileHwidTxt.Text = $"HWID: {GetHWID()}";
            ShowView(ViewProfile);
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            tgChatId = TgChatIdTxt.Text.Trim();
            enableTelegram = EnableTgChk.IsChecked == true;
            minimizeToTray = TrayChk.IsChecked == true;
            showOverlay = OverlayChk.IsChecked == true;

            SaveDataToFile();
            AddLog("⚙️ Настройки сохранены.");
            ShowView(ViewContracts);
        }

        private void SelectRoiInteractive_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Thread.Sleep(300);
            RoiSelectorWindow roiWin = new RoiSelectorWindow();
            if (roiWin.ShowDialog() == true)
            {
                customRoi = roiWin.SelectedRoi;
                SaveDataToFile();
                AddLog($"🎯 Зона ROI обновлена: X={customRoi.X}, Y={customRoi.Y}, W={customRoi.Width}, H={customRoi.Height}");
            }
            Show();
        }

        private void SelectCustomSound_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Audio Files (*.wav)|*.wav" };
            if (dlg.ShowDialog() == true)
            {
                customSoundPath = dlg.FileName;
                SaveDataToFile();
                AddLog("🎵 Пользовательский звук успешно установлен.");
            }
        }

        private void OverlayChk_Checked(object sender, RoutedEventArgs e)
        {
            showOverlay = true;
            if (overlayWin == null) { overlayWin = new OverlayWindow(); overlayWin.Show(); }
        }

        private void OverlayChk_Unchecked(object sender, RoutedEventArgs e)
        {
            showOverlay = false;
            overlayWin?.Close();
            overlayWin = null;
        }

        private async Task StartAppPipelineAsync() => await StartScanAsync();

        private void AddLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogTxt.Text += $"\n[{DateTime.Now:HH:mm:ss}] {msg}";
                LogScroll.ScrollToEnd();
            });
        }

        private void RunSecurityChecks()
        {
            if (IsDebuggerPresent()) Environment.Exit(0);
        }

        private void CleanOldLogs()
        {
            try
            {
                if (!Directory.Exists(LOGS_DIR)) Directory.CreateDirectory(LOGS_DIR);
                foreach (var file in Directory.GetFiles(LOGS_DIR, "*.txt"))
                    if (File.GetCreationTime(file) < DateTime.Now.AddDays(-3)) File.Delete(file);
            }
            catch { }
        }

        private async void StartMidnightResetTimer()
        {
            while (true)
            {
                DateTime now = DateTime.Now;
                await Task.Delay(now.Date.AddDays(1) - now);
                todayContracts = 0;
                Dispatcher.Invoke(() => TodayStatsTxt.Text = "0");
                SaveDataToFile();
            }
        }

        private void InitTrayIcon()
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            try
            {
                notifyIcon = new NotifyIcon { Icon = SystemIcons.Shield, Visible = true, Text = "Stark Helper" };
                notifyIcon.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
            }
            catch { }
        }

        private void SaveDataToFile()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                double foodSec = foodAvailableTime.HasValue ? (foodAvailableTime.Value - DateTime.Now).TotalSeconds : 0;
                double tuningSec = tuningAvailableTime.HasValue ? (tuningAvailableTime.Value - DateTime.Now).TotalSeconds : 0;

                var data = new SaveData
                {
                    TotalContracts = totalContracts,
                    TodayContracts = todayContracts,
                    LastDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    LicenseKey = activeLicenseKey,
                    BoundHwid = boundHwid,
                    CustomRoi = customRoi,
                    TgChatId = tgChatId,
                    EnableTelegram = enableTelegram,
                    MinimizeToTray = minimizeToTray,
                    ShowOverlay = showOverlay,
                    CustomSoundPath = customSoundPath,
                    SuccessfulScans = successfulScans,
                    TotalScans = totalScans,
                    FoodRemainingSeconds = foodSec > 0 ? foodSec : 0,
                    TuningRemainingSeconds = tuningSec > 0 ? tuningSec : 0
                };
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadSavedData()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(ConfigFile));
                    if (data != null)
                    {
                        totalContracts = data.TotalContracts;
                        activeLicenseKey = data.LicenseKey ?? "";
                        boundHwid = data.BoundHwid ?? "";
                        customRoi = data.CustomRoi;
                        tgChatId = data.TgChatId ?? "";
                        enableTelegram = data.EnableTelegram;
                        minimizeToTray = data.MinimizeToTray;
                        showOverlay = data.ShowOverlay;
                        customSoundPath = data.CustomSoundPath ?? "";
                        successfulScans = data.SuccessfulScans;
                        totalScans = data.TotalScans;
                        todayContracts = data.LastDate == DateTime.Now.ToString("yyyy-MM-dd") ? data.TodayContracts : 0;

                        if (data.FoodRemainingSeconds > 0) foodAvailableTime = DateTime.Now.AddSeconds(data.FoodRemainingSeconds);
                        if (data.TuningRemainingSeconds > 0) tuningAvailableTime = DateTime.Now.AddSeconds(data.TuningRemainingSeconds);

                        TotalStatsTxt.Text = $"{totalContracts}";
                        TodayStatsTxt.Text = $"{todayContracts}";
                        UpdateEfficiencyUi();
                    }
                }
            }
            catch { }
        }

        private void UpdateEfficiencyUi()
        {
            if (totalScans == 0) return;
            EfficiencyTxt.Text = $"{(double)successfulScans / totalScans * 100.0:F0}%";
        }

        private async Task StartScanAsync()
        {
            if (isScanning) return;
            isScanning = true;
            StatusTxt.Text = "🎮 Автоматическое отслеживание АКТИВНО";
            scanCts = new CancellationTokenSource();
            await Task.Run(() => StartScanLoop(scanCts.Token), scanCts.Token);
        }

        private async Task StartScanLoop(CancellationToken token)
        {
            var ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("ru")) ?? Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null) return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    IntPtr hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out uint pid);
                    bool isGtaActive = Process.GetProcessById((int)pid).ProcessName.ToLower().Contains("gta");

                    if (!isGtaActive) { await Task.Delay(1000, token); continue; }

                    Rectangle area = customRoi != Rectangle.Empty ? customRoi : new Rectangle(0, (int)(SystemParameters.PrimaryScreenHeight * 0.50), (int)SystemParameters.PrimaryScreenWidth, (int)(SystemParameters.PrimaryScreenHeight * 0.50));
                    using (Bitmap rawBmp = new Bitmap(area.Width, area.Height))
                    {
                        using (Graphics g = Graphics.FromImage(rawBmp)) g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            rawBmp.Save(ms, ImageFormat.Bmp); ms.Position = 0;
                            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
                            totalScans++;
                            string text = (await ocrEngine.RecognizeAsync(await decoder.GetSoftwareBitmapAsync())).Text.ToLower();
                            if (!string.IsNullOrWhiteSpace(text)) Dispatcher.Invoke(() => ProcessOcrText(text));
                        }
                    }
                    await Task.Delay(200, token);
                }
                catch { }
            }
        }

        private void ProcessOcrText(string text)
        {
            if (text.Contains("курьер") || text.Contains("фастфуд"))
            {
                if (!isFoodActive && foodAvailableTime == null && (text.Contains("начали") || text.Contains("списано"))) StartFoodContract();
                else if (isFoodActive && (text.Contains("завершили") || text.Contains("сдали"))) FinishFoodContract();
            }
            if (text.Contains("тюнинг"))
            {
                if (!isTuningActive && tuningAvailableTime == null && (text.Contains("начали") || text.Contains("списано"))) StartTuningContract();
                else if (isTuningActive && (text.Contains("завершили") || text.Contains("сдали"))) FinishTuningContract();
            }
        }

        private void StartFoodContract()
        {
            if (foodAvailableTime != null) return;
            isFoodActive = true; foodStartTime = DateTime.Now;
            FoodStatusTxt.Text = "🟢 Выполняется";
            AddLog("🍔 Начало: Курьер фастфуда"); PlayAlert();
        }

        private void FinishFoodContract()
        {
            if (foodAvailableTime != null) return;
            isFoodActive = false; string t = GetTimeSpentString(foodStartTime);
            foodAvailableTime = DateTime.Now.AddMinutes(90); foodStartTime = null;
            FoodStatusTxt.Text = $"✅ Завершен!{t}";
            successfulScans++; IncrementStats(); SaveDataToFile();
            AddLog($"✅ Сдан: Курьер фастфуда{t}"); PlayAlert();
        }

        private void StartTuningContract()
        {
            if (tuningAvailableTime != null) return;
            isTuningActive = true; tuningStartTime = DateTime.Now;
            TuningStatusTxt.Text = "🟢 Выполняется";
            AddLog("🔧 Начало: Тюнинг II"); PlayAlert();
        }

        private void FinishTuningContract()
        {
            if (tuningAvailableTime != null) return;
            isTuningActive = false; string t = GetTimeSpentString(tuningStartTime);
            tuningAvailableTime = DateTime.Now.AddHours(2); tuningStartTime = null;
            TuningStatusTxt.Text = $"✅ Завершен!{t}";
            successfulScans++; IncrementStats(); SaveDataToFile();
            AddLog($"✅ Сдан: Тюнинг II{t}"); PlayAlert();
        }

        private async void StartCooldownTimerLoop()
        {
            while (true)
            {
                string foodStr = "Готов";
                string tuningStr = "Готов";

                if (foodAvailableTime.HasValue)
                {
                    TimeSpan rem = foodAvailableTime.Value - DateTime.Now;
                    if (rem.TotalSeconds <= 0)
                    {
                        foodAvailableTime = null; FoodCdTxt.Text = "Готов!"; FoodPb.Value = 100; PlayAlert();
                    }
                    else
                    {
                        FoodCdTxt.Text = $"{rem.Hours}ч {rem.Minutes}м {rem.Seconds}с";
                        FoodPb.Value = (5400 - rem.TotalSeconds) / 5400.0 * 100;
                        foodStr = $"{rem.Minutes}м";
                    }
                }
                if (tuningAvailableTime.HasValue)
                {
                    TimeSpan rem = tuningAvailableTime.Value - DateTime.Now;
                    if (rem.TotalSeconds <= 0)
                    {
                        tuningAvailableTime = null; TuningCdTxt.Text = "Готов!"; TuningPb.Value = 100; PlayAlert();
                    }
                    else
                    {
                        TuningCdTxt.Text = $"{rem.Hours}ч {rem.Minutes}м {rem.Seconds}с";
                        TuningPb.Value = (7200 - rem.TotalSeconds) / 7200.0 * 100;
                        tuningStr = $"{rem.Minutes}м";
                    }
                }

                overlayWin?.UpdateTimers($"Фастфуд: {foodStr}", $"Тюнинг: {tuningStr}");
                await Task.Delay(1000);
            }
        }

        private void PlayAlert()
        {
            if (!isMuted)
            {
                try
                {
                    if (!string.IsNullOrEmpty(customSoundPath) && File.Exists(customSoundPath))
                        new System.Media.SoundPlayer(customSoundPath).Play();
                    else
                        System.Media.SystemSounds.Beep.Play();
                }
                catch { System.Media.SystemSounds.Beep.Play(); }
            }
        }

        private string GetTimeSpentString(DateTime? s) => s.HasValue ? (DateTime.Now - s.Value).Minutes > 0 ? $" ({(DateTime.Now - s.Value).Minutes}м {(DateTime.Now - s.Value).Seconds}с)" : $" ({(DateTime.Now - s.Value).Seconds}с)" : "";
        private void IncrementStats() { totalContracts++; todayContracts++; TotalStatsTxt.Text = $"{totalContracts}"; TodayStatsTxt.Text = $"{todayContracts}"; UpdateEfficiencyUi(); }

        private string GetHWID() { try { using var s = new ManagementObjectSearcher("Select ProcessorId From Win32_Processor"); foreach (ManagementObject m in s.Get()) return Regex.Replace(m["ProcessorId"]?.ToString() ?? "", @"\s+", ""); } catch { } return "DEFAULT-HWID"; }
        private void RegisterGlobalHotkeys() { try { RegisterHotKey(new WindowInteropHelper(this).Handle, 9001, 0, 0x74); } catch { } }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) { if (minimizeToTray) Hide(); else { notifyIcon?.Dispose(); Close(); } }
        private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void PinBtn_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            PinBtn.Foreground = Topmost ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.HotPink) : System.Windows.Media.Brushes.White;
        }
        private void MuteBtn_Click(object sender, RoutedEventArgs e) { isMuted = !isMuted; MuteBtn.Content = isMuted ? "🔇" : "🔊"; }

        private void ManualStartFood_Click(object sender, RoutedEventArgs e) => StartFoodContract();
        private void ManualFinishFood_Click(object sender, RoutedEventArgs e) => FinishFoodContract();
        private void ManualStartTuning_Click(object sender, RoutedEventArgs e) => StartTuningContract();
        private void ManualFinishTuning_Click(object sender, RoutedEventArgs e) => FinishTuningContract();

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            totalContracts = 0; todayContracts = 0; successfulScans = 0; totalScans = 0;
            TotalStatsTxt.Text = "0"; TodayStatsTxt.Text = "0"; FoodCdTxt.Text = "Готов"; TuningCdTxt.Text = "Готов";
            FoodPb.Value = 0; TuningPb.Value = 0; EfficiencyTxt.Text = "100%"; SaveDataToFile(); AddLog("Сброшено.");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { SaveDataToFile(); notifyIcon?.Dispose(); overlayWin?.Close(); base.OnClosing(e); }
    }
}