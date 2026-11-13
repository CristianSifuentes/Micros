using System.Globalization;
using System.Text;

namespace Catalog.API;

/// <summary>
/// Musa holds no photographs. Every piece is given a portrait drawn from its own identity: the same id
/// always yields the same image, and no two ids agree. Art is painted in washes, fashion falls in pleats,
/// design is composed on a grid.
/// </summary>
public static class GenerativeArtwork
{
    private const int W = 600, H = 750;

    private static readonly string[][] Palettes =
    [
        ["#0b1d3a", "#1f3b73", "#c9a227", "#e8e1d0", "#7a1f1f"], // Prussian nocturne
        ["#1a1a1a", "#8c1c13", "#bf4342", "#e7d7c1", "#a78a7f"], // urushi
        ["#283618", "#606c38", "#fefae0", "#dda15e", "#bc6c25"], // garden
        ["#f4f1de", "#e07a5f", "#3d405b", "#81b29a", "#f2cc8f"], // opaline
        ["#14110f", "#34312d", "#7e7f83", "#d9c5b2", "#f3f3f4"], // charred oak
        ["#2b193d", "#484d6d", "#4b8f8c", "#c5979d", "#eadeda"], // violet hour
    ];

    public static string Render(Guid id, Discipline discipline)
    {
        var random = new Seeded(id);
        var palette = Palettes[random.Next(Palettes.Length)];
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" role="img"><defs><filter id="grain"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="{random.Next(999)}"/><feColorMatrix values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 0.11 0"/></filter><filter id="soft"><feGaussianBlur stdDeviation="{F(8 + random.NextDouble() * 14)}"/></filter></defs>""");

        switch (discipline)
        {
            case Discipline.Art: Paint(svg, random, palette); break;
            case Discipline.Fashion: Drape(svg, random, palette); break;
            default: Compose(svg, random, palette); break;
        }

        svg.Append($"""<rect width="{W}" height="{H}" filter="url(#grain)"/></svg>""");
        return svg.ToString();
    }

    /// <summary>Layered washes with a few decisive strokes on top.</summary>
    private static void Paint(StringBuilder svg, Seeded r, string[] p)
    {
        svg.Append($"""<rect width="{W}" height="{H}" fill="{p[0]}"/><g filter="url(#soft)">""");
        for (var i = 0; i < 9; i++)
        {
            double cx = r.NextDouble() * W, cy = r.NextDouble() * H, rx = 90 + r.NextDouble() * 240, ry = 70 + r.NextDouble() * 220;
            svg.Append(CultureInfo.InvariantCulture,
                $"""<ellipse cx="{F(cx)}" cy="{F(cy)}" rx="{F(rx)}" ry="{F(ry)}" fill="{p[1 + r.Next(4)]}" opacity="{F(0.25 + r.NextDouble() * 0.5)}" transform="rotate({F(r.NextDouble() * 180)} {F(cx)} {F(cy)})"/>""");
        }

        svg.Append("</g>");
        for (var i = 0; i < 5; i++)
        {
            double x = r.NextDouble() * W, y = r.NextDouble() * H;
            svg.Append(CultureInfo.InvariantCulture,
                $"""<path d="M{F(x)} {F(y)} q{F(r.Signed(220))} {F(r.Signed(160))} {F(r.Signed(320))} {F(r.Signed(260))} t{F(r.Signed(240))} {F(r.Signed(240))}" fill="none" stroke="{p[2 + r.Next(3)]}" stroke-width="{F(1 + r.NextDouble() * 7)}" stroke-linecap="round" opacity="{F(0.5 + r.NextDouble() * 0.5)}"/>""");
        }
    }

    /// <summary>A column of cloth: pleats that sway from shoulder to hem.</summary>
    private static void Drape(StringBuilder svg, Seeded r, string[] p)
    {
        svg.Append($"""<rect width="{W}" height="{H}" fill="{p[3]}"/>""");
        var pleats = 26 + r.Next(22);
        double sway = 20 + r.NextDouble() * 70, waist = 0.55 + r.NextDouble() * 0.35;
        for (var i = 0; i <= pleats; i++)
        {
            var t = i / (double)pleats;
            double top = 190 + t * 220, bottom = 60 + t * 480, mid = 300 + (t - 0.5) * 320 * waist + Math.Sin(t * 9) * sway;
            svg.Append(CultureInfo.InvariantCulture,
                $"""<path d="M{F(top)} 70 C{F(mid)} 300 {F(mid + r.Signed(30))} 470 {F(bottom)} 700" fill="none" stroke="{p[i % 2 == 0 ? 0 : 1]}" stroke-width="{F(1.5 + r.NextDouble() * 9)}" opacity="{F(0.35 + r.NextDouble() * 0.55)}"/>""");
        }

        svg.Append(CultureInfo.InvariantCulture,
            $"""<path d="M190 70 Q300 {F(20 + r.NextDouble() * 60)} 410 70" fill="none" stroke="{p[2]}" stroke-width="6" stroke-linecap="round"/>""");
    }

    /// <summary>Circles, quarters and bars set on a grid, after the Bauhaus.</summary>
    private static void Compose(StringBuilder svg, Seeded r, string[] p)
    {
        svg.Append($"""<rect width="{W}" height="{H}" fill="{p[3]}"/>""");
        const int cell = 150;
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                int x = col * cell, y = row * cell;
                var colour = p[r.Next(p.Length)];
                svg.Append(r.Next(6) switch
                {
                    0 => $"""<circle cx="{x + 75}" cy="{y + 75}" r="{40 + r.Next(35)}" fill="{colour}"/>""",
                    1 => $"""<path d="M{x} {y} h{cell} a{cell} {cell} 0 0 1 -{cell} {cell} z" fill="{colour}"/>""",
                    2 => $"""<rect x="{x}" y="{y + 50}" width="{cell}" height="{20 + r.Next(40)}" fill="{colour}"/>""",
                    3 => $"""<path d="M{x} {y + cell} a{cell} {cell} 0 0 1 {cell} -{cell} v{cell} z" fill="{colour}"/>""",
                    4 => $"""<rect x="{x + 50}" y="{y}" width="{20 + r.Next(40)}" height="{cell}" fill="{colour}"/>""",
                    _ => "",
                });
            }
        }
    }

    private static string F(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>xorshift seeded from the piece id: deterministic on every machine and runtime.</summary>
    private sealed class Seeded
    {
        private ulong _state;

        public Seeded(Guid id)
        {
            Span<byte> bytes = stackalloc byte[16];
            id.TryWriteBytes(bytes);
            // splitmix64 finalizer: neighbouring ids must not look like neighbours.
            var z = (BitConverter.ToUInt64(bytes[..8]) ^ BitConverter.ToUInt64(bytes[8..])) + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            _state = (z ^ (z >> 31)) | 1UL;
        }

        private ulong Step()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return _state;
        }

        public int Next(int exclusiveMax) => (int)(Step() % (ulong)exclusiveMax);
        public double NextDouble() => (Step() >> 11) / (double)(1UL << 53);
        public double Signed(double magnitude) => (NextDouble() * 2 - 1) * magnitude;
    }
}
