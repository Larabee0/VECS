using LiteHtmlSharp;
using Hexa.NET.ImGui;
using System;
using System.Numerics;

namespace VECS.UI
{
    public static class LiteHtmlExtentions
    {
        public static ImRect GetImRect(this position pos)
        {
            var center = new Vector2(pos.x, pos.y);
            var extent = new Vector2(pos.width, pos.height);
            return new ImRect(center , extent + center);
        }

        public static ImColor GetImColor(this web_color colour)
        {
            return new(new Colour(colour.red, colour.green, colour.blue, colour.alpha).ToColour());
        }

        public unsafe static uint GetUintColour(this web_color colour)
        {
            uint col = 0;
            var vecsColour = new Colour(colour.red, colour.green, colour.blue,colour.alpha);
            Buffer.MemoryCopy(&vecsColour, &col, sizeof(uint), sizeof(Colour));

            return col;
        }
    }

    public class LiteHtml
    {
        private VkViewportContainer vkViewportContainer;
        
        public LiteHtml(IMGUI imGUi)
        {
            vkViewportContainer = new("html {\r\n    display: block;\r\n    height:100%;\r\n    width:100%;\r\n\tposition: relative;\r\n}\r\n\r\nhead {\r\n    display: none\r\n}\r\n\r\nmeta {\r\n    display: none\r\n}\r\n\r\ntitle {\r\n    display: none\r\n}\r\n\r\nlink {\r\n    display: none\r\n}\r\n\r\nstyle {\r\n    display: none\r\n}\r\n\r\nscript {\r\n    display: none\r\n}\r\n\r\nbody {\r\n\tdisplay:block; \r\n\tmargin:8px; \r\n    height:100%;\r\n    width:100%;\r\n}\r\n\r\np {\r\n\tdisplay:block; \r\n\tmargin-top:1em; \r\n\tmargin-bottom:1em;\r\n}\r\n\r\nb, strong {\r\n\tdisplay:inline; \r\n\tfont-weight:bold;\r\n}\r\n\r\ni, em {\r\n\tdisplay:inline; \r\n\tfont-style:italic;\r\n}\r\n\r\ncenter \r\n{\r\n\ttext-align:center;\r\n\tdisplay:block;\r\n}\r\n\r\na:link\r\n{\r\n\ttext-decoration: underline;\r\n\tcolor: #00f;\r\n\tcursor: pointer;\r\n}\r\n\r\nh1, h2, h3, h4, h5, h6, div {\r\n\tdisplay:block;\r\n}\r\n\r\nh1 {\r\n\tfont-weight:bold; \r\n\tmargin-top:0.67em; \r\n\tmargin-bottom:0.67em; \r\n\tfont-size: 2em;\r\n}\r\n\r\nh2 {\r\n\tfont-weight:bold; \r\n\tmargin-top:0.83em; \r\n\tmargin-bottom:0.83em; \r\n\tfont-size: 1.5em;\r\n}\r\n\r\nh3 {\r\n\tfont-weight:bold; \r\n\tmargin-top:1em; \r\n\tmargin-bottom:1em; \r\n\tfont-size:1.17em;\r\n}\r\n\r\nh4 {\r\n\tfont-weight:bold; \r\n\tmargin-top:1.33em; \r\n\tmargin-bottom:1.33em\r\n}\r\n\r\nh5 {\r\n\tfont-weight:bold; \r\n\tmargin-top:1.67em; \r\n\tmargin-bottom:1.67em;\r\n\tfont-size:.83em;\r\n}\r\n\r\nh6 {\r\n\tfont-weight:bold; \r\n\tmargin-top:2.33em; \r\n\tmargin-bottom:2.33em;\r\n\tfont-size:.67em;\r\n} \r\n\r\nbr {\r\n\tdisplay:inline-block;\r\n}\r\n\r\nbr[clear=\"all\"]\r\n{\r\n\tclear:both;\r\n}\r\n\r\nbr[clear=\"left\"]\r\n{\r\n\tclear:left;\r\n}\r\n\r\nbr[clear=\"right\"]\r\n{\r\n\tclear:right;\r\n}\r\n\r\nspan {\r\n\tdisplay:inline\r\n}\r\n\r\nimg {\r\n\tdisplay: inline-block;\r\n}\r\n\r\nimg[align=\"right\"]\r\n{\r\n\tfloat: right;\r\n}\r\n\r\nimg[align=\"left\"]\r\n{\r\n\tfloat: left;\r\n}\r\n\r\nhr {\r\n    display: block;\r\n    margin-top: 0.5em;\r\n    margin-bottom: 0.5em;\r\n    margin-left: auto;\r\n    margin-right: auto;\r\n    border-style: inset;\r\n    border-width: 1px\r\n}\r\n\r\n\r\n/***************** TABLES ********************/\r\n\r\ntable {\r\n    display: table;\r\n    border-collapse: separate;\r\n    border-spacing: 2px;\r\n    border-top-color:gray;\r\n    border-left-color:gray;\r\n    border-bottom-color:black;\r\n    border-right-color:black;\r\n}\r\n\r\ntbody, tfoot, thead {\r\n\tdisplay:table-row-group;\r\n\tvertical-align:middle;\r\n}\r\n\r\ntr {\r\n    display: table-row;\r\n    vertical-align: inherit;\r\n    border-color: inherit;\r\n}\r\n\r\ntd, th {\r\n    display: table-cell;\r\n    vertical-align: inherit;\r\n    border-width:1px;\r\n    padding:1px;\r\n}\r\n\r\nth {\r\n\tfont-weight: bold;\r\n}\r\n\r\ntable[border] {\r\n    border-style:solid;\r\n}\r\n\r\ntable[border|=0] {\r\n    border-style:none;\r\n}\r\n\r\ntable[border] td, table[border] th {\r\n    border-style:solid;\r\n    border-top-color:black;\r\n    border-left-color:black;\r\n    border-bottom-color:gray;\r\n    border-right-color:gray;\r\n}\r\n\r\ntable[border|=0] td, table[border|=0] th {\r\n    border-style:none;\r\n}\r\n\r\ncaption {\r\n\tdisplay: table-caption;\r\n}\r\n\r\ntd[nowrap], th[nowrap] {\r\n\twhite-space:nowrap;\r\n}\r\n\r\ntt, code, kbd, samp {\r\n    font-family: monospace\r\n}\r\n\r\npre, xmp, plaintext, listing {\r\n    display: block;\r\n    font-family: monospace;\r\n    white-space: pre;\r\n    margin: 1em 0\r\n}\r\n\r\n/***************** LISTS ********************/\r\n\r\nul, menu, dir {\r\n    display: block;\r\n    list-style-type: disc;\r\n    margin-top: 1em;\r\n    margin-bottom: 1em;\r\n    margin-left: 0;\r\n    margin-right: 0;\r\n    padding-left: 40px\r\n}\r\n\r\nol {\r\n    display: block;\r\n    list-style-type: decimal;\r\n    margin-top: 1em;\r\n    margin-bottom: 1em;\r\n    margin-left: 0;\r\n    margin-right: 0;\r\n    padding-left: 40px\r\n}\r\n\r\nli {\r\n    display: list-item;\r\n}\r\n\r\nul ul, ol ul {\r\n    list-style-type: circle;\r\n}\r\n\r\nol ol ul, ol ul ul, ul ol ul, ul ul ul {\r\n    list-style-type: square;\r\n}\r\n\r\ndd {\r\n    display: block;\r\n    margin-left: 40px;\r\n}\r\n\r\ndl {\r\n    display: block;\r\n    margin-top: 1em;\r\n    margin-bottom: 1em;\r\n    margin-left: 0;\r\n    margin-right: 0;\r\n}\r\n\r\ndt {\r\n    display: block;\r\n}\r\n\r\nol ul, ul ol, ul ul, ol ol {\r\n    margin-top: 0;\r\n    margin-bottom: 0\r\n}\r\n\r\nblockquote {\r\n\tdisplay: block;\r\n\tmargin-top: 1em;\r\n\tmargin-bottom: 1em;\r\n\tmargin-left: 40px;\r\n\tmargin-left: 40px;\r\n}\r\n\r\n/*********** FORM ELEMENTS ************/\r\n\r\nform {\r\n\tdisplay: block;\r\n\tmargin-top: 0em;\r\n}\r\n\r\noption {\r\n\tdisplay: none;\r\n}\r\n\r\ninput, textarea, keygen, select, button, isindex {\r\n\tmargin: 0em;\r\n\tcolor: initial;\r\n\tline-height: normal;\r\n\ttext-transform: none;\r\n\ttext-indent: 0;\r\n\ttext-shadow: none;\r\n\tdisplay: inline-block;\r\n}\r\ninput[type=\"hidden\"] {\r\n\tdisplay: none;\r\n}\r\n\r\n\r\narticle, aside, footer, header, hgroup, nav, section \r\n{\r\n\tdisplay: block;\r\n}\r\n\r\n",null);
            imGUi.ClearColour = Vector4.One;
            vkViewportContainer.RenderHtmlRequested += Container_RenderHtmlRequested;
        }


        public void LoadHtml(string html)
        {
            vkViewportContainer.Document.CreateFromString(html);
        }

        public void Render()
        {

            vkViewportContainer.CheckViewportChange(forceRender: true);
            vkViewportContainer.Draw();
        }
        private void Container_RenderHtmlRequested(string html)
        {
            LoadHtml(html);
        }

        public void SetViewport(int width, int height)
        {
            vkViewportContainer.ResetViewport();
            vkViewportContainer.SetViewport(new(),new(width,height));
        }

    }


    public class VkViewportContainer : ViewportContainer
    {
        public static string BaseURL;
        public event Action<string> RenderHtmlRequested;

        public VkViewportContainer(string masterCssData, ILibInterop libInterop) : base(masterCssData, LibInterop.Instance)
        {
        }

        protected override nuint CreateFont(
            string faceName,
            int size,
            int weight,
            font_style italic,
            font_decoration decoration,
            ref font_metrics fm)
        {
            fm.x_height = 8;
            fm.ascent = 11;
            fm.descent = 3;
            fm.height = 14;
            fm.draw_spaces = true;
            return 0;
        }

        protected override void DrawBackground(nuint hdc,
            string image,
            background_repeat repeat,
            ref web_color color,
            ref position pos,
            ref border_radiuses br,
            ref position borderBox,
            bool isRoot)
        {
            var viewPort = ImGui.GetMainViewport();

            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);

            ImRect rect = pos.GetImRect();

            if (string.IsNullOrEmpty(image))
            {
                float thickness = 1.0f;
                uint colour = color.GetUintColour();
                var p1 = new Vector2(rect.Max.X - br.top_right_x, rect.Max.Y);
                var p2 = new Vector2(rect.Max.X, rect.Max.Y);
                var p3 = new Vector2(rect.Max.X, rect.Max.Y + br.top_right_y);
                ImGui.AddBezierQuadratic(backgroundDrawList, p1, p2, p3, colour, thickness);

                p1 = new Vector2(rect.Max.X, rect.Min.Y - br.bottom_right_y);
                p2 = new Vector2(rect.Max.X, rect.Min.Y);
                p3 = new Vector2(rect.Max.X - br.bottom_right_x, rect.Min.Y);
                ImGui.AddBezierQuadratic(backgroundDrawList, p1, p2, p3, colour, thickness);

                p1 = new Vector2(rect.Min.X + br.bottom_left_x, rect.Min.Y);
                p2 = new Vector2(rect.Min.X, rect.Min.Y);
                p3 = new Vector2(rect.Min.X, rect.Min.Y - br.bottom_left_y);
                ImGui.AddBezierQuadratic(backgroundDrawList, p1, p2, p3, colour, thickness);

                p1 = new Vector2(rect.Min.X, rect.Max.Y + br.top_left_y);
                p2 = new Vector2(rect.Min.X, rect.Max.Y);
                p3 = new Vector2(rect.Min.X + br.top_left_x, rect.Max.Y);
                ImGui.AddBezierQuadratic(backgroundDrawList, p1, p2, p3, colour, thickness);

                ImGui.AddRectFilled(backgroundDrawList, rect.Min, rect.Max, colour);

            }
            else
            {
                DrawImage(image, rect);
            }
        }

        private void DrawImage(string image, ImRect rect)
        {
            var bitmap = LoadImage(image);
            if (bitmap == null)
            {
                return;
            }
            Vector2 Extents = (rect.Max - rect.Min) * 0.5f;
            // new ImTextureData(,)
            // ImGui.Image(, Extents);
        }

        private Texture2D LoadImage(string image)
        {
            return null;
        }

        protected override void DrawBorders(nuint hdc, ref borders borders, ref position draw_pos, bool root)
        {
            // Skinny controls can push borders off, in which case we can't create a rect with a negative size.
            if (draw_pos.width < 0) draw_pos.width = 0;
            if (draw_pos.height < 0) draw_pos.height = 0;
            var rect = draw_pos.GetImRect();
            var br = borders.radius;

            var viewPort = ImGui.GetMainViewport();

            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);

            if (borders.top.width > 0)
            {
                var p1 = new Vector2(rect.Min.X + br.top_left_x, rect.Max.Y);
                var p2 = new Vector2(rect.Max.X - br.top_right_x, rect.Max.Y);
                var p3 = new Vector2(rect.Max.X, rect.Max.Y);
                var p4 = new Vector2(rect.Max.X, rect.Max.Y + br.top_right_y);

                DrawCurvedPath(backgroundDrawList, p1, p2, p3, p4, ref borders.top.color, borders.top.width);
            }
            if (borders.right.width > 0)
            {
                var p1 = new Vector2(rect.Max.X, rect.Max.Y + br.top_right_y);
                var p2 = new Vector2(rect.Max.X, rect.Min.Y - br.bottom_right_y);
                var p3 = new Vector2(rect.Max.X, rect.Min.Y);
                var p4 = new Vector2(rect.Max.X - br.bottom_right_x, rect.Min.Y);

                DrawCurvedPath(backgroundDrawList, p1, p2, p3, p4, ref borders.right.color, borders.right.width);
            }
            if (borders.bottom.width > 0)
            {
                var p1 = new Vector2(rect.Max.X - br.bottom_right_x, rect.Min.Y);
                var p2 = new Vector2(rect.Min.X + br.bottom_left_x, rect.Min.Y);
                var p3 = new Vector2(rect.Min.X, rect.Min.Y);
                var p4 = new Vector2(rect.Min.X, rect.Min.Y - br.bottom_left_y);

                DrawCurvedPath(backgroundDrawList, p1, p2, p3, p4, ref borders.bottom.color, borders.bottom.width);
            }
            if (borders.left.width > 0)
            {
                var p1 = new Vector2(rect.Min.X, rect.Min.Y - br.bottom_left_y);
                var p2 = new Vector2(rect.Min.X, rect.Max.Y + br.top_left_y);
                var p3 = new Vector2(rect.Min.X, rect.Max.Y);
                var p4 = new Vector2(rect.Min.X + br.top_left_x, rect.Max.Y);

                DrawCurvedPath(backgroundDrawList, p1, p2, p3, p4, ref borders.left.color, borders.left.width);
            }
        }
        private static void DrawCurvedPath(ImDrawListPtr self,Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, ref web_color color, float thickness)
        {
            ImGui.AddBezierQuadratic(self, p2,p3,p4, color.GetUintColour(), thickness);
            ImGui.AddLine(self,p4,p1,color.GetUintColour());
        }

        protected override void DrawListMarker(string image, string baseURL, list_style_type marker_type, ref web_color color, ref position pos)
        {

            var viewPort = ImGui.GetMainViewport();

            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);
            var rect = pos.GetImRect();
            ImGui.AddRectFilled(backgroundDrawList, rect.Min, rect.Max, color.GetUintColour());
        }

        protected override void DrawText(string text, nuint font, ref web_color color, ref position pos)
        {
            var viewPort = ImGui.GetMainViewport();
            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);
            text = text.Replace(' ', (char)160);
            ImGui.AddText(backgroundDrawList, new(pos.x, pos.y), color.GetUintColour(), text);
        }

        protected override string GetDefaultFontName()
        {
            return "Arial";
        }

        protected override int GetDefaultFontSize()
        {
            return 12;
        }

        protected override void GetImageSize(string image, ref size size)
        {
            var bmp = LoadImage(image);
            if (bmp != null)
            {
                //size.width = bmp.PixelWidth;
                //size.height = bmp.PixelHeight;
            }
        }

        protected override int GetTextWidth(string text, nuint font)
        {
            text = text.Replace(' ', (char)160);
            return text.Length * (GetDefaultFontSize());
        }


        protected override int PTtoPX(int pt)
        {
            return pt;
        }

        protected override void SetBaseURL(string base_url)
        {
            base_url = BaseURL;
        }

        protected override void SetCaption(string caption)
        {

        }

        protected override void SetCursor(string cursor)
        {

        }


        public override void Render(string html)
        {
            RenderHtmlRequested?.Invoke(html);
        }

    }

}
