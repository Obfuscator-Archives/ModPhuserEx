using System.Reflection;

[assembly: AssemblyProduct("ModPhuserEx")]
[assembly: AssemblyCompany("IridiumIon Software")]
[assembly: AssemblyCopyright("Copyright (C) 0xFireball 2016, Ki 2014")]

#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
#else

[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyVersion("{{VER}}")]
[assembly: AssemblyFileVersion("{{VER}}")]
[assembly: AssemblyInformationalVersion("{{TAG}}")]