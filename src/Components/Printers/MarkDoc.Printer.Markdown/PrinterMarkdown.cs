﻿using MarkDoc.Elements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MarkDoc.Generator;
using MarkDoc.Linkers;
using MarkDoc.Members.Types;

namespace MarkDoc.Printer.Markdown
{
  /// <summary>
  /// Printer for markdown documentation
  /// </summary>
  public class PrinterMarkdown
    : IPrinter
  {
    #region Fields

    private const string EXTENSION = ".md";

    private readonly ILinker m_linker;
    private readonly ITypeComposer m_composer;

    #endregion

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="composer">Injected composer</param>
    /// <param name="linker">Injected linker</param>
    public PrinterMarkdown(ITypeComposer composer, ILinker linker)
    {
      m_linker = linker;
      m_composer = composer;
    }

    #region Methods

    /// <inheritdoc />
    public async Task Print(IEnumerable<IType> types, string path)
    {
      Task PrintIntermediate(IType type)
        => Print(m_composer.Compose(type), type, path);

      // Prepare the tasks of printing out pages for each respective type
      var tasks = types.AsParallel().Select(PrintIntermediate);

      // Execute the prepared tasks
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task Print(IElement element, IType type, string output)
    {
      // Compose the export path for thee given type
      var path = Path.Combine(output, m_linker.Paths[type]) + EXTENSION;

      // If the output path directory does not exist
      if (!Directory.Exists(path))
        // attempt to create it
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException());

      // Create the file to export to
      await using var file = File.CreateText(path);
      // For each text line from the page..
      foreach (var line in element.Print())
        // print it to the export file
        await file.WriteAsync(line).ConfigureAwait(false);
    }

    #endregion
  }
}
