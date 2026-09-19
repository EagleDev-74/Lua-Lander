public static class Log
{
    private const string PREFIX = "<color=cyan> [AW.SUR.MI.74] </color>";

    private static string Format (object msg, string color, bool bold, bool italic)
    {
        string text = msg.ToString ();

        if (bold) text = $"<b> {text} </b>";

        if (italic) text = $"<i> {text} </i>";

        return $"{PREFIX} <color={color}> {text} </color>";
    }

    public static string Red (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "red", bold, italic);

    public static string Green (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "green", bold, italic);

    public static string Blue (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "blue", bold, italic);

    public static string Yellow (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "yellow", bold, italic);

    public static string Cyan (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "cyan", bold, italic);

    public static string Magenta (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "magenta", bold, italic);

    public static string White (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "white", bold, italic);

    public static string Grey (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "grey", bold, italic);

    public static string Black (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "black", bold, italic);

    public static string Orange (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "orange", bold, italic);

    public static string Purple (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "purple", bold, italic);

    public static string Brown (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "brown", bold, italic);

    public static string Maroon (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "maroon", bold, italic);

    public static string Olive (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "olive", bold, italic);

    public static string Teal (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "teal", bold, italic);

    public static string Navy (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "navy", bold, italic);

    public static string DarkBlue (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "darkblue", bold, italic);

    public static string LightBlue (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "lightblue", bold, italic);

    public static string Lime (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "lime", bold, italic);

    public static string Aqua (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "aqua", bold, italic);

    public static string Fuchsia (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "fuchsia", bold, italic);

    public static string Silver (this object msg, bool bold = false, bool italic = false) =>
        Format (msg, "silver", bold, italic);
}