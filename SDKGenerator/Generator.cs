using System;
using System.Text;
using Reloaded.Memory.Pointers;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver;
using AsmResolver.DotNet.Serialized;

// It's been so long that I don't understand this shitcode anymore.
// Only refuctoring will save this shitcode.
// But it's working at 92%

namespace SDKGenerator;
public class Generator
{
	Arguments args;
	bool use_whitelist;

	string path;

	DirectoryInfo dest;
	DirectoryInfo managed;

	ModuleReaderParameters moduleReaderParams;

	AssemblyDefinition ThisAssembly;
	AssemblyDefinition TargetAssembly;
	ModuleDefinition TargetAssemblyModule;
	AssemblyDefinition SDK;
	ModuleDefinition SDKModule;

	TypeDefinition Mono;
	TypeDefinition asmContainer;
	ITypeDefOrRef valtype;
	ITypeDefOrRef nativeint;
	ITypeDefOrRef pointer;

	ITypeDefOrRef Enum;

	MethodDefinition GetImage;
	MethodDefinition GetClassInfo;
	MethodDefinition GetClassInfoS;
	MethodDefinition GetFieldInfo;
	MethodDefinition FieldOffset;
	MethodDefinition SetStaticVal; // generic fck, because it's easy to shoot yourself in the foot
	MethodDefinition GetStaticVal; // generic fck, don't mess around with that.
	MethodDefinition GetInstanceFieldValue; // generic fck
	MethodDefinition SetInstanceFieldValue; // generic fck
	MethodDefinition GetMethodInfo;
	MethodDefinition GetMethodInfoWithoutArgsInfo;
	MethodDefinition ReadString;
	MethodDefinition CreateString;
	MethodDefinition Unbox;
	MethodDefinition ReadPtr;       // generic fck
	MethodDefinition ReadVTPtr;     // generic fck
	MethodDefinition GetVirtFunc;
	MethodDefinition ConstructGenericAQNFrom1;
	MethodDefinition CallMethod0;	// generic fck
	MethodDefinition CallMethod1;	// generic fck
	MethodDefinition CallMethod2;   // generic fck
	MethodDefinition CallMethod3;   // generic fck
	MethodDefinition CallMethod4;   // generic fck
	MethodDefinition CallMethod5;   // generic fck
									// add for calls
	IMethodDescriptor gettype;
	TypeDefinition ArrayType;
	TypeSignature ArraySig;

	public Generator(string path, Arguments args)
	{
		this.path = path;
		this.args = args;
		use_whitelist = args.ClassWhitelist != null;

		managed = new FileInfo(path).Directory;
		moduleReaderParams = new ModuleReaderParameters(managed.FullName);
		ThisAssembly = AssemblyDefinition.FromFile(typeof(Generator).Assembly.Location);
		TargetAssemblyModule = ModuleDefinition.FromFile(path, moduleReaderParams);
		TargetAssembly = TargetAssemblyModule.Assembly;
		SDK = new AssemblyDefinition("GameSDK", new Version(1,0,0,0));
		SDKModule = new ModuleDefinition("GameSDK", KnownCorLibs.SystemRuntime_v6_0_0_0); // TODO: get knowncorlibs from current exe
		SDK.Modules.Add(SDKModule);

		// fixes some rare bug
		var framework = ThisAssembly.CustomAttributes.First(ca => ca.Constructor.DeclaringType.IsTypeOf("System.Runtime.Versioning", "TargetFrameworkAttribute"));
		SDK.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)SDKModule.DefaultImporter.ImportMethod(framework.Constructor), framework.Signature));
	}

	public void GenerateSDK(string destination)
	{
		// get destination dir
		dest = Directory.Exists(destination) ? new DirectoryInfo(destination) : Directory.CreateDirectory(destination);

		// clone MonoBridge class to GameSDK dll
		var cloned = new MemberCloner(SDKModule)
			.Include(ThisAssembly.ManifestModule.TopLevelTypes.First(t => t.Name == "MonoBridge"))
			.Clone();
		Mono = cloned.ClonedTopLevelTypes.First();
		Mono.Namespace = "SDK";
		SDKModule.TopLevelTypes.Add(Mono);

		// setup basic fields (aka metadata) for generation
		valtype = SDKModule.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "ValueType").ImportWith(SDKModule.DefaultImporter);
		nativeint = SDKModule.CorLibTypeFactory.UIntPtr.Type;
		pointer = SDKModule.DefaultImporter.ImportType(typeof(Pointer<>));
		pointer = pointer.ToTypeSignature().ImportWith(SDKModule.DefaultImporter).ToTypeDefOrRef();
		Enum = SDKModule.DefaultImporter.ImportType(typeof(Enum));
		gettype = SDKModule.DefaultImporter.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle)));

		// setup methods from MonoBridge
		GetImage = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetImage));
		GetClassInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetClassInfo));
		GetClassInfoS = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetClassInfoS));
		GetFieldInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetFieldInfo));
		FieldOffset = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetFieldOffset));
		SetStaticVal = Mono.Methods.First(m => m.Name == nameof(MonoBridge.SetStaticFieldValue));
		GetStaticVal = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetStaticFieldValue));
		GetInstanceFieldValue = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetField));
		SetInstanceFieldValue = Mono.Methods.First(m => m.Name == nameof(MonoBridge.SetField));
		ReadString = Mono.Methods.First(m => m.Name == nameof(MonoBridge.ReadString));
		CreateString = Mono.Methods.First(m => m.Name == nameof(MonoBridge.AllocStr));
		Unbox = Mono.Methods.First(m => m.Name == nameof(MonoBridge.Unbox));
		ReadPtr = Mono.Methods.First(m => m.Name == nameof(MonoBridge.Cast));
		ReadVTPtr = Mono.Methods.First(m => m.Name == nameof(MonoBridge.ReadValueType));
		GetMethodInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetMonoFunction));
		CallMethod0 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 0);
		CallMethod1 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 1);
		CallMethod2 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 2);
		CallMethod3 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 3);
		CallMethod4 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 4);
		CallMethod5 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 5);
		GetMethodInfoWithoutArgsInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetMonoFunctionFromName));
		GetVirtFunc = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetVirtFunction));
		ConstructGenericAQNFrom1 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.ConstructGenericAQNFrom1));

		// Class which contains all assembly names
		asmContainer = new TypeDefinition("SDK", "Assemblies", TypeAttributes.Public, SDKModule.CorLibTypeFactory.Object.Type);
		SDKModule.TopLevelTypes.Add(asmContainer);
		asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions.Clear();

		// filter field
		IEnumerable<TypeDefinition> goodTypes;

		var bas = TargetAssemblyModule.AssemblyReferences.FirstOrDefault(m => m.Name == "mscorlib")?.Resolve().ManifestModule;
		if (bas == null)
			bas = TargetAssemblyModule.AssemblyReferences.FirstOrDefault(m => m.Name == "netstandard")?.Resolve().ManifestModule
				.AssemblyReferences.FirstOrDefault(m => m.Name == "mscorlib")?.Resolve().ManifestModule;
		if (bas != null)
		{
			WriteType(bas.TopLevelTypes.First(t => t.FullName == "System.Array"), out ArrayType);
			ArraySig = ArrayType.ToTypeSignature();

			if (args.GetEverythingFromMscorlib)
			{
				goodTypes = bas.TopLevelTypes.Where(t => !t.IsInterface && t.GenericParameters.Count == 0);
				foreach (var i in goodTypes)
				{
					WriteType(i, out _);
				}
			}
		}

		// get target dll and filter types
		foreach (var dll in args.TargetDlls)
		{
			var copy = dll;
			if (copy[0] == ' ') // nice joke, CommandLine.
				copy = dll.Remove(0, 1);

			var module = ModuleDefinition.FromFile(copy, moduleReaderParams);

			if (use_whitelist)
				goodTypes = module.GetAllTypes().Where(t => args.ClassWhitelist.Contains(t.FullName));
			else
				goodTypes = module.TopLevelTypes.Where(t => !t.IsInterface && t.GenericParameters.Count == 0);

			// write proxies for all good types
			foreach (var i in goodTypes)
			{
				WriteType(i, out _);
			}
		}

		// force write all types from Unity.CoreModule
		if (args.GetEverythingFromUnityCoreModule)
		{
			var ue = TargetAssemblyModule.AssemblyReferences.FirstOrDefault(m => m.Name == "UnityEngine.CoreModule")?.Resolve().ManifestModule;
			if (ue != null)
			{
				goodTypes = ue.TopLevelTypes.Where(t => !t.IsInterface && t.GenericParameters.Count == 0);
				foreach (var i in goodTypes)
				{
					WriteType(i, out _);
				}
			}
		}

		asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions.Add(CilOpCodes.Ret);

		// Write Helper Methods (i.e: ReadString and CreateString)
		WriteHelpers();

		// save GameSDK dll
		SDK.Write(Path.Combine(destination, "GameSDK.dll"));
	}

	Dictionary<string, TypeDefinition> Bridge = new Dictionary<string, TypeDefinition>(); // cache
	TypeDefinition GetProxyTypeSafe(TypeDefinition target) // Return proxy type
	{
		if (Bridge.TryGetValue(target.FullName, out var result)) // try get one from cache
			return result;

		if (target.IsValueType) // primitive types must be ignored
		{
			if (target.IsTypeOf("System", "Void"))
			{
				result = SDKModule.CorLibTypeFactory.Void.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Char"))
			{
				result = SDKModule.CorLibTypeFactory.Char.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Single"))
			{
				result = SDKModule.CorLibTypeFactory.Single.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Double"))
			{
				result = SDKModule.CorLibTypeFactory.Double.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Boolean"))
			{
				result = SDKModule.CorLibTypeFactory.Boolean.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Byte"))
			{
				result = SDKModule.CorLibTypeFactory.Byte.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "SByte"))
			{
				result = SDKModule.CorLibTypeFactory.SByte.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Int16"))
			{
				result = SDKModule.CorLibTypeFactory.Int16.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "UInt16"))
			{
				result = SDKModule.CorLibTypeFactory.UInt16.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Int32"))
			{
				result = SDKModule.CorLibTypeFactory.Int32.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "UInt32"))
			{
				result = SDKModule.CorLibTypeFactory.UInt32.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "Int64"))
			{
				result = SDKModule.CorLibTypeFactory.Int64.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "UInt64"))
			{
				result = SDKModule.CorLibTypeFactory.UInt64.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "IntPtr"))
			{
				result = SDKModule.CorLibTypeFactory.IntPtr.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
			if (target.IsTypeOf("System", "UIntPtr"))
			{
				result = SDKModule.CorLibTypeFactory.UIntPtr.Type.Resolve();
				Bridge.Add(target.FullName, result);
				return result;
			}
		}

		// generate one
		WriteType(target, out var r);

		return r;
	}

	Dictionary<string, FieldDefinition> ImagePtr = new Dictionary<string, FieldDefinition>();
	FieldDefinition GetImagePtr(ModuleDefinition md)
	{
		if (ImagePtr.TryGetValue(md.Name, out var f)) return f;
		f = new FieldDefinition(md.Name.Value.Replace('-', '_').Replace('.','_').Replace("_dll", null), FieldAttributes.Assembly | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		asmContainer.Fields.Add(f);
		var body = asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions;
		body.Add(CilOpCodes.Ldstr, md.Name);
		body.Add(CilOpCodes.Call, GetImage);
		body.Add(CilOpCodes.Stsfld, f);
		ImagePtr.Add(md.Name, f);
		return f;
	}
	string GetAsmPref(ModuleDefinition md)
	{
		if (ImagePtr.TryGetValue(md.Name, out var f)) return f.Name;
		f = new FieldDefinition(md.Name.Value.Replace('-', '_').Replace('.', '_').Replace("_dll", null), FieldAttributes.Assembly | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		asmContainer.Fields.Add(f);
		var body = asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions;
		body.Add(CilOpCodes.Ldstr, md.Name);
		body.Add(CilOpCodes.Call, GetImage);
		body.Add(CilOpCodes.Stsfld, f);
		ImagePtr.Add(md.Name, f);
		return f.Name;
	}

	Dictionary<TypeDefinition, TypeSignature> PointerCache = new Dictionary<TypeDefinition, TypeSignature>();
	TypeSignature GetGenericPtr(TypeDefinition target)
	{
		if (PointerCache.TryGetValue(target, out var result)) return result;
		result = pointer.MakeGenericInstanceType(target.ToTypeSignature());
		//result = SDKModule.DefaultImporter.ImportTypeSignature(result.ToTypeDefOrRef().ToTypeSignature());
		PointerCache.Add(target, result);
		return result;
	}

	void WriteType(TypeDefinition def, out TypeDefinition writed)
	{
		if (def == null || def.IsModuleType || def.Name[0] == '<')
		{
			writed = null;
			return;
		}
		if (Bridge.TryGetValue(def.FullName, out var r))
		{
			writed = (TypeDefinition)r;
			return;
		}
		bool genericMode = false;
		int generics = 0;
		if (def.GenericParameters.Count > 0) // cursed thing
		{
			//writed = null;
			//return;

			genericMode = true;
			generics = def.GenericParameters.Count;
			if (generics != 1)
			{
				writed = null;
				return;
			}
		}
		if (def.IsEnum)
		{
			var en = new MemberCloner(SDKModule).Include(def).Clone().ClonedTopLevelTypes.First();
			SDKModule.TopLevelTypes.Add(en);
			Bridge.Add(def.FullName, en);
			//en.BaseType = Enum;
			en.Namespace = Utf8String.IsNullOrEmpty(def.Namespace) ? $"GameSDK.{GetAsmPref(def.Module)}" : $"GameSDK.{GetAsmPref(def.Module)}." + def.Namespace;
			if (def.DeclaringType != null)
			{
				WriteType(def.DeclaringType, out var parent);
				en.Namespace = parent.Namespace;
				en.Name = parent.Name + '_' + en.Name;
			}
			writed = en;
			return;
		}

		var clone = new TypeDefinition(Utf8String.IsNullOrEmpty(def.Namespace) ? $"GameSDK.{GetAsmPref(def.Module)}" : $"GameSDK.{GetAsmPref(def.Module)}." + def.Namespace, def.Name, TypeAttributes.Public, valtype);
		if (genericMode)
		{
			foreach(var gen in def.GenericParameters)
			{
				var generic = new GenericParameter(gen.Name, GenericParameterAttributes.NotNullableValueTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint);
				generic.Constraints.Add(new GenericParameterConstraint(valtype));
				clone.GenericParameters.Add(generic);
			}
		}
		clone.IsSequentialLayout = true;
		var cloneSig = genericMode ? (TypeSignature)new GenericInstanceTypeSignature(clone, true) : clone.ToTypeSignature();
		Bridge.Add(def.FullName, clone);
		SDKModule.TopLevelTypes.Add(clone);
		writed = clone;

		bool isStruct = def.IsValueType;
		var init = clone.GetOrCreateStaticConstructor().CilMethodBody.Instructions;
		init.Clear();
		var klass = new FieldDefinition("klass", FieldAttributes.Public | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		var vtable = new FieldDefinition("vtable", FieldAttributes.Public | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		FieldDefinition _this = isStruct ? null : new FieldDefinition("_this", FieldAttributes.Public, SDKModule.CorLibTypeFactory.UIntPtr);

		var gensigs = new TypeSignature[clone.GenericParameters.Count];
		for(int i = 0; i < clone.GenericParameters.Count; i++)
			gensigs[i] = new GenericParameterSignature(GenericParameterType.Type, i);

		var generic_this = genericMode && _this != null ? new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), "_this", _this.Signature) : null;

		clone.Fields.Add(klass);
		clone.Fields.Add(vtable);
		if (_this != null)
		{
			clone.Fields.Add(_this);
			var method = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RuntimeSpecialName, MethodSignature.CreateInstance(SDKModule.CorLibTypeFactory.Void, SDKModule.CorLibTypeFactory.UIntPtr));
			var body = new CilMethodBody(method);
			var instr = body.Instructions;
			method.CilMethodBody = body;
			instr.Add(CilOpCodes.Ldarg_0);
			instr.Add(CilOpCodes.Ldarg_1);
			if (genericMode)
				instr.Add(CilOpCodes.Stfld, generic_this);
			else
				instr.Add(CilOpCodes.Stfld, _this);
			instr.Add(CilOpCodes.Ret);

			clone.Methods.Add(method);
		}

		var aqn = new MethodDefinition("aqn", MethodAttributes.Public | MethodAttributes.Static, MethodSignature.CreateStatic(SDKModule.CorLibTypeFactory.String));
		var aqnbody = new CilMethodBody(aqn);
		aqn.CilMethodBody = aqnbody;
		aqnbody.Instructions.Add(CilOpCodes.Ldstr, $"{def.FullName}, {def.Module.Assembly.FullName}"); // AssemblyQualifiedName for Type.GetType(string)
		if (genericMode)
		{
			aqnbody.Instructions.Add(CilOpCodes.Ldtoken, new TypeSpecification(new GenericParameterSignature(SDKModule, GenericParameterType.Type, 0)));
			aqnbody.Instructions.Add(CilOpCodes.Call, gettype);
			aqnbody.Instructions.Add(CilOpCodes.Call, ConstructGenericAQNFrom1);
		}
		aqnbody.Instructions.Add(CilOpCodes.Ret);
		clone.Methods.Add(aqn);

		if (genericMode)
		{
			init.Add(CilOpCodes.Call, new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), "aqn", aqn.Signature));
			//init.Add(CilOpCodes.Ldtoken, new TypeSpecification(new GenericParameterSignature(SDKModule, GenericParameterType.Type, 0)));
			//init.Add(CilOpCodes.Call, gettype);
			init.Add(CilOpCodes.Ldsflda, new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), "klass", klass.Signature));
			init.Add(CilOpCodes.Ldsflda, new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), "vtable", vtable.Signature));
			init.Add(CilOpCodes.Call, GetClassInfoS);
		}
		else
		{
			init.Add(CilOpCodes.Ldsfld, GetImagePtr(def.Module));
			init.Add(CilOpCodes.Ldc_I4, def.MetadataToken.ToInt32());
			init.Add(CilOpCodes.Ldsflda, klass);
			init.Add(CilOpCodes.Ldsflda, vtable);
			init.Add(CilOpCodes.Call, GetClassInfo);
		}

		if (def.DeclaringType != null)
		{
			WriteType(def.DeclaringType, out var parent);
			clone.Namespace = parent.Namespace;
			clone.Name = parent.Name + '_' + clone.Name;
		}

		foreach (var child in def.NestedTypes)
			if (child.GenericParameters.Count == 0)
				WriteType(child, out var c);

		foreach(var field in def.Fields)
		{
			if (genericMode) 
				continue;

			bool genericField = false;

			if (field.Signature.FieldType is GenericInstanceTypeSignature gits)
			{
				genericField = true;
				if (GetProxyTypeSafe(gits.GenericType.Resolve()) == null) continue;
			}
			else gits = null;
			//if (genericMode && field.Signature.FieldType.ElementType == ElementType.GenericInst) continue;

			TypeDefinition fieldtype = field.Signature.FieldType.Resolve();
			if (fieldtype == null) continue;

			TypeDefinition target = null;
			if (field.Signature.FieldType is ArrayBaseTypeSignature)
				target = ArrayType;
			else
				target = GetProxyTypeSafe(fieldtype);

			if (target == null)
			{
				WriteType(fieldtype, out var c);
				target = c;
			}
			if (target == null) continue;
			if (target.IsTypeOf("System", "Void")) continue;
			TypeSignature[] genericFieldSigs = null;
			var buf = genericField ? gits.TypeArguments.Select(t => GetProxyTypeSafe(t.Resolve())) : null;
			if (buf != null)
			{
				if (!buf.Any(t => t == null))
					genericFieldSigs = buf.Select(t => t.ToTypeSignature()).ToArray();
				else
					continue;
			}
			var targetTypeSig = genericField ? target.MakeGenericInstanceType(genericFieldSigs) : target.ToTypeSignature();

			var info = new FieldDefinition(field.Name + "_info", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
			init.Add(CilOpCodes.Ldsfld, klass);
			init.Add(CilOpCodes.Ldc_I4, field.MetadataToken.ToInt32());
			init.Add(CilOpCodes.Call, GetFieldInfo);
			init.Add(CilOpCodes.Stsfld, info);
			clone.Fields.Add(info);

			if (field.IsStatic)
			{
				var getter = new MethodDefinition($"get_{field.Name}", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(targetTypeSig));
				var setter = new MethodDefinition($"set_{field.Name}", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(SDKModule.CorLibTypeFactory.Void, targetTypeSig));

				var gBody = new CilMethodBody(getter);
				getter.CilMethodBody = gBody;
				var g = gBody.Instructions;
				var sBody = new CilMethodBody(setter);
				setter.CilMethodBody = sBody;
				var s = sBody.Instructions;

				g.Add(CilOpCodes.Ldsfld, vtable);
				g.Add(CilOpCodes.Ldsfld, info);
				g.Add(CilOpCodes.Call, GetStaticVal.MakeGenericInstanceMethod(targetTypeSig));
				g.Add(CilOpCodes.Ret);

				s.Add(CilOpCodes.Ldsfld, vtable);
				s.Add(CilOpCodes.Ldsfld, info);
				s.Add(CilOpCodes.Ldarg_0);
				s.Add(CilOpCodes.Call, SetStaticVal.MakeGenericInstanceMethod(targetTypeSig));
				s.Add(CilOpCodes.Ret);

				clone.Methods.Add(getter);
				clone.Methods.Add(setter);
				var prop = new PropertyDefinition(field.Name, PropertyAttributes.None, new PropertySignature(CallingConventionAttributes.Default, targetTypeSig, Array.Empty<TypeSignature>()));
				prop.SetSemanticMethods(getter, setter);
				clone.Properties.Add(prop);
				// todo: check static proxy
			}
			else
			{
				if (_this == null) // unsafe fck
				{
					clone.Fields.Add(new FieldDefinition(field.Name, FieldAttributes.Public, targetTypeSig));
				}
				else
				{
					var offset = new FieldDefinition(field.Name + "_offset", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UInt32);
					clone.Fields.Add(offset);
					init.Add(CilOpCodes.Ldsfld, info);
					init.Add(CilOpCodes.Call, FieldOffset);
					init.Add(CilOpCodes.Conv_U4);
					init.Add(CilOpCodes.Stsfld, offset);

					var getter = new MethodDefinition($"get_{field.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateInstance(targetTypeSig));
					var gBody = new CilMethodBody(getter);
					getter.CilMethodBody = gBody;
					var g = gBody.Instructions;
					g.Add(CilOpCodes.Ldarg_0);
					g.Add(CilOpCodes.Ldfld, _this);
					g.Add(CilOpCodes.Ldsfld, offset);
					g.Add(CilOpCodes.Call, GetInstanceFieldValue.MakeGenericInstanceMethod(targetTypeSig));
					g.Add(CilOpCodes.Ret);

					var setter = new MethodDefinition($"set_{field.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateInstance(SDKModule.CorLibTypeFactory.Void, targetTypeSig)); // GetGenericPtr(target)
					var sBody = new CilMethodBody(setter);
					setter.CilMethodBody = sBody;
					var s = sBody.Instructions;
					s.Add(CilOpCodes.Ldarg_0);
					s.Add(CilOpCodes.Ldfld, _this);
					s.Add(CilOpCodes.Ldsfld, offset);
					s.Add(CilOpCodes.Ldarg_1);
					s.Add(CilOpCodes.Call, SetInstanceFieldValue.MakeGenericInstanceMethod(targetTypeSig));
					s.Add(CilOpCodes.Ret);

					clone.Methods.Add(getter);
					clone.Methods.Add(setter);
					var prop = new PropertyDefinition(field.Name, PropertyAttributes.None, new PropertySignature(CallingConventionAttributes.HasThis, targetTypeSig, Array.Empty<TypeSignature>()));
					prop.SetSemanticMethods(getter, setter);
					clone.Properties.Add(prop);
				}
			}
		}

		foreach(var method in def.Methods)
		{
			if (method.IsConstructor || method.GenericParameters.Count != 0 || method.Parameters.Any(p => p.ParameterType is GenericInstanceTypeSignature)) continue;
			int paramCount = method.Parameters.Count;
			if (paramCount > 5) continue; // wip
			if (method.IsStatic == false && _this == null) continue;
			if (method.Name[0] == '<') continue;
			if (method.Signature.ReturnType.ElementType == ElementType.SzArray) continue;
			if (method.Parameters.Any(p => p.ParameterType is ByReferenceTypeSignature || p.ParameterType is PointerTypeSignature /* TODO */)) continue;

			//if (def.IsTypeOf("System.Collections.Generic", "List`1") && method.Name == "get_item")
			//	Console.Write(1);

			TypeSignature rettype = null;
			var rettypeDef = method.Signature.ReturnType.ElementType == ElementType.Var ? null : GetProxyTypeSafe(method.Signature.ReturnType.Resolve());
			if (method.Signature.ReturnType is GenericInstanceTypeSignature gits)
			{
				if (rettypeDef == null) continue;
				if (gits.TypeArguments.Any(a => a is GenericParameterSignature)) continue;
				var methodGenArgs = gits.TypeArguments.Select(t => GetProxyTypeSafe(t.Resolve()));
				if (methodGenArgs.Any(a => a == null)) continue;
				var methodGenSigs = methodGenArgs.Select(a => a.ToTypeSignature()).ToArray();
				rettype = rettypeDef.MakeGenericInstanceType(methodGenSigs);
			}
			else
			{
				rettype = method.Signature.ReturnType.ElementType == ElementType.Var ? new GenericParameterSignature(((GenericParameterSignature)method.Signature.ReturnType).ParameterType, ((GenericParameterSignature)method.Signature.ReturnType).Index) : rettypeDef?.ToTypeSignature();
			}

			var args = method.Signature.ParameterTypes.Select(TransformParameters);
			if (args.Any(a => a == null) || rettype == null) continue;

			var info = new FieldDefinition($"{method.Name}_info", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
			var generic_info = genericMode ? new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), info.Name, info.Signature) : null;
			clone.Fields.Add(info);

			if (genericMode)
			{
				init.Add(CilOpCodes.Ldsfld, new MemberReference(new TypeSpecification(new GenericInstanceTypeSignature(clone, true, gensigs)), "klass", klass.Signature));
				init.Add(CilOpCodes.Ldstr, method.Name);
				init.Add(CilOpCodes.Ldc_I4, method.Parameters.Count);
				init.Add(CilOpCodes.Call, GetMethodInfoWithoutArgsInfo);
				init.Add(CilOpCodes.Stsfld, generic_info);
			}
			else
			{
				init.Add(CilOpCodes.Ldsfld, klass);
				init.Add(CilOpCodes.Ldstr, $":{method.Name}({string.Join(',', method.Parameters.Select(p => Clear(p.ParameterType)))})");
				init.Add(CilOpCodes.Call, GetMethodInfo);
				init.Add(CilOpCodes.Stsfld, info);
			}

			var proxy = new MethodDefinition(method.Name, method.Attributes, method.IsStatic ? MethodSignature.CreateStatic(rettype, args) : MethodSignature.CreateInstance(rettype, args));
			
			if (proxy.IsAbstract) proxy.IsAbstract = false;

			if (method.ParameterDefinitions.Count == method.Parameters.Count)
				for(int i = 0; i < proxy.Parameters.Count; i++)
					proxy.ParameterDefinitions.Add(new ParameterDefinition(method.ParameterDefinitions[i].Sequence, method.ParameterDefinitions[i].Name, (ParameterAttributes)0));

			proxy.IsPublic = true;
			var body = new CilMethodBody(proxy);
			proxy.CilMethodBody = body;
			var instr = body.Instructions;

			if (method.IsVirtual)
			{
				instr.Add(CilOpCodes.Ldarg_0);
				if (genericMode)
					instr.Add(CilOpCodes.Ldfld, generic_this);
				else
					instr.Add(CilOpCodes.Ldfld, _this);
			}

			if (genericMode)
				instr.Add(CilOpCodes.Ldsfld, generic_info);
			else
				instr.Add(CilOpCodes.Ldsfld, info);

			if (method.IsVirtual)
			{
				instr.Add(CilOpCodes.Call, GetVirtFunc);
			}

			if (method.IsStatic)
				instr.Add(CilOpCodes.Ldc_I4_0);
			else
			{
				instr.Add(CilOpCodes.Ldarg_0);
				if (genericMode)
					instr.Add(CilOpCodes.Ldfld, generic_this);
				else
					instr.Add(CilOpCodes.Ldfld, _this);
			}

			for (int i = 0; i < paramCount; i++)
			{
				instr.Add(CilOpCodes.Ldarg, proxy.Parameters[i]);
				instr.Add(CilOpCodes.Ldc_I4, method.Parameters[i].ParameterType.IsValueType ? 1 : 0); // 3038 high tech if else
			}

			bool retval = proxy.Signature.ReturnsValue;
			var retType = proxy.Signature.ReturnType;

			// hardcoded
			switch (paramCount)
			{
				case 0:
					instr.Add(CilOpCodes.Call, CallMethod0);
					break;
				case 1:
					instr.Add(CilOpCodes.Call, CallMethod1.MakeGenericInstanceMethod(proxy.Parameters[0].ParameterType));
					break;
				case 2:
					instr.Add(CilOpCodes.Call, CallMethod2.MakeGenericInstanceMethod(proxy.Parameters[0].ParameterType, proxy.Parameters[1].ParameterType));
					break;
				case 3:
					instr.Add(CilOpCodes.Call, CallMethod3.MakeGenericInstanceMethod(proxy.Parameters[0].ParameterType, proxy.Parameters[1].ParameterType, proxy.Parameters[2].ParameterType));
					break;
				case 4:
					instr.Add(CilOpCodes.Call, CallMethod4.MakeGenericInstanceMethod(proxy.Parameters[0].ParameterType, proxy.Parameters[1].ParameterType, proxy.Parameters[2].ParameterType, proxy.Parameters[3].ParameterType));
					break;
				case 5:
					instr.Add(CilOpCodes.Call, CallMethod5.MakeGenericInstanceMethod(proxy.Parameters[0].ParameterType, proxy.Parameters[1].ParameterType, proxy.Parameters[2].ParameterType, proxy.Parameters[3].ParameterType, proxy.Parameters[4].ParameterType));
					break;
			}

			if (retval == false)
				instr.Add(CilOpCodes.Pop);
			else
			{
				if (method.Signature.ReturnType.IsValueType)
				{
					instr.Add(CilOpCodes.Call, Unbox);
					instr.Add(CilOpCodes.Call, ReadVTPtr.MakeGenericInstanceMethod(retType));
				}
				else
					instr.Add(CilOpCodes.Call, ReadPtr.MakeGenericInstanceMethod(retType));
			}

			instr.Add(CilOpCodes.Ret);

			if (clone.Name.Contains('.')) clone.Name = clone.Name.Value.Replace('.', '_');

			clone.Methods.Add(proxy);
		}

		if (_this != null && def.BaseType != null)
		{
			var Base = GetProxyTypeSafe(def.BaseType.Resolve());
			if (Base == null) goto ExitFromBaseCast;
			var GetBase = new MethodDefinition("GetBase", MethodAttributes.Public, MethodSignature.CreateInstance(Base.ToTypeSignature()));
			var body = new CilMethodBody(GetBase);
			GetBase.CilMethodBody = body;
			body.Instructions.Add(CilOpCodes.Ldarg_0);
			body.Instructions.Add(CilOpCodes.Ldfld, _this);
			body.Instructions.Add(CilOpCodes.Newobj, Base.Methods.First(m => m.IsConstructor && m.Parameters.Count == 1));
			body.Instructions.Add(CilOpCodes.Ret);

			clone.Methods.Add(GetBase);
		}
		ExitFromBaseCast:

		init.Add(CilOpCodes.Ret);
	}

	private void WriteHelpers()
	{
		if (Bridge.TryGetValue("System.String", out var str))
		{
			var truesting = SDKModule.CorLibTypeFactory.String;

			var getstr = new MethodDefinition("GetString", MethodAttributes.Public, MethodSignature.CreateInstance(truesting));
			var body = new CilMethodBody(getstr);
			getstr.CilMethodBody = body;
			var b = body.Instructions;
												//				Net 4.X			  ||			Net 3.X
			var charoffset = str.Fields.First(f => f.Name == "m_firstChar_offset" || f.Name == "start_char_offset");
			var _this = str.Fields.First(f => f.Name == "_this");
			b.Add(CilOpCodes.Ldarg_0);
			b.Add(CilOpCodes.Ldfld, _this);
			b.Add(CilOpCodes.Ldsfld, charoffset);
			b.Add(CilOpCodes.Add); // ptr to chars
			b.Add(CilOpCodes.Ldarg_0);
			b.Add(CilOpCodes.Call, str.Methods.First(m => m.Name == "get_m_stringLength" || m.Name == "get_length"));
			// Call MonoBridge.ReadString(first_char_pointer, length);
			b.Add(CilOpCodes.Call, ReadString);
			b.Add(CilOpCodes.Ret);

			// result: return MonoBridge.ReadString(this._this + first_char_offset, this.get_Length());

			var createstr = new MethodDefinition("InjectString", MethodAttributes.Public | MethodAttributes.Static, MethodSignature.CreateStatic(str.ToTypeSignature(), truesting));
			body = new CilMethodBody(createstr);
			createstr.CilMethodBody = body;
			b = body.Instructions;
			b.Add(CilOpCodes.Ldarg_0);
			b.Add(CilOpCodes.Call, CreateString);
			b.Add(CilOpCodes.Ret);

			str.Methods.Add(getstr);
			str.Methods.Add(createstr);
		}

		// scope for GetTypeFromStr_Inject
		{
			var gtfs = Mono.Methods.First(m => m.Name == "GetTypeFromStr_Inject");
			var instr = gtfs.CilMethodBody.Instructions;
			var store1 = new CilLocalVariable(Bridge["System.Type"].ToTypeSignature());
			gtfs.CilMethodBody.LocalVariables.Add(store1);
			var store2 = new CilLocalVariable(Bridge["System.RuntimeTypeHandle"].ToTypeSignature());
			gtfs.CilMethodBody.LocalVariables.Add(store2);
			instr.Clear();
			instr.Add(CilOpCodes.Ldarg_0);
			instr.Add(CilOpCodes.Call, Bridge["System.String"].Methods.First(m => m.Name == "InjectString"));
			instr.Add(CilOpCodes.Ldc_I4_1);
			instr.Add(CilOpCodes.Call, Bridge["System.Type"].Methods.First(m => m.Name == "GetType" && m.Parameters.Count == 2));
			instr.Add(CilOpCodes.Stloc_S, store1);
			instr.Add(CilOpCodes.Ldloca_S, store1); // im hate struct`s ><
			instr.Add(CilOpCodes.Call, Bridge["System.Type"].Methods.First(m => m.Name == "get__impl"));
			instr.Add(CilOpCodes.Stloc_S, store2);
			instr.Add(CilOpCodes.Ldloca_S, store2); // im hate struct`s >< (x2)
			instr.Add(CilOpCodes.Ldfld, Bridge["System.RuntimeTypeHandle"].Fields.First(m => m.Name == "value"));
			instr.Add(CilOpCodes.Ret); 
		}
	}

	// unstable piece of shit
	private static string Clear(TypeSignature sig)
	{
		switch(sig.Name) // good
		{
			case "String": return "string";
			case "Single": return "single";
			case "Double": return "double";
			case "Boolean": return "bool";
			case "Byte": return "byte";
			case "SByte": return "sbyte";
			case "Char": return "char";
			case "Int16": return "short";
			case "UInt16": return "ushort";
			case "Int32": return "int";
			case "UInt32": return "uint";
			case "Int64": return "long";
			case "UInt64": return "ulong";
			//case "Object": return "object";
		}
		if (sig.FullName == "System.Object") return "object";

		if (sig is PointerTypeSignature pts) // why
		{
			switch (pts.Name)
			{
				case "String*": return "string*";
				case "Single*": return "single*";
				case "Double*": return "double*";
				case "Boolean*": return "bool*";
				case "Byte*": return "byte*";
				case "SByte*": return "sbyte*";
				case "Char*": return "char*";
				case "Int16*": return "short*";
				case "UInt16*": return "ushort*";
				case "Int32*": return "int*";
				case "UInt32*": return "uint*";
				case "Int64*": return "long*";
				case "UInt64*": return "ulong*";
			}
		}
		else if (sig is ArrayBaseTypeSignature ats) // why x2
		{
			switch (ats.Name)
			{
				case "String[]": return "string[]";
				case "Single[]": return "single[]";
				case "Double[]": return "double[]";
				case "Boolean[]": return "bool[]";
				case "Byte[]": return "byte[]";
				case "SByte[]": return "sbyte[]";
				case "Char[]": return "char[]";
				case "Int16[]": return "short[]";
				case "UInt16[]": return "ushort[]";
				case "Int32[]": return "int[]";
				case "UInt32[]": return "uint[]";
				case "Int64[]": return "long[]";
				case "UInt64[]": return "ulong[]";
			}
		}

		return sig.Name;
	}

	private static string Sanitize(string str) => str.Replace('/', '.');

	private TypeSignature TransformParameters(TypeSignature p)
	{
		if (p is GenericParameterSignature gps)
		{
			return new GenericParameterSignature(SDKModule, gps.ParameterType, gps.Index);
		}
		else if (p is ArrayBaseTypeSignature)
		{
			return ArrayType.ToTypeSignature();
		}
		else
		{
			return GetProxyTypeSafe(p.Resolve())?.ToTypeSignature();
		}
	}
}