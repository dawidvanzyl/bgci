using System.Reflection;

namespace Shared.Persistence.Migrations;

public sealed record MigrationScripts(
	IReadOnlyList<MigrationScript> Schema,
	IReadOnlyList<MigrationScript> Functions,
	IReadOnlyList<MigrationScript> Seeds)
{
	public static MigrationScripts LoadEmbedded(Assembly assembly, string resourcePrefix) =>
		new(
			Load(assembly, resourcePrefix, "Schema"),
			Load(assembly, resourcePrefix, "Functions"),
			Load(assembly, resourcePrefix, "Seeds"));

	private static List<MigrationScript> Load(Assembly assembly, string resourcePrefix, string category)
	{
		var prefix = $"{resourcePrefix}{category}.";
		return assembly.GetManifestResourceNames()
			.Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
			.Select(name => new MigrationScript(name[prefix.Length..], ReadResource(assembly, name)))
			.ToList();
	}

	private static string ReadResource(Assembly assembly, string resourceName)
	{
		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
		return reader.ReadToEnd();
	}
}