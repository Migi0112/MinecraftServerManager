// [소스 안내] server.properties 파일을 읽고 수정하는 기본 파일 처리입니다. 서버 옵션의 키와 값을 다룹니다.
using System.Text.RegularExpressions;

namespace NeoForgeServerManager;

// Values are shown in their Java .properties escaped representation; comments and ordering survive edits.
// 원본의 주석/줄 순서를 보존하는 파일 계층입니다. 외부 편집이 감지되면 덮어쓰기를 중단합니다.
public sealed class PropertiesFile
{
    private string[] lines = [];
    private string original = "";
    private string path = "";
    private readonly Dictionary<string, int> indices = new(StringComparer.Ordinal);
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public static string ReadValue(string path, string key, string fallback)
    {
        if (!File.Exists(path)) return fallback;
        var p = new PropertiesFile(); p.Load(path);
        return p.Values.GetValueOrDefault(key, fallback);
    }
    public void Load(string file)
    {
        path = file; original = File.Exists(file) ? File.ReadAllText(file) : "";
        lines = original.Replace("\r\n", "\n").Split('\n');
        Values.Clear(); indices.Clear();
        bool continuation = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string s = lines[i];
            bool continues = (s.Length - s.TrimEnd('\\').Length) % 2 != 0;
            bool skip = continuation || continues;
            continuation = continues;
            if (skip || s.TrimStart().StartsWith('#') || s.TrimStart().StartsWith('!')) continue;
            var m = Regex.Match(s, @"^\s*([A-Za-z0-9_.-]+)\s*[=:]\s*(.*)$");
            if (!m.Success) continue;
            Values[m.Groups[1].Value] = m.Groups[2].Value;
            indices[m.Groups[1].Value] = i;
        }
    }
    public void Save(Dictionary<string, string> edited)
    {
        if ((File.Exists(path) ? File.ReadAllText(path) : "") != original) throw new IOException("파일이 외부에서 변경되었습니다. 다시 읽은 후 편집하세요.");
        foreach (var (key, value) in edited)
        {
            if (value.Contains('\r') || value.Contains('\n') || (value.Length - value.TrimEnd('\\').Length) % 2 != 0)
                throw new ArgumentException($"{key}: 줄바꿈/이어쓰기 값은 지원하지 않습니다.");
            if (key == "server-port" && (!int.TryParse(value, out int port) || port is < 1 or > 65535)) throw new ArgumentException("포트는 1~65535입니다.");
        }
        var result = lines.ToArray();
        foreach (var (key, value) in edited)
            if (indices.TryGetValue(key, out int i) && Values[key] != value) result[i] = key + "=" + value;
        if (File.Exists(path)) File.Copy(path, path + ".manager.bak", true);
        Storage.AtomicWrite(path, string.Join(Environment.NewLine, result));
        Load(path);
    }
}


