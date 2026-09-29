using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using AsMobPlate;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;
using AsMobPlate.Windows;
using Dalamud.Bindings.ImGui;

// Render the real plugin's ImGui geometry without a game client or game assets.
internal static unsafe class DocumentationImages
{
    public static void Export(string root, byte* atlas, int atlasWidth, int atlasHeight)
    {
        var docs = Path.Combine(root, "docs", "images");
        var installer = Path.Combine(root, "images");
        Directory.CreateDirectory(docs);
        Directory.CreateDirectory(installer);
        var config = new Configuration { Language = UiLanguage.EN, CountdownFrameThickness = 4 };
        var painter = new NameplatePainter(config);
        var idle = new NameplateData(HuntRank.A, "Sample hunt", 118, 1, 23, null, "", false, 0, 10);
        var countdown = idle with { StartText = "ET 13:13 -> 13:19 in 6s", RemainingSeconds = 6 };
        var progress = idle with { StartText = "ET 13:13 -> 13:19 in 0s", IsInProgress = true };
        var warning = idle with { Distance = 850, TimeToKill = TimeSpan.FromSeconds(35), ArrivalSeconds = 48 };
        Plate("nameplate-idle.png", idle);
        Plate("nameplate-countdown.png", countdown);
        Plate("nameplate-in-progress.png", progress);
        Plate("nameplate-arrival-warning.png", warning);
        Plate("et-announcement-nameplate.png", idle with
        {
            Rank = HuntRank.S, Distance = 37, StartText = "ET 00:47 -> 01:15 in 33s", RemainingSeconds = 33,
        });

        Render(Path.Combine(docs, "et-chat-announcement.png"), 500, 70, () =>
        {
            var list = ImGui.GetForegroundDrawList();
            list.AddText(new Vector2(16, 12), 0xffaaaaaa, "Example announcement (illustration)");
            list.AddText(new Vector2(16, 36), 0xffffffff, "ET115 start - please wait until the announced time.");
        }, atlas, atlasWidth, atlasHeight);

        var window = new ConfigWindow(config, new DebugLog()) { IsOpen = true };
        Render(Path.Combine(docs, "settings-en.png"), 720, 590, () =>
        {
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.SetNextWindowSize(new Vector2(700, 560));
            window.Draw();
        }, atlas, atlasWidth, atlasHeight);

        Render(Path.Combine(installer, "image1.png"), 720, 400, () =>
        {
            var list = ImGui.GetForegroundDrawList();
            list.AddText(new Vector2(24, 20), 0xffffffff, "AS Mob Plate - UI examples");
            painter.Draw(list, new Vector2(190, 170), idle);
            painter.Draw(list, new Vector2(530, 170), countdown);
            painter.Draw(list, new Vector2(190, 345), progress);
            painter.Draw(list, new Vector2(530, 345), warning);
        }, atlas, atlasWidth, atlasHeight);

        Render(Path.Combine(installer, "icon.png"), 512, 512, () =>
        {
            var list = ImGui.GetForegroundDrawList();
            list.AddRectFilled(new Vector2(48, 80), new Vector2(464, 432), 0xff161616, 8);
            list.AddText(ImGui.GetFont(), 72, new Vector2(124, 140), 0xff38d6ff, "A / S");
            list.AddRectFilled(new Vector2(80, 264), new Vector2(432, 294), 0xff5cd633, 2);
            list.AddText(ImGui.GetFont(), 40, new Vector2(116, 335), 0xffffffff, "MOB PLATE");
        }, atlas, atlasWidth, atlasHeight);
        Console.WriteLine("Exported 9 game-free UI images from native ImGui geometry.");

        void Plate(string name, NameplateData data) => Render(Path.Combine(docs, name), 360, 180, () =>
        {
            var size = painter.Measure(data);
            painter.Draw(ImGui.GetForegroundDrawList(), new Vector2(180, (180 + size.Y) / 2), data);
        }, atlas, atlasWidth, atlasHeight);
    }

    internal static void Render(string path, int width, int height, Action draw, byte* atlas, int atlasWidth, int atlasHeight)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(width, height);
        // Two frames allow ImGui to settle first-use window and content sizes.
        for (var frame = 0; frame < 2; frame++)
        {
            ImGui.NewFrame();
            draw();
            ImGui.Render();
        }
        var buffer = new byte[width * height * 4];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 36; buffer[i + 1] = 32; buffer[i + 2] = 29; buffer[i + 3] = 255;
        }
        var data = ImGui.GetDrawData();
        for (var n = 0; n < data.CmdListsCount; n++)
        {
            var list = new ImDrawListPtr(data.CmdLists[n]);
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var command = list.CmdBuffer[c];
                for (var index = (int)command.IdxOffset; index < command.IdxOffset + command.ElemCount; index += 3)
                {
                    var a = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[index]];
                    var b = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[index + 1]];
                    var d = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[index + 2]];
                    Triangle(a, b, d, command.ClipRect, buffer, width, height, atlas, atlasWidth, atlasHeight);
                }
            }
        }
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
                System.Runtime.InteropServices.Marshal.Copy(buffer, y * width * 4, locked.Scan0 + y * locked.Stride, width * 4);
        }
        finally { bitmap.UnlockBits(locked); }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static void Triangle(ImDrawVert a, ImDrawVert b, ImDrawVert c, Vector4 clip, byte[] output,
        int width, int height, byte* atlas, int tw, int th)
    {
        var area = Edge(a.Pos, b.Pos, c.Pos);
        if (Math.Abs(area) < 0.00001f) return;
        if (area < 0) { (b, c) = (c, b); area = -area; }
        var minX = Math.Max(0, (int)MathF.Ceiling(Math.Max(clip.X, Math.Min(a.Pos.X, Math.Min(b.Pos.X, c.Pos.X))) - 0.5f));
        var minY = Math.Max(0, (int)MathF.Ceiling(Math.Max(clip.Y, Math.Min(a.Pos.Y, Math.Min(b.Pos.Y, c.Pos.Y))) - 0.5f));
        var maxX = Math.Min(width, (int)MathF.Ceiling(Math.Min(clip.Z, Math.Max(a.Pos.X, Math.Max(b.Pos.X, c.Pos.X))) - 0.5f));
        var maxY = Math.Min(height, (int)MathF.Ceiling(Math.Min(clip.W, Math.Max(a.Pos.Y, Math.Max(b.Pos.Y, c.Pos.Y))) - 0.5f));
        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var ea = Edge(b.Pos, c.Pos, p); var eb = Edge(c.Pos, a.Pos, p); var ec = Edge(a.Pos, b.Pos, p);
            if (!Inside(ea, b.Pos, c.Pos) || !Inside(eb, c.Pos, a.Pos) || !Inside(ec, a.Pos, b.Pos)) continue;
            var wa = ea / area; var wb = eb / area; var wc = ec / area;
            var uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
            var texel = (Math.Clamp((int)(uv.Y * th), 0, th - 1) * tw + Math.Clamp((int)(uv.X * tw), 0, tw - 1)) * 4;
            var alpha = Channel(a.Col, b.Col, c.Col, 24, wa, wb, wc) / 255 * atlas[texel + 3] / 255;
            var dest = (y * width + x) * 4;
            for (var channel = 0; channel < 3; channel++)
            {
                var source = Channel(a.Col, b.Col, c.Col, channel * 8, wa, wb, wc) * atlas[texel + channel] / 255;
                output[dest + 2 - channel] = (byte)Math.Clamp(source * alpha + output[dest + 2 - channel] * (1 - alpha), 0, 255);
            }
        }
    }

    private static bool Inside(float edge, Vector2 a, Vector2 b) => edge > 0 || (edge == 0 && (b.Y < a.Y || (b.Y == a.Y && b.X > a.X)));
    private static float Channel(uint a, uint b, uint c, int shift, float wa, float wb, float wc) =>
        ((a >> shift) & 255) * wa + ((b >> shift) & 255) * wb + ((c >> shift) & 255) * wc;
}
