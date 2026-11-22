using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ListsController(AppDbContext db) : ControllerBase
{
    [HttpGet("lists")]
    public async Task<ActionResult<IReadOnlyList<SharedListSummaryResponse>>> List(
        [FromQuery] Guid? circleId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (circleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            if (circle is null || HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            var circleLists = await db.SharedLists.AsNoTracking()
                .Include(x => x.Items)
                .Where(x => x.CircleId == cid)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
            return Ok(circleLists
                .Where(x => !HideWishList(circle, userId, x.Kind))
                .Select(ToSummary)
                .ToList());
        }

        var solo = await db.SharedLists.AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OwnerUserId == userId && x.CircleId == null)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(solo.Select(ToSummary).ToList());
    }

    [HttpPost("lists")]
    public async Task<ActionResult<SharedListDetailResponse>> Create(
        [FromBody] SharedListRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Circle? circle = null;
        if (request.CircleId is Guid cid)
        {
            circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
            if (circle is null || HiddenSurprise(circle, userId) || member is null)
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            if (circle.Status != CircleStatus.Active)
            {
                return BadRequest(new ErrorResponse("Este circulo esta travado."));
            }

            if (member.Role < CircleRole.Editor)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Seu papel neste circulo nao cria listas."));
            }
        }

        var list = new SharedList
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            CircleId = circle?.Id,
            Name = request.Name.Trim(),
            Kind = EnumText.ParseListKind(request.Kind),
            MemberAccess = EnumText.ParseListAccess(request.MemberAccess),
            CreatedAt = DateTime.UtcNow,
        };
        db.SharedLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/lists/{list.Id}", await Detail(list.Id, userId, cancellationToken));
    }

    [HttpGet("lists/{id:guid}")]
    public async Task<ActionResult<SharedListDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var detail = await Detail(id, userId, cancellationToken);
        return detail is null ? NotFound(new ErrorResponse("Lista nao encontrada.")) : Ok(detail);
    }

    [HttpPut("lists/{id:guid}/access")]
    public async Task<ActionResult<SharedListDetailResponse>> SetAccess(
        Guid id,
        [FromBody] SharedListAccessRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return NotFound(new ErrorResponse("Lista nao encontrada."));
        }

        if (!CanChangeAccess(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So o criador altera a permissao desta lista."));
        }

        access.List.MemberAccess = EnumText.ParseListAccess(request.MemberAccess);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await Detail(id, userId, cancellationToken));
    }

    [HttpPut("lists/{id:guid}/grants")]
    public async Task<ActionResult<SharedListDetailResponse>> SetGrant(
        Guid id,
        [FromBody] SharedListGrantRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return NotFound(new ErrorResponse("Lista nao encontrada."));
        }

        if (!CanChangeAccess(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So o criador altera a permissao desta lista."));
        }

        if (access.Circle is null || access.Circle.Members.All(x => x.UserId != request.UserId))
        {
            return BadRequest(new ErrorResponse("A pessoa precisa ser membro do circulo."));
        }

        var grant = access.List.Grants.FirstOrDefault(x => x.UserId == request.UserId);
        if (grant is null)
        {
            grant = new SharedListGrant
            {
                Id = Guid.NewGuid(),
                ListId = access.List.Id,
                UserId = request.UserId,
            };
            db.SharedListGrants.Add(grant);
            access.List.Grants.Add(grant);
        }

        grant.Access = EnumText.ParseListAccess(request.Access);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await Detail(id, userId, cancellationToken));
    }

    [HttpDelete("lists/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return NotFound(new ErrorResponse("Lista nao encontrada."));
        }

        if (!CanChangeAccess(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So o criador exclui esta lista."));
        }

        db.SharedLists.Remove(access.List);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("lists/{id:guid}/items")]
    public async Task<ActionResult<SharedListItemResponse>> AddItem(
        Guid id,
        [FromBody] SharedListItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return NotFound(new ErrorResponse("Lista nao encontrada."));
        }

        if (!CanAdd(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta lista nao permite adicionar itens."));
        }

        var item = new SharedListItem
        {
            Id = Guid.NewGuid(),
            ListId = access.List.Id,
            Text = request.Text.Trim(),
            Quantity = TrimOrNull(request.Quantity),
            Note = TrimOrNull(request.Note),
            SortOrder = access.List.Items.Count == 0 ? 0 : access.List.Items.Max(x => x.SortOrder) + 1,
            AddedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.SharedListItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        var names = await Names([userId], cancellationToken);
        return Created($"/api/lists/{id}/items/{item.Id}", ToItem(item, names));
    }

    [HttpPut("lists/{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<SharedListItemResponse>> UpdateItem(
        Guid id,
        Guid itemId,
        [FromBody] SharedListItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        var item = access?.List.Items.FirstOrDefault(x => x.Id == itemId);
        if (access is null || item is null)
        {
            return NotFound(new ErrorResponse("Item nao encontrado."));
        }

        if (!CanManageItems(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta lista nao permite editar itens."));
        }

        item.Text = request.Text.Trim();
        item.Quantity = TrimOrNull(request.Quantity);
        item.Note = TrimOrNull(request.Note);
        await db.SaveChangesAsync(cancellationToken);
        var names = await Names([item.AddedByUserId, item.DoneByUserId], cancellationToken);
        return Ok(ToItem(item, names));
    }

    [HttpPost("lists/{id:guid}/items/{itemId:guid}/done")]
    public async Task<ActionResult<SharedListItemResponse>> ToggleDone(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        var item = access?.List.Items.FirstOrDefault(x => x.Id == itemId);
        if (access is null || item is null)
        {
            return NotFound(new ErrorResponse("Item nao encontrado."));
        }

        if (!CanComplete(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta lista nao permite marcar itens."));
        }

        if (item.IsDone)
        {
            item.IsDone = false;
            item.DoneByUserId = null;
            item.DoneAt = null;
        }
        else
        {
            item.IsDone = true;
            item.DoneByUserId = userId;
            item.DoneAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var names = await Names([item.AddedByUserId, item.DoneByUserId], cancellationToken);
        return Ok(ToItem(item, names));
    }

    [HttpDelete("lists/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        var item = access?.List.Items.FirstOrDefault(x => x.Id == itemId);
        if (access is null || item is null)
        {
            return NotFound(new ErrorResponse("Item nao encontrado."));
        }

        if (!CanManageItems(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta lista nao permite excluir itens."));
        }

        db.SharedListItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("lists/{id:guid}/items/order")]
    public async Task<IActionResult> Reorder(
        Guid id,
        [FromBody] ReorderListItemsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return NotFound(new ErrorResponse("Lista nao encontrada."));
        }

        if (!CanManageItems(access))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta lista nao permite reordenar itens."));
        }

        for (var i = 0; i < request.Ids.Length; i++)
        {
            var item = access.List.Items.FirstOrDefault(x => x.Id == request.Ids[i]);
            if (item is not null)
            {
                item.SortOrder = i;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Access?> Load(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var list = await db.SharedLists
            .Include(x => x.Items)
            .Include(x => x.Grants)
            .Include(x => x.Circle)
                .ThenInclude(x => x!.Members)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (list is null)
        {
            return null;
        }

        if (list.CircleId is null)
        {
            return list.OwnerUserId == userId ? new Access(list, null, CircleRole.Owner, userId) : null;
        }

        var circle = list.Circle;
        if (circle is null || HiddenSurprise(circle, userId) || HideWishList(circle, userId, list.Kind))
        {
            return null;
        }

        var member = circle.Members.FirstOrDefault(x => x.UserId == userId);
        return member is null ? null : new Access(list, circle, member.Role, userId);
    }

    private async Task<SharedListDetailResponse?> Detail(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var access = await Load(id, userId, cancellationToken);
        if (access is null)
        {
            return null;
        }

        var ids = access.List.Items
            .SelectMany(x => new Guid?[] { x.AddedByUserId, x.DoneByUserId })
            .ToList();
        if (access.Circle is not null)
        {
            ids.AddRange(access.Circle.Members.Select(x => (Guid?)x.UserId));
        }

        var names = await Names(ids, cancellationToken);
        var members = access.Circle is null
            ? new List<SharedListMemberAccessResponse>()
            : access.Circle.Members
                .Where(x => x.UserId != access.List.OwnerUserId)
                .Select(member =>
                {
                    var grant = access.List.Grants.FirstOrDefault(x => x.UserId == member.UserId);
                    var level = grant?.Access ?? access.List.MemberAccess;
                    return new SharedListMemberAccessResponse(
                        member.UserId,
                        names.TryGetValue(member.UserId, out var name) ? name : "Pessoa",
                        EnumText.ListAccess(level),
                        grant is not null);
                })
                .ToList();

        return new SharedListDetailResponse(
            access.List.Id,
            access.List.Name,
            EnumText.ListKind(access.List.Kind),
            access.List.CircleId,
            EnumText.ListAccess(access.List.MemberAccess),
            EnumText.ListAccess(Effective(access)),
            CanAdd(access),
            CanComplete(access),
            CanManageItems(access),
            CanChangeAccess(access),
            members,
            access.List.Items.OrderBy(x => x.SortOrder).Select(x => ToItem(x, names)).ToList());
    }

    private async Task<Dictionary<Guid, string>> Names(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Profiles.AsNoTracking()
            .Where(x => wanted.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName, cancellationToken);
    }

    private static SharedListSummaryResponse ToSummary(SharedList list) =>
        new(
            list.Id,
            list.Name,
            EnumText.ListKind(list.Kind),
            list.CircleId,
            EnumText.ListAccess(list.MemberAccess),
            list.Items.Count,
            list.Items.Count(x => x.IsDone));

    private static SharedListItemResponse ToItem(SharedListItem item, IReadOnlyDictionary<Guid, string> names) =>
        new(
            item.Id,
            item.Text,
            item.Quantity,
            item.Note,
            item.IsDone,
            item.SortOrder,
            item.AddedByUserId,
            item.AddedByUserId is Guid added && names.TryGetValue(added, out var addedName) ? addedName : null,
            item.DoneByUserId,
            item.DoneByUserId is Guid done && names.TryGetValue(done, out var doneName) ? doneName : null);

    private static SharedListAccess Effective(Access access)
    {
        if (access.List.OwnerUserId == access.UserId || access.Circle is null)
        {
            return SharedListAccess.Manage;
        }

        var grant = access.List.Grants.FirstOrDefault(x => x.UserId == access.UserId);
        return grant?.Access ?? access.List.MemberAccess;
    }

    private static bool CircleWritable(Access access) =>
        access.Circle is null || access.Circle.Status == CircleStatus.Active;

    private static bool CanAdd(Access access) =>
        CircleWritable(access) && Effective(access) >= SharedListAccess.Add;

    private static bool CanComplete(Access access) =>
        CircleWritable(access) && Effective(access) >= SharedListAccess.Complete;

    private static bool CanManageItems(Access access) =>
        CircleWritable(access) && Effective(access) >= SharedListAccess.Manage;

    private static bool CanChangeAccess(Access access) =>
        access.List.OwnerUserId == access.UserId;

    private static bool HiddenSurprise(Circle circle, Guid userId) =>
        circle.IsSurprise && circle.RevealedAt is null && circle.SurpriseSubjectUserId == userId;

    private static bool HideWishList(Circle circle, Guid userId, SharedListKind kind) =>
        kind == SharedListKind.Wishes && HiddenSurprise(circle, userId);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Access(SharedList List, Circle? Circle, CircleRole Role, Guid UserId);
}
