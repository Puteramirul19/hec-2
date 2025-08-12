using System;

namespace Hec.Entities
{
    public class BillingComponent : Entity
    {
        public string ComponentName { get; set; }
        public string Description { get; set; }
        public double RatePerKWh { get; set; }  // For per-kWh components (in RM/kWh)
        public double FixedMonthlyRate { get; set; }  // For fixed monthly components (in RM/month)
        public int ThresholdKWh { get; set; }  // Threshold for applicability (e.g., 600 kWh)
        public double PercentageRate { get; set; }  // For percentage-based components (e.g., 8%, 1.6%)
        public bool IsPercentage { get; set; }  // True if this is a percentage-based component
        public bool IsApplicable { get; set; }  // True if this component is currently active
        public bool IsNegative { get; set; }  // True if this is a discount/rebate (negative amount)
        public int Sequence { get; set; }  // Order of calculation
        public string ComponentType { get; set; }  // ENERGY, AFA, CAPACITY, NETWORK, RETAIL, EEI, REBATE, SERVICE_TAX, RE_FUND
    }
}