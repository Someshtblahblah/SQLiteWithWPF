using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.ViewModels;

namespace Desktop.ImportTool.Views
{
    /// <summary>
    /// View-layer implementation of IDockingService.
    /// Compatible with older Telerik versions (e.g. 2016) and C# 7.3.
    /// - Reuses VM-created view instances (vm.TasksView / vm.HistoryView)
    /// - Calls TasksVM.LoadTasks() / HistoryVM.LoadHistory() before showing
    /// - Validates stored pane references by checking whether they are still present in the RadDocking tree
    ///   (robust against users moving panes between groups)
    /// </summary>
    public class DockingService : IDockingService
    {
        private readonly Window _owner;
        private readonly RadDocking _docking;
        private readonly RadPaneGroup _toolsGroup;

        // store active pane references by key
        private readonly Dictionary<string, RadPane> _panes = new Dictionary<string, RadPane>(StringComparer.OrdinalIgnoreCase);

        // store last placement info if you want to restore later (kept simple)
        private readonly Dictionary<string, PlacementInfo> _lastPlacement = new Dictionary<string, PlacementInfo>(StringComparer.OrdinalIgnoreCase);

        public DockingService(Window owner, RadDocking docking, RadPaneGroup toolsGroup)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (docking == null) throw new ArgumentNullException(nameof(docking));

            _owner = owner;
            _docking = docking;
            _toolsGroup = toolsGroup; // may be null - we fallback to first found group
        }

        public void OpenPane(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return;
            var vm = _owner.DataContext as MainWindowViewModel;
            if (vm == null) return;

            // Refresh underlying VM data before showing
            if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
            {
                if (vm.TasksVM != null) vm.TasksVM.LoadTasks();
            }
            else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase))
            {
                if (vm.HistoryVM != null) vm.HistoryVM.LoadHistory();
            }

            // Get the view instance from VM
            object viewInstance = null;
            if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
                viewInstance = vm.TasksView;
            else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase))
                viewInstance = vm.HistoryView;

            if (viewInstance == null) return;

            // If we have a stored pane reference, validate that it is still part of the docking control.
            RadPane storedPane;
            if (_panes.TryGetValue(paneKey, out storedPane) && storedPane != null)
            {
                if (IsPaneInDocking(storedPane))
                {
                    // stored pane is valid => select it
                    SelectPane(storedPane);
                    return;
                }

                // stored pane is not present in docking (it was closed/removed) => clear stored ref
                _panes[paneKey] = null;
                storedPane = null;
            }

            // Try to find an existing pane in the layout that hosts the same view instance
            var found = FindPaneByContent(viewInstance);
            if (found != null)
            {
                _panes[paneKey] = found;
                SelectPane(found);
                return;
            }

            // Create new RadPane and add it to a host group
            var pane = new RadPane
            {
                Header = paneKey,
                Content = viewInstance,
                CanUserClose = true
            };

            // Optionally capture placement when closing (we do not rely on per-group handlers anymore).
            // We'll store placement when we add it (index) in case you need it later.
            RadPaneGroup hostGroup = ResolveHostGroupForOpen(paneKey);
            if (hostGroup == null) throw new InvalidOperationException("No RadPaneGroup found to host panes.");

            // Insert at stored index if available
            PlacementInfo placement;
            if (_lastPlacement.TryGetValue(paneKey, out placement) && placement != null && placement.Group != null)
            {
                // Validate the stored group is still part of this docking
                if (IsPaneGroupInDocking(placement.Group) && placement.Index >= 0 && placement.Index <= hostGroup.Items.Count)
                {
                    hostGroup.Items.Insert(Math.Min(placement.Index, hostGroup.Items.Count), pane);
                }
                else
                {
                    hostGroup.Items.Add(pane);
                }
            }
            else
            {
                hostGroup.Items.Add(pane);
            }

            // store reference
            _panes[paneKey] = pane;

            // Select it
            SelectPane(pane);
        }

        /// <summary>
        /// Checks whether the given pane instance is currently present inside any RadPaneGroup within the RadDocking control.
        /// </summary>
        private bool IsPaneInDocking(RadPane pane)
        {
            if (pane == null) return false;

            // If parent is a RadPaneGroup and that group is part of docking -> consider it present.
            var parent = pane.Parent as RadPaneGroup;
            if (parent != null && IsPaneGroupInDocking(parent))
            {
                // additionally verify it's contained in group's Items
                try
                {
                    return parent.Items.OfType<object>().Any(i => object.ReferenceEquals(i, pane));
                }
                catch
                {
                    // best-effort: if Items access fails, still return false
                    return false;
                }
            }

            // Otherwise search all groups for reference equality (definitive)
            var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
            foreach (var g in groups)
            {
                try
                {
                    if (g.Items.OfType<object>().Any(i => object.ReferenceEquals(i, pane)))
                        return true;
                }
                catch
                {
                    // ignore failures and continue
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies whether a RadPaneGroup belongs to the current RadDocking visual/logical tree.
        /// </summary>
        private bool IsPaneGroupInDocking(RadPaneGroup group)
        {
            if (group == null) return false;
            var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
            foreach (var g in groups)
            {
                if (object.ReferenceEquals(g, group)) return true;
            }
            return false;
        }

        private RadPaneGroup ResolveHostGroupForOpen(string paneKey)
        {
            // Prefer configured tools group
            if (_toolsGroup != null) return _toolsGroup;

            // If we previously stored a placement and the group is still part of this docking, use it
            PlacementInfo placement;
            if (_lastPlacement.TryGetValue(paneKey, out placement) && placement != null && placement.Group != null)
            {
                if (IsPaneGroupInDocking(placement.Group))
                    return placement.Group;
                _lastPlacement.Remove(paneKey);
            }

            // fallback: first pane group in docking
            var first = _docking.GetAllChildren().OfType<RadPaneGroup>().FirstOrDefault();
            return first;
        }

        private RadPane FindPaneByContent(object content)
        {
            if (content == null) return null;

            // Search in the tools group first (if present)
            if (_toolsGroup != null)
            {
                try
                {
                    var p = _toolsGroup.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                    if (p != null) return p;
                }
                catch
                {
                    // ignore
                }
            }

            // Search all pane groups inside the docking control
            var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
            foreach (var group in groups)
            {
                try
                {
                    var p = group.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                    if (p != null) return p;
                }
                catch
                {
                    // ignore and continue
                }
            }

            return null;
        }

        private void SelectPane(RadPane pane)
        {
            if (pane == null) return;

            // Selecting the pane is enough to bring it into view in Telerik 2016.
            pane.IsSelected = true;

            // Try BringIntoView as a best-effort to ensure it becomes visible.
            try
            {
                var fe = pane as FrameworkElement;
                if (fe != null) fe.BringIntoView();
            }
            catch
            {
                // ignore if BringIntoView fails
            }
        }

        // Simple class used instead of tuple to be C#7.3-friendly
        private class PlacementInfo
        {
            public RadPaneGroup Group;
            public int Index;
        }
    }

    // helper to traverse logical tree (compatible with Telerik 2016)
    internal static class DockingExtensions
    {
        public static IEnumerable<System.Windows.DependencyObject> GetAllChildren(this System.Windows.DependencyObject parent)
        {
            if (parent == null) yield break;
            var children = LogicalTreeHelper.GetChildren(parent).OfType<System.Windows.DependencyObject>().ToList();
            foreach (var child in children)
            {
                yield return child;
                foreach (var desc in GetAllChildren(child))
                    yield return desc;
            }
        }
    }
}