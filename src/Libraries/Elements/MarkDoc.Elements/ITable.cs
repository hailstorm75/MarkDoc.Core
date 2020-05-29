﻿using MarkDoc.Elements.Extensions;
using System.Collections.Generic;

namespace MarkDoc.Elements
{
  /// <summary>
  /// Interface for the table element
  /// </summary>
  public interface ITable
    : IElement, IHasContent<IReadOnlyCollection<IReadOnlyCollection<IElement>>>, IHasHeading
  {
    /// <summary>
    /// Table headers
    /// </summary>
    /// <value>Collection of header names</value>
    IReadOnlyCollection<IText> Headings { get; }
  }
}
