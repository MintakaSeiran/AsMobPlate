using System.Numerics;
using AsMobPlate;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;
using AsMobPlate.Windows;
using Dalamud.Bindings.ImGui;

unsafe
{
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.IniFilename = null;
        io.DisplaySize = new Vector2(1920, 1080);
        io.DeltaTime = 1f / 60;
        io.Fonts.AddFontDefault();
        byte* pixels = null;
        var width = 0;
        var height = 0;
        io.Fonts.GetTexDataAsRGBA32(0, &pixels, &width, &height);
        if (pixels == null || width <= 0 || height <= 0)
            throw new Exception("Font atlas did not build.");

        var cases = 0;
        foreach (var language in Enum.GetValues<UiLanguage>())
        foreach (var scale in new[] { 0.5f, 1f, 2.5f })
        foreach (var hp in new[] { 0f, 0.5f, 1f })
        foreach (var inProgress in new[] { false, true })
        {
            var configuration = new Configuration { Language = language, Scale = scale, CountdownFrameThickness = 10 };
            var painter = new NameplatePainter(configuration);
            var data = new NameplateData(HuntRank.S, UiText.Get("Preview hunt name", language), 118, hp, 23,
                TimeSpan.FromSeconds(75), UiText.Format("Start countdown", language, "13:19", 6), inProgress, 6, 10);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(io.DisplaySize);
            ImGui.Begin("Rendering test");
            var list = ImGui.GetWindowDrawList();
            var beforeMeasure = list.VtxBuffer.Size;
            var size = painter.Measure(data);
            if (list.VtxBuffer.Size != beforeMeasure)
                throw new Exception("Measurement drew geometry.");
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
                throw new Exception("Invalid plate bounds.");
            var drawnSize = painter.Draw(list, new Vector2(900, 600), data);
            if (size != drawnSize || list.VtxBuffer.Size <= beforeMeasure)
                throw new Exception("Measured and drawn plate mismatch or blank output.");
            ImGui.End();
            ImGui.Render();
            cases++;
        }

        foreach (var language in Enum.GetValues<UiLanguage>())
        foreach (var windowSize in new[] { new Vector2(520, 320), new Vector2(700, 560), new Vector2(1100, 850) })
        {
            var configuration = new Configuration { Language = language, Scale = 2.5f, HpBarWidth = 360 };
            var window = new ConfigWindow(configuration, new DebugLog()) { IsOpen = true };
            for (var frame = 0; frame < 3; frame++)
            {
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(windowSize);
                window.Draw();
                ImGui.Render();
                if (ImGui.GetDrawData().TotalVtxCount <= 0)
                    throw new Exception("Settings preview rendered no geometry.");
            }
            if (configuration.Scale != 2.5f || configuration.HpBarWidth != 360)
                throw new Exception("Preview changed live settings.");
            cases++;
        }

        Console.WriteLine($"{cases} native ImGui rendering cases passed (4 languages, HP states, scales, window sizes).");
        Console.WriteLine("These tests validate geometry and API use; they do not verify in-game fonts or GPU rendering.");
    }
    finally
    {
        ImGui.DestroyContext(context);
    }
}
