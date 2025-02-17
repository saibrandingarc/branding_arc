using Microsoft.AspNetCore.Mvc;
using BrandingArcServices.Models;
using BrandingArcServices.Services;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using System.Text.Json;

namespace BrandingArcServices.Controllers;

[ApiController]
[Route("[controller]")]
public class SocialMediaController : ControllerBase
{
    private readonly SocialService _socialService;

    public SocialMediaController(SocialService socialService)
    {
        _socialService = socialService;
    }

    [HttpGet("linkedinAuthCode")]
    public string GetLinkedinAuthCode()
    {
        var url = _socialService.GetLinkedinAuthCode();
        return url;
    }

    [HttpGet("linkedinAccessToken/{AuthCodeUrl}")]
    public async Task<IActionResult> GetLinkedinAccessToken(string AuthCodeUrl)
    {
        var url = await _socialService.GetLinkedinAccessToken(AuthCodeUrl);
        return Ok(url);
    }

    [HttpGet("linkedinOrganizations")]
    public async Task<IActionResult> GetLinkedinOrganizations()
    {
        var url = await _socialService.GetLinkedinOrganizations();
        return Ok(url);
    }

    //[HttpGet("deliverables/published")]
    //public async Task<IActionResult> GetDeliverablesPublished()
    //{
    //    var cases = await _socialService.GetDeliverablesPublishedAsync();
    //    return Ok(cases);
    //}

    //[HttpGet("deliverables/duedate")]
    //public async Task<IActionResult> GetDeliverablesDueDate()
    //{
    //    var cases = await _socialService.GetDeliverablesDueDateAsync();
    //    return Ok(cases);
    //}

    //[HttpGet("zoho/cases/{companyId}")]
    //public async Task<IActionResult> GetCasesByCompany(string companyId)
    //{
    //    var cases = await _zohoOAuthService.GetZohoCasesByCompanyAsync(companyId);
    //    return Ok(cases);
    //}

    //[HttpGet("zoho/deliverables")]
    //public async Task<IActionResult> GetDeliverables()
    //{
    //    var deliverables = await _zohoService.GetZohoDeliverablesAsync();
    //    return Ok(deliverables);
    //}

    //[HttpGet("zoho/deliverables/{companyId}")]
    //public async Task<IActionResult> GetDeliverables(string companyId)
    //{
    //    var deliverables = await _zohoOAuthService.GetZohoDeliverablesByCompanyAsync(companyId);
    //    return Ok(deliverables);
    //}

    //[HttpGet("zoho/deliverables/{companyId}/{blockId}")]
    //public async Task<IActionResult> GetDeliverablesByBlock(string companyId, string blockId)
    //{
    //    var deliverables = await _zohoOAuthService.GetZohoDeliverablesByCompanyAndBlockAsync(companyId, blockId);
    //    return Ok(deliverables);
    //}

    //[HttpGet("zoho/companies")]
    //public async Task<IActionResult> GetCompanies()
    //{
    //    var deliverables = await _zohoOAuthService.GetZohoCompanies();
    //    return Ok(deliverables);
    //}
}