using Noesis;
using System;
using System.Collections.Generic;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.UI
{
    public class NoesisEditorView : PresentationSystemBase
    {
        private EntityQuery _hierarchyEntities;
        private EntityQuery _singleEntities;

        private StackPanel _hierarchyContainer;
        private readonly List<EntityHierarchyTree> _hierarchyTrees = [];
        private readonly List<EntityHierarchyTree> _singleEntityItems = [];


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

            MainView.View.Content.GotFocus += GotFocus;
            MainView.View.Content.LostFocus += LostFocus;
            MainView.View.Content.FocusableChanged += FocusChanged;
            _hierarchyContainer = (StackPanel)ControlTreeRoot.FindName("HierarchyContainer");
            _hierarchyContainer.Children.Clear();
            var gameview = (Image)ControlTreeRoot.FindName("GameView");
            var fowardRenderer = Presenter.Instance.Renderer;
            var colourTarget = fowardRenderer.MainColourAttachment.Target;
            var textureSource = new TextureSource(new NoesisTexture(colourTarget, false, true));
            gameview.Source = textureSource;

            ControlTreeRoot.UpdateLayout();
        }

        private void FocusChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var newValue = args.NewValue;
            if(newValue != null && newValue is TreeViewItem view)
            {
                Console.WriteLine(newValue.ToString());
                Console.WriteLine(view.Header);
            }
            Console.WriteLine("Focus Changed");
        }

        private void GotFocus(object sender, RoutedEventArgs args)
        {
            Console.WriteLine(args.Source.ToString());

            if (args.Source is ListBoxItem item && item.Content != null && item.Content is TreeView treeItem)
            {
               
                Console.WriteLine(((TreeViewItem)treeItem.Items.CurrentItem).Header.ToString());
            }
        }

        private void LostFocus(object sender, RoutedEventArgs args)
        {
            //Console.WriteLine(args.Source.ToString());
            if (args.Source is TreeViewItem)
            {
                //Console.WriteLine("TreeView");
            }
            
        }
        
        public override void OnUpdate(EntityManager entityManager)
        {
            UpdateHierarchy(entityManager);
            MainView.Update();
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
                if (_hierarchyTrees.Count != singleEntities.Count)
                {
                    RebuildSingleTrees(singleEntities, entityManager);
                    updateLayout = true;
                }
            }

            if (updateLayout)
            {
                ControlTreeRoot.UpdateLayout();
            }
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
                _hierarchyTrees.Add(new(_hierarchyContainer));
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
                _singleEntityItems.Add(new(_hierarchyContainer));
            }

            for (int i = 0; i < singleEntities.Count; i++)
            {
                _singleEntityItems[i].SetEntities(entityManager, singleEntities[i], null);
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            Application.NoesisDriver.CurrentFrameInfo = default;
        }

        public override void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            MainView.Render(frameInfo);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            MainView.View.Content.GotFocus -= GotFocus;
            MainView.View.Content.LostFocus -= LostFocus;
            MainView.View.Content.FocusableChanged -= FocusChanged;
            MainView.Dispose();
        }

        private class EntityHierarchyTree
        {
            public TreeView TreeView;
            private readonly StackPanel HierarchyContainer;

            public EntityHierarchyTree(StackPanel hierarchyContainer)
            {
                TreeView = new TreeView();
                hierarchyContainer.Children.Add(TreeView);
                HierarchyContainer = hierarchyContainer;
            }

            public void DestroyTree()
            {
                HierarchyContainer.Children.Remove(TreeView);
            }

            public void SetEntities(EntityManager entityManager, Entity entity, TreeViewItem parent)
            {
                var entityName = entityManager.GetEntityName(entity);
                
                TreeViewItem item = new()
                {
                    Header = entityName
                };
                
                if (parent == null)
                {
                    TreeView.Items.Clear();
                    TreeView.Items.Add(item);
                }
                else
                {
                    parent.Items.Add(item);
                }
                if (entityManager.GetComponent(entity, out Children children))
                {
                    for (int i = 0; i < children.Value.Length; i++)
                    {
                        SetEntities(entityManager, children.Value[i], item);
                    }
                }
            }
        }
    }
}
