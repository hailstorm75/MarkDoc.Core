﻿using System;
using System.Collections.Generic;
using System.Linq;
using MarkDoc.Helpers;
using MarkDoc.Members;
using MarkDoc.Members.Enums;
using MarkDoc.Members.Types;
using UT.Members.Data;
using Xunit;

namespace UT.Members
{
  public class ClassTests
  {
    #region Data providers

    private static IEnumerable<object?[]> GetClassNamesData()
    {
      var data = new[]
      {
        Constants.PUBLIC_CLASS,
        Constants.INTERNAL_CLASS,
        Constants.PUBLIC_NESTED_CLASS,
        Constants.PROTECTED_NESTED_CLASS,
        Constants.INTERNAL_NESTED_CLASS,
        Constants.PUBLIC_GENERIC_CLASS,
        Constants.PUBLIC_NESTED_GENERIC_CLASS
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassNamespacesData()
    {
      const string classNameSpace = "TestLibrary.Classes";

      var data = new[]
      {
        new object[] { Constants.PUBLIC_CLASS, classNameSpace },
        new object[] { Constants.INTERNAL_CLASS, classNameSpace },
        new object[] { Constants.PUBLIC_NESTED_CLASS, $"{classNameSpace}.ClassParent" },
        new object[] { Constants.PROTECTED_NESTED_CLASS, $"{classNameSpace}.ClassParent"},
        new object[] { Constants.INTERNAL_NESTED_CLASS, $"{classNameSpace}.ClassParent"}
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassAccessorsData()
    {
      var data = new[]
      {
        new object[] {Constants.PUBLIC_CLASS, AccessorType.Public},
        new object[] {Constants.INTERNAL_CLASS, AccessorType.Internal},
        new object[] {Constants.PUBLIC_NESTED_CLASS, AccessorType.Public},
        new object[] {Constants.PROTECTED_NESTED_CLASS, AccessorType.Protected},
        new object[] {Constants.INTERNAL_NESTED_CLASS, AccessorType.Internal},
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassAbstractData()
    {
      var data = new[]
      {
        new object[] { Constants.PUBLIC_CLASS, false },
        new object[] { Constants.PUBLIC_CLASS_STATIC, false },
        new object[] { Constants.PUBLIC_CLASS_SEALED, false },
        new object[] { Constants.PUBLIC_CLASS_ABSTRACT, true }
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassSealedData()
    {
      var data = new[]
      {
        new object[] { Constants.PUBLIC_CLASS, false },
        new object[] { Constants.PUBLIC_CLASS_STATIC, false },
        new object[] { Constants.PUBLIC_CLASS_ABSTRACT, false },
        new object[] { Constants.PUBLIC_CLASS_SEALED, true }
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassStaticData()
    {
      var data = new[]
      {
        new object[] { Constants.PUBLIC_CLASS, false },
        new object[] { Constants.PUBLIC_CLASS_ABSTRACT, false },
        new object[] { Constants.PUBLIC_CLASS_SEALED, false },
        new object[] { Constants.PUBLIC_CLASS_STATIC, true }
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassBaseClassData()
    {
      var data = new[]
      {
        new object[] { Constants.PUBLIC_CLASS, false },
        new object[] { Constants.PUBLIC_INHERITING_COMPLEX_CLASS_BASE, true },
        new object[] { Constants.PUBLIC_INHERITING_COMPLEX_CLASS_BASE_EMPTY, true },
        new object[] { Constants.PUBLIC_INHERITING_COMPLEX_CLASS, false },
        new object[] { Constants.PUBLIC_INHERITING_COMPLEX_CLASS_EMPTY, false },
        new object[] { Constants.PUBLIC_INHERITING_CLASS_BASE, true },
        new object[] { Constants.PUBLIC_INHERITING_CLASS_BASE_EMPTY, true },
        new object[] { Constants.PUBLIC_INHERITING_CLASS_EMPTY, false },
        new object[] { Constants.PUBLIC_INHERITING_CLASS, false },
      };

      return data.ComposeData();
    }

    private static IEnumerable<object[]> GetClassGenericData()
    {
      var generics = new Dictionary<string, (Variance, IReadOnlyCollection<string>)>
      {
        { "T1", (Variance.NonVariant, new string[]{}) },
        { "T2", (Variance.NonVariant, new []{ nameof(IDisposable) }) },
      };

      var data = new[]
      {
        new object[] { Constants.PUBLIC_GENERIC_CLASS, generics },
        new object[] { Constants.PUBLIC_NESTED_GENERIC_CLASS, generics },
      };

      return data.ComposeData();
    }

    #endregion

    private static IClass? GetClass(IResolver resolver, string name)
    {
      resolver.Resolve(Constants.TEST_ASSEMBLY);

      return resolver
        .GetTypes<IClass>()
        .FirstOrDefault(type => type.Name.Equals(name));
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassNamesData))]
    public void ValidateClassNames(IResolver resolver, string name)
    {
      var query = GetClass(resolver, name);

      Assert.NotNull(query);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassNamespacesData))]
    public void ValidateClassRawNames(IResolver resolver, string name, string expected)
    {
      var query = GetClass(resolver, name);

      Assert.True(query?.RawName.Equals($"{expected}.{name}"), $"{resolver.GetType().FullName}: The '{name}' raw name is invalid. Expected '{expected}.{name}' != Actual '{query?.RawName}'.");
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassAccessorsData))]
    public void ValidateClassAccessors(IResolver resolver, string name, AccessorType accessor)
    {
      var query = GetClass(resolver, name);

      Assert.True(query?.Accessor == accessor, $"{resolver.GetType().FullName}: The '{name}' accessor type is invalid. Expected '{accessor}' != Actual '{query?.Accessor}'");
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassAbstractData))]
    public void ValidateClassAbstract(IResolver resolver, string name, bool expected)
    {
      var query = GetClass(resolver, name);

      Assert.Equal(expected, query?.IsAbstract);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassSealedData))]
    public void ValidateClassSealed(IResolver resolver, string name, bool expected)
    {
      var query = GetClass(resolver, name);

      Assert.Equal(expected, query?.IsSealed);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassStaticData))]
    public void ValidateClassStatic(IResolver resolver, string name, bool expected)
    {
      var query = GetClass(resolver, name);

      Assert.Equal(expected, query?.IsStatic);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassNamespacesData))]
    public void ValidateClassNamespace(IResolver resolver, string name, string expected)
    {
      var query = GetClass(resolver, name);

      Assert.True(query?.TypeNamespace.Equals(expected), $"{resolver.GetType().FullName}: The '{name}' namespace is invalid. Expected '{expected}' != Actual '{query?.TypeNamespace}'.");
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassBaseClassData))]
    public void ValidateClassHasBase(IResolver resolver, string name, bool expected)
    {
      var query = GetClass(resolver, name);

      Assert.Equal(expected, query?.BaseClass != null);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassGenericData))]
    public void ValidateClassGenericVariances(IResolver resolver, string name, Dictionary<string, (Variance variance, IReadOnlyCollection<string>)> generics)
    {
      var query = GetClass(resolver, name);

      var interfaceGenerics = query?.Generics
        .Select(item => (item.Key, item.Value.variance))
        .OrderBy(key => key.Key);

      var expectedGenerics = generics
        .Select(item => (item.Key, item.Value.variance))
        .OrderBy(key => key.Key);

      Assert.Equal(expectedGenerics, interfaceGenerics);
    }

    [Theory]
    [Trait("Category", nameof(IClass))]
    [MemberData(nameof(GetClassGenericData))]
    public void ValidateClassGenericConstraints(IResolver resolver, string name, Dictionary<string, (Variance, IReadOnlyCollection<string> constraints)> generics)
    {
      var query = GetClass(resolver, name);

      var actualGenerics = query?.Generics
        .Select(item => (item.Key, string.Join(";", item.Value.constraints.Select(constraint => constraint.DisplayName).OrderBy(Linq.XtoX))))
        .OrderBy(key => key.Key);

      var expectedGenerics = generics
        .Select(item => (item.Key, string.Join(";", item.Value.constraints.OrderBy(Linq.XtoX))))
        .OrderBy(key => key.Key);

      Assert.Equal(expectedGenerics, actualGenerics);
    }
  }
}
