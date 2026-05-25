# Verification Report: 001

**Generated:** 2026-05-25T18:46:30.3258560+00:00
**Result:** FAILED

## `dotnet build JoyMon.sln`

- Exit code: 2
- Status: fail

```

build: -c: line 0: unexpected EOF while looking for matching `''
build: -c: line 1: syntax error: unexpected end of file

```

## `dotnet test JoyMon.Tests/JoyMon.Tests.csproj`

- Exit code: 2
- Status: fail

```

test: -c: line 0: unexpected EOF while looking for matching `''
test: -c: line 1: syntax error: unexpected end of file

```

## `echo "simulation: overworld smoke"`

- Exit code: 0
- Status: pass

```
simulation: overworld smoke
```

## Failures
- dotnet build JoyMon.sln (exit 2)
- dotnet test JoyMon.Tests/JoyMon.Tests.csproj (exit 2)
