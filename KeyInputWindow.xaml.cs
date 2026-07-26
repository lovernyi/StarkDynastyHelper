using System.Windows;

namespace StarkDynastyHelper
{
    public partial class KeyInputWindow : Window
    {
        public string EnteredKey { get; private set; } = "";

        public KeyInputWindow()
        {
            InitializeComponent();
        }

        private void ActivateBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyInputTxt.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                System.Windows.MessageBox.Show("Введите ключ доступа!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EnteredKey = key;
            DialogResult = true;
            Close();
        }
    }
}