using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;

namespace MomCare.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalogService;

    public ServicesController(IServiceCatalogService serviceCatalogService)
    {
        _serviceCatalogService = serviceCatalogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Browse([FromQuery] bool? isActive, [FromQuery] string? search)
    {
        var language = Request.Headers.AcceptLanguage.FirstOrDefault();
        var services = await _serviceCatalogService.BrowseAsync(isActive, search, language);
        return Ok(services);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDetail(int id)
    {
        var language = Request.Headers.AcceptLanguage.FirstOrDefault();
        var service = await _serviceCatalogService.GetByIdAsync(id, language);
        if (service == null)
        {
            return NotFound();
        }

        return Ok(service);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] UpsertServiceDto dto)
    {
        var created = await _serviceCatalogService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetDetail), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertServiceDto dto)
    {
        var ok = await _serviceCatalogService.UpdateAsync(id, dto);
        if (!ok)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _serviceCatalogService.DeleteAsync(id);
        if (!ok)
        {
            return NotFound();
        }

        return NoContent();
    }
}
