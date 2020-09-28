﻿namespace MarkDoc.Generator.Basic

open MarkDoc.Members.Members
open MarkDoc.Members.Enums
open System

module internal SignatureHelpers =
  /// <summary>
  /// Gets the generics from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IMethod"/> and <see cref="IDelegate"/> member types
  /// </remarks>
  let getGenerics (input: IMember) =
    // Get the generics from the given input member
    let generics = match input with
                   | :? IMethod as method -> method.Generics.Keys
                   | :? IDelegate as del -> del.Generics.Keys
                   | _ -> Seq.empty

    // If there are no generics..
    if Seq.isEmpty generics then
      // return empty
      ""
    // Otherwise..
    else
      // format the generics
      String.Format("<{0}>", String.Join(", ", generics))

  /// <summary>
  /// Gets the accessor from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  let getAccessor (input: IMember) =
    // Get the member accessor
    input.Accessor
    // Convert it to a string
    |> StringConverters.accessorStr
    // Ensure the string is lowercase
    |> StringConverters.toLower

  /// <summary>
  /// Gets the operator keyword from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IMethod"/> member types
  /// </remarks>
  let getOperator (input: IMember) =
    match input with
    | :? IMethod as method ->
      if method.Operator <> OperatorType.None then " operator" else ""
    | _ -> ""

  /// <summary>
  /// Gets the name of the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  let getName (input: IMember) =
    input.Name

  /// <summary>
  /// Gets the static keyword from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  let getStatic (input: IMember) =
    if input.IsStatic then " static" else ""

  /// <summary>
  /// Gets the return type from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IMethod"/>, <see cref="IDelegate"/>, <see cref="IEvent"/>, and <see cref="IProperty"/> member types
  /// </remarks>
  let getReturn (input: IMember) =
    match input with
    | :? IMethod as method ->
      // If the method is a conversion operator, replace the return type with a keyword
      match method.Operator with
      | OperatorType.Implicit -> "implicit"
      | OperatorType.Explicit -> "explicit"
      | OperatorType.None
      | OperatorType.Normal
      | _ -> if isNull method.Returns then "void" else method.Returns.DisplayName
    | :? IDelegate as deleg->
      if isNull deleg.Returns then "void" else deleg.Returns.DisplayName
    | :? IEvent as ev ->
      ev.Type.DisplayName
    | :? IProperty as prop ->
      prop.Type.DisplayName
    | _ -> ""

  /// <summary>
  /// Gets the async keyword from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IMethod"/> member types
  /// </remarks>
  let getAsync (input: IMember) =
    match input with
    | :? IMethod as method ->
      if method.IsAsync then "async " else ""
    | _ -> ""

  /// <summary>
  /// Gets from where the given <paramref name="input"/> member is inherited
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IMethod"/> and <see cref="IProperty"/> member types
  /// </remarks>
  let getInheritance (input: IMember) =
    let processInheritance inheritance =
      // If the member has no inheritance traits..
      if (inheritance = MemberInheritance.Normal) then
        // return nothing
        ""
      // Otherwise..
      else
        // convert the trait to a string and return it
        " " + (StringConverters.inheritanceStr inheritance)

    match input with
    | :? IMethod as method -> method.Inheritance |> processInheritance
    | :? IProperty as property -> property.Inheritance |> processInheritance
    | _ -> ""

  /// <summary>
  /// Gets the property method keywords from the given <paramref name="input"/> member
  /// </summary>
  /// <param name="input">Member to process</param>
  /// <remarks>
  /// Valid only for <see cref="IProperty"/> member types
  /// </remarks>
  let getPropertyMethods (input: IMember) =
    match input with
    | :? IProperty as property ->
      let accessor acc =
        // If the given getter or setter accessor is equal to the property accesor, exclude the keyword
        match acc with
        | AccessorType.Protected -> if property.Accessor.Equals acc then "" else "protected "
        | AccessorType.Internal -> if property.Accessor.Equals acc then "" else "internal "
        | _ -> ""
      String.Join("; ", seq [
        // If the property has a getter..
        if property.GetAccessor.HasValue then
          // return it
          yield (accessor property.GetAccessor.Value) + "get"
        // If the property has a setter..
        if property.SetAccessor.HasValue then
          // return it
          yield (accessor property.SetAccessor.Value) + "set"
      ])
    | _ -> ""

  /// <summary>
  /// Generates signature for the given <paramref name="input"/> member using the specified <paramref name="format"/> and <paramref name="processors"/>
  /// </summary>
  /// <param name="format">The formatting of the signature</param>
  /// <param name="processors">Processors which extract the signature parts from the given member</param>
  /// <param name="input">The member for which the signature is to be generated</param>
  let generateSignature format (processors: (IMember -> string) seq) input =
    let x input (col: obj[]) =
      String.Format(input, col)

    x format (processors |> Seq.map(fun x -> x input :> obj) |> Seq.toArray) |> Code

