using System.Windows;
using System.Windows.Input;

namespace StarkDynastyHelper
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        public void UpdateTimers(string foodText, string tuningText)
        {
            Dispatcher.Invoke(() =>
            {
                OverlayFoodTxt.Text = foodText;
                OverlayTuningTxt.Text = tuningText;
            });
        }
    }
}