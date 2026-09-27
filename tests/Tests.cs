var failed = 0;
void Test(string name, Action check) { try { check(); Console.WriteLine($"PASS {name}"); } catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e.Message}"); } }
string Temp() { var p = Path.Combine(Path.GetTempPath(), "library-test-" + Guid.NewGuid()); Directory.CreateDirectory(p); return p; }
void Equal(string expected, string actual) { if (expected != actual) throw new Exception($"expected [{expected}], got [{actual}]"); }
Test("holds block others, return permits first holder, overdue is flagged", () => {
    var d=Temp(); var books=Path.Combine(d,"books.csv"); var events=Path.Combine(d,"events.csv"); var output=Path.Combine(d,"out.csv");
    File.WriteAllText(books,"book,copies\nBookA,1\n");
    File.WriteAllText(events,"date,event,book,patron\n2025-01-01,checkout,BookA,Ana\n2025-01-02,hold,BookA,Bob\n2025-01-03,checkout,BookA,Cam\n2025-01-17,return,BookA,Ana\n2025-01-18,checkout,BookA,Bob\n");
    Equal("0",LibraryCirculation.Program.Main([books,events,output]).ToString());
    Equal("date,event,book,patron,status,due_date\n2025-01-01,checkout,BookA,Ana,checked_out,2025-01-15\n2025-01-02,hold,BookA,Bob,held,\n2025-01-03,checkout,BookA,Cam,blocked_no_copy,\n2025-01-17,return,BookA,Ana,returned_overdue,\n2025-01-18,checkout,BookA,Bob,checked_out,2025-02-01\n",File.ReadAllText(output).Replace("\r\n","\n"));
});
Test("enforces two active loans per patron", () => {
    var d=Temp(); var books=Path.Combine(d,"books.csv"); var events=Path.Combine(d,"events.csv"); var output=Path.Combine(d,"out.csv");
    File.WriteAllText(books,"book,copies\nA,1\nB,1\nC,1\n");
    File.WriteAllText(events,"date,event,book,patron\n2025-01-01,checkout,A,Pat\n2025-01-01,checkout,B,Pat\n2025-01-01,checkout,C,Pat\n");
    Equal("0",LibraryCirculation.Program.Main([books,events,output]).ToString());
    if (!File.ReadAllText(output).Contains("checkout,C,Pat,blocked_limit,")) throw new Exception("third loan not blocked");
});
Test("rejects out-of-order event dates", () => {
    var d=Temp(); var books=Path.Combine(d,"books.csv"); var events=Path.Combine(d,"events.csv"); var output=Path.Combine(d,"out.csv");
    File.WriteAllText(books,"book,copies\nA,1\n");
    File.WriteAllText(events,"date,event,book,patron\n2025-01-02,checkout,A,Pat\n2025-01-01,return,A,Pat\n");
    if (LibraryCirculation.Program.Main([books,events,output])==0 || File.Exists(output)) throw new Exception("out-of-order accepted");
});
Test("one hold reserves one of two available copies", () => {
    var d=Temp(); var books=Path.Combine(d,"books.csv"); var events=Path.Combine(d,"events.csv"); var output=Path.Combine(d,"out.csv");
    File.WriteAllText(books,"book,copies\nBookA,2\n");
    File.WriteAllText(events,"date,event,book,patron\n2025-01-01,hold,BookA,Ana\n2025-01-01,checkout,BookA,Bob\n2025-01-01,checkout,BookA,Cam\n2025-01-01,checkout,BookA,Ana\n");
    Equal("0",LibraryCirculation.Program.Main([books,events,output]).ToString());
    var text=File.ReadAllText(output);
    if (!text.Contains("checkout,BookA,Bob,checked_out,2025-01-15") || !text.Contains("checkout,BookA,Cam,blocked_reserved,") || !text.Contains("checkout,BookA,Ana,checked_out,2025-01-15")) throw new Exception("available copies not partitioned by reservations");
});
Test("second holder can collect second reserved copy", () => {
    var d=Temp(); var books=Path.Combine(d,"books.csv"); var events=Path.Combine(d,"events.csv"); var output=Path.Combine(d,"out.csv");
    File.WriteAllText(books,"book,copies\nBookA,2\n");
    File.WriteAllText(events,"date,event,book,patron\n2025-01-01,hold,BookA,Ana\n2025-01-01,hold,BookA,Bob\n2025-01-01,checkout,BookA,Bob\n2025-01-01,checkout,BookA,Cam\n2025-01-01,checkout,BookA,Ana\n");
    Equal("0",LibraryCirculation.Program.Main([books,events,output]).ToString());
    Equal("date,event,book,patron,status,due_date\n2025-01-01,hold,BookA,Ana,held,\n2025-01-01,hold,BookA,Bob,held,\n2025-01-01,checkout,BookA,Bob,checked_out,2025-01-15\n2025-01-01,checkout,BookA,Cam,blocked_reserved,\n2025-01-01,checkout,BookA,Ana,checked_out,2025-01-15\n",File.ReadAllText(output).Replace("\r\n","\n"));
});
Console.WriteLine($"{5 - failed}/5 passed"); if (failed > 0) Environment.Exit(1);
