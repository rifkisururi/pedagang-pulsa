using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Supplier name is required")]
    [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "API URL is required")]
    [Url(ErrorMessage = "Invalid URL format")]
    [StringLength(500, ErrorMessage = "URL cannot be longer than 500 characters")]
    public string ApiUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "API Key is required")]
    [StringLength(200, ErrorMessage = "API Key cannot be longer than 200 characters")]
    [DataType(DataType.Password)]
    public string ApiKey { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "API Secret cannot be longer than 200 characters")]
    [DataType(DataType.Password)]
    public string? ApiSecret { get; set; }

    [Required(ErrorMessage = "Timeout is required")]
    [Range(10, 120, ErrorMessage = "Timeout must be between 10 and 120 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [Required(ErrorMessage = "Initial balance is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Balance must be greater than or equal to 0")]
    public decimal InitialBalance { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Threshold must be greater than or equal to 0")]
    public decimal? BalanceThresholdLow { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Threshold must be greater than or equal to 0")]
    public decimal? BalanceThresholdCritical { get; set; }

    public bool IsActive { get; set; } = true;
}
