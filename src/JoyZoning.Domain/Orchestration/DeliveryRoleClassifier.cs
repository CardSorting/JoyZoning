using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

public static class DeliveryRoleClassifier
{
    public static DeliveryRoleKind Classify(WorkTask task)
    {
        var text = $"{task.Title}\n{task.Description}".ToLowerInvariant();

        if (ContainsAny(text, "product lock", "product architect", "docs/product-spec", "docs/product-lock", "user-flows", "acceptance-criteria"))
            return DeliveryRoleKind.ProductArchitect;

        if (ContainsAny(text, "architecture lock", "mobile architecture", "expo router shell", "architecture lead", "mobile architect", "docs/architecture-lock"))
            return DeliveryRoleKind.MobileArchitect;

        if (ContainsAny(text, "core flow", "main user journey", "quest domain", "quest engineer", "features/quests"))
            return DeliveryRoleKind.QuestDomain;

        if (ContainsAny(text, "ui coherence", "design system", "ux lead", "shared/ui", "theme tokens"))
            return DeliveryRoleKind.DesignSystem;

        if (ContainsAny(text, "brain dump", "journal engineer", "features/brain-dump", "features/journal"))
            return DeliveryRoleKind.BrainDumpJournal;

        if (ContainsAny(text, "data & persistence", "data and persistence", "persistence", "reliability engineer", "asyncstorage", "shared/storage"))
            return DeliveryRoleKind.Persistence;

        if (ContainsAny(text, "qa pass", "qa /", "accessibility engineer", "a11y audit", "release-checklist"))
            return DeliveryRoleKind.QaAccessibility;

        if (ContainsAny(text, "polish & recovery", "polish and recovery", "polish / recovery"))
            return DeliveryRoleKind.IntegrationCaptain;

        if (ContainsAny(text, "release seal", "integration", "release captain", "ship report"))
            return DeliveryRoleKind.IntegrationCaptain;

        return DeliveryRoleKind.Unknown;
    }

    public static string RoleLabel(DeliveryRoleKind role) =>
        role switch
        {
            DeliveryRoleKind.ProductArchitect => "product_architect",
            DeliveryRoleKind.MobileArchitect => "mobile_architect",
            DeliveryRoleKind.DesignSystem => "design_system",
            DeliveryRoleKind.QuestDomain => "quest_domain",
            DeliveryRoleKind.BrainDumpJournal => "brain_dump_journal",
            DeliveryRoleKind.Persistence => "persistence",
            DeliveryRoleKind.QaAccessibility => "qa_accessibility",
            DeliveryRoleKind.IntegrationCaptain => "integration",
            _ => "unknown",
        };

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
