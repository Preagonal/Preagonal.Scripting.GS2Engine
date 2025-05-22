using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Preagonal.Scripting.GS2Engine;

public delegate void DebugFunc(string? args);

public static class Tools
{
	#region Public Methods

	#region IsNumericType

	/// <summary>
	///     Determines whether the specified value is of numeric type.
	/// </summary>
	/// <param name="o">The object to check.</param>
	/// <returns>
	///     <c>true</c> if o is a numeric type; otherwise, <c>false</c>.
	/// </returns>
	public static bool IsNumericType(object? o) =>
		o is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

	#endregion

	#region IsPositive

	/// <summary>
	///     Determines whether the specified value is positive.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="zeroIsPositive">if set to <c>true</c> treats 0 as positive.</param>
	/// <returns>
	///     <c>true</c> if the specified value is positive; otherwise, <c>false</c>.
	/// </returns>
	public static bool IsPositive(object? value, bool zeroIsPositive) =>
		value != null &&
		Type.GetTypeCode(value.GetType()) switch
		{
			TypeCode.SByte   => zeroIsPositive ? (sbyte)value >= 0 : (sbyte)value > 0,
			TypeCode.Int16   => zeroIsPositive ? (short)value >= 0 : (short)value > 0,
			TypeCode.Int32   => zeroIsPositive ? (int)value >= 0 : (int)value > 0,
			TypeCode.Int64   => zeroIsPositive ? (long)value >= 0 : (long)value > 0,
			TypeCode.Single  => zeroIsPositive ? (float)value >= 0 : (float)value > 0,
			TypeCode.Double  => zeroIsPositive ? (double)value >= 0 : (double)value > 0,
			TypeCode.Decimal => zeroIsPositive ? (decimal)value >= 0 : (decimal)value > 0,
			TypeCode.Byte    => zeroIsPositive || (byte)value > 0,
			TypeCode.UInt16  => zeroIsPositive || (ushort)value > 0,
			TypeCode.UInt32  => zeroIsPositive || (uint)value > 0,
			TypeCode.UInt64  => zeroIsPositive || (ulong)value > 0,
			TypeCode.Char    => zeroIsPositive || (char)value != '\0',
			_                => false,
		};

	#endregion

	#region ToUnsigned

	/// <summary>
	///     Converts the specified values boxed type to its correpsonding unsigned
	///     type.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A boxed numeric object whos type is unsigned.</returns>
	public static object? ToUnsigned(object? value)
	{
		if (value != null)
			return Type.GetTypeCode(value.GetType()) switch
			{
				TypeCode.SByte   => (sbyte)value,
				TypeCode.Int16   => (short)value,
				TypeCode.Int32   => (int)value,
				TypeCode.Int64   => (long)value,
				TypeCode.Byte    => value,
				TypeCode.UInt16  => value,
				TypeCode.UInt32  => value,
				TypeCode.UInt64  => value,
				TypeCode.Single  => (float)value,
				TypeCode.Double  => (double)value,
				TypeCode.Decimal => (decimal)value,
				TypeCode.Empty   => null,
				_                => null,
			};
		return null;
	}

	#endregion

	#region ToInteger

	#endregion

	#region UnboxToLong

	public static long UnboxToLong(object? value, bool round)
	{
		if (value != null)
			return Type.GetTypeCode(value.GetType()) switch
			{
				TypeCode.SByte   => (sbyte)value,
				TypeCode.Int16   => (short)value,
				TypeCode.Int32   => (int)value,
				TypeCode.Int64   => (long)value,
				TypeCode.Byte    => (byte)value,
				TypeCode.UInt16  => (ushort)value,
				TypeCode.UInt32  => (uint)value,
				TypeCode.UInt64  => (long)(ulong)value,
				TypeCode.Single  => round ? (long)Math.Round((float)value) : (long)(float)value,
				TypeCode.Double  => round ? (long)Math.Round((double)value) : (long)(double)value,
				TypeCode.Decimal => round ? (long)Math.Round((decimal)value) : (long)(decimal)value,
				_                => 0,
			};
		return 0;
	}

	#endregion

	#region ReplaceMetaChars

	/// <summary>
	///     Replaces the string representations of meta chars with their corresponding
	///     character values.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <returns>A string with all string meta chars are replaced</returns>
	public static string ReplaceMetaChars(string input) =>
		Regex.Replace(input, @"(\\)(\d{3}|[^\d])?", ReplaceMetaCharsMatch);

	private static string ReplaceMetaCharsMatch(Match m)
	{
		// convert octal quotes (like \040)
		if (m.Groups[2].Length == 3)
			return Convert.ToChar(Convert.ToByte(m.Groups[2].Value, 8)).ToString();
		// convert all other special meta characters
		//TODO: \xhhh hex and possible dec !!
		return m.Groups[2].Value switch
		{
			"0" => // null
				"\0",
			"a" => // alert (beep)
				"\a",
			"b" => // BS
				"\b",
			"f" => // FF
				"\f",
			"v" => // vertical tab
				"\v",
			"r" => // CR
				"\r",
			"n" => // LF
				"\n",
			"t" => // Tab
				"\t",
			_ => m.Groups[2].Value,
		};
	}

	#endregion

	// ReSharper disable once InconsistentNaming
	// ReSharper disable once MemberCanBePrivate.Global
	public static bool DEBUG_ON { get; set; } = false;

	private static DebugFunc? DebugFuncWrite;
	private static DebugFunc? DebugFuncWriteLine;

	public static void SetDebugFuncWrite(DebugFunc debugFunc) => DebugFuncWrite = debugFunc;
	public static void SetDebugFuncWriteLine(DebugFunc debugFunc) => DebugFuncWriteLine = debugFunc;

	public static void Debug(string? text)
	{
		if (!DEBUG_ON) return;

		if (DebugFuncWrite != null)
			DebugFuncWrite(text);
		else
			Console.Write(text);
	}

	public static void DebugLine(string? text)
	{
		if (!DEBUG_ON) return;

		if (DebugFuncWriteLine != null)
			DebugFuncWriteLine(text);
		else
			Console.WriteLine(text);
	}

	public static string Format(string? format, params object?[] parameters)
	{
		#region Variables

		StringBuilder f = new();
		Regex         r = new(@"\%(\d*\$)?([\'\#\-\+ ]*)(\d*)(?:\.(\d+))?([hl])?([dioxXucsfeEgGpn%])");
		//"%[parameter][flags][width][.precision][length]type"
		Match?  m              = null;
		var     w              = string.Empty;
		var     defaultParamIx = 0;
		int     paramIx;
		object? o = null;

		var flagLeft2Right     = false;
		var flagAlternate      = false;
		var flagPositiveSign   = false;
		var flagPositiveSpace  = false;
		var flagZeroPadding    = false;
		var flagGroupThousands = false;

		var fieldLength        = 0;
		var fieldPrecision     = 0;
		var shortLongIndicator = '\0';
		var formatSpecifier    = '\0';
		var paddingCharacter   = ' ';

		#endregion

		// find all format parameters in format string
		f.Append(format);
		m = r.Match(f.ToString());
		while (m.Success)
		{
			#region parameter index

			paramIx = defaultParamIx;
			if (m.Groups[1] != null && m.Groups[1].Value.Length > 0)
			{
				var val = m.Groups[1].Value.Substring(0, m.Groups[1].Value.Length - 1);
				paramIx = Convert.ToInt32(val) - 1;
			}

			;

			#endregion

			#region format flags

			// extract format flags
			flagAlternate      = false;
			flagLeft2Right     = false;
			flagPositiveSign   = false;
			flagPositiveSpace  = false;
			flagZeroPadding    = false;
			flagGroupThousands = false;
			if (m.Groups[2] != null && m.Groups[2].Value.Length > 0)
			{
				var flags = m.Groups[2].Value;

				flagAlternate      = flags.IndexOf('#') >= 0;
				flagLeft2Right     = flags.IndexOf('-') >= 0;
				flagPositiveSign   = flags.IndexOf('+') >= 0;
				flagPositiveSpace  = flags.IndexOf(' ') >= 0;
				flagGroupThousands = flags.IndexOf('\'') >= 0;

				// positive + indicator overrides a
				// positive space character
				if (flagPositiveSign && flagPositiveSpace)
					flagPositiveSpace = false;
			}

			#endregion

			#region field length

			// extract field length and
			// pading character
			paddingCharacter = ' ';
			fieldLength      = int.MinValue;
			if (m.Groups[3] != null && m.Groups[3].Value.Length > 0)
			{
				fieldLength     = Convert.ToInt32(m.Groups[3].Value);
				flagZeroPadding = m.Groups[3].Value[0] == '0';
			}

			#endregion

			if (flagZeroPadding)
				paddingCharacter = '0';

			// left2right allignment overrides zero padding
			if (flagLeft2Right && flagZeroPadding)
			{
				flagZeroPadding  = false;
				paddingCharacter = ' ';
			}

			#region field precision

			// extract field precision
			fieldPrecision = int.MinValue;
			if (m.Groups[4] != null && m.Groups[4].Value.Length > 0)
				fieldPrecision = Convert.ToInt32(m.Groups[4].Value);

			#endregion

			#region short / long indicator

			// extract short / long indicator
			shortLongIndicator = char.MinValue;
			if (m.Groups[5] != null && m.Groups[5].Value.Length > 0)
				shortLongIndicator = m.Groups[5].Value[0];

			#endregion

			#region format specifier

			// extract format
			formatSpecifier = char.MinValue;
			if (m.Groups[6] != null && m.Groups[6].Value.Length > 0)
				formatSpecifier = m.Groups[6].Value[0];

			#endregion

			// default precision is 6 digits if none is specified except
			if (fieldPrecision == int.MinValue &&
			    formatSpecifier != 's' &&
			    formatSpecifier != 'c' &&
			    char.ToUpper(formatSpecifier) != 'X' &&
			    formatSpecifier != 'o')
				fieldPrecision = 6;

			#region get next value parameter

			// get next value parameter and convert value parameter depending on short / long indicator
			if (parameters == null || paramIx >= parameters.Length)
			{
				o = null;
			}
			else
			{
				o = parameters[paramIx];

				if (shortLongIndicator == 'h')
				{
					if (o is int)
						o = (short)(int)o;
					else if (o is long)
						o = (short)(long)o;
					else if (o is uint)
						o = (ushort)(uint)o;
					else if (o is ulong)
						o = (ushort)(ulong)o;
				}
				else if (shortLongIndicator == 'l')
				{
					if (o is short)
						o = (long)(short)o;
					else if (o is int)
						o = (long)(int)o;
					else if (o is ushort)
						o = (ulong)(ushort)o;
					else if (o is uint)
						o = (ulong)(uint)o;
				}
			}

			#endregion

			// convert value parameters to a string depending on the formatSpecifier
			w = string.Empty;
			switch (formatSpecifier)
			{
				#region % - character

				case '%': // % character
					w = "%";
					break;

				#endregion

				#region d - integer

				case 'd': // integer
					w = FormatNumber(
						flagGroupThousands ? "n" : "d",
						flagAlternate,
						fieldLength,
						int.MinValue,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region i - integer

				case 'i': // integer
					goto case 'd';

				#endregion

				#region o - octal integer

				case 'o': // octal integer - no leading zero
					w = FormatOct(
						"o",
						flagAlternate,
						fieldLength,
						int.MinValue,
						flagLeft2Right,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region x - hex integer

				case 'x': // hex integer - no leading zero
					w = FormatHex(
						"x",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region X - hex integer

				case 'X': // same as x but with capital hex characters
					w = FormatHex(
						"X",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region u - unsigned integer

				case 'u': // unsigned integer
					w = FormatNumber(
						flagGroupThousands ? "n" : "d",
						flagAlternate,
						fieldLength,
						int.MinValue,
						flagLeft2Right,
						false,
						false,
						paddingCharacter,
						ToUnsigned(o)
					);
					defaultParamIx++;
					break;

				#endregion

				#region c - character

				case 'c': // character
					if (IsNumericType(o))
						w = Convert.ToChar(o).ToString();
					else if (o is char)
						w = ((char)o).ToString();
					else if (o is string && ((string)o).Length > 0)
						w = ((string)o)[0].ToString();
					defaultParamIx++;
					break;

				#endregion

				#region s - string

				case 's': // string
					var t = "{0" +
					        (fieldLength != int.MinValue
						        ? "," + (flagLeft2Right ? "-" : string.Empty) + fieldLength
						        : string.Empty) +
					        ":s}";
					w = o?.ToString();
					if (fieldPrecision >= 0)
						w = w?.Substring(0, fieldPrecision);

					if (fieldLength != int.MinValue)
						if (flagLeft2Right)
							w = w?.PadRight(fieldLength, paddingCharacter);
						else
							w = w?.PadLeft(fieldLength, paddingCharacter);
					defaultParamIx++;
					break;

				#endregion

				#region f - double number

				case 'f': // double
					w = FormatNumber(
						flagGroupThousands ? "n" : "f",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region e - exponent number

				case 'e': // double / exponent
					w = FormatNumber(
						"e",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region E - exponent number

				case 'E': // double / exponent
					w = FormatNumber(
						"E",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region g - general number

				case 'g': // double / exponent
					w = FormatNumber(
						"g",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region G - general number

				case 'G': // double / exponent
					w = FormatNumber(
						"G",
						flagAlternate,
						fieldLength,
						fieldPrecision,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						o
					);
					defaultParamIx++;
					break;

				#endregion

				#region p - pointer

				case 'p': // pointer
					if (o is nint)
						w = "0x" + ((nint)o).ToString("x");
					defaultParamIx++;
					break;

				#endregion

				#region n - number of processed chars so far

				case 'n': // number of characters so far
					w = FormatNumber(
						"d",
						flagAlternate,
						fieldLength,
						int.MinValue,
						flagLeft2Right,
						flagPositiveSign,
						flagPositiveSpace,
						paddingCharacter,
						m.Index
					);
					break;

				#endregion

				default:
					w = string.Empty;
					defaultParamIx++;
					break;
			}

			// replace format parameter with parameter value
			// and start searching for the next format parameter
			// AFTER the position of the current inserted value
			// to prohibit recursive matches if the value also
			// includes a format specifier
			f.Remove(m.Index, m.Length);
			f.Insert(m.Index, w);
			if (w != null) m = r.Match(f.ToString(), m.Index + w.Length);
		}

		return f.ToString();
	}

	#endregion

	#region Private Methods

	#region FormatOCT

	private static string FormatOct(
		string nativeFormat,
		bool alternate,
		int fieldLength,
		int fieldPrecision,
		bool left2Right,
		char padding,
		object? value
	)
	{
		var w = string.Empty;
		var lengthFormat = "{0" +
		                   (fieldLength != int.MinValue
			                   ? "," + (left2Right ? "-" : string.Empty) + fieldLength
			                   : string.Empty) +
		                   "}";

		// ReSharper disable once InvertIf
		if (IsNumericType(value))
		{
			w = Convert.ToString(UnboxToLong(value, true), 8);

			if (left2Right || padding == ' ')
			{
				if (alternate && w != "0")
					w = "0" + w;
				w = string.Format(lengthFormat, w);
			}
			else
			{
				if (fieldLength != int.MinValue)
					w = w.PadLeft(fieldLength - (alternate && w != "0" ? 1 : 0), padding);
				if (alternate && w != "0")
					w = "0" + w;
			}
		}

		return w;
	}

	#endregion

	#region FormatHEX

	private static string FormatHex(
		string nativeFormat,
		bool alternate,
		int fieldLength,
		int fieldPrecision,
		bool left2Right,
		char padding,
		object? value
	)
	{
		var w = string.Empty;
		var lengthFormat = "{0" +
		                   (fieldLength != int.MinValue
			                   ? "," + (left2Right ? "-" : string.Empty) + fieldLength
			                   : string.Empty) +
		                   "}";
		var numberFormat = "{0:" +
		                   nativeFormat +
		                   (fieldPrecision != int.MinValue ? fieldPrecision.ToString() : string.Empty) +
		                   "}";

		if (IsNumericType(value))
		{
			w = string.Format(numberFormat, value);

			if (left2Right || padding == ' ')
			{
				if (alternate)
					w = (nativeFormat == "x" ? "0x" : "0X") + w;
				w = string.Format(lengthFormat, w);
			}
			else
			{
				if (fieldLength != int.MinValue)
					w = w.PadLeft(fieldLength - (alternate ? 2 : 0), padding);
				if (alternate)
					w = (nativeFormat == "x" ? "0x" : "0X") + w;
			}
		}

		return w;
	}

	#endregion

	#region FormatNumber

	private static string FormatNumber(
		string nativeFormat,
		bool alternate,
		int fieldLength,
		int fieldPrecision,
		bool left2Right,
		bool positiveSign,
		bool positiveSpace,
		char padding,
		object? value
	)
	{
		var w = string.Empty;
		var lengthFormat = "{0" +
		                   (fieldLength != int.MinValue
			                   ? "," + (left2Right ? "-" : string.Empty) + fieldLength
			                   : string.Empty) +
		                   "}";
		var numberFormat = "{0:" +
		                   nativeFormat +
		                   (fieldPrecision != int.MinValue ? fieldPrecision.ToString() : "0") +
		                   "}";


		if (IsNumericType(value))
		{
			//w = String.Format(numberFormat, Value);

			// fix by st14
			if (numberFormat.Contains("d") && value is double)
			{
				var num = (double)value;
				w = string.Format(numberFormat, (int)num);
			}
			else
			{
				w = string.Format(numberFormat, value);
			}

			if (left2Right || padding == ' ')
			{
				if (IsPositive(value, true))
					w = (positiveSign ? "+" : positiveSpace ? " " : string.Empty) + w;
				w = string.Format(lengthFormat, w);
			}
			else
			{
				if (w.StartsWith("-"))
					w = w.Substring(1);


#if zero
					if (FieldLength != int.MinValue)
						w = w.PadLeft(FieldLength - 1, Padding);
					if (IsPositive(Value, true))
						w = (PositiveSign ?
								"+" : (PositiveSpace ?
										" " : (FieldLength != int.MinValue ?
												Padding.ToString() : String.Empty))) + w;
					else
						w = "-" + w;
#endif

				// fix by st14 to take into account the size of the length of the value object
				if (fieldLength != int.MinValue && w.Length < fieldLength - 1)
					w = w.PadLeft(fieldLength - 1, padding);

				if (IsPositive(value, true))
					w = (positiveSign                                             ? "+" :
						    positiveSpace                                         ? " " :
						    fieldLength != int.MinValue && w.Length < fieldLength ? padding.ToString() :
							    string.Empty) +
					    w;
				else
					w = "-" + w;
			}
		}

		return w;
	}

	#endregion

	#endregion
}