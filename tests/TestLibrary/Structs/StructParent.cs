﻿// ReSharper disable All
using System;

namespace TestLibrary.Structs
{
  public class StructParent
  {
    public struct NestedStructPublic
    {
    }

    internal struct NestedStructInternal
    {
    }

    protected struct NestedStructProtected
    {
    }

    public struct NestedGenericStruct<T1, T2>
      where T2 : IDisposable
    {
    }
  }
}
