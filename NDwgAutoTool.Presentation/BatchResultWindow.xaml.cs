using NDwgAutoTool.Helpers;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace NDwgAutoTool
{
    public partial class BatchResultWindow : Window
    {
        public BatchResultWindow(
            string title,
            int processed,
            int succeeded,
            int failed,
            IEnumerable<string>? failedDrawings,
            Window? owner = null)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            ProcessedTextBlock.Text = processed.ToString();
            SucceededTextBlock.Text = succeeded.ToString();
            FailedTextBlock.Text = failed.ToString();

            var failedList = failedDrawings?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList() ?? new List<string>();

            FailedDrawingsTextBox.Text = failedList.Count == 0
                ? "No failed drawings."
                : string.Join("\r\n", failedList);

            CopyButton.IsEnabled = failedList.Count > 0;

            if (owner != null)
                Owner = owner;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FailedDrawingsTextBox.Text) &&
                FailedDrawingsTextBox.Text != "No failed drawings.")
            {
                Clipboard.SetText(FailedDrawingsTextBox.Text);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static void ShowResult(
            string title,
            int processed,
            int succeeded,
            int failed,
            IEnumerable<string>? failedDrawings,
            Window? owner = null)
        {
            var popup = new BatchResultWindow(title, processed, succeeded, failed, failedDrawings, owner);
            popup.ShowDialog();
        }

        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.ResizeFromBottomRight(this);
        }
    }
}
