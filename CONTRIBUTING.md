# Contributing

Pull requests are welcome. If you're planning something big, open an issue first so we can talk about it.

---

## Setup

You need Windows. The project targets .NET Framework 4.8, so grab the [Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) if you don't have it, or just use Visual Studio 2019/2022 with the .NET Desktop workload — it includes everything.

Fork the repo, clone it:

```powershell
git clone https://github.com/YOUR-USERNAME/Extreme-Injector-Open-Source.git
cd Extreme-Injector-Open-Source
```

Build:

```powershell
dotnet build ./src/ExtremeInjectorReplica/ExtremeInjectorReplica.csproj -c Release
```

Output ends up at `src/ExtremeInjectorReplica/bin/Release/net48/Extreme Injector v3.exe`.

---

## A few things to keep in mind

- Keep it standalone. No third-party DLL dependencies without discussion.
- P/Invoke declarations go in `Core/NativeMethods.cs`.
- UI dialogs and controls go under `UI/`.
- Standard C# naming: PascalCase for classes and methods, camelCase for locals.

---

## Submitting a PR

Branch off `main`, do your thing, push, open a PR. GitHub Actions will build it automatically and flag any compile errors.

```powershell
git checkout -b my-feature
# ...make changes...
git push origin my-feature
```

---

## Bugs & Feature Requests

Use the issue templates. For bugs, include your Windows version and the exact steps to reproduce.
