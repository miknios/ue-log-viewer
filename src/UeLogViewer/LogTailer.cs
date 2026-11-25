using System.IO;
using System.Text;

namespace UeLogViewer;

/// One delivery from the tailer. Reset = the file was replaced or truncated, so drop everything shown so far.
public sealed record TailBatch(bool Reset, List<LogEntry> Entries, bool FileMissing);

/// Polls a log file on a background thread and reports new complete lines.
/// Opens the file per poll with full sharing, so UE can keep writing, renaming and deleting it.
/// Detects a new run (UE renames <Project>.log to a backup and starts a new one) by comparing
/// the first bytes of the file ("Log file open, <date>") and by the file getting shorter.
/// Creation time is not used: NTFS tunnelling keeps it when a file is recreated under the same name.
public sealed class LogTailer : IDisposable
{
	const int PollMs = 200;
	const int ChunkSize = 4 << 20;
	const int HeaderSize = 128;

	readonly string path;
	readonly Action<TailBatch> onBatch;
	readonly CancellationTokenSource cts = new();
	readonly LogParser parser = new();
	readonly UTF8Encoding utf8 = new(false, false);

	long offset;
	byte[]? header;
	byte[] pending = [];
	bool missingReported;

	/// onBatch is called on a background thread.
	public LogTailer(string path, Action<TailBatch> onBatch)
	{
		this.path = path;
		this.onBatch = onBatch;
		Task.Run(Run);
	}

	public void Dispose()
	{
		cts.Cancel();
	}

	void Run()
	{
		while (!cts.IsCancellationRequested)
		{
			try
			{
				Poll();
			}
			catch (IOException)
			{
				// File busy or swapped mid-read; try again next poll.
			}
			catch (UnauthorizedAccessException)
			{
			}
			cts.Token.WaitHandle.WaitOne(PollMs);
		}
	}

	void Poll()
	{
		if (!File.Exists(path))
		{
			if (!missingReported)
			{
				missingReported = true;
				Deliver(new TailBatch(false, [], true));
			}
			return;
		}

		using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		long length = fs.Length;

		bool reset = false;
		if (header != null)
		{
			if (length < offset)
			{
				reset = true;
			}
			else if (!HeaderMatches(fs, length))
			{
				reset = true;
			}
		}

		if (reset)
		{
			offset = 0;
			header = null;
			pending = [];
			parser.Reset();
		}

		if (header == null || header.Length < HeaderSize)
		{
			header = ReadBytes(fs, 0, (int)Math.Min(HeaderSize, length));
		}

		bool needsReport = reset || missingReported;
		missingReported = false;

		if (length == offset)
		{
			if (needsReport)
			{
				Deliver(new TailBatch(reset, [], false));
			}
			return;
		}

		fs.Position = offset;
		while (offset < length)
		{
			int toRead = (int)Math.Min(ChunkSize, length - offset);
			long bufferFileStart = offset - pending.Length;
			byte[] buffer = new byte[pending.Length + toRead];
			Buffer.BlockCopy(pending, 0, buffer, 0, pending.Length);

			int read = ReadFully(fs, buffer, pending.Length, toRead);
			if (read == 0)
			{
				break;
			}
			offset += read;
			int total = pending.Length + read;

			int lastNewline = Array.LastIndexOf(buffer, (byte)'\n', total - 1);
			if (lastNewline < 0)
			{
				pending = buffer[..total];
				continue;
			}

			int start = 0;
			if (bufferFileStart == 0 && total >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
			{
				start = 3;
			}

			string text = utf8.GetString(buffer, start, lastNewline + 1 - start);
			pending = buffer[(lastNewline + 1)..total];

			Deliver(new TailBatch(reset, parser.ParseBlock(text), false));
			reset = false;
			needsReport = false;
		}

		if (needsReport)
		{
			Deliver(new TailBatch(reset, [], false));
		}
	}

	void Deliver(TailBatch batch)
	{
		if (cts.IsCancellationRequested)
		{
			return;
		}
		onBatch(batch);
	}

	bool HeaderMatches(FileStream fs, long length)
	{
		byte[] known = header!;
		if (length < known.Length)
		{
			return false;
		}

		byte[] current = ReadBytes(fs, 0, known.Length);
		return current.AsSpan().SequenceEqual(known);
	}

	static byte[] ReadBytes(FileStream fs, long position, int count)
	{
		byte[] bytes = new byte[count];
		fs.Position = position;
		int read = ReadFully(fs, bytes, 0, count);
		return read == count ? bytes : bytes[..read];
	}

	static int ReadFully(Stream stream, byte[] buffer, int index, int count)
	{
		int total = 0;
		while (total < count)
		{
			int read = stream.Read(buffer, index + total, count - total);
			if (read == 0)
			{
				break;
			}
			total += read;
		}
		return total;
	}
}
