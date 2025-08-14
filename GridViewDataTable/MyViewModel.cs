using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using GridViewDataTable.Infrastructure;
using GridViewDataTable.Models;

namespace GridViewDataTable
{
    public class MyViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TaskModel> Tasks { get; }
        public ICollectionView TasksView { get; }
        public RelayCommand DeleteSelectedTaskCommand { get; }
        public RelayCommand DeleteSelectedTasksCommand { get; private set; }

        private IList _selectedTasks;
        public IList SelectedTasks
        {
            get => _selectedTasks;
            set
            {
                if (_selectedTasks != value)
                {
                    _selectedTasks = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTasks)));
                    DeleteSelectedTasksCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private TaskModel _selectedTask;
        public TaskModel SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (_selectedTask != value)
                {
                    _selectedTask = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTask)));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private DataTable datatable;
        private readonly string dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";
        private readonly string filterColumn = "CreatedBy";
        private readonly string filterValue = Environment.UserName;

        public event PropertyChangedEventHandler PropertyChanged;

        public MyViewModel()
        {
            Tasks = new ObservableCollection<TaskModel>();

            var repo = new TaskRepository("Data Source=" + dbFilePath + ";");
            foreach (var task in repo.LoadTasks())
                Tasks.Add(task);
            TasksView = CollectionViewSource.GetDefaultView(Tasks);

            DeleteSelectedTaskCommand = new RelayCommand(param => DeleteTask(param as TaskModel));
            DeleteSelectedTasksCommand = new RelayCommand(_ => DeleteSelectedTasks(),
                                                          _ => SelectedTasks != null && SelectedTasks.Count > 0);
        }

        public void DeleteTask(TaskModel task)
        {
            if (task == null) return;
            var repo = new TaskRepository("Data Source=" + dbFilePath + ";");
            repo.DeleteTask(task.TaskOrder);
            Tasks.Remove(task);
        }



        private void DeleteSelectedTasks()
        {
            var repo = new TaskRepository("Data Source=" + dbFilePath + ";");
            var tasksToDelete = SelectedTasks.Cast<TaskModel>().ToList();

            foreach (var task in tasksToDelete)
            {
                repo.DeleteTask(task.TaskOrder, task.CreatedBy); // If you update DeleteTask for composite key
                Tasks.Remove(task);
            }
        }
    }
}
