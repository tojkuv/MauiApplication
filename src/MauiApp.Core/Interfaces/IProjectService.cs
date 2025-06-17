using MauiApp.Core.DTOs;

namespace MauiApp.Core.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, Guid ownerId);
    Task<ProjectDto?> GetProjectByIdAsync(Guid id, Guid userId);
    Task<IEnumerable<ProjectDto>> GetUserProjectsAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, Guid userId);
    Task<bool> DeleteProjectAsync(Guid id, Guid userId);
    Task<ProjectDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, Guid userId);
    Task<bool> RemoveMemberAsync(Guid projectId, Guid memberId, Guid userId);
    Task<ProjectMemberDto> UpdateMemberAsync(Guid projectId, Guid memberId, UpdateProjectMemberRequest request, Guid userId);
    Task<IEnumerable<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, Guid userId);
    Task<MilestoneDto> CreateMilestoneAsync(Guid projectId, CreateMilestoneRequest request, Guid userId);
    Task<MilestoneDto> UpdateMilestoneAsync(Guid projectId, Guid milestoneId, UpdateMilestoneRequest request, Guid userId);
    Task<bool> DeleteMilestoneAsync(Guid projectId, Guid milestoneId, Guid userId);
    Task<IEnumerable<MilestoneDto>> GetProjectMilestonesAsync(Guid projectId, Guid userId);
    Task<ProjectStatsDto> GetUserProjectStatsAsync(Guid userId);
}