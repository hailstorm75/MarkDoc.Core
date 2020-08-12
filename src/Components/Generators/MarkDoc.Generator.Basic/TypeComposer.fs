﻿namespace MarkDoc.Generator.Basic

open System
open Helpers
open MarkDoc.Documentation.Tags
open MarkDoc.Members.ResolvedTypes
open MarkDoc.Members.Members
open MarkDoc.Members.Types
open MarkDoc.Members
open MarkDoc.Documentation
open MarkDoc.Generator
open MarkDoc.Elements
open MarkDoc.Linkers
open MarkDoc.Helpers
open System.Collections.Generic
open MarkDoc.Members.Enums

type TypeComposer(creator, docResolver, memberResolve, linker) =
  let m_creator        : IElementCreator = creator
  let m_docResolver    : IDocResolver    = docResolver
  let m_memberResolver : IResolver       = memberResolve
  let m_linker         : ILinker         = linker

  let textNormal x = m_creator.CreateText(x, IText.TextStyle.Normal)
  let textBold   x = m_creator.CreateText(x, IText.TextStyle.Bold)
  let textItalic x = m_creator.CreateText(x, IText.TextStyle.Italic)
  let textCode   x = m_creator.CreateText(x, IText.TextStyle.Code)
  let textInline x = m_creator.CreateText(x, IText.TextStyle.CodeInline)

  let createHeadings headings =
    headings |> Seq.map textNormal

  let getTypeName (input : IType) =
    let joinGenerics (i : seq<string>) =
      let generics = i |> partial String.Join ", "
      "<" + generics + ">"
    let processInterface (input : IInterface) =
      let generics =
        input.Generics
        |> Seq.map (fun x -> (x.Value.ToTuple() |> fst |> varianceStr) + " " + x.Key)
      if not (Seq.isEmpty generics) then
        input.Name + joinGenerics generics
      else
        input.Name
    let processStruct (input : 'M when 'M :> IInterface) =
      let generics =
        input.Generics
        |> Seq.map (fun x -> x.Key)
      if not (Seq.isEmpty generics) then
        input.Name + joinGenerics generics
      else
        input.Name

    match input with
    | :? IClass as x -> processStruct x
    | :? IStruct as x -> processStruct x
    | :? IInterface as x -> processInterface x
    | _ -> input.Name

  let tryFindMember (input : IType, memberFull : string, memberCut : string) =
    let toMember (a: 'M when 'M :> IMember) =
      a :> IMember
    let findMember (a: 'M seq when 'M :> IMember) =
      a |> Seq.map toMember |> Seq.tryFind (fun x -> x.Name.Equals(memberCut))
    match input with
    | :? IInterface as i ->
      match memberFull.[0] with
      | 'M' -> i.Methods |> findMember
      | 'P' -> i.Properties |> findMember
      | 'E' -> i.Events |> findMember
      | _ -> None
    | :? IEnum as e -> e.Fields |> findMember
    | _ -> None

  let processReference (input: IType, reference : string) =
    let typeReference (reference : string) =
      let mutable result : IType = null
      if m_memberResolver.TryFindType(reference.[2..], &result) then
        m_creator.CreateLink(getTypeName result |> textNormal, lazy(m_linker.CreateLink(input, result))) :> ITextContent
      else
        let slice = reference.AsSpan(reference.LastIndexOf('.') + 1)
        let index = slice.IndexOf('`')
        if index <> -1 then
          let generateGenerics = 
            seq [ 1 .. (slice.Slice(index + 1).ToString() |> int) ]
            |> Seq.map (fun x -> "T" + x.ToString())

          String.Format("{0}<{1}>", slice.Slice(0, slice.IndexOf('`')).ToString(), String.Join(", ", generateGenerics)) |> textNormal :> ITextContent
        else
          slice.ToString() |> textNormal :> ITextContent

    let memberReference cutter =
      let memberString : string = cutter()
      let typeString = reference.[..reference.Length - memberString.Length - 2]
      let typeRef = typeReference typeString

      let memberAnchor = 
        let mutable result : IType = null
        if m_memberResolver.TryFindType(typeString.[2..], &result) then
          let mem = tryFindMember(result, reference, memberString)
          if Option.isSome mem then
            m_creator.CreateLink(memberString |> textNormal, m_linker.CreateAnchor(input, mem |> Option.get)) :> ITextContent
          else
            textNormal memberString :> ITextContent
        else
          textNormal memberString :> ITextContent

      m_creator.JoinTextContent(seq [ typeRef; memberAnchor ], ".") |> toTextContent

    let cutMethod() = 
      reference.Substring(reference.AsSpan(0, reference.IndexOf('(')).LastIndexOf('.') + 1)
    let cutMember() = 
      reference.Substring(reference.LastIndexOf('.') + 1)

    match reference.[0] with
    | 'T' -> typeReference reference
    | 'E' -> typeReference reference
    | 'M' -> memberReference cutMethod
    | 'P' -> memberReference cutMember
    | 'F' -> memberReference cutMember
    | _ -> reference.Substring(2) |> textNormal :> ITextContent

  let processResType (source: IType, item : IResType) =
    let tryLink (item : IResType) =
      let link = m_linker.CreateLink(source, item)
      if not (String.IsNullOrEmpty link) then
        m_creator.CreateLink(textInline item.DisplayName, lazy(link)) :> ITextContent
      else
        textInline item.DisplayName :> ITextContent

    // TODO: Generic arrays?
    match item with
    | :? IResGeneric as generic ->
      let generics = generic.Generics
                     |> Seq.map tryLink
      let content = seq [ tryLink generic; textNormal "<" :> ITextContent; m_creator.JoinTextContent(generics, ", "); textNormal ">" :> ITextContent ]
      m_creator.JoinTextContent(content, "")
    | _ -> tryLink item

  let processResType2(item : IResType) =
    let tryLink (item : IResType) =
      item.DisplayName

    // TODO: Generic arrays?
    match item with
    | :? IResGeneric as generic ->
      let generics = generic.Generics
                     |> Seq.map tryLink
      let content = seq [ tryLink generic; "<"; String.Join(", ", generics); ">" ]
      String.Join("", content)
    | _ -> tryLink item

  // TODO: Refactor
  let methodArguments (source: IType, item : IConstructor) =
    let argument (arg : IArgument) =
      let args = seq [ arg |> (argumentTypeStr >> textNormal) :> ITextContent; processResType(source, arg.Type); textNormal arg.Name :> ITextContent ]
      m_creator.JoinTextContent(args, " ")

    m_creator.JoinTextContent(item.Arguments |> Seq.map argument, ", ")

  // TODO: Refactor
  let methodArguments2(item : IMember) =
    let argument (arg : IArgument) =
      let args =
        seq [ arg |> argumentTypeStr; processResType2 arg.Type; arg.Name ]
        |> Seq.filter (String.IsNullOrEmpty >> not)
      String.Join(" ", args)
    let processArguments (args : IArgument seq) =
      String.Join(", ", args |> Seq.map argument)

    match item with 
    | :? IConstructor as c -> c.Arguments |> processArguments
    | :? IDelegate as d -> d.Arguments |> processArguments
    | _ -> raise (Exception())

  let rec processContent (input: IType, item : IContent) =
    let getInlineText (tag : IInnerTag) =
      tag.Content
      |> Seq.where(fun x -> x :? ITextTag)
      |> Seq.map (fun x -> (x :?> ITextTag).Content)

    let processColumn(column : seq<IContent>) =
      column
      |> Seq.map (fun x -> processContent(input, x))
      |> whereSome
      |> Seq.map toElement

    match item with
    | :? ITextTag as text -> Some(textNormal text.Content |> toElement)
    | :? IInnerTag as inner ->
      match inner.Type with
      | IInnerTag.InnerTagType.CodeSingle
        -> Some(getInlineText inner |> Seq.exactlyOne |> textInline |> toElement)
      | IInnerTag.InnerTagType.Code
        -> Some(getInlineText inner |> Seq.exactlyOne |> textCode |> toElement)
      | IInnerTag.InnerTagType.ParamRef
      | IInnerTag.InnerTagType.TypeRef
        -> Some(textInline inner.Reference |> toElement)
      | IInnerTag.InnerTagType.See
      | IInnerTag.InnerTagType.SeeAlso
        -> Some(processReference(input, inner.Reference) |> toElement)
      | IInnerTag.InnerTagType.Para
        -> Some(textNormal Environment.NewLine |> toElement)
      | _ -> None
    | :? IListTag as list ->
      match list.Type with
      | IListTag.ListType.Table ->
        let content = list.Rows
                      |> Seq.map (processColumn >> Linq.ToReadOnlyCollection)
        let headings = list.Headings
                       |> Seq.map (fun x -> processContent(input, x))
                       |> whereSome
                       |> Seq.filter(fun x-> x :? IText)
                       |> Seq.map(fun x -> x :?> IText)
        Some(m_creator.CreateTable(content, headings) |> toElement)
      | _ ->
        let content = list.Rows
                      |> Seq.collect id
                      |> Seq.map (fun x -> processContent(input, x))
                      |> whereSome

        Some(m_creator.CreateList(content, listType list.Type) |> toElement)
    | _ -> None

  let tagShort (input: IType, x : ITag) =
    let getCount =
      let isInvalid (item : IContent) =
        match item with
        | :? IListTag -> true
        | :? IInnerTag as tag ->
          match tag.Type with
          | IInnerTag.InnerTagType.See
          | IInnerTag.InnerTagType.SeeAlso
          | IInnerTag.InnerTagType.Code
          | IInnerTag.InnerTagType.InvalidTag -> true
          | _ -> false
        | _ -> false

      match (x.Content |> Seq.tryFindIndex isInvalid) with
      | None -> x.Content.Count
      | Some x -> x

    let count = getCount
    let readMore = if (count <> x.Content.Count) then Some(textNormal "..." |> toElement) else None
    let processed = x.Content
                    |> Seq.take count
                    |> Seq.map (fun x -> processContent(input, x))
    let content = seq [readMore]
                  |> Seq.append processed
                  |> whereSome
                  |> Seq.filter(fun x -> x :? ITextContent)
                  |> Seq.map(fun x -> x :?> ITextContent)

    m_creator.JoinTextContent(content, " ")

  let tagFull (input: IType, x : ITag) =
    let content = x.Content
                  |> Seq.map (fun x -> processContent(input, x))
                  |> whereSome

    let list = new LinkedList<ITextContent>()
    let result = seq [
      for item in content do
        if (item :? ITextContent && (not (item :? IText) || (item :?> IText).Style <> IText.TextStyle.Code)) then
          list.AddLast (item :?> ITextContent) |> ignore
        elif (list.Count = 0) then
          yield item
        else
          let joined = m_creator.JoinTextContent(list, " ") |> toElement
          list.Clear()

          yield joined
          yield item
    ]

    if (Seq.isEmpty result) then
      seq [ m_creator.JoinTextContent(list, " ") |> toElement ]
    else
      result

  let memberNameSummary(input: IType, name : ITextContent, summary : Option<ITag>) =
    match summary with
    | None -> name
    | Some x -> m_creator.JoinTextContent(seq [ name; tagShort(input, x) ], Environment.NewLine)

  let findTypeTag(input : IType, tag : ITag.TagType) =
    seq [
      let mutable typeDoc : IDocElement = null
      if (m_docResolver.TryFindType(input, &typeDoc)) then
        let mutable result : IReadOnlyCollection<ITag> = null
        if (typeDoc.Documentation.Tags.TryGetValue(tag, &result)) then
          result
    ]
    |> Seq.collect id

  let findTag(input : IType, mem : IMember, tag : ITag.TagType) =
    seq [
      let mutable typeDoc : IDocElement = null
      if (m_docResolver.TryFindType(input, &typeDoc)) then
        let mutable memberDoc : IDocMember = null
        if (typeDoc.Members.Value.TryGetValue(mem.RawName, &memberDoc)) then
          let mutable result : IReadOnlyCollection<ITag> = null
          if (memberDoc.Documentation.Tags.TryGetValue(tag, &result)) then
            result
    ]
    |> Seq.collect id

  let printIntroduction(input : IType) =
    match findTypeTag(input, ITag.TagType.Summary) |> Seq.tryExactlyOne with
    | None -> None
    | Some x -> Some(seq [ tagShort(input, x) :> IElement])

  let printMemberTables(input : IType) =
    let processInterface(input : IInterface) =
      let sectionHeading isStatic accessor section =
        seq [ accessorStr accessor; staticStr isStatic; section ]
        |> partial String.Join " "

      let createContent (members : seq<'M> when 'M :> IMember, newRow) =
        members
        |> Seq.groupBy (fun x-> x.Name)
        |> Seq.sortBy fst
        |> Seq.collect snd
        |> Seq.map newRow

      let createPropertySection(isStatic, accessor, properties : seq<IProperty>) =
        let createRow(property : IProperty) =
          let processName =
            let anchor = m_creator.CreateLink(textInline property.Name, m_linker.CreateAnchor(input, property)) |> toTextContent
            memberNameSummary(input, anchor, findTag(input, property, ITag.TagType.Summary) |> Seq.tryExactlyOne)

          seq [ processResType(input, property.Type); processName; m_creator.JoinTextContent(processMethods property |> Seq.map (fun x -> textInline x :> ITextContent), " ") ]
          |> Seq.map toElement
          |> Linq.ToReadOnlyCollection

        let grouped = createContent(properties, createRow)

        if (Seq.isEmpty properties) then
          None
        else
          m_creator.CreateTable(grouped, [ "Type"; "Name"; "Methods" ] |> createHeadings, sectionHeading isStatic accessor "properties", 3)
          :> IElement
          |> Some

      let createMethodSection(isStatic, accessor, methods : seq<IMethod>) =
        let methodsArray = methods |> Seq.toArray

        let createRow(method : IMethod) =
          let processReturn =
            let name = if isNull method.Returns then "void" else method.Returns.DisplayName
            let content = textInline name
            if isNull method.Returns then
              content :> ITextContent
            else
              let a = input :> IType
              let b = method.Returns
              let link = m_linker.CreateLink(a, b)
              if String.IsNullOrEmpty link then
                content :> ITextContent
              else
                m_creator.CreateLink(content, lazy(link)) :> ITextContent

          let processMethod =
            let hasOverloads =
              methodsArray
              |> Seq.where(fun x -> x.Name = method.Name)
              |> Seq.skip 1
              |> Seq.isEmpty
              |> not

            let signature =
              seq [
                match method.Operator with
                | OperatorType.Explicit ->
                  yield textInline "explicit" |> toTextContent
                  yield textNormal " " |> toTextContent
                  yield textInline "operator" |> toTextContent
                  yield textNormal " " |> toTextContent
                | OperatorType.Implicit ->
                  yield textInline "implicit" |> toTextContent
                  yield textNormal " " |> toTextContent
                  yield textInline "operator" |> toTextContent
                  yield textNormal " " |> toTextContent
                | OperatorType.Normal ->
                  yield textInline "operator" |> toTextContent
                  yield textNormal " " |> toTextContent
                | _ -> ()

                yield m_creator.CreateLink(textInline method.Name, m_linker.CreateAnchor(input, method)) |> toTextContent
                yield textNormal "(" |> toTextContent

                if hasOverloads then yield textInline "..." |> toTextContent
                else yield methodArguments(input,  method)

                yield textNormal ")" |> toTextContent
              ]
            let signatureText = m_creator.JoinTextContent(signature, "")
            memberNameSummary(input, signatureText, findTag(input, method, ITag.TagType.Summary) |> Seq.tryExactlyOne)

          seq [ processReturn; processMethod ]
          |> Seq.map toElement
          |> Linq.ToReadOnlyCollection

        let grouped = createContent(methodsArray |> Seq.distinctBy(fun x -> x.Name), createRow)

        if (Seq.isEmpty methods) then
          None
        else
          m_creator.CreateTable(grouped, [ "Returns"; "Name" ] |> createHeadings, sectionHeading isStatic accessor "methods", 3)
          :> IElement
          |> Some

      let processMembers item =
        item
        |> Seq.map flatten
        |> Seq.collect id

      let createTable x f =
        x
        |> groupMembers
        |> processMembers
        |> Seq.map f
        |> Seq.filter Option.isSome

      seq [
        (createTable input.Properties createPropertySection, "Properties");
        (createTable input.Methods createMethodSection, "Methods");
      ]
      |> Seq.filter (fst >> Seq.isEmpty >> not)
      |> Seq.map(fun x -> m_creator.CreateSection(x |> fst |> Seq.map Option.get, snd x, 2) |> toElement)

    match input with
    | :? IInterface as x -> processInterface x
    | _ -> Seq.empty
    |> emptyToNone

  let printDetailed(input : IType) =
    let single x =
      let tag = findTypeTag(input, x) |> Seq.tryExactlyOne
      if Option.isNone tag then
        None
      else
        tagFull(input, tag |> Option.get) |> emptyToNone

    let nested =
      let getNested (x : IStruct) =
        x.NestedTypes

      let groupByType (x : IType) =
        match x with
        | :? IClass
          -> "c" |> Some
        | :? IStruct
          -> "s" |> Some
        | :? IInterface
          -> "i" |> Some
        | :? IEnum
          -> "e" |> Some
        | _ -> None

      let processGroup (x : string option * seq<IType>) =
        let createTable (heading : string, group : seq<IType>) =
          m_creator.CreateList(group |> Seq.map (getTypeName >> textInline >> toElement), IList.ListType.Dotted, heading, 3)
          |> toElement
          |> Some

        match x |> (fst >> Option.get) with
        | "c" -> createTable("Classes", snd x)
        | "i" -> createTable("Interfaces", snd x)
        | "s" -> createTable("Structures", snd x)
        | "e" -> createTable("Enums", snd x)
        | _ -> None

      match input with
      | :? IStruct as x ->
           x
           |> getNested
           |> Seq.groupBy groupByType
           |> Seq.filter (fst >> Option.isSome)
           |> Seq.map processGroup
           |> whereSome
           |> emptyToNone
      | _ -> None

    let inheritance =
      let getInterfaces (x : 'M when 'M :> IInterface) =
        x.InheritedInterfaces
        |> Seq.map (fun x -> processResType(input, x) |> toElement)

      let createList l =
        if (Seq.isEmpty l) then
          None
        else
          seq [ m_creator.CreateList(l, IList.ListType.Dotted) |> toElement ] |> Some

      match input with
      | :? IClass as x ->
        let baseType = if (isNull x.BaseClass) then None else processResType(input, x.BaseClass) |> toElement |> Some
        let interfaces = getInterfaces x
        seq [ baseType ]
        |> whereSome
        |> Seq.append interfaces
        |> createList
      | :? IInterface as x ->
        x |> (getInterfaces >> createList)
      | _ -> None

    let typeParams =
      let getTypeParams =
        let processTag (x : ITag) =
          let generics = (input :?> IInterface).Generics
          let getConstraints (x : ITag) =
            if generics.ContainsKey(x.Reference) then
              let types = generics.[x.Reference].ToTuple()
                          |> snd
                          |> Seq.map (fun x -> processResType(input, x))
              m_creator.JoinTextContent(types, Environment.NewLine) |> Some
            else
              None

          let getName (x : ITag) =
            let result = seq [
              yield textInline x.Reference :> ITextContent
              if (generics.ContainsKey(x.Reference)) then
                let variance = generics.[x.Reference].ToTuple() |> fst
                if (variance <> Enums.Variance.NonVariant) then
                  yield variance |> varianceStr |> textInline :> ITextContent
            ]
            m_creator.JoinTextContent(result, " ") |> toElement

          let constraints = getConstraints x
          seq [
            yield getName x
            yield tagShort(input, x) |> toElement

            if (Option.isSome constraints) then
              yield constraints |> Option.get |> toElement
          ]

        if input :? IInterface then
          findTypeTag(input, ITag.TagType.Typeparam)
          |> Seq.map (processTag >> Linq.ToReadOnlyCollection)
          |> Some
        else
          None

      let ts = getTypeParams
      if (Option.isSome ts && ts |> (Option.get >> Seq.isEmpty >> not)) then
        seq [ m_creator.CreateTable(ts |> Option.get, seq [ "Type"; "Description"; "Constraints" ] |> createHeadings) |> toElement ] |> Some
      else
        None

    let getSingleTag (m : IMember, t : ITag.TagType) =
      let single (tags : ITag option) =
        match tags with
        | Some tag -> tagFull(input, tag) |> emptyToNone
        | _ -> None
      findTag(input, m, t)
      |> Seq.tryExactlyOne
      |> single

    let getExceptions (m : IMember) =
      let exceptions = findTag(input, m, ITag.TagType.Exception)
                       |> Seq.map (fun x -> seq [ processReference(input, x.Reference) |> toElement; tagShort(input, x) |> toElement ] |> Linq.ToReadOnlyCollection)

      if Seq.isEmpty exceptions then
        None
      else
        seq [ m_creator.CreateTable(exceptions, seq [ "Name"; "Description" ] |> createHeadings) |> toElement ] |> Some
    let getSeeAlso (m : IMember) =
      let seeAlsos = findTag(input, m, ITag.TagType.Seealso)
                     |> Seq.map (fun x -> tagShort(input, x) |> toElement)

      if Seq.isEmpty seeAlsos then
        None
      else
        seq [ m_creator.CreateList(seeAlsos, IList.ListType.Dotted) |> toElement ] |> Some
    let getArguments(c : IMember) = 
      let argumentDocs = findTag(input, c, ITag.TagType.Param)
                         |> Seq.map (fun x -> x.Reference, tagShort(input, x) |> toElement)
                         |> dict

      let processArguments (x : IArgument) =
        let getDescription =
          let mutable value : IElement = null
          seq [ if argumentDocs.TryGetValue(x.Name, &value) then yield value ]

        let argType = 
          let argType =seq [
            let argType = x |> argumentTypeStr
            if not (String.IsNullOrEmpty argType) then
              yield argType |> textInline :> ITextContent
          ]

          m_creator.JoinTextContent(seq [ processResType(input, x.Type) ] |> Seq.append argType, " ")

        let typeName = seq [ argType |> toElement; x.Name |> textNormal |> toElement ]
        getDescription
        |> Seq.append typeName
        |> Linq.ToReadOnlyCollection

      let generateResult (args : IArgument seq) =
        let arguments = args
                        |> Seq.map processArguments
        if (Seq.isEmpty args || Seq.isEmpty arguments) then
          None
        else
          seq [ m_creator.CreateTable(arguments, seq [ "Type"; "Name"; "Description" ] |> createHeadings) |> toElement ] |> Some

      match c with
      | :? IConstructor as x -> generateResult x.Arguments
      | :? IDelegate as x -> generateResult x.Arguments
      | _ -> raise (Exception())

    let getInheritedFrom(m : IMember) = 
      let getInheritance(x : IInterface) =
        let typeReference (t : IType) = 
          m_creator.CreateLink(getTypeName t |> textNormal, lazy(m_linker.CreateLink(input, t))) |> toElement

        let mutable result : IInterface = null
        if x.InheritedTypes.Value.TryGetValue(m, &result) then
          Some(seq [ typeReference result ])
        else
          None

      match input with
      | :? IInterface as i -> getInheritance i
      | _ -> None

    let constructors =
      let processCtors (ctors : IReadOnlyCollection<IConstructor>) =
        let overloads (members : 'a IReadOnlyCollection, i : int) =
          if members.Count > 1 then
            String.Format(" [{0}/{1}]", i + 1, members.Count)
          else
            ""
        let processCtor (i : int, ctor : IConstructor) =
          let signature =
            (ctor.Accessor |> accessorStr |> toLower) + (if ctor.IsStatic then " static " else " ") + ctor.Name + "(" + (methodArguments2 ctor) + ")"
            |> textCode
            |> toElement

          let content =
            seq [
              (getArguments ctor, "Arguments")
              (getSingleTag(ctor, ITag.TagType.Summary), "Summary")
              (getSingleTag(ctor, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(ctor, ITag.TagType.Example), "Example")
              (getExceptions ctor, "Exceptions")
              (getSeeAlso ctor, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          let joined = content
                       |> Seq.append (seq [ signature ])

          m_creator.CreateSection(joined, ctor.Name + overloads(ctors, i), 3) |> toElement

        ctors
        |> Seq.mapi (fun x y -> processCtor(x, y))

      match input with
      | :? IClass as x -> x.Constructors |> processCtors |> emptyToNone
      | _ -> None

    let methods = 
      let processMethods (methods : IMethod IReadOnlyCollection) =
        let overloads =
          methods
          |> Seq.groupBy (fun x -> x.Name)
          |> Seq.map (fun x -> (fst x, snd x |> Seq.mapi (fun x y -> (y.RawName, x)) |> dict))
          |> dict
        let processMethod (method : IMethod) =
          let getOverloads = 
            let ov = overloads.[method.Name]
            if ov.Count > 1 then
              String.Format(" [{0}/{1}]", ov.[method.RawName] + 1, ov.Count)
            else
              ""

          let signature =
            let getGenerics = 
              if Seq.isEmpty method.Generics then
                ""
              else
                String.Format("<{0}>", String.Join(", ", method.Generics))

            String.Format("{0}{1}{2} {3}{4}{5} {6}{7}({8})",
              (method.Accessor |> accessorStr |> toLower),
              (if method.IsStatic then " static" else ""),
              (if (method.Inheritance = MemberInheritance.Normal) then "" else " " + (inheritanceStr method.Inheritance)),
              (if method.IsAsync then "async " else ""),
              (
                match method.Operator with
                | OperatorType.Implicit ->
                  "implicit"
                | OperatorType.Explicit ->
                  "explicit"
                | OperatorType.None
                | OperatorType.Normal
                | _ ->
                  if isNull method.Returns then "void" else method.Returns.DisplayName
              ),
              (if method.Operator <> OperatorType.None then " operator" else ""),
              method.Name,
              getGenerics,
              (methodArguments2 method))
            |> textCode
            |> toElement
          let content =
            seq [
              (getArguments method, "Arguments")
              (getSingleTag(method, ITag.TagType.Summary), "Summary")
              (getSingleTag(method, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(method, ITag.TagType.Example), "Example")
              (getSingleTag(method, ITag.TagType.Returns), "Returns")
              (getExceptions method, "Exceptions")
              (getInheritedFrom method, "Inherited from")
              (getSeeAlso method, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          let joined = content
                       |> Seq.append (seq [ signature ])

          let name = (if method.Operator <> OperatorType.None then "Operator " else "") + method.Name + getOverloads
          m_linker.RegisterAnchor(method, lazy(name))
          m_creator.CreateSection(joined, name, 3) |> toElement

        methods
        |> Seq.map processMethod

      match input with
      | :? IInterface as x -> x.Methods |> processMethods |> emptyToNone
      | _ -> None

    let properties = 
      let processProperties (properties : IProperty IReadOnlyCollection) =
        let processProperty (property : IProperty) =
          let signature =
            String.Format("{0}{1}{2} {3} {4} {{ {5} }}",
              (property.Accessor |> accessorStr |> toLower),
              (if property.IsStatic then " static" else ""),
              (if (property.Inheritance = MemberInheritance.Normal) then "" else " " + (inheritanceStr property.Inheritance)),
              property.Type.DisplayName,
              property.Name,
              String.Join("; ", processMethods property))
            |> textCode
            |> toElement
          let content =
            seq [
              (getSingleTag(property, ITag.TagType.Summary), "Summary")
              (getSingleTag(property, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(property, ITag.TagType.Value), "Value")
              (getSingleTag(property, ITag.TagType.Example), "Example")
              (getExceptions property, "Exceptions")
              (getInheritedFrom property, "Inherited from")
              (getSeeAlso property, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          let joined = content
                       |> Seq.append (seq [ signature ])

          m_linker.RegisterAnchor(property, lazy(property.Name))
          m_creator.CreateSection(joined, property.Name, 3) |> toElement

        properties
        |> Seq.map processProperty
      match input with
      | :? IInterface as x -> x.Properties |> processProperties |> emptyToNone
      | _ -> None

    let events = 
      let processEvents (events : IEvent IReadOnlyCollection) =
        let processEvent (event : IEvent) =
          let signature =
            String.Format("{0}{1} event {2} {3}",
              (event.Accessor |> accessorStr |> toLower),
              (if event.IsStatic then " static" else ""),
              event.Type.DisplayName,
              event.Name)
            |> textCode
            |> toElement
          let content =
            seq [
              (getSingleTag(event, ITag.TagType.Summary), "Summary")
              (getSingleTag(event, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(event, ITag.TagType.Example), "Example")
              (getExceptions event, "Exceptions")
              (getInheritedFrom event, "Inherited from")
              (getSeeAlso event, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          let joined = content
                       |> Seq.append (seq [ signature ])

          m_linker.RegisterAnchor(event, lazy(event.Name))
          m_creator.CreateSection(joined, event.Name, 3) |> toElement

        events
        |> Seq.map processEvent

      match input with
      | :? IInterface as x -> x.Events |> processEvents |> emptyToNone
      | _ -> None

    let delegates = 
      let processDelegates (delegates : IDelegate IReadOnlyCollection) =
        let overloads =
          delegates
          |> Seq.groupBy (fun x -> x.Name)
          |> Seq.map (fun x -> (fst x, snd x |> Seq.mapi (fun x y -> (y.RawName, x)) |> dict))
          |> dict
        let processDelegate (deleg : IDelegate) =
          let getOverloads = 
            let ov = overloads.[deleg.Name]
            if ov.Count > 1 then
              String.Format(" [{0}/{1}]", ov.[deleg.RawName] + 1, ov.Count)
            else
              ""

          let signature =
            let getGenerics = 
              if Seq.isEmpty deleg.Generics then
                ""
              else
                String.Format("<{0}>", String.Join(", ", deleg.Generics))

            String.Format("{0}{1} delegate {2} {3}{4}({5})",
              (deleg.Accessor |> accessorStr |> toLower),
              (if deleg.IsStatic then " static" else ""),
              (if isNull deleg.Returns then "void" else deleg.Returns.DisplayName),
              deleg.Name,
              getGenerics,
              (methodArguments2 deleg))
            |> textCode
            |> toElement
          let content =
            seq [
              (getArguments deleg, "Arguments")
              (getSingleTag(deleg, ITag.TagType.Summary), "Summary")
              (getSingleTag(deleg, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(deleg, ITag.TagType.Example), "Example")
              (getSingleTag(deleg, ITag.TagType.Returns), "Returns")
              (getInheritedFrom deleg, "Inherited from")
              (getSeeAlso deleg, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          let joined = content
                       |> Seq.append (seq [ signature ])

          m_linker.RegisterAnchor(deleg, lazy(deleg.Name))
          m_creator.CreateSection(joined, deleg.Name, 3) |> toElement

        delegates
        |> Seq.map processDelegate

      match input with
      | :? IInterface as x -> x.Delegates |> processDelegates |> emptyToNone
      | _ -> None

    let enumFields = 
      let processFields(fields: IEnumField IReadOnlyCollection) =
        let processField (field: IEnumField) =
          let content =
            seq [
              (getSingleTag(field, ITag.TagType.Summary), "Summary")
              (getSingleTag(field, ITag.TagType.Remarks), "Remarks")
              (getSingleTag(field, ITag.TagType.Example), "Example")
              (getSingleTag(field, ITag.TagType.Returns), "Returns")
              (getSeeAlso field, "See also")
            ]
            |> Seq.filter (fst >> Option.isSome)
            |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 4) |> toElement)

          m_linker.RegisterAnchor(field, lazy(field.Name))
          m_creator.CreateSection((if Seq.isEmpty content then seq [ textNormal "" |> toElement ] else content), field.Name, 3) |> toElement

        fields
        |> Seq.map processField

      match input with
      | :? IEnum as e -> e.Fields |> processFields |> emptyToNone
      | _ -> None

    let sections =
      seq [
        (single ITag.TagType.Summary, "Summary");
        (single ITag.TagType.Remarks, "Remarks");
        (single ITag.TagType.Example, "Example");
        (typeParams, "Generic types");
        (inheritance, "Inheritance");
        (nested, "Nested types")
        (single ITag.TagType.Seealso, "See also")
        (constructors, "Constructors")
        (methods, "Methods")
        (properties, "Properties")
        (events, "Events")
        (delegates, "Delegates")
        (enumFields, "Fields")
      ]
      |> Seq.filter (fst >> Option.isSome)
      |> Seq.map(fun x -> m_creator.CreateSection(fst x |> Option.get, snd x, 2) |> toElement)

    sections |> emptyToNone

  let printContent (input : IType) =
    let createSection(x : seq<IElement>, y : string) =
      m_creator.CreateSection(x, y, 1)

    seq [
      (printIntroduction input, "Description");
      (printMemberTables input, "Members");
      (printDetailed input, "Details")
    ]
    |> Seq.filter (fst >> Option.isSome)
    |> Seq.map (fun x -> (x |> fst |> Option.get, x |> snd))
    |> Seq.map (createSection >> toElement)

  interface ITypeComposer with
    member __.Compose(input : IType) =
      if (isNull input) then
        raise (ArgumentNullException("input"))
      else
        m_creator.CreatePage(null, printContent input, getTypeName input)