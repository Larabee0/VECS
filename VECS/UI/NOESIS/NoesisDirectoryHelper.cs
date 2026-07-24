using Noesis;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Path = System.IO.Path;

namespace VECS.UI
{
    public static class NoesisDirectoryHelper
    {
        public static TextBlock DirectoryNameOut;
        public static StackPanel DirectoryStackPanel;

        private static TextureSource _folderIcon;
        private static TextureSource _imageIcon;
        private static TextureSource _unknownTypeIcon;
        private static TextureSource _shaderIcon;
        private static TextureSource _preCompiledShaderIcon;
        private static TextureSource _meshIcon;

        public static TextureSource FolderIcon
        {
            get
            {
                if(_folderIcon == null || _folderIcon.IsDisposed)
                {
                    var icon = LoadIcon("FolderIcon.png");
                    _folderIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _folderIcon;
            }
        }

        public static TextureSource ImageIcon
        {
            get
            {
                if (_imageIcon == null || _imageIcon.IsDisposed)
                {
                    var icon = LoadIcon("ImageIcon.png");
                    _imageIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _imageIcon;
            }
        }

        public static TextureSource UnknownTypeIcon
        {
            get
            {
                if (_unknownTypeIcon == null || _unknownTypeIcon.IsDisposed)
                {
                    var icon = LoadIcon("UnknownIcon.png");
                    _unknownTypeIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _unknownTypeIcon;
            }
        }

        public static TextureSource ShaderIcon
        {
            get
            {
                if (_shaderIcon == null || _shaderIcon.IsDisposed)
                {
                    var icon = LoadIcon("ShaderIcon.png");
                    _shaderIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _shaderIcon;
            }
        }
        public static TextureSource PreCompiledShaderIcon
        {
            get
            {
                if (_preCompiledShaderIcon == null || _preCompiledShaderIcon.IsDisposed)
                {
                    var icon = LoadIcon("PreCompiledShaderIcon.png");
                    _preCompiledShaderIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _preCompiledShaderIcon;
            }
        }

        public static TextureSource MeshIcon
        {
            get
            {
                if (_meshIcon == null || _meshIcon.IsDisposed)
                {
                    var icon = LoadIcon("MeshFileIcon.png");
                    _meshIcon = new TextureSource(new NoesisTexture(icon, true, true));
                }
                return _meshIcon;
            }
        }


        private static Texture2D LoadIcon(string iconName)
        {
            return TextureLoader.Load2D(Path.Combine(Asset.AssetsPath, "GUI", "Images", iconName), Vortice.Vulkan.VkFormat.R8G8B8A8Unorm);
        }

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
            var files = info.GetFiles().Where(x => x.Extension != ".meta").ToArray();
            if(subDirectories.Length == 0 && files.Length == 0) return;

            Console.WriteLine("Sub Directories: {0}\nFiles: {1}",subDirectories.Length,files.Length);

            for (int i = 0; i < subDirectories.Length; i++)
            {
                var lineItem = new ButtonWithImage
                {
                    Label = subDirectories[i].Name,
                    Tag = subDirectories[i].FullName
                };
                lineItem.IconImage.Source = FolderIcon;
                DirectoryStackPanel.Children.Add(lineItem);
                WeakReference weak = new(lineItem);
                ((ButtonWithImage)weak.Target).OnDoubleClick += DirectoryDoubleClick;
            }

            for (int i = 0; i < files.Length; i++)
            {
                var lineItem = new ButtonWithImage
                {
                    Label = Path.GetFileNameWithoutExtension(files[i].Name)
                };

                var type = AssetManager.GetTypeFromExtension(files[i].Name);

                if (type == AssetType.Texture)
                {
                    lineItem.IconImage.Source = ImageIcon;
                }
                else if(type == AssetType.Mesh)
                {
                    lineItem.IconImage.Source = MeshIcon;
                }
                else if(type == AssetType.Shader)
                {
                    lineItem.IconImage.Source = ShaderIcon;
                    lineItem.ToolTip = string.Format("plain text shader file ({0})", files[i].Extension);
                }
                else if ( type == AssetType.ShaderPreCompiled)
                {
                    lineItem.IconImage.Source = PreCompiledShaderIcon;
                    lineItem.ToolTip = "Pre-compiled shader";
                }
                else
                {
                    lineItem.IconImage.Source = UnknownTypeIcon;
                    lineItem.ToolTip = string.Format("Unknown File type ({0})", files[i].Extension);
                }

                DirectoryStackPanel.Children.Add(lineItem);
            }
        }

        private static void DirectoryDoubleClick(object arg1, MouseButtonEventArgs args)
        {
            if (arg1 == null) return;
            string path = (string)((ButtonWithImage)arg1).Tag;
            SelectInternal(path);
        }

    }
}