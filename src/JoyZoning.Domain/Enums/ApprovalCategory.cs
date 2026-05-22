namespace JoyZoning.Domain.Enums;

public enum ApprovalCategory
{
    FileDelete = 0,
    PackageInstall = 1,
    GitDestructive = 2,
    Migration = 3,
    Shell = 4,
    Network = 5,
    Credential = 6,
    Patch = 7,
    Other = 8,
}
