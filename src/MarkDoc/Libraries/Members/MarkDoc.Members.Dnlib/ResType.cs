﻿using System;
using System.Diagnostics;

namespace MarkDoc.Members.Dnlib
{
  [DebuggerDisplay("{DisplayName}")]
  public class ResType
    : IResType
  {
    #region Properties

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string TypeNamespace { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    #endregion

    public ResType(dnlib.DotNet.TypeSig source)
      : this(source, ResolveName(source)) { }

    protected ResType(dnlib.DotNet.TypeSig source, string displayName)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      Name = ResolveName(source);
      DisplayName = displayName;
      TypeNamespace = source.Namespace;
    }

    protected static string ResolveName(dnlib.DotNet.IType source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      var name = source.Name.String;
      var genericsIndex = name.IndexOf('`', StringComparison.InvariantCulture);
      if (genericsIndex == -1)
        return name;

      return name.Remove(genericsIndex);
    }
  }
}
