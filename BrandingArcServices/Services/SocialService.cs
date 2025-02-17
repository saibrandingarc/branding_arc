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
using System.Web;
using System.Security.Policy;
using Org.BouncyCastle.Asn1.Ocsp;
using SendGrid.Helpers.Mail;

namespace BrandingArcServices.Services
{
    public class SocialService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public SocialService(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        public string GetLinkedinAuthCode()
        {
            var client_id = _configuration["social:client_id"];
            var redirect_uri = _configuration["social:redirect_uri"];
            var scope = _configuration["social:scope"];
            var state = _configuration["social:state"];
            var connection = _configuration["social:connection"];

            var url = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={client_id}&redirect_uri={redirect_uri}&scope={scope}&state={state}&connection={connection}";
            return url;
        }

        // Method to Get Access Token
        public async Task<string> GetLinkedinAccessToken(string AuthCodeUrl)
        {
            var client_id = _configuration["social:client_id"];
            var client_secret = _configuration["social:client_secret"];
            var redirect_uri = _configuration["social:redirect_uri"];
            var scope = _configuration["social:scope"];
            var state = _configuration["social:state"];
            var connection = _configuration["social:connection"];
            var grant_type = _configuration["social:grant_type"];
            var AccessTokenUrl = _configuration["social:AccessTokenUrl"];

            // Extract the query string part of the URL
            var query = new Uri(AuthCodeUrl).Query;

            // Parse the query string into a NameValueCollection
            var queryParams = HttpUtility.ParseQueryString(query);

            // Get the value of the 'code' parameter
            string code = queryParams["code"];
            Console.WriteLine($"Code: {code}");

            var client = new HttpClient();
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", grant_type },
                { "code", code },
                { "client_id", client_id },
                { "client_secret", client_secret },
                { "redirect_uri", redirect_uri }
            };
            var queryString = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

            // Construct the full URL
            var fullUrl = $"{AccessTokenUrl}?{queryString}";
            try
            {
                var response = await client.GetAsync(fullUrl);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(json);
                    var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (responseData.ContainsKey("error"))
                    {
                        Console.WriteLine("Error: Missing required key");
                        return "";
                    }
                    else
                    {
                        Console.WriteLine("no errors");
                        var accessToken = await AddSettingsAsync(responseData, "linkedin");
                        //return accessToken;
                        return System.Text.Json.JsonSerializer.Serialize(accessToken);
                    }
                }
                else
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(json);
                    return System.Text.Json.JsonSerializer.Serialize(json);
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

        // Method to Get Access Token
        public async Task<string> GetLinkedinOrganizations()
        {
            var accessToken = await GetValidLinkedInTokenAsync();
            Console.WriteLine(accessToken);
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/organizationalEntityAcls?q=roleAssignee&projection=(elements*(organizationalTarget~))");
            request.Headers.Add("Authorization", "Bearer "+ accessToken);

            try
            {
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    // Parse JSON
                    JObject jsonResponse = JObject.Parse(json);
                    Console.WriteLine(jsonResponse);
                    // Iterate over JSON elements
                    foreach (JObject element in jsonResponse["elements"])
                    {
                        Console.WriteLine($"ID: {element}");
                        if (element is JObject elementObj)
                        {
                            Console.WriteLine($"Full Element: {elementObj}");

                            // Extract properties safely
                            string orgId = elementObj["organizationalTarget"]?.ToString();
                            Console.WriteLine(orgId);
                            string orgName = elementObj["organizationalTarget~"]?["localizedName"]?.ToString();
                            string localizedWebsite = elementObj["organizationalTarget~"]?["localizedWebsite"]?.ToString();
                            string id = elementObj["organizationalTarget~"]?["id"]?.ToString();
                            Console.WriteLine(orgName);
                            string publicurl = getDomainName(localizedWebsite);
                            var clientData = await _context.ClientDetails
                                            .ToListAsync();
                            Console.WriteLine(clientData);
                        }
                    }
                    return System.Text.Json.JsonSerializer.Serialize(response);
                }
                else
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(json);
                    return System.Text.Json.JsonSerializer.Serialize(json);
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

        public async Task<string> GetValidLinkedInTokenAsync()
        {
            var settings = await _context.Settings
                        .Where(p => p.tokenFrom == "linkedin")
                        .OrderByDescending(p => p.DateAdded)
                        .FirstOrDefaultAsync();

            if (settings == null)
            {
                return "Invalid Token";
            }

            // Calculate expiration time
            var expirationTime = settings.DateAdded.AddSeconds(settings.ExpiresIn);
            Console.WriteLine(expirationTime);
            Console.WriteLine(DateTime.UtcNow);
            if (DateTime.UtcNow >= expirationTime)
            {
                // Token has expired, regenerate using refresh token
                var newToken = await RegenerateLinkedInTokenAsync(settings.RefreshToken);

                // Update the token and date in the settings table
                settings.AccessToken = newToken;
                settings.DateAdded = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return newToken;
            }

            // Token is still valid
            return settings.AccessToken;
        }

        //// Method to regenerate the token using the refresh token
        private async Task<string> RegenerateLinkedInTokenAsync(string refreshToken)
        {
            // Replace with actual API call to Zoho to regenerate the token
            using (var httpClient = new HttpClient())
            {
                var client_id = _configuration["social:client_id"];
                var client_secret = _configuration["social:client_secret"];
                var AccessTokenUrl = _configuration["social:AccessTokenUrl"];

                var parameters = new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "client_id", client_id },
                    { "client_secret", client_secret },
                    { "grant_type", "refresh_token" }
                };

                try
                {
                    var response = await httpClient.PostAsync(AccessTokenUrl, new FormUrlEncodedContent(parameters));

                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await _context.Settings
                            .Where(p => p.tokenFrom == "linkedin")
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

        public async Task<string?> AddSettingsAsync(Dictionary<string, string>? responseData, string tokenFrom)
        {
            var accessToken = responseData["access_token"];
            var existData = await _context.Settings
                .Where(p => p.tokenFrom == tokenFrom)
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
                    tokenFrom = tokenFrom
                };

                _context.Settings.Add(newSetting);
            }

            await _context.SaveChangesAsync();
            return accessToken;
        }

        //public async Task<string> GetZohoCasesByDaysAsync()
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
        //            // Get the current date
        //            DateTime today = DateTime.Today;

        //            // Get the first day of the previous month
        //            DateTime startOfPreviousMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);

        //            // Get the last day of the previous month
        //            DateTime endOfPreviousMonth = startOfPreviousMonth.AddMonths(1).AddDays(-1);


        //            var content = new StringContent("{\n    \"select_query\":\"SELECT Case_Number, Case_Closed_Date, Description, Account_Name FROM Cases WHERE Case_Open_Date between '" + startOfPreviousMonth.ToString("yyyy-MM-dd") + "' and '" + endOfPreviousMonth.ToString("yyyy-MM-dd") + "';\"\n}\n", null, "application/json");
        //            request.Content = content;
        //            var response = await _httpClient.SendAsync(request);
        //            response.EnsureSuccessStatusCode();
        //            Console.WriteLine(await response.Content.ReadAsStringAsync());

        //            var cases = await response.Content.ReadAsStringAsync();

        //            var casesData = JsonConvert.DeserializeObject<dynamic>(cases);
        //            var caseList = casesData?.data;

        //            foreach (var caseObj in caseList)
        //            {
        //                // Ensure Account_Name is an object
        //                var accountName = caseObj.Account_Name;
        //                //string accountNameStr = accountName != null ? accountName.name.ToString() : null;
        //                //long? accountId = accountName != null && long.TryParse(accountName.id.ToString(), out long id) ? id : (long?)null;
        //                if (accountName != null)
        //                {
        //                    var caseData = new ZohoCasesMonth
        //                    {
        //                        Description = caseObj.Description,
        //                        Case_Number = long.TryParse(caseObj.Case_Number.ToString(), out long caseNumber) ? caseNumber : (long?)null,
        //                        Account_Name = accountName?.name ?? "Unknown",
        //                        Account_Id = accountName?.id.ToString(),
        //                        Case_Id = caseObj.id.ToString(),
        //                        Case_Closed_Date = DateTime.TryParse(caseObj.Case_Closed_Date.ToString(), out DateTime closedDate) ? closedDate : (DateTime?)null,
        //                        Created_Date = DateTime.UtcNow,
        //                        From_Date = startOfPreviousMonth,
        //                        To_Date = endOfPreviousMonth
        //                    };
        //                    Console.WriteLine(caseData);

        //                    _context.ZohoCasesMonths.Add(caseData);
        //                }
        //            }
        //            var result = await _context.SaveChangesAsync();
        //            return caseList;
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

        //public async Task<string> GetDeliverablesPublishedAsync()
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

        //            DateTime previous = today.AddDays(-60);
        //            Console.WriteLine("30 Days Before: " + previous.ToString("yyyy-MM-dd"));

        //            var content = new StringContent("{\n    \"select_query\":\"SELECT Company, Main_Status1, Main_Status, Sub_Category_YouTube1, Date_Published FROM Deliverables WHERE Date_Published between '" + previous.ToString("yyyy-MM-dd")+"' and '" + today.ToString("yyyy-MM-dd")+ "'\"\n}\n", null, "application/json");
        //            //var content = new StringContent("{\n    \"select_query\":\"SELECT Company FROM Deliverables WHERE Date_Published between '"+ today.ToString("yyyy-MM-dd")+ "' and '"+ previous.ToString("yyyy-MM-dd")+"';\"\n}\n", null, "application/json");
        //            request.Content = content;
        //            var response = await _httpClient.SendAsync(request);
        //            response.EnsureSuccessStatusCode();
        //            Console.WriteLine(await response.Content.ReadAsStringAsync());

        //            var deliverables = await response.Content.ReadAsStringAsync();

        //            var deliverablesData = JsonConvert.DeserializeObject<dynamic>(deliverables);
        //            var deliverablesList = deliverablesData?.data;

        //            foreach (var deliverablesObj in deliverablesList)
        //            {
        //                // Ensure Account_Name is an object
        //                var accountName = deliverablesObj.Company;
        //                if (accountName != null)
        //                {
        //                    var deliverableData = new ZohoDeliverablesMonth
        //                    {
        //                        Main_Status1 = deliverablesObj.Main_Status1,
        //                        Company_Name = accountName?.name ?? "Unknown",
        //                        Company_Id = accountName?.id.ToString(),
        //                        Main_Status = deliverablesObj.Main_Status,
        //                        Deliverables_Id = deliverablesObj.id.ToString(),
        //                        Date_Published = deliverablesObj.Date_Published,
        //                        Sub_Category_YouTube1 = deliverablesObj.Sub_Category_YouTube1,
        //                        Created_Date = DateTime.UtcNow,
        //                        From_Date = previous,
        //                        To_Date = today
        //                    };
        //                    Console.WriteLine(deliverableData);

        //                    _context.ZohoDeliverablesMonths.Add(deliverableData);
        //                    await _context.SaveChangesAsync();
        //                }
        //            }
        //            var result = await _context.SaveChangesAsync();
        //            return deliverablesList;
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

        //public async Task<string> GetDeliverablesDueDateAsync()
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

        //            DateTime next = today.AddDays(30);
        //            Console.WriteLine("30 Days Before: " + next.ToString("yyyy-MM-dd"));

        //            var content = new StringContent("{\n    \"select_query\":\"SELECT id, Company, Due_Date, Name FROM Deliverables WHERE Due_Date between '" + today.ToString("yyyy-MM-dd") + "' and '" + next.ToString("yyyy-MM-dd") + "'\"\n}\n", null, "application/json");
        //            //var content = new StringContent("{\n    \"select_query\":\"SELECT Company FROM Deliverables WHERE Date_Published between '"+ today.ToString("yyyy-MM-dd")+ "' and '"+ previous.ToString("yyyy-MM-dd")+"';\"\n}\n", null, "application/json");
        //            request.Content = content;
        //            var response = await _httpClient.SendAsync(request);
        //            response.EnsureSuccessStatusCode();
        //            Console.WriteLine(await response.Content.ReadAsStringAsync());

        //            var deliverables = await response.Content.ReadAsStringAsync();

        //            var deliverablesData = JsonConvert.DeserializeObject<dynamic>(deliverables);
        //            var deliverablesList = deliverablesData?.data;

        //            foreach (var deliverablesObj in deliverablesList)
        //            {
        //                // Ensure Account_Name is an object
        //                var accountName = deliverablesObj.Company;
        //                if (accountName != null)
        //                {
        //                    var deliverableData = new ZohoFutureDeliverables
        //                    {
        //                        Due_Date = deliverablesObj.Due_Date,
        //                        Name = deliverablesObj.Name,
        //                        Company_Name = accountName?.name ?? "Unknown",
        //                        Company_Id = accountName?.id.ToString(),
        //                        Main_Status1 = deliverablesObj.Main_Status1.ToString(),
        //                        Deliverables_Id = deliverablesObj.id.ToString(),
        //                        Created_Date = DateTime.UtcNow,
        //                        From_Date = today,
        //                        To_Date = next
        //                    };
        //                    Console.WriteLine(deliverableData);

        //                    _context.ZohoFutureDeliverabless.Add(deliverableData);
        //                }
        //            }
        //            var result = await _context.SaveChangesAsync();
        //            return deliverablesList;
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

        public string getDomainName(string website)
        {
            // Ensure it's a valid URL format
            if (!website.StartsWith("http"))
            {
                website = "https://" + website; // Add scheme if missing
            }

            Uri uri = new Uri(website);
            string host = uri.Host; // Get full domain

            // Remove "www." if present
            if (host.StartsWith("www."))
            {
                host = host.Substring(4);
            }

            Console.WriteLine($"Domain Name: {host}");
            return host;
        }
    }
}