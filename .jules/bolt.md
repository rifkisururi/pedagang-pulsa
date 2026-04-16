## 2026-04-16 - [AsNoTracking & Single Query Optimization]
**Learning:** In PedagangPulsa, the ReportService was using loops to query multiple days resulting in an N+1 query pattern. The queries also brought back many unneeded fields since EF was materializing full entities into tracking contexts.
**Action:** When working on reporting or data retrieval services, write projected queries (using Select into DTOs) and include .AsNoTracking() to optimize CPU, memory, and database trips.
