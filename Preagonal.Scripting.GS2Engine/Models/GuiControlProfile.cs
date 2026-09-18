using System;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiControlProfile(string name = "") : ScriptVariable(name)
{
	public new static readonly GuiControlProfileProperties PropertiesInstance = [];
	public override            IScriptProperties           Properties => PropertiesInstance;

	public string Align                 { get; set; } = "left";
	public bool   AutoSizeHeight        { get; set; }
	public bool   AutoSizeWidth         { get; set; }
	public string BackgroundInset       { get; set; } = string.Empty;
	public string Bitmap                { get; set; } = string.Empty;
	public string BevelColorHl          { get; set; } = string.Empty;
	public string BevelColorLl          { get; set; } = string.Empty;
	public int    Border                { get; set; }
	public string BorderColor           { get; set; } = string.Empty;
	public string BorderColorHl         { get; set; } = string.Empty;
	public string BorderColorNa         { get; set; } = string.Empty;
	public int    BorderThickness       { get; set; } = 1;
	public string BoxExtent             { get; set; } = string.Empty;
	public bool   CanKeyFocus           { get; set; }
	public string CursorColor           { get; set; } = string.Empty;
	public string FillColor             { get; set; } = string.Empty;
	public string FillColorHl           { get; set; } = string.Empty;
	public string FillColorNa           { get; set; } = string.Empty;
	public bool   FillOnlyNonChildArea  { get; set; }
	public bool   FocusOnShow           { get; set; }
	public string FontColor             { get; set; } = string.Empty;
	public string FontColorHl           { get; set; } = string.Empty;
	public string FontColorNa           { get; set; } = string.Empty;
	public string FontColorSel          { get; set; } = string.Empty;
	public string FontColorLink         { get; set; } = string.Empty;
	public string FontColorLinkHl       { get; set; } = string.Empty;
	public int    FontSize              { get; set; } = 12;
	public string FontStyle             { get; set; } = string.Empty;
	public string FontStyleControlWords { get; set; } = string.Empty;
	public string FontStyleIdentifiers  { get; set; } = string.Empty;
	public string FontStyleStrings      { get; set; } = string.Empty;
	public string FontStyleNumbers      { get; set; } = string.Empty;
	public string FontType              { get; set; } = string.Empty;
	public string GradientColor         { get; set; } = string.Empty;
	public string Justify               { get; set; } = "left";
	public int    LineSpacing           { get; set; }
	public string MouseOverBitmap       { get; set; } = string.Empty;
	public bool   MouseOverSelected     { get; set; }
	public bool   Modal                 { get; set; }
	public string NormalBitmap          { get; set; } = string.Empty;
	public bool   NumbersOnly           { get; set; }
	public bool   ReturnTab             { get; set; }
	public bool   Opaque                { get; set; }
	public bool   OverrideStyleFont     { get; set; }
	public string PressedBitmap         { get; set; } = string.Empty;
	public string ShadowColor           { get; set; } = string.Empty;
	public string ShadowOffset          { get; set; } = string.Empty;
	public string SoundButtonDown       { get; set; } = string.Empty;
	public string SoundButtonOver       { get; set; } = string.Empty;
	public bool   Tab                   { get; set; }
	public bool   TextGradient          { get; set; }
	public string TextOffset            { get; set; } = string.Empty;
	public bool   TextShadow            { get; set; }
	public double Transparency          { get; set; } = 1;
	public bool   FontPreloaded         { get; private set; }

	public void CopyFrom(GuiControlProfile source)
	{
		Align                 = source.Align;
		Justify               = source.Justify;
		AutoSizeHeight        = source.AutoSizeHeight;
		AutoSizeWidth         = source.AutoSizeWidth;
		BackgroundInset       = source.BackgroundInset;
		Bitmap                = source.Bitmap;
		BevelColorHl          = source.BevelColorHl;
		BevelColorLl          = source.BevelColorLl;
		Border                = source.Border;
		BorderColor           = source.BorderColor;
		BorderColorHl         = source.BorderColorHl;
		BorderColorNa         = source.BorderColorNa;
		BorderThickness       = source.BorderThickness;
		BoxExtent             = source.BoxExtent;
		CanKeyFocus           = source.CanKeyFocus;
		CursorColor           = source.CursorColor;
		FillColor             = source.FillColor;
		FillColorHl           = source.FillColorHl;
		FillColorNa           = source.FillColorNa;
		FillOnlyNonChildArea  = source.FillOnlyNonChildArea;
		FocusOnShow           = source.FocusOnShow;
		FontColor             = source.FontColor;
		FontColorHl           = source.FontColorHl;
		FontColorNa           = source.FontColorNa;
		FontColorSel          = source.FontColorSel;
		FontColorLink         = source.FontColorLink;
		FontColorLinkHl       = source.FontColorLinkHl;
		FontSize              = source.FontSize;
		FontStyle             = source.FontStyle;
		FontStyleControlWords = source.FontStyleControlWords;
		FontStyleIdentifiers  = source.FontStyleIdentifiers;
		FontStyleStrings      = source.FontStyleStrings;
		FontStyleNumbers      = source.FontStyleNumbers;
		FontType              = source.FontType;
		GradientColor         = source.GradientColor;
		LineSpacing           = source.LineSpacing;
		MouseOverBitmap       = source.MouseOverBitmap;
		MouseOverSelected     = source.MouseOverSelected;
		Modal                 = source.Modal;
		NormalBitmap          = source.NormalBitmap;
		NumbersOnly           = source.NumbersOnly;
		ReturnTab             = source.ReturnTab;
		Opaque                = source.Opaque;
		OverrideStyleFont     = source.OverrideStyleFont;
		PressedBitmap         = source.PressedBitmap;
		ShadowColor           = source.ShadowColor;
		ShadowOffset          = source.ShadowOffset;
		SoundButtonDown       = source.SoundButtonDown;
		SoundButtonOver       = source.SoundButtonOver;
		Tab                   = source.Tab;
		TextGradient          = source.TextGradient;
		TextOffset            = source.TextOffset;
		TextShadow            = source.TextShadow;
		Transparency          = source.Transparency;
		FontPreloaded         = source.FontPreloaded;
	}

	public int  GetTextWidth(string text) => text.Length * Math.Max(FontSize, 1) / 2;
	public int  GetTextHeight()           => Math.Max(FontSize, 1);
	public void PreloadFont()             => FontPreloaded = true;
}