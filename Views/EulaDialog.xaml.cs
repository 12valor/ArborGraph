using System.IO;
using System.Reflection;
using System.Windows;

namespace DiskScope.Views;

public partial class EulaDialog : Window
{
    private readonly bool _isReviewMode;

    public EulaDialog(bool isReviewMode = false)
    {
        InitializeComponent();
        _isReviewMode = isReviewMode;

        LoadEulaContent();

        if (_isReviewMode)
        {
            AgreeCheckBox.Visibility = Visibility.Collapsed;
            ReviewModeNotice.Visibility = Visibility.Visible;
            DeclineButton.Visibility = Visibility.Collapsed;
            AcceptButton.Content = "Close";
            AcceptButton.IsEnabled = true;
        }
    }

    private void LoadEulaContent()
    {
        try
        {
            // Attempt to load from adjacent file if present
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string potentialPath1 = Path.Combine(appDir, "eula.txt");
            string potentialPath2 = Path.Combine(appDir, "installer", "eula.txt");
            string potentialPath3 = Path.Combine(appDir, "LICENSE.md");

            if (File.Exists(potentialPath1))
            {
                EulaTextBox.Text = File.ReadAllText(potentialPath1);
                return;
            }
            if (File.Exists(potentialPath2))
            {
                EulaTextBox.Text = File.ReadAllText(potentialPath2);
                return;
            }
            if (File.Exists(potentialPath3))
            {
                EulaTextBox.Text = File.ReadAllText(potentialPath3);
                return;
            }
        }
        catch
        {
            // Fall back to embedded text
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArborGraph.License")
            ?? throw new InvalidOperationException("The MIT license text is missing.");
        using var reader = new StreamReader(stream);
        EulaTextBox.Text = reader.ReadToEnd();
    }

    public bool IsAgreementChecked
    {
        get => AgreeCheckBox.IsChecked == true;
        set => AgreeCheckBox.IsChecked = value;
    }

    public bool IsAcceptButtonEnabled => AcceptButton.IsEnabled;

    public bool IsAgreeCheckBoxVisible => AgreeCheckBox.Visibility == Visibility.Visible;

    private void OnAgreeCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_isReviewMode)
        {
            AcceptButton.IsEnabled = AgreeCheckBox.IsChecked == true;
        }
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnDeclineClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
