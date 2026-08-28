using System.Windows;

namespace RevitDesignReview.Revit2026;

public partial class QuickReviewWindow : Window
{
    public QuickReviewWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TitleTextBox.Focus();
    }

    public string ReviewTitle => TitleTextBox.Text.Trim();

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReviewTitle))
        {
            MessageBox.Show(this, "Enter a short review title.", "Design Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
