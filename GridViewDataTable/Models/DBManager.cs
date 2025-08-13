using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridViewDataTable.Models
{
    public class DBManager
    {
        public static DataTable CreateTable(string dbFilePath)
        {
            using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                connection.Open();
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS Tasks (
                        TaskOrder INTEGER,
                        Source TEXT NOT NULL,
                        Target TEXT NOT NULL,
                        CreationTime TEXT NOT NULL,
                        CreatedBy TEXT NOT NULL,
                        Metadata TEXT NOT NULL,
                        Settings TEXT NOT NULL
                    )";
                using (var cmd = new SQLiteCommand(createTableSql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                string sql = $"SELECT * FROM Tasks";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        public static void ClearTable(string dbFilePath, string tableName)
        {
            using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                connection.Open();
                string sql = $"DELETE FROM {tableName}";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void AddTask(string dbFilePath, string Source, string Target, string CreationTime,
            string CreatedBy, string Metadata, string Settings)
        {
            using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                connection.Open();
                string insertSql = "INSERT INTO Tasks (TaskOrder, Source, Target, CreationTime, CreatedBy, Metadata, Settings)" +
                    " VALUES (@TaskOrder, @Source, @Target, @CreationTime, @CreatedBy, @Metadata, @Settings)";
                using (var cmd = new SQLiteCommand(insertSql, connection))
                {
                    cmd.Parameters.AddWithValue("@TaskOrder", GetNextTaskOrder(dbFilePath, CreatedBy));
                    cmd.Parameters.AddWithValue("@Source", Source);
                    cmd.Parameters.AddWithValue("@Target", Target);
                    cmd.Parameters.AddWithValue("@CreationTime", CreationTime);
                    cmd.Parameters.AddWithValue("@CreatedBy", CreatedBy);
                    cmd.Parameters.AddWithValue("@Metadata", Metadata);
                    cmd.Parameters.AddWithValue("@Settings", Settings);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static int GetNextTaskOrder(string dbFilePath, string createdBy)
        {
            int nextOrder = 1;
            using (var conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT MAX(TaskOrder) FROM Tasks WHERE CreatedBy = @CreatedBy", conn))
                {
                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        nextOrder = Convert.ToInt32(result) + 1;
                }
            }
            return nextOrder;
        }
    }
}
