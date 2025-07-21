using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;


namespace CostcoApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = ((App)Application.Current).Services.GetRequiredService<ViewModels.MainViewModel>();
        }

        /// <summary>
        /// Opens the ComboBox drop-down on single click.
        /// </summary>IOException: Cannot locate resource 'assets/warningup.png'.
        private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var combo = (ComboBox)sender;
            if (!combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = true;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Shows the clicked product image in the overlay.
        /// </summary>
        private void ProductImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Source != null)
            {
                EnlargedImage.Source = img.Source;
                ImageOverlay.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Hides the image overlay on any click.
        /// </summary>
        private void ImageOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ImageOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        // Show overlay with the clicked image
        /// </summary>
        private void DataGridImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var img = (Image)sender;
            EnlargedImage.Source = img.Source;
            ImageOverlay.Visibility = Visibility.Visible;
        }
    }
}
