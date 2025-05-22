using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Preagonal.Scripting.GS2Engine;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class TString
{
	public  byte[] buffer = [];
	private int    readc;
	private int    writePos;

	private TString(string str)
	{
		AddBuffer(str, str.Length);
	}

	private TString(byte[] str)
	{
		buffer = str;
		Length = buffer.Length;
	}

	public TString()
	{
	}

	public int Length { get; private set; }

	private void AddBuffer(string? input, int length = 0)
	{
		Array.Resize(ref buffer, Length + length);
		ArgumentNullException.ThrowIfNull(input);
		foreach (var c in Encoding.ASCII.GetBytes(input))
		{
			buffer[writePos] = c;
			writePos++;
			Length++;
		}
	}

	private void AddBuffer(IReadOnlyList<byte> input, int start, int length = 0)
	{
		Array.Resize(ref buffer, Length + length);
		for (var i = start; i < start + length; i++)
		{
			buffer[writePos] = input[i];
			writePos++;
			Length++;
		}
	}

	private void AddBuffer(byte input)
	{
		Array.Resize(ref buffer, Length + 1);

		buffer[writePos] = input;
		writePos++;
		Length++;
	}

	public static implicit operator string(TString d)  => Encoding.ASCII.GetString(d.buffer);
	public static implicit operator TString(string b) => new(b);
	public static implicit operator TString(byte[] b)  => new(b);
	public static bool operator ==(TString? obj1, TString? obj2)
	{
		if (ReferenceEquals(obj1, obj2))
			return true;
		if (ReferenceEquals(obj1, null))
			return false;
		if (ReferenceEquals(obj2, null))
			return false;
		return obj1.Equals(obj2);
	}
	public static bool operator !=(TString? obj1, TString? obj2) => !(obj1 == obj2);
	public static TString operator +(TString a, TString b)
	{
		a.AddBuffer(b.buffer, 0, b.length());
		return a;
	}

	public void setRead(int i) => readc = i;

	public int bytesLeft() => Length - readc;

	public int readInt()
	{
		var val = Array.Empty<byte>();
		read(ref val, 4);
		return (val[0] << 24) + (val[1] << 16) + (val[2] << 8) + val[3];
	}

	private int read(ref byte[] pDest, int pSize)
	{
		var length = this.length() - readc < pSize ? this.length() - readc : pSize;
		if (length <= 0)
		{
			memset(ref pDest, 0, pSize);
			return 0;
		}

		memcpy(ref pDest, buffer, readc, length);
		readc += length;
		return length;
	}

	private static void memcpy(ref byte[] pDest, IReadOnlyList<byte> src, int start, int pSize)
	{
		var j = 0;
		Array.Resize(ref pDest, pSize);
		var end = start + pSize;
		for (var i = start; i < end; i++)
		{
			pDest[j] = src[i];
			j++;
		}
	}

	private static void memset(ref byte[] pDest, byte data, int pSize)
	{
		Array.Resize(ref pDest, pSize);
		for (var i = 0; i < pSize; i++)
			pDest[i] = data;
	}

	public int length() => Length;

	public TString readChars(int pLength)
	{
		TString retVal = new();
		pLength = Clamp(pLength, 0, length() - readc);
		retVal.AddBuffer(buffer, readc, pLength);
		readc += pLength;
		return retVal;
	}

	private static int Clamp(int val, int min, int max)
	{
		if (val.CompareTo(min) < 0) return min;
		return val.CompareTo(max) > 0 ? max : val;
	}

	public byte readChar()
	{
		byte[] val = [];
		read(ref val, 1);
		return val[0];
	}

	public short readShort()
	{
		byte[] val = [];
		read(ref val, 2);
		return (short)((val[0] << 8) + val[1]);
	}

	public short readGShort()
	{
		byte[] val = [];
		read(ref val, 2);
		return (short)(((val[0]-32) << 8) + (val[1]-32));
	}

	public void writeChar(byte pData, bool nullTerminate = false) => AddBuffer(pData);

	public bool starts(string startsWith) => Encoding.ASCII.GetString(buffer).StartsWith(startsWith);

	public void removeStart(int i)
	{
		buffer = buffer.Skip(i).ToArray();
		Length = buffer.Length;
	}

	public override string ToString() => Encoding.ASCII.GetString(buffer);

	private         bool Equals(TString? compare) => ToString() == compare?.ToString();
	public override bool Equals(object? obj)
	{
		if (obj == null || GetType() != obj.GetType()) return false;
		return Equals((TString?)obj);
	}

	public override int  GetHashCode()            => HashCode.Combine(buffer.GetHashCode(), length());

	public bool StartsWith(TString toString, StringComparison culture) =>
		ToString().StartsWith(toString.ToString(), culture);

	public TString ToLower() => ToString().ToLower();
}