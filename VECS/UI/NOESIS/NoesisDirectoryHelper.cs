using Noesis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace VECS.UI
{
    public static class NoesisDirectoryHelper
    {
        public static StackPanel DirectoryPath;
        public static StackPanel DirectoryStackPanel;
        public static StackPanel InspectorStackPanel;

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

        private static void SetDirectoryPath(string path)
        {
            DirectoryPath.Children.Clear();
            var assetPathInfo = new DirectoryInfo(Asset.AssetsPath);
            var relativePath = Path.GetRelativePath(Asset.AssetsPath, path);

            var directoriesSplit = relativePath.Split('\\');

            var directoryNameTextBlock = new TextBlock(Path.GetFileName(Asset.AssetsPath))
            {
                Tag = Asset.AssetsPath,
                FontSize = 20
            };
            ((SolidColorBrush)directoryNameTextBlock.Foreground).Color = Color.FromRgb(255, 255, 255);
            WeakReference weakReference = new(directoryNameTextBlock);
            ((TextBlock)weakReference.Target).MouseLeftButtonUp += (s, a) => SelectInternal((string)((TextBlock)s).Tag);

            DirectoryPath.Children.Add(directoryNameTextBlock);


            if (path == Asset.AssetsPath)
            {
                directoryNameTextBlock.FontWeight = FontWeight.Bold;
                return;
            }


            string buildUpPath = Asset.AssetsPath;

            for (int i = 0; i < directoriesSplit.Length; i++)
            {
                directoryNameTextBlock = new TextBlock(directoriesSplit[i])
                {
                    FontSize = 20,
                    
                };
                ((SolidColorBrush)directoryNameTextBlock.Foreground).Color = Color.FromRgb(255,255,255);
                if(i == directoriesSplit.Length - 1)
                {
                    directoryNameTextBlock.FontWeight = FontWeight.Bold;
                }
                var directorySeperator = new TextBlock(">")
                {
                    Tag = buildUpPath,
                    FontSize = 20
                };
                ((SolidColorBrush)directorySeperator.Foreground).Color = Color.FromRgb(255, 255, 255);
                buildUpPath = Path.Combine(buildUpPath, directoriesSplit[i]);
                directoryNameTextBlock.Tag = buildUpPath;
                DirectoryPath.Children.Add(directorySeperator);
                DirectoryPath.Children.Add(directoryNameTextBlock); 
                weakReference = new(directoryNameTextBlock);
                ((TextBlock)weakReference.Target).MouseLeftButtonUp += (s, a) => SelectInternal((string)((TextBlock)s).Tag);
                weakReference = new(directorySeperator);
                ((TextBlock)weakReference.Target).MouseLeftButtonUp += (s, a) => ListDirectoryContextMenu((TextBlock)s);
            }
        }

        private static void ListDirectoryContextMenu(TextBlock target)
        {
            if(target.ContextMenu != null)
            {
                target.ContextMenu.IsOpen = true;
                return;
            }
            var path = (string)target.Tag;
            
            if (!Directory.Exists(path)) return;
            DirectoryInfo info = new(path);

            var directories = info.GetDirectories();

            List<MenuItem> items = new(directories.Length);
            for (int i = 0; i < directories.Length; i++)
            {
                MenuItem menuItem = new()
                {
                    Header = directories[i].Name,
                    Tag = directories[i].FullName
                };
                WeakReference weakMenuItem = new(menuItem);
                ((MenuItem)weakMenuItem.Target).Click += (s, a) => SelectInternal((string)((MenuItem)s).Tag);
                items.Add(menuItem);
            }
            var contextmenu = new ContextMenu
            {
                ItemsSource = items
            };
            target.ContextMenu = contextmenu;
            contextmenu.IsOpen = true;
        }

        public static TreeViewItem GetDirectoryTree()
        {
            TreeViewItem rootAssets = new()
            {
                Header = Path.GetFileName(Asset.AssetsPath),
                IsExpanded = true,
                Tag = Asset.AssetsPath
            };


            //DirectoryNameOut.Text = Asset.AssetsPath;

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
            SetDirectoryPath(path);

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
                DirectoryStackPanel.Children.Add(lineItem);
                lineItem.IconImage = FolderIcon;
                WeakReference weak = new(lineItem);
                ((ButtonWithImage)weak.Target).OnDoubleClick += DirectoryDoubleClick;
            }

            

            for (int i = 0; i < files.Length; i++)
            {
                var lineItem = new ButtonWithImage
                {
                    Label = Path.GetFileNameWithoutExtension(files[i].Name),
                    Tag = files[i].FullName
                };

                var type = AssetManager.GetTypeFromExtension(files[i].Name);

                if (type == AssetType.Texture)
                {
                    lineItem.IconImage = ImageIcon;
                }
                else if(type == AssetType.Mesh)
                {
                    lineItem.IconImage = MeshIcon;
                }
                else if(type == AssetType.Shader)
                {
                    lineItem.IconImage = ShaderIcon;
                    lineItem.ToolTip = string.Format("plain text shader file ({0})", files[i].Extension);
                }
                else if ( type == AssetType.ShaderPreCompiled)
                {
                    lineItem.IconImage = PreCompiledShaderIcon;
                    lineItem.ToolTip = "Pre-compiled shader";
                }
                else
                {
                    lineItem.IconImage = UnknownTypeIcon;
                    lineItem.ToolTip = string.Format("Unknown File type ({0})", files[i].Extension);
                }

                DirectoryStackPanel.Children.Add(lineItem);
                WeakReference weak = new(lineItem);
                ((ButtonWithImage)weak.Target).MouseLeftButtonDown += LineItemSelect;
                ((ButtonWithImage)weak.Target).MouseRightButtonDown += LineItemSelect;
            }
        }

        private static void DirectoryDoubleClick(object sender, MouseButtonEventArgs args)
        {
            if (sender == null) return;
            string path = (string)((ButtonWithImage)sender).Tag;
            SelectInternal(path);
        }

        private static void LineItemSelect(object sender, MouseButtonEventArgs args)
        {
            if (sender == null) return;
            string path = (string)((ButtonWithImage)sender).Tag;
            PaintInspector(path);
        }

        private static string _selectedPath = string.Empty;
        
        private static void PaintInspector(string path)
        {
            NoesisInspectorHelper.InspectorTargetObjUpdated -= OnInspector;
            if (!File.Exists(path)) return;
            _selectedPath = path;
            var extension = Path.GetExtension(path);
            InspectorStackPanel.Children.Clear();
            if (extension == ".sp")
            {
                GraphicsPipelineDefinition graphicsDefinition = null;

                var loadShader = Task.Run(() =>
                {
                    graphicsDefinition = GraphicsPipelineDefinition.LoadDefinitionFromFile(path);
                });
                Stopwatch sw = Stopwatch.StartNew();
                var fieldHierarchy = NoesisInspectorHelper.GetTypeFields(typeof(GraphicsPipelineDefinition));
                var expander = NoesisInspectorHelper.ConstructTree(fieldHierarchy, null);

                var item = new Expander()
                {
                    Header = Path.GetFileName(path),
                    IsExpanded = true
                };
                StackPanel children = new();
                item.Content = children;
                InspectorStackPanel.Children.Add(item);

                children.Children.Add(expander);
                sw.Stop();
                Console.WriteLine("{0}ms",sw.ElapsedMilliseconds);
                loadShader.Wait();
                NoesisInspectorHelper.InspectorTargetObj = graphicsDefinition;
                NoesisInspectorHelper.UpdateValues(graphicsDefinition, expander,fieldHierarchy);
                NoesisInspectorHelper.InspectorTargetObjUpdated += OnInspector;
            }
        }

        private static void OnInspector()
        {
            var target = NoesisInspectorHelper.InspectorTargetObj;

            var extension = Path.GetExtension(_selectedPath);
            if(extension == ".sp" && target is GraphicsPipelineDefinition graphicsDefinition)
            {

                var pipelineName = Path.GetFileNameWithoutExtension(_selectedPath);

                var pipeline = AssetDataBase<GraphicsPipeline>.GetNamedSilentFail(pipelineName);
                if(pipeline != null && pipeline.Definition != null)
                {
                    if (pipeline.Definition.ShameShadersDifferentSettings(graphicsDefinition))
                    {
                        graphicsDefinition.Save(_selectedPath);
                        pipeline.SetDefinition(graphicsDefinition);
                        PipelineRecreation.EnqueueForRecreation(pipeline);
                    }
                    else if(!pipeline.Definition.SameShaderPrograms(graphicsDefinition))
                    {
                        graphicsDefinition.Save(_selectedPath);
                        pipeline.SetDefinition(graphicsDefinition);
                        PipelineRecreation.EnqueueShaderChanged(pipeline);
                    }
                }

            }
        }
    }
}