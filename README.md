# Library circulation

Replay checkout, return, and hold events against a small book inventory. The program reports each accepted action or the reason it was blocked, including overdue returns and holds that reserve the next available copy.

## Run

Requires .NET 10. From this directory:

```sh
dotnet run --project LibraryCirculation.csproj -- fixtures/books.csv fixtures/events.csv circulation.csv
dotnet run --project tests/Tests.csproj
```

`circulation.csv` must be a new path. Compare with `examples/circulation.csv`. All records are synthetic.

Books have `book,copies` (positive integer copies). Events have `date,event,book,patron`, with dates in nondecreasing `YYYY-MM-DD` order. Same-day events retain file order. Book and patron IDs contain ASCII letters, digits, `_`, or `-`. Unknown books, malformed dates or events, duplicate books, and out-of-order dates exit 2 without output.

A patron may have at most two active loans. Checkout lasts 14 days; returning after the due date is marked `returned_overdue`. Holds form a FIFO queue per book. Available copies are reserved for the earliest queued holds; any of those holders may collect their copy. Other patrons may borrow copies beyond the number of queued holds. A checkout consumes that patron's hold. Repeated or impossible actions appear as `blocked_*` or `already_held` statuses. The model has no renewals, hold expiry, fines, or patron registry.
