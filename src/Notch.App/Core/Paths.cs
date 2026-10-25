using System.IO;

namespace Notch.App.Core;

internal static class Paths
{
    public const string AppName = "Projeto Notch";
    public const string AppVersion = "1.0.0";

    public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjetoNotch");
    public static readonly string Plugins = Path.Combine(Data, "plugins");
    public static readonly string Shelf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjetoNotch", "Shelf");
    public static readonly string Log = Path.Combine(Data, "log.txt");

    public static void Ensure()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Plugins);
        Directory.CreateDirectory(Shelf);
    }

    public static void WriteLog(string message)
    {
        try
        {
            var info = new FileInfo(Log);
            if (info.Exists && info.Length > 512 * 1024) info.Delete();
            File.AppendAllText(Log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
    }
}
