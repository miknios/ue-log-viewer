namespace UeLogViewer;

public enum LogLevel
{
	Fatal,
	Error,
	Warning,
	Display,
	Log,
	Verbose,
	VeryVerbose,
}

public sealed class LogEntry
{
	public required int LineNo { get; init; }
	/// Full line as written in the file.
	public required string Raw { get; init; }
	/// Line without the [time][frame] prefix.
	public required string Short { get; init; }
	public required string Category { get; init; }
	public required LogLevel Level { get; init; }
	/// True for lines without a category prefix (callstacks, multi-line messages).
	/// They inherit the category and level of the line before them.
	public required bool IsContinuation { get; init; }
}
