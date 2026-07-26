using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using DrawingRect = System.Drawing.Rectangle;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace StarkDynastyHelper
{
    public partial class RoiSelectorWindow : Window
    {
        private WpfPoint startPoint;
        public DrawingRect SelectedRoi { get; private set; } = DrawingRect.Empty;

        public RoiSelectorWindow()
        {
            InitializeComponent();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                startPoint = e.GetPosition(this);
                SelectionBox.Width = 0;
                SelectionBox.Height = 0;
                Canvas.SetLeft(SelectionBox, startPoint.X);
                Canvas.SetTop(SelectionBox, startPoint.Y);
                SelectionBox.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                double x = Math.Min(pos.X, startPoint.X);
                double y = Math.Min(pos.Y, startPoint.Y);
                double w = Math.Abs(pos.X - startPoint.X);
                double h = Math.Abs(pos.Y - startPoint.Y);

                Canvas.SetLeft(SelectionBox, x);
                Canvas.SetTop(SelectionBox, y);
                SelectionBox.Width = w;
                SelectionBox.Height = h;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            double x = Canvas.GetLeft(SelectionBox);
            double y = Canvas.GetTop(SelectionBox);
            double w = SelectionBox.Width;
            double h = SelectionBox.Height;

            if (w > 20 && h > 10)
            {
                SelectedRoi = new DrawingRect((int)x, (int)y, (int)w, (int)h);
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }
    }
}