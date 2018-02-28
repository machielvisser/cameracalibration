using Microsoft.Win32;
using System.Windows;

namespace CameraCalibrationTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CalibrationToolViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _viewModel = new CalibrationToolViewModel();
        }

        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            if (dialog.ShowDialog() ?? false)
                _viewModel.SaveCalibration(dialog.FileName);
        }

        private void StartButtonClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Start();
        }

        private void StopButtonClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Stop();
        }

        private void FindPatternButtonClick(object sender, RoutedEventArgs e)
        {
            _viewModel.FindPatterns();
        }

        private void CalibrateIntrinsicButtonClick(object sender, RoutedEventArgs e)
        {
            _viewModel.CalibrateIntrinsic();
        }

        private void CalibrateExtrinsicButtonClick(object sender, RoutedEventArgs e)
        {
            _viewModel.CalibrateExtrinsic();
        }

        private void OpenCalibrationButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() ?? false)
                _viewModel.OpenCalibration(dialog.FileName);
        }
    }
}
