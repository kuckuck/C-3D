# C#3D

winget install Microsoft.DotNet.SDK.10 --source winget \
dotnet new console -o SilkFpsDemo \
cd SilkFpsDemo \
dotnet add package Silk.NET.Windowing \
dotnet add package Silk.NET.Input \
dotnet add package Silk.NET.OpenGL \
dotnet add package Silk.NET.Maths \
dotnet build \
dotnet run /p:AllowUnsafeBlocks=true \