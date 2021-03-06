[![.NET](https://github.com/hailstorm75/MarkDoc.Core/actions/workflows/dot-core.yml/badge.svg)](https://github.com/hailstorm75/MarkDoc.Core/actions/workflows/dot-core.yml) [![CodeFactor](https://www.codefactor.io/repository/github/hailstorm75/markdoc.core/badge)](https://www.codefactor.io/repository/github/hailstorm75/markdoc.core) ![GitHub issues](https://img.shields.io/github/issues/hailstorm75/MarkDoc.Core) ![GitHub repo size](https://img.shields.io/github/repo-size/hailstorm75/MarkDoc.Core)

# MarkDoc.Core
Markdown documentation generator for .NET libraries written in C# 9 and lower.

Additional details and a list of supported versions of .NET frameworks will be provided later.

In the current project state, the `Members` library is considered stable with 95% test coverage and can be used as a reflection library outside of this project that makes exploring assembly types much easier thanks a higher level of abstraction.
The `Documentation` library is still to be tested and is considered to be unstable; nevertheless, the manual tests proved that most common assemblies will have their documentation parsed correctly.
The biggest task, aside from testing all of the core libraries, is to create a modular UI that is, preferrably, cross-platform.

Like this project idea and would like to see it grow? Give it a star and follow for the latest updates.

## Produced result

This is my second attempt at creating such a tool. The first one is on [GitLab](https://gitlab.com/hailstorm75/markdoc). You can try it out for your self; however, it only supports .NET Framework libraries with NO DEPENDENCIES.

The result which it produces can be seen in the Wiki of my side project - [Common](https://gitlab.com/hailstorm75/Common/-/wikis/home). All of the Wiki content is generated using the legacy MarkDoc.

The generated structure is inspired by the one outputted by Doxygen. If you do not like it, you can always create your own **Generator** (see below in the technical description) to generate your own custom output.

## Running it

As of writing this ReadMe, there is no user-friendly way of running this project. However, you can play around with the benchmark test in the console application.

To do so, supply the paths to your dll's, exe's, and the output directory path in the `MarkDoc.Console` `Program`. You will find the respective fields there: `OUTPUT` and `DLL_PATHS`.

Run it at your own risk, the code is meant to serve for development purposes.

## Technical description

This project aims to be as modular as possible to support specifics of each **Git** platform and, if so be desired, to generate not only Markdown but other output types such as HTML, LaTeX, or whatever might be required in the future.
With this in mind, the project is separated into the following **component** types:

| Part | Description |
| ---- | ----------- |
| Members | Retrieve library types structure |
| Documentation | Retrieve library types documentation |
| Elements | Documentation building blocks |
| Generators | Binds the types to their documentation and generates the documentation output |
| Linkers | Define the documentation file output structure and allow linking types between files |
| Printers | Saves the generated output to files |

The parts above are represented as interfaces and thus allow creating decoupled component implementations.

### Relational chart

> Note: may vary depending on component implementations

```
             +--------------+
             | Input folder |
             ++------------++
     DLL, EXE |            | XML
              v            v
+-------------+---+    +---+-------------+
|     Members     |    |  Documentation  |
+-------------+---+    +---+-------------+
        IType |            | IDocElement
              v            v
       +------+------------+-----+
+------+------+            +-----+-------+
|   Elements  |  Composers |   Linkers   |
+------+------+            +-----+-------+
       +------------+------------+
                    | IElement
                    v
              +-----+-------+
              |  Printers   |
              +-----+-------+
                    | Documentaion file (.md, .html, ...)
                    v
             +------+--------+
             | Output folder |
             +---------------+

```

# Roadmap

The project is in its early stages of development.

| Stage | Status   | Milestone | Description |
| ----- | -------- | --------- | ----------- |
| Core  | :heavy_check_mark: | [Issues](https://github.com/hailstorm75/MarkDoc.Core/milestone/1) | Define the core interfaces and create components which implement them. The goal is to ensure that the interfaces provide everything necessary and to successfully generate documentation |
| Reorganize | :heavy_check_mark: | [Issues](https://github.com/hailstorm75/MarkDoc.Core/milestone/2)  | Reorganize the project structure such that the interfaces are separate from the components which implement them. Document the project core and cover it with unit tests |
| C# 9 support | :heavy_check_mark: | [Issues](https://github.com/hailstorm75/MarkDoc.Core/milestone/6) | Add support for new C#9 features |
| Add unit tests | :hammer: | [Issues](https://github.com/hailstorm75/MarkDoc.Core/milestone/7) | Cover the documentation layer with Unit Tests  |
| Smart settings   | :mag: |    | Create a framework for settings which will be propagated through plugins to components and will be displayed in the application UI |
| Plugins | :grey_question: |  | Create a MarkDoc plugin composed from __components__ |
| UI   | :grey_question: |    | Create the application UI |

**Legend**

| Icon               | Description       |
| ------------------ | ----------------- |
| :grey_question:    | To be processed   |
| :mag:              | Analysis phase    |
| :hammer:           | Development phase |
| :heavy_check_mark: | Complete          |
