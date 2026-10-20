// [소스 안내] 반복되는 차원/TPS 안내를 분류합니다. 경고·오류·채팅까지 숨기지 않도록 구분하는 필터입니다.
using System.Text.RegularExpressions;
namespace NeoForgeServerManager;

public static class ConsoleLogFilter
{
    public static bool IsDimensionRoutine(string line)
    {
        line=Regex.Replace(line,@"\x1B\[[0-?]*[ -/]*[@-~]|§.","");
        if(Regex.IsMatch(line,@"\b(WARN|ERROR|FATAL|Exception)\b|Caused by:|<[^>]+>|\[(Server|Not Secure)\]",RegexOptions.IgnoreCase))return false;
        string body=Regex.Replace(line,@"^(?:\[[^\]\r\n]*\]\s*)+:?\s*","").Trim();
        return Regex.IsMatch(body,@"^[^<>\r\n]{1,160}:\s*\d+(?:[.,]\d+)?\s+TPS\s*\(\d+(?:[.,]\d+)?\s+ms/tick\)\s*$",RegexOptions.IgnoreCase)
            ||Regex.IsMatch(body,@"^(?:Preparing start region for dimension|(?:Loading|Loaded|Unloading|Unloaded) (?:dimension|level))\s+[a-z0-9_.:/-]+\s*\.?$",RegexOptions.IgnoreCase);
    }
}
