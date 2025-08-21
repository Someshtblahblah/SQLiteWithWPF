using System.Collections.Generic;
using System.Data.SQLite;

namespace GridViewDataTable.Models
{
    public class TaskRepository
    {
        private readonly string _connectionString;

        public TaskRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<TaskModel> LoadTasks()
        {
            var tasks = new List<TaskModel>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                    @"SELECT TaskOrder, Source, Target, CreationTime, CreatedBy, Metadata, Settings FROM Tasks ORDER BY TaskOrder", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tasks.Add(new TaskModel
                        {
                            TaskOrder = reader.GetInt32(0),
                            Source = reader.GetString(1),
                            Target = reader.GetString(2),
                            CreationTime = reader.GetString(3),
                            CreatedBy = reader.GetString(4),
                            Metadata = reader.GetString(5),
                            Settings = reader.GetString(6)
                        });
                    }
                }
            }
            return tasks;
        }

        public void DeleteTask(int taskOrder)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM Tasks WHERE TaskOrder = @TaskOrder", conn))
                {
                    cmd.Parameters.AddWithValue("@TaskOrder", taskOrder);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteTask(int taskOrder, string createdBy)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM Tasks WHERE TaskOrder = @TaskOrder AND CreatedBy = @CreatedBy", conn))
                {
                    cmd.Parameters.AddWithValue("@TaskOrder", taskOrder);
                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        //private void SaveTasksToDatabase()
        //{
        //    foreach (var task in Tasks)
        //    {
        //        repository.UpdateTaskOrder(task.Id, task.TaskOrder);
        //    }
        //}
    }
}