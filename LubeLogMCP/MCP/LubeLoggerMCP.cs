using LubeLogMCP.Helper;
using LubeLogMCP.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace LubeLogMCP.MCP
{
    [McpServerToolType]
    public class LubeLoggerMCP
    {
        private string instance { get; set; }
        private string username { get; set; }
        private string password { get; set; }
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public LubeLoggerMCP(IConfiguration _config, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            instance = _config["LUBELOG_INSTANCE"] ?? string.Empty;
            username = _config["LUBELOG_USER"] ?? string.Empty;
            password = _config["LUBELOG_PASS"] ?? string.Empty;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }
        [McpServerTool, Description("Gets status of LubeLogger MCP.")]
        public async Task<string> GetLubeLoggerMCPStatus()
        {
            string result = $"MCP Version: {StaticHelper.VersionNumber}";
            result += Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(instance))
            {
                result += $"MCP Server Configured for {instance}";
                result += Environment.NewLine;
                string endpoint = $"{instance}/api/version";
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                AddAuthHeaders(request);
                if (request.Headers.Contains("Authorization") || request.Headers.Contains("x-api-key"))
                {
                    result += "Auth Configured";
                } else
                {
                    result += "Auth Not Configured";
                }
                result += Environment.NewLine;
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var serverResponse = await httpClient.SendAsync(request).Result.Content.ReadFromJsonAsync<ServerVersion>();
                    if (!string.IsNullOrWhiteSpace(serverResponse?.CurrentVersion))
                    {
                        result += $"LubeLogger Version: {serverResponse.CurrentVersion}";
                    }
                }
                catch (Exception ex)
                {
                    result += $"Failed to connect to LubeLogger instance: {ex.Message}";
                }
            }
            else
            {
                result += "LubeLogger Instance not Configured";
            }
            return result;
        }
        [McpServerTool, Description("Says which LubeLogger account the configured credentials belong to: username, email, and whether it is an admin and/or the root user. Useful for working out why a request is refused.")]
        public async Task<string> WhoAmI()
        {
            string endpoint = $"{instance}/api/whoami";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets vehicles in garage.")]
        public async Task<string> GetVehicles()
        {
            string endpoint = $"{instance}/api/vehicles";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadFromJsonAsync<List<Vehicle>>();
                var resultString = string.Empty;
                foreach(Vehicle vehicle in result ?? new List<Vehicle>())
                {
                    resultString += $"Id: {vehicle.Id} - {vehicle.Year} {vehicle.Make} {vehicle.Model}({vehicle.Identifier})";
                    resultString += Environment.NewLine;
                }
                return resultString;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets LubeLogger server information: version, locale, currency symbol, decimal separator and date format.")]
        public async Task<string> GetServerInformation()
        {
            string endpoint = $"{instance}/api/info";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Check if a vehicle is an electric vehicle")]
        public async Task<string> GetVehicleIsElectric([Description("id of the vehicle")] int vehicleId)
        {
            string endpoint = $"{instance}/api/vehicles";
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadFromJsonAsync<List<Vehicle>>();
                foreach (Vehicle vehicle in result ?? new List<Vehicle>())
                {
                    if (vehicle.Id == vehicleId && vehicle.IsElectric)
                    {
                        return "this is an electric vehicle";
                    }
                }
                return "this is not an electric vehicle";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets summary statistics for a vehicle, or for every vehicle you can see when vehicleId is omitted: record counts and costs per record type, reminder counts by urgency, the next reminder, planner counts and the last reported odometer.")]
        public async Task<string> GetVehicleInfo(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null
            )
        {
            string endpoint = $"{instance}/api/vehicle/info" + (vehicleId.HasValue ? $"?vehicleId={vehicleId.Value}" : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a fuel record.")]
        public async Task<string> AddFuelRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date of fuel up")] DateTime date,
            [Description("Odometer at time of fuel up")] int odometer,
            [Description("Volume of gas pumped")] decimal volume,
            [Description("Total cost of fuel up")] decimal cost,
            [Description("Is fueled up completely")] bool fillToFull,
            [Description("Any missed fuel ups")] bool missedFuelUp,
            [Description("State of Charge at beginning of charge session if charging an electric vehicle, default to 20 if not an electric vehicle")] int startingSoc,
            [Description("State of Charge at end of charge session if charging an electric vehicle, default to 80 if not an electric vehicle")] int endingSoc,
            [Description("Any extra fields configured for gasrecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                FuelConsumed = volume,
                Cost = cost,
                IsFillToFull = fillToFull,
                MissedFuelUp = missedFuelUp,
                StartingSoc = startingSoc,
                EndingSoc = endingSoc
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value  });
            }

            string endpoint = $"{instance}/api/vehicle/gasrecords/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets fuel (gas) records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetFuelRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/gasrecords" : "/api/vehicle/gasrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a service record.")]
        public async Task<string> AddServiceRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date serviced")] DateTime date,
            [Description("Odometer at time of service")] int odometer,
            [Description("Description of items serviced")] string description,
            [Description("Total cost of the service")] decimal cost,
            [Description("Any extra fields configured for servicerecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/servicerecords/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets service records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetServiceRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/servicerecords" : "/api/vehicle/servicerecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a repair record.")]
        public async Task<string> AddRepairRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date repaired")] DateTime date,
            [Description("Odometer at time of repair")] int odometer,
            [Description("Description of items repaired")] string description,
            [Description("Total cost of the repair")] decimal cost,
            [Description("Any extra fields configured for repairrecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/repairrecords/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets repair records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetRepairRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/repairrecords" : "/api/vehicle/repairrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds an upgrade record.")]
        public async Task<string> AddUpgradeRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date upgraded")] DateTime date,
            [Description("Odometer at time of upgrade")] int odometer,
            [Description("Description of items upgraded")] string description,
            [Description("Total cost of the upgrade")] decimal cost,
            [Description("Any extra fields configured for upgraderecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/upgraderecords/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets upgrade records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetUpgradeRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/upgraderecords" : "/api/vehicle/upgraderecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds an odometer record.")]
        public async Task<string> AddOdometerRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date recorded")] DateTime date,
            [Description("Odometer recorded")] int odometer,
            [Description("Any extra fields configured for odometerrecord")] List<ExtraField> extraFields,
            [Description("Ids of equipment equipped for the vehicle")] List<int> equipmentRecordIds)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer
            };

            if (equipmentRecordIds.Any())
            {
                requestData.EquipmentRecordId = string.Join(' ', equipmentRecordIds);
            }

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            // autoIncludeEquipment is sent explicitly so lubelog's QueryParamFilter does not go looking for it in
            // the request body. That filter is `async void` and only finds a rewindable body when the request
            // Content-Type is exactly "application/json" (StringContent adds "; charset=utf-8"), so with the
            // parameter missing it throws on the raw stream, which takes lubelog itself down.
            string endpoint = $"{instance}/api/vehicle/odometerrecords/add?vehicleId={vehicleId}&autoIncludeEquipment=false";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets planner (plan) records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetPlanRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/planrecords" : "/api/vehicle/planrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Get Equipped Equipment for a vehicle")]
        public async Task<string> GetEquippedEquipment(
            [Description("id of the vehicle")] int vehicleId
            )
        {

            string endpoint = $"{instance}/api/vehicle/equipmentrecords?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadFromJsonAsync<List<Equipment>>();
                result?.RemoveAll(x => !x.IsEquipped);
                var serializedResult = JsonSerializer.Serialize(result);
                return serializedResult;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets tax records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetTaxRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/taxrecords" : "/api/vehicle/taxrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a supply record.")]
        public async Task<string> AddSupplyRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date purchased")] DateTime date,
            [Description("Description of the supply")] string description,
            [Description("Quantity purchased")] decimal quantity,
            [Description("Cost of the supply")] decimal cost,
            [Description("Part number")] string partNumber,
            [Description("Part supplier")] string partSupplier,
            [Description("Any extra fields configured for supplyrecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Description = description,
                PartQuantity = quantity,
                Cost = cost,
                PartNumber = partNumber,
                PartSupplier = partSupplier
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/supplyrecords/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets supply records for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetSupplyRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/supplyrecords" : "/api/vehicle/supplyrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a shop supply record.")]
        public async Task<string> AddShopSupplyRecord(
            [Description("Date purchased")] DateTime date,
            [Description("Description of the supply")] string description,
            [Description("Quantity purchased")] decimal quantity,
            [Description("Cost of the supply")] decimal cost,
            [Description("Part number")] string partNumber,
            [Description("Part supplier")] string partSupplier,
            [Description("Any extra fields configured for supplyrecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Description = description,
                PartQuantity = quantity,
                Cost = cost,
                PartNumber = partNumber,
                PartSupplier = partSupplier
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/supplyrecords/add?vehicleId=0";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets notes for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological.")]
        public async Task<string> GetNotes(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {
            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/notes" : "/api/vehicle/notes/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer: lubelog writes several numeric fields
                // as bare JSON numbers, so a typed model here would break on the next release.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a vehicle record.")]
        public async Task<string> AddVehicleRecord(
            [Description("Model year of the vehicle")] int year,
            [Description("Make of the vehicle")] string make,
            [Description("Model of the vehicle")] string model,
            [Description("Optional license plate of the vehicle")] string licensePlate,
            [Description("Vehicle use engine hours")] bool useEngineHours,
            [Description("Odometer is optional for vehicle")] bool odometerOptional,
            [Description("Fuel type for the vehicle")] FuelType fuelType,
            [Description("Any extra fields configured for vehiclerecord")] List<ExtraField> extraFields)
        {
            var dataParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("year", year.ToString()),
                new KeyValuePair<string, string>("make", make),
                new KeyValuePair<string, string>("model", model),
                new KeyValuePair<string, string>("useEngineHours", useEngineHours.ToString()),
                new KeyValuePair<string, string>("odometerOptional", odometerOptional.ToString()),
                new KeyValuePair<string, string>("identifier", "LicensePlate"),
                new KeyValuePair<string, string>("licensePlate", licensePlate),
                new KeyValuePair<string, string>("fuelType", fuelType.ToString())
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                dataParams.Add(new KeyValuePair<string, string>($"extraFields[{i}][name]", extraFields[i].Name));
                dataParams.Add(new KeyValuePair<string, string>($"extraFields[{i}][value]", extraFields[i].Value));
            }

            string endpoint = $"{instance}/api/vehicles/add";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new FormUrlEncodedContent(dataParams)
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Adds a maintenance reminder for a vehicle: due at a date, an odometer reading, or whichever of the two comes first.")]
        public async Task<string> AddReminderRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("What the reminder is for")] string description,
            [Description("Date: due on a date only. Odometer: due at a reading only. Both: due whichever of dueDate/dueOdometer comes first")] ReminderMetric metric,
            [Description("Due date. Required unless metric is Odometer")] DateTime? dueDate,
            [Description("Due odometer reading. Required unless metric is Date")] int? dueOdometer,
            [Description("Optional notes")] string notes = "")
        {
            var requestData = new PostRequestModel
            {
                Description = description,
                Metric = metric.ToString(),
                DueDate = dueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                DueOdometer = dueOdometer,
                Notes = notes
            };

            string endpoint = $"{instance}/api/vehicle/reminders/add?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            };
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets latest odometer reading for a vehicle.")]
        public async Task<string> GetLatestOdometer(
            [Description("id of the vehicle")] int vehicleId
            )
        {

            string endpoint = $"{instance}/api/vehicle/odometerrecords/latest?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();

                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets odometer history for a vehicle, or for every vehicle you can see when vehicleId is omitted. Optionally limited to a date range and/or tags. Records are in the order LubeLogger stores them, which is not guaranteed to be chronological, so sort on the date field before working out average distance per day to project forward to a future mileage.")]
        public async Task<string> GetOdometerRecords(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only records on or after this date")] DateTime? startDate = null,
            [Description("Only records on or before this date")] DateTime? endDate = null,
            [Description("Space-separated tags; records with any of them are returned")] string tags = ""
            )
        {

            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            if (startDate.HasValue) query.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/odometerrecords" : "/api/vehicle/odometerrecords/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer below: lubelog's own export DTO for
                // this route types id/odometer as string with a lenient (string-or-number) converter
                // for *reading* an import payload, but what it actually writes back on GET is a bare
                // JSON number for those fields — a strongly-typed model here would only be one lubelog
                // release away from breaking again. Records are returned in the order
                // LubeLogger stores them, which is not guaranteed to be chronological, so callers should sort on
                // the "date" field.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets maintenance reminders for a vehicle, or for every vehicle you can see when vehicleId is omitted: what is due, on which metric (Date, Odometer, or Both), the due date/odometer, and lubelog's own urgency and days/distance-remaining countdown as of now. Optionally limited to certain urgencies and/or tags.")]
        public async Task<string> GetReminders(
            [Description("id of the vehicle; omit for all vehicles")] int? vehicleId = null,
            [Description("Only reminders with these urgencies (NotUrgent, Urgent, VeryUrgent, PastDue); omit for all")] List<ReminderUrgency>? urgencies = null,
            [Description("Space-separated tags; reminders with any of them are returned")] string tags = ""
            )
        {

            var query = new List<string>();
            if (vehicleId.HasValue) query.Add($"vehicleId={vehicleId.Value}");
            foreach (var urgency in urgencies ?? new List<ReminderUrgency>()) query.Add($"urgencies={urgency}");
            if (!string.IsNullOrWhiteSpace(tags)) query.Add($"tags={Uri.EscapeDataString(tags)}");
            string route = vehicleId.HasValue ? "/api/vehicle/reminders" : "/api/vehicle/reminders/all";
            string endpoint = $"{instance}{route}" + (query.Any() ? "?" + string.Join("&", query) : string.Empty);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is - same reasoning as GetOdometerRecords above.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Get Extra Fields for a Record Type")]
        public async Task<string> GetExtraFields(
            [Description("Record type")] ImportMode importMode
            )
        {

            string endpoint = $"{instance}/api/extrafields";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddAuthHeaders(request);
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadFromJsonAsync<List<ExtraFieldsVM>>();
                result?.RemoveAll(x => x.RecordType != importMode.ToString());
                var serializedResult = JsonSerializer.Serialize(result);
                return serializedResult;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        private void AddAuthHeaders(HttpRequestMessage request)
        {
            //check if we have headers
            if (_httpContextAccessor.HttpContext?.Request.Headers.TryGetValue("Authorization", out var authHeader) ?? false)
            {
                request.Headers.Add("Authorization", authHeader.FirstOrDefault());
            }
            else if (_httpContextAccessor.HttpContext?.Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader) ?? false)
            {
                request.Headers.Add("x-api-key", apiKeyHeader.FirstOrDefault());
            }
            else if (_httpContextAccessor.HttpContext?.Request.Query.TryGetValue("apiKey", out var apiKey) ?? false)
            {
                request.Headers.Add("x-api-key", apiKey.FirstOrDefault());
            }
            else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                var authenticationString = $"{username}:{password}";
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));
                request.Headers.Add("Authorization", "Basic " + base64EncodedAuthenticationString);
            }
        }
    }
}
