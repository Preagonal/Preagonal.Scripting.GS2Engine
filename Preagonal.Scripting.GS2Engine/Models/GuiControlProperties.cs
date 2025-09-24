using System.Linq;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiControlProperties : ScriptProperties<GuiControl>
{
	public GuiControlProperties() : base(typeof(ScriptVariable))
	{
		var propertyDefinitions = new PropertyDefinitions<GuiControl>();
		propertyDefinitions.Add("active", "", control => control.Active, (control, active) => control.Active = active);
		propertyDefinitions.Add("canmove", "", control => control.CanMove, (control, canMove) => control.CanMove = canMove);
		propertyDefinitions.Add("canresize", "", control => control.CanResize, (control, canResize) => control.CanResize = canResize);
		propertyDefinitions.Add("clipchildren", "", control => control.ClipChildren, (control, clipChildren) => control.ClipChildren = clipChildren);
		propertyDefinitions.Add("clipmove", "", control => control.ClipMove, (control, clipMove) => control.ClipMove = clipMove);
		propertyDefinitions.Add("cliptobounds", "", control => control.ClipToBounds, (control, clipToBounds) => control.ClipToBounds = clipToBounds);
		propertyDefinitions.Add("cursor", "", control => control.Cursor, (control, cursor) => control.Cursor = cursor);
		propertyDefinitions.Add("editing", "", control => control.Editing, (control, editing) => control.Editing = editing);
		propertyDefinitions.Add("extent", "", control => control.Extent, (control, extent) => control.Extent = extent);
		propertyDefinitions.Add("parent", "", control => control.Parent, (control, parent) => control.Parent = parent);
		propertyDefinitions.Add("flickering", "", control => control.Flickering, (control, flickering) => control.Flickering = flickering);
		propertyDefinitions.Add("flickertime", "", control => control.FlickerTime, (control, flickerTime) => control.FlickerTime = flickerTime);
		propertyDefinitions.Add("hint", "", control => control.Hint, (control, hint) => control.Hint = hint);
		propertyDefinitions.Add("horizsizing", "", control => control.HorizSizing, (control, horizSizing) => control.HorizSizing = horizSizing);
		propertyDefinitions.Add("vertsizing", "", control => control.VertSizing, (control, vertSizing) => control.VertSizing = vertSizing);
		propertyDefinitions.Add("minextent", "", control => control.MinExtent, (control, minExtent) => control.MinExtent = minExtent);
		propertyDefinitions.Add("minsize", "", control => control.MinSize, (control, minSize) => control.MinSize = minSize);
		propertyDefinitions.Add("position", "", control => control.Position, (control, position) => control.Position = position);
		propertyDefinitions.Add("profile", "", control => control.Profile, (control, profile) => control.Profile = profile);
		propertyDefinitions.Add("resizewidth", "", control => control.ResizeWidth, (control, resizeWidth) => control.ResizeWidth = resizeWidth);
		propertyDefinitions.Add("resizeheight", "", control => control.ResizeHeight, (control, resizeHeight) => control.ResizeHeight = resizeHeight);
		propertyDefinitions.Add("scrolllinex", "", control => control.ScrollLineX, (control, scrollLineX) => control.ScrollLineX = scrollLineX);
		propertyDefinitions.Add("scrollliney", "", control => control.ScrollLineY, (control, scrollLineY) => control.ScrollLineY = scrollLineY);
		propertyDefinitions.Add("showhint", "", control => control.ShowHint, (control, showHint) => control.ShowHint = showHint);
		propertyDefinitions.Add("useownprofile", "", control => control.UseOwnProfile, (control, useOwnProfile) => control.UseOwnProfile = useOwnProfile);
		propertyDefinitions.Add("visible", "", control => control.Visible, (control, visible) => control.Visible = visible);
		propertyDefinitions.Add("width", "", control => control.Width, (control, width) => control.Width = width);
		propertyDefinitions.Add("height", "", control => control.Height, (control, height) => control.Height = height);
		propertyDefinitions.Add("x", "", control => control.X, (control, x) => control.X = x);
		propertyDefinitions.Add("y", "", control => control.Y, (control, y) => control.Y = y);
		propertyDefinitions.Add("clientextent", "", control => control.ClientExtent, (control, clientExtent) => control.ClientExtent = clientExtent);
		propertyDefinitions.Add("controls", "", control => control.Controls);
		propertyDefinitions.Add("awake", "", control => control.Awake);

		AddProperties(this, propertyDefinitions);

		var functionDefinitions = new FunctionDefinitions<GuiControl>();
		functionDefinitions.Add<object?>("addcontrol", "",
		                                 (control, o2) =>
		                                 {
			                                 var control2 = o2.FirstOrDefault();
			                                 switch (control2)
			                                 {
				                                 case GuiControl newControl:
					                                 control.AddControl(newControl);
					                                 break;
				                                 case IStackEntry newControlStackEntry:
					                                 control.AddControl(newControlStackEntry.GetValue<GuiControl>());
					                                 break;
			                                 }
			                                 return control2;
		                                 }
		);

		AddFunctions(this, functionDefinitions);

		Compile();
	}
}