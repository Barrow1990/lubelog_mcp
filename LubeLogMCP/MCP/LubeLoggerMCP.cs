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
                if (request.Headers.Contains("Authorization"))
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
        [McpServerTool, Description("Updates an existing fuel record. This REPLACES the whole record - fields you " +
            "leave out are cleared, not left unchanged. Read the record first with GetFuelRecords and resend its " +
            "notes/tags/extraFields alongside whatever you're actually changing, unless you mean to clear them.")]
        public async Task<string> UpdateFuelRecord(
            [Description("id of the record to update, from GetFuelRecords")] int recordId,
            [Description("Date of fuel up")] DateTime date,
            [Description("Odometer at time of fuel up")] int odometer,
            [Description("Volume of gas pumped")] decimal volume,
            [Description("Total cost of fuel up")] decimal cost,
            [Description("Is fueled up completely")] bool fillToFull,
            [Description("Any missed fuel ups")] bool missedFuelUp,
            [Description("Any extra fields configured for gasrecord")] List<ExtraField> extraFields,
            [Description("State of Charge at beginning of charge session if an electric vehicle")] int startingSoc = 20,
            [Description("State of Charge at end of charge session if an electric vehicle")] int endingSoc = 80,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                FuelConsumed = volume,
                Cost = cost,
                IsFillToFull = fillToFull,
                MissedFuelUp = missedFuelUp,
                StartingSoc = startingSoc,
                EndingSoc = endingSoc,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/gasrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a fuel record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteFuelRecord(
            [Description("id of the record to delete, from GetFuelRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/gasrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteFuelRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/gasrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Updates an existing service record. This REPLACES the whole record - fields you " +
            "leave out are cleared, not left unchanged. Read the record first with GetServiceRecords and resend its " +
            "notes/tags/extraFields alongside whatever you're actually changing, unless you mean to clear them.")]
        public async Task<string> UpdateServiceRecord(
            [Description("id of the record to update, from GetServiceRecords")] int recordId,
            [Description("Date serviced")] DateTime date,
            [Description("Odometer at time of service")] int odometer,
            [Description("Description of items serviced")] string description,
            [Description("Total cost of the service")] decimal cost,
            [Description("Any extra fields configured for servicerecord")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/servicerecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a service record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteServiceRecord(
            [Description("id of the record to delete, from GetServiceRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/servicerecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteServiceRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/servicerecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Updates an existing repair record. This REPLACES the whole record - fields you " +
            "leave out are cleared, not left unchanged. Read the record first with GetRepairRecords and resend its " +
            "notes/tags/extraFields alongside whatever you're actually changing, unless you mean to clear them.")]
        public async Task<string> UpdateRepairRecord(
            [Description("id of the record to update, from GetRepairRecords")] int recordId,
            [Description("Date")] DateTime date,
            [Description("Odometer at time of repair record")] int odometer,
            [Description("Description")] string description,
            [Description("Total cost")] decimal cost,
            [Description("Any extra fields configured for this record type")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/repairrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a repair record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteRepairRecord(
            [Description("id of the record to delete, from GetRepairRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/repairrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteRepairRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/repairrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Updates an existing upgrade record. This REPLACES the whole record - fields you " +
            "leave out are cleared, not left unchanged. Read the record first with GetUpgradeRecords and resend its " +
            "notes/tags/extraFields alongside whatever you're actually changing, unless you mean to clear them.")]
        public async Task<string> UpdateUpgradeRecord(
            [Description("id of the record to update, from GetUpgradeRecords")] int recordId,
            [Description("Date")] DateTime date,
            [Description("Odometer at time of upgrade record")] int odometer,
            [Description("Description")] string description,
            [Description("Total cost")] decimal cost,
            [Description("Any extra fields configured for this record type")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Odometer = odometer,
                Description = description,
                Cost = cost,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/upgraderecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a upgrade record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteUpgradeRecord(
            [Description("id of the record to delete, from GetUpgradeRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/upgraderecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteUpgradeRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/upgraderecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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

            string endpoint = $"{instance}/api/vehicle/odometerrecords/add?vehicleId={vehicleId}";

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
        [McpServerTool, Description("Updates an existing odometer record. This REPLACES the whole record - fields " +
            "you leave out are cleared, not left unchanged. Read the record first with GetOdometerRecords.")]
        public async Task<string> UpdateOdometerRecord(
            [Description("id of the record to update, from GetOdometerRecords")] int recordId,
            [Description("Date recorded")] DateTime date,
            [Description("Odometer reading immediately before this one, for distance-travelled calculations")] int initialOdometer,
            [Description("Odometer recorded")] int odometer,
            [Description("Any extra fields configured for odometerrecord")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                InitialOdometer = initialOdometer,
                Odometer = odometer,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/odometerrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a odometer record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteOdometerRecord(
            [Description("id of the record to delete, from GetOdometerRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/odometerrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteOdometerRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/odometerrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Updates an existing supply record (including shop supplies). This REPLACES the " +
            "whole record - fields you leave out are cleared, not left unchanged. Read the record first with " +
            "GetSupplyRecords and resend its notes/tags/extraFields alongside whatever you're actually changing.")]
        public async Task<string> UpdateSupplyRecord(
            [Description("id of the record to update, from GetSupplyRecords")] int recordId,
            [Description("Date purchased")] DateTime date,
            [Description("Description of the supply")] string description,
            [Description("Quantity purchased")] decimal quantity,
            [Description("Cost of the supply")] decimal cost,
            [Description("Part number")] string partNumber,
            [Description("Part supplier")] string partSupplier,
            [Description("Any extra fields configured for supplyrecord")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Description = description,
                PartQuantity = quantity,
                Cost = cost,
                PartNumber = partNumber,
                PartSupplier = partSupplier,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/supplyrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a supply record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteSupplyRecord(
            [Description("id of the record to delete, from GetSupplyRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/supplyrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteSupplyRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/supplyrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Updates an existing maintenance reminder. This REPLACES the whole record - " +
            "fields you leave out are cleared, not left unchanged. Read it first with GetReminders.")]
        public async Task<string> UpdateReminderRecord(
            [Description("id of the reminder to update, from GetReminders")] int recordId,
            [Description("What the reminder is for")] string description,
            [Description("Date: due on a date only. Odometer: due at a reading only. Both: due whichever of dueDate/dueOdometer comes first")] ReminderMetric metric,
            [Description("Due date. Required unless metric is Odometer")] DateTime? dueDate,
            [Description("Due odometer reading. Required unless metric is Date")] int? dueOdometer,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Description = description,
                Metric = metric.ToString(),
                DueDate = dueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                DueOdometer = dueOdometer,
                Notes = notes,
                Tags = tags
            };

            string endpoint = $"{instance}/api/vehicle/reminders/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a maintenance reminder. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteReminderRecord(
            [Description("id of the record to delete, from GetReminders")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/reminders/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteReminderRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/reminders/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Adds an equipment record to a vehicle (e.g. winter tyres, a roof box) - whether " +
            "it is currently fitted or just owned.")]
        public async Task<string> AddEquipmentRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Description of the equipment")] string description,
            [Description("Is this equipment currently fitted to the vehicle")] bool isEquipped,
            [Description("Any extra fields configured for equipmentrecord")] List<ExtraField> extraFields,
            [Description("Notes")] string notes = "",
            [Description("Space-separated tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Description = description,
                IsEquipped = isEquipped,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/equipmentrecords/add?vehicleId={vehicleId}";

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
        [McpServerTool, Description("Updates an existing equipment record. This REPLACES the whole record - fields " +
            "you leave out are cleared, not left unchanged. Read it first with GetEquippedEquipment (or LubeLogger's " +
            "UI for unequipped items).")]
        public async Task<string> UpdateEquipmentRecord(
            [Description("id of the record to update")] int recordId,
            [Description("Description of the equipment")] string description,
            [Description("Is this equipment currently fitted to the vehicle")] bool isEquipped,
            [Description("Any extra fields configured for equipmentrecord")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Description = description,
                IsEquipped = isEquipped,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/equipmentrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a equipment record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteEquipmentRecord(
            [Description("id of the record to delete, from GetEquippedEquipment")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/equipmentrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteEquipmentRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/equipmentrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Adds a planned future record (e.g. a service to schedule) to a vehicle.")]
        public async Task<string> AddPlanRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Description")] string description,
            [Description("Estimated cost")] decimal cost,
            [Description("What kind of record this will become once completed")] PlanType type,
            [Description("Priority")] PlanPriority priority,
            [Description("Progress")] PlanProgress progress,
            [Description("Any extra fields configured for planrecord")] List<ExtraField> extraFields,
            [Description("Notes")] string notes = "")
        {
            var requestData = new PostRequestModel
            {
                Description = description,
                Cost = cost,
                Type = type.ToString(),
                Priority = priority.ToString(),
                Progress = progress.ToString(),
                Notes = notes
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/planrecords/add?vehicleId={vehicleId}";

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
        [McpServerTool, Description("Updates an existing planned record. This REPLACES the whole record - fields " +
            "you leave out are cleared, not left unchanged. Read it first with GetPlanRecords.")]
        public async Task<string> UpdatePlanRecord(
            [Description("id of the record to update, from GetPlanRecords")] int recordId,
            [Description("Description")] string description,
            [Description("Estimated cost")] decimal cost,
            [Description("What kind of record this will become once completed")] PlanType type,
            [Description("Priority")] PlanPriority priority,
            [Description("Progress")] PlanProgress progress,
            [Description("Any extra fields configured for planrecord")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Description = description,
                Cost = cost,
                Type = type.ToString(),
                Priority = priority.ToString(),
                Progress = progress.ToString(),
                Notes = notes
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/planrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a planned record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeletePlanRecord(
            [Description("id of the record to delete, from GetPlanRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/planrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeletePlanRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/planrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Adds a note to a vehicle.")]
        public async Task<string> AddNoteRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Short description/title of the note")] string description,
            [Description("The note's own text")] string noteText,
            [Description("Pin this note to the top")] bool pinned,
            [Description("Any extra fields configured for noterecord")] List<ExtraField> extraFields,
            [Description("Space-separated tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Description = description,
                NoteText = noteText,
                Pinned = pinned,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/notes/add?vehicleId={vehicleId}";

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
        [McpServerTool, Description("Updates an existing note. This REPLACES the whole note - fields you leave out " +
            "are cleared, not left unchanged. Read it first with GetNotes.")]
        public async Task<string> UpdateNoteRecord(
            [Description("id of the note to update, from GetNotes")] int recordId,
            [Description("Short description/title of the note")] string description,
            [Description("The note's own text")] string noteText,
            [Description("Pin this note to the top")] bool pinned,
            [Description("Any extra fields configured for noterecord")] List<ExtraField> extraFields,
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Description = description,
                NoteText = noteText,
                Pinned = pinned,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/notes/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a note. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteNoteRecord(
            [Description("id of the record to delete, from GetNotes")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/notes/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteNoteRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/notes/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Adds a tax record (e.g. registration, road tax, a loan payment).")]
        public async Task<string> AddTaxRecord(
            [Description("id of the vehicle")] int vehicleId,
            [Description("Date")] DateTime date,
            [Description("Description")] string description,
            [Description("Total cost")] decimal cost,
            [Description("Any extra fields configured for taxrecord")] List<ExtraField> extraFields)
        {
            var requestData = new PostRequestModel
            {
                Date = date.ToString("yyyy-MM-dd"),
                Description = description,
                Cost = cost
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/taxrecords/add?vehicleId={vehicleId}";

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
        [McpServerTool, Description("Updates an existing tax record. This REPLACES the whole record - fields you " +
            "leave out are cleared, not left unchanged. Read the record first with GetTaxRecords and resend its " +
            "notes/tags/extraFields alongside whatever you're actually changing, unless you mean to clear them.")]
        public async Task<string> UpdateTaxRecord(
            [Description("id of the record to update, from GetTaxRecords")] int recordId,
            [Description("Date")] DateTime date,
            [Description("Description")] string description,
            [Description("Total cost")] decimal cost,
            [Description("Any extra fields configured for this record type")] List<ExtraField> extraFields,
            [Description("Notes; omitting this clears any existing notes")] string notes = "",
            [Description("Space-separated tags; omitting this clears any existing tags")] string tags = "")
        {
            var requestData = new PostRequestModel
            {
                Id = recordId,
                Date = date.ToString("yyyy-MM-dd"),
                Description = description,
                Cost = cost,
                Notes = notes,
                Tags = tags
            };

            for (int i = 0; i < extraFields.Count; i++)
            {
                requestData.ExtraFields.Add(new ExtraFieldPostModel { Name = extraFields[i].Name, Value = extraFields[i].Value });
            }

            string endpoint = $"{instance}/api/vehicle/taxrecords/update";

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
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
        [McpServerTool, Description("Deletes a tax record. Needs a Manager-tier API key (an Editor-tier key is " +
            "refused by LubeLogger itself). Without confirm=true this only shows the record that would be deleted and " +
            "changes nothing - call it again with confirm=true, after telling the user what will be deleted, to actually " +
            "delete it. There is no undo.")]
        public async Task<string> DeleteTaxRecord(
            [Description("id of the record to delete, from GetTaxRecords")] int recordId,
            [Description("Must be true to actually delete; otherwise this only previews the record")] bool confirm = false)
        {
            if (!confirm)
            {
                string previewEndpoint = $"{instance}/api/vehicle/taxrecords/all?id={recordId}";
                var previewRequest = new HttpRequestMessage(HttpMethod.Get, previewEndpoint);
                previewRequest.Headers.Add("culture-invariant", "true");
                AddAuthHeaders(previewRequest);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var preview = await httpClient.SendAsync(previewRequest).Result.Content.ReadAsStringAsync();
                    return "Not deleted - this is a preview. Call DeleteTaxRecord again with confirm=true to actually " +
                        "delete it; there is no undo. Record: " + preview;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            string endpoint = $"{instance}/api/vehicle/taxrecords/delete?id={recordId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
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
        [McpServerTool, Description("Gets odometer history for a vehicle, oldest first. Use this plus the current date to work out average distance per day and project forward to any future mileage.")]
        public async Task<string> GetOdometerRecords(
            [Description("id of the vehicle")] int vehicleId
            )
        {

            string endpoint = $"{instance}/api/vehicle/odometerrecords?vehicleId={vehicleId}";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("culture-invariant", "true");
            AddAuthHeaders(request);
            try
            {
                // Passed through as-is, like GetLatestOdometer below: lubelog's own export DTO for
                // this route types id/odometer as string with a lenient (string-or-number) converter
                // for *reading* an import payload, but what it actually writes back on GET is a bare
                // JSON number for those fields — a strongly-typed model here would only be one lubelog
                // release away from breaking again. Records come back oldest-first already; if that
                // ever isn't true for your data, sort on the "date" field.
                var httpClient = _httpClientFactory.CreateClient();
                var result = await httpClient.SendAsync(request).Result.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        [McpServerTool, Description("Gets maintenance reminders for a vehicle: what is due, on which metric (Date, Odometer, or Both), the due date/odometer, and lubelog's own urgency and days/distance-remaining countdown as of now.")]
        public async Task<string> GetReminders(
            [Description("id of the vehicle")] int vehicleId
            )
        {

            string endpoint = $"{instance}/api/vehicle/reminders?vehicleId={vehicleId}";

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
