using System;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class VersionProperties : ScriptProperties<Version>
{
	public static readonly VersionProperties Instance = [];

	private VersionProperties() : base(null)
	{
		AddProperties(
			this,
			new()
			{
				{ "major", "The major version number.", version => version.Major },
				{ "minor", "The minor version number.", version => version.Minor },
				{ "build", "The build version number.", version => version.Build },
				{ "revision", "The revision version number.", version => version.Revision },
			}
		);

		Compile();
	}
}