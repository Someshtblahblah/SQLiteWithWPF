using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.Models;
using Desktop.ImportTool.Views;

namespace Desktop.ImportTool.ViewModels
{
    public class PaneViewModel : INotifyPropertyChanged
    {
        private string _header;
        private object _content;

        public string Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(nameof(Header)); }
        }

        public object Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(nameof(Content)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly string dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";
        private readonly DBManager manager;
        private int _addTaskCounter = 1;

        public TasksViewModel TasksVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public object TasksView { get; }
        public object HistoryView { get; }
        public ICommand FinishTaskCommand { get; }
        public ICommand FailTaskCommand { get; }
        public ICommand AddTaskCommand { get; }

        public ObservableCollection<PaneViewModel> OpenPanes { get; } = new ObservableCollection<PaneViewModel>();

        public ICommand ToggleTasksPaneCommand { get; }
        public ICommand ToggleHistoryPaneCommand { get; }

        private readonly PaneViewModel _tasksPane;
        private readonly PaneViewModel _historyPane;

        private bool _isTasksSelected = true;
        public bool IsTasksSelected
        {
            get => _isTasksSelected;
            set { _isTasksSelected = value; OnPropertyChanged(nameof(IsTasksSelected)); }
        }

        private bool _isHistorySelected = false;
        public bool IsHistorySelected
        {
            get => _isHistorySelected;
            set { _isHistorySelected = value; OnPropertyChanged(nameof(IsHistorySelected)); }
        }

        public MainWindowViewModel()
        {
            _tasksPane = new PaneViewModel { Header = "Tasks", Content = new TasksView() };
            _historyPane = new PaneViewModel { Header = "History", Content = new HistoryView() };

            OpenPanes.Add(_tasksPane);
            OpenPanes.Add(_historyPane);

            ToggleTasksPaneCommand = new RelayCommand(_ => TogglePane(_tasksPane));
            ToggleHistoryPaneCommand = new RelayCommand(_ => TogglePane(_historyPane));

            manager = new DBManager(dbFilePath);
            TasksVM = new TasksViewModel();
            HistoryVM = new HistoryViewModel();

            // Pass those instances as DataContext for the views
            TasksView = new TasksView();       // replace with your actual view or UserControl
            HistoryView = new HistoryView();

            FinishTaskCommand = new RelayCommand(_ => FinishTask());
            FailTaskCommand = new RelayCommand(_ => FailTask());
            AddTaskCommand = new RelayCommand(_ => AddTask());
        }

        private void TogglePane(PaneViewModel pane)
        {
            if (OpenPanes.Contains(pane))
                OpenPanes.Remove(pane);
            else
                OpenPanes.Add(pane);
        }

        private void FinishTask()
        {

            try
            {
                var currentUser = Environment.UserName;
                var tasks = manager.GetTasks()
                                   .Where(t => t.CreatedBy == currentUser && t.Status != TaskStatus.Paused)
                                   .OrderBy(t => t.TaskOrder)
                                   .ToList();

                var taskToFinish = tasks.FirstOrDefault();
                if (taskToFinish == null) return;

                taskToFinish.Status = TaskStatus.Finished;
                taskToFinish.FinishingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                manager.AddHistoryRow(taskToFinish);
                manager.DeleteTask(taskToFinish.Id);

                TasksVM.LoadTasks();
                HistoryVM.LoadHistory();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        private void FailTask()
        {
            try
            {
                var currentUser = Environment.UserName;
                var tasks = manager.GetTasks()
                                   .Where(t => t.CreatedBy == currentUser && t.Status != TaskStatus.Paused)
                                   .OrderBy(t => t.TaskOrder)
                                   .ToList();

                var taskToFail = tasks.FirstOrDefault();
                if (taskToFail == null) return;

                taskToFail.Status = TaskStatus.Failed;
                taskToFail.FinishingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                manager.AddHistoryRow(taskToFail);
                manager.DeleteTask(taskToFail.Id);

                TasksVM.LoadTasks();
                HistoryVM.LoadHistory();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }
        private void AddTask()
        {
            try
            {
                var currentUser = Environment.UserName;
                manager.AddTask("Source" + _addTaskCounter, "Target" + _addTaskCounter, "Metadata" + _addTaskCounter, "Settings" + _addTaskCounter);
                _addTaskCounter++;
                TasksVM.LoadTasks();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}