using LiteHtmlSharp;
using SDL3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{
    public static class LiteHTML
    {

        static LiteHTML()
        {
            
        }
    }
   
    public class VkViewportContainer : ViewportContainer
    {
        public VkViewportContainer(string masterCssData, ILibInterop libInterop) : base(masterCssData, libInterop)
        {

        }

        protected override nuint CreateFont(string faceName, int size, int weight, font_style italic, font_decoration decoration, ref font_metrics fm)
        {
            throw new NotImplementedException();
        }

        protected override void DrawBackground(nuint hdc, string image, background_repeat repeat, ref web_color color, ref position pos, ref border_radiuses borderRadiuses, ref position borderBox, bool isRoot)
        {
            throw new NotImplementedException();
        }

        protected override void DrawBorders(nuint hdc, ref borders borders, ref position draw_pos, bool root)
        {
            throw new NotImplementedException();
        }

        protected override void DrawListMarker(string image, string baseURL, list_style_type marker_type, ref web_color color, ref position pos)
        {
            throw new NotImplementedException();
        }

        protected override void DrawText(string text, nuint font, ref web_color color, ref position pos)
        {
            throw new NotImplementedException();
        }

        protected override string GetDefaultFontName()
        {
            throw new NotImplementedException();
        }

        protected override int GetDefaultFontSize()
        {
            throw new NotImplementedException();
        }

        protected override void GetImageSize(string image, ref size size)
        {
            throw new NotImplementedException();
        }

        protected override int GetTextWidth(string text, nuint font)
        {
            throw new NotImplementedException();
        }

        protected override int PTtoPX(int pt)
        {
            throw new NotImplementedException();
        }

        protected override void SetBaseURL(string base_url)
        {
            throw new NotImplementedException();
        }

        protected override void SetCaption(string caption)
        {
            throw new NotImplementedException();
        }

        protected override void SetCursor(string cursor)
        {
            throw new NotImplementedException();
        }
    }

}
