// url= (replace your current DockingService.cs with this file)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.Infrastructure;

namespace Desktop.ImportTool.Views
{
    public class DockingService : IDockingService
    {
        private readonly Window _owner;
        private readonly RadDocking _docking;
        private readonly RadPaneGroup _toolsGroup;

        private readonly Dictionary<string, RadPane> _panes = new Dictionary<string, RadPane>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PlacementInfo> _lastPlacement = new Dictionary<string, PlacementInfo>(StringComparer.OrdinalIgnoreCase);

        public DockingService(Window owner, RadDocking docking, RadPaneGroup toolsGroup)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _docking = docking ?? throw new ArgumentNullException(nameof(docking));
            _toolsGroup = toolsGroup;
        }

        // IDockingService event
        public event EventHandler<PaneStateChangedEventArgs> PaneStateChanged;

        public void OpenPane(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return;
            var vm = _owner.DataContext as dynamic; // avoid strong coupling here
            if (vm == null) return;

            // refresh VM data (best-effort)
            try
            {
                if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
                    vm.TasksVM?.LoadTasks();
                else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase))
                    vm.HistoryVM?.LoadHistory();
            }
            catch { /* ignore */ }

            // Get the VM-created view instance
            object viewInstance = null;
            try
            {
                if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
                    viewInstance = vm.TasksView;
                else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase))
                    viewInstance = vm.HistoryView;
            }
            catch { /* ignore */ }

            if (viewInstance == null) return;

            // If we have a stored pane reference and it's still present anywhere, select it.
            if (_panes.TryGetValue(paneKey, out var storedPane) && storedPane != null)
            {
                if (IsPanePresentAnywhere(storedPane))
                {
                    SelectPane(storedPane);
                    RaisePaneStateChanged(paneKey, true);
                    return;
                }

                _panes[paneKey] = null;
                storedPane = null;
            }

            // 1) Try find a pane that already hosts the same VM view instance
            var foundByContent = FindPaneByContent(viewInstance);
            if (foundByContent != null)
            {
                MovePaneToPreferredGroupIfNeeded(foundByContent, paneKey);
                _panes[paneKey] = foundByContent;
                SelectPane(foundByContent);
                RaisePaneStateChanged(paneKey, true);
                return;
            }

            // 2) Try find an existing serialized/restored pane by tag/header
            var foundByTag = FindCanonicalPaneByTagOrHeader(paneKey);
            if (foundByTag != null)
            {
                try { if (!object.ReferenceEquals(foundByTag.Content, viewInstance)) foundByTag.Content = viewInstance; } catch { }
                MovePaneToPreferredGroupIfNeeded(foundByTag, paneKey);
                AttachCloseWatcher(foundByTag, paneKey);
                _panes[paneKey] = foundByTag;
                SelectPane(foundByTag);
                RaisePaneStateChanged(paneKey, true);
                return;
            }

            // 3) Create a new pane and add to a resolved group
            var pane = new RadPane { Header = paneKey, Content = viewInstance, CanUserClose = true };
            try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }

            AttachCloseWatcher(pane, paneKey);

            var host = ResolveHostGroupForOpen(paneKey) ?? CreateNewPaneGroupAtStart();
            if (host == null) throw new InvalidOperationException("No RadPaneGroup found to host panes.");

            try { host.Items.Insert(0, pane); } catch { try { host.Items.Add(pane); } catch { } }

            _panes[paneKey] = pane;
            RaisePaneStateChanged(paneKey, true);
            SelectPane(pane);
        }

        // Return true if a pane for paneKey exists in any known place (docked or undocked).
        // This is used by the VM to disable the Open command when the pane is already open.
        public bool IsPaneOpen(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return false;

            // 1) Check dictionary
            if (_panes.TryGetValue(paneKey, out var p) && p != null && IsPanePresentAnywhere(p))
                return true;

            // 2) Search docking tree / floating windows for a pane with matching serialization tag/header
            if (FindCanonicalPaneByTagOrHeader(paneKey) != null) return true;

            // 3) Search all application windows just in case (floating tool windows)
            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    var found = FindPaneInVisualOrLogicalTree(w, paneKey);
                    if (found != null) return true;
                }
            }
            catch { }

            return false;
        }

        // Helper - raises PaneStateChanged
        private void RaisePaneStateChanged(string paneKey, bool isOpen)
        {
            try
            {
                PaneStateChanged?.Invoke(this, new PaneStateChangedEventArgs(paneKey, isOpen));
            }
            catch { }
        }

        // Attach handlers to know when pane is closed/removed so we can update IsPaneOpen and raise events.
        private void AttachCloseWatcher(RadPane pane, string paneKey)
        {
            if (pane == null || string.IsNullOrWhiteSpace(paneKey)) return;

            try
            {
                // When pane gets unloaded / removed, clear stored reference and raise change
                RoutedEventHandler unloaded = null;
                unloaded = (s, e) =>
                {
                    try
                    {
                        if (_panes.ContainsKey(paneKey) && object.ReferenceEquals(_panes[paneKey], pane))
                            _panes[paneKey] = null;
                    }
                    catch { }

                    try { RaisePaneStateChanged(paneKey, false); } catch { }

                    // detach
                    try { pane.Unloaded -= unloaded; } catch { }
                };

                pane.Unloaded += unloaded;

                // Also watch for parent collection changes (pane removed from a group)
                try
                {
                    var parentGroup = pane.Parent as RadPaneGroup;
                    if (parentGroup != null && parentGroup.Items is System.Collections.Specialized.INotifyCollectionChanged items)
                    {
                        System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = null;
                        handler = (s, e) =>
                        {
                            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
                                e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
                            {
                                var removed = e.OldItems?.OfType<object>().FirstOrDefault(x => object.ReferenceEquals(x, pane));
                                if (removed != null)
                                {
                                    try
                                    {
                                        if (_panes.ContainsKey(paneKey) && object.ReferenceEquals(_panes[paneKey], pane))
                                            _panes[paneKey] = null;
                                    }
                                    catch { }

                                    try { RaisePaneStateChanged(paneKey, false); } catch { }

                                    try { items.CollectionChanged -= handler; } catch { }
                                }
                            }
                        };
                        items.CollectionChanged += handler;
                    }
                }
                catch { }
            }
            catch { }
        }

        // Create a new pane group at start (used when no suitable host found)
        private RadPaneGroup CreateNewPaneGroupAtStart()
        {
            try
            {
                var split = _docking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
                if (split == null)
                {
                    split = new RadSplitContainer();
                    try { _docking.Items.Add(split); } catch { }
                }

                var group = new RadPaneGroup();
                try { split.Items.Insert(0, group); } catch { try { split.Items.Add(group); } catch { } }
                return group;
            }
            catch { return null; }
        }

        // Move pane to preferred named group if available
        private void MovePaneToPreferredGroupIfNeeded(RadPane pane, string paneKey)
        {
            if (pane == null || string.IsNullOrWhiteSpace(paneKey)) return;
            try
            {
                RadPaneGroup preferred = null;
                if (_owner != null)
                {
                    preferred = _owner.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (preferred != null)
                    {
                        // ensure it's part of current tree
                        if (!_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, preferred)))
                            preferred = null;
                    }
                }

                if (preferred != null)
                {
                    var current = pane.Parent as RadPaneGroup;
                    if (!object.ReferenceEquals(current, preferred))
                    {
                        try { current?.Items.Remove(pane); } catch { }
                        try { preferred.Items.Insert(0, pane); } catch { try { preferred.Items.Add(pane); } catch { } }
                        try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }
                    }
                }
            }
            catch { }
        }

        // Find a canonical pane by tag/header (prefers named XAML pane in named group, then non-hidden, then any)
        private RadPane FindCanonicalPaneByTagOrHeader(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return null;
            try
            {
                var panes = _docking.GetAllChildren().OfType<RadPane>().Where(p =>
                {
                    try
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }).ToList();

                if (!panes.Any()) return null;

                // prefer XAML-named pane inside named group
                try
                {
                    var namedPane = _owner?.FindName(paneKey + "Pane") as RadPane;
                    var namedGroup = _owner?.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (namedPane != null && namedGroup != null)
                    {
                        if (panes.Any(p => object.ReferenceEquals(p, namedPane) && p.Parent is RadPaneGroup parent && object.ReferenceEquals(parent, namedGroup)))
                            return panes.First(p => object.ReferenceEquals(p, namedPane));
                    }
                }
                catch { }

                var nonHidden = panes.FirstOrDefault(p => { try { return !p.IsHidden; } catch { return true; } });
                if (nonHidden != null) return nonHidden;

                return panes.First();
            }
            catch { return null; }
        }

        // Find pane by content inside docking pane groups only
        private RadPane FindPaneByContent(object content)
        {
            if (content == null) return null;
            try
            {
                var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                foreach (var g in groups)
                {
                    try
                    {
                        var p = g.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                        if (p != null) return p;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // Search for a pane with matching tag/header inside a visual/logical tree rooted at root (used for floating windows)
        private RadPane FindPaneInVisualOrLogicalTree(DependencyObject root, string paneKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(paneKey)) return null;
            try
            {
                var stack = new Stack<DependencyObject>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    if (node is RadPane rp)
                    {
                        try
                        {
                            var tag = (RadDocking.GetSerializationTag(rp) ?? rp.Header)?.ToString() ?? string.Empty;
                            var hdr = (rp.Header ?? string.Empty).ToString() ?? string.Empty;
                            if (string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase))
                                return rp;
                        }
                        catch { }
                    }

                    // iterate logical children
                    try
                    {
                        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                            stack.Push(child);
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // Returns true if pane exists anywhere (docked or undocked)
        private bool IsPanePresentAnywhere(RadPane pane)
        {
            if (pane == null) return false;

            // If parent is a pane group that's part of docking, treat as present
            try
            {
                var parent = pane.Parent as RadPaneGroup;
                if (parent != null && _docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, parent)))
                    return parent.Items.OfType<object>().Any(i => object.ReferenceEquals(i, pane));
            }
            catch { }

            // Otherwise search application windows
            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    var found = FindPaneInVisualOrLogicalTree(w, (RadDocking.GetSerializationTag(pane) ?? pane.Header)?.ToString() ?? string.Empty);
                    if (found != null && object.ReferenceEquals(found, pane)) return true;
                }
            }
            catch { }

            // fallback: not found
            return false;
        }

        private void SelectPane(RadPane pane)
        {
            if (pane == null) return;
            try { pane.IsSelected = true; } catch { }
            try { (pane as FrameworkElement)?.BringIntoView(); } catch { }
        }

        private RadPaneGroup ResolveHostGroupForOpen(string paneKey)
        {
            try
            {
                if (_owner != null)
                {
                    var named = _owner.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (named != null && _docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, named)))
                        return named;
                }

                var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                foreach (var g in groups)
                {
                    try
                    {
                        var has = g.Items.OfType<RadPane>().Any(p =>
                        {
                            var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                            var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                            return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                        });
                        if (has) return g;
                    }
                    catch { }
                }

                if (_lastPlacement.TryGetValue(paneKey, out var placement) && placement?.Group != null)
                {
                    if (_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, placement.Group)))
                        return placement.Group;
                }
            }
            catch { }

            return _docking.GetAllChildren().OfType<RadPaneGroup>().FirstOrDefault();
        }

        // Simple holder class
        private class PlacementInfo { public RadPaneGroup Group; public int Index; }
    }
}