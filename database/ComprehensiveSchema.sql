-- ========================================================
-- COMPREHENSIVE DATABASE SCHEMA FOR ENTERPRISE PROJECT MANAGEMENT PLATFORM
-- ========================================================
-- This schema supports all 8 microservices with proper relationships
-- and constraints for data integrity across the platform.

-- ========================================================
-- IDENTITY AND USER MANAGEMENT
-- ========================================================

-- Users table (Core Identity)
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) NOT NULL UNIQUE,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    AvatarUrl NVARCHAR(500) NULL,
    Status INT NOT NULL DEFAULT 1, -- 1=Active, 2=Inactive, 3=Suspended, 4=PendingVerification
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- ASP.NET Identity fields
    UserName NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(MAX) NULL,
    SecurityStamp NVARCHAR(MAX) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    PhoneNumber NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEnd DATETIMEOFFSET NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 1,
    AccessFailedCount INT NOT NULL DEFAULT 0,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    NormalizedEmail NVARCHAR(256) NULL,
    NormalizedUserName NVARCHAR(256) NULL
);

CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_Status ON Users(Status);
CREATE INDEX IX_Users_NormalizedEmail ON Users(NormalizedEmail);
CREATE INDEX IX_Users_NormalizedUserName ON Users(NormalizedUserName);

-- User Roles
CREATE TABLE Roles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(256) NOT NULL,
    NormalizedName NVARCHAR(256) NOT NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);

CREATE TABLE UserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);

-- User Claims
CREATE TABLE UserClaims (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Refresh Tokens
CREATE TABLE RefreshTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(500) NOT NULL UNIQUE,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsRevoked BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);

-- ========================================================
-- PROJECT MANAGEMENT
-- ========================================================

-- Projects
CREATE TABLE Projects (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    CoverImageUrl NVARCHAR(500) NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NULL,
    Status INT NOT NULL DEFAULT 1, -- 1=Planning, 2=Active, 3=OnHold, 4=Completed, 5=Cancelled
    Priority INT NOT NULL DEFAULT 2, -- 1=Low, 2=Medium, 3=High, 4=Critical
    OwnerId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (OwnerId) REFERENCES Users(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_Projects_OwnerId ON Projects(OwnerId);
CREATE INDEX IX_Projects_Status ON Projects(Status);
CREATE INDEX IX_Projects_Priority ON Projects(Priority);
CREATE INDEX IX_Projects_StartDate ON Projects(StartDate);

-- Project Members
CREATE TABLE ProjectMembers (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Member', -- Owner, Manager, Member, Viewer
    JoinedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    UNIQUE (ProjectId, UserId)
);

CREATE INDEX IX_ProjectMembers_ProjectId ON ProjectMembers(ProjectId);
CREATE INDEX IX_ProjectMembers_UserId ON ProjectMembers(UserId);

-- Project Milestones
CREATE TABLE Milestones (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    DueDate DATETIME2 NOT NULL,
    IsCompleted BIT NOT NULL DEFAULT 0,
    CompletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Milestones_ProjectId ON Milestones(ProjectId);
CREATE INDEX IX_Milestones_DueDate ON Milestones(DueDate);

-- ========================================================
-- TASK MANAGEMENT
-- ========================================================

-- Tasks
CREATE TABLE Tasks (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(2000) NULL,
    Status INT NOT NULL DEFAULT 1, -- 1=ToDo, 2=InProgress, 3=Review, 4=Done, 5=Cancelled
    Priority INT NOT NULL DEFAULT 2, -- 1=Low, 2=Medium, 3=High, 4=Critical
    DueDate DATETIME2 NULL,
    EstimatedHours INT NOT NULL DEFAULT 0,
    ActualHours INT NOT NULL DEFAULT 0,
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    AssigneeId UNIQUEIDENTIFIER NULL,
    CreatedById UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (AssigneeId) REFERENCES Users(Id) ON DELETE SET NULL,
    FOREIGN KEY (CreatedById) REFERENCES Users(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_Tasks_ProjectId ON Tasks(ProjectId);
CREATE INDEX IX_Tasks_AssigneeId ON Tasks(AssigneeId);
CREATE INDEX IX_Tasks_Status ON Tasks(Status);
CREATE INDEX IX_Tasks_Priority ON Tasks(Priority);
CREATE INDEX IX_Tasks_DueDate ON Tasks(DueDate);
CREATE INDEX IX_Tasks_CreatedById ON Tasks(CreatedById);

-- Task Comments
CREATE TABLE TaskComments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TaskId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsEdited BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TaskComments_TaskId ON TaskComments(TaskId);
CREATE INDEX IX_TaskComments_UserId ON TaskComments(UserId);
CREATE INDEX IX_TaskComments_CreatedAt ON TaskComments(CreatedAt);

-- Task Attachments
CREATE TABLE TaskAttachments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TaskId UNIQUEIDENTIFIER NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    FileSize BIGINT NOT NULL,
    BlobUrl NVARCHAR(500) NOT NULL,
    UploadedById UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
    FOREIGN KEY (UploadedById) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TaskAttachments_TaskId ON TaskAttachments(TaskId);

-- Task Dependencies
CREATE TABLE TaskDependencies (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TaskId UNIQUEIDENTIFIER NOT NULL,
    DependsOnTaskId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE NO ACTION,
    FOREIGN KEY (DependsOnTaskId) REFERENCES Tasks(Id) ON DELETE NO ACTION,
    UNIQUE (TaskId, DependsOnTaskId)
);

-- Time Entries
CREATE TABLE TimeEntries (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Description NVARCHAR(500) NOT NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NULL,
    DurationMinutes INT NOT NULL,
    TaskId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    IsBillable BIT NOT NULL DEFAULT 1,
    HourlyRate DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TimeEntries_TaskId ON TimeEntries(TaskId);
CREATE INDEX IX_TimeEntries_UserId ON TimeEntries(UserId);
CREATE INDEX IX_TimeEntries_StartTime ON TimeEntries(StartTime);

-- ========================================================
-- FILE MANAGEMENT
-- ========================================================

-- Project Files
CREATE TABLE ProjectFiles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FileName NVARCHAR(255) NOT NULL,
    OriginalFileName NVARCHAR(255) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    FileSize BIGINT NOT NULL,
    BlobUrl NVARCHAR(500) NOT NULL,
    ThumbnailUrl NVARCHAR(500) NULL,
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    UploadedById UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (UploadedById) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProjectFiles_ProjectId ON ProjectFiles(ProjectId);
CREATE INDEX IX_ProjectFiles_UploadedById ON ProjectFiles(UploadedById);

-- File Versions
CREATE TABLE FileVersions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FileId UNIQUEIDENTIFIER NOT NULL,
    VersionNumber INT NOT NULL,
    BlobUrl NVARCHAR(500) NOT NULL,
    FileSize BIGINT NOT NULL,
    UploadedById UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (FileId) REFERENCES ProjectFiles(Id) ON DELETE CASCADE,
    FOREIGN KEY (UploadedById) REFERENCES Users(Id) ON DELETE CASCADE
);

-- File Shares
CREATE TABLE FileShares (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FileId UNIQUEIDENTIFIER NOT NULL,
    SharedWithUserId UNIQUEIDENTIFIER NOT NULL,
    Permission NVARCHAR(20) NOT NULL DEFAULT 'View', -- View, Edit, Admin
    SharedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (FileId) REFERENCES ProjectFiles(Id) ON DELETE CASCADE,
    FOREIGN KEY (SharedWithUserId) REFERENCES Users(Id) ON DELETE CASCADE,
    UNIQUE (FileId, SharedWithUserId)
);

-- File Comments
CREATE TABLE FileComments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FileId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (FileId) REFERENCES ProjectFiles(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ========================================================
-- COLLABORATION
-- ========================================================

-- Chat Messages
CREATE TABLE ChatMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    AuthorId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(2000) NOT NULL,
    MessageType NVARCHAR(50) NOT NULL DEFAULT 'text',
    ParentMessageId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsEdited BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (AuthorId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentMessageId) REFERENCES ChatMessages(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_ChatMessages_ProjectId ON ChatMessages(ProjectId);
CREATE INDEX IX_ChatMessages_AuthorId ON ChatMessages(AuthorId);
CREATE INDEX IX_ChatMessages_CreatedAt ON ChatMessages(CreatedAt);

-- Message Reactions
CREATE TABLE MessageReactions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MessageId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Emoji NVARCHAR(10) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (MessageId) REFERENCES ChatMessages(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    UNIQUE (MessageId, UserId, Emoji)
);

-- ========================================================
-- NOTIFICATIONS
-- ========================================================

-- Notifications
CREATE TABLE Notifications (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SenderId UNIQUEIDENTIFIER NULL,
    Title NVARCHAR(200) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    Type INT NOT NULL DEFAULT 1, -- NotificationType enum
    Priority INT NOT NULL DEFAULT 2, -- NotificationPriority enum
    Status INT NOT NULL DEFAULT 1, -- NotificationStatus enum
    DataJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    ActionUrl NVARCHAR(500) NULL,
    ImageUrl NVARCHAR(500) NULL,
    ExpiresAt DATETIME2 NULL,
    ScheduledAt DATETIME2 NULL,
    IsScheduled BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ReadAt DATETIME2 NULL,
    SentAt DATETIME2 NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (SenderId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Notifications_UserId_Status ON Notifications(UserId, Status);
CREATE INDEX IX_Notifications_UserId_ReadAt ON Notifications(UserId, ReadAt);
CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt);
CREATE INDEX IX_Notifications_Type_Status ON Notifications(Type, Status);
CREATE INDEX IX_Notifications_ScheduledAt ON Notifications(ScheduledAt);

-- Device Tokens
CREATE TABLE DeviceTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(500) NOT NULL,
    Platform INT NOT NULL, -- DevicePlatform enum
    AppVersion NVARCHAR(50) NULL,
    DeviceModel NVARCHAR(100) NULL,
    OSVersion NVARCHAR(50) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastUsedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    UNIQUE (Token, Platform)
);

CREATE INDEX IX_DeviceTokens_UserId_IsActive ON DeviceTokens(UserId, IsActive);
CREATE INDEX IX_DeviceTokens_LastUsedAt ON DeviceTokens(LastUsedAt);

-- Notification Preferences
CREATE TABLE NotificationPreferences (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    PushNotificationsEnabled BIT NOT NULL DEFAULT 1,
    EmailNotificationsEnabled BIT NOT NULL DEFAULT 1,
    InAppNotificationsEnabled BIT NOT NULL DEFAULT 1,
    TypePreferencesJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    QuietHoursStart TIME NULL,
    QuietHoursEnd TIME NULL,
    QuietHoursDaysJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    MaxNotificationsPerHour INT NOT NULL DEFAULT 10,
    MaxNotificationsPerDay INT NOT NULL DEFAULT 50,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    UNIQUE (UserId)
);

-- ========================================================
-- SYNC SERVICE
-- ========================================================

-- Sync Clients
CREATE TABLE SyncClients (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ClientInfo NVARCHAR(500) NULL,
    LastSyncTimestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastSeenAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    EntityLastSyncTimestampsJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SyncClients_UserId ON SyncClients(UserId);
CREATE INDEX IX_SyncClients_LastSeenAt ON SyncClients(LastSeenAt);
CREATE INDEX IX_SyncClients_UserId_IsActive ON SyncClients(UserId, IsActive);

-- Sync Items
CREATE TABLE SyncItems (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Operation NVARCHAR(50) NOT NULL,
    Data NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status INT NOT NULL DEFAULT 1, -- SyncStatus enum
    RetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage NVARCHAR(1000) NULL,
    LastRetry DATETIME2 NULL,
    ClientId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ClientId) REFERENCES SyncClients(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_SyncItems_ClientId_Status ON SyncItems(ClientId, Status);
CREATE INDEX IX_SyncItems_EntityType_EntityId ON SyncItems(EntityType, EntityId);
CREATE INDEX IX_SyncItems_Timestamp ON SyncItems(Timestamp);
CREATE INDEX IX_SyncItems_UserId_Status ON SyncItems(UserId, Status);

-- ========================================================
-- ANALYTICS
-- ========================================================

-- Analytics Cache
CREATE TABLE AnalyticsCache (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CacheKey NVARCHAR(200) NOT NULL UNIQUE,
    Data NVARCHAR(MAX) NOT NULL,
    DataType NVARCHAR(100) NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NOT NULL
);

CREATE INDEX IX_AnalyticsCache_ExpiresAt ON AnalyticsCache(ExpiresAt);
CREATE INDEX IX_AnalyticsCache_EntityType_EntityId ON AnalyticsCache(EntityType, EntityId);

-- Daily Metrics
CREATE TABLE DailyMetrics (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Date DATE NOT NULL,
    MetricType NVARCHAR(100) NOT NULL,
    MetricName NVARCHAR(200) NOT NULL,
    Value FLOAT NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NULL,
    Metadata NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UNIQUE (Date, MetricType, EntityType, EntityId)
);

CREATE INDEX IX_DailyMetrics_Date ON DailyMetrics(Date);

-- ========================================================
-- ACTIVITY LOGGING
-- ========================================================

-- User Activity Logs
CREATE TABLE UserActivityLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ActivityType NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId UNIQUEIDENTIFIER NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    AdditionalData NVARCHAR(MAX) NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserActivityLogs_UserId ON UserActivityLogs(UserId);
CREATE INDEX IX_UserActivityLogs_Timestamp ON UserActivityLogs(Timestamp);
CREATE INDEX IX_UserActivityLogs_UserId_ActivityType ON UserActivityLogs(UserId, ActivityType);

-- ========================================================
-- AUDIT TRAIL
-- ========================================================

-- Audit Logs (for tracking all changes)
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TableName NVARCHAR(100) NOT NULL,
    RecordId UNIQUEIDENTIFIER NOT NULL,
    Action NVARCHAR(10) NOT NULL, -- INSERT, UPDATE, DELETE
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    UserId UNIQUEIDENTIFIER NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IX_AuditLogs_TableName_RecordId ON AuditLogs(TableName, RecordId);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);

-- ========================================================
-- CONSTRAINTS AND BUSINESS RULES
-- ========================================================

-- Ensure task due date is not in the past when creating
-- (This would be enforced in application logic)

-- Ensure project end date is after start date
ALTER TABLE Projects ADD CONSTRAINT CK_Projects_EndDateAfterStart 
    CHECK (EndDate IS NULL OR EndDate >= StartDate);

-- Ensure time entries have valid duration
ALTER TABLE TimeEntries ADD CONSTRAINT CK_TimeEntries_ValidDuration 
    CHECK (DurationMinutes >= 0);

-- Ensure file size is positive
ALTER TABLE ProjectFiles ADD CONSTRAINT CK_ProjectFiles_PositiveFileSize 
    CHECK (FileSize > 0);

ALTER TABLE TaskAttachments ADD CONSTRAINT CK_TaskAttachments_PositiveFileSize 
    CHECK (FileSize > 0);

-- ========================================================
-- INITIAL DATA SETUP
-- ========================================================

-- Insert default roles
INSERT INTO Roles (Name, NormalizedName) VALUES 
('Admin', 'ADMIN'),
('ProjectManager', 'PROJECTMANAGER'),
('TeamLead', 'TEAMLEAD'),
('Developer', 'DEVELOPER'),
('Viewer', 'VIEWER');

-- ========================================================
-- STORED PROCEDURES FOR COMMON OPERATIONS
-- ========================================================

-- Get project dashboard data
CREATE OR ALTER PROCEDURE GetProjectDashboard
    @ProjectId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verify user has access to project
    IF NOT EXISTS (
        SELECT 1 FROM ProjectMembers 
        WHERE ProjectId = @ProjectId AND UserId = @UserId
    )
    BEGIN
        RAISERROR('User does not have access to this project', 16, 1);
        RETURN;
    END
    
    -- Project basic info
    SELECT 
        p.Id,
        p.Name,
        p.Description,
        p.Status,
        p.StartDate,
        p.EndDate,
        p.CoverImageUrl,
        u.FirstName + ' ' + u.LastName AS OwnerName
    FROM Projects p
    INNER JOIN Users u ON p.OwnerId = u.Id
    WHERE p.Id = @ProjectId;
    
    -- Task statistics
    SELECT 
        COUNT(*) AS TotalTasks,
        SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS CompletedTasks,
        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS InProgressTasks,
        SUM(CASE WHEN DueDate < GETUTCDATE() AND Status != 4 THEN 1 ELSE 0 END) AS OverdueTasks
    FROM Tasks
    WHERE ProjectId = @ProjectId;
    
    -- Team members
    SELECT 
        u.Id,
        u.FirstName,
        u.LastName,
        u.AvatarUrl,
        pm.Role
    FROM ProjectMembers pm
    INNER JOIN Users u ON pm.UserId = u.Id
    WHERE pm.ProjectId = @ProjectId;
END;

-- Get user dashboard data
CREATE OR ALTER PROCEDURE GetUserDashboard
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- User's active projects
    SELECT 
        p.Id,
        p.Name,
        p.Status,
        p.CoverImageUrl,
        COUNT(t.Id) AS TaskCount,
        SUM(CASE WHEN t.Status = 4 THEN 1 ELSE 0 END) AS CompletedTasks
    FROM Projects p
    INNER JOIN ProjectMembers pm ON p.Id = pm.ProjectId
    LEFT JOIN Tasks t ON p.Id = t.ProjectId
    WHERE pm.UserId = @UserId AND p.Status IN (1, 2) -- Planning or Active
    GROUP BY p.Id, p.Name, p.Status, p.CoverImageUrl;
    
    -- User's tasks summary
    SELECT 
        COUNT(*) AS TotalTasks,
        SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END) AS CompletedTasks,
        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS InProgressTasks,
        SUM(CASE WHEN DueDate < GETUTCDATE() AND Status != 4 THEN 1 ELSE 0 END) AS OverdueTasks
    FROM Tasks
    WHERE AssigneeId = @UserId;
    
    -- Recent activity
    SELECT TOP 10
        ActivityType,
        Description,
        Timestamp
    FROM UserActivityLogs
    WHERE UserId = @UserId
    ORDER BY Timestamp DESC;
END;

-- ========================================================
-- TRIGGERS FOR AUDIT LOGGING
-- ========================================================

-- Create audit trigger for Projects table
CREATE OR ALTER TRIGGER TR_Projects_Audit
ON Projects
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Handle INSERT
    IF EXISTS(SELECT * FROM inserted) AND NOT EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO AuditLogs (TableName, RecordId, Action, NewValues, UserId)
        SELECT 'Projects', Id, 'INSERT', 
               (SELECT * FROM inserted i WHERE i.Id = inserted.Id FOR JSON PATH),
               NULL -- UserId would need to be passed from application context
        FROM inserted;
    END
    
    -- Handle UPDATE
    IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO AuditLogs (TableName, RecordId, Action, OldValues, NewValues, UserId)
        SELECT 'Projects', i.Id, 'UPDATE',
               (SELECT * FROM deleted d WHERE d.Id = i.Id FOR JSON PATH),
               (SELECT * FROM inserted ins WHERE ins.Id = i.Id FOR JSON PATH),
               NULL
        FROM inserted i;
    END
    
    -- Handle DELETE
    IF NOT EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO AuditLogs (TableName, RecordId, Action, OldValues, UserId)
        SELECT 'Projects', Id, 'DELETE',
               (SELECT * FROM deleted d WHERE d.Id = deleted.Id FOR JSON PATH),
               NULL
        FROM deleted;
    END
END;

-- ========================================================
-- VIEWS FOR COMMON QUERIES
-- ========================================================

-- Project overview with statistics
CREATE OR ALTER VIEW ProjectOverview AS
SELECT 
    p.Id,
    p.Name,
    p.Description,
    p.Status,
    p.StartDate,
    p.EndDate,
    p.CoverImageUrl,
    o.FirstName + ' ' + o.LastName AS OwnerName,
    COUNT(DISTINCT pm.UserId) AS TeamSize,
    COUNT(DISTINCT t.Id) AS TotalTasks,
    COUNT(DISTINCT CASE WHEN t.Status = 4 THEN t.Id END) AS CompletedTasks,
    COUNT(DISTINCT CASE WHEN t.DueDate < GETUTCDATE() AND t.Status != 4 THEN t.Id END) AS OverdueTasks,
    p.CreatedAt,
    p.UpdatedAt
FROM Projects p
INNER JOIN Users o ON p.OwnerId = o.Id
LEFT JOIN ProjectMembers pm ON p.Id = pm.ProjectId
LEFT JOIN Tasks t ON p.Id = t.ProjectId
GROUP BY 
    p.Id, p.Name, p.Description, p.Status, p.StartDate, p.EndDate, 
    p.CoverImageUrl, o.FirstName, o.LastName, p.CreatedAt, p.UpdatedAt;

-- Task overview with project and user details
CREATE OR ALTER VIEW TaskOverview AS
SELECT 
    t.Id,
    t.Title,
    t.Description,
    t.Status,
    t.Priority,
    t.DueDate,
    t.EstimatedHours,
    t.ActualHours,
    p.Name AS ProjectName,
    a.FirstName + ' ' + a.LastName AS AssigneeName,
    c.FirstName + ' ' + c.LastName AS CreatedByName,
    t.CreatedAt,
    t.UpdatedAt,
    CASE 
        WHEN t.DueDate < GETUTCDATE() AND t.Status != 4 THEN 1 
        ELSE 0 
    END AS IsOverdue
FROM Tasks t
INNER JOIN Projects p ON t.ProjectId = p.Id
LEFT JOIN Users a ON t.AssigneeId = a.Id
INNER JOIN Users c ON t.CreatedById = c.Id;

-- ========================================================
-- PERFORMANCE OPTIMIZATION
-- ========================================================

-- Additional indexes for common query patterns
CREATE INDEX IX_Tasks_ProjectId_Status_Priority ON Tasks(ProjectId, Status, Priority);
CREATE INDEX IX_ProjectMembers_UserId_Role ON ProjectMembers(UserId, Role);
CREATE INDEX IX_ChatMessages_ProjectId_CreatedAt ON ChatMessages(ProjectId, CreatedAt DESC);
CREATE INDEX IX_Notifications_UserId_CreatedAt ON Notifications(UserId, CreatedAt DESC);
CREATE INDEX IX_TimeEntries_UserId_StartTime ON TimeEntries(UserId, StartTime DESC);

-- ========================================================
-- DATA RETENTION POLICIES
-- ========================================================

-- Procedure to clean up old data
CREATE OR ALTER PROCEDURE CleanupOldData
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate DATETIME2 = DATEADD(YEAR, -2, GETUTCDATE());
    
    -- Clean up old audit logs (keep 2 years)
    DELETE FROM AuditLogs WHERE Timestamp < @CutoffDate;
    
    -- Clean up old user activity logs (keep 1 year)
    DELETE FROM UserActivityLogs WHERE Timestamp < DATEADD(YEAR, -1, GETUTCDATE());
    
    -- Clean up expired refresh tokens
    DELETE FROM RefreshTokens WHERE ExpiresAt < GETUTCDATE() OR IsRevoked = 1;
    
    -- Clean up expired analytics cache
    DELETE FROM AnalyticsCache WHERE ExpiresAt < GETUTCDATE();
    
    -- Clean up old notifications (keep 6 months)
    DELETE FROM Notifications WHERE CreatedAt < DATEADD(MONTH, -6, GETUTCDATE()) AND Status IN (5, 6, 8); -- Read, Failed, Expired
    
    PRINT 'Data cleanup completed successfully.';
END;

-- ========================================================
-- SECURITY FUNCTIONS
-- ========================================================

-- Function to check if user has project access
CREATE OR ALTER FUNCTION HasProjectAccess(@UserId UNIQUEIDENTIFIER, @ProjectId UNIQUEIDENTIFIER)
RETURNS BIT
AS
BEGIN
    DECLARE @HasAccess BIT = 0;
    
    IF EXISTS (
        SELECT 1 FROM ProjectMembers 
        WHERE ProjectId = @ProjectId AND UserId = @UserId
    )
        SET @HasAccess = 1;
    
    RETURN @HasAccess;
END;

-- Function to check if user can edit task
CREATE OR ALTER FUNCTION CanEditTask(@UserId UNIQUEIDENTIFIER, @TaskId UNIQUEIDENTIFIER)
RETURNS BIT
AS
BEGIN
    DECLARE @CanEdit BIT = 0;
    
    IF EXISTS (
        SELECT 1 FROM Tasks t
        INNER JOIN ProjectMembers pm ON t.ProjectId = pm.ProjectId
        WHERE t.Id = @TaskId 
        AND pm.UserId = @UserId 
        AND (pm.Role IN ('Owner', 'Manager') OR t.AssigneeId = @UserId OR t.CreatedById = @UserId)
    )
        SET @CanEdit = 1;
    
    RETURN @CanEdit;
END;

-- ========================================================
-- COMPLETION MESSAGE
-- ========================================================

PRINT 'Comprehensive database schema created successfully!';
PRINT 'Schema includes:';
PRINT '- Identity and user management';
PRINT '- Project and task management';
PRINT '- File storage and collaboration';
PRINT '- Notifications and sync services';
PRINT '- Analytics and audit logging';
PRINT '- Security functions and constraints';
PRINT '- Performance optimization indexes';
PRINT '- Data retention policies';