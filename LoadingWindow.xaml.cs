using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace StarkDynastyHelper
{
    public partial class LoadingWindow : Window
    {
        public bool IsSuccess { get; private set; } = false;
        private readonly string _licenseKey;

        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StarkHelper");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "stark_config.json");

        public LoadingWindow(string licenseKey)
        {
            InitializeComponent();
            _licenseKey = licenseKey;
            LoadPb.IsIndeterminate = true;
            _ = RunServerVerificationAsync();
        }

        private async Task RunServerVerificationAsync()
        {
            try
            {
                UpdateStatus("Подключение к защищенному серверу Stark...", 20);
                await Task.Delay(1000);

                string serverUrl = $"https://raw.githubusercontent.com/lovernyi/stark_keys/main/keys.json?t={DateTime.Now.Ticks}";
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                string json = await client.GetStringAsync(serverUrl);

                UpdateStatus("Проверка подлинности ключа доступа...", 60);
                using JsonDocument doc = JsonDocument.Parse(json);

                bool keyValid = false;
                if (doc.RootElement.TryGetProperty("keys", out JsonElement keysElement))
                {
                    if (keysElement.TryGetProperty(_licenseKey, out JsonElement keyData))
                    {
                        bool isActive = keyData.GetProperty("active").GetBoolean();
                        if (isActive) keyValid = true;
                    }
                }

                await Task.Delay(800);

                if (keyValid)
                {
                    UpdateStatus("Ключ успешно верифицирован! Запуск...", 100);
                    await Task.Delay(500);
                    IsSuccess = true;
                }
                else
                {
                    UpdateStatus("❌ Ошибка: Неверный или заблокированный ключ!", 100);
                    await Task.Delay(2000);
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("⚠️ Ошибка соединения с сервером авторизации!", 100);
                await Task.Delay(2000);
                IsSuccess = false;
            }

            Dispatcher.Invoke(() => Close());
        }

        private void UpdateStatus(string text, int progress)
        {
            Dispatcher.Invoke(() => StatusTxt.Text = text);
        }
    }
}