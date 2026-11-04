using System.Reflection;

namespace GameCollection.Infrastructure.Postgres.Migrations;

public sealed record MigrationScripts(
	IReadOnlyList<MigrationScript> Schema,
	IReadOnlyList<MigrationScript> Functions,
	IReadOnlyList<MigrationScript> Seeds)
{
	private const string _resourcePrefix = "GameCollection.Infrastructure.Postgres.Scripts.";

	public static MigrationScripts LoadEmbedded()
	{
		var assembly = typeof(MigrationScripts).Assembly;
		return new MigrationScripts(
			Load(assembly, "Schema"),
			Load(assembly, "Functions"),
			Load(assembly, "Seeds"));
	}

	private static List<MigrationScript> Load(Assembly assembly, string category)
	{
		var prefix = $"{_resourcePrefix}{category}.";
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