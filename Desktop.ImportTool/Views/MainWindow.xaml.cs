using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.ViewModels;

namespace Desktop.ImportTool.Views
{
    public partial class MainWindow : Window
    {
        private DockingService _dockingService;

        public MainWindow()
        {
            InitializeComponent();

            // create view-layer DockingService and inject into the VM
            _dockingService = new DockingService(this, MainDocking, ToolsPaneGroup);

            // set DataContext with docking service injected (VM uses IDockingService only)
            DataContext = new MainWindowViewModel(_dockingService);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists("dockingLayout.xml"))
            {
                using (var fs = new FileStream("dockingLayout.xml", FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        MainDocking.LoadLayout(fs);
                    }
                    catch
                    {
                        // If layout is corrupt or incompatible, ignore and continue with default layout.
                        // You may want to log this in production.
                    }
                }

                // After layout load we must reattach the actual view instances (VM-created).
                // Use Dispatcher.BeginInvoke to ensure layout tree is fully built.
                Dispatcher.BeginInvoke(new Action(RestorePaneContents), DispatcherPriority.Loaded);
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            using (var fs = new FileStream("dockingLayout.xml", FileMode.Create))
            {
                MainDocking.SaveLayout(fs);
            }
        }

        /// <summary>
        /// Reattach the VM-created view instances to RadPanes restored by LoadLayout.
        /// Matches panes by Header text ("Tasks", "History") and replaces empty/non-FrameworkElement content.
        /// </summary>
        private void RestorePaneContents()
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            // iterate all RadPane instances in the docking control
            var allPanes = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
            foreach (var pane in allPanes)
            {
                try
                {
                    // if content is already a visual element, skip
                    var content = pane.Content;
                    if (content is FrameworkElement)
                        continue;

                    // Header may be object; try to get string
                    var headerText = (pane.Header ?? string.Empty).ToString();

                    if (string.Equals(headerText, "Tasks", StringComparison.OrdinalIgnoreCase))
                    {
                        // assign the view instance from VM
                        pane.Content = vm.TasksView;
                    }
                    else if (string.Equals(headerText, "History", StringComparison.OrdinalIgnoreCase))
                    {
                        pane.Content = vm.HistoryView;
                    }
                    else
                    {
                        // For any other custom panes you might have, you can add cases here.
                    }
                }
                catch
                {
                    // swallow so a single pane won't break the whole restore process
                }
            }
        }
    }
}