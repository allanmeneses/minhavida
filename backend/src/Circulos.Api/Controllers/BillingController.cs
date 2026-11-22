using System.Text.Json;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
public sealed class BillingController(AppDbContext db, BillingService billing) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("api/plans")]
    public ActionResult<PlanCatalogResponse> Catalog() => Ok(billing.Catalog());

    [Authorize]
    [HttpPost("api/plan/checkout")]
    public async Task<ActionResult<PlanResponse>> Checkout(
        [FromBody] CheckoutRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        var result = await billing.SubscribeAsync(user, request?.Period, request?.CardToken, cancellationToken);
        if (result.Error is not null)
        {
            return StatusCode(result.Status, new ErrorResponse(result.Error));
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("api/plan/cancel")]
    public async Task<ActionResult<PlanResponse>> Cancel(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        var result = await billing.CancelAsync(user, cancellationToken);
        if (result.Error is not null)
        {
            return StatusCode(result.Status, new ErrorResponse(result.Error));
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("webhooks/mercadopago")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        JsonDocument document;
        try
        {
            document = string.IsNullOrWhiteSpace(raw) ? JsonDocument.Parse("{}") : JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return BadRequest(new ErrorResponse("Corpo do webhook invalido."));
        }

        using (document)
        {
            var type = Request.Query["type"].FirstOrDefault()
                ?? Request.Query["topic"].FirstOrDefault()
                ?? ReadString(document.RootElement, "type")
                ?? ReadString(document.RootElement, "topic")
                ?? string.Empty;
            var dataId = Request.Query["data.id"].FirstOrDefault()
                ?? ReadNestedId(document.RootElement)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dataId))
            {
                return BadRequest(new ErrorResponse("Webhook sem identificador."));
            }

            var signature = Request.Headers["x-signature"].FirstOrDefault();
            var requestId = Request.Headers["x-request-id"].FirstOrDefault();
            if (!billing.SignatureValid(signature, requestId, dataId))
            {
                return Unauthorized(new ErrorResponse("Webhook com assinatura invalida."));
            }

            var topic = string.IsNullOrWhiteSpace(type) ? "subscription_preapproval" : type;
            var result = await billing.ApplyWebhookAsync(topic, dataId, cancellationToken);
            return Ok(new { result });
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadNestedId(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
        {
            return id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
        }

        return null;
    }
}
