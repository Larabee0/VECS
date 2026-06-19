using VECS.ECS;
using VECS.ECS.Presentation;

namespace VECS.UI
{
    public class NoesisTest : PresentationSystemBase
    {
        private NoesisViewWrapper MainView;

        public override void OnCreate(EntityManager entityManager)
        {
            MainView = new("ThemePreview.xaml");
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            MainView.Update();
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
            MainView.Dispose();
        }
    }
}