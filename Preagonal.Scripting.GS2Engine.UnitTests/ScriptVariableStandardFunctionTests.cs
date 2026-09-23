using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public sealed class ScriptVariableStandardFunctionTests
{
	[Fact]
	public async Task Dynamic_names_are_sorted_and_exclude_registered_properties_and_functions()
	{
		var script = CompileScript("this.zebra = 1; this.alpha = 2; return this.getdynamicvarnames();");
		var result = await script.Call("onCreated");
		Assert.Equal(new[] { "alpha", "zebra" }, result.GetValue<string[]>());
	}

	[Fact]
	public async Task Static_names_include_inherited_properties_but_not_dynamic_fields_or_functions()
	{
		var script = CompileScript("this.value = 1; return this.getstaticvarnames();");
		var result = await script.Call("onCreated");
		var names  = result.GetValue<string[]>();
		Assert.NotNull(names);
		Assert.Contains("joinedclasses", names);
		Assert.DoesNotContain("value", names);
		Assert.DoesNotContain("getstaticvarnames", names);
		Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
	}

	[Fact]
	public async Task Combined_names_include_dynamic_fields_and_properties()
	{
		var script = CompileScript("this.value = 1; return this.getvarnames();");
		var result = await script.Call("onCreated");
		var names  = result.GetValue<string[]>();
		Assert.NotNull(names);
		Assert.Contains("joinedclasses", names);
		Assert.Contains("value", names);
		Assert.DoesNotContain("getvarnames", names);
	}

	[Fact]
	public async Task Call_Given_degrees_When_degtorad_is_called_Then_returns_radians()
	{
		var script = CompileScript("return degtorad(180);");

		var result = await script.Call("onCreated");

		Assert.Equal(Math.PI, result.GetValue<double>(), 10);
	}

	[Fact]
	public async Task Call_Given_joined_class_When_isinclass_is_called_Then_returns_true()
	{
		var script = CompileScript("this.join(\"example\"); return this.isinclass(\"EXAMPLE\");");

		var result = await script.Call("onCreated");

		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Call_Given_object_variables_When_clearvars_is_called_Then_removes_them()
	{
		var script = CompileScript("this.value = 1; this.clearvars(); return this.value;");

		var result = await script.Call("onCreated");

		Assert.Equal(string.Empty, result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_number_When_type_is_called_Then_returns_number_type()
	{
		//Arrange
		var script = CompileScript("temp.value = 12; return temp.value.type();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_string_When_type_is_called_Then_returns_string_type()
	{
		//Arrange
		var script = CompileScript("return \"text\".type();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_script_object_When_type_is_called_Then_returns_object_type()
	{
		//Arrange
		var script = CompileScript("return this.type();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(2.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_array_When_type_is_called_Then_returns_array_type()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; return temp.values.type();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_source_array_When_addarray_is_called_Then_appends_all_values()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; temp.values.addarray({2, 3}); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d, 3.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_non_array_source_When_addarray_is_called_Then_leaves_target_unchanged()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; temp.values.addarray(2); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_same_source_and_target_When_addarray_is_called_Then_appends_original_values_once()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; temp.values.addarray(temp.values); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d, 1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_source_array_When_insertarray_is_called_Then_inserts_all_values_in_source_order()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 4}; temp.values.insertarray(1, {2, 3}); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d, 3.0d, 4.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_index_past_end_When_insertarray_is_called_Then_appends_values_in_reverse_source_order()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; temp.values.insertarray(4, {2, 3}); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 3.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_same_source_and_target_When_insertarray_is_called_Then_reads_mutated_source_like_graal()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; temp.values.insertarray(0, temp.values); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([2.0d, 2.0d, 1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_duplicate_values_When_indices_is_called_Then_returns_matching_indices()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2, 1}; return temp.values.indices(1);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([0.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_missing_value_When_indices_is_called_Then_returns_empty_array()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; return temp.values.indices(3);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Empty(result.GetValue<List<object?>>()!);
	}

	[Fact]
	public async Task Call_Given_near_equal_numbers_When_indices_is_called_Then_uses_script_numeric_tolerance()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; return temp.values.indices(1.00005);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([0.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_differently_cased_strings_When_indices_is_called_Then_does_not_match_them()
	{
		//Arrange
		var script = CompileScript("temp.values = {\"One\"}; return temp.values.indices(\"one\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Empty(result.GetValue<List<object?>>()!);
	}

	[Fact]
	public async Task Call_Given_repeated_strings_When_indices_is_called_Then_returns_only_exact_matches()
	{
		var script = CompileScript("temp.values = {\"One\", \"one\", \"One\"}; return temp.values.indices(\"One\");");

		var result = await script.Call("onCreated");

		Assert.Equal([0.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_padded_string_When_trim_is_called_Then_removes_outer_whitespace()
	{
		//Arrange
		var script = CompileScript("return \"  one two  \".trim();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("one two", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_uppercase_string_When_lower_is_called_Then_returns_lowercase_string()
	{
		//Arrange
		var script = CompileScript("return \"ONE Two\".lower();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("one two", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_lowercase_string_When_upper_is_called_Then_returns_uppercase_string()
	{
		//Arrange
		var script = CompileScript("return \"one Two\".upper();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("ONE TWO", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_space_and_comma_separated_string_When_tokenize_has_no_delimiters_Then_uses_graal_defaults()
	{
		//Arrange
		var script = CompileScript("return \"one, two\".tokenize();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(["one", "two"], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_custom_delimiter_When_tokenize_is_called_Then_splits_on_each_delimiter_character()
	{
		//Arrange
		var script = CompileScript("return \"one|two;three\".tokenize(\"|;\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(["one", "two", "three"], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_valid_index_When_charat_is_called_Then_returns_character()
	{
		//Arrange
		var script = CompileScript("return \"abc\".charat(1);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("b", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_out_of_range_index_When_charat_is_called_Then_returns_empty_string()
	{
		//Arrange
		var script = CompileScript("return \"abc\".charat(3);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(string.Empty, result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_present_substring_When_pos_is_called_Then_returns_first_position()
	{
		//Arrange
		var script = CompileScript("return \"ababa\".pos(\"ba\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_missing_substring_When_pos_is_called_Then_returns_negative_one()
	{
		//Arrange
		var script = CompileScript("return \"abc\".pos(\"z\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(-1.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_overlapping_substrings_When_positions_is_called_Then_returns_every_position()
	{
		//Arrange
		var script = CompileScript("return \"ababa\".positions(\"aba\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([0.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_differently_cased_suffix_When_ends_is_called_Then_matches_case_insensitively()
	{
		//Arrange
		var script = CompileScript("return \"LoginServer\".ends(\"SERVER\");");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_start_and_length_When_substring_is_called_Then_returns_requested_text()
	{
		//Arrange
		var script = CompileScript("return \"abcdef\".substring(2, 3);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("cde", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_only_start_When_substring_is_called_Then_returns_remaining_text()
	{
		//Arrange
		var script = CompileScript("return \"abcdef\".substring(2);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("cdef", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Call_Given_array_When_clear_is_called_Then_removes_every_value()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; temp.values.clear(); return temp.values.size();");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Call_Given_invalid_index_When_delete_is_called_Then_leaves_array_unchanged()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; temp.values.delete(-1); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_missing_value_When_remove_is_called_Then_leaves_array_unchanged()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; temp.values.remove(3); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_index_past_end_When_replace_is_called_Then_appends_value()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; temp.values.replace(4, 2); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_index_past_end_When_insert_is_called_Then_appends_value()
	{
		//Arrange
		var script = CompileScript("temp.values = {1}; temp.values.insert(4, 2); return temp.values;");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([1.0d, 2.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_only_start_When_subarray_is_called_Then_returns_remaining_values()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2, 3}; return temp.values.subarray(1);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal([2.0d, 3.0d], result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_subarray_When_modifying_slice_Then_source_array_is_unchanged()
	{
		var script = CompileScript("temp.values = {1, 2, 3}; temp.slice = temp.values.subarray(1, 1); temp.slice[0] = 9; return temp.values;");

		var result = await script.Call("onCreated");

		Assert.Equal([1.0d, 2.0d, 3.0d], result.GetValue<List<object?>>());
	}

	[Theory]
	[InlineData("0, 0", new double[] { })]
	[InlineData("-1, 2", new double[] { 1, 2 })]
	[InlineData("1, -1", new double[] { 2, 3 })]
	[InlineData("1, 20", new double[] { 2, 3 })]
	[InlineData("3, 1", new double[] { })]
	[InlineData("20, 1", new double[] { })]
	public async Task Call_Given_slice_bounds_When_subarray_is_called_Then_clamps_to_array(string arguments, double[] expected)
	{
		var script = CompileScript($"temp.values = {{1, 2, 3}}; return temp.values.subarray({arguments});");

		var result = await script.Call("onCreated");

		Assert.Equal(expected.Cast<object?>(), result.GetValue<List<object?>>());
	}

	[Fact]
	public async Task Call_Given_missing_value_When_index_is_called_Then_returns_negative_one()
	{
		//Arrange
		var script = CompileScript("temp.values = {1, 2}; return temp.values.index(3);");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(-1.0d, result.GetValue());
	}

	private static Script CompileScript(string body)
	{
		var compilation = Interface.CompileCode($"function onCreated() {{ {body} }}", "weapon", "script-variable-standard-functions", withHeader: false);
		if (!compilation.Success)
			throw new InvalidOperationException($"Script failure: {compilation.ErrMsg}");

		var scriptManager = new ScriptManager(new FakeLogger<ScriptManager>());
		return new(scriptManager, "script-variable-standard-functions", compilation.ByteCode);
	}
}