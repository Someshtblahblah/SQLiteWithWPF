using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GridViewDataTable.Models;
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

        public static IList GetBindableSelectedItems(DependencyObject obj) =>
            (IList)obj.GetValue(BindableSelectedItemsProperty);

        public static void SetBindableSelectedItems(DependencyObject obj, IList value) =>
            obj.SetValue(BindableSelectedItemsProperty, value);

        private static void OnBindableSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as RadGridView;
            if (grid == null) return;

            grid.SelectedItems.Clear();
            if (e.NewValue is IList newList)
            {
                foreach (var item in newList)
                    grid.SelectedItems.Add(item);
            }

            grid.SelectionChanged -= TasksGrid_SelectionChanged;
            grid.SelectionChanged += TasksGrid_SelectionChanged;
        }

        private static void TasksGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var grid = sender as RadGridView;
            if (grid == null) return;

            var bindableSelectedItems = GetBindableSelectedItems(grid);
            if (bindableSelectedItems == null) return;

            bindableSelectedItems.Clear();
            foreach (var item in grid.SelectedItems)
                bindableSelectedItems.Add(item);
        }
    }
}
