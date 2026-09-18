using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiAnimationProperties : ScriptProperties<GuiAnimation>
{
	public GuiAnimationProperties() : base(typeof(ScriptVariable))
	{
		var properties = new PropertyDefinitions<GuiAnimation>
		{
			{ "currenttime", "The animation's current playback time.", animation => animation.CurrentTime, (animation, value) => animation.CurrentTime = value },
			{ "alpha", "The alpha value applied by the animation.", animation => animation.Alpha, (animation, value) => animation.Alpha = value },
			{ "amplitude", "The animation's movement amplitude.", animation => animation.Amplitude, (animation, value) => animation.Amplitude = value },
			{ "bounds", "The bounds used by the animation.", animation => animation.Bounds, (animation, value) => animation.Bounds = value },
			{ "delay", "The delay before the animation starts.", animation => animation.Delay, (animation, value) => animation.Delay = value },
			{ "duration", "The animation's duration.", animation => animation.Duration, (animation, value) => animation.Duration = value },
			{ "interval", "The animation's update interval.", animation => animation.Interval, (animation, value) => animation.Interval = value },
			{ "rotation", "The rotation applied by the animation.", animation => animation.Rotation, (animation, value) => animation.Rotation = value },
			{ "sound", "The sound played by the animation.", animation => animation.Sound, (animation, value) => animation.Sound = value },
			{ "tabfirstonshow", "Whether the first control receives focus when shown.", animation => animation.TabFirstOnShow, (animation, value) => animation.TabFirstOnShow = value },
			{ "timing", "The timing mode used by the animation.", animation => animation.Timing, (animation, value) => animation.Timing = value },
			{ "transition", "The transition mode used by the animation.", animation => animation.Transition, (animation, value) => animation.Transition = value }
		};

		AddProperties(this, properties);
		Compile();
	}
}