using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
public class GuiControl : ScriptVariable, IGuiControl, IDisposable
{
	public new static readonly GuiControlProperties PropertiesInstance = [];
	public override            IScriptProperties    Properties => PropertiesInstance;

	protected readonly            string               Id;
	protected readonly            Script?              Script;
	private readonly              List<GuiAnimation>   _animations = [];
	private readonly              HashSet<int>         _mouseLocks = [];
	private                       int                  _areaClickPriority;
	private                       int                  _height;
	private                       bool                 _maximized;
	private                       string               _minExtent = "";
	private                       IGuiControl?         _parent;
	private                       object?              _profile;
	private                       GuiControlProfile?   _ownProfile;
	private                       string               _text = string.Empty;
	private                       bool                 _visible;
	private                       int                  _clientHeight;
	private                       int                  _clientWidth;
	private                       int                  _width;
	private                       int                  _x;
	private                       int                  _y;
	[ThreadStatic] private static HashSet<GuiControl>? _drawStack;
	protected event Action<string>?                    TextChanged;

	public GuiControl(string id, Script? script) : base(id)
	{
		Id          = id;
		Script      = script;
		OwnerScript = script;
		Script?.ScriptManager.RegisterGlobalObject(id.ToLowerInvariant(), this);
		Active         = true;
		CanMove        = false;
		CanResize      = false;
		ClipMove       = true;
		ClipChildren   = true;
		HorizSizing    = "right";
		VertSizing     = "bottom";
		MinExtent      = "8 8";
		Hint           = "";
		Cursor         = "";
		Color          = "1 1 1 1";
		Style          = "";
		RotationCenter = "";
		_x             = 0;
		_y             = 0;
		_width         = 64;
		_height        = 64;
		_clientWidth   = _width;
		_clientHeight  = _height;
		_visible       = true;
		Alpha          = 1;
		Red            = 1;
		Green          = 1;
		Blue           = 1;
	}

	public bool   AcceptDropFiles { get; set; }
	public bool   Active          { get; set; }
	public double Alpha           { get; set; }

	public int AreaClickPriority
	{
		get => _areaClickPriority;
		set => _areaClickPriority = Math.Clamp(value, 0, 2);
	}

	public bool   Awake       { get; set; }
	public bool   BitmapCache { get; set; }
	public double Blue        { get; set; }

	public string Bounds
	{
		get => $"{X} {Y} {Width} {Height}";
		set => SetBounds(value);
	}

	public bool CanMove     { get; set; }
	public bool CanClose    { get; set; }
	public bool CanMaximize { get; set; }
	public bool CanMinimize { get; set; }
	public bool CanResize   { get; set; }

	public int ClientHeight
	{
		get => _clientHeight;
		set => Resize(X, Y, Width, Height + value - _clientHeight);
	}

	public int ClientWidth
	{
		get => _clientWidth;
		set => Resize(X, Y, Width + value - _clientWidth, Height);
	}

	public bool ClipChildren { get; set; }
	public bool ClipMove     { get; set; }
	public bool ClipToBounds { get; set; } = true;

	public string Color
	{
		get => $"{Red.ToString(CultureInfo.InvariantCulture)} {Green.ToString(CultureInfo.InvariantCulture)} {Blue.ToString(CultureInfo.InvariantCulture)} {Alpha.ToString(CultureInfo.InvariantCulture)}";
		set => SetColor(value);
	}

	public string       Cursor          { get; set; }
	public bool         Editing         { get; set; }
	public bool         FastChildRender { get; set; }
	public IGuiControl? FirstResponder  { get; set; }

	public IGuiControl? Parent
	{
		get => _parent;
		set
		{
			if (CanUseParent(value))
			{
				var oldParent = _parent;
				_parent = value;
				if (!ReferenceEquals(oldParent, _parent))
					OnParentChanged(oldParent, _parent);
				if (_maximized)
					MaximizeToParent();
			}
		}
	}

	public bool                              Flickering         { get; set; }
	public double                            FlickerBaseTime    { get; set; }
	public double                            FlickerTime        { get; set; }
	public double                            Green              { get; set; }
	public string                            Hint               { get; set; }
	public double                            HintTime           { get; set; }
	public string                            HorizSizing        { get; set; }
	public bool                              IsDragging         { get; private set; }
	public bool                              IsExternal         { get; set; }
	public bool                              IsInAnimation      { get; set; }
	public bool                              IsInInOutAnimation { get; set; }
	public IReadOnlyCollection<GuiAnimation> Animations         => _animations;
	public string                            VertSizing         { get; set; }
	public int                               Layer              { get; set; }
	public bool                              LockMouseDown      { get; set; }

	public bool Maximized
	{
		get => _maximized;
		set
		{
			_maximized = value;
			if (_maximized)
				MaximizeToParent();
		}
	}

	public string MinExtent
	{
		get => _minExtent;
		set => _minExtent = value;
	}

	public string MinSize
	{
		get => MinExtent;
		set => MinExtent = value;
	}

	public int  Mode         { get; set; }
	public bool Modal        { get; set; }
	public bool NeedsRepaint { get; private set; }

	public object? Profile
	{
		get => _ownProfile ?? _profile;
		set
		{
			_profile = value;
			if (_ownProfile != null && ResolveProfile(value) is { } assignedProfile)
				_ownProfile.CopyFrom(assignedProfile);
		}
	}

	public GuiControlProfile? GetResolvedProfile() => _ownProfile ?? ResolveProfile(_profile);
	public double             Red                  { get; set; }
	public bool               ResizeWidth          { get; set; }
	public bool               ResizeHeight         { get; set; }
	public double             Rotation             { get; set; }
	public string             RotationCenter       { get; set; }
	public int                ScrollLineX          { get; set; }
	public int                ScrollLineY          { get; set; }
	public bool               ShowHint             { get; set; }
	public bool               AlwaysOnTop          { get; set; }
	public string             Style                { get; set; }

	public string Text
	{
		get => _text;
		set
		{
			value ??= string.Empty;
			if (string.Equals(_text, value, StringComparison.Ordinal)) return;
			_text = value;
			TextChanged?.Invoke(value);
		}
	}

	public bool UseOwnProfile
	{
		get => _ownProfile != null;
		set
		{
			if (value)
			{
				EnsureOwnProfile();
			}
			else
			{
				_ownProfile = null;
			}
		}
	}

	public bool Visible
	{
		get => _visible;
		set => SetVisible(value, true);
	}

	internal void SetVisible(bool value, bool stopAnimations)
	{
		if (_visible == value) return;

		var wasActuallyVisible = IsActuallyVisible();
		_visible = value;
		if (stopAnimations) StopInOutAnimations();
		var isActuallyVisible = IsActuallyVisible();
		if (wasActuallyVisible != isActuallyVisible)
		{
			ClearFirstResponders();
			NotifyVisible(isActuallyVisible);
		}
	}

	public int Width
	{
		get => _width;
		set => Resize(X, Y, value, Height);
	}

	public int Height
	{
		get => _height;
		set => Resize(X, Y, Width, value);
	}

	public int X
	{
		get => _x;
		set => Resize(value, Y, Width, Height);
	}

	public int Y
	{
		get => _y;
		set => Resize(X, value, Width, Height);
	}

	public string Extent
	{
		get => GetExtent();
		set => SetExtent(value);
	}

	public string ClientExtent
	{
		get => GetClientExtent();
		set => SetClientExtent(value);
	}

	public string Position
	{
		get => GetPosition();
		set => SetPosition(value);
	}

	public List<IGuiControl?> Controls { get; } = [];

	public void Dispose() => Active = false;

	public void SetSize(int width, int height) => Resize(X, Y, width, height);

	public void NotifyResize()
	{
		OnResize(Width, Height, Width, Height);
		InvokeEvent("onResize", Width, Height);
	}

	public void Destroy()
	{
		ClearFirstResponders();

		IGuiControl?[] controls;
		lock (Controls)
		{
			controls = Controls.ToArray();
			Controls.Clear();
		}

		foreach (var control in controls) control?.Destroy();

		Script?.ScriptManager.UnregisterGlobalObject(Id, this);
		if (Parent is GuiControl parent)
			parent.RemoveControl(this);

		Dispose();
	}

	public void AddControl(IGuiControl? obj)
	{
		if (obj == null) return;
		if (obj is GuiControl child)
		{
			if (!child.CanUseParent(this))
				return;

			if (child.Parent is GuiControl oldParent && !ReferenceEquals(oldParent, this))
				oldParent.RemoveControl(child);
		}

		obj.Parent = this;
		if (!ReferenceEquals(obj.Parent, this)) return;

		lock (Controls)
		{
			if (!Controls.Contains(obj))
				Controls.Add(obj);
		}

		if (Awake && obj is GuiControl guiControl)
			guiControl.Awaken();
	}

	public void RemoveControl(IGuiControl? obj)
	{
		if (obj == null) return;

		lock (Controls)
		{
			Controls.Remove(obj);
		}

		if (ReferenceEquals(obj.Parent, this))
			obj.Parent = null;
	}

	public void Awaken()
	{
		if (Awake) return;

		IGuiControl?[] controls;
		lock (Controls)
		{
			controls = Controls.ToArray();
		}

		foreach (var control in controls.OfType<GuiControl>())
			control.Awaken();

		Awake = true;
		OnWake();
		if (IsActuallyVisible())
			NotifyVisible(true);
	}

	public void BringToFront()
	{
		if (Parent is not GuiControl parent) return;
		lock (parent.Controls)
		{
			if (!parent.Controls.Remove(this)) return;
			parent.Controls.Add(this);
		}
	}

	public void ClearControls()
	{
		IGuiControl?[] controls;
		lock (Controls)
		{
			controls = Controls.ToArray();
			Controls.Clear();
		}

		foreach (var control in controls) control?.Destroy();
	}

	public GuiAnimation? CreateAnimation()
	{
		if (_animations.Count > 999) return null;

		var animation = new GuiAnimation(this);
		Show();
		_animations.Add(animation);
		IsInAnimation = true;
		Repaint();
		return animation;
	}

	public IGuiControl? FindControl(string position)
	{
		var parts = ParseParts(position, 2);
		if (parts.Length < 2) return null;

		return Controls.OfType<GuiControl>().LastOrDefault(control => control.Visible && control.Active && parts[0] >= control.X && parts[1] >= control.Y && parts[0] < control.X + control.Width && parts[1] < control.Y + control.Height);
	}

	public IGuiControl? GetParent() => Parent;

	public virtual string GlobalToLocalCoord(string position)
	{
		var (x, y)             = ParsePoint(position);
		var (globalX, globalY) = GetGlobalPosition();
		return FormatPoint(x - globalX, y - globalY);
	}

	public void Hide() => Visible = false;

	public bool IsActuallyVisible() => Visible && Active && (Parent is not GuiControl parent || parent.IsActuallyVisible());

	public bool IsOwnedBy(Script script) => ReferenceEquals(Script, script);

	public bool IsFirstResponder() => ReferenceEquals(Parent is GuiControl parent ? parent.FirstResponder : FirstResponder, this);

	public bool IsMouseLocked(int id) => _mouseLocks.Contains(id);

	public virtual string LocalToGlobalCoord(string position)
	{
		var (x, y)             = ParsePoint(position);
		var (globalX, globalY) = GetGlobalPosition();
		return FormatPoint(x + globalX, y + globalY);
	}

	public void MakeFirstResponder(bool firstResponder)
	{
		if (firstResponder)
		{
			SetFirstResponder(this);
			return;
		}

		ClearFirstResponder(this);
	}

	public void MouseLock(int id) => _mouseLocks.Add(id);

	public void MouseUnlock(int id) => _mouseLocks.Remove(id);

	public void MouseUnlockAll() => _mouseLocks.Clear();

	public void PushToBack()
	{
		if (Parent is not GuiControl parent) return;
		lock (parent.Controls)
		{
			if (!parent.Controls.Remove(this)) return;
			var controls = parent.Controls.ToArray();
			parent.Controls.Clear();
			parent.Controls.Add(this);
			foreach (var control in controls) parent.Controls.Add(control);
		}
	}

	public void Repaint()
	{
		NeedsRepaint = true;
	}

	public virtual void Resize(int x, int y, int width, int height)
	{
		var oldX            = _x;
		var oldY            = _y;
		var oldWidth        = _width;
		var oldHeight       = _height;
		var oldClientWidth  = _clientWidth;
		var oldClientHeight = _clientHeight;
		var (minWidth, minHeight) = GetMinimumExtent();

		_x                              = x;
		_y                              = y;
		_width                          = Math.Max(width, minWidth);
		_height                         = Math.Max(height, minHeight);
		var (clientWidth, clientHeight) = GetClientSizeForBounds(_width, _height);
		_clientWidth                    = Math.Max(clientWidth, 0);
		_clientHeight                   = Math.Max(clientHeight, 0);

		if (oldClientWidth != _clientWidth || oldClientHeight != _clientHeight)
			ResizeChildren(oldClientWidth, oldClientHeight, _clientWidth, _clientHeight);

		if (Parent is GuiControl parent)
			parent.OnChildResized(this);

		if (oldX != _x || oldY != _y)
		{
			InvokeEvent("onMove", _x, _y);
			OnMoved();
		}

		if (oldWidth != _width || oldHeight != _height)
		{
			OnResize(oldWidth, oldHeight, _width, _height);
			InvokeEvent("onResize", _width, _height);
		}
	}

	public void Show() => Visible = true;

	private void NotifyVisible(bool visible)
	{
		OnVisibilityChanged(visible);
		InvokeEvent(visible ? "onShow" : "onHide");

		IGuiControl?[] controls;
		lock (Controls)
		{
			controls = Controls.ToArray();
		}

		foreach (var control in controls.OfType<GuiControl>())
		{
			if (control.Awake && control.Visible)
				control.NotifyVisible(visible);
		}
	}

	private void EnsureOwnProfile()
	{
		if (_ownProfile != null) return;

		_ownProfile = new($"{Id}_profile");
		if (ResolveProfile(_profile) is { } assignedProfile)
			_ownProfile.CopyFrom(assignedProfile);
	}

	private GuiControlProfile? ResolveProfile(object? profileValue) =>
		profileValue switch
		{
			GuiControlProfile resolvedProfile => resolvedProfile,
			IStackEntry { } entry             => ResolveProfile(entry.GetValue()),
			TString profileName               => ResolveProfile(profileName.ToString()),
			string profileName                => ResolveProfile(profileName),
			_                                 => null
		};

	private GuiControlProfile? ResolveProfile(string profileName)
	{
		if (string.IsNullOrWhiteSpace(profileName) || Script?.ScriptManager.GlobalVariables.TryGetVariable(profileName.ToLowerInvariant(), out var entry) != true)
		{
			return null;
		}

		return ResolveProfile(entry);
	}

	public void ShowTop()
	{
		Show();
		BringToFront();
		FindFirstTabable()?.MakeFirstResponder(true);
	}

	public void ShowAlwaysTop()
	{
		AlwaysOnTop = true;
		ShowTop();
	}

	public void SortControls()
	{
		lock (Controls)
		{
			if (Controls.Count < 2) return;

			var controls = Controls.OrderBy(control => control is GuiControl guiControl ? guiControl.Y : 0).ThenBy(control => control is GuiControl guiControl ? guiControl.X : 0).ToArray();

			Controls.Clear();
			foreach (var control in controls)
				Controls.Add(control);
		}
	}

	public void StartDrag()
	{
		IsDragging = true;
	}

	public void StopAnimations()
	{
		foreach (var animation in _animations) animation.RestoreAlpha();
		_animations.Clear();
		IsInAnimation      = false;
		IsInInOutAnimation = false;
	}

	public void AdvanceAnimations(double elapsedSeconds)
	{
		string? finishedTransition = null;
		foreach (var animation in _animations.ToArray())
		{
			if (animation.Advance(elapsedSeconds)) continue;
			_animations.Remove(animation);
			animation.Complete();
			finishedTransition = animation.Transition;
		}

		IsInAnimation      = _animations.Count > 0;
		IsInInOutAnimation = _animations.Any(animation => !string.IsNullOrEmpty(animation.Transition));
		if (finishedTransition != null && !IsInAnimation)
			InvokeEvent("onAnimationFinished", finishedTransition);

		GuiControl[] children;
		lock (Controls)
			children = Controls.OfType<GuiControl>().ToArray();
		foreach (var child in children)
			child.AdvanceAnimations(elapsedSeconds);
	}

	public void StopInOutAnimations()
	{
		foreach (var animation in _animations.Where(animation => !string.IsNullOrEmpty(animation.Transition)))
			animation.RestoreAlpha();
		_animations.RemoveAll(animation => !string.IsNullOrEmpty(animation.Transition));
		IsInInOutAnimation = false;
		if (_animations.Count == 0)
			IsInAnimation = false;
	}

	public IGuiControl? TabFirst()
	{
		var first = FindFirstTabable();
		if (first != null) SetFirstResponder(first);
		return first;
	}

	private GuiControl? FindFirstTabable()
	{
		if (!Active || !Visible) return null;

		GuiControl[] controls;
		lock (Controls)
		{
			controls = Controls.OfType<GuiControl>().ToArray();
		}

		foreach (var control in controls)
		{
			if (control.FindFirstTabable() is { } tabable)
				return tabable;
		}

		return GetResolvedProfile()?.Tab == true ? this : null;
	}

	private void SetFirstResponder(IGuiControl? control)
	{
		FirstResponder = control;
		if (Parent is GuiControl parent)
			parent.SetFirstResponder(control);
	}

	private void ClearFirstResponder(IGuiControl control)
	{
		if (ReferenceEquals(FirstResponder, control))
			FirstResponder = null;

		if (Parent is GuiControl parent)
			parent.ClearFirstResponder(control);
	}

	private void ClearFirstResponders()
	{
		ClearFirstResponder(this);

		GuiControl[] children;
		lock (Controls)
			children = Controls.OfType<GuiControl>().ToArray();

		foreach (var child in children)
			child.ClearFirstResponders();
	}

	// ReSharper disable once UnusedMember.Global
	protected void CallAction() => InvokeEvent("onAction");

	protected void InvokeEvent(string eventName, params object[] args)
	{
		Script?.Call($"{Id}.{eventName}", args).ConfigureAwait(false).GetAwaiter().GetResult();
	}

	public void InstallEventCatchers(Script sourceScript) => Script?.InstallObjectEventCatchers(Id, sourceScript);

	internal void RemoveEventCatchersFrom(Script sourceScript) => Script?.RemoveEventCatchersFrom(sourceScript);

	public virtual void Draw()
	{
		NeedsRepaint = false;
		DrawChildControls();
	}

	protected void DrawChildControls()
	{
		var drawStack = _drawStack ??= new(ReferenceEqualityComparer.Instance);
		if (!drawStack.Add(this)) return;
		try
		{
			IGuiControl?[] controls;
			lock (Controls)
			{
				controls = Controls.ToArray();
			}

			foreach (var control in controls)
			{
				if (control == null) continue;
				if (control is GuiControl guiControl)
				{
					if (drawStack.Contains(guiControl)) continue;
					if (guiControl.Visible)
						guiControl.Draw();
				}
				else
				{
					control.Draw();
				}
			}
		}
		finally
		{
			drawStack.Remove(this);
			if (drawStack.Count == 0)
				_drawStack = null;
		}
	}

	protected void ResizeChildren(int oldWidth, int oldHeight, int newWidth, int newHeight)
	{
		if (oldWidth == newWidth && oldHeight == newHeight) return;
		GuiControl[] controls;
		lock (Controls)
		{
			controls = Controls.OfType<GuiControl>().ToArray();
		}

		foreach (var control in controls)
			control.OnParentResized(oldWidth, oldHeight, newWidth, newHeight);
	}

	protected virtual void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
	{
	}

	protected virtual void OnWake() => InvokeEvent("onWake");

	protected virtual void OnVisibilityChanged(bool visible)
	{
	}

	protected virtual (int Width, int Height) GetClientSizeForBounds(int width, int height) => (width, height);

	protected int ClientAreaWidth => _clientWidth;

	protected int ClientAreaHeight => _clientHeight;

	protected void SetClientAreaSize(int width, int height)
	{
		width  = Math.Max(width, 0);
		height = Math.Max(height, 0);
		if (_clientWidth == width && _clientHeight == height) return;

		var oldClientWidth  = _clientWidth;
		var oldClientHeight = _clientHeight;
		_clientWidth  = width;
		_clientHeight = height;
		ResizeChildren(oldClientWidth, oldClientHeight, _clientWidth, _clientHeight);
	}

	protected virtual void OnChildResized(GuiControl control)
	{
	}

	protected virtual void OnParentChanged(IGuiControl? oldParent, IGuiControl? newParent)
	{
	}

	protected virtual void OnMoved()
	{
		GuiControl[] controls;
		lock (Controls)
		{
			controls = Controls.OfType<GuiControl>().ToArray();
		}

		foreach (var control in controls)
			control.OnMoved();
	}

	protected virtual void OnParentResized(int oldParentWidth, int oldParentHeight, int newParentWidth, int newParentHeight)
	{
		if (Maximized)
		{
			Resize(0, 0, newParentWidth, newParentHeight);
			return;
		}

		var newX      = X;
		var newY      = Y;
		var newWidth  = Width;
		var newHeight = Height;
		var deltaX    = newParentWidth - oldParentWidth;
		var deltaY    = newParentHeight - oldParentHeight;

		ApplySizing(
			HorizSizing,
			oldParentWidth,
			newParentWidth,
			deltaX,
			X,
			Width,
			ref newX,
			ref newWidth
		);
		ApplySizing(
			VertSizing,
			oldParentHeight,
			newParentHeight,
			deltaY,
			Y,
			Height,
			ref newY,
			ref newHeight
		);

		if (newX != X || newY != Y || newWidth != Width || newHeight != Height)
			Resize(newX, newY, newWidth, newHeight);
	}

	private static void ApplySizing(
		string sizing,
		int oldParentExtent,
		int newParentExtent,
		int delta,
		int position,
		int extent,
		ref int newPosition,
		ref int newExtent
	)
	{
		switch (sizing)
		{
			case "width":
			case "height":
				newExtent += delta;
				break;
			case "left":
			case "top":
				newPosition += delta;
				break;
			case "center":
				newPosition = (newParentExtent - extent) / 2;
				break;
			case "relative" when oldParentExtent != 0:
				newPosition = newParentExtent * position / oldParentExtent;
				newExtent   = newParentExtent * (position + extent) / oldParentExtent - newPosition;
				break;
		}
	}

	protected void SetClientExtent(object? posVar)
	{
		var width  = ClientWidth;
		var height = ClientHeight;
		switch (posVar)
		{
			case List<object> var:
				width  = ToInt(var[0]);
				height = ToInt(var[1]);
				break;
			case TString posVarString:
			{
				string? positionString = posVarString;
				if (positionString?.Length <= 0 || positionString == null) return;
				var p = TokenizeParts(positionString);

				if (TryParseDouble(p.ElementAtOrDefault(0), out var p0)) width  = (int)p0;
				if (TryParseDouble(p.ElementAtOrDefault(1), out var p1)) height = (int)p1;
				break;
			}
		}

		Resize(X, Y, Width + width - ClientWidth, Height + height - ClientHeight);
	}

	private string GetClientExtent() => $"{ClientWidth} {ClientHeight}";

	protected void SetExtent(object? posVar)
	{
		var width  = Width;
		var height = Height;
		switch (posVar)
		{
			case List<object> var:
				width  = ToInt(var[0]);
				height = ToInt(var[1]);
				break;
			case string posVarString:
			{
				if (posVarString.Length <= 0) return;
				var p = TokenizeParts(posVarString);

				if (TryParseDouble(p.ElementAtOrDefault(0), out var p0)) width  = (int)p0;
				if (TryParseDouble(p.ElementAtOrDefault(1), out var p1)) height = (int)p1;
				break;
			}
			case TString posVarString:
			{
				var positionString = posVarString.ToString();
				if (positionString.Length <= 0) return;
				var p = TokenizeParts(positionString);

				if (TryParseDouble(p.ElementAtOrDefault(0), out var p0)) width  = (int)p0;
				if (TryParseDouble(p.ElementAtOrDefault(1), out var p1)) height = (int)p1;
				break;
			}
		}

		Resize(X, Y, width, height);
	}

	private string GetExtent() => $"{Width} {Height}";

	private string GetPosition() => $"{X} {Y}";

	private void SetPosition(object? posVar)
	{
		var x = X;
		var y = Y;
		switch (posVar)
		{
			case List<object> var:
				x = ToInt(var[0]);
				y = ToInt(var[1]);
				break;
			case TString posVarString:
			{
				string? positionString = posVarString;
				if (positionString?.Length <= 0 || positionString == null) return;
				var p = TokenizeParts(positionString);

				if (TryParseDouble(p.ElementAtOrDefault(0), out var p0)) x = (int)p0;
				if (TryParseDouble(p.ElementAtOrDefault(1), out var p1)) y = (int)p1;
				break;
			}
			case string posVarString:
			{
				if (posVarString.Length <= 0) return;
				var p = TokenizeParts(posVarString);

				if (TryParseDouble(p.ElementAtOrDefault(0), out var p0)) x = (int)p0;
				if (TryParseDouble(p.ElementAtOrDefault(1), out var p1)) y = (int)p1;
				break;
			}
		}

		Resize(x, y, Width, Height);
	}

	private void SetBounds(string value)
	{
		var parts = ParseParts(value, 4);
		if (parts.Length < 4) return;

		Resize((int)parts[0], (int)parts[1], (int)parts[2], (int)parts[3]);
	}

	protected (int Width, int Height) GetMinimumExtent()
	{
		var parts = ParseParts(MinExtent, 2);
		if (parts.Length < 2) return (0, 0);
		return ((int)parts[0], (int)parts[1]);
	}

	private void SetColor(string value)
	{
		var parts = ParseParts(value, 4);
		if (parts.Length < 3) return;

		Red   = parts[0];
		Green = parts[1];
		Blue  = parts[2];
		if (parts.Length > 3) Alpha = parts[3];
	}

	private static double[] ParseParts(string value, int maxParts) => value.TokenizeForScript(" ,").Take(maxParts).Select(part => TryParseDouble(part, out var parsed) ? parsed : 0).ToArray();

	private static string[] TokenizeParts(string value) => value.TokenizeForScript(" ,").ToArray();

	private static (double X, double Y) ParsePoint(string value)
	{
		var parts = ParseParts(value, 2);
		return parts.Length >= 2 ? (parts[0], parts[1]) : (0, 0);
	}

	private (double X, double Y) GetGlobalPosition()
	{
		double              x       = X;
		double              y       = Y;
		HashSet<GuiControl> visited = new(ReferenceEqualityComparer.Instance) { this };
		var                 parent  = Parent as GuiControl;
		while (parent != null && visited.Add(parent))
		{
			x      += parent.X;
			y      += parent.Y;
			parent =  parent.Parent as GuiControl;
		}

		return (x, y);
	}

	private bool CanUseParent(IGuiControl? parent)
	{
		if (parent == null) return true;
		if (ReferenceEquals(parent, this)) return false;

		var current = parent as GuiControl;
		while (current != null)
		{
			if (ReferenceEquals(current, this)) return false;
			current = current.Parent as GuiControl;
		}

		return true;
	}

	private static string FormatPoint(double x, double y) => $"{FormatFloat(x)},{FormatFloat(y)}";

	private void MaximizeToParent()
	{
		if (Parent is not GuiControl parent) return;
		Resize(0, 0, parent.Width, parent.Height);
	}

	private static string FormatFloat(double value) => value == 0 ? "0" : value.ToString("G", CultureInfo.InvariantCulture);

	private static int ToInt(object? value)
	{
		if (value is IStackEntry entry) value = entry.GetValue();
		return value switch
		{
			null                                                              => -1,
			double d                                                          => (int)d,
			float f                                                           => (int)f,
			int i                                                             => i,
			TString text when TryParseDouble(text.ToString(), out var parsed) => (int)parsed,
			string text when TryParseDouble(text, out var parsed)             => (int)parsed,
			IConvertible convertible                                          => Convert.ToInt32(convertible, CultureInfo.InvariantCulture),
			_                                                                 => -1
		};
	}

	private static bool TryParseDouble(string? value, out double parsed) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
}