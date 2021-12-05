
# ModPhuserEx

ModPhuserEx is a open-source protector for .NET applications.
It is a fork of the now discontinued [ConfuserEx](https://github.com/yck1509/ConfuserEx/) project.

## Features

* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5
* (**NEW!**) [Experimental support for .NET Core/ASP.NET Core](https://github.com/0xFireball/ModPhuserEx/wiki/Working-with-.NET-Core)
* Symbol renaming (Support WPF/BAML, JSON.NET, ASP.NET Core, NancyFx)
* Protection against debuggers/profilers
* Protection against memory dumping
* Protection against tampering (method encryption)
* Control flow obfuscation
* Constant/resources encryption
* Reference hiding proxies
* Disable decompilers
* Embedding dependency
* Compressing output
* Extensible plugin API
* Many more are coming!

## Usage

`Confuser.CLI <path to project file>`

The project file is a ConfuserEx Project (*.crproj).
The format of project file can be found in docs\ProjectFormat.md

## Found a bug?

See the [Issues](issues/) section of project.


## License

See LICENSE file for details.

## Credits

**[0xFireball](https://github.com/0xFireball)** for maintaining ModPhuserEx and improving on the awesome ConfuserEx!  
**[yck1509](https://github.com/yck1509)** for developing ConfuserEx!  
**[0xd4d](https://github.com/0xd4d)** for his awesome work and extensive knowledge!  
Members of **[Black Storm Forum](http://board.b-at-s.info/)** for their help!
