using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace Anity.UnityApiParity;

public sealed class ApiSurfaceReader
{
    private static readonly HashSet<string> IgnoredAttributeNames = new(StringComparer.Ordinal)
    {
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Diagnostics.DebuggerStepThroughAttribute",
        "System.Diagnostics.DebuggerHiddenAttribute"
    };

    public ApiSurface ReadAssemblyFiles(
        IEnumerable<string> assemblyPaths,
        IReadOnlyCollection<string> namespacePrefixes)
    {
        if (assemblyPaths == null) throw new ArgumentNullException(nameof(assemblyPaths));
        ValidatePrefixes(namespacePrefixes);

        string[] paths = assemblyPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0) throw new ArgumentException("At least one assembly path is required.", nameof(assemblyPaths));

        var missing = paths.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
            throw new FileNotFoundException("Assembly not found: " + missing[0], missing[0]);

        string[] probeDirectories = paths
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .SelectMany(path => new[] { path, Directory.GetParent(path)?.FullName })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var context = new ProbeLoadContext(probeDirectories);
        var issues = new List<ApiLoadIssue>();
        var types = new SortedDictionary<string, ApiType>(StringComparer.Ordinal);
        try
        {
            foreach (string path in paths)
            {
                Assembly assembly;
                try
                {
                    assembly = context.LoadFromAssemblyPath(path);
                }
                catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
                {
                    issues.Add(new ApiLoadIssue(Path.GetFileName(path), exception.Message));
                    continue;
                }

                foreach (Type type in GetLoadableTypes(assembly, issues))
                {
                    if (!IsApiVisible(type) || !MatchesNamespace(type.Namespace, namespacePrefixes)) continue;
                    try
                    {
                        var model = ReadType(type);
                        if (!types.TryAdd(model.Name, model))
                        {
                            issues.Add(new ApiLoadIssue(
                                assembly.GetName().Name ?? Path.GetFileName(path),
                                "Duplicate public type: " + model.Name));
                        }
                    }
                    catch (Exception exception) when (exception is TypeLoadException or FileNotFoundException or NotSupportedException)
                    {
                        issues.Add(new ApiLoadIssue(
                            assembly.GetName().Name ?? Path.GetFileName(path),
                            $"{type.FullName}: {exception.Message}"));
                    }
                }
            }
        }
        finally
        {
            context.Unload();
        }

        return new ApiSurface(types, issues);
    }

    public ApiSurface ReadAssembly(Assembly assembly, IReadOnlyCollection<string> namespacePrefixes)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        ValidatePrefixes(namespacePrefixes);
        var issues = new List<ApiLoadIssue>();
        var types = new SortedDictionary<string, ApiType>(StringComparer.Ordinal);
        foreach (Type type in GetLoadableTypes(assembly, issues))
        {
            if (!IsApiVisible(type) || !MatchesNamespace(type.Namespace, namespacePrefixes)) continue;
            var model = ReadType(type);
            types[model.Name] = model;
        }
        return new ApiSurface(types, issues);
    }

    public ApiType ReadType(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        string name = type.FullName ?? type.Name;
        string kind = GetTypeKind(type);
        string modifiers = GetTypeModifiers(type);
        string baseType = type.BaseType == null ? "" : CanonicalTypeName(type.BaseType);
        string interfaces = string.Join(",", type.GetInterfaces().Select(CanonicalTypeName).OrderBy(x => x, StringComparer.Ordinal));
        string generic = FormatGenericParameters(type.IsGenericTypeDefinition ? type.GetGenericArguments() : Array.Empty<Type>());
        string attributes = FormatAttributes(CustomAttributeData.GetCustomAttributes(type));
        string fingerprint = $"kind={kind};mod={modifiers};base={baseType};ifaces={interfaces};generic={generic};attrs={attributes}";

        var members = ReadMembers(type)
            .OrderBy(member => member.Identity, StringComparer.Ordinal)
            .ThenBy(member => member.Fingerprint, StringComparer.Ordinal)
            .ToArray();
        return new ApiType(name, type.Assembly.GetName().Name ?? string.Empty, kind, fingerprint, members);
    }

    private static IEnumerable<ApiMember> ReadMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;

        foreach (ConstructorInfo constructor in type.GetConstructors(flags).Where(IsApiVisible))
        {
            string parameters = FormatParameterIdentity(constructor.GetParameters());
            string fingerprint = $"vis={Visibility(constructor)};static={constructor.IsStatic};params={FormatParameters(constructor.GetParameters())};attrs={FormatAttributes(CustomAttributeData.GetCustomAttributes(constructor))}";
            yield return new ApiMember($"constructor|{parameters}", "constructor", fingerprint, $"{type.Name}({FormatParameters(constructor.GetParameters())})");
        }

        foreach (MethodInfo method in type.GetMethods(flags).Where(IsApiVisible))
        {
            if (method.IsSpecialName && IsAccessor(method.Name)) continue;
            string parameters = FormatParameterIdentity(method.GetParameters());
            int arity = method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0;
            string conversionTarget = method.Name is "op_Implicit" or "op_Explicit"
                ? "|" + CanonicalTypeName(method.ReturnType)
                : "";
            string identity = $"method|{method.Name}|{arity}|{parameters}{conversionTarget}";
            string fingerprint = $"vis={Visibility(method)};static={method.IsStatic};abstract={method.IsAbstract};virtual={method.IsVirtual};final={method.IsFinal};return={CanonicalTypeName(method.ReturnType)};params={FormatParameters(method.GetParameters())};generic={FormatGenericParameters(method.IsGenericMethodDefinition ? method.GetGenericArguments() : Array.Empty<Type>())};attrs={FormatAttributes(CustomAttributeData.GetCustomAttributes(method))}";
            yield return new ApiMember(identity, "method", fingerprint, $"{CanonicalTypeName(method.ReturnType)} {method.Name}({FormatParameters(method.GetParameters())})");
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            MethodInfo? getter = property.GetMethod;
            MethodInfo? setter = property.SetMethod;
            if (!(getter != null && IsApiVisible(getter)) && !(setter != null && IsApiVisible(setter))) continue;
            ParameterInfo[] index = property.GetIndexParameters();
            string identity = $"property|{property.Name}|{FormatParameterIdentity(index)}";
            string fingerprint = $"type={CanonicalTypeName(property.PropertyType)};index={FormatParameters(index)};get={VisibilityOrNone(getter)};set={VisibilityOrNone(setter)};static={getter?.IsStatic ?? setter?.IsStatic ?? false};attrs={FormatAttributes(CustomAttributeData.GetCustomAttributes(property))}";
            yield return new ApiMember(identity, "property", fingerprint, $"{CanonicalTypeName(property.PropertyType)} {property.Name}[{FormatParameters(index)}]");
        }

        foreach (FieldInfo field in type.GetFields(flags).Where(IsApiVisible))
        {
            string value = field.IsLiteral ? FormatConstant(field.GetRawConstantValue()) : "";
            string fingerprint = $"vis={Visibility(field)};type={CanonicalTypeName(field.FieldType)};static={field.IsStatic};literal={field.IsLiteral};readonly={field.IsInitOnly};value={value};attrs={FormatAttributes(CustomAttributeData.GetCustomAttributes(field))}";
            yield return new ApiMember($"field|{field.Name}", "field", fingerprint, $"{CanonicalTypeName(field.FieldType)} {field.Name}");
        }

        foreach (EventInfo eventInfo in type.GetEvents(flags))
        {
            MethodInfo? add = eventInfo.AddMethod;
            MethodInfo? remove = eventInfo.RemoveMethod;
            if (!(add != null && IsApiVisible(add)) && !(remove != null && IsApiVisible(remove))) continue;
            string fingerprint = $"type={CanonicalTypeName(eventInfo.EventHandlerType ?? typeof(void))};add={VisibilityOrNone(add)};remove={VisibilityOrNone(remove)};static={add?.IsStatic ?? remove?.IsStatic ?? false};attrs={FormatAttributes(CustomAttributeData.GetCustomAttributes(eventInfo))}";
            yield return new ApiMember($"event|{eventInfo.Name}", "event", fingerprint, $"event {CanonicalTypeName(eventInfo.EventHandlerType ?? typeof(void))} {eventInfo.Name}");
        }
    }

    private static Type[] GetLoadableTypes(Assembly assembly, ICollection<ApiLoadIssue> issues)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            foreach (Exception? loaderException in exception.LoaderExceptions)
            {
                if (loaderException != null)
                    issues.Add(new ApiLoadIssue(assembly.GetName().Name ?? string.Empty, loaderException.Message));
            }
            return exception.Types.Where(type => type != null).Cast<Type>().ToArray();
        }
    }

    private static bool MatchesNamespace(string? value, IReadOnlyCollection<string> prefixes)
        => value != null && prefixes.Any(prefix => value.Equals(prefix, StringComparison.Ordinal)
                                                   || value.StartsWith(prefix + ".", StringComparison.Ordinal));

    private static void ValidatePrefixes(IReadOnlyCollection<string> prefixes)
    {
        if (prefixes == null || prefixes.Count == 0 || prefixes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty namespace prefix is required.", nameof(prefixes));
    }

    private static bool IsApiVisible(Type type)
        => type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;

    private static bool IsApiVisible(MethodBase method)
        => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool IsApiVisible(FieldInfo field)
        => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static string Visibility(MethodBase method)
        => method.IsPublic ? "public" : method.IsFamily ? "protected" : method.IsFamilyOrAssembly ? "protectedinternal" : "other";

    private static string Visibility(FieldInfo field)
        => field.IsPublic ? "public" : field.IsFamily ? "protected" : field.IsFamilyOrAssembly ? "protectedinternal" : "other";

    private static string VisibilityOrNone(MethodInfo? method)
        => method == null || !IsApiVisible(method) ? "none" : Visibility(method);

    private static bool IsAccessor(string name)
        => name.StartsWith("get_", StringComparison.Ordinal)
           || name.StartsWith("set_", StringComparison.Ordinal)
           || name.StartsWith("add_", StringComparison.Ordinal)
           || name.StartsWith("remove_", StringComparison.Ordinal);

    private static string GetTypeKind(Type type)
    {
        if (type.IsEnum) return "enum";
        if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType)) return "delegate";
        if (type.IsInterface) return "interface";
        if (type.IsValueType) return "struct";
        return "class";
    }

    private static string GetTypeModifiers(Type type)
    {
        var values = new List<string>();
        if (type.IsAbstract && type.IsSealed) values.Add("static");
        else
        {
            if (type.IsAbstract) values.Add("abstract");
            if (type.IsSealed) values.Add("sealed");
        }
        return string.Join(",", values);
    }

    private static string FormatParameterIdentity(IEnumerable<ParameterInfo> parameters)
        => string.Join(",", parameters.Select(parameter => $"{ParameterModifier(parameter)}{CanonicalTypeName(UnwrapByRef(parameter.ParameterType))}"));

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
        => string.Join(",", parameters.Select(parameter =>
        {
            string optional = parameter.IsOptional ? " optional=" + FormatConstant(parameter.DefaultValue) : "";
            string attributes = FormatAttributes(CustomAttributeData.GetCustomAttributes(parameter));
            string attributeSuffix = string.IsNullOrEmpty(attributes) ? "" : " attrs=" + attributes;
            return $"{ParameterModifier(parameter)}{CanonicalTypeName(UnwrapByRef(parameter.ParameterType))} {parameter.Name}{optional}{attributeSuffix}";
        }));

    private static string ParameterModifier(ParameterInfo parameter)
    {
        if (parameter.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "System.ParamArrayAttribute")) return "params ";
        if (!parameter.ParameterType.IsByRef) return "";
        if (parameter.IsOut) return "out ";
        if (parameter.IsIn || parameter.GetRequiredCustomModifiers().Any(type => type.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute")) return "in ";
        return "ref ";
    }

    private static Type UnwrapByRef(Type type) => type.IsByRef ? type.GetElementType()! : type;

    internal static string CanonicalTypeName(Type type)
    {
        if (type.IsByRef) return CanonicalTypeName(type.GetElementType()!) + "&";
        if (type.IsPointer) return CanonicalTypeName(type.GetElementType()!) + "*";
        if (type.IsArray) return CanonicalTypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter)
            return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition.ToString(CultureInfo.InvariantCulture);
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            return (definition.FullName ?? definition.Name) + "<" + string.Join(",", type.GetGenericArguments().Select(CanonicalTypeName)) + ">";
        }
        return type.FullName ?? type.Name;
    }

    private static string FormatGenericParameters(IEnumerable<Type> parameters)
        => string.Join(";", parameters.Select(parameter =>
        {
            string constraints = string.Join(",", parameter.GetGenericParameterConstraints().Select(CanonicalTypeName).OrderBy(x => x, StringComparer.Ordinal));
            return $"{parameter.GenericParameterPosition}:{parameter.GenericParameterAttributes}:{constraints}";
        }));

    private static string FormatAttributes(IEnumerable<CustomAttributeData> attributes)
        => string.Join(",", attributes
            .Where(attribute => !IgnoredAttributeNames.Contains(attribute.AttributeType.FullName ?? attribute.AttributeType.Name))
            .Select(FormatAttribute)
            .OrderBy(value => value, StringComparer.Ordinal));

    private static string FormatAttribute(CustomAttributeData attribute)
    {
        string positional = string.Join(",", attribute.ConstructorArguments.Select(FormatAttributeValue));
        string named = string.Join(",", attribute.NamedArguments
            .OrderBy(argument => argument.MemberName, StringComparer.Ordinal)
            .Select(argument => argument.MemberName + "=" + FormatAttributeValue(argument.TypedValue)));
        string separator = positional.Length > 0 && named.Length > 0 ? "," : "";
        return $"{attribute.AttributeType.FullName}({positional}{separator}{named})";
    }

    private static string FormatAttributeValue(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> values)
            return "[" + string.Join(",", values.Select(FormatAttributeValue)) + "]";
        return FormatConstant(argument.Value);
    }

    private static string FormatConstant(object? value)
    {
        if (value == null) return "null";
        if (value is string text) return "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        if (value is char character) return "'" + character + "'";
        if (value is bool boolean) return boolean ? "true" : "false";
        if (value is Type type) return "typeof(" + CanonicalTypeName(type) + ")";
        if (value is Enum enumeration) return Convert.ToInt64(enumeration, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        if (value == DBNull.Value || value == Missing.Value) return "missing";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class ProbeLoadContext : AssemblyLoadContext
    {
        private readonly string[] _probeDirectories;

        public ProbeLoadContext(string[] probeDirectories)
            : base("AnityUnityApiParity-" + Guid.NewGuid().ToString("N"), isCollectible: true)
        {
            _probeDirectories = probeDirectories;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            foreach (string directory in _probeDirectories)
            {
                string candidate = Path.Combine(directory, assemblyName.Name + ".dll");
                if (File.Exists(candidate)) return LoadFromAssemblyPath(candidate);
            }
            return null;
        }
    }
}
