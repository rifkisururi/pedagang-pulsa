namespace PedagangPulsa.Api.DTOs;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public decimal? Denomination { get; set; }
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
