using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.ViewModels;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

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

        public void OpenPane(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return;
            var vm = _owner.DataContext as MainWindowViewModel;
            if (vm == null) return;

            if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase)) vm.TasksVM?.LoadTasks();
            if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase)) vm.HistoryVM?.LoadHistory();

            object viewInstance = paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase) ? vm.TasksView
                                 : paneKey.Equals("History", StringComparison.OrdinalIgnoreCase) ? vm.HistoryView
                                 : null;
            if (viewInstance == null) return;

            // 1) existing pane by content
            var byContent = FindPaneByContent(viewInstance);
            if (byContent != null)
            {
                MovePaneToPreferredGroupIfNeeded(byContent, paneKey);
                _panes[paneKey] = byContent;
                SelectPane(byContent);
                return;
            }

            // 2) find canonical pane by tag/header (prefer keeper selection)
            var byTag = FindCanonicalPaneByTagOrHeader(paneKey);
            if (byTag != null)
            {
                try { if (!object.ReferenceEquals(byTag.Content, viewInstance)) byTag.Content = viewInstance; } catch { }
                MovePaneToPreferredGroupIfNeeded(byTag, paneKey);
                _panes[paneKey] = byTag;
                SelectPane(byTag);
                return;
            }

            // 3) otherwise create a new pane
            var pane = new RadPane { Header = paneKey, Content = viewInstance, CanUserClose = true };
            try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }

            var hostGroup = ResolveHostGroupForOpen(paneKey) ?? CreateNewPaneGroupAtStart();
            if (hostGroup == null) throw new InvalidOperationException("No RadPaneGroup to host panes.");

            try { hostGroup.Items.Insert(0, pane); } catch { try { hostGroup.Items.Add(pane); } catch { } }

            _panes[paneKey] = pane;
            SelectPane(pane);
        }

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
                var created = new RadPaneGroup();
                try { split.Items.Insert(0, created); } catch { try { split.Items.Add(created); } catch { } }
                return created;
            }
            catch { return null; }
        }

        // Canonical pane selection: prefer named XAML pane in named group, then non-hidden, then first.
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
                        return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }).ToList();

                if (!panes.Any()) return null;

                // 1) prefer XAML-named pane inside named group
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

                // 2) prefer first non-hidden
                var nonHidden = panes.FirstOrDefault(p =>
                {
                    try { return !p.IsHidden; } catch { return true; }
                });
                if (nonHidden != null) return nonHidden;

                // 3) fallback first
                return panes.First();
            }
            catch { }
            return null;
        }

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
                        var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                        if (!groups.Any(g => object.ReferenceEquals(g, preferred))) preferred = null;
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

        private void SelectPane(RadPane pane)
        {
            if (pane == null) return;
            pane.IsSelected = true;
            try { (pane as FrameworkElement)?.BringIntoView(); } catch { }
        }

        private RadPaneGroup ResolveHostGroupForOpen(string paneKey)
        {
            try
            {
                if (_owner != null)
                {
                    var named = _owner.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (named != null)
                    {
                        if (_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, named)))
                            return named;
                    }
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
                            return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                        });
                        if (has) return g;
                    }
                    catch { }
                }

                if (_lastPlacement.TryGetValue(paneKey, out var placement) && placement != null && placement.Group != null)
                {
                    if (_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, placement.Group)))
                        return placement.Group;
                }
            }
            catch { }

            return _docking.GetAllChildren().OfType<RadPaneGroup>().FirstOrDefault();
        }

        private class PlacementInfo { public RadPaneGroup Group; public int Index; }
    }
}