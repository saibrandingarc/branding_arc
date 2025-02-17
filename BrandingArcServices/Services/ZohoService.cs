using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BrandingArcServices.Models;
using BrandingArcServices.Context;

namespace BrandingArcServices.Services
{
    public class ZohoService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public ZohoService(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        public string GetAuthorizationUrl()
        {
            var clientId = _configuration["Zoho:ClientId"];
            var redirectUri = _configuration["Zoho:RedirectUri"];
            var authEndpoint = _configuration["Zoho:AuthEndpoint"];

            return $"{authEndpoint}?scope=AaaServer.profile.Read,ZohoCRM.modules.ALL&client_id={clientId}&response_type=token&access_type=offline&redirect_uri={redirectUri}";
        }

        // Method to Get Access Token
        public async Task<string> GetAccessTokenAsync()
        {
            var clientId = _configuration["Zoho:ClientId"];
            var clientSecret = _configuration["Zoho:ClientSecret"];
            var tokenEndpoint = _configuration["Zoho:TokenEndpoint"];
            var code = _configuration["Zoho:code"];

            var client = new HttpClient();
            var parameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "authorization_code" }
            };
            try
            {
                var response = await client.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (responseData.ContainsKey("error"))
                    {
                        Console.WriteLine("Error: Missing required key");
                        return "";
                    }
                    else
                    {
                        Console.WriteLine("no errors");
                        var accessToken = await AddSettingsAsync(responseData);
                        return accessToken;
                    }
                }
                else
                {
                    throw new Exception("Failed to regenerate token.");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return ex.Message;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            var settings = await _context.Settings
                        .Where(p => p.tokenFrom == "zoho")
                        .OrderByDescending(p => p.DateAdded)
                        .FirstOrDefaultAsync();

            if (settings == null)
            {
                var tokenObj = await GetAccessTokenAsync();
                if (tokenObj == "")
                {
                    return "Invalid Token";
                }
                else
                {
                    settings = await _context.Settings
                        .Where(p => p.tokenFrom == "zoho")
                        .OrderByDescending(p => p.DateAdded)
                        .FirstOrDefaultAsync();
                }
            }

            // Calculate expiration time
            var expirationTime = settings.DateAdded.AddSeconds(settings.ExpiresIn);
            Console.WriteLine(expirationTime);
            Console.WriteLine(DateTime.UtcNow);
            if (DateTime.UtcNow >= expirationTime)
            {
                // Token has expired, regenerate using refresh token
                var newToken = await RegenerateTokenAsync(settings.RefreshToken);

                // Update the token and date in the settings table
                settings.AccessToken = newToken;
                settings.DateAdded = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return newToken;
            }

            // Token is still valid
            return settings.AccessToken;
        }

        // Method to regenerate the token using the refresh token
        private async Task<string> RegenerateTokenAsync(string refreshToken)
        {
            // Replace with actual API call to Zoho to regenerate the token
            using (var httpClient = new HttpClient())
            {
                var clientId = _configuration["Zoho:ClientId"];
                var clientSecret = _configuration["Zoho:ClientSecret"];
                var tokenEndpoint = _configuration["Zoho:TokenEndpoint"];
                var code = _configuration["Zoho:code"];
                var parameters = new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "refresh_token" }
                };

                try
                {
                    var response = await httpClient.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await _context.Settings
                            .Where(p => p.tokenFrom == "zoho")
                            .OrderByDescending(p => p.DateAdded)
                            .FirstOrDefaultAsync();

                        var json = await response.Content.ReadAsStringAsync();
                        var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                        // Return the new access token
                        return responseData["access_token"];
                    }
                    else
                    {
                        throw new Exception("Failed to regenerate token.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string?> AddSettingsAsync(Dictionary<string, string>? responseData)
        {
            var accessToken = responseData["access_token"];
            var existData = await _context.Settings
                .Where(p => p.tokenFrom == "zoho")
                .OrderByDescending(p => p.DateAdded)
                .FirstOrDefaultAsync();

            if (existData != null)
            {
                existData.DateAdded = DateTime.UtcNow;
                existData.AccessToken = accessToken;

                // Save changes
                await _context.SaveChangesAsync();
            }
            else
            {
                var newSetting = new Settings
                {
                    AccessToken = responseData["access_token"],
                    RefreshToken = responseData["refresh_token"],
                    Scope = responseData["scope"],
                    ExpiresIn = int.Parse(responseData["expires_in"]),
                    DateAdded = DateTime.UtcNow,
                    tokenFrom = "zoho"
                };

                _context.Settings.Add(newSetting);
            }

            await _context.SaveChangesAsync();
            return accessToken;
        }

        public async Task<string> GetZohoCasesByDaysAsync()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    // Get the current date
                    DateTime today = DateTime.Today;

                    // Get the first day of the previous month
                    DateTime startOfPreviousMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);

                    // Get the last day of the previous month
                    DateTime endOfPreviousMonth = startOfPreviousMonth.AddMonths(1).AddDays(-1);


                    var content = new StringContent("{\n    \"select_query\":\"SELECT Case_Number, Case_Closed_Date, Description, Account_Name FROM Cases WHERE Case_Open_Date between '" + startOfPreviousMonth.ToString("yyyy-MM-dd") + "' and '" + endOfPreviousMonth.ToString("yyyy-MM-dd") + "';\"\n}\n", null, "application/json");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    var cases = await response.Content.ReadAsStringAsync();

                    var casesData = JsonConvert.DeserializeObject<dynamic>(cases);
                    var caseList = casesData?.data;

                    foreach (var caseObj in caseList)
                    {
                        // Ensure Account_Name is an object
                        var accountName = caseObj.Account_Name;
                        //string accountNameStr = accountName != null ? accountName.name.ToString() : null;
                        //long? accountId = accountName != null && long.TryParse(accountName.id.ToString(), out long id) ? id : (long?)null;
                        if (accountName != null)
                        {
                            var caseData = new ZohoCasesMonth
                            {
                                Description = caseObj.Description,
                                Case_Number = long.TryParse(caseObj.Case_Number.ToString(), out long caseNumber) ? caseNumber : (long?)null,
                                Account_Name = accountName?.name ?? "Unknown",
                                Account_Id = accountName?.id.ToString(),
                                Case_Id = caseObj.id.ToString(),
                                Case_Closed_Date = DateTime.TryParse(caseObj.Case_Closed_Date.ToString(), out DateTime closedDate) ? closedDate : (DateTime?)null,
                                Created_Date = DateTime.UtcNow,
                                From_Date = startOfPreviousMonth,
                                To_Date = endOfPreviousMonth
                            };
                            Console.WriteLine(caseData);

                            _context.ZohoCasesMonths.Add(caseData);
                        }
                    }
                    var result = await _context.SaveChangesAsync();
                    return caseList;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetDeliverablesPublishedAsync()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    DateTime today = DateTime.Now;
                    Console.WriteLine("Today: " + today.ToString("yyyy-MM-dd"));

                    DateTime previous = today.AddDays(-60);
                    Console.WriteLine("30 Days Before: " + previous.ToString("yyyy-MM-dd"));

                    var content = new StringContent("{\n    \"select_query\":\"SELECT Company, id, Name, Topic_Category, Main_Status, Main_Status1, Sub_Category_YouTube1, Date_Published FROM Deliverables WHERE Date_Published between '" + previous.ToString("yyyy-MM-dd")+"' and '" + today.ToString("yyyy-MM-dd")+ "'\"\n}\n", null, "application/json");
                    //var content = new StringContent("{\n    \"select_query\":\"SELECT Company FROM Deliverables WHERE Date_Published between '"+ today.ToString("yyyy-MM-dd")+ "' and '"+ previous.ToString("yyyy-MM-dd")+"';\"\n}\n", null, "application/json");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    var deliverables = await response.Content.ReadAsStringAsync();

                    var deliverablesData = JsonConvert.DeserializeObject<dynamic>(deliverables);
                    var deliverablesList = deliverablesData?.data;

                    foreach (var deliverablesObj in deliverablesList)
                    {
                        if (deliverablesObj != null)
                        {
                            // Ensure Account_Name is an object
                            var accountName = deliverablesObj.Company;
                            if (accountName != null)
                            {
                                var deliverableData = new ZohoDeliverablesMonth
                                {
                                    Company_Name = accountName?.name ?? "Unknown",
                                    Company_Id = accountName?.id.ToString(),
                                    Deliverables_Id = deliverablesObj.id.ToString(),
                                    Name = deliverablesObj.Name.ToString(),
                                    Topic_Category = deliverablesObj.Topic_Category.ToString(),
                                    Main_Status = deliverablesObj.Main_Status,
                                    Created_Date = DateTime.UtcNow,
                                    From_Date = previous,
                                    To_Date = today,
                                    Date_Published = deliverablesObj.Date_Published,
                                    Main_Status1 = deliverablesObj.Main_Status1,
                                    Sub_Category_YouTube1 = deliverablesObj.Sub_Category_YouTube1
                                };
                                Console.WriteLine(deliverableData);

                                _context.ZohoDeliverablesMonths.Add(deliverableData);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    var result = await _context.SaveChangesAsync();
                    return deliverablesList;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetDeliverablesDueDateAsync()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    DateTime today = DateTime.Now;
                    Console.WriteLine("Today: " + today.ToString("yyyy-MM-dd"));

                    DateTime next = today.AddDays(30);
                    Console.WriteLine("30 Days Before: " + next.ToString("yyyy-MM-dd"));

                    var content = new StringContent("{\n    \"select_query\":\"SELECT id, Company, Due_Date, Name, Topic_Category, Main_Status, Main_Status1, Sub_Category_YouTube1, Date_Published FROM Deliverables WHERE Due_Date between '" + today.ToString("yyyy-MM-dd") + "' and '" + next.ToString("yyyy-MM-dd") + "'\"\n}\n", null, "application/json");
                    //var content = new StringContent("{\n    \"select_query\":\"SELECT Company FROM Deliverables WHERE Date_Published between '"+ today.ToString("yyyy-MM-dd")+ "' and '"+ previous.ToString("yyyy-MM-dd")+"';\"\n}\n", null, "application/json");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    var deliverables = await response.Content.ReadAsStringAsync();

                    var deliverablesData = JsonConvert.DeserializeObject<dynamic>(deliverables);
                    var deliverablesList = deliverablesData?.data;

                    foreach (var deliverablesObj in deliverablesList)
                    {
                        // Ensure Account_Name is an object
                        var accountName = deliverablesObj.Company;
                        if (accountName != null)
                        {
                            var deliverableData = new ZohoFutureDeliverables
                            {
                                Company_Name = accountName?.name ?? "Unknown",
                                Company_Id = accountName?.id.ToString(),
                                Deliverables_Id = deliverablesObj.id.ToString(),
                                Name = deliverablesObj.Name,
                                Topic_Category = deliverablesObj.Topic_Category,
                                Main_Status = deliverablesObj.Main_Status.ToString(),
                                Due_Date = deliverablesObj.Due_Date,
                                Main_Status1 = deliverablesObj.Main_Status1.ToString(),
                                Created_Date = DateTime.UtcNow,
                                From_Date = today,
                                To_Date = next
                            };
                            Console.WriteLine(deliverableData);

                            _context.ZohoFutureDeliverabless.Add(deliverableData);
                        }
                    }
                    var result = await _context.SaveChangesAsync();
                    return deliverablesList;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        //public async Task<string> GetZohoDeliverablesAsync()
        //{
        //    var accessToken = await GetValidTokenAsync();
        //    Console.WriteLine(accessToken);
        //    if (accessToken == "Invalid Token")
        //    {
        //        return null;
        //    }
        //    else
        //    {
        //        try
        //        {
        //            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
        //            {
        //                Headers = { { "Authorization", $"Bearer {accessToken}" } }
        //            };

        //            DateTime today = DateTime.Now;
        //            Console.WriteLine("Today: " + today.ToString("yyyy-MM-dd"));

        //            DateTime next = today.AddDays(60);
        //            Console.WriteLine("60 Days Later: " + next.ToString("yyyy-MM-dd"));

        //            var content = new StringContent("{\n    \"select_query\":\"SELECT COUNT(Main_Status1) AS count, Company, Main_Status1 FROM Deliverables WHERE Date_Published between '"+ today.ToString("yyyy-MM-dd")+ "' and '"+ next.ToString("yyyy-MM-dd")+"' GROUP BY Company, Main_Status1;\"\n}\n", null, "application/json");
        //            request.Content = content;
        //            var response = await _httpClient.SendAsync(request);
        //            response.EnsureSuccessStatusCode();
        //            Console.WriteLine(await response.Content.ReadAsStringAsync());

        //            var cases = await response.Content.ReadAsStringAsync();
        //            return cases;
        //        }
        //        catch (HttpRequestException ex)
        //        {
        //            Console.WriteLine($"Request failed: {ex.Message}");
        //            return ex.Message;
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"An error occurred: {ex.Message}");
        //            return ex.Message;
        //        }
        //    }
        //}
    }
}