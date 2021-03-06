﻿using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace MidiToMGBA {
    public static class DynamicDll {

        private readonly static IntPtr NULL = IntPtr.Zero;

        public static Dictionary<string, string> DllMap = new Dictionary<string, string>();

        // Windows
        [DllImport("kernel32")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32")]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        // Linux
        private const int RTLD_NOW = 2;
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPTStr)] string filename, int flags);
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPTStr)] string symbol);
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();

        private static IntPtr _EntryPoint = NULL;
        public static IntPtr EntryPoint {
            get {
                if (_EntryPoint != NULL)
                    return _EntryPoint;

                return _EntryPoint = OpenLibrary(null);
            }
        }

        private static IntPtr _Mono = NULL;
        public static IntPtr Mono {
            get {
                if (_Mono != NULL)
                    return _Mono;

                return _Mono = OpenLibrary(
                    Environment.OSVersion.Platform == PlatformID.Win32NT ? "mono.dll" :
                    Environment.OSVersion.Platform == PlatformID.MacOSX ? "libmono.0.dylib" :
                    "libmono.so"
                );
            }
        }

        private static IntPtr _PThread = NULL;
        public static IntPtr PThread {
            get {
                if (_PThread != NULL)
                    return _PThread;

                if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
                    return NULL;

                return _PThread = OpenLibrary(
                    Environment.OSVersion.Platform == PlatformID.MacOSX ? "libpthread.dylib" :
                    "libpthread.so"
                );
            }
        }

        public static IntPtr OpenLibrary(string name) {
            string mapped;
            if (DllMap.TryGetValue(name, out mapped))
                name = mapped;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                IntPtr lib = GetModuleHandle(name);
                if (lib == NULL) {
                    lib = LoadLibrary(name);
                }
                return lib;
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) {
                IntPtr e = IntPtr.Zero;
                IntPtr lib = dlopen(name, RTLD_NOW);
                if ((e = dlerror()) != IntPtr.Zero) {
                    Console.WriteLine($"PInvokeHelper can't access {name}!");
                    Console.WriteLine("dlerror: " + Marshal.PtrToStringAnsi(e));
                    return NULL;
                }
                return lib;
            }

            return NULL;
        }

        public static IntPtr GetFunction(this IntPtr lib, string name) {
            if (lib == NULL)
                return NULL;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return GetProcAddress(lib, name);

            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) {
                IntPtr s, e;

                s = dlsym(lib, name);
                if ((e = dlerror()) != IntPtr.Zero) {
                    Console.WriteLine("PInvokeHelper can't access " + name + "!");
                    Console.WriteLine("dlerror: " + Marshal.PtrToStringAnsi(e));
                    return NULL;
                }
                return s;
            }

            return NULL;
        }

        public static T GetDelegate<T>(this IntPtr lib, string name) where T : class {
            if (lib == NULL)
                return null;

            IntPtr s = lib.GetFunction(name);
            if (s == NULL)
                return null;

            return s.AsDelegate<T>();
        }
        public static T GetDelegateAtRVA<T>(this IntPtr basea, long rva) where T : class {
            return new IntPtr(basea.ToInt64() + rva).AsDelegate<T>();
        }
        public static T AsDelegate<T>(this IntPtr s) where T : class {
            return Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
        }

        // Windows
        [DllImport("kernel32")]
        private static extern uint GetCurrentThreadId();
        // Linux
        private delegate ulong d_pthread_self();
        private static d_pthread_self pthread_self;

        public static ulong CurrentThreadId {
            get {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    return GetCurrentThreadId();

                if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    return (pthread_self = pthread_self ?? PThread.GetDelegate<d_pthread_self>("pthread_self"))?.Invoke() ?? 0;

                return 0;
            }
        }

        public static void ResolveDynamicDllImports(this Type type) {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
                bool found = true;
                foreach (DynamicDllImportAttribute attrib in field.GetCustomAttributes(typeof(DynamicDllImportAttribute), true)) {
                    found = false;
                    IntPtr asm = OpenLibrary(attrib.DLL);
                    if (asm == NULL)
                        continue;

                    foreach (string ep in attrib.EntryPoints) {
                        IntPtr func = asm.GetFunction(ep);
                        if (func == NULL)
                            continue;
                        field.SetValue(null, Marshal.GetDelegateForFunctionPointer(func, field.FieldType));
                        found = true;
                        break;
                    }

                    if (found)
                        break;
                }
                if (!found)
                    throw new EntryPointNotFoundException($"No matching entry point found for {field.Name} in {field.DeclaringType.FullName}");
            }
        }

    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class DynamicDllImportAttribute : Attribute {
        public string DLL;
        public string[] EntryPoints;
        public DynamicDllImportAttribute(string dll, params string[] entryPoints) {
            DLL = dll;
            EntryPoints = entryPoints;
        }
    }
}
