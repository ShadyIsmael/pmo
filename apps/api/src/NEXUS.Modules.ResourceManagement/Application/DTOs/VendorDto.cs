namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class VendorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Category { get; set; }
    public Guid? VendorCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Status { get; set; }
    public decimal? ContractValue { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public bool IsActive { get; set; }
}

public class CreateVendorDto
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? VendorCategoryId { get; set; }
    public string? Status { get; set; }
    public decimal? ContractValue { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
}

public class UpdateVendorDto
{
    public string? Name { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? VendorCategoryId { get; set; }
    public string? Status { get; set; }
    public decimal? ContractValue { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public bool? IsActive { get; set; }
}
