namespace TALXIS.CLI.Core.Platforms.PowerPlatform;

/// <summary>
/// A connector available in a Power Platform environment (e.g. shared_teams).
/// </summary>
public sealed record ConnectorSummary(
    string Name,
    string DisplayName,
    string? Tier,
    bool IsCustomApi,
    string? Description);

/// <summary>
/// One operation (action or trigger) exposed by a connector, distilled from
/// its OpenAPI definition.
/// </summary>
public sealed record ConnectorOperationSummary(
    string OperationId,
    string Kind,
    string? Summary,
    string? Visibility,
    string? Description);

/// <summary>
/// A single operation parameter. Body schema leaves are flattened into
/// slash-joined names (e.g. <c>item/subject</c>) — exactly the key format a
/// cloud flow definition uses in <c>inputs.parameters</c>.
/// </summary>
public sealed record ConnectorOperationParameter(
    string Name,
    string In,
    string? Type,
    bool Required,
    string? Summary,
    string? Description,
    IReadOnlyList<string>? EnumValues,
    bool IsDynamic);

/// <summary>
/// Full parameter-level detail of one connector operation, ready to be copied
/// into a cloud flow action (<c>host.apiId</c>, <c>host.operationId</c>,
/// <c>inputs.parameters</c> keys).
/// </summary>
public sealed record ConnectorOperationDetail(
    string ConnectorName,
    string ApiId,
    string OperationId,
    string Kind,
    string? Summary,
    string? Description,
    string HttpMethod,
    string Path,
    IReadOnlyList<ConnectorOperationParameter> Parameters);

/// <summary>
/// Read-only discovery over the connectors available in the target
/// environment and their operations, backed by the Power Apps connector API.
/// Exists so agents authoring cloud flow definitions can look up exact
/// operation ids and parameter names instead of guessing them.
/// </summary>
public interface IConnectorCatalogService
{
    /// <summary>Lists connectors available in the profile's environment.</summary>
    Task<IReadOnlyList<ConnectorSummary>> ListConnectorsAsync(
        string? profileName,
        CancellationToken ct);

    /// <summary>Lists all operations (actions and triggers) of one connector.</summary>
    Task<IReadOnlyList<ConnectorOperationSummary>> ListOperationsAsync(
        string? profileName,
        string connectorName,
        CancellationToken ct);

    /// <summary>
    /// Returns parameter-level detail for one operation, or null when the
    /// operation does not exist on the connector.
    /// </summary>
    Task<ConnectorOperationDetail?> GetOperationAsync(
        string? profileName,
        string connectorName,
        string operationId,
        CancellationToken ct);
}
