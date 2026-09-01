using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Organizations.Application.CreateOrganization;
using SecureFlow.Modules.Organizations.Application.GetOrganization;
using SecureFlow.Modules.Organizations.Application.ListOrganizations;
using SecureFlow.Modules.Organizations.Presentation.Contracts;

namespace SecureFlow.Modules.Organizations.Presentation.Controllers;

[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public sealed class OrganizationsController(
    CreateOrganizationHandler createOrganizationHandler,
    ListOrganizationsHandler listOrganizationsHandler,
    GetOrganizationHandler getOrganizationHandler,
    IValidator<CreateOrganizationRequest> createOrganizationValidator) : ControllerBase
{
    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await this.ValidateAsync(createOrganizationValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await createOrganizationHandler.HandleAsync(
            new CreateOrganizationCommand(currentUserId, request.Name, request.LogoUrl),
            cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                title: result.Error.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        var response = OrganizationResponse.From(result.Value);
        return CreatedAtAction(nameof(Get), new { organizationId = response.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationResponse>>> List(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var organizations = await listOrganizationsHandler.HandleAsync(currentUserId, cancellationToken);
        return Ok(organizations.Select(OrganizationResponse.From).ToArray());
    }

    [HttpGet("{organizationId:guid}")]
    public async Task<ActionResult<OrganizationResponse>> Get(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var organization = await getOrganizationHandler.HandleAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        // IDOR: üye olunmayan tenant için kaynağın varlığı açıklanmaz.
        return organization is null
            ? NotFound()
            : Ok(OrganizationResponse.From(organization));
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
