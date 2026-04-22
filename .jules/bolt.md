## 2024-04-17 - [Optimizing GetDailyProfitSummaryAsync]
**Learning:** Report generation methods querying transactions inside a loop per day is a severe N+1 problem. Batched queries with projection greatly reduce memory tracking overhead when `AsNoTracking()` is combined with selecting only the required fields.
**Action:** Always batch queries with `Select()` projection combined with `AsNoTracking()` when aggregating daily/monthly values over a range of records to prevent entity overhead.
## 2024-04-18 - [Export Service Memory Optimizations]
**Learning:** Large dataset queries (like balance ledgers up to 50k records) explicitly written to generate Excel files inside `ExportService.cs` were causing immense tracking overhead. Because EF Core's ChangeTracker was on by default, exporting reports would eat significant heap memory.
**Action:** Always append `.AsNoTracking()` to `.AsQueryable()` or `ToList()` chains when exporting large amounts of data to files since these entities are never edited or saved.
## 2024-05-18 - [Pagination Query Memory Optimizations]
**Learning:** `Get*PagedAsync` methods across all `PedagangPulsa.Application/Services` were building initial queries without `.AsNoTracking()`. Paged queries returning lists of entities (like products or users) suffer from memory overhead because EF Core tracks entities by default, which is unnecessary for read-only grid data.
**Action:** Always insert `.AsNoTracking()` before `.AsQueryable()` when initializing EF Core queries for retrieving paged data lists that will not be immediately mutated.
