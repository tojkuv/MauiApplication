namespace MauiApp.Services;

public interface ICurrentUserService
{
    Guid CurrentUserId { get; }
    string CurrentUserName { get; }
    void SetCurrentUser(Guid userId, string userName);
}

public class CurrentUserService : ICurrentUserService
{
    public Guid CurrentUserId { get; private set; }
    public string CurrentUserName { get; private set; } = string.Empty;

    public void SetCurrentUser(Guid userId, string userName)
    {
        CurrentUserId = userId;
        CurrentUserName = userName;
    }
}