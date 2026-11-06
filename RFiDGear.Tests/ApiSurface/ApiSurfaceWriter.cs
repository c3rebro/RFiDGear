#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RFiDGear.Tests.ApiSurface
{
    /// <summary>
    /// Describes the public surface of an assembly as plain, sorted text, one declaration per line, so a change to
    /// it shows up as a readable diff. It is the engine behind the approved-snapshot test of
    /// <c>RFiDGear.Contracts</c>.
    /// <para>
    /// What counts as surface: every exported type, and on it every public or protected member that is not an
    /// accessor, together with the details a consumer's code can depend on: modifiers, base types, generic
    /// constraints, parameter and return types with their nullable annotations, default values, <c>params</c>/
    /// <c>ref</c>/<c>out</c>, <c>init</c> accessors, constant values, enum values, and the attributes that change how
    /// a member may be used (<c>Obsolete</c>, <c>Flags</c>, and any attribute defined by RFiDGear itself).
    /// Compiler-generated members of records (equality, <c>ToString</c>, clone) are left out because they follow from
    /// the declaration.
    /// </para>
    /// </summary>
    internal sealed class ApiSurfaceWriter
    {
        private const string OwnNamespace = "RFiDGear.Contracts";

        private static readonly Dictionary<Type, string> Keywords = new()
        {
            [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint",
            [typeof(long)] = "long", [typeof(ulong)] = "ulong", [typeof(char)] = "char", [typeof(decimal)] = "decimal",
            [typeof(double)] = "double", [typeof(float)] = "float", [typeof(object)] = "object", [typeof(string)] = "string",
        };

        private const BindingFlags AllDeclared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private readonly NullabilityInfoContext _nullability = new();

        private ApiSurfaceWriter()
        {
        }

        /// <summary>Describes every exported type of <paramref name="assembly"/>.</summary>
        /// <param name="assembly">The assembly to describe.</param>
        /// <returns>The lines of the description, types in ordinal name order, members sorted within each type.</returns>
        public static IReadOnlyList<string> Describe(Assembly assembly) =>
            new ApiSurfaceWriter().DescribeTypes(assembly.GetExportedTypes());

        /// <summary>Describes the given types only; used to test the writer itself.</summary>
        /// <param name="types">The types to describe; nested types are described when listed.</param>
        /// <returns>The lines of the description.</returns>
        public static IReadOnlyList<string> Describe(params Type[] types) =>
            new ApiSurfaceWriter().DescribeTypes(types);

        private IReadOnlyList<string> DescribeTypes(IEnumerable<Type> types)
        {
            var lines = new List<string>();
            foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (lines.Count > 0)
                {
                    lines.Add(string.Empty);
                }

                DescribeType(type, lines);
            }

            return lines;
        }

        // ── types ─────────────────────────────────────────────────────────────

        private void DescribeType(Type type, List<string> lines)
        {
            lines.Add(TypeHeader(type));

            if (type.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(type);
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => Convert.ToDecimal(f.GetRawConstantValue(), CultureInfo.InvariantCulture)))
                {
                    lines.Add($"    {field.Name} = {Convert.ChangeType(field.GetRawConstantValue(), underlying, CultureInfo.InvariantCulture)}");
                }

                return;
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return; // the header already carries the signature
            }

            var members = new List<(int Rank, string Name, string Text)>();
            var isRecord = IsRecord(type);

            foreach (var constructor in type.GetConstructors(AllDeclared).Where(IsVisible))
            {
                // The protected copy constructor of a record follows from the declaration.
                var parameters = constructor.GetParameters();
                if (isRecord && parameters.Length == 1 && parameters[0].ParameterType == type)
                {
                    continue;
                }

                members.Add((0, "ctor", Member(constructor, $"{Visibility(constructor.IsPublic, constructor.IsFamilyOrAssembly)} ctor{Parameters(constructor.GetParameters())}")));
            }

            foreach (var field in type.GetFields(AllDeclared).Where(f => IsVisible(f) && !f.IsSpecialName))
            {
                members.Add((1, field.Name, DescribeField(field)));
            }

            foreach (var property in type.GetProperties(AllDeclared))
            {
                if (isRecord && property.Name == "EqualityContract")
                {
                    continue;
                }

                var text = DescribeProperty(type, property);
                if (text != null)
                {
                    members.Add((2, property.Name, text));
                }
            }

            foreach (var evt in type.GetEvents(AllDeclared))
            {
                var text = DescribeEvent(type, evt);
                if (text != null)
                {
                    members.Add((3, evt.Name, text));
                }
            }

            foreach (var method in type.GetMethods(AllDeclared).Where(IsVisible).Where(m => IsApiMethod(m)))
            {
                members.Add((4, method.Name, DescribeMethod(type, method)));
            }

            foreach (var member in members.OrderBy(m => m.Rank).ThenBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.Text, StringComparer.Ordinal))
            {
                lines.Add("    " + member.Text);
            }
        }

        private string TypeHeader(Type type)
        {
            var attributes = AttributePrefix(type.GetCustomAttributesData());
            var visibility = type.IsNested && !type.IsNestedPublic ? "protected" : "public";
            var name = DeclarationName(type);

            if (type.IsEnum)
            {
                return $"{attributes}{visibility} enum {name} : {Keywords[Enum.GetUnderlyingType(type)]}";
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                var invoke = type.GetMethod("Invoke")!;
                return $"{attributes}{visibility} delegate {Format(invoke.ReturnType, _nullability.Create(invoke.ReturnParameter))} {name}{Parameters(invoke.GetParameters())}";
            }

            string kind;
            string modifiers = string.Empty;

            if (type.IsInterface)
            {
                kind = "interface";
            }
            else if (type.IsValueType)
            {
                kind = HasAttribute(type, "IsReadOnlyAttribute") ? "readonly struct" : "struct";
            }
            else
            {
                kind = IsRecord(type) ? "record" : "class";

                if (type.IsAbstract && type.IsSealed)
                {
                    modifiers = "static ";
                }
                else if (type.IsAbstract)
                {
                    modifiers = "abstract ";
                }
                else if (type.IsSealed)
                {
                    modifiers = "sealed ";
                }
            }

            var bases = new List<string>();
            if (!type.IsInterface && !type.IsValueType && type.BaseType != null && type.BaseType != typeof(object))
            {
                bases.Add(Format(type.BaseType, null));
            }

            var inherited = type.BaseType?.GetInterfaces() ?? Array.Empty<Type>();
            bases.AddRange(type.GetInterfaces().Except(inherited).Select(i => Format(i, null)).OrderBy(s => s, StringComparer.Ordinal));

            var header = $"{attributes}{visibility} {modifiers}{kind} {name}";
            if (bases.Count > 0)
            {
                header += " : " + string.Join(", ", bases);
            }

            return header + Constraints(type.GetGenericArguments().Where(a => a.DeclaringType == type));
        }

        private static string DeclarationName(Type type)
        {
            var name = type.IsNested ? DeclarationName(type.DeclaringType!) + "." + StripArity(type.Name) : (type.Namespace + "." + StripArity(type.Name));
            var own = type.GetGenericArguments().Where(a => !type.IsNested || a.DeclaringType == type).ToArray();
            return own.Length == 0 ? name : name + "<" + string.Join(", ", own.Select(a => a.Name)) + ">";
        }

        private static bool IsRecord(Type type) =>
            type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null;

        // ── members ───────────────────────────────────────────────────────────

        private string DescribeField(FieldInfo field)
        {
            var info = _nullability.Create(field);
            var type = Format(field.FieldType, info);
            var visibility = Visibility(field.IsPublic, field.IsFamilyOrAssembly);

            if (field.IsLiteral)
            {
                return Member(field, $"{visibility} const {type} {field.Name} = {Constant(field.GetRawConstantValue())}");
            }

            var modifiers = (field.IsStatic ? "static " : string.Empty) + (field.IsInitOnly ? "readonly " : string.Empty);
            return Member(field, $"{visibility} {modifiers}{type} {field.Name}");
        }

        private string? DescribeProperty(Type owner, PropertyInfo property)
        {
            var getter = property.GetMethod != null && IsVisible(property.GetMethod) ? property.GetMethod : null;
            var setter = property.SetMethod != null && IsVisible(property.SetMethod) ? property.SetMethod : null;
            if (getter == null && setter == null)
            {
                return null;
            }

            var primary = getter ?? setter!;
            var propertyVisibility = (getter != null && getter.IsPublic) || (setter != null && setter.IsPublic)
                ? "public"
                : Visibility(false, primary.IsFamilyOrAssembly);

            string Accessor(MethodInfo? method, string keyword)
            {
                if (method == null)
                {
                    return string.Empty;
                }

                var isInit = keyword == "set" && method.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
                var word = isInit ? "init" : keyword;
                var own = Visibility(method.IsPublic, method.IsFamilyOrAssembly);
                return (own == propertyVisibility ? string.Empty : own + " ") + word + "; ";
            }

            var indexParameters = property.GetIndexParameters();
            var name = indexParameters.Length > 0 ? "this" + Parameters(indexParameters, "[", "]") : property.Name;
            var type = Format(property.PropertyType, _nullability.Create(property));
            var text = $"{propertyVisibility} {MethodModifiers(owner, primary)}{type} {name} {{ {Accessor(getter, "get")}{Accessor(setter, "set")}}}";
            return Member(property, text);
        }

        private string? DescribeEvent(Type owner, EventInfo evt)
        {
            var add = evt.AddMethod;
            if (add == null || !IsVisible(add))
            {
                return null;
            }

            var text = $"{Visibility(add.IsPublic, add.IsFamilyOrAssembly)} {MethodModifiers(owner, add)}event {Format(evt.EventHandlerType!, _nullability.Create(evt))} {evt.Name}";
            return Member(evt, text);
        }

        private string DescribeMethod(Type owner, MethodInfo method)
        {
            var generics = method.IsGenericMethodDefinition
                ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">"
                : string.Empty;
            var returnType = Format(method.ReturnType, _nullability.Create(method.ReturnParameter));
            var text = $"{Visibility(method.IsPublic, method.IsFamilyOrAssembly)} {MethodModifiers(owner, method)}{returnType} {method.Name}{generics}{Parameters(method.GetParameters())}" +
                       (method.IsGenericMethodDefinition ? Constraints(method.GetGenericArguments()) : string.Empty);
            return Member(method, text);
        }

        private static bool IsApiMethod(MethodInfo method)
        {
            if (method.Name.Contains('<', StringComparison.Ordinal) || method.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                return false;
            }

            // Accessors are described with their property or event.
            return !(method.IsSpecialName && (method.Name.StartsWith("get_", StringComparison.Ordinal)
                || method.Name.StartsWith("set_", StringComparison.Ordinal)
                || method.Name.StartsWith("add_", StringComparison.Ordinal)
                || method.Name.StartsWith("remove_", StringComparison.Ordinal)));
        }

        private static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

        private static bool IsVisible(FieldInfo field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

        private static string Visibility(bool isPublic, bool isFamilyOrAssembly) =>
            isPublic ? "public" : isFamilyOrAssembly ? "protected internal" : "protected";

        private static string MethodModifiers(Type owner, MethodInfo method)
        {
            var parts = new List<string>();
            if (method.IsStatic)
            {
                parts.Add("static");
            }
            else if (!owner.IsInterface)
            {
                if (method.IsAbstract)
                {
                    parts.Add("abstract");
                }
                else if (method.IsVirtual && !method.IsFinal)
                {
                    parts.Add(method.GetBaseDefinition().DeclaringType != method.DeclaringType ? "override" : "virtual");
                }
            }

            return parts.Count == 0 ? string.Empty : string.Join(" ", parts) + " ";
        }

        private static string Member(MemberInfo member, string text) =>
            AttributePrefix(member.GetCustomAttributesData()) + text;

        // ── parameters, types, constraints ────────────────────────────────────

        private string Parameters(ParameterInfo[] parameters, string open = "(", string close = ")")
        {
            var rendered = parameters.Select(p =>
            {
                var type = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                var prefix = p.ParameterType.IsByRef
                    ? (p.IsOut ? "out " : p.IsIn ? "in " : "ref ")
                    : p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0 ? "params " : string.Empty;
                var text = $"{prefix}{Format(type, _nullability.Create(p))} {p.Name}";
                if (p.HasDefaultValue)
                {
                    text += " = " + (p.RawDefaultValue == null || p.RawDefaultValue == DBNull.Value
                        ? (type.IsValueType && Nullable.GetUnderlyingType(type) == null ? "default" : "null")
                        : type.IsEnum ? Constant(Enum.ToObject(type, p.RawDefaultValue)) : Constant(p.RawDefaultValue));
                }

                return text;
            });
            return open + string.Join(", ", rendered) + close;
        }

        private static string Constraints(IEnumerable<Type> genericArguments)
        {
            var parts = new List<string>();
            foreach (var argument in genericArguments.Where(a => a.IsGenericParameter))
            {
                var items = new List<string>();
                var attributes = argument.GenericParameterAttributes;
                if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                {
                    items.Add("class");
                }

                if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                {
                    items.Add("struct");
                }

                items.AddRange(argument.GetGenericParameterConstraints()
                    .Where(c => c != typeof(ValueType))
                    .Select(c => Format(c, null)));

                if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
                    && !attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                {
                    items.Add("new()");
                }

                if (items.Count > 0)
                {
                    parts.Add($" where {argument.Name} : {string.Join(", ", items)}");
                }
            }

            return string.Concat(parts);
        }

        private static string Format(Type type, NullabilityInfo? info)
        {
            if (type.IsByRef)
            {
                return Format(type.GetElementType()!, info?.ElementType);
            }

            if (type.IsArray)
            {
                var commas = new string(',', type.GetArrayRank() - 1);
                var array = Format(type.GetElementType()!, info?.ElementType) + "[" + commas + "]";
                return info?.ReadState == NullabilityState.Nullable ? array + "?" : array;
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return Format(underlying, null) + "?";
            }

            string name;
            if (type.IsGenericParameter)
            {
                name = type.Name;
            }
            else if (Keywords.TryGetValue(type, out var keyword))
            {
                name = keyword;
            }
            else if (type.IsGenericType)
            {
                var arguments = type.GetGenericArguments();
                var rendered = arguments.Select((a, i) =>
                    Format(a, info != null && i < info.GenericTypeArguments.Length ? info.GenericTypeArguments[i] : null));
                name = QualifiedName(type) + "<" + string.Join(", ", rendered) + ">";
            }
            else
            {
                name = QualifiedName(type);
            }

            return !type.IsValueType && info?.ReadState == NullabilityState.Nullable ? name + "?" : name;
        }

        private static string QualifiedName(Type type)
        {
            var simple = StripArity(type.Name);
            if (type.IsNested)
            {
                return QualifiedName(type.DeclaringType!) + "." + simple;
            }

            return type.Namespace == OwnNamespace || string.IsNullOrEmpty(type.Namespace) ? simple : type.Namespace + "." + simple;
        }

        private static string StripArity(string name)
        {
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            return tick < 0 ? name : name[..tick];
        }

        // ── attributes and constants ──────────────────────────────────────────

        private static bool HasAttribute(Type type, string attributeName) =>
            type.GetCustomAttributesData().Any(a => a.AttributeType.Name == attributeName);

        private static string AttributePrefix(IEnumerable<CustomAttributeData> data)
        {
            var rendered = data
                .Where(IsSurfaceAttribute)
                .Select(FormatAttribute)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            return rendered.Count == 0 ? string.Empty : string.Join(" ", rendered) + " ";
        }

        private static bool IsSurfaceAttribute(CustomAttributeData data)
        {
            var type = data.AttributeType;
            return type == typeof(ObsoleteAttribute)
                || type == typeof(FlagsAttribute)
                || (type.Namespace ?? string.Empty).StartsWith("RFiDGear", StringComparison.Ordinal);
        }

        private static string FormatAttribute(CustomAttributeData data)
        {
            var name = data.AttributeType.Name;
            name = name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
            var arguments = data.ConstructorArguments.Select(a => ConstantOf(a.Value))
                .Concat(data.NamedArguments.Select(n => $"{n.MemberName} = {ConstantOf(n.TypedValue.Value)}"))
                .ToList();
            return arguments.Count == 0 ? $"[{name}]" : $"[{name}({string.Join(", ", arguments)})]";
        }

        private static string ConstantOf(object? value) => value is Type type ? $"typeof({Format(type, null)})" : Constant(value);

        private static string Constant(object? value) => value switch
        {
            null => "null",
            string s => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"",
            bool b => b ? "true" : "false",
            char c => "'" + c + "'",
            Enum e => e.GetType().Name + "." + e,
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
