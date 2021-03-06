﻿using dnlib.DotNet;
using MarkDoc.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using MarkDoc.Members.Dnlib.Properties;
using System.Collections.Concurrent;
using MarkDoc.Members.Dnlib.ResolvedTypes;
using MarkDoc.Members.Dnlib.Types;
using MarkDoc.Members.ResolvedTypes;
using MarkDoc.Members.Types;
using IType = MarkDoc.Members.Types.IType;
using TypeDef = dnlib.DotNet.TypeDef;
using MarkDoc.Members.Dnlib.Helpers;

namespace MarkDoc.Members.Dnlib
{
  /// <summary>
  /// Resolves assembly types using Dnlib library reflection
  /// </summary>
  public class Resolver
    : IResolver
  {
    private delegate IResType ResTypeInitializer(Resolver resolver, TypeSig signature,
      IReadOnlyDictionary<string, string>? generics, bool isByRef, IReadOnlyList<bool>? dynamicsMap,
      IReadOnlyList<string>? tupleMap);

    #region Fields

    private static readonly IReadOnlyDictionary<ElementType, ResTypeInitializer> ELEMENT_RES_TYPES;
    private static readonly HashSet<string> EXCLUDED_NAMESPACES = new HashSet<string> {"System", "Microsoft"};

    private readonly ConcurrentBag<IEnumerable<IGrouping<string, IReadOnlyCollection<IType>>>> m_groups =
      new ConcurrentBag<IEnumerable<IGrouping<string, IReadOnlyCollection<IType>>>>();

    private readonly ConcurrentDictionary<string, IResType> m_resCache = new ConcurrentDictionary<string, IResType>();
    private readonly Lazy<TrieNamespace> m_namespaces;

    private static readonly HashSet<string> RECORD_ATTRIBUTES = new HashSet<string>
    {
      "System.Runtime.CompilerServices.NullableAttribute",
      "System.Runtime.CompilerServices.NullableContextAttribute"
    };

    #endregion

    #region Properties

    /// <inheritdoc />
    public Lazy<IReadOnlyDictionary<string, IReadOnlyCollection<IType>>> Types { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    public Resolver()
    {
      // Transforms groupings of types into a dictionary
      IReadOnlyDictionary<string, IReadOnlyCollection<IType>> ComposeTypes()
        => m_groups
          // Flatten the collection
          .SelectMany(Linq.XtoX)
          // Create a dictionary of types grouped by their namespaces
          .ToDictionary(Linq.GroupKey, x => x.GroupValuesOfValues().ToReadOnlyCollection());

      Types = new Lazy<IReadOnlyDictionary<string, IReadOnlyCollection<IType>>>(ComposeTypes,
        LazyThreadSafetyMode.PublicationOnly);
      m_namespaces = new Lazy<TrieNamespace>(() => new TrieNamespace().AddRange(Types.Value.Keys),
        LazyThreadSafetyMode.PublicationOnly);
    }

    static Resolver()
    {
      ELEMENT_RES_TYPES = new Dictionary<ElementType, ResTypeInitializer>
      {
        {
          ElementType.SZArray,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResArray(resolver, signature, generics, dynamicsMap, tupleMap, isByRef)
        },
        {
          ElementType.Array,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResArray(resolver, signature, generics, dynamicsMap, tupleMap, isByRef)
        },
        {
          ElementType.Var,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResGenericValueType(resolver, signature, generics, isByRef)
        },
        {
          ElementType.MVar,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResGenericValueType(resolver, signature, generics, isByRef)
        },
        {
          ElementType.Boolean,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "bool", isByRef)
        },
        {
          ElementType.Object,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, dynamicsMap?.FirstOrDefault() ?? false ? "dynamic" : "object",
              isByRef)
        },
        {
          ElementType.String,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "string", isByRef)
        },
        {
          ElementType.Char,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "char", isByRef)
        },
        {
          ElementType.I1,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "sbyte", isByRef)
        },
        {
          ElementType.U1,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "byte", isByRef)
        },
        {
          ElementType.I2,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "short", isByRef)
        },
        {
          ElementType.U2,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "ushort", isByRef)
        },
        {
          ElementType.I4,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "int", isByRef)
        },
        {
          ElementType.U4,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "uint", isByRef)
        },
        {
          ElementType.I8,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "long", isByRef)
        },
        {
          ElementType.U8,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "ulong", isByRef)
        },
        {
          ElementType.R4,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "float", isByRef)
        },
        {
          ElementType.R8,
          (resolver, signature, generics, isByRef, dynamicsMap, tupleMap)
            => new ResValueType(resolver, signature, "double", isByRef)
        },
      };
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">When attempting to resolve after <see cref="Types"/> has been read</exception>
    /// <exception cref="FileNotFoundException">When the <paramref name="assembly"/> does not exist</exception>
    public void Resolve(string assembly)
    {
      static bool FilterNamespaces(IGrouping<string, TypeDef> grouping)
      {
        // Extract the grouping key
        var typeNamespace = Linq.GroupKey(grouping);
        // Return true if the namespace is not empty and is not excluded
        return !string.IsNullOrEmpty(typeNamespace)
               && !EXCLUDED_NAMESPACES.Contains(typeNamespace.Contains('.', StringComparison.InvariantCulture)
                 ? typeNamespace.Remove(typeNamespace.IndexOf('.', StringComparison.InvariantCulture))
                 : typeNamespace);
      }

      // If the resolved types were read..
      if (Types.IsValueCreated)
        // throw an exception because resolving new types is no longer allowed
        throw new InvalidOperationException(Resources.resolveAfterMaterializeForbidden);
      // If the provided assembly does not exist..
      if (!File.Exists(assembly))
        // throw an exception to halt the operation
        throw new FileNotFoundException(assembly);

      // Load the assembly
      var module = ModuleDefMD.Load(assembly);
      // Resolve and group assembly types:
      var group = module
        // Get the types within the assembly
        .GetTypes()
        // Filter out the types generated by the compiler
        .Where(type => !type.FullName.Equals("<Module>", StringComparison.InvariantCultureIgnoreCase))
        // Group types by their namespaces
        .GroupBy(type => type.Namespace.String)
        // Filter out the namespaces generated by the compile
        .Where(FilterNamespaces)
        // Group resolved types by their namespaces
        .GroupBy(Linq.GroupKey, grouping => grouping.SelectMany(ResolveTypes).ToReadOnlyCollection());

      // Add the resulting group to the collection
      m_groups.Add(group);
    }

    internal IResType Resolve(TypeSig signature,
      IReadOnlyDictionary<string, string>? generics = null,
      ParamDef? metadata = null)
      => Resolve(signature, generics, false, metadata?.GetDynamicTypes(signature), metadata?.GetValueTupleNames());

    /// <summary>
    /// Resolves type to a <see cref="IResType"/>
    /// </summary>
    /// <param name="signature">Type to resolve</param>
    /// <param name="generics">Dictionary of type generics</param>
    /// <param name="isByRef">Is the resolved type a reference type</param>
    /// <param name="dynamicsMap">Map indicating what types are dynamic</param>
    /// <param name="tupleMap">Map of value tuple names</param>
    /// <returns>Resolved type</returns>
    /// <exception cref="ArgumentNullException">If the <paramref name="signature"/> argument is null</exception>
    /// <exception cref="NotSupportedException">If the <paramref name="signature"/> is not a <see cref="TypeSig"/></exception>
    internal IResType Resolve(TypeSig signature,
      IReadOnlyDictionary<string, string>? generics,
      bool isByRef,
      IReadOnlyList<bool>? dynamicsMap = null,
      IReadOnlyList<string>? tupleMap = null)
    {
      string GetKey(IFullName sig)
        => sig.FullName + (dynamicsMap is null ? string.Empty : $"${string.Join(string.Empty, dynamicsMap)}");

      // If the signature is null..
      if (signature is null)
        // throw an exception
        throw new ArgumentNullException(nameof(signature));

      // Get the type name
      var key = GetKey(signature);

      // If the type was cached..
      if (m_resCache.TryGetValue(key, out var resolution))
        // return the cached type
        return resolution!;

      // If the type is by reference..
      if (isByRef)
        // retrieve the referenced type
        signature = signature.Next;

      // Resolve the type based on what it is
      var result = ProcessElementByType(signature, generics, isByRef, dynamicsMap, tupleMap);

      // Cache the resolved type
      m_resCache.AddOrUpdate(key, result, (x, y) => result);

      // Return the resolved type
      return result;
    }

    private IResType ProcessElementByType(TypeSig signature, IReadOnlyDictionary<string, string>? generics,
      bool isByRef, IReadOnlyList<bool>? dynamicsMap, IReadOnlyList<string>? tupleMap)
    {
      if (ELEMENT_RES_TYPES.TryGetValue(signature.ElementType, out var resType))
        return resType!(this, signature, generics, isByRef, dynamicsMap, tupleMap);

      if (signature.ElementType is ElementType.GenericInst && IsGeneric(signature))
        return IsTuple(signature, out var valueTuple)
          ? new ResTuple(this, signature, valueTuple, generics, dynamicsMap, tupleMap, isByRef)
          : new ResGeneric(this, signature, generics, dynamicsMap, isByRef) as IResType;
      if (signature.ElementType is ElementType.ByRef || signature.ElementType is ElementType.CModReqd)
        return Resolve(signature, generics, true, dynamicsMap, tupleMap);
      if (signature.ElementType is ElementType.ValueType &&
          signature.FullName.Equals("System.Decimal", StringComparison.InvariantCulture))
        return new ResValueType(this, signature, "decimal", isByRef);

      return new ResType(this, signature);
    }

    private static bool IsGeneric(dnlib.DotNet.IType source)
      => source.ReflectionName.Contains('`', StringComparison.InvariantCulture);

    private static bool IsTuple(dnlib.DotNet.IType source, out bool isValueTuple)
    {
      isValueTuple = default;
      // Extract the type name
      var name = source.ReflectionName.Remove(source.ReflectionName.IndexOf('`', StringComparison.InvariantCulture));

      // If the type is a tuple..
      if (name.Equals(nameof(Tuple), StringComparison.InvariantCulture))
        // return true
        return true;

      // If the type is a ValueTuple..
      if (name.Equals(nameof(ValueTuple), StringComparison.InvariantCulture))
      {
        // note that it is a value tuple
        isValueTuple = true;
        // return true
        return true;
      }

      // The type is not a tuple
      return false;
    }

    private static TypeDef? ResolveParent(object? parent)
    {
      if (parent is null) return null;
      if (!(parent is TypeDef type))
        throw new InvalidOperationException($"Argument type of {parent} is not {nameof(TypeDef)}.");

      return type;
    }

    /// <summary>
    /// Links a <paramref name="type"/> instance to a <see name="IType"/> instance
    /// </summary>
    /// <param name="source">Source of <paramref name="type"/></param>
    /// <param name="type">Type to link to</param>
    /// <remarks>
    /// This method can be called after of the <see cref="Types"/> have been resolved.
    /// Calling during resolution of <see cref="Types"/> will render incorrect results.
    /// <para/>
    /// Utilize lazy loading to overcome this issue
    /// </remarks>
    /// <returns>Linked <see name="IType"/> instance. Null if unresolved.</returns>
    /// <exception cref="InvalidOperationException">When attempting to access <see cref="Types"/> too early</exception>
    /// <exception cref="ArgumentNullException">If the <paramref name="source"/> argument is null</exception>
    /// <exception cref="NotSupportedException">If the <paramref name="source"/> is not a <see cref="TypeSig"/></exception>
    internal IType? FindReference(object source, IResType type)
    {
      // If the signature is null..
      if (source is null)
        // throw an exception
        throw new ArgumentNullException(nameof(source));

      // If the signature is not a supported type..
      if (!(source is TypeSig signature))
        // throw an exception
        throw new NotSupportedException(Resources.sourceNotTypeSignature);

      // If there are no resolved types..
      if (!Types.IsValueCreated
          || Types.Value.Count == 0)
        // throw an exception
        throw new InvalidOperationException(Resources.linkBeforeAllResolvedForbidden);

      // If a type is matched by namespace..
      if (Types.Value.ContainsKey(signature.Namespace))
        // find and return a matching link by name
        return Types.Value[signature.Namespace]
          .FirstOrDefault(x => x.RawName.Equals(type.RawName, StringComparison.InvariantCulture));

      // Return null if link is not resolved
      return null;
    }

    /// <summary>
    /// Resolves given <paramref name="subject"/> to a type
    /// </summary>
    /// <param name="subject">Subject to resolve</param>
    /// <param name="parent">Parent of <paramref name="subject"/></param>
    /// <returns>Resolved type</returns>
    /// <exception cref="ArgumentNullException">If the <paramref name="subject"/> argument is null</exception>
    /// <exception cref="NotSupportedException">If the <paramref name="subject"/> is not a <see cref="TypeSig"/></exception>
    internal IType ResolveType(object subject, object? parent = null)
    {
      // If the subject is null..
      if (subject is null)
        // throw an exception
        throw new ArgumentNullException(nameof(subject));

      // If the subject is not a supported type..
      if (!(subject is TypeDef subjectSig))
        // throw an exception
        throw new NotSupportedException(Resources.sourceNotTypeSignature);

      // Resolve the subjects parent
      var nestedParent = ResolveParent(parent);

      if (subjectSig.IsEnum)
        return new EnumDef(this, subjectSig, nestedParent);
      if (subjectSig.IsValueType)
        return new StructDef(this, subjectSig, nestedParent);
      if (subjectSig.IsClass)
      {
        return IsRecord(subjectSig)
          ? new RecordDef(this, subjectSig, nestedParent)
          : new ClassDef(this, subjectSig, nestedParent);
      }

      if (subjectSig.IsInterface)
        return new InterfaceDef(this, subjectSig, nestedParent);

      // Throw an exception since the subject is none of the supported types
      throw new NotSupportedException(Resources.subjectNotSupported);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If the <paramref name="fullname"/> argument is null</exception>
    /// <exception cref="InvalidOperationException">When attempting to access <see cref="Types"/> to early</exception>
    public bool TryFindType(string fullname, out IType? result)
    {
      // If the type name is null..
      if (fullname is null)
        // throw an exception
        throw new ArgumentNullException(nameof(fullname));

      // If there are no resolved types..
      if (!Types.IsValueCreated
          || Types.Value.Count == 0)
        // throw an exception
        throw new InvalidOperationException(Resources.linkBeforeAllResolvedForbidden);

      // Assume no type is found
      result = null;

      // If either the namespace is unknown..
      if (!m_namespaces.Value.TryFindKnownNamespace(fullname, out var ns)
          // or the type does not exist..
          || !Types.Value.TryGetValue(ns, out var types))
        // Return false
        return false;

      // Locate the type based on its name
      result = types!.FirstOrDefault(x => x.RawName.Equals(fullname, StringComparison.InvariantCulture));

      // Return true if a type was found
      return result != null;
    }

    private IEnumerable<IType> ResolveTypes(TypeDef subject)
    {
      // If the subject is an enum..
      if (subject.IsEnum)
      {
        // return a resolved enum
        yield return new EnumDef(this, subject, null);
        // exit
        yield break;
      }

      // Resolve subject to a type
      var type = GetTypeWithNested(this, subject);
      // Return the resolved type
      yield return type;
      // Iterate over its nested types and..
      foreach (var item in IterateNested(type))
        // return them
        yield return item;
    }

    private static bool IsRecord(TypeDef type)
    {
      if (!type.IsClass)
        return false;

      if (type.CustomAttributes
            .Select(x => x.TypeFullName)
            .All(x => RECORD_ATTRIBUTES.Contains(x))
          || type.Properties.FirstOrDefault(prop => prop.Name.Equals("EqualityContract"))
            ?.GetMethod.CustomAttributes.FirstOrDefault(attr =>
              attr.TypeFullName.Equals("System.Runtime.CompilerServices.CompilerGeneratedAttribute")) != null
          || type.Methods.FirstOrDefault(meth => meth.Name.Equals("<Clone>$")) != null)
        return true;

      return false;
    }

    private static IInterface GetTypeWithNested(Resolver resolver, TypeDef source)
    {
      if (source.IsValueType) return new StructDef(resolver, source, null);
      if (source.IsClass)
        return IsRecord(source)
          ? new RecordDef(resolver, source, null)
          : new ClassDef(resolver, source, null);

      if (source.IsInterface) return new InterfaceDef(resolver, source, null);

      // The provided signature is not supported
      throw new NotSupportedException(Resources.subjectNotSupported);
    }

    private static IEnumerable<IType> IterateNested(IInterface type)
    {
      // If there are no nested types..
      if (!type.NestedTypes.Any())
        // exit
        yield break;

      // For each nested type..
      foreach (var nested in type.NestedTypes)
      {
        // Return the type
        yield return nested;
        // If the nested type can't have its own nested types..
        if (!(nested is IInterface nestedType))
          // continue to the next nested type
          continue;

        // Otherwise for nested types within the given nested type..
        foreach (var nestedNested in IterateNested(nestedType))
          // return them
          yield return nestedNested;
      }
    }

    #endregion
  }
}