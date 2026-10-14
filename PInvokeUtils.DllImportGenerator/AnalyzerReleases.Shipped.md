; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.6

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PI1001 | Naming | Error | Invalid class name
PI1002 |  | Error | Invalid attributes combination
PI1003 |  | Error | ImportPlatform specified multiple times
PI1004 |  | Error | Invalid AccessModifier specified
PI1005 | Naming | Error | Invalid namespace name
PI1006 |  | Error | Class name not provided

## Release 1.14

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PI1007 |  | Error | StaticCallMethod mismatch
PI1008 |  | Error | Extern methods in generic type
PI1009 |  | Warning | Extern methods without DllImport
PI1010 |  | Error | Partial method cannot be implemented
PI1011 |  | Error | RuntimeInformation not available
PI1012 |  | Warning | Method with DllImportFor ignored
PI1013 |  | Error | Generic method
PI1014 |  | Error | Invalid ImportPlatform
PI1015 |  | Error | DllImportFor without a library specified more times
PI1016 |  | Error | No ImportLibrary
PI1017 |  | Error | StringMarshalling not supported
