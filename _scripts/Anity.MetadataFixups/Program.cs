using Mono.Cecil;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Anity.MetadataFixups <Anity.Core.dll>");
    return 2;
}

string assemblyPath = Path.GetFullPath(args[0]);
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 2;
}

using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters
{
    InMemory = true,
    ReadSymbols = false
});

TypeDefinition gameObject = assembly.MainModule.GetType("UnityEngine.GameObject")
    ?? throw new InvalidOperationException("UnityEngine.GameObject was not found.");
MethodDefinition instantiateGameObjects = gameObject.Methods.Single(method =>
    method.Name == "InstantiateGameObjects" &&
    method.IsPublic &&
    method.IsStatic &&
    method.Parameters.Count == 5 &&
    method.Parameters[4].ParameterType.FullName == "UnityEngine.SceneManagement.Scene");

ParameterDefinition destinationScene = instantiateGameObjects.Parameters[4];
destinationScene.IsOptional = true;
destinationScene.Constant = null;

string temporaryPath = assemblyPath + $".metadata-fixup.{Environment.ProcessId}.{Guid.NewGuid():N}";
assembly.Write(temporaryPath, new WriterParameters { WriteSymbols = false });
File.Move(temporaryPath, assemblyPath, true);
return 0;
