using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class LeaseRiskMapper
{
    public static LeaseRiskLevel FromTaskRisk(RiskLevel risk) =>
        risk switch
        {
            RiskLevel.Critical or RiskLevel.High => LeaseRiskLevel.Critical,
            RiskLevel.Medium => LeaseRiskLevel.Medium,
            _ => LeaseRiskLevel.Low,
        };
}
