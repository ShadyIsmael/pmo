using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.Mapping;

public static class ResourceMapping
{
    public static ResourceDto ToDto(this Resource resource)
    {
        return new ResourceDto
        {
            Id = resource.Id,
            UserId = resource.UserId,
            Name = resource.Name,
            EmployeeId = resource.EmployeeId,
            JobTitle = resource.JobTitle,
            Email = resource.Email,
            Phone = resource.Phone,
            AvatarUrl = resource.AvatarUrl,
            Type = resource.Type,
            Status = resource.Status,
            CostPerHour = resource.CostPerHour,
            BillableRate = resource.BillableRate,
            Currency = resource.Currency,
            EmploymentType = resource.EmploymentType,
            Seniority = resource.Seniority,
            DateOfJoining = resource.DateOfJoining,
            BusinessUnitId = resource.BusinessUnitId,
            DepartmentId = resource.DepartmentId,
            TeamId = resource.TeamId,
            LocationId = resource.LocationId,
            VendorId = resource.VendorId,
            VendorName = resource.Vendor?.Name,
            Timezone = resource.Timezone,
            UtilizationTarget = resource.UtilizationTarget,
            CurrentUtilization = resource.CurrentUtilization,
            Notes = resource.Notes,
            IsActive = resource.IsActive,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            Skills = resource.Skills?.Select(s => s.ToDto()).ToList() ?? new(),
            Allocations = resource.Allocations?.Select(a => a.ToDto()).ToList() ?? new()
        };
    }

    public static ResourceSkillDto ToDto(this ResourceSkill skill)
    {
        return new ResourceSkillDto
        {
            Id = skill.Id,
            SkillId = skill.SkillId,
            SkillName = skill.Skill?.Name ?? string.Empty,
            SkillCategory = skill.Skill?.Category,
            Proficiency = skill.Proficiency,
            YearsOfExperience = skill.YearsOfExperience
        };
    }

    public static ResourceAllocationDto ToDto(this ResourceAllocation allocation)
    {
        return new ResourceAllocationDto
        {
            Id = allocation.Id,
            ProjectId = allocation.ProjectId,
            ProjectName = allocation.ProjectName,
            Role = allocation.Role,
            AllocationPercentage = allocation.AllocationPercentage,
            PlannedHours = allocation.PlannedHours,
            ActualHours = allocation.ActualHours,
            StartDate = allocation.StartDate,
            EndDate = allocation.EndDate,
            Status = allocation.Status
        };
    }

    public static Resource ToEntity(this CreateResourceDto dto)
    {
        return new Resource
        {
            UserId = dto.UserId ?? Guid.NewGuid(),
            Name = dto.Name,
            EmployeeId = dto.EmployeeId,
            JobTitle = dto.JobTitle,
            Email = dto.Email,
            Phone = dto.Phone,
            AvatarUrl = dto.AvatarUrl,
            Type = dto.Type,
            Status = dto.Status,
            CostPerHour = dto.CostPerHour,
            BillableRate = dto.BillableRate,
            Currency = dto.Currency,
            EmploymentType = dto.EmploymentType,
            Seniority = dto.Seniority,
            DateOfJoining = dto.DateOfJoining,
            BusinessUnitId = dto.BusinessUnitId,
            DepartmentId = dto.DepartmentId,
            TeamId = dto.TeamId,
            LocationId = dto.LocationId,
            VendorId = dto.VendorId,
            Timezone = dto.Timezone,
            UtilizationTarget = dto.UtilizationTarget,
            Notes = dto.Notes,
            IsActive = true
        };
    }

    public static void ApplyUpdate(this Resource resource, UpdateResourceDto dto)
    {
        if (dto.Name != null) resource.Name = dto.Name;
        if (dto.EmployeeId != null) resource.EmployeeId = dto.EmployeeId;
        if (dto.JobTitle != null) resource.JobTitle = dto.JobTitle;
        if (dto.Email != null) resource.Email = dto.Email;
        if (dto.Phone != null) resource.Phone = dto.Phone;
        if (dto.AvatarUrl != null) resource.AvatarUrl = dto.AvatarUrl;
        if (dto.Type != null) resource.Type = dto.Type;
        if (dto.Status != null) resource.Status = dto.Status;
        if (dto.CostPerHour.HasValue) resource.CostPerHour = dto.CostPerHour;
        if (dto.BillableRate.HasValue) resource.BillableRate = dto.BillableRate;
        if (dto.Currency != null) resource.Currency = dto.Currency;
        if (dto.EmploymentType != null) resource.EmploymentType = dto.EmploymentType;
        if (dto.Seniority != null) resource.Seniority = dto.Seniority;
        if (dto.DateOfJoining.HasValue) resource.DateOfJoining = dto.DateOfJoining;
        if (dto.BusinessUnitId.HasValue) resource.BusinessUnitId = dto.BusinessUnitId;
        if (dto.DepartmentId.HasValue) resource.DepartmentId = dto.DepartmentId;
        if (dto.TeamId.HasValue) resource.TeamId = dto.TeamId;
        if (dto.LocationId.HasValue) resource.LocationId = dto.LocationId;
        if (dto.VendorId.HasValue) resource.VendorId = dto.VendorId;
        if (dto.Timezone != null) resource.Timezone = dto.Timezone;
        if (dto.UtilizationTarget.HasValue) resource.UtilizationTarget = dto.UtilizationTarget;
        if (dto.Notes != null) resource.Notes = dto.Notes;
        if (dto.IsActive.HasValue) resource.IsActive = dto.IsActive.Value;
    }
}
