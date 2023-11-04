using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures.Types;
using System.Diagnostics;

namespace SDKGenerator;
internal static class Extensions
{
	public static GenericParameterSignature ToTypeSignature(this GenericParameter @this)
	{
		return @this.Owner switch
		{
			TypeDefinition => new GenericParameterSignature(@this.Owner.Module, GenericParameterType.Type, @this.Number),
			MethodDefinition => new GenericParameterSignature(@this.Owner.Module, GenericParameterType.Method, @this.Number),
			_ => throw new NotSupportedException()
		};
	}
}
