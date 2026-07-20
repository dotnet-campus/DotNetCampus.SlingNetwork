namespace DotNetCampus.SlingNetwork.Transports.Models;

/// <summary>
/// RFC 3489 中已废弃的 NAT 类型分类。
/// </summary>
public enum Rfc3489NatType
{
    /// <summary>
    /// 当前映射和过滤行为无法由 RFC 3489 分类精确表示。
    /// </summary>
    NotRepresentable,

    FullCone,
    RestrictedCone,
    PortRestrictedCone,
    Symmetric,
}
