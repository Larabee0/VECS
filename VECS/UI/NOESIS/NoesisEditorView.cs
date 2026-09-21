using Noesis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.ECS;
using VECS.ECS.Transforms;

namespace VECS.UI
{
    public class NoesisEditorView : SystemBase
    {
        private EntityQuery _hierarchyEntities;
        private EntityQuery _singleEntities;

        private TreeView _hierarchyTreeView;
        private TreeView _directoryTreeView;
        private readonly List<EntityHierarchyTree> _hierarchyTrees = [];
        private readonly List<EntityHierarchyTree> _singleEntityItems = [];

        private StackPanel _inspectorStackPanel;
        private Image _gameView;

        private uint SelectedEntityId;
        private uint LastSelectedEntityId;

        private NoesisViewWrapper MainView;

        private FrameworkElement ControlTreeRoot => MainView.ControlTreeRoot;

        public override void OnCreate(EntityManager entityManager)
        {
            _hierarchyEntities = new EntityQuery(entityManager)
                .WithAll(typeof(Children))
                .WithNone(typeof(Parent))
                .Build();

            _singleEntities = new EntityQuery(entityManager)
                .WithNone(typeof(Children), typeof(Parent))
                .Build();


            MainView = new NoesisViewWrapper("Editor/MainWindow.xaml");
            
            // MainView.View.Content.FocusableChanged += FocusChanged;
            _hierarchyTreeView = (TreeView)ControlTreeRoot.FindName("HierarchyTreeView");
            _hierarchyTreeView.SelectedItemChanged += EntityItemChanged;
            //_hierarchyTreeView.LostFocus += ClearInspector;

            _inspectorStackPanel = (StackPanel)ControlTreeRoot.FindName("InspectorStackPanel");

            _gameView = (Image)ControlTreeRoot.FindName("GameView");
            //UpdateGameView();

            NoesisDirectoryHelper.DirectoryPath = (StackPanel)ControlTreeRoot.FindName("DirectoryPath");
            NoesisDirectoryHelper.DirectoryStackPanel = (StackPanel)ControlTreeRoot.FindName("DirectoryStack");
            NoesisDirectoryHelper.InspectorStackPanel = _inspectorStackPanel;
            _directoryTreeView = (TreeView)ControlTreeRoot.FindName("OutlineTreeView");
            _directoryTreeView.SelectedItemChanged += NoesisDirectoryHelper.DirectoryTreeViewItemSelected;
            _directoryTreeView.Items.Add(NoesisDirectoryHelper.GetDirectoryTree());

            NoesisDirectoryHelper.SelectInternal(Asset.AssetsPath);

            ControlTreeRoot.UpdateLayout();
        }

        private void UpdateGameView()
        {
            var renderer = Presenter.Instance.Renderer;
            var colourTarget = renderer.MainColourAttachment.Target;
            var textureSource = new TextureSource(new NoesisTexture(colourTarget, false, true));
            _gameView.Source = textureSource;
        }

        private void ClearInspector(object sender, RoutedEventArgs args)
        {
            //SelectedEntityId = Entity.Null.Id;
        }


        private void EntityItemChanged(object sender, RoutedEventArgs args)
        {
            Console.WriteLine("TreeViewItemChanged"); 
            if(_hierarchyTreeView.SelectedItem is TreeViewItem treeView)
            {
                Console.WriteLine("Selected TreeView Item {0}",treeView.Header);
                if(treeView.Tag is uint entityId)
                {
                    SelectedEntityId = entityId;
                }
                else
                {
                    SelectedEntityId = Entity.Null.Id;
                }
            }
            else
            {
                SelectedEntityId = Entity.Null.Id;
                Console.WriteLine("De selected Item");
            }
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            UpdateInspectorEntity(entityManager);
            UpdateHierarchy(entityManager);
            MainView.Update();
        }

        private void UpdateInspectorEntity(EntityManager entityManager)
        {
            if(SelectedEntityId != LastSelectedEntityId)
            {
                _inspectorStackPanel.Children.Clear();
                AddEntityInspectorComponents(entityManager);
                LastSelectedEntityId = SelectedEntityId;
            }
        }

        private void AddEntityInspectorComponents(EntityManager entityManager)
        {
            var selectedEntity = entityManager.GetEntityFromId(SelectedEntityId);
            if(selectedEntity == Entity.Null) return;
            string entityName = string.Format("Entity: {0}", entityManager.GetEntityName(selectedEntity));
            var item = new Expander()
            {
                Header = entityName,
                IsExpanded = true
            };

            StackPanel children = new();
            item.Content = children;

            _inspectorStackPanel.Children.Add(item);
            var arcehtypeId = entityManager.ComputeArchetypeHash(selectedEntity);

            var componentIds = entityManager._archetypeIdsToComponentIds[arcehtypeId];
            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach(var componentId in componentIds)
            {
                var componentType = entityManager.GetComponentType(componentId);
                
                var types = NoesisInspectorHelper.GetTypeFields(componentType);
                var treeViewItem = NoesisInspectorHelper.ConstructTree(types,null);
                children.Children.Add(treeViewItem);

                NoesisInspectorHelper.UpdateValues(entityManager, treeViewItem, types, componentId, selectedEntity);
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ToString());

            NoesisInspectorHelper.SelectedEntity = SelectedEntityId;

        }


        private void UpdateHierarchy(EntityManager entityManager)
        {
            bool updateLayout = false;
            if (_hierarchyEntities.HasEntities)
            {
                var hierarchyEntities = _hierarchyEntities.GetEntities();
                if(_hierarchyTrees.Count != hierarchyEntities.Count)
                {
                    RebuildHierarchies(hierarchyEntities, entityManager);
                    updateLayout = true;
                }
            }

            if (_singleEntities.HasEntities)
            {
                var  singleEntities = _singleEntities.GetEntities();
                if (_singleEntityItems.Count != singleEntities.Count)
                {
                    RebuildSingleTrees(singleEntities, entityManager);
                    updateLayout = true;
                }
            }

            if (updateLayout)
            {
                ControlTreeRoot.UpdateLayout();
            }
            ControlTreeRoot.UpdateLayout();
        }

        private void RebuildHierarchies(List<Entity> hierarchyEntities, EntityManager entityManager)
        {
            while (hierarchyEntities.Count < _hierarchyTrees.Count)
            {
                var last = _hierarchyTrees[^1];
                last.DestroyTree();
                _hierarchyTrees.RemoveAt(_hierarchyTrees.Count - 1);
            }

            while (hierarchyEntities.Count > _hierarchyTrees.Count)
            {
                _hierarchyTrees.Add(new(_hierarchyTreeView));
            }

            for (int i = 0; i < hierarchyEntities.Count; i++)
            {
                _hierarchyTrees[i].SetEntities(entityManager, hierarchyEntities[i], null);
            }
        }

        private void RebuildSingleTrees(List<Entity> singleEntities, EntityManager entityManager)
        {
            while (singleEntities.Count < _singleEntityItems.Count)
            {
                var last = _singleEntityItems[^1];
                last.DestroyTree();
                _singleEntityItems.RemoveAt(_singleEntityItems.Count - 1);
            }

            while (singleEntities.Count > _singleEntityItems.Count)
            {
                _singleEntityItems.Add(new(_hierarchyTreeView));
            }

            for (int i = 0; i < singleEntities.Count; i++)
            {
                _singleEntityItems[i].SetEntities(entityManager, singleEntities[i], null);
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (Presenter.NewSwapChain)
            {
                UpdateGameView();
            }
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            _hierarchyTreeView.SelectedItemChanged -= EntityItemChanged;
            _directoryTreeView.SelectedItemChanged -= NoesisDirectoryHelper.DirectoryTreeViewItemSelected;
            _hierarchyTreeView.LostFocus -= ClearInspector;
            MainView.Dispose();
        }

        private class EntityHierarchyTree
        {
            public readonly TreeView TreeView;
            private TreeViewItem rootItem;

            public EntityHierarchyTree(TreeView treeView)
            {
                TreeView = treeView;
            }

            public void DestroyTree()
            {
                TreeView.Items.Remove(rootItem);
            }

            public void SetEntities(EntityManager entityManager, Entity entity, TreeViewItem parent)
            {
                var entityName = entityManager.GetEntityName(entity);


                TreeViewItem item = new()
                {

                    Header = entityName,
                    Tag = entity.Id

                };
                if (parent == null)
                {
                    rootItem = item;
                    TreeView.Items.Add(item);
                }
                else
                {
                    parent.Items.Add(item);
                }
                if (entityManager.GetComponent(entity, out Children children))
                {
                    for (int i = 0; i < children.Values.Length; i++)
                    {
                        SetEntities(entityManager, children.Values[i], item);
                    }
                }
            }
        }
    }
}
