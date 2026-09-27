using System.Numerics;
using AsMobPlate;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;
using AsMobPlate.Windows;
using Dalamud.Bindings.ImGui;

unsafe
{
    var estimator = new ArrivalEstimator();
    var target = new Vector3(850, 0, 0);
    if (estimator.Estimate(Vector3.Zero, target, 25, 3) != null)
        throw new Exception("Unsampled arrival should be unknown.");
    for (var i = 0; i <= 20; i++)
        estimator.Update(new Vector3(i, 0, 0), i * 0.1);
    var arrival = estimator.Estimate(new Vector3(20, 0, 0), target, 25, 3);
    if (arrival == null || Math.Abs(arrival.Value - 83.5) > 0.01)
        throw new Exception("Approach speed did not produce the expected arrival time.");
    if (estimator.Estimate(new Vector3(20, 0, 0), new Vector3(-850, 0, 0), 25, 3) != null
        || estimator.Estimate(new Vector3(20, 0, 0), new Vector3(219, 0, 0), 25, 3) != null)
        throw new Exception("Receding or nearby targets should not produce an estimate.");
    estimator.Update(new Vector3(20, 0, 0), 2.1);
    if (estimator.Estimate(new Vector3(20, 0, 0), target, 25, 3) != null)
        throw new Exception("Stopping retained a stale arrival estimate.");
    estimator.Update(new Vector3(500, 0, 0), 2.2);
    if (estimator.Estimate(new Vector3(500, 0, 0), target, 25, 3) != null)
        throw new Exception("Teleport retained an arrival estimate.");
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
        foreach (var late in new[] { false, true })
        {
            var configuration = new Configuration { Language = language, Scale = scale, CountdownFrameThickness = 10 };
            var painter = new NameplatePainter(configuration);
            var data = new NameplateData(HuntRank.S, UiText.Get("Preview hunt name", language), 118, hp, 23,
                TimeSpan.FromSeconds(75), UiText.Format("Start countdown", language, "13:19", 6), inProgress, 6, 10, late ? 100 : null);
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
            if (late)
            {
                var red = ImGui.ColorConvertFloat4ToU32(configuration.ArrivalWarningRed);
                var yellow = ImGui.ColorConvertFloat4ToU32(configuration.ArrivalWarningYellow);
                var foundRed = false;
                var foundYellow = false;
                for (var v = beforeMeasure; v < list.VtxBuffer.Size; v++)
                {
                    foundRed |= list.VtxBuffer[v].Col == red;
                    foundYellow |= list.VtxBuffer[v].Col == yellow;
                }
                if (!foundRed || !foundYellow)
                    throw new Exception("Warning stripes missing their configured colors and alpha.");
            }
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
