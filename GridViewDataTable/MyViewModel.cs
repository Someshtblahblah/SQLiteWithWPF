using System;
using System.Collections;
using System.Collections.Generic;
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
        public RelayCommand MoveUpCommand { get; }
        public RelayCommand MoveDownCommand { get; }

        private ObservableCollection<TaskModel> _selectedTasks = new ObservableCollection<TaskModel>();
        public ObservableCollection<TaskModel> SelectedTasks
        {
            get => _selectedTasks;
            set
            {
                if (_selectedTasks != value)
                {
                    if (_selectedTasks != null)
                        _selectedTasks.CollectionChanged -= SelectedTasks_CollectionChanged;

                    _selectedTasks = value ?? new ObservableCollection<TaskModel>();
                    _selectedTasks.CollectionChanged += SelectedTasks_CollectionChanged;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTasks)));
                    DeleteSelectedTasksCommand?.RaiseCanExecuteChanged(); // Safe navigation
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
            SelectedTasks = new ObservableCollection<TaskModel>();
            DeleteSelectedTaskCommand = new RelayCommand(param => DeleteTask(param as TaskModel));
            DeleteSelectedTasksCommand = new RelayCommand(_ => DeleteSelectedTasks(),
                                                          _ => SelectedTasks != null && SelectedTasks.Count > 0);

            SelectedTasks.CollectionChanged += SelectedTasks_CollectionChanged;

            MoveUpCommand = new RelayCommand(_ => MoveTasksUp(), _ => CanMoveTasksUp());
            MoveDownCommand = new RelayCommand(_ => MoveTasksDown(), _ => CanMoveTasksDown());
            SelectedTasks.CollectionChanged += (s, e) =>
            {
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
            };

            var repo = new TaskRepository("Data Source=" + dbFilePath + ";");
            foreach (var task in repo.LoadTasks())
                Tasks.Add(task);
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
        }

        private void SelectedTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DeleteSelectedTasksCommand.RaiseCanExecuteChanged();
            // Do the same for MoveUpCommand, MoveDownCommand if needed
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

        private void MoveTasksUp()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0)
                return;

            var all = Tasks.ToList();

            // Group selection by CreatedBy
            var byUser = SelectedTasks
                .GroupBy(t => t.CreatedBy)
                .ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                // Only consider tasks of this CreatedBy in the full list
                var userTasks = all
                    .Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                // Find indices of selected tasks for this user
                var selectedIndices = group
                    .Select(t => userTasks.FindIndex(u => u.task == t))
                    .Where(i => i >= 0)
                    .OrderBy(i => i)
                    .ToList();

                // Find contiguous blocks (within user's tasks)
                var blocks = new List<(int start, int end)>();
                int? blockStart = null;
                for (int i = 0; i < selectedIndices.Count; i++)
                {
                    if (blockStart == null)
                        blockStart = selectedIndices[i];
                    if (i == selectedIndices.Count - 1 || selectedIndices[i + 1] != selectedIndices[i] + 1)
                    {
                        blocks.Add((blockStart.Value, selectedIndices[i]));
                        blockStart = null;
                    }
                }

                // Process each block from top to bottom (within user's tasks)
                foreach (var (start, end) in blocks)
                {
                    if (start == 0)
                        continue; // Block is already at top for this user

                    // Swap block with the item above (within user's tasks)
                    int blockSize = end - start + 1;

                    // Get the actual indices in the main list
                    int aboveIdx = userTasks[start - 1].idx;
                    var blockIndices = userTasks.GetRange(start, blockSize).Select(u => u.idx).ToList();

                    // Remove block from main list and insert above previous user task
                    var block = blockIndices.Select(idx => all[idx]).ToList();
                    foreach (var idx in blockIndices.OrderByDescending(x => x))
                        all.RemoveAt(idx);
                    var insertAt = aboveIdx;
                    foreach (var task in block)
                        all.Insert(insertAt++, task);
                }
            }

            // Reflect changes in ObservableCollection
            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();
        }

        private void MoveTasksDown()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0)
                return;

            var all = Tasks.ToList();

            // Group selection by CreatedBy
            var byUser = SelectedTasks
                .GroupBy(t => t.CreatedBy)
                .ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                // Only consider tasks of this CreatedBy in the full list
                var userTasks = all
                    .Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                // Find indices of selected tasks for this user
                var selectedIndices = group
                    .Select(t => userTasks.FindIndex(u => u.task == t))
                    .Where(i => i >= 0)
                    .OrderBy(i => i)
                    .ToList();

                // Find contiguous blocks (within user's tasks)
                var blocks = new List<(int start, int end)>();
                int? blockStart = null;
                for (int i = 0; i < selectedIndices.Count; i++)
                {
                    if (blockStart == null)
                        blockStart = selectedIndices[i];
                    if (i == selectedIndices.Count - 1 || selectedIndices[i + 1] != selectedIndices[i] + 1)
                    {
                        blocks.Add((blockStart.Value, selectedIndices[i]));
                        blockStart = null;
                    }
                }

                // Process each block from bottom to top (within user's tasks)
                for (int b = blocks.Count - 1; b >= 0; b--)
                {
                    var (start, end) = blocks[b];
                    int blockSize = end - start + 1;
                    if (end >= userTasks.Count - 1)
                        continue; // Block is already at bottom for this user

                    // Swap block with the item below (within user's tasks)
                    int belowIdx = userTasks[end + 1].idx;
                    var blockIndices = userTasks.GetRange(start, blockSize).Select(u => u.idx).ToList();

                    // Remove block from main list
                    var block = blockIndices.Select(idx => all[idx]).ToList();
                    foreach (var idx in blockIndices.OrderByDescending(x => x))
                        all.RemoveAt(idx);

                    // Because we've removed the block, the insertion index becomes:
                    // Find the updated index of the "below" item after removals
                    int updatedBelowIdx = all.IndexOf(userTasks[end + 1].task);

                    // Insert block immediately after the "below" item
                    int insertAt = updatedBelowIdx + 1;
                    foreach (var task in block)
                        all.Insert(insertAt++, task);
                }
            }

            // Reflect changes in ObservableCollection
            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();
        }

        /// <summary>
        /// Updates TaskOrder for all tasks, but only sequences within each CreatedBy group.
        /// </summary>
        private void UpdateTaskOrders()
        {
            var groups = Tasks.GroupBy(t => t.CreatedBy);
            foreach (var group in groups)
            {
                int order = 1;
                foreach (var task in Tasks.Where(t => t.CreatedBy == group.Key))
                {
                    task.TaskOrder = order++;
                }
            }
            // Optionally: Save to DB here if needed
        }

        private bool CanMoveTasksUp()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return false;

            var selectedGroups = SelectedTasks.GroupBy(t => t.CreatedBy);
            foreach (var group in selectedGroups)
            {
                var allInGroup = Tasks.Where(t => t.CreatedBy == group.Key).OrderBy(t => t.TaskOrder).ToList();
                var selectedInGroup = group.Select(t => allInGroup.IndexOf(t)).OrderBy(i => i).ToList();

                if (selectedInGroup.Any(i => i > 0 && !selectedInGroup.Contains(i - 1)))
                    return true;
            }
            return false;
        }

        private bool CanMoveTasksDown()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return false;

            var selectedGroups = SelectedTasks.GroupBy(t => t.CreatedBy);
            foreach (var group in selectedGroups)
            {
                var allInGroup = Tasks.Where(t => t.CreatedBy == group.Key).OrderBy(t => t.TaskOrder).ToList();
                var selectedInGroup = group.Select(t => allInGroup.IndexOf(t)).OrderByDescending(i => i).ToList();

                if (selectedInGroup.Any(i => i < allInGroup.Count - 1 && !selectedInGroup.Contains(i + 1)))
                    return true;
            }
            return false;
        }

        private void SaveTasksToDatabase()
        {
            var repo = new TaskRepository("Data Source=" + dbFilePath + ";");
            foreach (var task in Tasks)
            {
                //repo.UpdateTaskOrder(task.Id, task.TaskOrder);
            }
        }
    }
}
