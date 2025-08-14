using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using GridViewDataTable.Models;
using Telerik.Windows.Controls;
using Telerik.Windows.Data;

namespace GridViewDataTable
{
    public partial class MainWindow : UserControl//, IDisposable
    {
        private bool refreshInProgress;
        private DispatcherTimer timer;
        public MainWindow()
        {
            InitializeComponent();

            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(20);
            //timer.Start();
            //timer.Tick += Timer_Tick;

            //this.Unloaded += (s, e) => this.Dispose();
        }

        private void TasksGrid_SelectionChanged(object sender, SelectionChangeEventArgs e)
        {
            var viewModel = (MyViewModel)this.DataContext;
            viewModel.SelectedTasks = ((RadGridView)sender).SelectedItems.Cast<TaskModel>().ToList();
        }

        //public void Dispose()
        //{
        //    this.timer.Stop();

        //    this.timer.Tick -= Timer_Tick;
        //    this.timer = null;
        //}

        //private void Timer_Tick(object sender, EventArgs e)
        //{
        //    Random random = new Random();
        //    if (refreshInProgress == false)
        //    {
        //        refreshInProgress = true;
        //        int rowCount = (this.DataContext as MyViewModel).DataTable.Rows.Count;

        //        for (int i = 0; i < rowCount; i++)
        //        {
        //            DataRow row = (this.DataContext as MyViewModel).DataTable.Rows[i];
        //            //row["City"] = random.Next(0, 500);
        //            //row["LastTimeUpdate"] = System.DateTime.Now;
        //        }
        //        refreshInProgress = false;
        //    }
        //}
    }
}
