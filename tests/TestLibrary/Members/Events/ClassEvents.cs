﻿#pragma warning disable 67
#pragma warning disable CS0414
// ReSharper disable All
using System;

namespace TestLibrary.Members.Events
{
  public class ClassEvents
  {
    public event EventHandler EventPublic = null!;
    internal event EventHandler EventInternal = null!;
    protected event EventHandler EventProtected = null!;
    protected internal event EventHandler EventProtectedInternal = null!;
    public static event EventHandler EventStatic = null!;
  }
}
