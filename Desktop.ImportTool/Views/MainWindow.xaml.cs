using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.ViewModels;

namespace Desktop.ImportTool.Views
{
    public partial class MainWindow : Window
    {
        private DockingService _dockingService;
        private const string PersistFile = "panePositions.cfg";

        public MainWindow()
        {
            InitializeComponent();

            // Avoid biasing DockingService with a single group - pass null
            _dockingService = new DockingService(this, MainDocking, null);

            DataContext = new MainWindowViewModel(_dockingService);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // We no longer rely on RadDocking.LoadLayout. Instead load our simple layout and
            // build the docking tree deterministically.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { LoadSimpleLayoutAndRestorePanes(); } catch { RestorePaneContents(); } // fallback to existing restore
                try { MainDocking.UpdateLayout(); } catch { }
            }), DispatcherPriority.Loaded);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                // Make sure panes are attached and logical tree is stable
                try { RestorePaneContents(); } catch { }
                try { MainDocking.UpdateLayout(); } catch { }

                // Persist our small, deterministic layout (not the Telerik XML)
                try { SaveSimpleLayout(); } catch { }
            }
            catch { }
        }

        // ---------- NEW SIMPLE LAYOUT PERSISTENCE ----------
        // A tiny human-editable file format:
        // TasksIndex=0
        // TasksOpen=1
        // HistoryIndex=1
        // HistoryOpen=1

        private void SaveSimpleLayout()
        {
            try
            {
                // Determine group index (within first RadSplitContainer) for Tasks and History
                var split = MainDocking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
                var groupsInSplit = split?.Items.OfType<RadPaneGroup>().ToList() ?? MainDocking.GetAllChildren().OfType<RadPaneGroup>().ToList();

                int? tasksIndex = null, historyIndex = null;
                bool tasksOpen = false, historyOpen = false;

                // find canonical panes
                var tasksPane = FindPaneByTagOrHeader("Tasks");
                var historyPane = FindPaneByTagOrHeader("History");

                if (tasksPane != null)
                {
                    tasksOpen = true;
                    var parent = tasksPane.Parent as RadPaneGroup;
                    if (parent != null)
                        tasksIndex = groupsInSplit.IndexOf(parent);
                }

                if (historyPane != null)
                {
                    historyOpen = true;
                    var parent = historyPane.Parent as RadPaneGroup;
                    if (parent != null)
                        historyIndex = groupsInSplit.IndexOf(parent);
                }

                // Default indices if missing: tasks->0, history->1
                var lines = new List<string>();
                lines.Add($"TasksIndex={(tasksIndex.HasValue ? tasksIndex.Value.ToString() : "0")}");
                lines.Add($"TasksOpen={(tasksOpen ? "1" : "0")}");
                lines.Add($"HistoryIndex={(historyIndex.HasValue ? historyIndex.Value.ToString() : "1")}");
                lines.Add($"HistoryOpen={(historyOpen ? "1" : "0")}");

                File.WriteAllLines(PersistFile, lines);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private void LoadSimpleLayoutAndRestorePanes()
        {
            // Step 1: dedupe existing RadDocking content (in case there are leftovers)
            try { DeduplicateLayout(); } catch { }

            // Step 2: read persisted file if exists
            int tasksIndex = 0, historyIndex = 1;
            bool tasksOpen = true, historyOpen = true;
            if (File.Exists(PersistFile))
            {
                try
                {
                    var lines = File.ReadAllLines(PersistFile);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();
                        if (string.Equals(key, "TasksIndex", StringComparison.OrdinalIgnoreCase)) int.TryParse(val, out tasksIndex);
                        if (string.Equals(key, "HistoryIndex", StringComparison.OrdinalIgnoreCase)) int.TryParse(val, out historyIndex);
                        if (string.Equals(key, "TasksOpen", StringComparison.OrdinalIgnoreCase)) tasksOpen = val == "1";
                        if (string.Equals(key, "HistoryOpen", StringComparison.OrdinalIgnoreCase)) historyOpen = val == "1";
                    }
                }
                catch { }
            }

            // Step 3: ensure we have at least one RadSplitContainer and enough PaneGroups
            var split = MainDocking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
            if (split == null)
            {
                split = new RadSplitContainer();
                try { MainDocking.Items.Add(split); } catch { }
            }

            // Ensure we have at least max(tasksIndex,historyIndex)+1 groups
            int neededGroups = Math.Max(tasksIndex, historyIndex) + 1;
            for (int i = 0; i < neededGroups; i++)
            {
                if (split.Items.OfType<RadPaneGroup>().ElementAtOrDefault(i) == null)
                {
                    var g = new RadPaneGroup();
                    try { split.Items.Add(g); } catch { }
                }
            }

            // Get the groups array (current state)
            var groupsList = split.Items.OfType<RadPaneGroup>().ToList();

            // Step 4: attach panes according to the saved data (or defaults)
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            // Helper to get or create canonical pane for key
            RadPane EnsureSinglePaneInGroup(string key, RadPaneGroup targetGroup, object contentView)
            {
                // Find any existing pane globally by tag/header
                var existing = FindPaneByTagOrHeader(key);
                if (existing != null)
                {
                    // move into targetGroup if needed
                    try
                    {
                        var currentParent = existing.Parent as RadPaneGroup;
                        if (!object.ReferenceEquals(currentParent, targetGroup))
                        {
                            try { currentParent?.Items.Remove(existing); } catch { }
                            try { targetGroup.Items.Insert(0, existing); } catch { try { targetGroup.Items.Add(existing); } catch { } }
                        }
                    }
                    catch { }

                    // attach content
                    try { if (!object.ReferenceEquals(existing.Content, contentView)) existing.Content = contentView; } catch { }
                    TrySetSerializationTag(existing, key);
                    return existing;
                }

                // Not found -> create exactly one placeholder and add to targetGroup
                var newPane = new RadPane { Header = key, CanUserClose = true };
                TrySetSerializationTag(newPane, key);
                try { newPane.Content = contentView; } catch { }
                try { targetGroup.Items.Insert(0, newPane); } catch { try { targetGroup.Items.Add(newPane); } catch { } }
                return newPane;
            }

            try
            {
                // Tasks
                if (tasksOpen)
                {
                    var tg = groupsList.ElementAtOrDefault(tasksIndex) ?? groupsList.First();
                    EnsureSinglePaneInGroup("Tasks", tg, vm.TasksView);
                }

                // History
                if (historyOpen)
                {
                    var hg = groupsList.ElementAtOrDefault(historyIndex) ?? groupsList.ElementAtOrDefault(1) ?? groupsList.First();
                    EnsureSinglePaneInGroup("History", hg, vm.HistoryView);
                }
            }
            catch
            {
                // on any error fallback to a simpler restore
                RestorePaneContents();
            }
        }

        // ---------- SMALL REUSE HELPERS (kept compatible with earlier attempts) ----------

        // Find a pane (any) by serialization tag or header
        private RadPane FindPaneByTagOrHeader(string paneKey)
        {
            try
            {
                var all = MainDocking.GetAllChildren().OfType<RadPane>();
                foreach (var p in all)
                {
                    try
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        if (string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase))
                            return p;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // Attach VM-created view instances to any panes that exist (defensive fallback)
        private void RestorePaneContents()
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            try
            {
                var all = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                foreach (var pane in all)
                {
                    try
                    {
                        var headerText = (pane.Header ?? string.Empty).ToString();
                        var serTag = (RadDocking.GetSerializationTag(pane) ?? string.Empty).ToString();

                        if (string.Equals(headerText, "Tasks", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(serTag, "Tasks", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!object.ReferenceEquals(pane.Content, vm.TasksView))
                                pane.Content = vm.TasksView;
                            TrySetSerializationTag(pane, "Tasks");
                        }
                        else if (string.Equals(headerText, "History", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(serTag, "History", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!object.ReferenceEquals(pane.Content, vm.HistoryView))
                                pane.Content = vm.HistoryView;
                            TrySetSerializationTag(pane, "History");
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Deduplicate duplicates created by Telerik serialization
        private void DeduplicateLayout()
        {
            try
            {
                var panes = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                if (!panes.Any()) return;
                var keys = new[] { "Tasks", "History" };

                foreach (var key in keys)
                {
                    var matches = panes.Where(p =>
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(tag, key, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(hdr, key, StringComparison.OrdinalIgnoreCase);
                    }).ToList();

                    if (matches.Count <= 1) continue;

                    RadPane keeper = null;

                    try
                    {
                        var namedPane = this.FindName(key + "Pane") as RadPane;
                        var namedGroup = this.FindName(key + "PaneGroup") as RadPaneGroup;
                        if (namedPane != null && namedGroup != null)
                        {
                            if (matches.Any(p => object.ReferenceEquals(p, namedPane) && p.Parent is RadPaneGroup parent && object.ReferenceEquals(parent, namedGroup)))
                                keeper = matches.First(p => object.ReferenceEquals(p, namedPane));
                        }
                    }
                    catch { }

                    if (keeper == null)
                        keeper = matches.FirstOrDefault(p => { try { return !p.IsHidden; } catch { return true; } });

                    if (keeper == null)
                        keeper = matches.First();

                    foreach (var extra in matches.Where(p => !object.ReferenceEquals(p, keeper)).ToList())
                    {
                        try { if (extra.Parent is RadPaneGroup parent) parent.Items.Remove(extra); } catch { }
                    }

                    panes = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                }
            }
            catch { }
        }

        private static void TrySetSerializationTag(RadPane pane, string tag)
        {
            try { RadDocking.SetSerializationTag(pane, tag); } catch { }
        }
    }
}