using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace LibraryCirculation;

public static class Program
{
    private sealed record Event(DateOnly Date, string Action, string Book, string Patron);

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("usage: LibraryCirculation BOOKS.csv EVENTS.csv NEW_OUTPUT.csv");
            var available = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in ReadCsv(args[0], "book,copies"))
            {
                var book = Id(row[0], "book");
                if (!int.TryParse(row[1], NumberStyles.None, CultureInfo.InvariantCulture, out var copies) || copies is < 1 or > 1_000_000)
                    throw new InvalidDataException($"invalid copies: {row[1]}");
                if (!available.TryAdd(book, copies)) throw new InvalidDataException($"duplicate book: {book}");
            }
            if (available.Count == 0) throw new InvalidDataException("books are empty");
            var events = new List<Event>(); DateOnly? previous = null;
            foreach (var row in ReadCsv(args[1], "date,event,book,patron"))
            {
                if (!DateOnly.TryParseExact(row[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year < 1900 || date.Year > 2100)
                    throw new InvalidDataException($"invalid date: {row[0]}");
                if (previous is not null && date < previous) throw new InvalidDataException("events must be in nondecreasing date order");
                previous = date;
                if (row[1] is not ("checkout" or "return" or "hold")) throw new InvalidDataException($"unknown event: {row[1]}");
                var book = Id(row[2], "book"); var patron = Id(row[3], "patron");
                if (!available.ContainsKey(book)) throw new InvalidDataException($"unknown book: {book}");
                events.Add(new Event(date, row[1], book, patron));
            }
            if (events.Count == 0) throw new InvalidDataException("events are empty");
            var loans = new Dictionary<(string Book, string Patron), DateOnly>();
            var activeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var holds = available.Keys.ToDictionary(k => k, _ => new Queue<string>(), StringComparer.Ordinal);
            var outputRows = new List<string>();
            foreach (var item in events)
            {
                var key = (item.Book, item.Patron);
                var queue = holds[item.Book];
                var status = ""; var dueText = "";
                switch (item.Action)
                {
                    case "hold":
                        if (loans.ContainsKey(key)) status = "blocked_already_borrowed";
                        else if (queue.Contains(item.Patron)) status = "already_held";
                        else { queue.Enqueue(item.Patron); status = "held"; }
                        break;
                    case "checkout":
                        if (loans.ContainsKey(key)) status = "blocked_duplicate";
                        else if (activeCounts.GetValueOrDefault(item.Patron) >= 2) status = "blocked_limit";
                        else if (available[item.Book] == 0) status = "blocked_no_copy";
                        else if (queue.Count >= available[item.Book] && !queue.Take(available[item.Book]).Contains(item.Patron)) status = "blocked_reserved";
                        else
                        {
                            // A hold protects one copy; copies beyond the hold count remain borrowable.
                            // Consume this patron's hold even when they borrow a surplus copy.
                            var holdCount = queue.Count;
                            for (var i = 0; i < holdCount; i++)
                            {
                                var holder = queue.Dequeue();
                                if (holder != item.Patron) queue.Enqueue(holder);
                            }
                            available[item.Book]--;
                            activeCounts[item.Patron] = activeCounts.GetValueOrDefault(item.Patron) + 1;
                            var due = item.Date.AddDays(14);
                            loans.Add(key, due); dueText = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            status = "checked_out";
                        }
                        break;
                    case "return":
                        if (!loans.Remove(key, out var dueDate)) status = "blocked_no_loan";
                        else
                        {
                            available[item.Book]++;
                            activeCounts[item.Patron]--;
                            status = item.Date > dueDate ? "returned_overdue" : "returned";
                        }
                        break;
                }
                outputRows.Add($"{item.Date:yyyy-MM-dd},{item.Action},{item.Book},{item.Patron},{status},{dueText}");
            }
            if (File.Exists(args[2])) throw new IOException($"output already exists: {args[2]}");
            using var writer = new StreamWriter(new FileStream(args[2], FileMode.CreateNew, FileAccess.Write));
            writer.NewLine = "\n"; writer.WriteLine("date,event,book,patron,status,due_date");
            foreach (var row in outputRows) writer.WriteLine(row);
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine($"LibraryCirculation: {e.Message}"); return 2; }
    }
    private static string Id(string value, string name)
    {
        if (value.Length == 0 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) throw new InvalidDataException($"invalid {name}: {value}");
        return value;
    }
    private static List<string[]> ReadCsv(string path, string header)
    {
        using var parser = new TextFieldParser(path); parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true;
        var rows = new List<string[]>();
        try
        {
            var actual = parser.ReadFields();
            if (actual is null || string.Join(',', actual) != header) throw new InvalidDataException($"expected CSV header: {header}");
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is null || fields.Length != actual.Length) throw new InvalidDataException($"wrong column count near line {parser.LineNumber}");
                rows.Add(fields);
            }
        }
        catch (MalformedLineException e) { throw new InvalidDataException($"malformed CSV near line {e.LineNumber}", e); }
        return rows;
    }
}
