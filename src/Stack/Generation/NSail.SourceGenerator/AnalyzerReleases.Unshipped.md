; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NSG001 | NSail.Generation | Error | A route token has no route-bound property on the message.
NSG002 | NSail.Generation | Error | A [Pushed] type does not implement IMessage.
NSG003 | NSail.Generation | Error | A [Pushed] event declares a public property.
NSG004 | NSail.Generation | Error | An organization axis would be mapped by name alone.
NSG005 | NSail.Generation | Error | A [Primitive] member's type differs from its source's.
NSG006 | NSail.Generation | Error | A mapped member has no conversion from its source.
NSG007 | NSail.Generation | Error | A mapped type has no parameterless constructor.
NSG008 | NSail.Generation | Error | [MapFrom] names a type that is not a class.
NSG009 | NSail.Generation | Error | A projection cannot carry one of its source's derived types.
NSG010 | NSail.Generation | Error | [MapFrom] or [MapTo] is declared on an open generic type.
