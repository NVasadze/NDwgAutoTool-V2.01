using NDwgAutoTool.Helpers;
using System.Windows;
using System.Windows.Controls;

namespace NDwgAutoTool
{
    public partial class StyledBlockChoiceWindow : Window
    {
        private readonly bool _allowMultiple;

        public List<string> SelectedBlockFiles { get; private set; } = new List<string>();

        public StyledBlockChoiceWindow(IEnumerable<string> blockFiles, Window? owner = null)
            : this(
                blockFiles,
                "Insert Optional Blocks",
                "Select optional block(s) to insert.",
                true,
                owner)
        {
        }

        public StyledBlockChoiceWindow(
            IEnumerable<string> blockFiles,
            string title,
            string instruction,
            bool allowMultiple,
            Window? owner = null)
        {
            InitializeComponent();

            _allowMultiple = allowMultiple;
            Title = title;
            TitleTextBlock.Text = title;
            InstructionTextBlock.Text = instruction;
            NoButton.Content = allowMultiple ? "None" : "Cancel";

            if (owner != null)
                Owner = owner;

            foreach (string blockFile in blockFiles.OrderBy(x => x))
            {
                var checkBox = new CheckBox
                {
                    Content = blockFile,
                    Style = TryFindResource("DialogCheckBoxStyle") as Style
                };

                if (!allowMultiple)
                    checkBox.Checked += SingleChoiceCheckBox_Checked;

                BlockListPanel.Children.Add(checkBox);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedBlockFiles = BlockListPanel.Children
                .OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Content?.ToString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!_allowMultiple && SelectedBlockFiles.Count == 0)
            {
                StyledMessageWindow.ShowMessage("Insert Block", "Select one block to insert.", this);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedBlockFiles = new List<string>();
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedBlockFiles = new List<string>();
            DialogResult = false;
            Close();
        }

        public static List<string> ShowChoice(IEnumerable<string> blockFiles, Window? owner = null)
        {
            var popup = new StyledBlockChoiceWindow(blockFiles, owner);
            return popup.ShowDialog() == true ? popup.SelectedBlockFiles : new List<string>();
        }

        public static string? ShowSingleChoice(
            IEnumerable<string> blockFiles,
            string title,
            string instruction,
            Window? owner = null)
        {
            var popup = new StyledBlockChoiceWindow(blockFiles, title, instruction, false, owner);
            return popup.ShowDialog() == true ? popup.SelectedBlockFiles.FirstOrDefault() : null;
        }

        private void SingleChoiceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var checkBox in BlockListPanel.Children.OfType<CheckBox>())
            {
                if (!ReferenceEquals(checkBox, sender))
                    checkBox.IsChecked = false;
            }
        }

        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }
    }
}
