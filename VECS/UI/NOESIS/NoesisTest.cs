using VECS.ECS;

namespace VECS.UI
{
    public class NoesisTest : SystemBase
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

        public override void OnDestroy(EntityManager entityManager)
        {
            MainView.Dispose();
        }
    }
}