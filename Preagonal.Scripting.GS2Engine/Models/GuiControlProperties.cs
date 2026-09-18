using System.Linq;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiControlProperties : ScriptProperties<GuiControl>
{
	public GuiControlProperties() : base(typeof(ScriptVariable))
	{
		_ = ScriptVariable.PropertiesInstance;
		_ = GuiAnimation.PropertiesInstance;

		var propertyDefinitions = new PropertyDefinitions<GuiControl>();
		propertyDefinitions.Add("name", "The registered name of the control.", control => control.Name);
		propertyDefinitions.Add("acceptdropfiles", "Whether the control accepts dropped files.", control => control.AcceptDropFiles, (control, acceptDropFiles) => control.AcceptDropFiles = acceptDropFiles);
		propertyDefinitions.Add("active", "Whether the control is active.", control => control.Active, (control, active) => control.Active                                                 = active);
		propertyDefinitions.Add("alpha", "The control opacity.", control => control.Alpha, (control, alpha) => control.Alpha                                                               = alpha);
		propertyDefinitions.Add("areaclickpriority", "The control's area-click priority.", control => control.AreaClickPriority, (control, areaClickPriority) => control.AreaClickPriority = areaClickPriority);
		propertyDefinitions.Add("awake", "Whether the control is awake.", control => control.Awake);
		propertyDefinitions.Add("bitmapcache", "Whether bitmap caching is enabled.", control => control.BitmapCache, (control, bitmapCache) => control.BitmapCache                      = bitmapCache);
		propertyDefinitions.Add("blue", "The control's blue color multiplier.", control => control.Blue, (control, blue) => control.Blue                                                = blue);
		propertyDefinitions.Add("bounds", "The control position and extent.", control => control.Bounds, (control, bounds) => control.Bounds                                            = bounds);
		propertyDefinitions.Add("canclose", "Whether the control can be closed.", control => control.CanClose, (control, canClose) => control.CanClose                                  = canClose);
		propertyDefinitions.Add("canmaximize", "Whether the control can be maximized.", control => control.CanMaximize, (control, canMaximize) => control.CanMaximize                   = canMaximize);
		propertyDefinitions.Add("canminimize", "Whether the control can be minimized.", control => control.CanMinimize, (control, canMinimize) => control.CanMinimize                   = canMinimize);
		propertyDefinitions.Add("canmove", "Whether the control can be moved.", control => control.CanMove, (control, canMove) => control.CanMove                                       = canMove);
		propertyDefinitions.Add("canresize", "Whether the control can be resized.", control => control.CanResize, (control, canResize) => control.CanResize                             = canResize);
		propertyDefinitions.Add("clientextent", "The control's client-area extent.", control => control.ClientExtent, (control, clientExtent) => control.ClientExtent                   = clientExtent);
		propertyDefinitions.Add("clientheight", "The height of the control's client area.", control => control.ClientHeight, (control, clientHeight) => control.ClientHeight            = clientHeight);
		propertyDefinitions.Add("clientwidth", "The width of the control's client area.", control => control.ClientWidth, (control, clientWidth) => control.ClientWidth                 = clientWidth);
		propertyDefinitions.Add("clipchildren", "Whether child controls are clipped.", control => control.ClipChildren, (control, clipChildren) => control.ClipChildren                 = clipChildren);
		propertyDefinitions.Add("clipmove", "Whether movement is clipped to the parent area.", control => control.ClipMove, (control, clipMove) => control.ClipMove                     = clipMove);
		propertyDefinitions.Add("cliptobounds", "Whether rendering is clipped to the control bounds.", control => control.ClipToBounds, (control, clipToBounds) => control.ClipToBounds = clipToBounds);
		propertyDefinitions.Add("color", "The control color.", control => control.Color, (control, color) => control.Color                                                              = color);
		propertyDefinitions.Add("cursor", "The cursor shown over the control.", control => control.Cursor, (control, cursor) => control.Cursor                                          = cursor);
		propertyDefinitions.Add("editing", "Whether the control is in editing mode.", control => control.Editing, (control, editing) => control.Editing                                 = editing);
		propertyDefinitions.Add("mode", "The control drawing mode.", control => control.Mode, (control, mode) => control.Mode                                                           = mode);
		propertyDefinitions.Add("objecttype", "The runtime type name of the control.", control => control.GetType().Name);
		propertyDefinitions.Add("extent", "The control width and height.", control => control.Extent, (control, extent) => control.Extent                                                                       = extent);
		propertyDefinitions.Add("fastchildrender", "Whether optimized child rendering is enabled.", control => control.FastChildRender, (control, fastChildRender) => control.FastChildRender                   = fastChildRender);
		propertyDefinitions.Add<object?>("firstresponder", "The child control holding keyboard focus.", control => control.FirstResponder, (control, firstResponder) => control.FirstResponder                  = GetControl(firstResponder));
		propertyDefinitions.Add("flickering", "Whether the control is flickering.", control => control.Flickering, (control, flickering) => control.Flickering                                                  = flickering);
		propertyDefinitions.Add("flickertime", "The current flicker duration.", control => control.FlickerTime, (control, flickerTime) => control.FlickerTime                                                   = flickerTime);
		propertyDefinitions.Add("flickerbasetime", "The base flicker interval.", control => control.FlickerBaseTime, (control, flickerBaseTime) => control.FlickerBaseTime                                      = flickerBaseTime);
		propertyDefinitions.Add("green", "The control's green color multiplier.", control => control.Green, (control, green) => control.Green                                                                   = green);
		propertyDefinitions.Add("height", "The control height.", control => control.Height, (control, height) => control.Height                                                                                 = height < 1 ? 1 : height);
		propertyDefinitions.Add("hint", "The control tooltip text.", control => control.Hint, (control, hint) => control.Hint                                                                                   = hint);
		propertyDefinitions.Add("hinttime", "The delay before showing the control hint.", control => control.HintTime, (control, hintTime) => control.HintTime                                                  = hintTime);
		propertyDefinitions.Add("horizsizing", "The horizontal resizing behavior.", control => control.HorizSizing, (control, horizSizing) => control.HorizSizing                                               = horizSizing);
		propertyDefinitions.Add("isexternal", "Whether the control is hosted externally.", control => control.IsExternal, (control, isExternal) => control.IsExternal                                           = isExternal);
		propertyDefinitions.Add("isinanimation", "Whether the control is running a standard animation.", control => control.IsInAnimation, (control, isInAnimation) => control.IsInAnimation                    = isInAnimation);
		propertyDefinitions.Add("isininoutanimation", "Whether the control is running an in/out animation.", control => control.IsInInOutAnimation, (control, isInInOutAnimation) => control.IsInInOutAnimation = isInInOutAnimation);
		propertyDefinitions.Add("lockmousedown", "Whether mouse-down input remains locked to the control.", control => control.LockMouseDown, (control, lockMouseDown) => control.LockMouseDown                 = lockMouseDown);
		propertyDefinitions.Add("maximized", "Whether the control is maximized.", control => control.Maximized, (control, maximized) => control.Maximized                                                       = maximized);
		propertyDefinitions.Add("vertsizing", "The vertical resizing behavior.", control => control.VertSizing, (control, vertSizing) => control.VertSizing                                                     = vertSizing);
		propertyDefinitions.Add("minextent", "The minimum control extent.", control => control.MinExtent, (control, minExtent) => control.MinExtent                                                             = minExtent);
		propertyDefinitions.Add("minsize", "The minimum control size.", control => control.MinSize, (control, minSize) => control.MinSize                                                                       = minSize);
		propertyDefinitions.Add("modal", "Whether the control is modal.", control => control.Modal, (control, modal) => control.Modal                                                                           = modal);
		propertyDefinitions.Add<object?>("parent", "The parent control.", control => control.Parent);
		propertyDefinitions.Add("position", "The control position.", control => control.Position, (control, position) => control.Position                                                   = position);
		propertyDefinitions.Add<object?>("profile", "The visual profile used by the control.", control => control.GetResolvedProfile(), (control, profile) => control.Profile               = GetValue(profile));
		propertyDefinitions.Add("red", "The control's red color multiplier.", control => control.Red, (control, red) => control.Red                                                         = red);
		propertyDefinitions.Add("resizewidth", "The width used by resize operations.", control => control.ResizeWidth, (control, resizeWidth) => control.ResizeWidth                        = resizeWidth);
		propertyDefinitions.Add("resizeheight", "The height used by resize operations.", control => control.ResizeHeight, (control, resizeHeight) => control.ResizeHeight                   = resizeHeight);
		propertyDefinitions.Add("rotation", "The control rotation.", control => control.Rotation, (control, rotation) => control.Rotation                                                   = rotation);
		propertyDefinitions.Add("rotationcenter", "The center used to rotate the control.", control => control.RotationCenter, (control, rotationCenter) => control.RotationCenter          = rotationCenter);
		propertyDefinitions.Add("scrolllinex", "The horizontal scrolling increment.", control => control.ScrollLineX, (control, scrollLineX) => control.ScrollLineX                         = scrollLineX);
		propertyDefinitions.Add("scrollliney", "The vertical scrolling increment.", control => control.ScrollLineY, (control, scrollLineY) => control.ScrollLineY                           = scrollLineY);
		propertyDefinitions.Add("showhint", "Whether the control hint can be shown.", control => control.ShowHint, (control, showHint) => control.ShowHint                                  = showHint);
		propertyDefinitions.Add("alwaysOnTop", "Whether the control remains above sibling controls.", control => control.AlwaysOnTop, (control, alwaysOnTop) => control.AlwaysOnTop         = alwaysOnTop);
		propertyDefinitions.Add("style", "The control style.", control => control.Style, (control, style) => control.Style                                                                  = style);
		propertyDefinitions.Add("text", "The control text.", control => control.Text, (control, text) => control.Text                                                                       = text);
		propertyDefinitions.Add("useownprofile", "Whether the control owns a separate visual profile.", control => control.UseOwnProfile, (control, useOwnProfile) => control.UseOwnProfile = useOwnProfile);
		propertyDefinitions.Add("visible", "Whether the control is visible.", control => control.Visible, (control, visible) => control.Visible                                             = visible);
		propertyDefinitions.Add("width", "The control width.", control => control.Width, (control, width) => control.Width                                                                  = width < 1 ? 1 : width);
		propertyDefinitions.Add("x", "The horizontal position of the control.", control => control.X, (control, x) => control.X                                                             = x);
		propertyDefinitions.Add("y", "The vertical position of the control.", control => control.Y, (control, y) => control.Y                                                               = y);
		propertyDefinitions.Add("controls", "The child controls.", control => control.Controls);

		AddProperties(this, propertyDefinitions);

		var functionDefinitions = new FunctionDefinitions<GuiControl>();
		functionDefinitions.Add<string>("objecttype", "Returns the runtime type name of the control.", (control, _) => control.GetType().Name);
		functionDefinitions.Add<int>(
			"addcontrol",
			"Adds a child control.",
			(control, o2) =>
			{
				var control2 = o2.FirstOrDefault();
				switch (control2)
				{
					case GuiControl newControl:
						control.AddControl(newControl);
						break;
					case IStackEntry newControlStackEntry:
						var stackControl = newControlStackEntry.GetValue<GuiControl>();
						control.AddControl(stackControl);
						break;
				}

				return 0;
			},
			[new("control", typeof(object))]
		);
		functionDefinitions.Add<int>(
			"bringtofront",
			"Moves the control in front of its siblings.",
			(control, _) =>
			{
				control.BringToFront();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"clearcontrols",
			"Removes all child controls.",
			(control, _) =>
			{
				control.ClearControls();
				return 0;
			}
		);
		functionDefinitions.Add<object>("createanimation", "Creates an animation owned by the control.", (control, _) => control.CreateAnimation() is { } animation ? animation : 0);
		functionDefinitions.Add<int>(
			"destroy",
			"Destroys the control.",
			(control, _) =>
			{
				control.Destroy();
				return 0;
			}
		);
		functionDefinitions.Add<object>("findcontrol", "Finds a descendant control by name.", (control, args) => control.FindControl(GetString(args, 0)) is { } foundControl ? foundControl : 0, [new("name", typeof(string))]);
		functionDefinitions.Add<object>("getparent", "Returns the parent control.", (control, _) => control.GetParent() is { } parent ? parent : 0);
		functionDefinitions.Add<string>("globaltolocalcoord", "Converts global coordinates to control-local coordinates.", (control, args) => control.GlobalToLocalCoord(GetString(args, 0)), [new("position", typeof(string))]);
		functionDefinitions.Add<string>("gettext", "Returns the control text.", (control, _) => control.Text);
		functionDefinitions.Add<int>(
			"hide",
			"Hides the control.",
			(control, _) =>
			{
				control.Hide();
				return 0;
			}
		);
		functionDefinitions.Add<bool>("isempty", "Returns whether the control text is empty.", (control, _) => string.IsNullOrEmpty(control.Text));
		functionDefinitions.Add<bool>("isactuallyvisible", "Returns whether the control is visible through its parent hierarchy.", (control, _) => control.IsActuallyVisible());
		functionDefinitions.Add<bool>("isfirstresponder", "Returns whether the control holds keyboard focus.", (control, _) => control.IsFirstResponder());
		functionDefinitions.Add<bool>("ismouselocked", "Returns whether the specified mouse button is locked to the control.", (control, args) => control.IsMouseLocked(GetInt(args, 0)), [new("button", typeof(int))]);
		functionDefinitions.Add<string>("localtoglobalcoord", "Converts control-local coordinates to global coordinates.", (control, args) => control.LocalToGlobalCoord(GetString(args, 0)), [new("position", typeof(string))]);
		functionDefinitions.Add<int>(
			"makefirstresponder",
			"Sets or clears keyboard focus for the control.",
			(control, args) =>
			{
				control.MakeFirstResponder(GetBool(args, 0));
				return 0;
			},
			[new("active", typeof(bool))]
		);
		functionDefinitions.Add<int>(
			"mouselock",
			"Locks the specified mouse button to the control.",
			(control, args) =>
			{
				control.MouseLock(GetInt(args, 0));
				return 0;
			},
			[new("button", typeof(int))]
		);
		functionDefinitions.Add<int>(
			"mouseunlock",
			"Unlocks the specified mouse button from the control.",
			(control, args) =>
			{
				control.MouseUnlock(GetInt(args, 0));
				return 0;
			},
			[new("button", typeof(int))]
		);
		functionDefinitions.Add<int>(
			"mouseunlockall",
			"Releases every mouse-button lock held by the control.",
			(control, _) =>
			{
				control.MouseUnlockAll();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"pushtoback",
			"Moves the control behind its siblings.",
			(control, _) =>
			{
				control.PushToBack();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"resize",
			"Changes the control position and size.",
			(control, args) =>
			{
				control.Resize(GetInt(args, 0), GetInt(args, 1), GetInt(args, 2), GetInt(args, 3));
				return 0;
			},
			[new("x", typeof(int)), new("y", typeof(int)), new("width", typeof(int)), new("height", typeof(int))]
		);
		functionDefinitions.Add<int>(
			"repaint",
			"Requests that the control be redrawn.",
			(control, _) =>
			{
				control.Repaint();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"settext",
			"Sets the control text.",
			(control, args) =>
			{
				control.Text = GetString(args, 0);
				return 0;
			},
			[new("text", typeof(string))]
		);
		functionDefinitions.Add<int>(
			"show",
			"Shows the control.",
			(control, _) =>
			{
				control.Show();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"showtop",
			"Shows the control in front of its siblings.",
			(control, _) =>
			{
				control.ShowTop();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"showAlwaysTop",
			"Shows the control and keeps it above sibling controls.",
			(control, _) =>
			{
				control.ShowAlwaysTop();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"sortcontrols",
			"Sorts the child controls by their display order.",
			(control, _) =>
			{
				control.SortControls();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"startdrag",
			"Begins dragging the control.",
			(control, _) =>
			{
				control.StartDrag();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"stopanimations",
			"Stops the control's active animations.",
			(control, _) =>
			{
				control.StopAnimations();
				return 0;
			}
		);
		functionDefinitions.Add<int>(
			"stopinoutanimations",
			"Stops the control's active in/out animations.",
			(control, _) =>
			{
				control.StopInOutAnimations();
				return 0;
			}
		);
		functionDefinitions.Add<object>("tabfirst", "Returns the first child in keyboard tab order.", (control, _) => control.TabFirst() is { } firstControl ? firstControl : 0);

		AddFunctions(this, functionDefinitions);

		Compile();
	}

	private static IGuiControl? GetControl(object? value)
	{
		if (value is IStackEntry entry) value = entry.GetValue();
		return value as IGuiControl;
	}

	private static object? GetValue(object? value) => value is IStackEntry entry ? entry.GetValue() : value;

	private static bool GetBool(IStackEntry[] args, int index) => args.Length > index && args[index].GetValue<bool>();

	private static int GetInt(IStackEntry[] args, int index) => args.Length > index ? (int)args[index].GetValue<double>() : 0;

	private static string GetString(IStackEntry[] args, int index) => args.Length > index ? args[index].GetValue()?.ToString() ?? string.Empty : string.Empty;
}