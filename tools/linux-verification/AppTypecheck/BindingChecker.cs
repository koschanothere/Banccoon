using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Checks every {Binding Path} in the App's XAML against the real compiled view-model types, scoped
// by x:DataType the way MAUI's compiled bindings (XamlC) resolve them. Also checks Command="{Binding X}"
// targets exist and that {loc:Translate Key} keys exist in the neutral resx.
public static class BindingChecker
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2009/xaml";
    private static int errors;
    private static int checkedCount;
    private static int unscoped;
    private static HashSet<string> resxKeys = new();

    public static int Main(string[] args)
    {
        var appSrc = Path.Combine(FindRepoRoot(), "src", "Banccoon.App");
        resxKeys = XDocument.Load($"{appSrc}/Resources/Strings/AppStrings.resx").Root!
            .Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();
        var files = args.Length > 0 ? args : Directory.GetFiles(appSrc, "*.xaml", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            CheckFile(file);
        }
        Console.WriteLine($"binding check: {checkedCount} bindings/keys checked ({unscoped} unscoped reflection bindings skipped), {errors} errors");
        return errors == 0 ? 0 : 1;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Banccoon.sln"))) return dir.FullName;
        }

        throw new InvalidOperationException("Run from inside the Banccoon repository.");
    }

    private static void CheckFile(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        var prefixes = doc.Root!.Attributes().Where(a => a.IsNamespaceDeclaration)
            .ToDictionary(a => a.Name.LocalName, a => a.Value);
        Walk(doc.Root!, null, path, prefixes);
    }

    private static void Walk(XElement element, Type? scope, string file, Dictionary<string, string> prefixes)
    {
        var dataType = element.Attribute(X + "DataType");
        if (dataType is not null)
        {
            scope = ResolveType(dataType.Value, prefixes);
            if (scope is null)
            {
                Report(file, element, $"x:DataType '{dataType.Value}' not found");
            }
        }

        foreach (var attribute in element.Attributes())
        {
            CheckValue(attribute.Value, scope, file, element, attribute.Name.LocalName, prefixes);
        }

        foreach (var child in element.Elements())
        {
            Walk(child, scope, file, prefixes);
        }
    }

    private static void CheckValue(string value, Type? scope, string file, XElement element, string attributeName, Dictionary<string, string> prefixes)
    {
        foreach (Match translate in Regex.Matches(value, @"\{loc:Translate\s+(?:Key=)?([A-Za-z0-9_]+)\s*\}"))
        {
            checkedCount++;
            if (!resxKeys.Contains(translate.Groups[1].Value))
            {
                Report(file, element, $"resx key '{translate.Groups[1].Value}' missing");
            }
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("{Binding", StringComparison.Ordinal))
        {
            return;
        }

        var inner = trimmed["{Binding".Length..^1].Trim();
        var parts = SplitTopLevel(inner);
        string? bindingPath = null;
        Type? bindingScope = scope;
        foreach (var part in parts)
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            if (p.StartsWith("Path=", StringComparison.Ordinal)) bindingPath = p[5..].Trim();
            else if (p.StartsWith("Source=", StringComparison.Ordinal))
            {
                var ancestor = Regex.Match(p, @"AncestorType=\{x:Type\s+([\w:]+)\}");
                if (ancestor.Success) bindingScope = ResolveType(ancestor.Groups[1].Value, prefixes);
                else return; // x:Reference / static sources: out of scope
            }
            else if (!p.Contains('=')) bindingPath ??= p;
        }

        bindingPath ??= ".";
        checkedCount++;
        if (bindingScope is null)
        {
            // Not a compiled binding (no x:DataType in scope, e.g. RecurrenceEditorView) - MAUI
            // resolves it by reflection at runtime, so there's nothing to check statically.
            unscoped++;
            return;
        }

        if (bindingPath == ".")
        {
            return;
        }

        var current = bindingScope;
        foreach (var segment in bindingPath.Split('.'))
        {
            var name = Regex.Replace(segment, @"\[.*\]$", string.Empty);
            var property = current.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property is null)
            {
                Report(file, element, $"'{bindingPath}' - '{name}' not found on {current.Name}");
                return;
            }

            current = property.PropertyType;
            if (segment.EndsWith(']'))
            {
                current = current.IsArray ? current.GetElementType()! : current.GetGenericArguments().LastOrDefault() ?? typeof(object);
            }
        }
    }

    private static List<string> SplitTopLevel(string text)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                result.Add(text[start..i]);
                start = i + 1;
            }
        }
        result.Add(text[start..]);
        return result;
    }

    private static Type? ResolveType(string qualified, Dictionary<string, string> prefixes)
    {
        var pieces = qualified.Split(':');
        if (pieces.Length == 1)
        {
            // Default XAML namespace (e.g. x:DataType="Color") - MAUI's own types.
            return typeof(Microsoft.Maui.Graphics.Color).Assembly.GetType($"Microsoft.Maui.Graphics.{qualified}");
        }

        if (pieces.Length != 2 || !prefixes.TryGetValue(pieces[0], out var ns)) return null;
        var clrNamespace = Regex.Match(ns, @"clr-namespace:([\w.]+)").Groups[1].Value;
        return typeof(BindingChecker).Assembly.GetType($"{clrNamespace}.{pieces[1]}")
            ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType($"{clrNamespace}.{pieces[1]}")).FirstOrDefault(t => t is not null);
    }

    private static void Report(string file, XElement element, string message)
    {
        errors++;
        var line = ((System.Xml.IXmlLineInfo)element).LineNumber;
        Console.WriteLine($"ERROR {Path.GetFileName(file)}:{line}: {message}");
    }
}
