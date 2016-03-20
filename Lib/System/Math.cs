using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib
{
	static public class Math
	{
		const float PI = (float)System.Math.PI;
		const float ONE_DED_RAD = PI / 180.0f;

		/// <summary>
		/// 角度からラジアン
		/// </summary>
		/// <param name="angle"></param>
		/// <returns></returns>
		static public float DegToRad(float angle)
		{
			return angle * ONE_DED_RAD;
		}

		/// <summary>
		/// ラジアンから角度
		/// </summary>
		/// <param name="rad"></param>
		/// <returns></returns>
		static public float RadToDeg(float rad)
		{
			return rad / ONE_DED_RAD;
		}

		static public Type Max<Type>(Type a, Type b)
			where Type : IComparable
		{
			return (a.CompareTo(b) > 0) ? a : b;
		}

		static public Type Min<Type>(Type a, Type b)
			where Type : IComparable
		{
			return (a.CompareTo(b) < 0) ? a : b;
		}

		static public Type Clamp<Type>(Type value, Type min, Type max)
			where Type : IComparable
		{
			return Min(max, Max(value, min));
		}


		/// <summary>
		/// ハルトンシーケンス
		/// </summary>
		static public float HaltonSequence(int index, int base_value)
		{
			float result = 0;
			float f = 1.0f / base_value;
			int i = index;

			while (i > 0) {
				result = result + f * (float)(i % base_value);
				i = (int)(System.Math.Floor((float)i/ base_value));
				f = f / base_value;
			}

			return result;
		}


		/// <summary>
		/// Hammersley Sequence
		/// </summary>
		static public SlimDX.Vector2 Hammersley(uint i, uint N) 
		{
			return new SlimDX.Vector2((float)i / (float)N, radicalInverse_VdC(i));
		}


		static float radicalInverse_VdC(uint bits)
		{
			bits = (bits << 16) | (bits >> 16);
			bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
			bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
			bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
			bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
			return (float)bits * 2.3283064365386963e-10f; // / 0x100000000
		}


		/// <summary>
		/// f16をf32に変換
		/// </summary>
		/// <param name="aVal"></param>
		/// <returns></returns>
		static public float CPf16Tof32(ushort aVal)
		{
			uint signVal = (uint)(aVal >> 15);              //sign bit in MSB
			uint exponent = (uint)((aVal >> 10) & 0x01f);   //next 5 bits after signbit
			uint mantissa = (uint)(aVal & 0x03ff);          //lower 10 bits
			uint rawFloat32Data;                      //raw binary float32 data

			//convert s10e5  5-bit exponent to IEEE754 s23e8  8-bit exponent
			if (exponent == 31) {  // infinity or Nan depending on mantissa
				exponent = 255;
			} else if (exponent == 0) {  //  denormalized floats  mantissa is treated as = 0.f
				exponent = 0;
			} else {  //change 15base exponent to 127base exponent 
				//normalized floats mantissa is treated as = 1.f
				exponent += (127 - 15);
			}

			//convert 10-bit mantissa to 23-bit mantissa
			mantissa <<= (23 - 10);

			//assemble s23e8 number using logical operations
			rawFloat32Data = (signVal << 31) | (exponent << 23) | mantissa;

			//treat raw data as a 32 bit float
			var ary = BitConverter.GetBytes(rawFloat32Data);
			return BitConverter.ToSingle(ary, 0);
		}


		/// <summary>
		/// f32をf16に変換
		/// </summary>
		/// <param name="aVal"></param>
		/// <returns></returns>
		static public ushort CPf32Tof16(float aVal)
		{
			var ary = BitConverter.GetBytes(aVal);
			uint rawf32Data = BitConverter.ToUInt32(ary, 0);

			uint signVal = (rawf32Data >> 31);              //sign bit in MSB
			uint exponent = ((rawf32Data >> 23) & 0xff);    //next 8 bits after signbit
			uint mantissa = (rawf32Data & 0x7fffff);        //mantissa = lower 23 bits

			ushort rawf16Data;

			//convert IEEE754 s23e8 8-bit exponent to s10e5  5-bit exponent      
			if (exponent == 255) {//special case 32 bit float is inf or NaN, use mantissa as is
				exponent = 31;
			} else if (exponent < ((127 - 15) - 10)) {//special case, if  32-bit float exponent is out of 16-bit float range, then set 16-bit float to 0
				exponent = 0;
				mantissa = 0;
			} else if (exponent >= (127 + (31 - 15))) {  // max 15based exponent for s10e5 is 31
				// force s10e5 number to represent infinity by setting mantissa to 0
				//  and exponent to 31
				exponent = 31;
				mantissa = 0;
			} else if (exponent <= (127 - 15)) {  //convert normalized s23e8 float to denormalized s10e5 float

				//add implicit 1.0 to mantissa to convert from 1.f to use as a 0.f mantissa
				mantissa |= (1 << 23);

				//shift over mantissa number of bits equal to exponent underflow
				mantissa = mantissa >> (int)(1 + ((127 - 15) - exponent));

				//zero exponent to treat value as a denormalized number
				exponent = 0;
			} else {  //change 127base exponent to 15base exponent 
				// no underflow or overflow of exponent 
				//normalized floats mantissa is treated as= 1.f, so 
				// no denormalization or exponent derived shifts to the mantissa         
				exponent -= (127 - 15);
			}

			//convert 23-bit mantissa to 10-bit mantissa
			mantissa >>= (23 - 10);

			//assemble s10e5 number using logical operations
			rawf16Data = (ushort)((signVal << 15) | (exponent << 10) | mantissa);

			//return re-assembled raw data as a 32 bit float
			return rawf16Data;
		}
	}

}
