## 2024-04-17 - [Optimizing GetDailyProfitSummaryAsync]
**Learning:** Report generation methods querying transactions inside a loop per day is a severe N+1 problem. Batched queries with projection greatly reduce memory tracking overhead when `AsNoTracking()` is combined with selecting only the required fields.
**Action:** Always batch queries with `Select()` projection combined with `AsNoTracking()` when aggregating daily/monthly values over a range of records to prevent entity overhead.
## 2024-04-18 - [Export Service Memory Optimizations]
**Learning:** Large dataset queries (like balance ledgers up to 50k records) explicitly written to generate Excel files inside `ExportService.cs` were causing immense tracking overhead. Because EF Core's ChangeTracker was on by default, exporting reports would eat significant heap memory.
**Action:** Always append `.AsNoTracking()` to `.AsQueryable()` or `ToList()` chains when exporting large amounts of data to files since these entities are never edited or saved.
## 2024-05-18 - [Eliminate Entity Graph Loading Overhead with Projection]
**Learning:** Found significant N+1 and full-entity overhead when generating reports (`ReportService.cs`) using `.Include()` and `.ThenInclude()`. In EF Core, even with `.AsNoTracking()`, pulling huge graphs and grouping in-memory causes major GC pressure and slows execution significantly.
**Action:** Replace `Include().ToListAsync()` followed by in-memory grouping with `.Select().ToListAsync()` projections. This ensures only necessary primitive fields are materialized from SQL, massively reducing memory allocation per report request.
