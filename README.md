# Py#

> [!IMPORTANT]
> Language is not finished. Try to visit it later :)

Py# (PySharp) is an implementation of the [Python language](https://www.python.org/) on the [.NET platform](https://dotnet.microsoft.com/). Unlike [IronPython](https://ironpython.net/), my goal is to make *Python-first* compiler. It means that Py# wouldn't contain runtime that tries to include tools for all dynamic languages of the Iron-family and focus only on Python semantics specifically.

## Motivation of the project

First of all, it is a sort of the learning project cause i am new in the programming and language creating. But it doesn't mean that i won't develop it properly.
Also i consider Py# as a long-term project. At least i would try to support it as long as it gives me feedback would it be just green tests or something another. So if you want to support this project too, even small activity would be a big motivation for me :)

In the second i have goal to make good alternative for the [Python.NET](https://pythonnet.github.io/) library, IronPython and another projects that tries to make .NET friend for Python. It doesn't means that these projects are bad, but they are not native for the modern .NET with all of AOT and source generation stuff. And i want to try fix it.

## State of the project

### Here is the my current goals:
- [ ] Run `print("Bau Bau!")`
- [ ] Turing complete
- [ ] Basic classes, functions, simple object semantics
- [ ] Bytecode VM (on top of the CLR)
- [ ] Calling CLR code
- [ ] Full Python syntax support
- [ ] Exporting code to CLR
- [ ] Pure Python support
- [ ] Std re-implementation (using .NET std as backend)
- [ ] Another cool stuff

### Already implemented:
- Tokenizer supporting full Python spec.
- PEG-parser generator (inspired by CPython's [pegen](https://github.com/we-like-parsers/pegen/)).

### Currently working on:

Python parser & improving parser generator.

## Getting started

**Requirements**: .NET10

#### Install repository locally:

```bash
git clone https://github.com/NiyazBiyaz/pysharp
```

#### Get latest changes:

```bash
git pull
```

#### Run tests:

```bash
dotnet test
```

#### Folders structure

```
pysharp
├── PySharp.Benchmarks                          -- Speed tests
├── src
│   ├── PySharp                                 -- Main project of the language
│   │   └── Runtime                             -- Draft of the runtime library
│   ├── PySharp.SyntaxAnalysis.Common           -- Base library for PEG generator
│   ├── PySharp.SyntaxAnalysis.Generator        -- PEG-Generator
│   └── PySharp.SyntaxAnalysis.Tokens           -- Tokenizer
├── tests
│   ├── PySharp.SyntaxAnalysis.Generator.Tests  -- Test for the PEG parser generator
│   └── PySharp.Tests
│       ├── Data                                -- Tests data
│       └── SyntaxAnalysis                      -- Tests for parsing & lexing
└── Tools
```

---

## License

This project is licensed under the MIT License. See *LICENSE* file for full text.