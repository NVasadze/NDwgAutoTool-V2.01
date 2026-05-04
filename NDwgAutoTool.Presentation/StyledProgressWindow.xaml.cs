using System.Windows;
using NDwgAutoTool.Helpers;


namespace NDwgAutoTool
{
    public partial class StyledProgressWindow : Window
    {
        public bool WasClosedByUser { get; private set; }

        public StyledProgressWindow(string title, Window? owner = null)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;

            if (owner != null)
                Owner = owner;
        }

        public void UpdateProgress(string status, string currentItem, int currentIndex, int totalCount)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = status;
                CurrentItemTextBlock.Text = "Current: " + currentItem;
                CountTextBlock.Text = $"{currentIndex} / {totalCount}";

                double percent = totalCount <= 0 ? 0 : (double)currentIndex / totalCount * 100.0;
                MainProgressBar.Value = percent;
                PercentTextBlock.Text = $"{Math.Round(percent)}%";
            });
        }

        public void MarkComplete(string message, int totalCount)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = message;
                CurrentItemTextBlock.Text = "Current: Done";
                CountTextBlock.Text = $"{totalCount} / {totalCount}";
                MainProgressBar.Value = 100;
                PercentTextBlock.Text = "100%";
                CancelButton.Content = "OK";
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WasClosedByUser = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            WasClosedByUser = true;
            Close();
        }

        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }
    }
}