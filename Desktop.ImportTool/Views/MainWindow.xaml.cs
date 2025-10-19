using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Desktop.ImportTool.ViewModels;

namespace Desktop.ImportTool.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists("dockingLayout.xml"))
            {
                using (var fs = new FileStream("dockingLayout.xml", FileMode.Open, FileAccess.Read))
                {
                    MainDocking.LoadLayout(fs);
                }
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            using (var fs = new FileStream("dockingLayout.xml", FileMode.Create))
            {
                MainDocking.SaveLayout(fs);
            }
        }
    }
}