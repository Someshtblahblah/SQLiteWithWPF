using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
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
    /// - Detects user-close by monitoring the host group's Items.CollectionChanged (no RadPane.Closed)
    /// - Does not use RadPaneGroup.IsSelected (not present in that version); activates panes by pane.IsSelected = true
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

            // If we have a stored pane reference, try to select it
            RadPane existingPane;
            if (_panes.TryGetValue(paneKey, out existingPane) && existingPane != null)
            {
                SelectPane(existingPane);
                return;
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

            // Remember placement when closing: subscribe to the host group's CollectionChanged and detect removal
            RadPaneGroup hostGroup = ResolveHostGroupForOpen(paneKey);
            if (hostGroup == null) throw new InvalidOperationException("No RadPaneGroup found to host panes.");

            // Handler to observe removal of this pane from its host group's Items collection
            NotifyCollectionChangedEventHandler removalHandler = null;
            removalHandler = (s, e) =>
            {
                try
                {
                    if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
                    {
                        foreach (var old in e.OldItems)
                        {
                            if (object.ReferenceEquals(old, pane))
                            {
                                // pane was removed from the group (user closed it). Clear stored reference.
                                RadPane dummy;
                                if (_panes.TryGetValue(paneKey, out dummy) && object.ReferenceEquals(dummy, pane))
                                    _panes[paneKey] = null;

                                // store last placement index (if needed later)
                                var idx = hostGroup.Items.IndexOf(pane);
                                if (idx < 0) idx = 0;
                                _lastPlacement[paneKey] = new PlacementInfo { Group = hostGroup, Index = idx };

                                // detach handler
                                var notify = s as INotifyCollectionChanged;
                                if (notify != null) notify.CollectionChanged -= removalHandler;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // swallow - avoid raising errors from collection-changed handler
                }
            };

            // Attach handler BEFORE adding so we capture eventual future removal
            var hostNotify = hostGroup.Items as INotifyCollectionChanged;
            if (hostNotify != null) hostNotify.CollectionChanged += removalHandler;

            // Add pane (this will cause the pane to appear)
            hostGroup.Items.Add(pane);

            // store reference and select
            _panes[paneKey] = pane;
            SelectPane(pane);
        }

        private RadPaneGroup ResolveHostGroupForOpen(string paneKey)
        {
            // Prefer configured tools group
            if (_toolsGroup != null) return _toolsGroup;

            // If we previously stored a placement and the group is still part of this docking, use it
            PlacementInfo placement;
            if (_lastPlacement.TryGetValue(paneKey, out placement) && placement != null && placement.Group != null)
            {
                // verify group is still inside the docking control
                var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                foreach (var g in groups)
                {
                    if (object.ReferenceEquals(g, placement.Group))
                        return placement.Group;
                }
                // if not valid anymore, fall through and drop old placement
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
                var p = _toolsGroup.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                if (p != null) return p;
            }

            // Search all pane groups inside the docking control
            var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
            foreach (var group in groups)
            {
                var p = group.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                if (p != null) return p;
            }

            return null;
        }

        private void SelectPane(RadPane pane)
        {
            if (pane == null) return;

            // Selecting the pane is enough to bring it into view in Telerik 2016.
            // Setting pane.IsSelected is supported.
            pane.IsSelected = true;

            // Do not rely on RadPaneGroup.IsSelected (it might not exist in older versions)
            // Optionally try BringIntoView to attempt scrolling/focus
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