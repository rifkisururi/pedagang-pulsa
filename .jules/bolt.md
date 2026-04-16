## 2025-03-24 - [Add IMemoryCache to ProductService for GetCategoriesAsync]
**Learning:** Adding memory caching to lookup lists like Product Categories is critical for reducing repetitive DB hits since they rarely change. Injecting `IMemoryCache` seamlessly handles state storage.
**Action:** Injected `IMemoryCache` into `ProductService.cs` and utilized it inside `GetCategoriesAsync`.
