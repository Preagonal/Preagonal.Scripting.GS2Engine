using System.Globalization;
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
		propertyDefinitions.Add(
			"areaclickpriority",
			"The touch hit-test priority: 2 accepts nearby taps, 1 suppresses nearby button targets, and 0 requires a direct hit without a competing area-click target.",
			control => control.AreaClickPriority,
			(control, areaClickPriority) => control.AreaClickPriority = areaClickPriority
		);
		propertyDefinitions.Add("awake", "Whether the control is awake.", control => control.Awake);
		propertyDefinitions.Add("bitmapcache", "The bitmap-cache setting for this control and its children, intended for mostly static graphics.", control => control.BitmapCache, (control, bitmapCache) => control.BitmapCache = bitmapCache);
		propertyDefinitions.Add("blue", "The control's blue color multiplier.", control => control.Blue, (control, blue) => control.Blue                                                                                         = blue);
		propertyDefinitions.Add("bounds", "The control rectangle as {x,y,width,height}, combining position and extent.", control => control.Bounds, (control, bounds) => control.Bounds                                          = bounds);
		propertyDefinitions.Add("canclose", "Whether the control can be closed.", control => control.CanClose, (control, canClose) => control.CanClose                                                                           = canClose);
		propertyDefinitions.Add("canmaximize", "Whether the control can be maximized.", control => control.CanMaximize, (control, canMaximize) => control.CanMaximize                                                            = canMaximize);
		propertyDefinitions.Add("canminimize", "Whether the control can be minimized.", control => control.CanMinimize, (control, canMinimize) => control.CanMinimize                                                            = canMinimize);
		propertyDefinitions.Add("canmove", "Whether the control can be moved.", control => control.CanMove, (control, canMove) => control.CanMove                                                                                = canMove);
		propertyDefinitions.Add("canresize", "Whether the control can be resized.", control => control.CanResize, (control, canResize) => control.CanResize                                                                      = canResize);
		propertyDefinitions.Add("clientextent", "The control's client-area extent.", control => control.ClientExtent, (control, clientExtent) => control.ClientExtent                                                            = clientExtent);
		propertyDefinitions.Add("clientheight", "The height of the control's client area.", control => control.ClientHeight, (control, clientHeight) => control.ClientHeight                                                     = clientHeight);
		propertyDefinitions.Add("clientwidth", "The width of the control's client area.", control => control.ClientWidth, (control, clientWidth) => control.ClientWidth                                                          = clientWidth);
		propertyDefinitions.Add("clipchildren", "Clips child controls to their bounds when enabled; the default is true.", control => control.ClipChildren, (control, clipChildren) => control.ClipChildren                      = clipChildren);
		propertyDefinitions.Add("clipmove", "Keeps a user-moved control inside its parent's bounds; enabled by default.", control => control.ClipMove, (control, clipMove) => control.ClipMove                                   = clipMove);
		propertyDefinitions.Add(
			"cliptobounds",
			"Clips drawing to this control's bounds; enabled by default and ignored when the parent disables clipchildren.",
			control => control.ClipToBounds,
			(control, clipToBounds) => control.ClipToBounds = clipToBounds
		);
		propertyDefinitions.Add("color", "The control color.", control => control.Color, (control, color) => control.Color = color);
		propertyDefinitions.Add(
			"cursor",
			"The pointer displayed over this control, such as default, hand, text, crosshair, drag, col-resize, row-resize, wait, progress, or help; availability is platform-dependent.",
			control => control.Cursor,
			(control, cursor) => control.Cursor = cursor
		);
		propertyDefinitions.Add("editing", "Whether the control is in editing mode.", control => control.Editing, (control, editing) => control.Editing                       = editing);
		propertyDefinitions.Add("mode", "The blend mode: 0 additive, 1 transparent, 2 subtractive, or 3 day/night.", control => control.Mode, (control, mode) => control.Mode = mode);
		propertyDefinitions.Add("objecttype", "The runtime type name of the control.", control => control.GetType().Name);
		propertyDefinitions.Add("extent", "The control's width and height as a pair.", control => control.Extent, (control, extent) => control.Extent = extent);
		propertyDefinitions.Add(
			"fastchildrender",
			"The legacy fast-render preference for simple bitmap children without tiling, nested controls, tinting, or source rectangles.",
			control => control.FastChildRender,
			(control, fastChildRender) => control.FastChildRender = fastChildRender
		);
		propertyDefinitions.Add<object?>("firstresponder", "The child control currently receiving keyboard input.", control => control.FirstResponder, (control, firstResponder) => control.FirstResponder = GetControl(firstResponder));
		propertyDefinitions.Add("flickering", "Alternates the control's visibility at intervals set by flickertime.", control => control.Flickering, (control, flickering) => control.Flickering = flickering);
		propertyDefinitions.Add("flickertime", "The visibility-toggle interval in seconds when flickering is enabled.", control => control.FlickerTime, (control, flickerTime) => control.FlickerTime = flickerTime);
		propertyDefinitions.Add("flickerbasetime", "The phase offset used to stagger controls that flicker at the same frequency.", control => control.FlickerBaseTime, (control, flickerBaseTime) => control.FlickerBaseTime = flickerBaseTime);
		propertyDefinitions.Add("green", "The control's green color multiplier.", control => control.Green, (control, green) => control.Green = green);
		propertyDefinitions.Add("height", "The control height.", control => control.Height, (control, height) => control.Height = height < 1 ? 1 : height);
		propertyDefinitions.Add("hint", "The tooltip text displayed while the pointer rests over this control.", control => control.Hint, (control, hint) => control.Hint = hint);
		propertyDefinitions.Add("hinttime", "The idle-pointer delay before the tooltip appears.", control => control.HintTime, (control, hintTime) => control.HintTime = hintTime);
		propertyDefinitions.Add("horizsizing", "Horizontal layout behavior on parent resize: right, width, left, center, or relative.", control => control.HorizSizing, (control, horizSizing) => control.HorizSizing = horizSizing);
		propertyDefinitions.Add("isexternal", "Whether the control is hosted externally.", control => control.IsExternal, (control, isExternal) => control.IsExternal = isExternal);
		propertyDefinitions.Add("isinanimation", "Whether the control is running a standard animation.", control => control.IsInAnimation, (control, isInAnimation) => control.IsInAnimation = isInAnimation);
		propertyDefinitions.Add("isininoutanimation", "Whether the control is running an in/out animation.", control => control.IsInInOutAnimation, (control, isInInOutAnimation) => control.IsInInOutAnimation = isInInOutAnimation);
		propertyDefinitions.Add("lockmousedown", "Keeps mouse input captured by this control from a press until release.", control => control.LockMouseDown, (control, lockMouseDown) => control.LockMouseDown = lockMouseDown);
		propertyDefinitions.Add("maximized", "Whether the control is maximized.", control => control.Maximized, (control, maximized) => control.Maximized = maximized);
		propertyDefinitions.Add("vertsizing", "Vertical layout behavior on parent resize: bottom, height, top, center, or relative.", control => control.VertSizing, (control, vertSizing) => control.VertSizing = vertSizing);
		propertyDefinitions.Add("minextent", "The minimum permitted width and height.", control => control.MinExtent, (control, minExtent) => control.MinExtent = minExtent);
		propertyDefinitions.Add("minsize", "An alias for the minimum dimensions in minextent.", control => control.MinSize, (control, minSize) => control.MinSize = minSize);
		propertyDefinitions.Add("modal", "Whether the control is modal.", control => control.Modal, (control, modal) => control.Modal = modal);
		propertyDefinitions.Add<object?>("parent", "The parent control.", control => control.Parent);
		propertyDefinitions.Add("position", "The control position.", control => control.Position, (control, position) => control.Position                                                                                  = position);
		propertyDefinitions.Add<object?>("profile", "The visual profile used by the control.", control => control.GetResolvedProfile(), (control, profile) => control.Profile                                              = GetValue(profile));
		propertyDefinitions.Add("red", "The control's red color multiplier.", control => control.Red, (control, red) => control.Red                                                                                        = red);
		propertyDefinitions.Add("resizewidth", "The width used by resize operations.", control => control.ResizeWidth, (control, resizeWidth) => control.ResizeWidth                                                       = resizeWidth);
		propertyDefinitions.Add("resizeheight", "The height used by resize operations.", control => control.ResizeHeight, (control, resizeHeight) => control.ResizeHeight                                                  = resizeHeight);
		propertyDefinitions.Add("rotation", "The control rotation.", control => control.Rotation, (control, rotation) => control.Rotation                                                                                  = rotation);
		propertyDefinitions.Add("rotationcenter", "The rotation pivot, initially {0,0}.", control => control.RotationCenter, (control, rotationCenter) => control.RotationCenter                                           = rotationCenter);
		propertyDefinitions.Add("scrolllinex", "The horizontal scrollbar-button step in pixels when this is a GuiScrollCtrl's first child.", control => control.ScrollLineX, (control, scrollLineX) => control.ScrollLineX = scrollLineX);
		propertyDefinitions.Add("scrollliney", "The vertical scrollbar-button step in pixels when this is a GuiScrollCtrl's first child.", control => control.ScrollLineY, (control, scrollLineY) => control.ScrollLineY   = scrollLineY);
		propertyDefinitions.Add("showhint", "Enables the tooltip when the pointer rests over the control.", control => control.ShowHint, (control, showHint) => control.ShowHint                                           = showHint);
		propertyDefinitions.Add("alwaysOnTop", "Whether the control remains above sibling controls.", control => control.AlwaysOnTop, (control, alwaysOnTop) => control.AlwaysOnTop                                        = alwaysOnTop);
		propertyDefinitions.Add("style", "The control style.", control => control.Style, (control, style) => control.Style                                                                                                 = style);
		propertyDefinitions.Add("text", "The control text.", control => control.Text, (control, text) => control.Text                                                                                                      = text);
		propertyDefinitions.Add("useownprofile", "Whether the control owns a separate visual profile.", control => control.UseOwnProfile, (control, useOwnProfile) => control.UseOwnProfile                                = useOwnProfile);
		propertyDefinitions.Add("visible", "Whether the control is visible.", control => control.Visible, (control, visible) => control.Visible                                                                            = visible);
		propertyDefinitions.Add("width", "The control width.", control => control.Width, (control, width) => control.Width                                                                                                 = width < 1 ? 1 : width);
		propertyDefinitions.Add("x", "The horizontal position of the control.", control => control.X, (control, x) => control.X                                                                                            = x);
		propertyDefinitions.Add("y", "The vertical position of the control.", control => control.Y, (control, y) => control.Y                                                                                              = y);
		propertyDefinitions.Add("controls", "The child controls.", control => control.Controls);

		AddProperties(this, propertyDefinitions);

		var functionDefinitions = new FunctionDefinitions<GuiControl>();
		functionDefinitions.Add<string>("objecttype", "Returns this object's script type name.", (control, _) => control.GetType().Name);
		functionDefinitions.Add(
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
		functionDefinitions.Add(
			"bringtofront",
			"Moves the control in front of its siblings.",
			(control, _) =>
			{
				control.BringToFront();
				return 0;
			}
		);
		functionDefinitions.Add(
			"clearcontrols",
			"Removes all child controls.",
			(control, _) =>
			{
				control.ClearControls();
				return 0;
			}
		);
		functionDefinitions.Add<object>("createanimation", "Creates an animation for changes to the control's position, rotation, or color.", (control, _) => control.CreateAnimation() is { } animation ? animation : 0);
		functionDefinitions.Add(
			"destroy",
			"Destroys the control.",
			(control, _) =>
			{
				control.Destroy();
				return 0;
			}
		);
		functionDefinitions.Add<object>("findcontrol", "Finds the control at a supplied {x,y} point.", (control, args) => control.FindControl(GetString(args, 0)) is { } foundControl ? foundControl : 0, [new("name", typeof(string))]);
		functionDefinitions.Add<object>("getparent", "Returns the parent control.", (control, _) => control.GetParent() is { } parent ? parent : 0);
		functionDefinitions.Add<double[]>(
			"globaltolocalcoord",
			"Converts screen coordinates to coordinates relative to this control's origin.",
			(control, args) => GetPointResult(control.GlobalToLocalCoord(GetPointArgument(args))),
			[new("position", typeof(string))]
		);
		functionDefinitions.Add<string>("gettext", "Returns the control text.", (control, _) => control.Text);
		functionDefinitions.Add(
			"hide",
			"Hides the control.",
			(control, _) =>
			{
				control.Hide();
				return 0;
			}
		);
		functionDefinitions.Add("isempty", "Returns whether the control text is empty.", (control, _) => string.IsNullOrEmpty(control.Text));
		functionDefinitions.Add("isactuallyvisible", "Returns whether the control is visible through its parent hierarchy.", (control, _) => control.IsActuallyVisible());
		functionDefinitions.Add("isfirstresponder", "Returns whether the control holds keyboard focus.", (control, _) => control.IsFirstResponder());
		functionDefinitions.Add("ismouselocked", "Reports whether this control has captured the given mouse ID, normally 0.", (control, args) => control.IsMouseLocked(GetInt(args, 0)), [new("button", typeof(int))]);
		functionDefinitions.Add<double[]>(
			"localtoglobalcoord",
			"Converts coordinates relative to this control's origin into screen coordinates.",
			(control, args) => GetPointResult(control.LocalToGlobalCoord(GetPointArgument(args))),
			[new("position", typeof(string))]
		);
		functionDefinitions.Add(
			"makefirstresponder",
			"Sets or clears keyboard focus for the control.",
			(control, args) =>
			{
				control.MakeFirstResponder(GetBool(args, 0));
				return 0;
			},
			[new("active", typeof(bool))]
		);
		functionDefinitions.Add(
			"mouselock",
			"Captures the supplied mouse ID, normally 0, so drag and release events continue outside the control.",
			(control, args) =>
			{
				control.MouseLock(GetInt(args, 0));
				return 0;
			},
			[new("button", typeof(int))]
		);
		functionDefinitions.Add(
			"mouseunlock",
			"Releases mouse capture so pointer events again depend on being inside the control.",
			(control, args) =>
			{
				control.MouseUnlock(GetInt(args, 0));
				return 0;
			},
			[new("button", typeof(int))]
		);
		functionDefinitions.Add(
			"mouseunlockall",
			"Releases every mouse or touch capture held by this control.",
			(control, _) =>
			{
				control.MouseUnlockAll();
				return 0;
			}
		);
		functionDefinitions.Add(
			"pushtoback",
			"Moves the control behind its siblings.",
			(control, _) =>
			{
				control.PushToBack();
				return 0;
			}
		);
		functionDefinitions.Add(
			"resize",
			"Changes the control position and size.",
			(control, args) =>
			{
				control.Resize(GetInt(args, 0), GetInt(args, 1), GetInt(args, 2), GetInt(args, 3));
				return 0;
			},
			[new("x", typeof(int)), new("y", typeof(int)), new("width", typeof(int)), new("height", typeof(int))]
		);
		functionDefinitions.Add(
			"repaint",
			"Requests a complete redraw of the control, primarily for external windows.",
			(control, _) =>
			{
				control.Repaint();
				return 0;
			}
		);
		functionDefinitions.Add(
			"settext",
			"Sets the control text.",
			(control, args) =>
			{
				control.Text = GetString(args, 0);
				return 0;
			},
			[new("text", typeof(string))]
		);
		functionDefinitions.Add(
			"show",
			"Shows the control.",
			(control, _) =>
			{
				control.Show();
				return 0;
			}
		);
		functionDefinitions.Add(
			"showtop",
			"Shows the control, gives it tab focus, and brings it to the front.",
			(control, _) =>
			{
				control.ShowTop();
				return 0;
			}
		);
		functionDefinitions.Add(
			"showAlwaysTop",
			"Shows the control and keeps it above sibling controls.",
			(control, _) =>
			{
				control.ShowAlwaysTop();
				return 0;
			}
		);
		functionDefinitions.Add(
			"sortcontrols",
			"Orders child controls by their vertical position.",
			(control, _) =>
			{
				control.SortControls();
				return 0;
			}
		);
		functionDefinitions.Add(
			"startdrag",
			"Begins dragging the control.",
			(control, _) =>
			{
				control.StartDrag();
				return 0;
			}
		);
		functionDefinitions.Add(
			"stopanimations",
			"Stops the control's previously created animations.",
			(control, _) =>
			{
				control.StopAnimations();
				return 0;
			}
		);
		functionDefinitions.Add(
			"stopinoutanimations",
			"Stops animations that move the control into or out of view.",
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

	private static string GetPointArgument(IStackEntry[] args) => args.Length > 0 ? args[0].GetValue<string>() ?? string.Empty : string.Empty;

	// Points must support GS2 indexing as well as the usual comma-separated string conversion.
	private static double[] GetPointResult(string point) => point.Split(',').Select(component => double.Parse(component, CultureInfo.InvariantCulture)).ToArray();
}