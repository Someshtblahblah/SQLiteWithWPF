using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Telerik.Windows.Controls;

namespace GridViewDataTable
{
    /// <summary>
    /// Interaction logic for TabWindow.xaml
    /// </summary>
    public partial class TabWindow : Window
    {
        public TabWindow()
        {
            InitializeComponent();
            this.DataContext = new TabWindowViewModel();
        }


    }

    public class TabWindowViewModel
    {
        public TabWindowViewModel()
        {
            InitializeTabView();
        }
        private ObservableCollection<TabWindowModel> _tableCollection;
        public ObservableCollection<TabWindowModel> TableCollection
        {
            get
            {
                if (_tableCollection == null)
                {
                    _tableCollection = new ObservableCollection<TabWindowModel>();
                }
                return _tableCollection;
            }
            set { _tableCollection = value; }
        }
        private void InitializeTabView()
        {
            for (int i = 0; i < 10; i++)
            {
                TabWindowModel model = new TabWindowModel();
                model.HeaderName = "Screen " + i;
                model.View = new MainWindow();
                TableCollection.Add(model);
            }
        }
    }

    public class TabWindowModel
    {
        public string HeaderName { get; internal set; }
        public MainWindow View { get; internal set; }
    }
}
