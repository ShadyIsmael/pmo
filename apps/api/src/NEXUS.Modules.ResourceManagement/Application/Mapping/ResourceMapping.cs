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
            UserId = resource.UserId == Guid.Empty ? null : resource.UserId,
            Name = resource.Name,
            EmployeeId = resource.EmployeeId,
            JobTitle = resource.JobTitle,
            Email = resource.Email,
            Phone = resource.Phone,
            AvatarUrl = resource.AvatarUrl,
            Type = ResourceTypeConstants.ToApiCode(resource.Type),
            Status = resource.Status,
            EmploymentType = resource.EmploymentType,
            Seniority = resource.Seniority,
            DateOfJoining = resource.DateOfJoining,
            BusinessUnitId = resource.BusinessUnitId,
            DepartmentId = resource.DepartmentId,
            LocationId = resource.LocationId,
            VendorId = resource.VendorId,
            VendorName = resource.Vendor?.Name,
            BaseCalendarId = resource.BaseCalendarId,
            BaseCalendarName = resource.BaseCalendar?.Name,
            HoursPerDay = resource.HoursPerDay,
            HoursPerWeek = resource.HoursPerWeek,
            Timezone = resource.Timezone,
            UtilizationTarget = resource.UtilizationTarget,
            CurrentUtilization = resource.CurrentUtilization,
            Notes = resource.Notes,
            IsActive = resource.IsActive,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            Skills = resource.Skills?.Select(s => s.ToDto()).ToList() ?? new(),
            Allocations = resource.Allocations?.Select(a => a.ToDto()).ToList() ?? new(),
            // Cost rates are confidential and are returned only by the separately
            // authorized ResourceCostProfile endpoint.
            CostProfiles = new()
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
            AllocationPercentage = allocation.AllocationPercentage,
            PlannedHours = allocation.PlannedHours,
            StartDate = allocation.StartDate,
            EndDate = allocation.EndDate,
            Status = allocation.Status
        };
    }

    public static Resource ToEntity(this CreateResourceDto dto)
    {
        return new Resource
        {
            UserId = dto.UserId,
            Name = dto.Name,
            EmployeeId = dto.EmployeeId,
            JobTitle = dto.JobTitle,
            Email = dto.Email,
            Phone = dto.Phone,
            AvatarUrl = dto.AvatarUrl,
            Type = ResourceTypeConstants.ToStoredCode(dto.Type),
            Status = dto.Status,
            EmploymentType = dto.EmploymentType,
            Seniority = dto.Seniority,
            DateOfJoining = dto.DateOfJoining,
            BusinessUnitId = dto.BusinessUnitId,
            DepartmentId = dto.DepartmentId,
            LocationId = dto.LocationId,
            VendorId = dto.VendorId,
            BaseCalendarId = dto.BaseCalendarId,
            HoursPerDay = dto.HoursPerDay,
            HoursPerWeek = dto.HoursPerWeek,
            Timezone = dto.Timezone,
            UtilizationTarget = dto.UtilizationTarget,
            Notes = dto.Notes,
            IsActive = true
        };
    }

public static void ApplyUpdate(this Resource resource, UpdateResourceDto dto)
    {
        if (dto.RemoveUserAssociation == true) resource.UserId = null;
        else if (dto.UserId.HasValue) resource.UserId = dto.UserId.Value;
        if (dto.Name != null) resource.Name = dto.Name;
        if (dto.EmployeeId != null) resource.EmployeeId = dto.EmployeeId;
        if (dto.JobTitle != null) resource.JobTitle = dto.JobTitle;
        if (dto.Email != null) resource.Email = dto.Email;
        if (dto.Phone != null) resource.Phone = dto.Phone;
        if (dto.AvatarUrl != null) resource.AvatarUrl = dto.AvatarUrl;
        if (dto.Type.HasValue) resource.Type = ResourceTypeConstants.ToStoredCode(dto.Type.Value);
        if (dto.Status != null) resource.Status = dto.Status;
        if (dto.EmploymentType != null) resource.EmploymentType = dto.EmploymentType;
        if (dto.Seniority != null) resource.Seniority = dto.Seniority;
        if (dto.DateOfJoining.HasValue) resource.DateOfJoining = dto.DateOfJoining;
        if (dto.BusinessUnitId.HasValue) resource.BusinessUnitId = dto.BusinessUnitId;
        if (dto.DepartmentId.HasValue) resource.DepartmentId = dto.DepartmentId;
        if (dto.LocationId.HasValue) resource.LocationId = dto.LocationId;
        if (dto.VendorId.HasValue) resource.VendorId = dto.VendorId;
        if (dto.BaseCalendarId.HasValue) resource.BaseCalendarId = dto.BaseCalendarId;
        if (dto.ClearHoursPerDay) resource.HoursPerDay = null;
        else if (dto.HoursPerDay.HasValue) resource.HoursPerDay = dto.HoursPerDay.Value;
        if (dto.ClearHoursPerWeek) resource.HoursPerWeek = null;
        else if (dto.HoursPerWeek.HasValue) resource.HoursPerWeek = dto.HoursPerWeek.Value;
        if (dto.Timezone != null) resource.Timezone = dto.Timezone;
        if (dto.UtilizationTarget.HasValue) resource.UtilizationTarget = dto.UtilizationTarget;
        if (dto.Notes != null) resource.Notes = dto.Notes;
        if (dto.IsActive.HasValue) resource.IsActive = dto.IsActive.Value;
    }
}
