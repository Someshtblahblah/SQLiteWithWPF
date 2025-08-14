using System;
using System.Collections;
using System.Windows;
using Telerik.Windows.Controls;

namespace GridViewDataTable.Extensions
{
    public static class RepositoryGridViewSelectionBehavior
    {
        public static readonly DependencyProperty BindableSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "BindableSelectedItems",
                typeof(IList),
                typeof(RepositoryGridViewSelectionBehavior),
                new PropertyMetadata(null, OnBindableSelectedItemsChanged));

        // Static guard to prevent recursive updates
        private static bool _isUpdating = false;

        public static IList GetBindableSelectedItems(DependencyObject obj) =>
            (IList)obj.GetValue(BindableSelectedItemsProperty);

        public static void SetBindableSelectedItems(DependencyObject obj, IList value) =>
            obj.SetValue(BindableSelectedItemsProperty, value);

        private static void OnBindableSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as RadGridView;
            if (grid == null) return;

            if (_isUpdating)
                return;

            _isUpdating = true;
            try
            {
                grid.SelectedItems.Clear();
                if (e.NewValue is IList newList)
                {
                    foreach (var item in newList)
                        grid.SelectedItems.Add(item);
                }

                grid.SelectionChanged -= TasksGrid_SelectionChanged;
                grid.SelectionChanged += TasksGrid_SelectionChanged;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static void TasksGrid_SelectionChanged(object sender, SelectionChangeEventArgs e)
        {
            var grid = sender as RadGridView;
            if (grid == null) return;

            if (_isUpdating)
                return;

            _isUpdating = true;
            try
            {
                var bindableSelectedItems = GetBindableSelectedItems(grid);
                if (bindableSelectedItems == null) return;

                bindableSelectedItems.Clear();
                foreach (var item in grid.SelectedItems)
                    bindableSelectedItems.Add(item);
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}