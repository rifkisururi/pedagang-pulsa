namespace PedagangPulsa.Api.DTOs;

public class EwalletAccountInquiryResponse
{
    public bool Success { get; set; } = true;
    public AccountInquiryData? Data { get; set; }
}

public class AccountInquiryData
{
    public string Type { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
