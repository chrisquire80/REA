using System.Diagnostics;
using System.Text;
using NotepadREA;

int[] sizesMb = args.Length > 0
    ? args[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
        .ToArray()
    : [1, 16, 64];
int iterations = args.Length > 1
    ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)
    : 3;

if (sizesMb.Length == 0 || sizesMb.Any(size => size <= 0) || iterations < 1)
    throw new ArgumentOutOfRangeException(nameof(args), "Specificare dimensioni positive e almeno un'iterazione.");

string root = Path.Combine(Path.GetTempPath(), $"norea-benchmark-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
Console.WriteLine("size_mb,operation,median_ms,allocated_mb_per_op");

try
{
    foreach (int sizeMb in sizesMb)
    {
        int byteLength = checked(sizeMb * 1024 * 1024);
        string inputPath = Path.Combine(root, "input.txt");
        string outputPath = Path.Combine(root, "output.txt");
        byte[] content = CreateTextPayload(byteLength);
        File.WriteAllBytes(inputPath, content);
        string text = TextCodec.Decode(content).Text;

        Report(sizeMb, "snapshot_from_bytes", () => DiskFileSnapshot.FromBytes(inputPath, content), iterations);
        Report(sizeMb, "snapshot_from_file", () => DiskFileSnapshot.Capture(inputPath), iterations);
        Report(sizeMb, "read_and_decode", () => TextCodec.Decode(File.ReadAllBytes(inputPath)), iterations);
        Report(sizeMb, "encode_and_write", () => File.WriteAllBytes(outputPath, TextCodec.Encode(text, FileEncoding.Utf8)), iterations);

        File.Delete(inputPath);
        File.Delete(outputPath);
    }
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static byte[] CreateTextPayload(int length)
{
    const string line = "NOREA benchmark sample text with UTF-8 content.\r\n";
    byte[] pattern = Encoding.UTF8.GetBytes(line);
    var bytes = new byte[length];
    for (int offset = 0; offset < bytes.Length; offset += pattern.Length)
        Buffer.BlockCopy(pattern, 0, bytes, offset, Math.Min(pattern.Length, bytes.Length - offset));
    return bytes;
}

static void Report(int sizeMb, string operationName, Action operation, int iterations)
{
    operation();
    var elapsed = new double[iterations];
    var allocated = new double[iterations];

    for (int i = 0; i < iterations; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        operation();
        timer.Stop();
        elapsed[i] = timer.Elapsed.TotalMilliseconds;
        allocated[i] = (GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore) / (1024d * 1024d);
    }

    Array.Sort(elapsed);
    Array.Sort(allocated);
    Console.WriteLine(string.Join(',',
        sizeMb.ToString(System.Globalization.CultureInfo.InvariantCulture),
        operationName,
        elapsed[elapsed.Length / 2].ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
        allocated[allocated.Length / 2].ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
}
