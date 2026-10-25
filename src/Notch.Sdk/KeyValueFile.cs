using System.Globalization;
using System.IO;

namespace Notch.Sdk;

/// <summary>Arquivo de texto simples no formato chave=valor (um por linha).</summary>
public sealed class KeyValueFile
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string Path { get; }

    public KeyValueFile(string path)
    {
        Path = path;
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                var i = line.IndexOf('=');
                if (i > 0) _values[line[..i].Trim()] = line[(i + 1)..];
            }
        }
        catch (IOException) { }
    }

    public string Get(string key, string fallback = "") =>
        _values.TryGetValue(key, out var v) ? v : fallback;

    public int GetInt(string key, int fallback) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public bool GetBool(string key, bool fallback) =>
        _values.TryGetValue(key, out var v) ? v == "1" : fallback;

    public void Set(string key, string value) => _values[key] = value;
    public void Set(string key, int value) => _values[key] = value.ToString(CultureInfo.InvariantCulture);
    public void Set(string key, bool value) => _values[key] = value ? "1" : "0";

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllLines(Path, _values.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        catch (IOException) { }
    }
}
