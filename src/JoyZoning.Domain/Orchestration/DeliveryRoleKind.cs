namespace JoyZoning.Domain.Orchestration;

/// <summary>Classifies delivery roles for path scoping in bounded single-role sessions.</summary>
public enum DeliveryRoleKind
{
    Unknown = 0,
    ProductArchitect,
    MobileArchitect,
    DesignSystem,
    QuestDomain,
    BrainDumpJournal,
    Persistence,
    QaAccessibility,
    IntegrationCaptain,
}
