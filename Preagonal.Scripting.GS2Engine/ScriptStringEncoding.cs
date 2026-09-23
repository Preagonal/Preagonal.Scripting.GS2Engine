using System;
using System.Security.Cryptography;

namespace Preagonal.Scripting.GS2Engine;

internal static class ScriptStringEncoding
{
	public static bool IsUtf8(TString value)
	{
		var bytes     = value.toByteArray();
		var multibyte = false;
		for (var index = 0; index < bytes.Length;)
		{
			var first = bytes[index++];
			if (first < 0x80) continue;
			var length = first < 0xc0 ? 0 : first < 0xe0 ? 2 : first < 0xf0 ? 3 : first < 0xf8 ? 4 : first < 0xfc ? 5 : first < 0xfe ? 6 : 0;
			if (length == 0 || bytes.Length - index < length - 1) return false;
			for (var part = 1; part < length; part++)
				if ((bytes[index++] & 0xc0) != 0x80)
					return false;
			multibyte = true;
		}

		return multibyte;
	}

	public static string EncodeBase64(TString value) => Convert.ToBase64String(value.toByteArray());

	public static TString DecodeBase64(TString value)
	{
		var input  = value.toByteArray();
		var output = new byte[input.Length / 4 * 3 + 2];
		var count  = 0;
		var bits   = 0;
		var buffer = 0;
		foreach (var character in input)
		{
			var digit = character switch
			{
				>= (byte)'A' and <= (byte)'Z' => character - 'A',
				>= (byte)'a' and <= (byte)'z' => character - 'a' + 26,
				>= (byte)'0' and <= (byte)'9' => character - '0' + 52,
				(byte)'+'                     => 62,
				(byte)'/'                     => 63,
				_                             => -1,
			};
			if (digit < 0) break;
			buffer =  (buffer << 6) | digit;
			bits   += 6;
			if (bits < 8) continue;
			bits            -= 8;
			output[count++] =  (byte)(buffer >> bits);
			buffer          &= (1 << bits) - 1;
		}

		return output[..count];
	}

	public static string Md5(TString value) => Convert.ToHexStringLower(MD5.HashData(value.toByteArray()));
}