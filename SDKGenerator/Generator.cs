using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Reloaded.Assembler;
using Reloaded.Injector;
using Reloaded.Memory.Buffers;
using Reloaded.Memory.Pointers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Utilities;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Cloning;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver;

namespace SDKGenerator;
public class Generator
{
	string path;

	StringBuilder source = new StringBuilder(4096);

	DirectoryInfo dest;
	DirectoryInfo managed;

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
	//IMethodDescriptor EFlags;

	MethodDefinition GetImage;
	MethodDefinition GetClassInfo;
	MethodDefinition GetFieldInfo;
	MethodDefinition FieldOffset;
	MethodDefinition SetStaticVal; // generic fck
	MethodDefinition GetStaticVal; // generic fck
	MethodDefinition GetInstanceFieldValue; // generic fck
	MethodDefinition SetInstanceFieldValue; // generic fck
	MethodDefinition GetMethodInfo;
	MethodDefinition Unbox;
	MethodDefinition ReadPtr;		// generic fck
	MethodDefinition CallMethod0;	// generic fck
	MethodDefinition CallMethod1;	// generic fck
	MethodDefinition CallMethod2;   // generic fck
	MethodDefinition CallMethod3;   // generic fck
	MethodDefinition CallMethod4;   // generic fck
	MethodDefinition CallMethod5;   // generic fck
									// add for calls

	FieldDefinition gameMem; // unused

	public Generator(string path)
	{
		this.path = path;
		managed = new FileInfo(path).Directory;
		ThisAssembly = AssemblyDefinition.FromFile(typeof(Generator).Assembly.Location);
		TargetAssemblyModule = ModuleDefinition.FromFile(path, new AsmResolver.DotNet.Serialized.ModuleReaderParameters(managed.FullName));
		TargetAssembly = TargetAssemblyModule.Assembly;
		SDK = new AssemblyDefinition("GameSDK", new Version(1,0,0,0));
		SDKModule = new ModuleDefinition("GameSDK", KnownCorLibs.SystemRuntime_v6_0_0_0);
		SDK.Modules.Add(SDKModule);

		var framework = ThisAssembly.CustomAttributes.First(ca => ca.Constructor.DeclaringType.IsTypeOf("System.Runtime.Versioning", "TargetFrameworkAttribute"));
		SDK.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)SDKModule.DefaultImporter.ImportMethod(framework.Constructor), framework.Signature));
	}

	public void GenerateSDK(string destination)
	{
		dest = Directory.Exists(destination) ? new DirectoryInfo(destination) : Directory.CreateDirectory(destination);

		var cloned = new MemberCloner(SDKModule)
			.Include(ThisAssembly.ManifestModule.TopLevelTypes.First(t => t.Name == "MonoBridge"))
			.Clone();
		Mono = cloned.ClonedTopLevelTypes.First();
		Mono.Namespace = "SDK";
		SDKModule.TopLevelTypes.Add(Mono);

		//valtype = SDKModule.DefaultImporter.ImportType(typeof(ValueType));
		valtype = SDKModule.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "ValueType").ImportWith(SDKModule.DefaultImporter);
		nativeint = SDKModule.CorLibTypeFactory.UIntPtr.Type;
		pointer = SDKModule.DefaultImporter.ImportType(typeof(Pointer<>));
		pointer = pointer.ToTypeSignature().ImportWith(SDKModule.DefaultImporter).ToTypeDefOrRef();
		Enum = SDKModule.DefaultImporter.ImportType(typeof(Enum));
		//EFlags = SDKModule.DefaultImporter.ImportMethod(typeof(System.FlagsAttribute).GetConstructors()[0]);

		GetImage = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetImage));
		GetClassInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetClassInfo));
		GetFieldInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetFieldInfo));
		FieldOffset = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetFieldOffset));
		SetStaticVal = Mono.Methods.First(m => m.Name == nameof(MonoBridge.SetStaticFieldValue));
		GetStaticVal = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetStaticFieldValue));
		GetInstanceFieldValue = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetField));
		SetInstanceFieldValue = Mono.Methods.First(m => m.Name == nameof(MonoBridge.SetField));
		Unbox = Mono.Methods.First(m => m.Name == nameof(MonoBridge.Unbox));
		ReadPtr = Mono.Methods.First(m => m.Name == nameof(MonoBridge.Read));
		GetMethodInfo = Mono.Methods.First(m => m.Name == nameof(MonoBridge.GetMonoFunction));
		CallMethod0 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 0);
		CallMethod1 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 1);
		CallMethod2 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 2);
		CallMethod3 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 3);
		CallMethod4 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 4);
		CallMethod5 = Mono.Methods.First(m => m.Name == nameof(MonoBridge.CallMonoFunction) && m.GenericParameters.Count == 5);
		gameMem = Mono.Fields.First(m => m.Name == nameof(MonoBridge.gameMemory));

		asmContainer = new TypeDefinition("SDK", "Assemblies", TypeAttributes.Public, SDKModule.CorLibTypeFactory.Object.Type);
		SDKModule.TopLevelTypes.Add(asmContainer);
		asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions.Clear();

		var goodTypes = TargetAssemblyModule.TopLevelTypes.Where(t => !t.IsAbstract && !t.IsInterface && t.GenericParameters.Count == 0);
		
		foreach (var i in goodTypes)
		{
			WriteType(i, out _);
		}
		asmContainer.GetOrCreateStaticConstructor().CilMethodBody.Instructions.Add(CilOpCodes.Ret);
		SDK.Write(Path.Combine(destination, "GameSDK.dll"));
	}

	Dictionary<string, TypeDefinition> Bridge = new Dictionary<string, TypeDefinition>();
	TypeDefinition GetProxyTypeSafe(TypeDefinition target)
	{
		if (Bridge.TryGetValue(target.FullName, out var result))
			return result;

		if (target.IsValueType)
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
		if (def == null || def.IsModuleType || def.Name[0] == '<' || def.GenericParameters.Count != 0)
		{
			writed = null;
			return;
		}
		if (Bridge.TryGetValue(def.FullName, out var r))
		{
			writed = (TypeDefinition)r;
			return;
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
		clone.IsSequentialLayout = true;
		Bridge.Add(def.FullName, clone);
		SDKModule.TopLevelTypes.Add(clone);
		writed = clone;

		bool isStruct = def.IsValueType;
		var init = clone.GetOrCreateStaticConstructor().CilMethodBody.Instructions;
		init.Clear();
		var klass = new FieldDefinition("klass", FieldAttributes.Private | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		var vtable = new FieldDefinition("vtable", FieldAttributes.Private | FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
		FieldDefinition _this = isStruct ? null : new FieldDefinition("_this", FieldAttributes.Public, SDKModule.CorLibTypeFactory.UIntPtr);
		
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
			instr.Add(CilOpCodes.Stfld, _this);
			instr.Add(CilOpCodes.Ret);

			clone.Methods.Add(method);
		}
		init.Add(CilOpCodes.Ldsfld, GetImagePtr(def.Module));
		init.Add(CilOpCodes.Ldc_I4, def.MetadataToken.ToInt32());
		init.Add(CilOpCodes.Ldsflda, klass);
		init.Add(CilOpCodes.Ldsflda, vtable);
		init.Add(CilOpCodes.Call, GetClassInfo);

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
			if (field.Signature.FieldType is GenericInstanceTypeSignature || field.Signature.FieldType.ElementType == ElementType.SzArray) continue;

			var fieldtype = field.Signature.FieldType.Resolve();
			if (fieldtype == null) continue;
			var target = GetProxyTypeSafe(fieldtype);
			if (target == null)
			{
				WriteType(fieldtype, out var c);
				target = c;
			}
			if (target == null) continue;
			if (target.IsTypeOf("System", "Void")) continue;

			var info = new FieldDefinition(field.Name + "_info", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
			init.Add(CilOpCodes.Ldsfld, klass);
			init.Add(CilOpCodes.Ldc_I4, field.MetadataToken.ToInt32());
			init.Add(CilOpCodes.Call, GetFieldInfo);
			init.Add(CilOpCodes.Stsfld, info);
			clone.Fields.Add(info);

			if (field.IsStatic)
			{
				var getter = new MethodDefinition($"get_{field.Name}", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(target.ToTypeSignature()));
				var setter = new MethodDefinition($"set_{field.Name}", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateStatic(SDKModule.CorLibTypeFactory.Void, target.ToTypeSignature()));

				var gBody = new CilMethodBody(getter);
				getter.CilMethodBody = gBody;
				var g = gBody.Instructions;
				var sBody = new CilMethodBody(setter);
				setter.CilMethodBody = sBody;
				var s = sBody.Instructions;

				g.Add(CilOpCodes.Ldsfld, vtable);
				g.Add(CilOpCodes.Ldsfld, info);
				g.Add(CilOpCodes.Call, GetStaticVal.MakeGenericInstanceMethod(target.ToTypeSignature()));
				g.Add(CilOpCodes.Ret);

				s.Add(CilOpCodes.Ldsfld, vtable);
				s.Add(CilOpCodes.Ldsfld, info);
				s.Add(CilOpCodes.Ldarg_0);
				s.Add(CilOpCodes.Call, SetStaticVal.MakeGenericInstanceMethod(target.ToTypeSignature()));
				s.Add(CilOpCodes.Ret);

				clone.Methods.Add(getter);
				clone.Methods.Add(setter);
				var prop = new PropertyDefinition(field.Name, PropertyAttributes.None, new PropertySignature(CallingConventionAttributes.Default, target.ToTypeSignature(), Array.Empty<TypeSignature>()));
				prop.SetSemanticMethods(getter, setter);
				clone.Properties.Add(prop);
				// todo: check static proxy
			}
			else
			{
				if (_this == null) // unsafe fck
				{
					clone.Fields.Add(new FieldDefinition(field.Name, FieldAttributes.Public, target.ToTypeSignature()));
				}
				else
				{
					var offset = new FieldDefinition(field.Name + "_offset", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UInt32);
					clone.Fields.Add(offset);
					init.Add(CilOpCodes.Ldsfld, info);
					init.Add(CilOpCodes.Call, FieldOffset);
					init.Add(CilOpCodes.Conv_U4);
					init.Add(CilOpCodes.Stsfld, offset);

					var getter = new MethodDefinition($"get_{field.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateInstance(target.ToTypeSignature())); // GetGenericPtr(target)
					var gBody = new CilMethodBody(getter);
					getter.CilMethodBody = gBody;
					var g = gBody.Instructions;
					g.Add(CilOpCodes.Ldarg_0);
					g.Add(CilOpCodes.Ldfld, _this);
					g.Add(CilOpCodes.Ldsfld, offset);
					g.Add(CilOpCodes.Call, GetInstanceFieldValue.MakeGenericInstanceMethod(target.ToTypeSignature()));
					g.Add(CilOpCodes.Ret);

					var setter = new MethodDefinition($"set_{field.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, MethodSignature.CreateInstance(SDKModule.CorLibTypeFactory.Void, target.ToTypeSignature())); // GetGenericPtr(target)
					var sBody = new CilMethodBody(setter);
					setter.CilMethodBody = sBody;
					var s = sBody.Instructions;
					s.Add(CilOpCodes.Ldarg_0);
					s.Add(CilOpCodes.Ldfld, _this);
					s.Add(CilOpCodes.Ldsfld, offset);
					s.Add(CilOpCodes.Ldarg_1);
					s.Add(CilOpCodes.Call, SetInstanceFieldValue.MakeGenericInstanceMethod(target.ToTypeSignature()));
					s.Add(CilOpCodes.Ret);

					clone.Methods.Add(getter);
					clone.Methods.Add(setter);
					var prop = new PropertyDefinition(field.Name, PropertyAttributes.None, new PropertySignature(CallingConventionAttributes.HasThis, target.ToTypeSignature(), Array.Empty<TypeSignature>()));
					prop.SetSemanticMethods(getter, setter);
					clone.Properties.Add(prop);
				}
			}
		}

		foreach(var method in def.Methods)
		{
			if (method.IsVirtual || method.IsConstructor || method.GenericParameters.Count != 0 || method.Parameters.Any(p => p.ParameterType is GenericInstanceTypeSignature)) continue;
			int paramCount = method.Parameters.Count;
			if (paramCount > 5) continue; // wip
			if (method.IsStatic == false && _this == null) continue;
			if (method.Name[0] == '<') continue;

			var rettype = GetProxyTypeSafe(method.Signature.ReturnType.Resolve());
			var args = method.Signature.ParameterTypes.Select(p => GetProxyTypeSafe(p.Resolve()));
			if (args.Any(a => a == null) || rettype == null) continue;
			var trueArgs = args.Select(a => a.ToTypeSignature());

			var info = new FieldDefinition($"{method.Name}_info", FieldAttributes.Static, SDKModule.CorLibTypeFactory.UIntPtr);
			clone.Fields.Add(info);

			init.Add(CilOpCodes.Ldsfld, klass);
			init.Add(CilOpCodes.Ldstr, $":{method.Name}({string.Join(',', method.Parameters.Select(p => Clear(p.ParameterType.Name)))})");
			init.Add(CilOpCodes.Call, GetMethodInfo);
			init.Add(CilOpCodes.Stsfld, info);
			
			var proxy = new MethodDefinition(method.Name, method.Attributes, method.IsStatic ? MethodSignature.CreateStatic(rettype.ToTypeSignature(), trueArgs) : MethodSignature.CreateInstance(rettype.ToTypeSignature(), trueArgs));
			for(int i = 0; i < proxy.Parameters.Count; i++)
				proxy.ParameterDefinitions.Add(new ParameterDefinition(method.ParameterDefinitions[i].Sequence, method.ParameterDefinitions[i].Name, (ParameterAttributes)0));

			proxy.IsPublic = true;
			var body = new CilMethodBody(proxy);
			proxy.CilMethodBody = body;
			var instr = body.Instructions;

			instr.Add(CilOpCodes.Ldsfld, info);

			if (method.IsStatic)
				instr.Add(CilOpCodes.Ldc_I4_0);
			else
			{
				instr.Add(CilOpCodes.Ldarg_0);
				instr.Add(CilOpCodes.Ldfld, _this);
			}

			for (int i = 0; i < paramCount; i++)
			{
				instr.Add(CilOpCodes.Ldarg, proxy.Parameters[i]);
			}

			bool retval = proxy.Signature.ReturnsValue;
			var retType = proxy.Signature.ReturnType;

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
					instr.Add(CilOpCodes.Call, Unbox);
				instr.Add(CilOpCodes.Call, ReadPtr.MakeGenericInstanceMethod(retType));
			}

			instr.Add(CilOpCodes.Ret);

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

	/*
	private void GenerateForLib(string lib)
	{
		{ // scope
			currentAssembly = AssemblyDefinition.ReadAssembly(lib, new ReaderParameters() { AssemblyResolver = this });
			var name = '_' + currentAssembly.Name.Name.Replace('-', '_').Replace('.', '_') + "DLL";
			GenerateAssemblyMetadataFile(currentAssembly, name, new FileInfo(lib).Name);
			var types = currentAssembly.MainModule.Types;
			foreach (var type in types)
			{
				if (type.Name.Contains('<') || type.HasGenericParameters) continue;
				source.Clear();
				WriteType(type, name);
			}
		}

		string managedFolder = managed.FullName;

		while (true)
		{
			var current = ActuallToParse;
			if (current.Count == 0) break;
			state = !state;
			foreach (var target in current)
			{
				//if (AlreadyParsed.Contains(target)) continue;

				var type = target.Resolve();
				var asm = type.Module.Assembly;
				if (!DefinedLibs.Keys.Contains(asm))
				{
					var name = '_' + asm.Name.Name.Replace('-', '_').Replace('.', '_') + "DLL";
					GenerateAssemblyMetadataFile(asm, name, new FileInfo(Path.Combine(managedFolder, asm.Name.Name + ".dll")).Name);
					DefinedLibs.Add(asm, name);
				}

				source.Clear();
				if (!File.Exists(Path.Combine(dest.FullName, type.Namespace.Replace('.', Path.DirectorySeparatorChar), type.Name + ".cs")))
					WriteType(type, DefinedLibs[asm]);
				//AlreadyParsed.Add(type);
			}
		}
	}

	private void WriteType(TypeDefinition type, string name, bool top = true)
	{
		//if (ActuallToParse.Contains(type))
		//	ActuallToParse.Remove(type);

		if (top)
		{
			source.AppendLine("using Reloaded.Memory.Pointers;\n"); // usings
			if (string.IsNullOrEmpty(type.Namespace))
				source.AppendLine("namespace GameSDK;\n");
			else
				source.AppendLine($"namespace GameSDK.{type.Namespace};\n");
		}
		else if (type.Name.Contains('<') || type.HasGenericParameters || type.IsGenericInstance)
		{
			return;
		}

		if (type.IsEnum)
		{
			source.AppendLine($"public enum {type.Name} : {SanitizePrimitive(type.GetEnumUnderlyingType().FullName)}{{\n");
			foreach (var field in type.Fields)
			{
				if (!field.Name.EndsWith("__"))
					source.AppendLine($"\t{Add(field.Name)} = {field.Constant},");
			}
			source.AppendLine("}");
			goto save;
		}

		var safeFields = type.Fields.Where(f => !f.FieldType.FullName.Contains('<') && !f.FieldType.IsArray && !f.Name.Contains('<') && !f.FieldType.IsPointer);
		var staticFields = safeFields.Where(f => f.IsStatic);
		var objectFields = safeFields.Where(f => !f.IsStatic);
		safeFields = null;
		bool isStruct = type.IsValueType;

		source.AppendLine($"public unsafe struct {type.Name} {{\n"); // struct name
		if (!isStruct)
			source.AppendLine($"\tpublic Pointer<long> _this;");

		source.AppendLine($"\tstatic long klass;\n"); // metadata for class
		source.AppendLine($"\tstatic long vtable;\n"); // metadata for class
		foreach (var field in staticFields) // create metadata for static fields
		{
			//if (!AlreadyParsed.Contains(field.FieldType))
			if (!File.Exists(Path.Combine(dest.FullName, type.Namespace.Replace('.', Path.DirectorySeparatorChar), type.Name + ".cs")))
				ActuallToParse.Add(field.FieldType);

			source.AppendLine($"\tstatic long {field.Name}_info_ptr;");
		}
		foreach (var field in objectFields) // create metadata for object fields
		{
			//if (!AlreadyParsed.Contains(field.FieldType))
			if (!File.Exists(Path.Combine(dest.FullName, type.Namespace.Replace('.', Path.DirectorySeparatorChar), type.Name + ".cs")))
				ActuallToParse.Add(field.FieldType);

			source.AppendLine($"\tstatic long {field.Name}_info_ptr;");
			source.AppendLine($"\tstatic long {field.Name}_offset;");
		}
		source.AppendLine($"\tstatic {type.Name}() {{"); // get metadata
		source.AppendLine($"\t\tvar info = MonoBridge.GetClassInfo({name}.image, (uint){type.MetadataToken.ToUInt32()});"); // for class
		source.AppendLine($"\t\tklass = info.klass;");
		source.AppendLine($"\t\tvtable = info.vtable;");
		foreach (var field in staticFields) // for static fields
		{
			source.AppendLine($"\t\t{field.Name}_info_ptr = MonoBridge.GetFieldInfo(klass, (uint){field.MetadataToken.ToUInt32()});");
		}
		foreach (var field in objectFields) // for object fields
		{
			source.AppendLine($"\t\t{field.Name}_info_ptr = MonoBridge.GetFieldInfo(klass, (uint){field.MetadataToken.ToUInt32()});");
			source.AppendLine($"\t\t{field.Name}_offset = MonoBridge.GetFieldOffset({field.Name}_info_ptr);");
		}
		source.AppendLine($"\t}}\n"); // end for metedata

		if (!isStruct)
		{
			source.AppendLine($"\tpublic {type.Name}(nuint address) {{\n"); // constructor for class
			source.AppendLine($"\t\t_this = new Pointer<long>(address);");
			source.AppendLine($"\t}}\n");
			source.AppendLine($"\tpublic {type.Name}(void* address) {{\n"); // constructor for class
			source.AppendLine($"\t\t_this = new Pointer<long>((nuint)address);");
			source.AppendLine($"\t}}\n");
		}
		//else
		//	source.AppendLine($"\tpublic {type.Name}() {{\n\t}}\n"); // constructor for struct

		foreach (var field in staticFields) // create proxy for static fields
		{
			string fullname = Sanitize(field.FieldType.IsRequiredModifier ? field.FieldType.FullName.Split(' ')[0] : field.FieldType.FullName);
			string sanitized = SanitizePrimitive(fullname);
			string fieldtype = field.FieldType.IsValueType ? (sanitized != fullname ? sanitized : "GameSDK." + Sanitize(fullname)) : $"Pointer<GameSDK.{Sanitize(fullname)}>";
			//fieldtype = SanitizePrimitive(fieldtype);
			bool convert = fieldtype.StartsWith("Pointer<");
			if (convert)
			{
				source.AppendLine($"\tpublic static GameSDK.{Sanitize(fullname)} {Add(field.Name)} {{ // static field");
				source.AppendLine($"\t\tget => new GameSDK.{Sanitize(fullname)}(MonoBridge.GetStaticFieldValue<nuint>(vtable, {field.Name}_info_ptr));");
				source.AppendLine($"\t\tset => MonoBridge.SetStaticFieldValue<nuint>(vtable, {field.Name}_info_ptr, (nuint)value._this.Address);");
			}
			else
			{
				source.AppendLine($"\tpublic static {fieldtype} {Add(field.Name)} {{ // static field");
				source.AppendLine($"\t\tget => MonoBridge.GetStaticFieldValue<{fieldtype}>(vtable, {field.Name}_info_ptr);");
				source.AppendLine($"\t\tset => MonoBridge.SetStaticFieldValue<{fieldtype}>(vtable, {field.Name}_info_ptr, value);");
				
			}
			source.AppendLine($"\t}}");
		}

		foreach (var field in objectFields) // create proxy for object fields
		{
			string fullname = Sanitize(field.FieldType.IsRequiredModifier ? field.FieldType.FullName.Split(' ')[0] : field.FieldType.FullName);
			string sanitized = SanitizePrimitive(fullname);
			string fieldtype = field.FieldType.IsValueType ? (sanitized != fullname ? sanitized : "GameSDK." + Sanitize(fullname)) : $"Pointer<GameSDK.{Sanitize(fullname)}>";
			//bool convert = fieldtype.StartsWith("Pointer<");
			if (isStruct) // for structs just fields
			{
				source.AppendLine($"\tpublic ref {fieldtype} {Add(field.Name)}_Ref {{ // object field");
				source.AppendLine($"\t\tget => ref System.Runtime.CompilerServices.Unsafe.AsRef<{fieldtype}>(System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref this, {field.Name}_offset));");
			}
			else // for pointers: new Pointer(baseptr + offset);
			{
				source.AppendLine($"\tpublic Pointer<{fieldtype}> {Add(field.Name)}_Ref {{ // object ref field");
				source.AppendLine($"\t\tget => new Pointer<{fieldtype}>((nuint)_this.Address + {field.Name}_offset);");
			}
			source.AppendLine($"\t}}");
		}

		foreach(var t in type.NestedTypes)
		{
			WriteType(t, name, false);
		}

		source.AppendLine("}");
	save:

		if (!top) return;

		var path = Path.Combine(dest.FullName, type.Namespace.Replace('.', Path.DirectorySeparatorChar), type.Name + ".cs");
		var dir = new FileInfo(path).Directory;
		if (!dir.Exists)
			dir.Create();

		File.WriteAllText(path, source.ToString());
	}

	private void GenerateAssemblyMetadataFile(AssemblyDefinition asm, string name, string dllname)
	{
		source.Clear();
		source.AppendLine("namespace GameSDK;\n");
		source.AppendLine($"internal static class {name} {{\n");
		source.AppendLine($"\tinternal static long image = MonoBridge.GetImage(\"{dllname.Replace(".dll", string.Empty)}\");");
		source.AppendLine("}");
		File.WriteAllText(Path.Combine(dest.FullName, name + ".cs"), source.ToString());
	}

	private void GenerateSTDFile()
	{
		File.WriteAllText(Path.Combine(dest.FullName, "MonoBrdige.cs"), @"using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Reloaded.Assembler;
using Reloaded.Injector;
using Reloaded.Memory.Buffers;
using Reloaded.Memory.Buffers.Internal.Testing;
using Reloaded.Memory.Pointers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Utilities;

namespace GameSDK;

public static unsafe class MonoBridge
{
	const string mono = ""mono-2.0-bdwgc.dll"";
	const string mono_domain_get = ""mono_get_root_domain"";
	const string mono_class_vtable = ""mono_class_vtable"";
	const string mono_field_get_offset = ""mono_field_get_offset"";
	const string mono_image_loaded = ""mono_image_loaded"";
	const string mono_class_get = ""mono_class_get"";
	const string mono_field_static_get_value = ""mono_field_static_get_value"";
	const string mono_field_static_set_value = ""mono_field_static_set_value"";
	const string mono_class_get_field = ""mono_class_get_field"";
	const string mono_runtime_invoke = ""mono_runtime_invoke"";

	static long mono_domain_get_ptr;
	static long mono_class_vtable_ptr;
	static long mono_field_get_offset_ptr;
	static long mono_image_loaded_ptr;
	static long mono_class_get_ptr;
	static long mono_field_static_get_value_ptr;
	static long mono_field_static_set_value_ptr;
	static long mono_class_get_field_ptr;
	static long mono_runtime_invoke_ptr;

	static Process game;
	static Shellcode shell;
	static Assembler asm;
	static MemoryBufferHelper bufferHelper;
	static PrivateMemoryBuffer proxyBuffer;
	static CircularBuffer stringsBuffer;
	static CircularBuffer argsBuffer;
	static IMemory gameMemory;
	static Pointer<long> result;
	static Pointer<long> func;
	static Pointer<long> arg1;
	static Pointer<long> arg2;
	static Pointer<long> arg3;
	static Pointer<long> refPointer;

	static long proxy;
	static long monomodule;

	static long domain;

	static object locker = new object();

	public static void Init(Process target)
	{
		game = target;
		shell = new Shellcode(game);
		bufferHelper = new MemoryBufferHelper(game);
		proxyBuffer = bufferHelper.CreatePrivateMemoryBuffer(512);
		gameMemory = proxyBuffer.MemorySource;
		stringsBuffer = new CircularBuffer(2048, gameMemory);
		argsBuffer = new CircularBuffer(1024, gameMemory);
		result = new Pointer<long>(proxyBuffer.Add(8), false, gameMemory);
		func = new Pointer<long>(proxyBuffer.Add(8), false, gameMemory);
		arg1 = new Pointer<long>(proxyBuffer.Add(8), false, gameMemory);
		arg2 = new Pointer<long>(proxyBuffer.Add(8), false, gameMemory);
		arg3 = new Pointer<long>(proxyBuffer.Add(8), false, gameMemory);
		refPointer = new Pointer<long>(proxyBuffer.Add(256), false, gameMemory);
		asm = new Assembler();
		var _call = AssembleCall();
		proxy = (long)proxyBuffer.Add(_call);
		foreach (ProcessModule module in game.Modules)
		{
			if (module.ModuleName == mono)
			{
				monomodule = (long)module.BaseAddress;
				break;
			}
		}

		mono_domain_get_ptr = shell.GetProcAddress(monomodule, mono_domain_get);
		mono_image_loaded_ptr = shell.GetProcAddress(monomodule, mono_image_loaded);
		mono_class_get_ptr = shell.GetProcAddress(monomodule, mono_class_get);
		mono_class_vtable_ptr = shell.GetProcAddress(monomodule, mono_class_vtable);
		mono_field_get_offset_ptr = shell.GetProcAddress(monomodule, mono_field_get_offset);
		mono_field_static_get_value_ptr = shell.GetProcAddress(monomodule, mono_field_static_get_value);
		mono_field_static_set_value_ptr = shell.GetProcAddress(monomodule, mono_field_static_set_value);
		mono_class_get_field_ptr = shell.GetProcAddress(monomodule, mono_class_get_field);
		mono_runtime_invoke_ptr = shell.GetProcAddress(monomodule, mono_runtime_invoke);
		

		domain = CallFunc(mono_domain_get_ptr);
		proxy = (long)proxyBuffer.Add(AssembleThreadSafeCall());
	}

	public static void Free()
	{
		stringsBuffer.Dispose();
		proxyBuffer.Dispose();
	}

	internal static long GetImage(string dll)
	{
		lock (locker)
		{
			return CallFunc(mono_image_loaded_ptr, (long)stringsBuffer.Add(Encoding.UTF8.GetBytes(dll + '\0')));
		}
	}

	internal static (long klass, long vtable) GetClassInfo(long image, uint token)
	{
		lock (locker)
		{
			var klass = CallFunc(mono_class_get_ptr, image, (long)token);
			var vtable = CallFunc(mono_class_vtable_ptr, domain, klass);
			return (klass, vtable);
		}
	}

	internal static long GetFieldInfo(long klass, uint token)
	{
		lock (locker)
		{
			return CallFunc(mono_class_get_field_ptr, klass, (long)token);
		}
	}

	internal static T GetStaticFieldValue<T>(long vtable, long field)
	{
		lock (locker)
		{
			CallFunc(mono_field_static_get_value_ptr, vtable, field, (long)refPointer.Address);
			return new Pointer<T>((nuint)refPointer.Address, true, gameMemory).GetValue();
		}
	}

	internal static void SetStaticFieldValue<T>(long vtable, long field, T value)
	{
		lock (locker)
		{
			var ptr = new Pointer<T>((nuint)refPointer.Address, false, gameMemory);
			ptr.SetValue(value);
			CallFunc(mono_field_static_set_value_ptr, vtable, field, (long)refPointer.Address);
		}
	}

	internal static long GetFieldOffset(long field)
	{
		lock(locker)
		{
			return CallFunc(mono_field_get_offset_ptr, field);
		}
	}

	internal static long CallFunc(long _func, long _arg1 = 0, long _arg2 = 0, long _arg3 = 0)
	{
		func.SetValue(_func);
		arg1.SetValue(_arg1);
		arg2.SetValue(_arg2);
		arg3.SetValue(_arg3);

		var hThread = CreateRemoteThread(game.Handle, IntPtr.Zero, 0, (IntPtr)proxy, (IntPtr)0, 0, out var lpThreadId);
		WaitForSingleObject(hThread, uint.MaxValue);
		GetExitCodeThread(hThread, out var lpExitCode);
		return result.GetValue();
	}

	private static long CallMonoFunctionImpl(long func, long _this, long args) => CallFunc(mono_runtime_invoke_ptr, func, _this, args);

	internal static long CallMonoFunction(long func, long _this)
	{
		lock (locker)
			return CallMonoFunctionImpl(func, _this, 0);
	}

	internal static long CallMonoFunction<T>(long func, long _this, T arg1) where T : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			refPointer.SetValue((long)_arg1);
			return CallMonoFunctionImpl(func, _this, (long)refPointer.Address);
		}
	}

	internal static long CallMonoFunction<T1, T2>(long func, long _this, T1 arg1, T2 arg2) where T1 : struct where T2 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			argsBuffer.Add(ref arg2);
			refPointer.SetValue((long)_arg1);
			return CallMonoFunctionImpl(func, _this, (long)refPointer.Address);
		}
	}

	internal static long CallMonoFunction<T1, T2, T3>(long func, long _this, T1 arg1, T2 arg2, T3 arg3) where T1 : struct where T2 : struct where T3 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			argsBuffer.Add(ref arg2);
			argsBuffer.Add(ref arg3);
			refPointer.SetValue((long)_arg1);
			return CallMonoFunctionImpl(func, _this, (long)refPointer.Address);
		}
	}

	internal static long CallMonoFunction<T1, T2, T3, T4, T5>(long func, long _this, T1 arg1, T2 arg2, T3 arg3, T4 arg4) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			argsBuffer.Add(ref arg2);
			argsBuffer.Add(ref arg3);
			argsBuffer.Add(ref arg4);
			refPointer.SetValue((long)_arg1);
			return CallMonoFunctionImpl(func, _this, (long)refPointer.Address);
		}
	}

	internal static long CallMonoFunction<T1, T2, T3, T4, T5>(long func, long _this, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			argsBuffer.Add(ref arg2);
			argsBuffer.Add(ref arg3);
			argsBuffer.Add(ref arg4);
			argsBuffer.Add(ref arg5);
			refPointer.SetValue((long)_arg1);
			return CallMonoFunctionImpl(func, _this, (long)refPointer.Address);
		}
	}

	static byte[] AssembleCall() => asm.Assemble(new string[] {
		""use64"",
		""sub rsp, 40"",
		$""mov rax, qword [qword {(long)func.Address}]"", // func ptr
		$""mov rcx, qword[qword {(long)arg1.Address}]"", // arg1
		$""mov rdx, qword[qword {(long)arg2.Address}]"", // arg2
		$""mov r8, qword[qword {(long)arg3.Address}]"", // arg3
		 ""call rax"", // call func ptr
		$""mov qword [qword {(long)result.Address}], rax"", // set result
		 ""add rsp, 40"",
		 ""ret""
	});

	static byte[] AssembleThreadSafeCall() => asm.Assemble(new string[] {
		""use64"",
		""sub rsp, 40"",
		$""mov rax, {mono_domain_get_ptr}"", // func ptr
		$""mov rcx, {domain}"", // func ptr
		 ""call rax"", // call func ptr
		$""mov rax, qword [qword {(long)func.Address}]"", // func ptr
		$""mov rcx, qword[qword {(long)arg1.Address}]"", // arg1
		$""mov rdx, qword[qword {(long)arg2.Address}]"", // arg2
		$""mov r8, qword[qword {(long)arg3.Address}]"", // arg3
		 ""call rax"", // call func ptr
		$""mov qword [qword {(long)result.Address}], rax"", // set result
		 ""add rsp, 40"",
		 ""ret""
	});

	[DllImport(""kernel32.dll"")]
static extern IntPtr CreateRemoteThread(IntPtr hProcess,
	IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
	IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

[DllImport(""kernel32.dll"", SetLastError = true)]
static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

[DllImport(""kernel32.dll"", SetLastError = true)]
static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
}
");
	}
	*/

	private static string Clear(string name)
	{
		switch(name)
		{
			case "String": return "string";
			case "Single": return "single";
			case "Double": return "double";
			case "Boolean": return "bool";
			case "Byte": return "byte";
			case "SByte": return "sbyte";
			case "Int16": return "short";
			case "UInt16": return "ushort";
			case "Int32": return "int";
			case "UInt32": return "uint";
			case "Int64": return "long";
			case "UInt64": return "ulong";
			default:
				return name;
		}
	}

	private static string Sanitize(string str) => str.Replace('/', '.');
}
#if true
public static unsafe class MonoBridge
{
	const string mono = "mono-2.0-bdwgc.dll";
	const string mono_domain_get = "mono_get_root_domain";
	const string mono_thread_attach = "mono_thread_attach";
	const string mono_class_vtable = "mono_class_vtable";
	const string mono_field_get_offset = "mono_field_get_offset";
	const string mono_image_loaded = "mono_image_loaded";
	const string mono_class_get = "mono_class_get";
	const string mono_field_static_get_value = "mono_field_static_get_value";
	const string mono_field_static_set_value = "mono_field_static_set_value";
	const string mono_class_get_field = "mono_class_get_field";
	const string mono_runtime_invoke = "mono_runtime_invoke";
	const string mono_class_get_method_from_name = "mono_class_get_method_from_name";
	const string mono_method_desc_new = "mono_method_desc_new";
	const string mono_method_desc_search_in_class = "mono_method_desc_search_in_class";
	const string mono_method_desc_free = "mono_method_desc_free";
	const string mono_object_unbox = "mono_object_unbox";

	static nuint mono_domain_get_ptr;
	static nuint mono_thread_attach_ptr;
	static nuint mono_class_vtable_ptr;
	static nuint mono_field_get_offset_ptr;
	static nuint mono_image_loaded_ptr;
	static nuint mono_class_get_ptr;
	static nuint mono_field_static_get_value_ptr;
	static nuint mono_field_static_set_value_ptr;
	static nuint mono_class_get_field_ptr;
	static nuint mono_runtime_invoke_ptr;
	static nuint mono_class_get_method_from_name_ptr;
	static nuint mono_method_desc_new_ptr;
	static nuint mono_method_desc_search_in_class_ptr;
	static nuint mono_method_desc_free_ptr;
	static nuint mono_object_unbox_ptr;

	static Process game;
	static Shellcode shell;
	static Assembler asm;
	static MemoryBufferHelper bufferHelper;
	static PrivateMemoryBuffer proxyBuffer;
	static CircularBuffer stringsBuffer;
	static CircularBuffer argsBuffer;
	internal static IMemory gameMemory;
	static Pointer<nuint> result;
	static Pointer<nuint> func;
	static Pointer<nuint> arg1;
	static Pointer<nuint> arg2;
	static Pointer<nuint> arg3;
	static Pointer<nuint> arg4;
	static Pointer<nuint> refPointer;
	public static Pointer<nuint> refException;

	static nuint proxy;
	static long monomodule;

	static nuint domain;

	static object locker = new object();

	public static void Init(Process target)
	{
		game = target;
		shell = new Shellcode(game);
		bufferHelper = new MemoryBufferHelper(game);
		proxyBuffer = bufferHelper.CreatePrivateMemoryBuffer(512);
		gameMemory = proxyBuffer.MemorySource;
		stringsBuffer = new CircularBuffer(2048, gameMemory);
		argsBuffer = new CircularBuffer(1024, gameMemory);
		result = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		func = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		arg1 = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		arg2 = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		arg3 = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		arg4 = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		refPointer = new Pointer<nuint>(proxyBuffer.Add(256), false, gameMemory);
		refException = new Pointer<nuint>(proxyBuffer.Add(8), false, gameMemory);
		asm = new Assembler();
		var _call = AssembleCall();
		proxy = proxyBuffer.Add(_call);
		foreach (ProcessModule module in game.Modules)
		{
			if (module.ModuleName == mono)
			{
				monomodule = (long)module.BaseAddress;
				break;
			}
		}

		mono_domain_get_ptr = (nuint)shell.GetProcAddress(monomodule, mono_domain_get);
		mono_thread_attach_ptr = (nuint)shell.GetProcAddress(monomodule, mono_thread_attach);
		mono_image_loaded_ptr = (nuint)shell.GetProcAddress(monomodule, mono_image_loaded);
		mono_class_get_ptr = (nuint)shell.GetProcAddress(monomodule, mono_class_get);
		mono_class_vtable_ptr = (nuint)shell.GetProcAddress(monomodule, mono_class_vtable);
		mono_field_get_offset_ptr = (nuint)shell.GetProcAddress(monomodule, mono_field_get_offset);
		mono_field_static_get_value_ptr = (nuint)shell.GetProcAddress(monomodule, mono_field_static_get_value);
		mono_field_static_set_value_ptr = (nuint)shell.GetProcAddress(monomodule, mono_field_static_set_value);
		mono_class_get_field_ptr = (nuint)shell.GetProcAddress(monomodule, mono_class_get_field);
		mono_runtime_invoke_ptr = (nuint)shell.GetProcAddress(monomodule, mono_runtime_invoke);
		mono_class_get_method_from_name_ptr = (nuint)shell.GetProcAddress(monomodule, mono_class_get_method_from_name);
		mono_method_desc_new_ptr = (nuint)shell.GetProcAddress(monomodule, mono_method_desc_new);
		mono_method_desc_search_in_class_ptr = (nuint)shell.GetProcAddress(monomodule, mono_method_desc_search_in_class);
		mono_method_desc_free_ptr = (nuint)shell.GetProcAddress(monomodule, mono_method_desc_free);
		mono_object_unbox_ptr = (nuint)shell.GetProcAddress(monomodule, mono_object_unbox);

#if DEBUG
		Console.WriteLine($"mono_domain_get_ptr {mono_domain_get_ptr}");
		Console.WriteLine($"mono_thread_attach_ptr {mono_thread_attach_ptr}");
		Console.WriteLine($"mono_image_loaded_ptr {mono_image_loaded_ptr}");
		Console.WriteLine($"mono_class_get_ptr {mono_class_get_ptr}");
		Console.WriteLine($"mono_class_vtable_ptr {mono_class_vtable_ptr}");
		Console.WriteLine($"mono_field_get_offset_ptr {mono_field_get_offset_ptr}");
		Console.WriteLine($"mono_field_static_get_value_ptr {mono_field_static_get_value_ptr}");
		Console.WriteLine($"mono_field_static_set_value_ptr {mono_field_static_set_value_ptr}");
		Console.WriteLine($"mono_class_get_field_ptr {mono_class_get_field_ptr}");
		Console.WriteLine($"mono_runtime_invoke_ptr {mono_runtime_invoke_ptr}");
		Console.WriteLine($"mono_class_get_method_from_name_ptr {mono_class_get_method_from_name_ptr}");
		Console.WriteLine($"mono_method_desc_new_ptr {mono_method_desc_new_ptr}");
		Console.WriteLine($"mono_method_desc_search_in_class_ptr {mono_method_desc_search_in_class_ptr}");
		Console.WriteLine($"mono_method_desc_free_ptr {mono_method_desc_free_ptr}");
		Console.WriteLine($"mono_object_unbox_ptr {mono_object_unbox_ptr}");
#endif

		domain = CallFunc(mono_domain_get_ptr);
		proxy = proxyBuffer.Add(AssembleThreadSafeCall());
	}

	public static void Free()
	{
		stringsBuffer.Dispose();
		proxyBuffer.Dispose();
	}

	internal static nuint GetImage(string dll)
	{
		lock (locker)
		{
			var image = CallFunc(mono_image_loaded_ptr, stringsBuffer.Add(Encoding.UTF8.GetBytes(dll.Replace(".dll", null))));
#if DEBUG
			Console.WriteLine($"GetImage({dll}) -> {image}");
#endif
			return image;
		}
	}

	internal static void GetClassInfo(nuint image, uint token, ref nuint klass, ref nuint vtable)
	{
		lock (locker)
		{
			klass = CallFunc(mono_class_get_ptr, image, token);
			vtable = CallFunc(mono_class_vtable_ptr, domain, klass);
#if DEBUG
			Console.WriteLine($"GetClassInfo({image}, {token}, {klass}, {vtable})");
#endif
		}
	}

	internal static nuint GetFieldInfo(nuint klass, uint token)
	{
		lock (locker)
		{
			var info = CallFunc(mono_class_get_field_ptr, klass, (nuint)token);
#if DEBUG
			Console.WriteLine($"GetFieldInfo({klass}, {token}) -> {info}");
#endif
			return info;
		}
	}

	internal static T GetStaticFieldValue<T>(nuint vtable, nuint field)
	{
		lock (locker)
		{
			CallFunc(mono_field_static_get_value_ptr, vtable, field, (nuint)refPointer.Address);
			return new Pointer<T>((nuint)refPointer.Address, false, gameMemory).GetValue();
		}
	}

	internal static void SetStaticFieldValue<T>(nuint vtable, nuint field, T value)
	{
		lock (locker)
		{
			var ptr = new Pointer<T>((nuint)refPointer.Address, false, gameMemory);
			ptr.SetValue(value);
			CallFunc(mono_field_static_set_value_ptr, vtable, field, (nuint)refPointer.Address);
		}
	}

	internal static nuint GetFieldOffset(nuint field)
	{
		lock(locker)
		{
			return CallFunc(mono_field_get_offset_ptr, field);
		}
	}

	internal static nuint CallFunc(nuint _func, nuint _arg1 = 0, nuint _arg2 = 0, nuint _arg3 = 0, nuint _arg4 = 0)
	{
		func.SetValue(_func);
		arg1.SetValue(_arg1);
		arg2.SetValue(_arg2);
		arg3.SetValue(_arg3);
		arg4.SetValue(_arg4);

		var hThread = CreateRemoteThread(game.Handle, IntPtr.Zero, 0, proxy, (IntPtr)0, 0, out var lpThreadId);
		WaitForSingleObject(hThread, uint.MaxValue);
		GetExitCodeThread(hThread, out var lpExitCode);
		return result.GetValue();
	}

	internal static nuint Unbox(nuint boxed)
	{
		if (boxed == 0)
		{
#if DEBUG
			Console.WriteLine($"Unbox(0) -> 0");
#endif
			return 0;
		}
		return CallFunc(mono_object_unbox_ptr, boxed);
	}

	internal static nuint GetMonoFunction(nuint klass, string descr)
	{
		lock(locker)
		{
			var desc = CallFunc(mono_method_desc_new_ptr, stringsBuffer.Add(Encoding.UTF8.GetBytes(descr)));
			var func = CallFunc(mono_method_desc_search_in_class_ptr, desc, klass);
#if DEBUG
			Console.WriteLine($"GetMonoFunction({klass}, {descr}) -> func at ptr {func}");
#endif
			CallFunc(mono_method_desc_free_ptr, desc);
			return func;
		}
	}

	private static nuint CallMonoFunctionImpl(nuint func, nuint _this, nuint args)
	{
#if DEBUG
		Console.WriteLine($"CallMonoFunctionImpl({func}, {_this}, {args})");
#endif
		var result = CallFunc(mono_runtime_invoke_ptr, func, _this, args, (nuint)refException.Address);
#if DEBUG
		Console.WriteLine($"CallMonoFunctionImpl() returns {result}");
#endif
		return result;
	}

	internal static nuint CallMonoFunction(nuint func, nuint _this)
	{
		lock (locker)
			return CallMonoFunctionImpl(func, _this, 0);
	}

	internal static nuint CallMonoFunction<T>(nuint func, nuint _this, T arg1) where T : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			refPointer.SetValue(_arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)refPointer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2>(nuint func, nuint _this, T1 arg1, T2 arg2) where T1 : struct where T2 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			var _arg2 = argsBuffer.Add(ref arg2);
			refPointer.SetValue(_arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)refPointer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3>(nuint func, nuint _this, T1 arg1, T2 arg2, T3 arg3) where T1 : struct where T2 : struct where T3 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			var _arg2 = argsBuffer.Add(ref arg2);
			var _arg3 = argsBuffer.Add(ref arg3);
			refPointer.SetValue(_arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)refPointer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3, T4>(nuint func, nuint _this, T1 arg1, T2 arg2, T3 arg3, T4 arg4) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			var _arg2 = argsBuffer.Add(ref arg2);
			var _arg3 = argsBuffer.Add(ref arg3);
			var _arg4 = argsBuffer.Add(ref arg4);
			refPointer.SetValue(_arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)refPointer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3, T4, T5>(nuint func, nuint _this, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
	{
		lock (locker)
		{
			argsBuffer.Offset = 0;
			var _arg1 = argsBuffer.Add(ref arg1);
			var _arg2 = argsBuffer.Add(ref arg2);
			var _arg3 = argsBuffer.Add(ref arg3);
			var _arg4 = argsBuffer.Add(ref arg4);
			var _arg5 = argsBuffer.Add(ref arg5);
			refPointer.SetValue(_arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)refPointer.Address);
		}
	}

	internal static T GetField<T>(UIntPtr _this, uint offset)
	{
		return new Pointer<T>((nuint)_this + (nuint)offset, false, gameMemory).GetValue();
	}

	internal static void SetField<T>(UIntPtr _this, uint offset, T value)
	{
		new Pointer<T>((nuint)_this + (nuint)offset, false, gameMemory).SetValue(ref value);
	}

	internal static T Read<T>(nuint target) where T : struct // ignore wrong name
	{
		if (target == 0)
		{
#if DEBUG
			Console.WriteLine($"READ ZERO!!!!");
#endif
			return default;
		}
#if DEBUG
		Console.WriteLine($"READ at {target}");
#endif
		//return new Pointer<T>(target, true, gameMemory).GetValue();
		return *(T*)&target; // haha pointers go brr
	}

	static byte[] AssembleCall() => asm.Assemble(new string[] {
		"use64",
		"sub rsp, 40",
		$"mov rax, qword [qword {(long)func.Address}]", // func ptr
		$"mov rcx, qword[qword {(long)arg1.Address}]", // arg1
		$"mov rdx, qword[qword {(long)arg2.Address}]", // arg2
		$"mov r8, qword[qword {(long)arg3.Address}]", // arg3
		$"mov r9, qword[qword {(long)arg4.Address}]", // arg4
		 "call rax", // call func ptr
		$"mov qword [qword {(long)result.Address}], rax", // set result
		 "add rsp, 40",
		 "ret"
	});

	static byte[] AssembleThreadSafeCall() => asm.Assemble(new string[] {
		"use64",
		"sub rsp, 40",
		$"mov rax, {mono_thread_attach_ptr}", // func ptr
		$"mov rcx, {domain}", // domaint
		 "call rax", // call func ptr
		$"mov rax, qword [qword {(long)func.Address}]", // func ptr
		$"mov rcx, qword[qword {(long)arg1.Address}]", // arg1
		$"mov rdx, qword[qword {(long)arg2.Address}]", // arg2
		$"mov r8, qword[qword {(long)arg3.Address}]", // arg3
		$"mov r9, qword[qword {(long)arg4.Address}]", // arg4
		 "call rax", // call func ptr
		$"mov qword [qword {(long)result.Address}], rax", // set result
		 "add rsp, 40",
		 "ret"
	});

	[DllImport("kernel32.dll")]
	static extern IntPtr CreateRemoteThread(IntPtr hProcess,
		IntPtr lpThreadAttributes, uint dwStackSize, nuint lpStartAddress,
		IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
}
#endif