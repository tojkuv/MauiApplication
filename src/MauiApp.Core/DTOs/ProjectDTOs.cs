using System.ComponentModel.DataAnnotations;
using MauiApp.Core.Entities;

namespace MauiApp.Core.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int MemberCount { get; set; }
    public List<ProjectMemberDto> Members { get; set; } = new();
    public List<MilestoneDto> Milestones { get; set; } = new();
}

public class CreateProjectRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public List<Guid> MemberIds { get; set; } = new();
    public List<CreateMilestoneRequest> Milestones { get; set; } = new();
}

public class UpdateProjectRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    public ProjectStatus Status { get; set; }
}

public class ProjectMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddProjectMemberRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public ProjectRole Role { get; set; }
}

public class UpdateProjectMemberRequest
{
    [Required]
    public ProjectRole Role { get; set; }

    [Required]
    public bool IsActive { get; set; }
}

public class MilestoneDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMilestoneRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime DueDate { get; set; }
}

public class UpdateMilestoneRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime DueDate { get; set; }

    public bool IsCompleted { get; set; }
}

public class ProjectStatsDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int OverdueProjects { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
}