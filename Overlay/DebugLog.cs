using System;
using System.Text;

namespace AsMobPlate.Overlay;

public sealed class DebugLog
{
    private const int Capacity = 160;

    private readonly string[] lines = new string[Capacity];
    private int nextIndex;
    private int count;

    public string ClockStatus { get; internal set; } = "Game ET: waiting";

    public void Add(string message)
    {
        this.lines[this.nextIndex] = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        this.nextIndex = (this.nextIndex + 1) % Capacity;
        if (this.count < Capacity)
            this.count++;
    }

    public void Clear()
    {
        Array.Clear(this.lines, 0, this.lines.Length);
        this.nextIndex = 0;
        this.count = 0;
    }

    public string Dump()
    {
        if (this.count == 0)
            return string.Empty;

        var builder = new StringBuilder(this.count * 96);
        var start = (this.nextIndex - this.count + Capacity) % Capacity;
        for (var i = 0; i < this.count; i++)
        {
            var index = (start + i) % Capacity;
            builder.AppendLine(this.lines[index]);
        }

        return builder.ToString();
    }
}
