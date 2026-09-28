using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.Mapping;

public static class VendorMapping
{
    public static VendorDto ToDto(this Vendor vendor)
    {
        return new VendorDto
        {
            Id = vendor.Id,
            Name = vendor.Name,
            Code = vendor.Code,
            ContactName = vendor.ContactName,
            ContactEmail = vendor.ContactEmail,
            ContactPhone = vendor.ContactPhone,
            Category = vendor.Category,
            VendorCategoryId = vendor.VendorCategoryId,
            Status = vendor.Status,
            ContractValue = vendor.ContractValue,
            ContractStart = vendor.ContractStart,
            ContractEnd = vendor.ContractEnd,
            IsActive = vendor.IsActive
        };
    }

    public static Vendor ToEntity(this CreateVendorDto dto)
    {
        return new Vendor
        {
            Name = dto.Name,
            ContactName = dto.ContactName,
            ContactEmail = dto.ContactEmail,
            ContactPhone = dto.ContactPhone,
            VendorCategoryId = dto.VendorCategoryId,
            Status = dto.Status ?? "Active",
            ContractValue = dto.ContractValue,
            ContractStart = dto.ContractStart,
            ContractEnd = dto.ContractEnd,
            IsActive = true
        };
    }

    public static void ApplyUpdate(this Vendor vendor, UpdateVendorDto dto)
    {
        if (dto.Name != null) vendor.Name = dto.Name;
        if (dto.ContactName != null) vendor.ContactName = dto.ContactName;
        if (dto.ContactEmail != null) vendor.ContactEmail = dto.ContactEmail;
        if (dto.ContactPhone != null) vendor.ContactPhone = dto.ContactPhone;
        if (dto.VendorCategoryId.HasValue && dto.VendorCategoryId.Value != Guid.Empty && dto.VendorCategoryId.Value != vendor.VendorCategoryId)
            vendor.VendorCategoryId = dto.VendorCategoryId.Value;
        if (dto.Status != null) vendor.Status = dto.Status;
        if (dto.ContractValue.HasValue) vendor.ContractValue = dto.ContractValue;
        if (dto.ContractStart.HasValue) vendor.ContractStart = dto.ContractStart;
        if (dto.ContractEnd.HasValue) vendor.ContractEnd = dto.ContractEnd;
        if (dto.IsActive.HasValue) vendor.IsActive = dto.IsActive.Value;
    }
}
