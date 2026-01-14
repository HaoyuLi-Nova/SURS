using System.Windows;
using SURS.App.ViewModels;

namespace SURS.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
