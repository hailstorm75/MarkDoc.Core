﻿namespace MarkDoc.Members
{
  /// <summary>
  /// Interface for resolved types
  /// </summary>
  public interface IResType
  {
    /// <summary>
    /// Resolved type display name
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Resolved type name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resolved type namespace
    /// </summary>
    string TypeNamespace { get; }
  }
}
