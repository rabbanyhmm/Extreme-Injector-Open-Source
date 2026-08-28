# Contributing to Extreme Injector

Thank you for your interest in contributing to **Extreme Injector (Open Source Replica)**! We welcome bug reports, feature requests, code contributions, and documentation improvements.

---

## 🛠️ Local Development Setup

### Prerequisites
- **Operating System:** Windows 10 or Windows 11 (x86 / x64)
- **Development Tools:** 
  - [.NET SDK 8.0+](https://dotnet.microsoft.com/download) (or Visual Studio 2022 with *.NET Desktop Development* workload)
  - [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48)

### Getting the Code

1. Fork the repository on GitHub.
2. Clone your fork locally:
   ```powershell
   git clone https://github.com/YOUR-USERNAME/Extreme-Injector-Open-Source.git
   cd Extreme-Injector-Open-Source
   ```

### Building the Project

Run `dotnet build` from the command line:

```powershell
# Build in Release configuration
dotnet build ./src/ExtremeInjectorReplica/ExtremeInjectorReplica.csproj -c Release
```

The output executable will be compiled to:
`src/ExtremeInjectorReplica/bin/Release/net48/Extreme Injector v3.exe`

---

## 📝 Guidelines & Code Style

- **C# Formatting:** Follow standard C# naming conventions (PascalCase for methods/classes, camelCase for parameters/local variables).
- **Zero External Dependencies:** Keep the application standalone — do not add third-party DLL dependencies unless discussed first.
- **P/Invoke & Native Methods:** Place Win32/NTAPI P/Invoke signatures inside `Core/NativeMethods.cs`.
- **UI & Themes:** Place UI controls and WinForms dialogs under `UI/`.

---

## 🚀 Submitting a Pull Request

1. Create a feature branch:
   ```powershell
   git checkout -b feature/my-new-feature
   ```
2. Commit your changes with clear, human-readable commit messages.
3. Push to your fork:
   ```powershell
   git push origin feature/my-new-feature
   ```
4. Open a **Pull Request** against the `main` branch. GitHub Actions will automatically verify that your code compiles cleanly!

---

## 🐛 Reporting Bugs & Suggesting Features

- Open an issue using the provided **Bug Report** or **Feature Request** template.
- Provide step-by-step reproduction instructions and Windows OS version details for bug reports.
