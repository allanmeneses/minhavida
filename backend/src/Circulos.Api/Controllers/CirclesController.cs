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
    IFileStorage storage,
    SuccessionService succession,
    CircleArchiveService archive,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CircleSummaryResponse>>> Mine(CancellationToken cancellationToken)
    {
        await archive.ArchiveExpiredAsync(cancellationToken);
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
            .Where(x => ids.Contains(x.Id) && x.Status != CircleStatus.Deleted)
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
            .Where(x => x.Status != CircleStatus.Deleted)
            .Where(x => !x.IsSurprise || x.RevealedAt != null || x.SurpriseSubjectUserId != userId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }

        var items = await query.ToListAsync(cancellationToken);
        return Ok(items.Select(circle => ToSummary(circle, RoleOf(circle, userId), userId)).ToList());
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<IReadOnlyList<NearbyHitResponse>>> NearbyCircles(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radiusKm = 10,
        CancellationToken cancellationToken = default)
    {
        var hits = await Nearby.FindAsync(db, User.GetUserId(), lat, lng, radiusKm, "circle", cancellationToken);
        return Ok(hits);
    }

    [HttpPost]
    public async Task<ActionResult<CircleSummaryResponse>> Create(
        [FromBody] CreateCircleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var kind = EnumText.ParseCircleKind(request.Kind);
        if (request.EndsAt is not null && kind == CircleKind.Lasting)
        {
            return BadRequest(new ErrorResponse("Data de fim so vale em circulo temporario ou diario."));
        }

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
            FindableByLocation = request.FindableByLocation,
            Latitude = request.FindableByLocation ? request.Latitude : null,
            Longitude = request.FindableByLocation ? request.Longitude : null,
            IsSurprise = request.IsSurprise,
            SurpriseSubjectUserId = subjectId,
            Kind = kind,
            Status = CircleStatus.Active,
            EndsAt = request.EndsAt?.ToUniversalTime(),
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
        await archive.ArchiveIfDueAsync(id, cancellationToken);
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted || HiddenSurprise(circle, userId))
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
        var shared = await SharedEntriesAsync(circle.Id, cancellationToken);
        return Ok(ToDetail(circle, names, posts, log, joinRequests, shared, await CurrentOfferAsync(circle.Id, userId, cancellationToken)));
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
        return Ok(ShareInvite(token, circle.Name));
    }

    [HttpPost("{id:guid}/invite-link")]
    public async Task<ActionResult<InviteCreatedResponse>> InviteLink(Guid id, CancellationToken cancellationToken)
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
            Email = string.Empty,
            IsOpenLink = true,
            TokenHash = TokenHash.Hash(token),
            Role = CircleRole.Viewer,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        });
        db.CircleLogs.Add(Log(circle.Id, userId, "invited", "Link de convite criado."));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ShareInvite(token, circle.Name));
    }

    [AllowAnonymous]
    [HttpGet("invites/{token}")]
    public async Task<ActionResult<InvitePreviewResponse>> PreviewInvite(string token, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(token);
        var invite = await db.CircleInvites.Include(x => x.Circle)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (invite is null || invite.ExpiresAt < DateTime.UtcNow || (invite.AcceptedAt is not null && !invite.IsOpenLink))
        {
            return NotFound(new ErrorResponse("Convite invalido."));
        }

        return Ok(new InvitePreviewResponse(invite.Circle.Name, EnumText.Role(invite.Role), invite.IsOpenLink, invite.ExpiresAt));
    }

    [HttpPost("invites/{token}/accept")]
    public async Task<ActionResult<CircleSummaryResponse>> AcceptInvite(string token, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var hash = TokenHash.Hash(token);
        var invite = await db.CircleInvites.Include(x => x.Circle).ThenInclude(x => x.Members)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (invite is null
            || invite.ExpiresAt < DateTime.UtcNow
            || (invite.AcceptedAt is not null && !invite.IsOpenLink))
        {
            return BadRequest(new ErrorResponse("Convite invalido."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, invite.CircleId, cancellationToken);
        await db.Entry(invite).ReloadAsync(cancellationToken);
        await db.Entry(invite.Circle).Collection(x => x.Members).LoadAsync(cancellationToken);
        if (invite.ExpiresAt < DateTime.UtcNow || (invite.AcceptedAt is not null && !invite.IsOpenLink))
        {
            return BadRequest(new ErrorResponse("Convite invalido."));
        }

        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        if (!invite.IsOpenLink && !string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
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
            db.CircleLogs.Add(Log(invite.CircleId, userId, "joined", "Entrou no circulo por convite."));
        }

        if (!invite.IsOpenLink)
        {
            invite.AcceptedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var circle = await LoadCircle(invite.CircleId, cancellationToken);
        var role = circle!.Members.First(x => x.UserId == userId).Role;
        return Ok(ToSummary(circle, role, userId));
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
        circle.OwnerLostAt = null;
        circle.OwnershipNextCheckAt = null;
        db.CircleLogs.Add(Log(circle.Id, userId, "transferred", "Dono transferiu o circulo em vida. Historico e permissoes ficam."));

        var oldOwner = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var newOwner = await db.Users.FirstAsync(x => x.Id == target.UserId, cancellationToken);
        if (!circle.IsStarter)
        {
            if (oldOwner.OwnedCircleCount > 0)
            {
                oldOwner.OwnedCircleCount -= 1;
            }

            newOwner.OwnedCircleCount += 1;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var names = await MemberNames(circle, cancellationToken);
        return Ok(ToDetail(
            circle,
            names,
            circle.Posts.OrderByDescending(x => x.CreatedAt).Select(ToPost).ToList(),
            circle.Logs.OrderByDescending(x => x.CreatedAt).Select(ToLog).ToList(),
            await PendingJoinRequests(circle.Id, cancellationToken),
            await SharedEntriesAsync(circle.Id, cancellationToken),
            null));
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
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await CircleRowLock.AcquireAsync(db, id, cancellationToken);
            circle = await LoadCircle(id, cancellationToken);
            member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
            if (circle is null || member is null)
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            var remaining = circle.Members.Where(item => item.UserId != userId).ToList();
            if (remaining.Count == 0)
            {
                return BadRequest(new ErrorResponse("Exclua o circulo se voce e a unica pessoa nele."));
            }

            if (remaining.Any(item => item.Role >= CircleRole.Admin))
            {
                return BadRequest(new ErrorResponse("O dono precisa transferir o circulo antes de sair."));
            }

            CircleOwnership.MarkLost(circle, DateTime.UtcNow);
            db.CircleMembers.Remove(member);
            if (!circle.IsStarter)
            {
                await quota.ReleaseCircleAsync(userId, cancellationToken);
            }
            await succession.OpenIfNeededAsync(circle, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }

        db.CircleMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/location")]
    public async Task<ActionResult<CircleSummaryResponse>> SetLocation(
        Guid id,
        [FromBody] CircleLocationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || circle.Status == CircleStatus.Deleted || HiddenSurprise(circle, userId) || member is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (member.Role < CircleRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So administrador ou dono liga a busca por perto."));
        }

        circle.FindableByLocation = request.FindableByLocation;
        circle.Latitude = request.FindableByLocation ? request.Latitude : null;
        circle.Longitude = request.FindableByLocation ? request.Longitude : null;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToSummary(circle, member.Role, userId));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> DeleteForever(
        Guid id,
        [FromBody] DeleteCircleRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation?.Trim(), "excluir definitivo", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse("Para excluir de vez, escreva: excluir definitivo"));
        }

        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || circle.Status == CircleStatus.Deleted || member is null || member.Role != CircleRole.Owner)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        circle.Status = CircleStatus.Deleted;
        circle.FindableByLocation = false;
        circle.Discoverable = false;
        db.CircleLogs.Add(Log(circle.Id, userId, "deleted-forever", "Exclusao definitiva. Nao da para desfazer."));
        if (!circle.IsStarter)
        {
            await quota.ReleaseCircleAsync(userId, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/posts")]
    public async Task<ActionResult<CirclePostResponse>> Post(
        Guid id,
        [FromBody] CirclePostRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted || HiddenSurprise(circle, userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (!CircleAccess.IsWritable(circle))
        {
            return BadRequest(new ErrorResponse(CircleAccess.LockedMessage));
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new ErrorResponse("A entrada nao pode ficar vazia."));
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
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/circles/{id}/posts/{post.Id}", ToPost(post));
    }

    [HttpDelete("{id:guid}/posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid id, Guid postId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || circle.Status == CircleStatus.Deleted || member is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        if (!CircleAccess.IsWritable(circle))
        {
            return BadRequest(new ErrorResponse(CircleAccess.LockedMessage));
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
        await transaction.CommitAsync(cancellationToken);
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
    public async Task<ActionResult<IReadOnlyList<CircleLogResponse>>> LogList(
        Guid id,
        [FromQuery] string? action,
        [FromQuery] Guid? actorUserId,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted || HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        IEnumerable<CircleLog> logs = circle.Logs;
        if (!string.IsNullOrWhiteSpace(action))
        {
            logs = logs.Where(item => string.Equals(item.Action, action.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (actorUserId is Guid actor)
        {
            logs = logs.Where(item => item.ActorUserId == actor);
        }

        var page = ListPaging.Clamp(skip, take);
        var ordered = logs.OrderByDescending(x => x.CreatedAt).ToList();
        Response.Headers["X-Total-Count"] = ordered.Count.ToString();
        return Ok(ordered.Skip(page.Skip).Take(page.Take == int.MaxValue ? ordered.Count : page.Take).Select(ToLog).ToList());
    }

    [HttpGet("{id:guid}/reports")]
    public async Task<ActionResult<IReadOnlyList<ReportItemResponse>>> CircleReports(Guid id, CancellationToken cancellationToken)
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

        var items = await db.ContentReports.AsNoTracking()
            .Include(item => item.Reporter)
            .Where(item => item.CircleId == id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(ToReport).ToList());
    }

    [HttpPost("{id:guid}/reports/{reportId:guid}/decide")]
    public async Task<IActionResult> DecideReport(
        Guid id,
        Guid reportId,
        [FromBody] CircleReportDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var circle = await LoadCircle(id, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || member is null || member.Role < CircleRole.Admin)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var report = await db.ContentReports.FirstOrDefaultAsync(item => item.Id == reportId && item.CircleId == id, cancellationToken);
        if (report is null)
        {
            return NotFound(new ErrorResponse("Denuncia nao encontrada."));
        }

        if (report.Status == "circle-resolved")
        {
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not "keep" and not "hide")
        {
            return BadRequest(new ErrorResponse("Decida manter ou esconder."));
        }

        report.Status = "circle-resolved";
        report.CircleDecision = decision;
        report.CircleReviewerUserId = userId;
        report.CircleReviewedAt = DateTime.UtcNow;
        db.CircleLogs.Add(Log(id, userId, "report-decided", decision == "hide" ? "Denuncia: conteudo escondido." : "Denuncia: conteudo mantido."));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/gift-ideas")]
    public async Task<ActionResult<IReadOnlyList<GiftIdeaShareResponse>>> GiftIdeas(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var shares = await db.GiftIdeaShares.AsNoTracking()
            .Include(item => item.Reminder)
            .Where(item => item.CircleId == id)
            .ToListAsync(cancellationToken);
        return Ok(shares.Select(item => new GiftIdeaShareResponse(
            item.ReminderId,
            item.Reminder.Title,
            item.Reminder.PersonName,
            System.Text.Json.JsonSerializer.Deserialize<string[]>(item.Reminder.GiftIdeasJson) ?? [])).ToList());
    }

    [HttpPost("{id:guid}/gift-ideas/{reminderId:guid}/ideas")]
    public async Task<ActionResult<GiftIdeaShareResponse>> AddGiftIdea(
        Guid id,
        Guid reminderId,
        [FromBody] AddGiftIdeaRequest request,
        CancellationToken cancellationToken)
    {
        var idea = request.Idea.Trim();
        if (idea.Length == 0)
        {
            return BadRequest(new ErrorResponse("Informe a ideia."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var (status, error, share) = await GiftIdeaEditorAsync(id, reminderId, cancellationToken);
        if (error is not null)
        {
            return StatusCode(status, new ErrorResponse(error));
        }

        var ideas = System.Text.Json.JsonSerializer.Deserialize<string[]>(share!.Reminder.GiftIdeasJson)?.ToList() ?? [];
        ideas.Add(idea);
        share.Reminder.GiftIdeasJson = System.Text.Json.JsonSerializer.Serialize(ideas);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new GiftIdeaShareResponse(share.ReminderId, share.Reminder.Title, share.Reminder.PersonName, ideas.ToArray()));
    }

    [HttpPut("{id:guid}/gift-ideas/{reminderId:guid}")]
    public async Task<ActionResult<GiftIdeaShareResponse>> ReplaceGiftIdeas(
        Guid id,
        Guid reminderId,
        [FromBody] ReplaceGiftIdeasRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, id, cancellationToken);
        var (status, error, share) = await GiftIdeaEditorAsync(id, reminderId, cancellationToken);
        if (error is not null)
        {
            return StatusCode(status, new ErrorResponse(error));
        }

        var ideas = (request.Ideas ?? [])
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
        share!.Reminder.GiftIdeasJson = System.Text.Json.JsonSerializer.Serialize(ideas);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new GiftIdeaShareResponse(share.ReminderId, share.Reminder.Title, share.Reminder.PersonName, ideas));
    }

    private async Task<(int Status, string? Error, GiftIdeaShare? Share)> GiftIdeaEditorAsync(
        Guid circleId,
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await LoadCircle(circleId, cancellationToken);
        var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
        if (circle is null || HiddenSurprise(circle, userId) || member is null)
        {
            return (StatusCodes.Status404NotFound, "Circulo nao encontrado.", null);
        }

        if (!CircleAccess.IsWritable(circle))
        {
            return (StatusCodes.Status400BadRequest, CircleAccess.LockedMessage, null);
        }

        if (member.Role < CircleRole.Editor)
        {
            return (StatusCodes.Status403Forbidden, "Quem so visualiza nao edita ideias de presente.", null);
        }

        var share = await db.GiftIdeaShares
            .Include(item => item.Reminder)
            .FirstOrDefaultAsync(item => item.CircleId == circleId && item.ReminderId == reminderId, cancellationToken);
        if (share is null)
        {
            return (StatusCodes.Status404NotFound, "Ideias de presente nao encontradas.", null);
        }

        return (StatusCodes.Status200OK, null, share);
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
            circle.FindableByLocation,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            EnumText.Role(role),
            circle.Members.Count,
            circle.EndsAt);
    }

    private async Task<OwnershipOfferNoticeResponse?> CurrentOfferAsync(
        Guid circleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var offer = await db.OwnershipOffers.AsNoTracking()
            .Include(item => item.Circle)
            .FirstOrDefaultAsync(
                item => item.CircleId == circleId
                    && item.UserId == userId
                    && item.Status == OwnershipOfferStatus.Pending
                    && item.ExpiresAt > now,
                cancellationToken);
        if (offer is null)
        {
            return null;
        }

        return new OwnershipOfferNoticeResponse(offer.CircleId, offer.Circle.Name, offer.OfferedAt, offer.ExpiresAt);
    }

    private static CircleDetailResponse ToDetail(
        Circle circle,
        IReadOnlyList<CircleMemberDto> names,
        IReadOnlyList<CirclePostResponse> posts,
        IReadOnlyList<CircleLogResponse> log,
        IReadOnlyList<CircleJoinRequestDto> joinRequests,
        IReadOnlyList<SharedLifeEntryResponse> sharedEntries,
        OwnershipOfferNoticeResponse? ownershipOffer) =>
        new(
            circle.Id,
            circle.Name,
            circle.Description,
            EnumText.Visibility(circle.Visibility),
            circle.Discoverable,
            circle.FindableByLocation,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            circle.OwnerUserId,
            names,
            posts,
            log,
            joinRequests,
            sharedEntries,
            circle.EndsAt,
            ownershipOffer);

    private async Task<List<SharedLifeEntryResponse>> SharedEntriesAsync(Guid circleId, CancellationToken cancellationToken)
    {
        var shares = await db.LifeEntryShares
            .AsNoTracking()
            .Include(x => x.LifeEntry)
            .Where(x => x.CircleId == circleId)
            .OrderByDescending(x => x.LifeEntry.UpdatedAt)
            .ToListAsync(cancellationToken);
        if (shares.Count == 0)
        {
            return [];
        }

        var authorIds = shares.Select(x => x.LifeEntry.UserId).Distinct().ToList();
        var names = await db.Profiles.AsNoTracking()
            .Where(x => authorIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        return shares.Select(share =>
        {
            var entry = share.LifeEntry;
            names.TryGetValue(entry.UserId, out var profile);
            return new SharedLifeEntryResponse(
                entry.Id,
                entry.LifeAreaId,
                entry.Title,
                entry.Body,
                entry.NumberValue,
                entry.OccurredOn,
                entry.Location,
                entry.PhotoStorageKey is not null,
                TagList.FromJson(entry.TagsJson),
                entry.UserId,
                profile?.DisplayName ?? "Pessoa",
                entry.CreatedAt,
                entry.UpdatedAt);
        }).ToList();
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
            circle.FindableByLocation,
            circle.IsSurprise,
            circle.RevealedAt is not null,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            null,
            [],
            [],
            [],
            [],
            [],
            circle.EndsAt);

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

    private InviteCreatedResponse ShareInvite(string token, string circleName)
    {
        var root = (configuration["PUBLIC_BASE_URL"] ?? configuration["PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        var path = $"/i/{token}";
        var url = string.IsNullOrWhiteSpace(root) ? path : $"{root}{path}";
        var text = $"Entre no circulo {circleName} no Circulos: {url}";
        return new InviteCreatedResponse(token, text, url);
    }

    private static ReportItemResponse ToReport(ContentReport item) =>
        new(
            item.Id,
            item.ReporterUserId,
            item.Reporter.Email,
            item.TargetKind,
            item.TargetId,
            item.Note,
            item.CreatedAt,
            item.CircleId,
            item.Status,
            item.CircleDecision);
}
