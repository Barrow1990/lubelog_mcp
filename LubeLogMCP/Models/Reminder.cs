namespace LubeLogMCP.Models
{
    // Mirrors lubelog's ReminderAPIExportModel (Models/Shared/ImportModel.cs), the shape
    // GET /api/vehicle/reminders(/all) returns. DueDays/DueDistance are lubelog's own
    // "how far off is this" countdown (computed server-side against today/current mileage);
    // DueDate/DueOdometer are the fixed targets Metric actually uses (Date, Odometer, or Both).
    public class ReminderApiModel
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public string DueOdometer { get; set; } = string.Empty;
        public string DueDays { get; set; } = string.Empty;
        public string DueDistance { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
