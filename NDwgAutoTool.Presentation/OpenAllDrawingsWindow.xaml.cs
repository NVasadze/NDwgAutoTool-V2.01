using NDwgAutoTool.Helpers;
using System.Windows;
using System.Windows.Input;

namespace NDwgAutoTool
{
    public partial class OpenAllDrawingsWindow : Window
    {
        private static readonly char[] Separators = ['\r', '\n', '\t', ' ', ',', ';'];

        public OpenAllDrawingsWindow(Window? owner = null)
        {
            InitializeComponent();

            if (owner != null)
                Owner = owner;

            Loaded += (_, _) => DrawingNumbersTextBox.Focus();
        }

        public IReadOnlyList<string> DrawingNumbers { get; private set; } = Array.Empty<string>();

        public static IReadOnlyList<string>? Ask(Window? owner = null)
        {
            var popup = new OpenAllDrawingsWindow(owner);
            return popup.ShowDialog() == true ? popup.DrawingNumbers : null;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = ParseDrawingNumbers(DrawingNumbersTextBox.Text);

            if (drawingNumbers.Count == 0)
            {
                StyledMessageWindow.ShowMessage("Open All", "Paste at least one drawing number.", this);
                DrawingNumbersTextBox.Focus();
                return;
            }

            DrawingNumbers = drawingNumbers;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void WindowDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }

        private static IReadOnlyList<string> ParseDrawingNumbers(string text)
        {
            return text
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim().Trim('"', '\''))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
