using System.Text.RegularExpressions;

namespace UeLogViewer;

/// Parses Unreal Engine (UE4/UE5) log lines, as written by FOutputDeviceHelper::FormatLogLine:
///   [2026.01.15-10.40.49:563][  0]LogInit: Display: message
///   [2026.01.15-10.40.49:563][  0]LogNet: message            (Log verbosity is not written)
/// Also accepted: a hex thread id block after the frame number, and no space between
/// the category and the verbosity ("LogInit:Display: message"), which some engine builds use.
/// The timestamp block is missing for lines written before the log timer starts.
/// Stateful: remembers the last category so continuation lines can inherit it.
public sealed class LogParser
{
	public const string NoCategory = "(none)";

	static readonly Regex Prefix = new(
		@"^(?<ts>\[[^\]\r\n]*\]\[\s*\d+\](?:\[[0-9a-fA-F]+\])?)?" +
		@"(?:(?<cat>[A-Za-z_][A-Za-z0-9_]*):(?: ?(?<lvl>Fatal|Error|Warning|Display|Log|Verbose|VeryVerbose):)?(?=\s|$))?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	int lineNo;
	string lastCategory = NoCategory;
	LogLevel lastLevel = LogLevel.Log;

	public void Reset()
	{
		lineNo = 0;
		lastCategory = NoCategory;
		lastLevel = LogLevel.Log;
	}

	/// Splits a block of complete lines (ending with '\n') into entries.
	public List<LogEntry> ParseBlock(string text)
	{
		var entries = new List<LogEntry>();
		int start = 0;
		while (start < text.Length)
		{
			int end = text.IndexOf('\n', start);
			if (end < 0)
			{
				end = text.Length;
			}

			int lineEnd = end;
			if (lineEnd > start && text[lineEnd - 1] == '\r')
			{
				lineEnd--;
			}

			entries.Add(Parse(text.Substring(start, lineEnd - start)));
			start = end + 1;
		}
		return entries;
	}

	public LogEntry Parse(string line)
	{
		lineNo++;
		Match match = Prefix.Match(line);
		Group ts = match.Groups["ts"];
		Group cat = match.Groups["cat"];

		if (cat.Success)
		{
			Group lvl = match.Groups["lvl"];
			lastCategory = cat.Value;
			lastLevel = lvl.Success ? Enum.Parse<LogLevel>(lvl.Value) : LogLevel.Log;
			return Make(line, ts, false);
		}

		if (ts.Success)
		{
			lastCategory = NoCategory;
			lastLevel = LogLevel.Log;
			return Make(line, ts, false);
		}

		return Make(line, ts, true);
	}

	LogEntry Make(string line, Group ts, bool isContinuation)
	{
		return new LogEntry
		{
			LineNo = lineNo,
			Raw = line,
			Short = ts.Success ? line.Substring(ts.Length) : line,
			Category = lastCategory,
			Level = lastLevel,
			IsContinuation = isContinuation,
		};
	}
}
