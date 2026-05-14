using Noesis;
using SDL3;
using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisEditorView : PresentationSystemBase
    {
        private const bool ALWAYS_RE_RENDER = true;
        private static readonly int inputTextureId = "inputTexture".GetShaderPropertyId();

        private EntityQuery _hierarchyEntities;
        private EntityQuery _singleEntities;

        private ItemsControl _hierarchyContainer;
        private List<EntityHierarchyTree> _hierarchyTrees = [];


        private NoesisViewWrapper MainView;

        private NoesisRenderTarget RenderTarget;

        private Texture2D RenderTargetTex2D => ((NoesisTexture)RenderTarget.Texture).Texture;

        private Material _blitVariant;

        private int _framesSinceLastRender = 0;
        private FrameworkElement controlTreeRoot;

        public override void OnCreate(EntityManager entityManager)
        {
            _hierarchyEntities = new EntityQuery(entityManager)
                .WithAll(typeof(Children))
                .WithNone(typeof(Parent))
                .Build();

            _singleEntities = new EntityQuery(entityManager)
                .WithNone(typeof(Children), typeof(Parent))
                .Build();

            controlTreeRoot = (FrameworkElement)GUI.LoadXaml(System.IO.Path.Combine(Asset.AssetsPath, "GUI", "Editor/MainWindow.xaml"));

            MainView = new NoesisViewWrapper(controlTreeRoot, Application.NoesisDriver)
            {
                RenderFlags = RenderFlags.PPAA
            };
            MainView.SetSize(Screen.Width, Screen.Height);

            MainView.View.Content.GotKeyboardFocus += GotKeyboardFocus;
            MainView.View.Content.LostKeyboardFocus += LostKeyboardFocus;
            _hierarchyContainer = (ItemsControl)controlTreeRoot.FindName("HierarchyContainer");
            _hierarchyContainer.Items.Clear();
            var gameview = (Image)controlTreeRoot.FindName("GameView");
            var fowardRenderer = (Presenter<ForwardRenderer>)Presenter.Instance;
            var colourTarget = fowardRenderer.Renderer.MainColourAttachment.Target;
            var textureSource = new TextureSource(new NoesisTexture(colourTarget, false, true));
            gameview.Source = textureSource;

            InputManager.Instance.OnKeyDown += ViewKeyDown;
            InputManager.Instance.OnKeyUp += ViewKeyUp;

            controlTreeRoot.UpdateLayout();
            RenderTarget = (NoesisRenderTarget)NoesisHandler.NoesisDriver.CreateRenderTarget("Noesis_RT", (uint)Screen.Width, (uint)Screen.Height, 1, true);

            _blitVariant = EnginePipes.Blit.Create("Noesis_Blitter");
            _blitVariant.SetTexture(inputTextureId, RenderTargetTex2D);
            Application.NoesisDriver.CreatePipelines(VkFormat.R8G8B8A8Unorm, VkFormat.S8Uint);
        }

        private void GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if(args.NewFocus is TextBoxBase || args.NewFocus is PasswordBox)
            {
                SDL3WindowManager.MainWindow.BeginText();
            }
        }


        private void LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            SDL3WindowManager.MainWindow.EndText();
        }

        private void ViewKeyDown(SDL_Keycode keycode)
        {
            MainView.View.KeyDown(keycode.ToNoesis());
        }

        private void ViewKeyUp(SDL_Keycode keycode)
        {
            MainView.View.KeyUp(keycode.ToNoesis());
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            UpdateHierarchy(entityManager);

            MainView.Update();
            UpdateInputs();
        }

        private void UpdateHierarchy(EntityManager entityManager)
        {

            if (_hierarchyEntities.HasEntities)
            {
                var hierarchyEntities = _hierarchyEntities.GetEntities();
                if(_hierarchyTrees.Count != hierarchyEntities.Count)
                {
                    RebuildHierarchies(hierarchyEntities, entityManager);
                    controlTreeRoot.UpdateLayout();
                }
            }

            if (_singleEntities.HasEntities)
            {
                var  singleEntities = _singleEntities.GetEntities();

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

        private void UpdateInputs()
        {
            var view = MainView.View;

            InputManager input = InputManager.Instance;
            var mousePos = new Vector2Int((int)input.MousePos.X, (int)input.MousePos.Y);
            mousePos.X = Math.Clamp(mousePos.X, 0, Screen.Width);
            mousePos.Y = Math.Clamp(mousePos.Y, 0, Screen.Height);
            for (MouseButton i = 0; i <= MouseButton.XButton2; i++)
            {
                MouseButtonDown(mousePos.X, mousePos.Y, i, view);
                MouseButtonUp(mousePos.X,mousePos.Y, i, view);
            }
            view.MouseMove(mousePos.X, mousePos.Y);
            if (!string.IsNullOrEmpty(input.Text))
            {
                for (int i = 0; i < input.Text.Length; i++)
                {
                    view.Char(input.Text[i]);
                }
            }
            int xDir = Math.Clamp((int)input.MouseWheelH, -1, 1);
            int yDir = Math.Clamp((int)input.MouseWheel, -1, 1);
            view.MouseWheel(xDir, 0, (int)Math.Abs(input.MouseWheelH));
            view.MouseWheel(0, yDir, (int)Math.Abs(input.MouseWheel));
        }

        private static void MouseButtonDown(int x, int y, MouseButton button, View view)
        {
            if (InputManager.Instance.GetMouseButtonDown((int)button))
            {
                view.MouseButtonDown(x,y, button);
            }
        }

        private static void MouseButtonUp(int x, int y, MouseButton button, View view)
        {
            if (InputManager.Instance.GetMouseButtonUp((int)button))
            {
                view.MouseButtonUp(x, y, button);
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            Application.NoesisDriver.CurrentFrameInfo = default;
        }

        public override void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            Application.NoesisDriver.CurrentFrameInfo = frameInfo;

            if (MainView.PreRender() || ALWAYS_RE_RENDER)
            {
                _framesSinceLastRender = 0;
            }

            if (_framesSinceLastRender < SwapChain.MAX_CONCURRENT_FRAMES + 1)
            {
                GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "NOESIS Begin On-Screen Render");
                Application.NoesisDriver.FormatHash = HashCode.Combine(RenderTargetTex2D.Format, VkFormat.S8Uint);
                _framesSinceLastRender++;
                NoesisHandler.NoesisDriver.SetRenderTarget(RenderTarget);

                NoesisHandler.NoesisDriver.BeginTile(RenderTarget, new() { Y = 0, X = 0, Height = (uint)Screen.Height, Width = (uint)Screen.Width});
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(frameInfo.CommandBuffer, VkSampleCountFlags.Count1);
                MainView.Render();
                
                NoesisHandler.NoesisDriver.EndTile(RenderTarget);
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
                GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
            }

            BlitToMain(frameInfo);
        }

        public unsafe void BlitToMain(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "NOESIS Blit to Main");
            var _outputTarget = EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId).First;

            if (RenderTargetTex2D.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            else if (RenderTargetTex2D.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.FragmentShader);
            }

            var targetLayout = _outputTarget.ImageLayout;

            if (targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (_outputTarget.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (_outputTarget.ImageLayout == VkImageLayout.TransferSrcOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
            }

            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = _outputTarget._imageView,
                imageLayout = _outputTarget.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)_outputTarget.Width, (uint)_outputTarget.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, _outputTarget.Height, _outputTarget.Width, -_outputTarget.Height);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(_outputTarget.Width, _outputTarget.Height)));

            _blitVariant.Bind(frameInfo);

            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            if (targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (targetLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (targetLayout == VkImageLayout.TransferSrcOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
                }
            }

            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            SDL3WindowManager.MainWindow.EndText();
            MainView.View.Content.GotKeyboardFocus -= GotKeyboardFocus;
            MainView.View.Content.LostKeyboardFocus -= LostKeyboardFocus;
            InputManager.Instance.OnKeyDown -= ViewKeyDown;
            InputManager.Instance.OnKeyUp -= ViewKeyUp;
            MainView.View.Renderer.Shutdown();
            MainView.View.Dispose();
        }

        private class EntityHierarchyTree
        {
            public TreeView TreeView;
            private readonly ItemsControl HierarchyContainer;

            public EntityHierarchyTree(ItemsControl hierarchyContainer)
            {
                TreeView = new TreeView();
                hierarchyContainer.Items.Add(TreeView);
                HierarchyContainer = hierarchyContainer;
            }

            public void DestroyTree()
            {
                HierarchyContainer.Items.Remove(TreeView);
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
