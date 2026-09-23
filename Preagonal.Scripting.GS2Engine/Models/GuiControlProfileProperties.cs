using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiControlProfileProperties : ScriptProperties<GuiControlProfile>
{
	public GuiControlProfileProperties() : base(typeof(ScriptVariable))
	{
		var properties = new PropertyDefinitions<GuiControlProfile>
		{
			{
				"align", "The profile's text alignment.", profile => profile.Align, (profile, value) =>
				{
					profile.Align   = value;
					profile.Justify = value;
				}
			},
			{ "autosizeheight", "Whether controls automatically size their height to content.", profile => profile.AutoSizeHeight, (profile, value) => profile.AutoSizeHeight = value },
			{ "autosizewidth", "Whether controls automatically size their width to content.", profile => profile.AutoSizeWidth, (profile, value) => profile.AutoSizeWidth = value },
			{ "backgroundinset", "The inset applied when drawing the background.", profile => profile.BackgroundInset, (profile, value) => profile.BackgroundInset = value },
			{ "bitmap", "The profile bitmap resource.", profile => profile.Bitmap, (profile, value) => profile.Bitmap = value },
			{ "bevelcolorhl", "The highlighted bevel color.", profile => profile.BevelColorHl, (profile, value) => profile.BevelColorHl = value },
			{ "bevelcolorll", "The lowlight bevel color.", profile => profile.BevelColorLl, (profile, value) => profile.BevelColorLl = value },
			{ "border", "The profile border style.", profile => profile.Border, (profile, value) => profile.Border = value },
			{ "bordercolor", "The normal border color.", profile => profile.BorderColor, (profile, value) => profile.BorderColor = value },
			{ "bordercolorhl", "The highlighted border color.", profile => profile.BorderColorHl, (profile, value) => profile.BorderColorHl = value },
			{ "bordercolorna", "The disabled border color.", profile => profile.BorderColorNa, (profile, value) => profile.BorderColorNa = value },
			{ "borderthickness", "The border thickness.", profile => profile.BorderThickness, (profile, value) => profile.BorderThickness = value },
			{ "boxextent", "The checkbox dimensions used by GuiCheckBoxCtrl.", profile => profile.BoxExtent, (profile, value) => profile.BoxExtent = value },
			{ "cankeyfocus", "Whether controls using the profile can receive keyboard focus.", profile => profile.CanKeyFocus, (profile, value) => profile.CanKeyFocus = value },
			{ "cursorcolor", "The text cursor color.", profile => profile.CursorColor, (profile, value) => profile.CursorColor = value },
			{ "fillcolor", "The normal fill color.", profile => profile.FillColor, (profile, value) => profile.FillColor = value },
			{ "fillcolorhl", "The highlighted fill color.", profile => profile.FillColorHl, (profile, value) => profile.FillColorHl = value },
			{ "fillcolorna", "The disabled fill color.", profile => profile.FillColorNa, (profile, value) => profile.FillColorNa = value },
			{ "fillonlynonchildarea", "When opaque, fills only the background area not occupied by child controls.", profile => profile.FillOnlyNonChildArea, (profile, value) => profile.FillOnlyNonChildArea = value },
			{ "focusonshow", "Whether controls receive keyboard focus when shown.", profile => profile.FocusOnShow, (profile, value) => profile.FocusOnShow = value },
			{ "fontcolor", "The normal font color.", profile => profile.FontColor, (profile, value) => profile.FontColor = value },
			{ "fontcolorhl", "The highlighted font color.", profile => profile.FontColorHl, (profile, value) => profile.FontColorHl = value },
			{ "fontcolorna", "The disabled font color.", profile => profile.FontColorNa, (profile, value) => profile.FontColorNa = value },
			{ "fontcolorsel", "The selected font color.", profile => profile.FontColorSel, (profile, value) => profile.FontColorSel = value },
			{ "fontcolorlink", "The hyperlink font color.", profile => profile.FontColorLink, (profile, value) => profile.FontColorLink = value },
			{ "fontcolorlinkhl", "The highlighted hyperlink font color.", profile => profile.FontColorLinkHl, (profile, value) => profile.FontColorLinkHl = value },
			{ "fontsize", "The font size.", profile => profile.FontSize, (profile, value) => profile.FontSize = value },
			{ "fontstyle", "The font style flags.", profile => profile.FontStyle, (profile, value) => profile.FontStyle = value },
			{ "fontstylecontrolwords", "The style used for control words.", profile => profile.FontStyleControlWords, (profile, value) => profile.FontStyleControlWords = value },
			{ "fontstyleidentifiers", "The style used for identifiers.", profile => profile.FontStyleIdentifiers, (profile, value) => profile.FontStyleIdentifiers = value },
			{ "fontstylestrings", "The style used for string literals.", profile => profile.FontStyleStrings, (profile, value) => profile.FontStyleStrings = value },
			{ "fontstylenumbers", "The style used for numeric literals.", profile => profile.FontStyleNumbers, (profile, value) => profile.FontStyleNumbers = value },
			{ "fonttype", "The font resource name.", profile => profile.FontType, (profile, value) => profile.FontType = value },
			{ "gradientcolor", "The secondary color used for gradients.", profile => profile.GradientColor, (profile, value) => profile.GradientColor = value },
			{
				"justify", "An alias for the profile's align setting.", profile => profile.Align, (profile, value) =>
				{
					profile.Align   = value;
					profile.Justify = value;
				}
			},
			{ "linespacing", "The spacing between text lines.", profile => profile.LineSpacing, (profile, value) => profile.LineSpacing = value },
			{ "mouseoverbitmap", "The bitmap shown while the pointer is over a control.", profile => profile.MouseOverBitmap, (profile, value) => profile.MouseOverBitmap = value },
			{ "mouseoverselected", "Whether pointer hover uses the selected visual state.", profile => profile.MouseOverSelected, (profile, value) => profile.MouseOverSelected = value },
			{ "modal", "Whether controls using the profile are modal.", profile => profile.Modal, (profile, value) => profile.Modal = value },
			{ "normalbitmap", "The bitmap shown in the normal state.", profile => profile.NormalBitmap, (profile, value) => profile.NormalBitmap = value },
			{ "numbersonly", "Restricts text-entry controls to numeric input.", profile => profile.NumbersOnly, (profile, value) => profile.NumbersOnly = value },
			{ "returntab", "Whether tab and return input is retained by the control.", profile => profile.ReturnTab, (profile, value) => profile.ReturnTab = value },
			{ "opaque", "Whether the profile draws an opaque background.", profile => profile.Opaque, (profile, value) => profile.Opaque = value },
			{ "overridestylefont", "Allows the profile's font settings to override those of the GUI style.", profile => profile.OverrideStyleFont, (profile, value) => profile.OverrideStyleFont = value },
			{ "pressedbitmap", "The bitmap shown while a control is pressed.", profile => profile.PressedBitmap, (profile, value) => profile.PressedBitmap = value },
			{ "shadowcolor", "The text shadow color.", profile => profile.ShadowColor, (profile, value) => profile.ShadowColor = value },
			{ "shadowoffset", "The text shadow offset.", profile => profile.ShadowOffset, (profile, value) => profile.ShadowOffset = value },
			{ "soundbuttondown", "The sound played when a button is pressed.", profile => profile.SoundButtonDown, (profile, value) => profile.SoundButtonDown = value },
			{ "soundbuttonover", "The sound played when the pointer enters a button.", profile => profile.SoundButtonOver, (profile, value) => profile.SoundButtonOver = value },
			{ "tab", "Whether controls using the profile participate in tab order.", profile => profile.Tab, (profile, value) => profile.Tab = value },
			{ "textgradient", "Whether text uses gradient coloring.", profile => profile.TextGradient, (profile, value) => profile.TextGradient = value },
			{ "textoffset", "The offset applied when drawing text.", profile => profile.TextOffset, (profile, value) => profile.TextOffset = value },
			{ "textshadow", "Whether text shadows are enabled.", profile => profile.TextShadow, (profile, value) => profile.TextShadow = value },
			{ "transparency", "The profile transparency mode.", profile => profile.Transparency, (profile, value) => profile.Transparency = value }
		};
		AddProperties(this, properties);

		var functions = new FunctionDefinitions<GuiControlProfile>
		{
			{ "gettextwidth", "Measures text width using this profile's font.", (profile, args) => profile.GetTextWidth(args.Length > 0 ? args[0].GetValue()?.ToString() ?? string.Empty : string.Empty), [new("text", typeof(string))], typeof(int) },
			{ "gettextheight", "Measures text height using this profile's font.", (profile, _) => profile.GetTextHeight(), [], typeof(int) },
			{
				"preloadfont", "Loads the profile's font in advance for later rendering.", (profile, _) =>
				{
					profile.PreloadFont();
					return 0;
				}
			}
		};
		AddFunctions(this, functions);
		Compile();
	}
}