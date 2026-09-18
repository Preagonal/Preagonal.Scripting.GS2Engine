using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class GuiControlTests
{
	[Fact]
	public void Fade_in_starts_transparent_before_first_frame_and_stopping_restores_opacity()
	{
		var control   = new GuiControl("panel", null) { Alpha = 0.8 };
		var animation = control.CreateAnimation()!;
		animation.Transition = "fadein";
		Assert.Equal(0, control.Alpha);
		control.AdvanceAnimations(0.5);
		control.StopAnimations();
		Assert.Equal(0.8, control.Alpha);
		Assert.False(control.IsInAnimation);
	}

	[Fact]
	public void Completing_one_animation_does_not_cancel_other_animations()
	{
		var control = new GuiControl("panel", null);
		var first   = control.CreateAnimation()!;
		first.Transition = "moveoutbottom";
		first.Duration   = 0.25;
		var second = control.CreateAnimation()!;
		second.Transition = "fadeout";
		second.Duration   = 1;
		control.AdvanceAnimations(0.3);
		Assert.True(control.IsInAnimation);
		control.AdvanceAnimations(0.7);
		Assert.False(control.IsInAnimation);
	}

	[Fact]
	public void Animation_on_hidden_control_is_retained_and_completes()
	{
		var control   = new GuiControl("panel", null) { Visible = false, Alpha = 0.8 };
		var animation = control.CreateAnimation()!;
		animation.Transition = "fadein";
		animation.Duration   = 1;
		control.AdvanceAnimations(0.5);
		Assert.True(control.IsInAnimation);
		Assert.Equal(0.4, control.Alpha, 6);
		control.AdvanceAnimations(0.5);
		Assert.False(control.IsInAnimation);
		Assert.True(control.Visible);
		Assert.Equal(0.8, control.Alpha, 6);
	}

	[Fact]
	public void Out_animation_finishes_even_when_control_is_inactive()
	{
		var parent  = new GuiControl("parent", null);
		var control = new GuiControl("panel", null) { Active = false };
		parent.AddControl(control);
		var animation = control.CreateAnimation()!;
		animation.Transition = "moveoutbottom";
		animation.Duration   = 0.25;
		parent.AdvanceAnimations(0.3);
		Assert.False(control.IsInAnimation);
		Assert.False(control.Visible);
	}

	[Fact]
	public void Given_child_controls_When_clearing_controls_Then_children_are_destroyed_without_modifying_enumeration()
	{
		var parent = new GuiControl("parent", null);
		var child  = new GuiControl("child", null);
		parent.AddControl(child);

		parent.ClearControls();

		Assert.Empty(parent.Controls);
		Assert.False(child.Active);
		Assert.Null(child.Parent);
	}

	[Fact]
	public void Given_gui_control_When_reading_objecttype_property_Then_control_type_name_is_returned()
	{
		var control = new GuiControl("control", null);

		var objectType = control.Properties.Single(property => !property.IsFunction && property.PropertyName == "objecttype");

		Assert.Equal("GuiControl", objectType.Read(control));
	}

	[Fact]
	public void Given_gui_control_When_calling_objecttype_function_Then_control_type_name_is_returned()
	{
		var control = new GuiControl("control", null);

		var objectType = control.Properties.Single(property => property.IsFunction && property.PropertyName == "objecttype");

		Assert.Equal("GuiControl", objectType.Call(control));
	}

	[Fact]
	public void Given_readable_gui_control_property_When_called_Then_read_value_is_returned()
	{
		var control = new GuiControl("control", null);

		var objectType = control.Properties.Single(property => !property.IsFunction && property.PropertyName == "objecttype");

		Assert.Equal("GuiControl", objectType.Call(control));
	}

	[Fact]
	public void Given_focused_control_When_control_is_hidden_Then_first_responder_is_cleared()
	{
		var root  = new GuiControl("root", null);
		var input = new GuiControl("input", null);
		root.AddControl(input);
		input.MakeFirstResponder(true);

		input.Hide();

		Assert.Null(root.FirstResponder);
	}

	[Fact]
	public void Given_focused_descendant_When_parent_is_hidden_Then_first_responder_is_cleared()
	{
		var root  = new GuiControl("root", null);
		var panel = new GuiControl("panel", null);
		var input = new GuiControl("input", null);
		root.AddControl(panel);
		panel.AddControl(input);
		input.MakeFirstResponder(true);

		panel.Hide();

		Assert.Null(root.FirstResponder);
	}

	[Fact]
	public void Given_focused_control_When_control_is_destroyed_Then_first_responder_is_cleared()
	{
		var root  = new GuiControl("root", null);
		var input = new GuiControl("input", null);
		root.AddControl(input);
		input.MakeFirstResponder(true);

		input.Destroy();

		Assert.Null(root.FirstResponder);
	}

	[Fact]
	public void Given_focused_descendant_When_parent_is_destroyed_Then_first_responder_is_cleared()
	{
		var root  = new GuiControl("root", null);
		var panel = new GuiControl("panel", null);
		var input = new GuiControl("input", null);
		root.AddControl(panel);
		panel.AddControl(input);
		input.MakeFirstResponder(true);

		panel.Destroy();

		Assert.Null(root.FirstResponder);
	}
}