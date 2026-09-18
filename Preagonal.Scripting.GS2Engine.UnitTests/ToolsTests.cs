namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ToolsTests
{
	[Fact]
	public void Given_hex_precision_format_When_formatting_rgb_values_Then_values_are_zero_padded()
	{
		var formatted = Tools.Format("#%.2x%.2x%.2x", 0, 224, 255);

		Assert.Equal("#00e0ff", formatted);
	}

	[Fact]
	public void Given_debug_is_disabled_When_writing_interpolated_debug_line_Then_values_are_not_formatted()
	{
		var previousDebug = Tools.DEBUG_ON;
		var value         = new FormattingProbe();
		try
		{
			Tools.DEBUG_ON = false;

			Tools.DebugLine($"value: {value}");

			Assert.False(value.WasFormatted);
		}
		finally
		{
			Tools.DEBUG_ON = previousDebug;
		}
	}

	private sealed class FormattingProbe
	{
		public bool WasFormatted { get; private set; }

		public override string ToString()
		{
			WasFormatted = true;
			return string.Empty;
		}
	}
}