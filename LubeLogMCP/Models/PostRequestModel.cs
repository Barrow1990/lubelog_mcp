using System.Text.Json.Serialization;

namespace LubeLogMCP.Models
{
    public class PostRequestModel
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
        [JsonPropertyName("odometer")]
        public int Odometer { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("fuelConsumed")]
        public decimal FuelConsumed { get; set; }
        [JsonPropertyName("cost")]
        public decimal Cost { get; set; }
        [JsonPropertyName("isFillToFull")]
        public bool IsFillToFull { get; set; }
        [JsonPropertyName("missedFuelUp")]
        public bool MissedFuelUp { get; set; }
        [JsonPropertyName("extraFields")]
        public List<ExtraFieldPostModel> ExtraFields { get; set; } = new List<ExtraFieldPostModel>();
        [JsonPropertyName("equipmentRecordId")]
        public string EquipmentRecordId { get; set; } = string.Empty;
        [JsonPropertyName("partQuantity")]
        public decimal PartQuantity { get; set; }
        [JsonPropertyName("partNumber")]
        public string PartNumber { get; set; } = string.Empty;
        [JsonPropertyName("partSupplier")]
        public string PartSupplier { get; set; } = string.Empty;
        [JsonPropertyName("startingSoc")]
        public int StartingSoc { get; set; }
        [JsonPropertyName("endingSoc")]
        public int EndingSoc { get; set; }
        [JsonPropertyName("metric")]
        public string Metric { get; set; } = string.Empty;
        [JsonPropertyName("dueDate")]
        public string DueDate { get; set; } = string.Empty;
        [JsonPropertyName("dueOdometer")]
        public int? DueOdometer { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
        [JsonPropertyName("id")]
        public int? Id { get; set; }
        [JsonPropertyName("tags")]
        public string Tags { get; set; } = string.Empty;
        [JsonPropertyName("initialOdometer")]
        public int? InitialOdometer { get; set; }
        [JsonPropertyName("noteText")]
        public string NoteText { get; set; } = string.Empty;
        [JsonPropertyName("pinned")]
        public bool? Pinned { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;
        [JsonPropertyName("progress")]
        public string Progress { get; set; } = string.Empty;
        [JsonPropertyName("isEquipped")]
        public bool? IsEquipped { get; set; }
    }
    public class ExtraFieldPostModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
    public class VehicleUpdateModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("year")]
        public int Year { get; set; }
        [JsonPropertyName("make")]
        public string Make { get; set; } = string.Empty;
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        [JsonPropertyName("licensePlate")]
        public string LicensePlate { get; set; } = string.Empty;
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "LicensePlate";
        [JsonPropertyName("useEngineHours")]
        public bool UseEngineHours { get; set; }
        [JsonPropertyName("odometerOptional")]
        public bool OdometerOptional { get; set; }
        [JsonPropertyName("fuelType")]
        public string FuelType { get; set; } = string.Empty;
        [JsonPropertyName("extraFields")]
        public List<ExtraFieldPostModel> ExtraFields { get; set; } = new List<ExtraFieldPostModel>();
    }
    public enum PlanType
    {
        ServiceRecord, RepairRecord, GasRecord, TaxRecord, UpgradeRecord, ReminderRecord,
        NoteRecord, SupplyRecord, Dashboard, PlanRecord, OdometerRecord, VehicleRecord, InspectionRecord
    }
    public enum PlanPriority { Critical, Normal, Low }
    public enum PlanProgress { Backlog, InProgress, Testing, Done }
}
