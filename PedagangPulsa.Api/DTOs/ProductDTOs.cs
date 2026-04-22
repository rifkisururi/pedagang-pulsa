namespace PedagangPulsa.Api.DTOs;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? ProductGroupId { get; set; }
    public string? ProductGroupName { get; set; }
    public string? Operator { get; set; }
    public decimal? Denomination { get; set; }
    public int? ValidityDays { get; set; }
    public string? ValidityText { get; set; }
    public int? QuotaMb { get; set; }
    public string? QuotaText { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public bool Available { get; set; }
}

public class ProductListResponse : PagedResponse<ProductListDto>
{
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public List<string> SubCategories { get; set; } = new();
}

public class CategoryListResponse
{
    public bool Success { get; set; }
    public List<CategoryDto> Data { get; set; } = new();
}

// --- Catalog DTOs ---

public class CatalogCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Icon { get; set; }
}

public class CatalogCategoryListResponse
{
    public bool Success { get; set; }
    public List<CatalogCategoryDto> Data { get; set; } = new();
}

public class CatalogProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal? Denomination { get; set; }
    public int? ValidityDays { get; set; }
    public string? ValidityText { get; set; }
    public int? QuotaMb { get; set; }
    public string? QuotaText { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public bool Available { get; set; }
}

public class CatalogGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public List<CatalogProductDto> Products { get; set; } = new();
}

public class CatalogByCategoryResponse
{
    public bool Success { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<CatalogGroupDto> Data { get; set; } = new();
}
