namespace DesktopFolder;

internal static class L10n
{
    public static string Language = "zh";

    public static bool IsEn => Language == "en";

    public static string Get(string zh, string en) => IsEn ? en : zh;
}
