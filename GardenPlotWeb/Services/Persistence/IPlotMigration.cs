// <copyright file="IPlotMigration.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json.Nodes;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// A single forward migration of a persisted plot-library document. Each implementation
/// upgrades a document from <see cref="FromVersion"/> to <c>FromVersion + 1</c>.
/// </summary>
/// <remarks>
/// Migrations operate on the raw <see cref="JsonObject"/> root so that old field shapes
/// can be transformed before deserialization onto the current typed model.
/// </remarks>
public interface IPlotMigration
{
    /// <summary>The schema version this migration upgrades away from.</summary>
    int FromVersion { get; }

    /// <summary>
    /// Mutates <paramref name="document"/> in place (or replaces children as needed) so that
    /// it conforms to schema version <c>FromVersion + 1</c>. Implementations must be
    /// idempotent against repeated application of the same step (i.e. defensive against
    /// already-migrated input shapes).
    /// </summary>
    void Migrate(JsonObject document);
}
