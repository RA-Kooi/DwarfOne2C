# DwarfOne2C

Parses output from DWARFOne by LuigiBlood (https://github.com/LuigiBlood/dwarfone) and turns it into C(++) code.

Heavily tailored towards MetroWerks compiled GameCube games.  
May or may not work with all MetroWerks compiled GC games, probably crashes and burns on Wii games.

# How to build

Install dotnet SDK and runtimes (included with Visual Studio).

```sh
dotnet publish -c Release
```

The compiled project will be in `.\DwarfOne2C\bin\Release\net5.0\publish`.  
Optionally copy all files in the publish folder to somewhere in your `$PATH` for easy access.

# How to use

As an example we will take a fictional game called YourGame which has an elf with debug info included.

```sh
dwarfone YourGame.elf > YourGame-dwarf.txt
DwarfOne2C.exe --list-files YourGame-dwarf.txt

# Pick a file from the output

# Output in current folder, add path to output folder at the end if you want it to output somewhere else
DwarfOne2C.exe YourGame-dwarf.txt C:\YourGame\ C:\YourGame\src\main.cpp
```

If you're lucky and it didn't crash, you should now have a file called `main.cpp` in `.\src\`
