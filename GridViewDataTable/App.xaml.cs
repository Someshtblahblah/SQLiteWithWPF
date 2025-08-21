using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Telerik.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using GridViewDataTable.Models;

namespace GridViewDataTable
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Override members

        protected override void OnStartup(StartupEventArgs e)
        {
            var dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";
            DBManager.CreateTable(dbFilePath);
            DBManager.ClearTable(dbFilePath, "Tasks");
            for (int i = 1; i <= 8; i++)
            {
                DBManager.AddTask(dbFilePath, "WWWW" + i, "WWWW" + i, "WWWW" + i, Environment.UserName, "WWWW" + i, "WWWW" + i);
            }
            for (int i = 1; i <= 4; i++)
            {
                DBManager.AddTask(dbFilePath, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i);
            }
            for (int i = 2; i <= 4; i++)
            {
                DBManager.AddTask(dbFilePath, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i, "WWWW" + i);
            }
            for (int i = 1; i <= 8; i++)
            {
                DBManager.AddTask(dbFilePath, "WWWW" + i, "WWWW" + i, "WWWW" + i, Environment.UserName, "WWWW" + i, "WWWW" + i);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {

            base.OnExit(e);
        }

        #endregion
    }
}
