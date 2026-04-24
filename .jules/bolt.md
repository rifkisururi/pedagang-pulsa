## 2024-04-17 - [Optimizing GetDailyProfitSummaryAsync]
**Learning:** Report generation methods querying transactions inside a loop per day is a severe N+1 problem. Batched queries with projection greatly reduce memory tracking overhead when `AsNoTracking()` is combined with selecting only the required fields.
**Action:** Always batch queries with `Select()` projection combined with `AsNoTracking()` when aggregating daily/monthly values over a range of records to prevent entity overhead.
## 2024-04-18 - [Export Service Memory Optimizations]
**Learning:** Large dataset queries (like balance ledgers up to 50k records) explicitly written to generate Excel files inside `ExportService.cs` were causing immense tracking overhead. Because EF Core's ChangeTracker was on by default, exporting reports would eat significant heap memory.
**Action:** Always append `.AsNoTracking()` to `.AsQueryable()` or `ToList()` chains when exporting large amounts of data to files since these entities are never edited or saved.

## 2024-04-24 - Entity Tracking Safety
**Learning:** Adding `.AsNoTracking()` to generic read methods (like `GetProductByIdAsync`) is a valid optimization but can be dangerous if the codebase reuses these methods in update scenarios before calling `SaveChanges()`. In this architecture, update methods (like `UpdateProductAsync`) correctly run their own tracked queries, making this optimization safe for the generic read methods.
**Action:** Always audit call sites of generic read methods for `SaveChanges()` tracking requirements before applying `.AsNoTracking()`.
