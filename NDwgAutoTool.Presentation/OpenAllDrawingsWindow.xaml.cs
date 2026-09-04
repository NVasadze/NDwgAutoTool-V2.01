using NDwgAutoTool.Helpers;
using NDwgAutoTool.Infrastructure.Settings;
using System.Windows;
using System.Windows.Input;

namespace NDwgAutoTool
{
    public partial class OpenAllDrawingsWindow : Window
    {
        private static readonly char[] Separators = ['\r', '\n', '\t', ' ', ',', ';'];

        private bool _showDocumentTypeOptions = true;
        private string _emptyInputMessage = "Paste at least one drawing, container, or model number.";

        public OpenAllDrawingsWindow(Window? owner = null)
        {
            InitializeComponent();

            if (owner != null)
                Owner = owner;

            LoadSavedOptions();
            Loaded += (_, _) => DrawingNumbersTextBox.Focus();
        }

        public OpenAllRequest Request { get; private set; } =
            new(Array.Empty<string>(), new OpenAllDocumentSelection(true, false, false));

        public IReadOnlyList<string> DrawingNumbers { get; private set; } = Array.Empty<string>();

        public static OpenAllRequest? Ask(Window? owner = null)
        {
            var popup = new OpenAllDrawingsWindow(owner);
            return popup.ShowDialog() == true ? popup.Request : null;
        }

        public static IReadOnlyList<string>? AskDrawingNumbersOnly(
            Window? owner = null,
            string title = "Drawing Numbers",
            string primaryButtonText = "OK")
        {
            var popup = new OpenAllDrawingsWindow(owner)
            {
                Title = title,
                _showDocumentTypeOptions = false,
                _emptyInputMessage = "Paste at least one drawing number.",
                Height = 360,
                MinHeight = 320
            };

            popup.DialogTitleTextBlock.Text = title;
            popup.InputLabelTextBlock.Text = "Drawing number(s)";
            popup.PrimaryButton.Content = primaryButtonText;
            popup.OptionsLabelTextBlock.Visibility = Visibility.Collapsed;
            popup.OptionsPanel.Visibility = Visibility.Collapsed;
            popup.OptionsTopSpacerRow.Height = new GridLength(0);
            popup.OptionsBottomSpacerRow.Height = new GridLength(0);

            return popup.ShowDialog() == true ? popup.DrawingNumbers : null;
        }

        private void LoadSavedOptions()
        {
            var settings = UserSettingsStore.Load();

            DrawingsCheckBox.IsChecked = settings.OpenAll.Drawings;
            ContainersCheckBox.IsChecked = settings.OpenAll.Containers;
            ModelsCheckBox.IsChecked = settings.OpenAll.Models;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = ParseDrawingNumbers(DrawingNumbersTextBox.Text);

            if (drawingNumbers.Count == 0)
            {
                StyledMessageWindow.ShowMessage(Title, _emptyInputMessage, this);
                DrawingNumbersTextBox.Focus();
                return;
            }

            DrawingNumbers = drawingNumbers;

            if (!_showDocumentTypeOptions)
            {
                DialogResult = true;
                Close();
                return;
            }

            var selection = new OpenAllDocumentSelection(
                DrawingsCheckBox.IsChecked == true,
                ContainersCheckBox.IsChecked == true,
                ModelsCheckBox.IsChecked == true);

            if (selection.SelectedCount == 0)
            {
                StyledMessageWindow.ShowMessage(Title, "Select at least one document type.", this);
                return;
            }

            var settings = UserSettingsStore.Load();
            settings.OpenAll = new OpenAllPreferences
            {
                Drawings = selection.Drawings,
                Containers = selection.Containers,
                Models = selection.Models
            };
            UserSettingsStore.Save(settings);

            Request = new OpenAllRequest(drawingNumbers, selection);
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

    public sealed record OpenAllRequest(
        IReadOnlyList<string> Numbers,
        OpenAllDocumentSelection Selection);

    public sealed record OpenAllDocumentSelection(
        bool Drawings,
        bool Containers,
        bool Models)
    {
        public int SelectedCount =>
            (Drawings ? 1 : 0) +
            (Containers ? 1 : 0) +
            (Models ? 1 : 0);

        public string Description
        {
            get
            {
                var selected = new List<string>();

                if (Drawings)
                    selected.Add("drawings");

                if (Containers)
                    selected.Add("containers");

                if (Models)
                    selected.Add("models");

                return string.Join(", ", selected);
            }
        }
    }
}
