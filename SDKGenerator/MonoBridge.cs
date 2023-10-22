using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Reloaded.Assembler;
using Reloaded.Injector;
using Reloaded.Memory.Buffers;
using Reloaded.Memory.Pointers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Utilities;

namespace SDKGenerator;
#if true
public static class MonoBridge
{
	const string mono = "mono.dll";
	const string mono2 = "mono-2.0-bdwgc.dll";
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
	const string mono_class_from_name = "mono_class_from_name";
	const string mono_reflection_type_from_name = "mono_reflection_type_from_name";
	const string mono_type_get_class = "mono_type_get_class";
	const string mono_string_new = "mono_string_new";
	const string mono_value_box = "mono_value_box";
	const string mono_class_from_mono_type = "mono_class_from_mono_type";
	const string mono_object_get_virtual_method = "mono_object_get_virtual_method";
	const string mono_gc_collect = "mono_gc_collect";
	const string mono_gc_max_generation = "mono_gc_max_generation";

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
	static nuint mono_reflection_type_from_name_ptr;
	static nuint mono_type_get_class_ptr;
	static nuint mono_string_new_ptr;
	static nuint mono_value_box_ptr;
	static nuint mono_class_from_mono_type_ptr;
	static nuint mono_object_get_virtual_method_ptr;
	static nuint mono_gc_collect_ptr;
	static nuint mono_gc_max_generation_ptr;

	static Process game;
	static Shellcode shell;
	static Assembler asm;
	static MemoryBufferHelper bufferHelper;
	static PrivateMemoryBuffer proxyBuffer;
	static CircularBuffer stringsBuffer;
	static CircularBuffer argsPtrBuffer;
	static CircularBuffer argsContentBuffer;
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
		stringsBuffer = new CircularBuffer(1024 * 4, gameMemory);
		argsPtrBuffer = new CircularBuffer(512, gameMemory);
		argsContentBuffer = new CircularBuffer(1024, gameMemory);
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
			if (module.ModuleName == mono || module.ModuleName == mono2)
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
		mono_reflection_type_from_name_ptr = (nuint)shell.GetProcAddress(monomodule, mono_reflection_type_from_name);
		mono_type_get_class_ptr = (nuint)shell.GetProcAddress(monomodule, mono_type_get_class);
		mono_string_new_ptr = (nuint)shell.GetProcAddress(monomodule, mono_string_new);
		mono_value_box_ptr = (nuint)shell.GetProcAddress(monomodule, mono_value_box);
		mono_class_from_mono_type_ptr = (nuint)shell.GetProcAddress(monomodule, mono_class_from_mono_type);
		mono_object_get_virtual_method_ptr = (nuint)shell.GetProcAddress(monomodule, mono_object_get_virtual_method);
		mono_gc_collect_ptr = (nuint)shell.GetProcAddress(monomodule, mono_gc_collect);
		mono_gc_max_generation_ptr = (nuint)shell.GetProcAddress(monomodule, mono_gc_max_generation);

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
		Console.WriteLine($"mono_reflection_type_from_name_ptr {mono_reflection_type_from_name_ptr}");
		Console.WriteLine($"mono_type_get_class_ptr {mono_type_get_class_ptr}");
		Console.WriteLine($"mono_string_new_ptr {mono_string_new_ptr}");
		Console.WriteLine($"mono_class_from_mono_type_ptr {mono_class_from_mono_type_ptr}");
		Console.WriteLine($"mono_object_get_virtual_method_ptr {mono_object_get_virtual_method_ptr}");
		Console.WriteLine($"mono_gc_collect_ptr {mono_gc_collect_ptr}");
		Console.WriteLine($"mono_gc_max_generation_ptr {mono_gc_max_generation_ptr}");
#endif

		domain = CallFunc(mono_domain_get_ptr);
		proxy = proxyBuffer.Add(AssembleThreadSafeCall());
	}

	public static void Free()
	{
		stringsBuffer.Dispose();
		proxyBuffer.Dispose();
	}

	public static void CollectGarbage()
	{
		lock(locker)
		{
			var maxgen = CallFunc(mono_gc_max_generation_ptr);
			for(nuint i = 1; i < maxgen; i++)
				CallFunc(mono_gc_collect_ptr, i);
		}
	}

	internal static nuint GetImage(string dll)
	{
		lock (locker)
		{
			var image = CallFunc(mono_image_loaded_ptr, stringsBuffer.Add(Encoding.UTF8.GetBytes(dll.Replace(".dll", null))));
#if DEBUG
			if (image == 0)
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
			if (klass == 0 || vtable == 0)
				Console.WriteLine($"GetClassInfo({image}, {token}, {klass}, {vtable})");
#endif
		}
	}

	internal static void GetClassInfoS(string genstr, ref nuint klass, ref nuint vtable)
	{
#if DEBUG
		Console.WriteLine($"GetClassInfoS for {genstr}");
#endif
		var type = GetTypeFromStr_Inject(genstr);

		lock (locker)
		{
			klass = CallFunc(mono_class_from_mono_type_ptr, type);
			vtable = 0; // skip at this moment
#if DEBUG
			Console.WriteLine($"GetClassInfoS result ({klass}, {vtable})");
#endif
		}
	}

	internal static nuint GetTypeFromStr_Inject(string str) => throw new Exception("Oh no, here we go again. No generics, bro.");

	internal static nuint GetFieldInfo(nuint klass, uint token)
	{
		lock (locker)
		{
			var info = CallFunc(mono_class_get_field_ptr, klass, (nuint)token);
#if false
			Console.WriteLine($"GetFieldInfo({klass}, {token}) -> {info}");
#endif
			return info;
		}
	}

	internal unsafe static T GetStaticFieldValue<T>(nuint vtable, nuint field)
	{
		lock (locker)
		{
			CallFunc(mono_field_static_get_value_ptr, vtable, field, (nuint)refPointer.Address);
			return new Pointer<T>((nuint)refPointer.Address, false, gameMemory).GetValue();
		}
	}

	internal unsafe static void SetStaticFieldValue<T>(nuint vtable, nuint field, T value)
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
		var res = result.GetValue();
#if DEBUG
		//Console.WriteLine($"CallFunc({_func}, {_arg1}, {_arg2}, {_arg3}, {_arg4}) -> {res}");
#endif
		return res;
	}

	public static nuint Unbox(nuint boxed)
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
			if (klass == 0 || func == 0)
				Console.WriteLine($"GetMonoFunction({klass}, {descr}) -> func at ptr {func}");
#endif
			CallFunc(mono_method_desc_free_ptr, desc);
			return func;
		}
	}

	internal static nuint GetMonoFunctionFromName(nuint klass, string name, int args)
	{
		lock(locker)
		{
			var result = CallFunc(mono_class_get_method_from_name_ptr, klass, stringsBuffer.Add(Encoding.UTF8.GetBytes(name + '\0')), (nuint)args);
#if DEBUG
			if (result == 0)
				Console.WriteLine($"Cant get func from name for {name}");
#endif
			return result;
		}
	}

	private unsafe static nuint CallMonoFunctionImpl(nuint func, nuint _this, nuint args)
	{
		if (func == 0)
			throw new Exception("Cant invoke nullptr method!");
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

	internal static nuint CallMonoFunction<T>(nuint func, nuint _this, T arg1, bool vt1) where T : struct
	{
		lock (locker)
		{
#if false
			Console.WriteLine($"CallMonoFunction<T>({_this}, {arg1}, {vt1})");
#endif

			argsPtrBuffer.Offset = 0;
			argsContentBuffer.Offset = 0;
			if (vt1)
			{
				var arg1Content = argsContentBuffer.Add(ref arg1);
				argsPtrBuffer.Add(ref arg1Content);
			}
			else
				argsPtrBuffer.Add(ref arg1);
			return CallMonoFunctionImpl(func, _this, (nuint)argsPtrBuffer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2>(nuint func, nuint _this, T1 arg1, bool vt1, T2 arg2, bool vt2) where T1 : struct where T2 : struct
	{
		lock (locker)
		{
			argsPtrBuffer.Offset = 0;
			argsContentBuffer.Offset = 0;
			if (vt1)
			{
				var arg1Content = argsContentBuffer.Add(ref arg1);
				argsPtrBuffer.Add(ref arg1Content);
			}
			else
				argsPtrBuffer.Add(ref arg1);
			if (vt2)
			{
				var arg2Content = argsContentBuffer.Add(ref arg2);
				argsPtrBuffer.Add(ref arg2Content);
			}
			else
				argsPtrBuffer.Add(ref arg2);
			return CallMonoFunctionImpl(func, _this, (nuint)argsPtrBuffer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3>(nuint func, nuint _this, T1 arg1, bool vt1, T2 arg2, bool vt2, T3 arg3, bool vt3) where T1 : struct where T2 : struct where T3 : struct
	{
		lock (locker)
		{
			argsPtrBuffer.Offset = 0;
			argsContentBuffer.Offset = 0;
			if (vt1)
			{
				var arg1Content = argsContentBuffer.Add(ref arg1);
				argsPtrBuffer.Add(ref arg1Content);
			}
			else
				argsPtrBuffer.Add(ref arg1);
			if (vt2)
			{
				var arg2Content = argsContentBuffer.Add(ref arg2);
				argsPtrBuffer.Add(ref arg2Content);
			}
			else
				argsPtrBuffer.Add(ref arg2);
			if (vt3)
			{
				var arg3Content = argsContentBuffer.Add(ref arg3);
				argsPtrBuffer.Add(ref arg3Content);
			}
			else
				argsPtrBuffer.Add(ref arg3);
			return CallMonoFunctionImpl(func, _this, (nuint)argsPtrBuffer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3, T4>(nuint func, nuint _this, T1 arg1, bool vt1, T2 arg2, bool vt2, T3 arg3, bool vt3, T4 arg4, bool vt4) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
	{
		lock (locker)
		{
			argsPtrBuffer.Offset = 0;
			argsContentBuffer.Offset = 0;
			if (vt1)
			{
				var arg1Content = argsContentBuffer.Add(ref arg1);
				argsPtrBuffer.Add(ref arg1Content);
			}
			else
				argsPtrBuffer.Add(ref arg1);
			if (vt2)
			{
				var arg2Content = argsContentBuffer.Add(ref arg2);
				argsPtrBuffer.Add(ref arg2Content);
			}
			else
				argsPtrBuffer.Add(ref arg2);
			if (vt3)
			{
				var arg3Content = argsContentBuffer.Add(ref arg3);
				argsPtrBuffer.Add(ref arg3Content);
			}
			else
				argsPtrBuffer.Add(ref arg3);
			if (vt4)
			{
				var arg4Content = argsContentBuffer.Add(ref arg4);
				argsPtrBuffer.Add(ref arg4Content);
			}
			else
				argsPtrBuffer.Add(ref arg4);
			return CallMonoFunctionImpl(func, _this, (nuint)argsPtrBuffer.Address);
		}
	}

	internal static nuint CallMonoFunction<T1, T2, T3, T4, T5>(nuint func, nuint _this, T1 arg1, bool vt1, T2 arg2, bool vt2, T3 arg3, bool vt3, T4 arg4, bool vt4, T5 arg5, bool vt5) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
	{
		lock (locker)
		{
			argsPtrBuffer.Offset = 0;
			argsContentBuffer.Offset = 0;
			if (vt1)
			{
				var arg1Content = argsContentBuffer.Add(ref arg1);
				argsPtrBuffer.Add(ref arg1Content);
			}
			else
				argsPtrBuffer.Add(ref arg1);
			if (vt2)
			{
				var arg2Content = argsContentBuffer.Add(ref arg2);
				argsPtrBuffer.Add(ref arg2Content);
			}
			else
				argsPtrBuffer.Add(ref arg2);
			if (vt3)
			{
				var arg3Content = argsContentBuffer.Add(ref arg3);
				argsPtrBuffer.Add(ref arg3Content);
			}
			else
				argsPtrBuffer.Add(ref arg3);
			if (vt4)
			{
				var arg4Content = argsContentBuffer.Add(ref arg4);
				argsPtrBuffer.Add(ref arg4Content);
			}
			else
				argsPtrBuffer.Add(ref arg4);
			if (vt5)
			{
				var arg5Content = argsContentBuffer.Add(ref arg5);
				argsPtrBuffer.Add(ref arg5Content);
			}
			else
				argsPtrBuffer.Add(ref arg5);
			return CallMonoFunctionImpl(func, _this, (nuint)argsPtrBuffer.Address);
		}
	}

	// you can clear dictionary manually
	public static Dictionary<(nuint, nuint), nuint> CachedVirtFunctions = new Dictionary<(nuint, nuint), nuint>();

	internal static nuint GetVirtFunction(nuint _this, nuint func)
	{
		lock(locker)
		{
			if (CachedVirtFunctions.TryGetValue((_this, func), out var result))
				return result;

			result = CallFunc(mono_object_get_virtual_method_ptr, _this, func);
#if DEBUG
			Console.WriteLine($"GetVirtFunction({_this}, {func}) -> {result}");
#endif
			if (result == 0)
				throw new Exception("Cant get virtual function!");

			CachedVirtFunctions.Add((_this, func), result);

			return result;
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

	public unsafe static T Cast<T>(nuint target) where T : struct
	{
#if DEBUG
		Console.WriteLine($"CAST at {target} to {typeof(T).FullName}");
#endif
		//return *(T*)&target; // haha pointers do memory leaks ><
		return Unsafe.As<nuint, T>(ref target);
	}

	public static T ReadValueType<T>(nuint target) where T : struct
	{
		var res = new Pointer<T>(target, true, gameMemory).GetValue();
#if DEBUG
		Console.WriteLine($"READ at {target} to {typeof(T).Name} -> {res}");
#endif
		return res;
	}

	public unsafe static nuint BoxValue(nuint klass, nuint val)
	{
		lock(locker)
		{
			refPointer.SetValue(val);
			return CallFunc(mono_value_box_ptr, domain, klass, (nuint)refPointer.Address);
		}
	}

	public unsafe static string ReadString(nuint ptr, int length)
	{
#if DEBUG
		Console.WriteLine($"ReadString at {ptr} with length {length}");
#endif
		gameMemory.ReadRaw(ptr, out byte[] chars, length * 2);
		fixed (byte* b = &chars[0])
			return new string(new ReadOnlySpan<char>(b, length));
	}

	public static nuint AllocStr(string str)
	{
		return CallFunc(mono_string_new_ptr, domain, stringsBuffer.Add(Encoding.UTF8.GetBytes(str + '\0')));
	}

	// Gets Type Info for usage in Type.Get.
	// Ex: List<int> -> System.Collections.Generic`1[[System.Int32]]
	internal static string ConstructGenericAQNFrom1(string main, Type gen1)
	{
		var gen1aqn = gen1.GetMethod("aqn").Invoke(null, null) as string;
		return main.Insert(main.LastIndexOf("`1")+2, $"[[{gen1aqn}]]");
	}

	static unsafe byte[] AssembleCall() => asm.Assemble(new string[] {
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

	static unsafe byte[] AssembleThreadSafeCall() => asm.Assemble(new string[] {
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