﻿using System;
using MarkDoc.Members.Enums;
using MarkDoc.Members.Members;

namespace MarkDoc.Members.Dnlib.Members
{
  public abstract class MemberDef
    : IMember
  {
    #region Properties

    protected IResolver Resolver { get; }

    /// <inheritdoc />
    public abstract bool IsStatic { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract AccessorType Accessor { get; }

    public abstract string RawName { get; }

    #endregion

    /// <summary>
    /// Default constructor
    /// </summary>
    protected internal MemberDef(IResolver resolver, dnlib.DotNet.IMemberDef source)
    {
      if (source is null)
        throw new ArgumentNullException(nameof(source));

      Resolver = resolver;
    }
  }
}