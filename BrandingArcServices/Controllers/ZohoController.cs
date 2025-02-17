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
public class ZohoController : ControllerBase
{
    
    private readonly ZohoService _zohoService;

    public ZohoController(ZohoService zohoService)
    {
        _zohoService = zohoService;
    }

    [HttpGet("cases")]
    public async Task<IActionResult> GetCasesByDays()
    {
        var cases = await _zohoService.GetZohoCasesByDaysAsync();
        return Ok(cases);
    }

    [HttpGet("deliverables/published")]
    public async Task<IActionResult> GetDeliverablesPublished()
    {
        var cases = await _zohoService.GetDeliverablesPublishedAsync();
        return Ok(cases);
    }

    [HttpGet("deliverables/duedate")]
    public async Task<IActionResult> GetDeliverablesDueDate()
    {
        var cases = await _zohoService.GetDeliverablesDueDateAsync();
        return Ok(cases);
    }

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