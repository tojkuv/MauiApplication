using Microsoft.EntityFrameworkCore;
using MauiApp.Core.DTOs;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.ProjectsService.Data;

namespace MauiApp.ProjectsService.Services;

public class ProjectService : IProjectService
{
    private readonly ProjectsDbContext _context;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ProjectsDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, Guid ownerId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CoverImageUrl = request.CoverImageUrl,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            OwnerId = ownerId,
            Status = ProjectStatus.Planning
        };

        _context.Projects.Add(project);

        // Add owner as project member
        var ownerMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectRole.Owner
        };
        _context.ProjectMembers.Add(ownerMember);

        // Add other members
        foreach (var memberId in request.MemberIds)
        {
            if (memberId != ownerId) // Don't add owner twice
            {
                var member = new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    UserId = memberId,
                    Role = ProjectRole.Developer
                };
                _context.ProjectMembers.Add(member);
            }
        }

        // Add milestones
        foreach (var milestoneRequest in request.Milestones)
        {
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = milestoneRequest.Title,
                Description = milestoneRequest.Description,
                DueDate = milestoneRequest.DueDate
            };
            _context.Milestones.Add(milestone);
        }

        await _context.SaveChangesAsync();

        return await GetProjectByIdAsync(project.Id, ownerId) ?? throw new InvalidOperationException("Failed to retrieve created project");
    }

    public async Task<ProjectDto?> GetProjectByIdAsync(Guid id, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return null;
        }

        // Check if user has access to this project
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null)
        {
            throw new UnauthorizedAccessException("User does not have access to this project");
        }

        return MapToProjectDto(project);
    }

    public async Task<IEnumerable<ProjectDto>> GetUserProjectsAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var projects = await _context.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Milestones)
            .Where(p => p.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return projects.Select(MapToProjectDto);
    }

    public async Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // Check if user can edit this project (owner or admin)
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null || (userMember.Role != ProjectRole.Owner && userMember.Role != ProjectRole.Admin))
        {
            throw new UnauthorizedAccessException("User does not have permission to edit this project");
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.CoverImageUrl = request.CoverImageUrl;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Status = request.Status;
        project.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectByIdAsync(id, userId) ?? throw new InvalidOperationException("Failed to retrieve updated project");
    }

    public async Task<bool> DeleteProjectAsync(Guid id, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return false;
        }

        // Only owner can delete
        if (project.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("Only the project owner can delete the project");
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ProjectDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // Check if user can manage members (owner or admin)
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null || (userMember.Role != ProjectRole.Owner && userMember.Role != ProjectRole.Admin))
        {
            throw new UnauthorizedAccessException("User does not have permission to manage project members");
        }

        // Check if user is already a member
        var existingMember = project.Members.FirstOrDefault(m => m.UserId == request.UserId);
        if (existingMember != null)
        {
            if (existingMember.IsActive)
            {
                throw new ArgumentException("User is already a member of this project");
            }
            else
            {
                // Reactivate existing member
                existingMember.IsActive = true;
                existingMember.Role = request.Role;
                existingMember.JoinedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var newMember = new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = request.UserId,
                Role = request.Role
            };
            _context.ProjectMembers.Add(newMember);
        }

        await _context.SaveChangesAsync();

        return await GetProjectByIdAsync(projectId, userId) ?? throw new InvalidOperationException("Failed to retrieve updated project");
    }

    public async Task<bool> RemoveMemberAsync(Guid projectId, Guid memberId, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            return false;
        }

        // Check if user can manage members (owner or admin)
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null || (userMember.Role != ProjectRole.Owner && userMember.Role != ProjectRole.Admin))
        {
            throw new UnauthorizedAccessException("User does not have permission to manage project members");
        }

        var memberToRemove = project.Members.FirstOrDefault(m => m.Id == memberId);
        if (memberToRemove == null)
        {
            return false;
        }

        // Can't remove the project owner
        if (memberToRemove.Role == ProjectRole.Owner)
        {
            throw new ArgumentException("Cannot remove the project owner");
        }

        memberToRemove.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ProjectMemberDto> UpdateMemberAsync(Guid projectId, Guid memberId, UpdateProjectMemberRequest request, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // Check if user can manage members
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null || (userMember.Role != ProjectRole.Owner && userMember.Role != ProjectRole.Admin))
        {
            throw new UnauthorizedAccessException("User does not have permission to manage project members");
        }

        var memberToUpdate = project.Members.FirstOrDefault(m => m.Id == memberId);
        if (memberToUpdate == null)
        {
            throw new InvalidOperationException("Member not found");
        }

        memberToUpdate.Role = request.Role;
        memberToUpdate.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return MapToProjectMemberDto(memberToUpdate);
    }

    public async Task<IEnumerable<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, Guid userId)
    {
        var project = await _context.Projects
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // Check if user has access
        var userMember = project.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
        if (userMember == null)
        {
            throw new UnauthorizedAccessException("User does not have access to this project");
        }

        return project.Members.Where(m => m.IsActive).Select(MapToProjectMemberDto);
    }

    public async Task<MilestoneDto> CreateMilestoneAsync(Guid projectId, CreateMilestoneRequest request, Guid userId)
    {
        // Implementation for creating milestones
        throw new NotImplementedException("Milestone creation will be implemented in the next phase");
    }

    public async Task<MilestoneDto> UpdateMilestoneAsync(Guid projectId, Guid milestoneId, UpdateMilestoneRequest request, Guid userId)
    {
        throw new NotImplementedException("Milestone updates will be implemented in the next phase");
    }

    public async Task<bool> DeleteMilestoneAsync(Guid projectId, Guid milestoneId, Guid userId)
    {
        throw new NotImplementedException("Milestone deletion will be implemented in the next phase");
    }

    public async Task<IEnumerable<MilestoneDto>> GetProjectMilestonesAsync(Guid projectId, Guid userId)
    {
        throw new NotImplementedException("Milestone listing will be implemented in the next phase");
    }

    public async Task<ProjectStatsDto> GetUserProjectStatsAsync(Guid userId)
    {
        var stats = await _context.Projects
            .Where(p => p.Members.Any(m => m.UserId == userId && m.IsActive))
            .GroupBy(p => 1)
            .Select(g => new ProjectStatsDto
            {
                TotalProjects = g.Count(),
                ActiveProjects = g.Count(p => p.Status == ProjectStatus.Active),
                CompletedProjects = g.Count(p => p.Status == ProjectStatus.Completed),
                OverdueProjects = g.Count(p => p.EndDate.HasValue && p.EndDate < DateTime.UtcNow && p.Status != ProjectStatus.Completed)
            })
            .FirstOrDefaultAsync();

        return stats ?? new ProjectStatsDto();
    }

    private static ProjectDto MapToProjectDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CoverImageUrl = project.CoverImageUrl,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Status = project.Status,
            OwnerId = project.OwnerId,
            OwnerName = project.Owner?.FullName ?? "Unknown",
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            TaskCount = 0, // Will be implemented when Tasks service is ready
            CompletedTaskCount = 0,
            MemberCount = project.Members.Count(m => m.IsActive),
            Members = project.Members.Where(m => m.IsActive).Select(MapToProjectMemberDto).ToList(),
            Milestones = project.Milestones.Select(MapToMilestoneDto).ToList()
        };
    }

    private static ProjectMemberDto MapToProjectMemberDto(ProjectMember member)
    {
        return new ProjectMemberDto
        {
            Id = member.Id,
            UserId = member.UserId,
            UserName = member.User?.FullName ?? "Unknown",
            UserEmail = member.User?.Email ?? "unknown@email.com",
            UserAvatarUrl = member.User?.AvatarUrl,
            Role = member.Role,
            JoinedAt = member.JoinedAt,
            IsActive = member.IsActive
        };
    }

    private static MilestoneDto MapToMilestoneDto(Milestone milestone)
    {
        return new MilestoneDto
        {
            Id = milestone.Id,
            Title = milestone.Title,
            Description = milestone.Description,
            DueDate = milestone.DueDate,
            IsCompleted = milestone.IsCompleted,
            CompletedAt = milestone.CompletedAt,
            CreatedAt = milestone.CreatedAt
        };
    }
}