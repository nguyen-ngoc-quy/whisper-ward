using System;
using UnityEngine;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Serializable provenance for one FSM-owned visibility query. The policy
    /// resolves the player camera-eye and authoritative E20 endpoint; the FSM
    /// supplies the immutable relay origin and identity.
    /// </summary>
    public struct SuppressionVisibilityQuery
    {
        public SuppressionVisibilityQuery(string queryId, Vector3 relayOrigin,
            string originProvenance)
        {
            QueryId = queryId ?? string.Empty;
            RelayOrigin = relayOrigin;
            OriginProvenance = originProvenance ?? string.Empty;
        }

        public string QueryId { get; }
        public Vector3 RelayOrigin { get; }
        public string OriginProvenance { get; }
    }

    /// <summary>Typed result retained on the suppression transport record.</summary>
    public struct SuppressionVisibilityResult
    {
        public SuppressionVisibilityResult(bool visible, string queryId,
            string queryProvenance)
        {
            Visible = visible;
            QueryId = queryId ?? string.Empty;
            QueryProvenance = queryProvenance ?? string.Empty;
        }

        public bool Visible { get; }
        public string QueryId { get; }
        public string QueryProvenance { get; }
    }

    /// <summary>Injected visibility seam owned by GuardFSM suppression decisions.</summary>
    public interface ISuppressionVisibilityPolicy
    {
        /// <summary>Compatibility query used by deterministic legacy fixtures.</summary>
        bool IsVisibleToPlayer(Vector3 worldPosition);
    }

    /// <summary>
    /// Optional typed seam used by production composition. Implementations must
    /// perform exactly one authoritative query and preserve its provenance.
    /// </summary>
    public interface ITypedSuppressionVisibilityPolicy : ISuppressionVisibilityPolicy
    {
        SuppressionVisibilityResult Evaluate(SuppressionVisibilityQuery query);
    }

    /// <summary>Adapter for deterministic fixtures and composition roots.</summary>
    public sealed class DelegateSuppressionVisibilityPolicy
        : ITypedSuppressionVisibilityPolicy
    {
        private readonly Func<Vector3, bool> _legacyQuery;
        private readonly Func<SuppressionVisibilityQuery,
            SuppressionVisibilityResult> _typedQuery;

        public DelegateSuppressionVisibilityPolicy(Func<Vector3, bool> query)
        {
            _legacyQuery = query ?? throw new ArgumentNullException(nameof(query));
        }

        public DelegateSuppressionVisibilityPolicy(
            Func<SuppressionVisibilityQuery, SuppressionVisibilityResult> query)
        {
            _typedQuery = query ?? throw new ArgumentNullException(nameof(query));
        }

        public bool IsVisibleToPlayer(Vector3 worldPosition)
        {
            if (_legacyQuery != null) return _legacyQuery(worldPosition);
            return Evaluate(new SuppressionVisibilityQuery("legacy-query",
                worldPosition, "legacy-adapter")).Visible;
        }

        public SuppressionVisibilityResult Evaluate(SuppressionVisibilityQuery query)
        {
            if (_typedQuery != null) return _typedQuery(query);
            return new SuppressionVisibilityResult(_legacyQuery(query.RelayOrigin),
                query.QueryId, "legacy-adapter");
        }
    }
}
