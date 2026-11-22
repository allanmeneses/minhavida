using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/circles")]
public sealed class CirclesController(
    AppDbContext db,
    QuotaService quota,
    IEmailSender email,
    IFileStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CircleSummaryResponse>>> Mine(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var memberships = await db.CircleMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.CircleId, x.Role })
            .ToListAsync(cancellationToken);
        var ids = memberships.Select(x => x.CircleId).ToList();
        var circles = await db.Circles
            .AsNoTracking()
            .Include(x => x.Members)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var revealedForMe = await db.Circles
            .AsNoTracking()
            .Include(x => x.Members)
            .Where(x => x.IsSurprise && x.RevealedAt != null && x.SurpriseSubjectUserId == userId && !ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var roles = memberships.ToDictionary(x => x.CircleId, x => x.Role);
        var result = circles
            .Where(circle => !HiddenSurprise(circle, userId))
            .Select(circle => ToSummary(circle, roles[circle.Id], userId))
            .Concat(revealedForMe.Select(circle => ToSummary(circle, CircleRole.Viewer, userId)))
            .ToList();
        return Ok(result);
    }

    [HttpGet("discover")]
    public async Task<ActionResult<IReadOnlyList<CircleSummaryResponse>>> Discover(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var query = db.Circles
            .AsNoTracking()
            .Include(x => x.Members)
            .Where(x => x.Discoverable && x.Status == CircleStatus.Active)
            .Where(x => !x.IsSurprise || x.RevealedAt != null || x.SurpriseSubjectUserId != userId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }

        var items = await query.ToListAsync(cancellationToken);
        return Ok(items.Select(circle => ToSummary(circle, RoleOf(circle, userId), userId)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CircleSummaryResponse>> Create(
        [FromBody] CreateCircleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var quotaError = await quota.TryAddCircleAsync(userId, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        Guid? subjectId = null;
        if (request.IsSurprise && !string.IsNullOrWhiteSpace(request.SurpriseSubjectEmail))
        {
            var subject = await db.Users.FirstOrDefaultAsync(
                x => x.Email == request.SurpriseSubjectEmail.Trim().ToLowerInvariant(),
                cancellationToken);
            subjectId = subject?.Id;
        }

        Guid? successorId = null;
        if (!string.IsNullOrWhiteSpace(request.SuccessorEmail))
        {
            var successor = await db.Users.FirstOrDefaultAsync(
                x => x.Email == request.SuccessorEmail.Trim().ToLowerInvariant(),
                cancellationToken);
            successorId = successor?.Id;
        }

        var now = DateTime.UtcNow;
        var circle = new Circle
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            SuccessorUserId = successorId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Visibility = EnumText.ParseVisibility(request.Visibility),
            Discoverable = request.Discoverable,
            IsSurprise = request.IsSurprise,
            SurpriseSubjectUserId = subjectId,
            Kind = EnumText.ParseCircleKind(request.Kind),
            Status = CircleStatus.Active,
            CreatedAt = now,
        };
        db.Circles.Add(circle);
        db.CircleMembers.Add(new CircleMember
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            UserId = userId,
            Role = CircleRole.Owner,
            JoinedAt = now,
        });
        db.CircleLogs.Add(Log(circle.Id, userId, "created", "Circulo criado."));
        await db.SaveChangesAsync(cancellationToken);
        circle.Members = await db.CircleMembers.Where(x => x.CircleId == circle.Id).ToListAsync(cancellationToken);
        return Created($"/api/circles/{circle.Id}", ToSummary(circle, CircleRole.Owner, userId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CircleDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var member = circle.Members.FirstOrDefault(x => x.UserId == userId);
        if (member is null)
        {
            if (CircleAccess.CanSeeAfterReveal(circle, userId)
                || circle.Visibility == CircleVisibility.Public
                || circle.Discoverable)
            {
                return Ok(PublicPreview(circle));
            }

            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var names = await MemberNames(circle, cancellationToken);
        var posts = circle.Posts.OrderByDescending(x => x.CreatedAt).Select(ToPost).ToList();
        var log = circle.Logs.OrderByDescending(x => x.CreatedAt).Select(ToLog).ToList();
        var joinRequests = member.Role >= CircleRole.Admin
            ? await PendingJoinRequests(circle.Id, cancellationToken)
            : [];
        return Ok(new CircleDetailResponse(
            circle.Id,
            circle.Name,
            circle.Description,
            EnumText.Visibility(circle.Visibility),
            circle.Discoverable,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            circle.OwnerUserId,
            names,
            posts,
            log,
            joinRequests));
    }

    [HttpPost("{id:guid}/invites")]
    public async Task<ActionResult<InviteCreatedResponse>> Invite(
        Guid id,
        [FromBody] InviteCircleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var member = circle.Members.FirstOrDefault(x => x.UserId == userId);
        if (member is null || member.Role < CircleRole.Admin)
        {
            return Forbid();
        }

        var token = TokenHash.NewToken();
        db.CircleInvites.Add(new CircleInvite
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            CreatedByUserId = userId,
            Email = request.Email.Trim().ToLowerInvariant(),
            TokenHash = TokenHash.Hash(token),
            Role = EnumText.ParseRole(request.Role),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        });
        db.CircleLogs.Add(Log(circle.Id, userId, "invited", $"Convite enviado para {request.Email.Trim().ToLowerInvariant()}."));
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(
            request.Email.Trim().ToLowerInvariant(),
            "Convite para um circulo",
            $"Use este codigo de convite: {token}",
            cancellationToken);
        return Ok(new InviteCreatedResponse(token));
    }

    [HttpPost("invites/{token}/accept")]
    public async Task<ActionResult<CircleSummaryResponse>> AcceptInvite(string token, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var hash = TokenHash.Hash(token);
        var invite = await db.CircleInvites.Include(x => x.Circle).ThenInclude(x => x.Members)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (invite is null || invite.AcceptedAt is not null || invite.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ErrorResponse("Convite invalido."));
        }

        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse("Este convite e para outro e-mail."));
        }

        if (HiddenSurprise(invite.Circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (invite.Circle.Members.All(x => x.UserId != userId))
        {
            db.CircleMembers.Add(new CircleMember
            {
                Id = Guid.NewGuid(),
                CircleId = invite.CircleId,
                UserId = userId,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow,
            });
        }

        invite.AcceptedAt = DateTime.UtcNow;
        db.CircleLogs.Add(Log(invite.CircleId, userId, "joined", "Entrou no circulo por convite."));
        await db.SaveChangesAsync(cancellationToken);
        var circle = await LoadCircle(invite.CircleId, cancellationToken);
        return Ok(ToSummary(circle!, invite.Role, userId));
    }

    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (circle.Members.Any(x => x.UserId == userId))
        {
            return NoContent();
        }

        if (circle.Visibility == CircleVisibility.Public)
        {
            db.CircleMembers.Add(new CircleMember
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                UserId = userId,
                Role = CircleRole.Viewer,
                JoinedAt = DateTime.UtcNow,
            });
            db.CircleLogs.Add(Log(circle.Id, userId, "joined", "Entrou no circulo publico."));
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        var pending = await db.CircleJoinRequests.AnyAsync(
            x => x.CircleId == id && x.UserId == userId && x.Status == "pending",
            cancellationToken);
        if (!pending)
        {
            db.CircleJoinRequests.Add(new CircleJoinRequest
            {
                Id = Guid.NewGuid(),
                CircleId = id,
                UserId = userId,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Accepted();
    }

    [HttpPost("{id:guid}/join-requests/{requestId:guid}/accept")]
    public async Task<IActionResult> AcceptJoin(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        var actor = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || actor is null || actor.Role < CircleRole.Admin)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var join = await db.CircleJoinRequests.FirstOrDefaultAsync(
            x => x.Id == requestId && x.CircleId == id && x.Status == "pending",
            cancellationToken);
        if (join is null)
        {
            return NotFound(new ErrorResponse("Pedido nao encontrado."));
        }

        join.Status = "accepted";
        if (circle.Members.All(x => x.UserId != join.UserId))
        {
            db.CircleMembers.Add(new CircleMember
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                UserId = join.UserId,
                Role = CircleRole.Viewer,
                JoinedAt = DateTime.UtcNow,
            });
        }

        db.CircleLogs.Add(Log(circle.Id, userId, "joined", "Pedido de entrada aceito."));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer")]
    public async Task<ActionResult<CircleDetailResponse>> Transfer(
        Guid id,
        [FromBody] TransferCircleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);

        var circle = await LoadCircle(id, cancellationToken);
        var owner = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || HiddenSurprise(circle, userId) || owner is null || owner.Role != CircleRole.Owner || circle.OwnerUserId != userId)
        {
            return BadRequest(new ErrorResponse("So o dono atual pode transferir o circulo."));
        }

        if (request.MemberUserId == userId)
        {
            return BadRequest(new ErrorResponse("Escolha outra pessoa do circulo."));
        }

        var target = circle.Members.FirstOrDefault(x => x.UserId == request.MemberUserId);
        if (target is null)
        {
            return BadRequest(new ErrorResponse("A pessoa precisa ja fazer parte do circulo."));
        }

        owner.Role = CircleRole.Admin;
        target.Role = CircleRole.Owner;
        circle.OwnerUserId = target.UserId;
        db.CircleLogs.Add(Log(circle.Id, userId, "transferred", "Dono transferiu o circulo em vida. Historico e permissoes ficam."));

        var oldOwner = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var newOwner = await db.Users.FirstAsync(x => x.Id == target.UserId, cancellationToken);
        if (oldOwner.OwnedCircleCount > 0)
        {
            oldOwner.OwnedCircleCount -= 1;
        }

        newOwner.OwnedCircleCount += 1;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var names = await MemberNames(circle, cancellationToken);
        return Ok(new CircleDetailResponse(
            circle.Id,
            circle.Name,
            circle.Description,
            EnumText.Visibility(circle.Visibility),
            circle.Discoverable,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            circle.OwnerUserId,
            names,
            circle.Posts.OrderByDescending(x => x.CreatedAt).Select(ToPost).ToList(),
            circle.Logs.OrderByDescending(x => x.CreatedAt).Select(ToLog).ToList(),
            await PendingJoinRequests(circle.Id, cancellationToken)));
    }

    [HttpDelete("{id:guid}/membership")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var member = circle.Members.FirstOrDefault(x => x.UserId == userId);
        if (member is null)
        {
            return NoContent();
        }

        if (member.Role == CircleRole.Owner)
        {
            return BadRequest(new ErrorResponse("O dono precisa transferir o circulo antes de sair."));
        }

        db.CircleMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/posts")]
    public async Task<ActionResult<CirclePostResponse>> Post(
        Guid id,
        [FromBody] CirclePostRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var member = circle.Members.FirstOrDefault(x => x.UserId == userId);
        if (member is null || member.Role < CircleRole.Editor)
        {
            return Forbid();
        }

        var post = new CirclePost
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            AuthorUserId = userId,
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.CirclePosts.Add(post);
        db.CircleLogs.Add(Log(circle.Id, userId, "posted", "Nova entrada."));
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/circles/{id}/posts/{post.Id}", ToPost(post));
    }

    [HttpDelete("{id:guid}/posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid id, Guid postId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || member is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var post = circle.Posts.FirstOrDefault(x => x.Id == postId);
        if (post is null)
        {
            return NotFound(new ErrorResponse("Entrada nao encontrada."));
        }

        if (post.AuthorUserId != userId && member.Role < CircleRole.Admin)
        {
            return Forbid();
        }

        if (post.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(post.PhotoStorageKey, cancellationToken);
        }

        db.CirclePosts.Remove(post);
        db.CircleLogs.Add(Log(circle.Id, userId, "deleted", "Entrada apagada."));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reveal")]
    public async Task<ActionResult<RevealSummaryResponse>> Reveal(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || member is null || member.Role < CircleRole.Admin)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (!circle.IsSurprise)
        {
            return BadRequest(new ErrorResponse("Este circulo nao e uma surpresa."));
        }

        circle.RevealedAt ??= DateTime.UtcNow;
        db.CircleLogs.Add(Log(circle.Id, userId, "revealed", "Surpresa revelada."));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToReveal(circle));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<CircleSummaryResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || HiddenSurprise(circle, userId) || member is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (member.Role < CircleRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So administrador ou dono arquiva o ciclo."));
        }

        if (circle.Kind != CircleKind.Temporary && circle.Kind != CircleKind.Daily)
        {
            return BadRequest(new ErrorResponse("So circulos temporarios ou diarios arquivam no fim do ciclo."));
        }

        if (circle.Status != CircleStatus.Archived)
        {
            circle.Status = CircleStatus.Archived;
            db.CircleLogs.Add(Log(circle.Id, userId, "archived", "Ciclo encerrado. A retrospectiva fica para consulta."));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(ToSummary(circle, member.Role, userId));
    }

    [HttpGet("{id:guid}/reveal-summary")]
    public async Task<ActionResult<RevealSummaryResponse>> RevealSummary(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || !CanSeeReveal(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        return Ok(ToReveal(circle));
    }

    [HttpPost("{id:guid}/keep-memory")]
    public async Task<ActionResult<MemoryResponse>> KeepMemory(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || circle.RevealedAt is null || !CanSeeReveal(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var existing = await db.Memories.FirstOrDefaultAsync(
            x => x.AuthorUserId == userId && x.SourceCircleId == circle.Id,
            cancellationToken);
        var now = DateTime.UtcNow;
        var title = $"Recordacao: {circle.Name}";
        var body = string.Join("\n", circle.Posts.OrderBy(x => x.CreatedAt).Select(x => x.Body).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (existing is null)
        {
            existing = new Memory
            {
                Id = Guid.NewGuid(),
                AuthorUserId = userId,
                SourceCircleId = circle.Id,
                Title = title,
                Body = body,
                OccurredOn = DateOnly.FromDateTime(circle.RevealedAt ?? now),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Memories.Add(existing);
        }
        else
        {
            existing.Title = title;
            existing.Body = body;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new MemoryResponse(
            existing.Id,
            existing.AuthorUserId,
            existing.CircleId,
            existing.SourceCircleId,
            existing.Title,
            existing.Body,
            existing.OccurredOn,
            existing.PhotoStorageKey is not null,
            existing.CreatedAt,
            existing.UpdatedAt));
    }

    [HttpGet("{id:guid}/log")]
    public async Task<ActionResult<IReadOnlyList<CircleLogResponse>>> LogList(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        return Ok(circle.Logs.OrderByDescending(x => x.CreatedAt).Select(ToLog).ToList());
    }

    private Task<Circle?> LoadCircle(Guid id, CancellationToken cancellationToken) =>
        db.Circles
            .Include(x => x.Members)
            .Include(x => x.Posts)
            .Include(x => x.Logs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private async Task<List<CircleMemberDto>> MemberNames(Circle circle, CancellationToken cancellationToken)
    {
        var ids = circle.Members.Select(x => x.UserId).ToList();
        var profiles = await db.Profiles.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        return circle.Members
            .Select(member => new CircleMemberDto(
                member.UserId,
                profiles.TryGetValue(member.UserId, out var profile) ? profile.DisplayName : "Pessoa",
                EnumText.Role(member.Role)))
            .ToList();
    }

    private static bool HiddenSurprise(Circle circle, Guid userId) =>
        CircleAccess.HiddenSurprise(circle, userId);

    private static bool CanSeeReveal(Circle circle, Guid userId)
    {
        if (CircleAccess.HiddenSurprise(circle, userId))
        {
            return false;
        }

        if (circle.Members.Any(x => x.UserId == userId))
        {
            return true;
        }

        return CircleAccess.CanSeeAfterReveal(circle, userId);
    }

    private static RevealSummaryResponse ToReveal(Circle circle) =>
        new(
            circle.Id,
            circle.Name,
            circle.Description,
            circle.RevealedAt,
            circle.Posts.OrderBy(x => x.CreatedAt).Select(post => new RevealMessageDto(
                post.Id,
                post.AuthorUserId,
                post.Body,
                post.PhotoStorageKey is not null,
                post.CreatedAt)).ToList(),
            circle.Posts.Count(x => x.PhotoStorageKey is not null));

    private static CircleRole RoleOf(Circle circle, Guid userId) =>
        circle.Members.FirstOrDefault(x => x.UserId == userId)?.Role ?? CircleRole.Viewer;

    private static CircleSummaryResponse ToSummary(Circle circle, CircleRole role, Guid userId)
    {
        var hidden = HiddenSurprise(circle, userId);
        return new CircleSummaryResponse(
            circle.Id,
            hidden ? null : circle.Name,
            hidden ? null : circle.Description,
            EnumText.Visibility(circle.Visibility),
            circle.Discoverable,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            EnumText.Role(role),
            circle.Members.Count);
    }

    private async Task<List<CircleJoinRequestDto>> PendingJoinRequests(Guid circleId, CancellationToken cancellationToken)
    {
        var pending = await db.CircleJoinRequests.AsNoTracking()
            .Where(x => x.CircleId == circleId && x.Status == "pending")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var ids = pending.Select(x => x.UserId).ToList();
        var profiles = await db.Profiles.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        return pending
            .Select(request => new CircleJoinRequestDto(
                request.Id,
                request.UserId,
                profiles.TryGetValue(request.UserId, out var profile) ? profile.DisplayName : "Pessoa",
                request.Status,
                request.CreatedAt))
            .ToList();
    }

    private static CircleDetailResponse PublicPreview(Circle circle) =>
        new(
            circle.Id,
            circle.Name,
            circle.Description,
            EnumText.Visibility(circle.Visibility),
            circle.Discoverable,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            null,
            [],
            [],
            [],
            []);

    private static CirclePostResponse ToPost(CirclePost post) =>
        new(post.Id, post.AuthorUserId, post.Body, post.PhotoStorageKey is not null, post.CreatedAt);

    private static CircleLogResponse ToLog(CircleLog log) =>
        new(log.Id, log.ActorUserId, log.Action, log.Detail, log.CreatedAt);

    private static CircleLog Log(Guid circleId, Guid? actorId, string action, string detail) =>
        new()
        {
            Id = Guid.NewGuid(),
            CircleId = circleId,
            ActorUserId = actorId,
            Action = action,
            Detail = detail,
            CreatedAt = DateTime.UtcNow,
        };
}
