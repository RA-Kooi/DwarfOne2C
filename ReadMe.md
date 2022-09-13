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
DwarfOne2C.exe list YourGame-dwarf.txt

#Fictional output: C:\GameCube\src\main.cpp

# Pick a list files from the output and put them in a file called files.txt
# Make a list of paths you want to strip from the file paths in files.txt and put them in a file called split.txt

# split.txt contents: C:\GameCube\

# Output in a folder called out
DwarfOne2C.exe dump YourGame-dwarf.txt files.txt split.txt -o out
```

If you're lucky and it didn't crash, you should now have a file called `main.cpp` in `.out\src\`
