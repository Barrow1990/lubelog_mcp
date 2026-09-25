namespace LubeLogMCP.Models
{
    // Mirrors lubelog's OdometerRecordExportModel (Models/Shared/ImportModel.cs). Every
    // field comes back as a string there (custom converters on the way in, plain strings
    // on the way out), and System.Text.Json ignores JSON fields with no matching property,
    // so this only lists what the mileage-projection tools need.
    public class OdometerRecordApiModel
    {
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Odometer { get; set; } = string.Empty;
    }
}
