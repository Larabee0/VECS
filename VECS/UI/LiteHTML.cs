using Hexa.NET.ImGui;
using LiteHtmlSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace VECS.UI
{
    public static class LiteHtmlExtentions
    {
        public static string DefaultHtmlPath => Path.Combine(Asset.AssetsPath, "Html");
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
            string css = File.ReadAllText(Path.Combine(LiteHtmlExtentions.DefaultHtmlPath, "master.css"));
            vkViewportContainer = new(css,imGUi, null);
            //imGUi.ClearColour = Vector4.One;
            vkViewportContainer.RenderHtmlRequested += Container_RenderHtmlRequested;
        }


        public void LoadHtml(string htmlFile)
        {
            vkViewportContainer?.LoadHtml(htmlFile);
            ImGui.ShowDemoWindow();
        }

        public void Render()
        {
            vkViewportContainer.CheckViewportChange(forceRender: true);
            
            vkViewportContainer.Draw();
            
            //Console.WriteLine(new Vector2((float)vkViewportContainer.Size.Width, (float)vkViewportContainer.Size.Width));
        }
        private void Container_RenderHtmlRequested(string html)
        {
            LoadHtml(html);
        }

        public void SetViewport(int width, int height)
        {
            vkViewportContainer.ResetViewport();
            vkViewportContainer.SetViewport(new(),new(width,height));
            //Console.WriteLine(new Vector2(width,height));
        }

    }


    public class VkViewportContainer : ViewportContainer
    {
        public static string BaseURL;
        private FileInfo _fileInfo;
        private DirectoryInfo _directoryInfo;
        public event Action<string> RenderHtmlRequested;
        private readonly IMGUI _imgui;

        private Dictionary<int, ImTextureID> _textureLibrary = [];

        public VkViewportContainer(string masterCssData, IMGUI imgui, ILibInterop libInterop) : base(masterCssData, LibInterop.Instance)
        {
            _imgui = imgui;
        }

        protected unsafe override nuint CreateFont(
            string faceName,
            int size,
            int weight,
            font_style italic,
            font_decoration decoration,
            ref font_metrics fm)
        {
            var fontId = _imgui.AddFontTTF(Path.Combine(LiteHtmlExtentions.DefaultHtmlPath, "arial.ttf"),size);
            var font = _imgui.GetFont(fontId);
            
            var baked = font->GetFontBaked(size);
            ImGui.PushFont(font, size);
            float x_height = ImGui.CalcTextSize("x").Y;
            float ascent = baked->Ascent;
            float descent = baked->Descent;
            float height = ImGui.CalcTextSize("|").Y;

            fm.x_height = (int)x_height;
            fm.ascent = (int)ascent;
            fm.descent = (int)descent;
            fm.height = (int)height;
            fm.draw_spaces = true;
            ImGui.PopFont();
            return fontId;
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

        private unsafe void DrawImage(string image, ImRect rect)
        {
            var bitmap = LoadImage(image);
            if (bitmap == null)
            {
                return;
            }
            
            var viewPort = ImGui.GetMainViewport();

            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);

            var id = GetTexture(image);

            ImGui.AddImage(backgroundDrawList, new ImTextureRef(null, id), new(rect.Min.X,rect.Max.Y), new(rect.Max.X,rect.Min.Y));
        }

        private ImTextureID GetTexture(string image)
        {
            int originalTextureHash = ShaderProperties.Hash(image);
            if (_textureLibrary.TryGetValue(originalTextureHash, out var id))
            {
                return id;
            }
            return default;
        }

        private Texture2D LoadImage(string image)
        {
            int originalTextureHash = ShaderProperties.Hash(image);
            if(_textureLibrary.TryGetValue(originalTextureHash, out ImTextureID textureId))
            {
                return _imgui.GetTexture(textureId);
            }
            DirectoryInfo baseDirectory = new(_directoryInfo.FullName);
            while (image.StartsWith("../"))
            {
                baseDirectory = baseDirectory.Parent;
                image = image.Substring(3);
            }

            var textureInfo = new FileInfo(Path.Combine(baseDirectory.FullName, image));


            if (textureInfo.Exists)
            {
                var loadedTexture = AssetDataBase<Texture2D>.GetNamedSilentFail( Path.GetFileNameWithoutExtension(textureInfo.Name));


                loadedTexture ??=  new Texture2D(textureInfo.FullName, false);
                _textureLibrary.TryAdd(originalTextureHash, loadedTexture.Hash);
                _imgui.AddTexture(loadedTexture.Hash, loadedTexture);
            }

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

        protected unsafe override void DrawText(string text, nuint font, ref web_color color, ref position pos)
        {
            text = text.Replace(' ', (char)160);
            var viewPort = ImGui.GetMainViewport();
            var backgroundDrawList = ImGui.GetBackgroundDrawList(viewPort);
            var fontVal = _imgui.GetFont((uint)font);
            ImGui.PushFont(fontVal, GetDefaultFontSize());
            
            ImGui.AddText(backgroundDrawList, new(pos.x, pos.y), color.GetUintColour(), text);
            ImGui.PopFont();
        }

        protected override string GetDefaultFontName()
        {
            return "Arial";
        }

        protected override int GetDefaultFontSize()
        {
            return 45;
        }

        protected override void GetImageSize(string image, ref size size)
        {
            var bmp = LoadImage(image);
            if (bmp != null)
            {
                size.width = bmp.Width;
                size.height = bmp.Height;
            }
        }

        protected unsafe override int GetTextWidth(string text, nuint font)
        {
            text = text.Replace(' ', (char)160);
            var fontVal = _imgui.GetFont((uint)font);

            ImGui.PushFont(fontVal, GetDefaultFontSize());
            var textSize = ImGui.CalcTextSize(text);

            //ImGui.PopFont();
            return (int)Math.Ceiling(textSize.X);
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


        public void LoadHtml(string htmlFile)
        {

            _fileInfo = new FileInfo( Path.Combine(LiteHtmlExtentions.DefaultHtmlPath, htmlFile));
            if (_fileInfo.Exists)
            {
                _directoryInfo = _fileInfo.Directory;
                string html = File.ReadAllText(_fileInfo.FullName);
                Document.CreateFromString(html);
            }
            else
            {
                Console.WriteLine("HTML File not found at {0}", _fileInfo.FullName);
            }
        }
    }

}
