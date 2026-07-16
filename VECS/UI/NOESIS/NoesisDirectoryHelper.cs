using Noesis;
using System;
using System.IO;
using Path = System.IO.Path;

namespace VECS.UI
{
    public static class NoesisDirectoryHelper
    {
        public static TextBlock DirectoryNameOut;
        public static StackPanel DirectoryStackPanel;

        public static void BindDirectoryChanged()
        {
            
        }

        public static TreeViewItem GetDirectoryTree()
        {
            TreeViewItem rootAssets = new()
            {
                Header = Path.GetFileName(Asset.AssetsPath),
                IsExpanded = true,
                Tag = Asset.AssetsPath
            };

            DirectoryNameOut.Text = Asset.AssetsPath;

            var directories = Directory.GetDirectories( Asset.AssetsPath);
            
            for (int i = 0; i < directories.Length; i++)
            {
                rootAssets.Items.Add(ConstructTreeRecursively(directories[i]));
            }
            
            return rootAssets;
        }


        public static TreeViewItem ConstructTreeRecursively(string path)
        {
            TreeViewItem item = new()
            {
                Header = Path.GetFileName(path),
                IsExpanded = false,
                Tag = path
            };

            var directories = Directory.GetDirectories(path);
            for (int i = 0; i < directories.Length; i++)
            {
                item.Items.Add(ConstructTreeRecursively(directories[i]));
                
            }
            
            return item;
        }

        public static void DirectoryTreeViewItemSelected(object sender, RoutedEventArgs args)
        {
            if (sender is not TreeView item || item.SelectedItem is not TreeViewItem treeViewItem || treeViewItem.Tag is not string tagPath) return;
            
            
            SelectInternal(tagPath);
        }

        public static void SelectInternal(string path)
        {
            DirectoryNameOut.Text = path;
            
            DirectoryStackPanel.Children.Clear();
            
            DirectoryInfo info = new(path);
            
            if (!info.Exists) return;
            
            var subDirectories = info.GetDirectories();
            var files = info.GetFiles();
            if(subDirectories.Length == 0 && files.Length == 0) return;

            Console.WriteLine("Sub Directories: {0}\nFiles: {1}",subDirectories.Length,files.Length);
        }
    }
}