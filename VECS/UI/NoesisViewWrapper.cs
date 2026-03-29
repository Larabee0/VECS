using Noesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{
    public class NoesisViewWrapper
    {
        public View View { get; set; }

        public TessellationMaxPixelError Quality
        {
            get => View.GetTessellationMaxPixelError();
            set
            {

                if (View.GetTessellationMaxPixelError().Equals(value))
                {
                    return;
                }

                View.SetTessellationMaxPixelError(value);
            }
        }


        public RenderFlags RenderFlags
        {
            get => View.GetFlags();
            set
            {
                if (View.GetFlags() == value)
                {
                    return;
                }

                if (View.Content is not null)
                {
                    if ((value & RenderFlags.PPAA) == RenderFlags.PPAA)
                    {
                        View.Content.PPAAMode = PPAAMode.Default;
                    }
                    else
                    {
                        View.Content.PPAAMode = PPAAMode.Disabled;
                    }
                }

                View?.SetFlags(value);
            }
        }

        public NoesisViewWrapper(FrameworkElement rootElement, RenderDevice renderDevice)
        {
            View = GUI.CreateView(rootElement);

            View.Renderer.Init(renderDevice);
        }

        public void PreRender()
        {
            View.Renderer.RenderOffscreen();
        }

        public void Render()
        {
            View.Renderer.Render();
        }

        public void SetSize(int width, int height)
        {
            View.SetSize(width, height);

            View.Update(0);

            View.Renderer.UpdateRenderTree();
        }

        public void Update(float deltaTime)
        {
            View.Update(deltaTime);

            View.Renderer.UpdateRenderTree();
        }
    }
}
