namespace UeLogViewer.Tests;

public class LogParserTests
{
	[Theory]
	[InlineData("[2026.01.15-10.40.49:563][  0]LogInit: Display: Engine is initialized.", "LogInit", LogLevel.Display)]
	[InlineData("[2026.01.15-10.40.49:563][ 12]LogTemp: Warning: Spawn point is blocked", "LogTemp", LogLevel.Warning)]
	[InlineData("[2026.01.15-10.40.49:563][ 12]LogScript: Error: Accessed None", "LogScript", LogLevel.Error)]
	[InlineData("[2026.01.15-10.40.49:563][ 12]LogNet: Browse: /Game/Maps/Arena", "LogNet", LogLevel.Log)]
	[InlineData("[0012.34][  5]LogStreaming: Verbose: Flushing", "LogStreaming", LogLevel.Verbose)]
	[InlineData("LogInit: Display: Running engine", "LogInit", LogLevel.Display)]
	[InlineData("[2026.01.15-10.40.49:563][  0][5a94]LogInit:Display: Engine is initialized.", "LogInit", LogLevel.Display)]
	[InlineData("[2026.01.15-10.40.49:563][  0][5a94]LogTemp:Warning: Compact form", "LogTemp", LogLevel.Warning)]
	public void ParsesCategoryAndVerbosity(string line, string category, LogLevel level)
	{
		LogEntry entry = new LogParser().Parse(line);

		Assert.Equal(category, entry.Category);
		Assert.Equal(level, entry.Level);
		Assert.False(entry.IsContinuation);
	}

	[Fact]
	public void ShortLineDropsTimestampPrefix()
	{
		LogEntry entry = new LogParser().Parse("[2026.01.15-10.40.49:563][  0]LogTemp: Warning: Hello");

		Assert.Equal("LogTemp: Warning: Hello", entry.Short);
		Assert.Equal("[2026.01.15-10.40.49:563][  0]LogTemp: Warning: Hello", entry.Raw);
	}

	[Fact]
	public void ContinuationLinesInheritCategoryAndLevel()
	{
		var parser = new LogParser();
		List<LogEntry> entries = parser.ParseBlock(
			"[2026.01.15-10.40.49:563][  0]LogScript: Error: Script Msg: Accessed None\r\n" +
			"\tBP_Hero_C /Game/Maps/Arena.Arena:PersistentLevel.BP_Hero_C_0\r\n" +
			"[2026.01.15-10.40.49:600][  1]LogTemp: Next line\n");

		Assert.Equal(3, entries.Count);
		Assert.True(entries[1].IsContinuation);
		Assert.Equal("LogScript", entries[1].Category);
		Assert.Equal(LogLevel.Error, entries[1].Level);
		Assert.Equal("LogTemp", entries[2].Category);
		Assert.Equal(LogLevel.Log, entries[2].Level);
		Assert.Equal(new[] { 1, 2, 3 }, entries.Select(e => e.LineNo));
	}

	[Fact]
	public void TimestampedLineWithoutCategoryStartsNoCategory()
	{
		var parser = new LogParser();
		parser.Parse("[2026.01.15-10.40.49:563][  0]LogTemp: Error: Boom");
		LogEntry entry = parser.Parse("[2026.01.15-10.40.49:600][  1]Plain message");

		Assert.Equal(LogParser.NoCategory, entry.Category);
		Assert.Equal(LogLevel.Log, entry.Level);
		Assert.False(entry.IsContinuation);
	}

	[Fact]
	public void TextAfterCategoryThatIsNotAVerbosityStaysLog()
	{
		LogEntry entry = new LogParser().Parse("[2026.01.15-10.40.49:563][  0]LogNet: Warningless message");

		Assert.Equal("LogNet", entry.Category);
		Assert.Equal(LogLevel.Log, entry.Level);
	}
}
