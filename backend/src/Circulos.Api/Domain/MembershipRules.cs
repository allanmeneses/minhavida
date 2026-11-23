namespace Circulos.Api.Domain;

/// <summary>
/// Decisao do Allan (auditoria item 4): o sistema nao considera ninguem inativo.
/// Nao existe prazo de inatividade. Tempo sem uso nao muda papel nem direitos.
/// </summary>
public static class MembershipRules
{
    public const bool ConsidersMemberInactiveByTime = false;
}
