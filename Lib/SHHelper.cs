#define SCAN_LINE_INVERSE_		//イメージデータ縦反転

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;


namespace Lib
{
	/// <summary>
	/// SH計算用データ格納
	/// </summary>
	public class SHCalcData
	{
		Color3[][]	colorData_;
		uint		mapSize_;

		public uint MapSize { get { return mapSize_; } }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="texture"></param>
		public SHCalcData(Texture texture)
		{
			int[] s_tex_index = new int[] {
				1,	//-x
				0,	//+x
				4,	//+z
				5,	//-z
				2,	//+y
				3,	//-y
			};

			mapSize_ = (uint)texture.Width;	// wとhが同じ前提

			//	ピクセルデータセットアップ
			colorData_ = new Color3[6][];

			uint size = mapSize_ * mapSize_;
			float inv = 1.0f / 255.0f;

			//	イメージデータ取得
			for(uint id = 0; id < 6; id++) {
				//	テクスチャデータ取得
				byte[] buffer = texture.GetBufferData(s_tex_index[id]);
				colorData_[id] = new Color3[mapSize_ * mapSize_];

				//	テクスチャデータを格納
				if (texture.ImageFormat == SlimDX.DXGI.Format.R8G8B8A8_UNorm) {
					uint bytePerPix = 1 * 4;
					Parallel.For(0, size, idx => {
						uint i = (uint)idx;
						uint address = bytePerPix * i;
						colorData_[id][i].Red = buffer[address + 0] * inv;
						colorData_[id][i].Green = buffer[address + 1] * inv;
						colorData_[id][i].Blue = buffer[address + 2] * inv;
					});
				} else if( texture.ImageFormat == SlimDX.DXGI.Format.R16G16B16A16_Float) {
					//	テクスチャデータを格納
					uint bytePerPix = 2 * 4;
					Parallel.For(0, size, idx => {
						uint i = (uint)idx;
						uint address = bytePerPix * i;
						ushort r = BitConverter.ToUInt16(buffer, (int)address + 0);
						ushort g = BitConverter.ToUInt16(buffer, (int)address + 2);
						ushort b = BitConverter.ToUInt16(buffer, (int)address + 4);
						colorData_[id][i].Red = Math.CPf16Tof32(r);
						colorData_[id][i].Green = Math.CPf16Tof32(g);
						colorData_[id][i].Blue = Math.CPf16Tof32(b);
					});
				}
			}
		}


		//=================================
		//	各面のカラーデータの先頭ポインタを取得する
		//=================================
		public Color3[] GetCubeColorData(uint cube_face) 
		{
			return colorData_[cube_face];
		}

	}



	/// <summary>
	/// SHヘルパークラス
	/// </summary>
	public class SHHelper
	{
		//	球面調和関数の各次の係数の数
		static int[] s_SHCoefNum = new int[] {
			0,
			1,
			4,
			9,
			16,
			25,
			36,
		};


		//	球面調和関数展開に必要な事前計算済み係数
		static float[] c_shPreComputeCoef = new float[] {
			0.282095f,          // Y{0,  0}

			-0.488603f,          // Y{1, -1}
			0.488603f,          // Y{1,  0}
			-0.488603f,          // Y{1,  1}

			1.092548f,          // Y{2, -2}
			-1.092548f,          // Y{2, -1}
			0.315392f,          // Y{2,  0}
			-1.092548f,          // Y{2,  1}
			0.546274f,          // Y{2,  2}

			-0.590044f,          // Y{3, -3}
			2.89061f,           // Y{3, -2}
			-0.457046f,          // Y{3, -1}
			0.373176f,          // Y{3,  0}
			-0.457046f,          // Y{3,  1}
			1.44531f,           // Y{3,  2}
			-0.590044f,          // Y{3,  3}

			2.503343f,          // Y{4, -4}
			-1.770131f,          // Y{4, -3}
			0.946175f,          // Y{4, -2}
			-0.669047f,          // Y{4, -1}
			0.105786f,          // Y{4,  0}
			-0.669047f,          // Y{4,  1}
			0.473087f,          // Y{4,  2}
			-1.770131f,          // Y{4,  3}
			0.625836f,          // Y{4,  4}

			-0.656383f,          // Y{5, -5}
			8.302649f,          // Y{5, -4}
			-0.489238f,          // Y{5, -3}
			4.793537f,          // Y{5, -2}
			-0.452947f,          // Y{5, -1}
			0.116950f,          // Y{5,  0}
			-0.452947f,          // Y{5,  1}
			2.396768f,          // Y{5,  2}
			0.489238f,          // Y{5,  3}
			2.075662f,          // Y{5,  4}
			-0.656383f,          // Y{5,  5}
		};


		public enum CubeMapFace : int
		{
			Left,
			Right,
			Front,
			Back,
			Top,
			Bottom,
			Max,
		};


		float[,,]	m_pCubeSHTable;					//球面調和関数テーブル
		float[,]	m_pDeltaFormFactorTable;		//立体角テーブル
		uint		m_cubeMapSize;					//キューブマップの1面のサイズ
		uint		m_shBandNum;					//球面調和関数を何次まで計算するか




		/// <summary>
		/// 
		/// </summary>
		/// <param name="sh_band_num"></param>
		/// <param name="cubemap_size"></param>
		public SHHelper(uint sh_band_num, uint cubemap_size)
		{
			m_shBandNum = sh_band_num;
			m_pCubeSHTable = new float[(int)CubeMapFace.Max, cubemap_size * cubemap_size, s_SHCoefNum[sh_band_num]];
			m_pDeltaFormFactorTable = new float[(int)CubeMapFace.Max, cubemap_size * cubemap_size];
			m_cubeMapSize = cubemap_size;
		}


		/// <summary>
		/// 立体角を計算する
		/// </summary>
		private void ComputeDeltaFormFactor()
		{
			Vector3 vec;
			float dS = ( 2.0f * 2.0f ) / (float)( m_cubeMapSize * m_cubeMapSize );

			//	キューブマップの全ピクセル位置に対して計算
			for( uint v = 0; v < m_cubeMapSize; v++ ) {
				for( uint u = 0; u < m_cubeMapSize; u++ ) {
#if !SCAN_LINE_INVERSE_
					// left
					{
						vec.X = -1.0f;
						vec.Z = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Left, v * m_cubeMapSize + u ] = d * dS;
					}

					// right 
					{
						vec.X = +1.0f;
						vec.Z = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Right, v * m_cubeMapSize + u ] = d * dS;
					}

					// front 
					{
						vec.Z = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Front, v * m_cubeMapSize + u ] = d * dS;
					}

					// back 
					{  
						vec.Z = +1.0f;
						vec.X = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[(int)CubeMapFace.Back, v * m_cubeMapSize + u ] = d * dS;
					}

					// top
					{
						vec.Y = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = +( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Top, v * m_cubeMapSize + u ] = d * dS;
					}

					// bottom
					{
						vec.Y = +1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Bottom, v * m_cubeMapSize + u ] = d * dS;
					}
#else
					// left
					{
						vec.X = 1.0f;
						vec.Z = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Left, v * m_cubeMapSize + u ] = d * dS;
					}

					// right 
					{
						vec.X = -1.0f;
						vec.Z = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Right, v * m_cubeMapSize + u ] = d * dS;
					}

					// front 
					{
						vec.Z = +1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Front, v * m_cubeMapSize + u ] = d * dS;
					}

					// back 
					{  
						vec.Z = -1.0f;
						vec.X = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Back, v * m_cubeMapSize + u ] = d * dS;
					}

					// top
					{
						vec.Y = +1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = +( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Top, v * m_cubeMapSize + u ] = d * dS;
					}

					// bottom
					{
						vec.Y = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );

						float mag = vec.Length() * vec.LengthSquared();
						float d = 1.0f / mag;
						m_pDeltaFormFactorTable[ (int)CubeMapFace.Bottom, v * m_cubeMapSize + u ] = d * dS;
					}
#endif
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="u"></param>
		/// <param name="v"></param>
		/// <returns></returns>
		private uint UVToIndex(uint u, uint v)
		{
			return v * m_cubeMapSize + u;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cube_face"></param>
		/// <param name="u"></param>
		/// <param name="v"></param>
		/// <param name="sh"></param>
		private void SetToSHTable(int cube_face, uint u, uint v, float[] sh)
		{
			for( int i = 0; i < s_SHCoefNum[m_shBandNum]; i++){
				m_pCubeSHTable[cube_face, UVToIndex(u, v), i] = sh[i];
			}
		}


		/// <summary>
		/// 球面調和関数テーブルを計算する
		/// </summary>
		private void ComputeSHTable()
		{
			float[] sh = new float[36];		//	最大係数分用意しておく
			Vector3 vec;

			for( uint v = 0; v < m_cubeMapSize; v++ ) {
				for( uint u = 0; u < m_cubeMapSize; u++ ) {
#if !SCAN_LINE_INVERSE_
					// left 
					{
						vec.X = -1.0f;
						vec.Z = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Left, u, v, sh);
					}

					// right
					{
						vec.X = +1.0f;
						vec.Z = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Right, u, v, sh);
					}

					// front 
					{
						vec.Z = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Front, u, v, sh);
					}

					// back
					{
						vec.Z = +1.0f;
						vec.X = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Back, u, v, sh);
					}

					// top
					{
						vec.Y = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = +( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Top, u, v, sh);
					}

					// bottom
					{
						vec.Y = +1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Bottom, u, v, sh);
					}
#else
					// left 
					{
						vec.X = 1.0f;
						vec.Z = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Left, u, v, sh);
					}

					// right
					{
						vec.X = -1.0f;
						vec.Z = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Right, u, v, sh);
					}

					// front 
					{
						vec.Z = 1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Front, u, v, sh);
					}

					// back
					{
						vec.Z = -1.0f;
						vec.X = -( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Y = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Back, u, v, sh);
					}

					// top
					{
						vec.Y = 1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = +( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Top, u, v, sh);
					}

					// bottom
					{
						vec.Y = -1.0f;
						vec.X = +( 2.0f * (( (float)( u ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f );
						vec.Z = -( 2.0f * (( (float)( v ) + 0.5f ) / (float)( m_cubeMapSize )) - 1.0f ); 
						vec.Normalize();

						ComputeSphericalHarmonics( sh, ref vec, m_shBandNum );
						SetToSHTable((int)CubeMapFace.Bottom, u, v, sh);
					}
#endif
				}
			}
		}


		/// <summary>
		/// 球面調和関数を計算する
		/// 計算式に関してはStupid Spherical Harmonics(SH)の</summary>
		/// Appendix A2参照
		/// <param name="o_coef"></param>
		/// <param name="vec"></param>
		/// <param name="sh_band"></param>
		void ComputeSphericalHarmonics(float[] o_coef, ref Vector3 vec, uint sh_band)
		{
			//	1次
			// Y{0,0} 
			o_coef[0] = c_shPreComputeCoef[0];

			//	2次
			// Y{1,-1}, Y{1,0}, Y{1,1}
			o_coef[1] = c_shPreComputeCoef[1] * vec.Y;
			o_coef[2] = c_shPreComputeCoef[2] * vec.Z;
			o_coef[3] = c_shPreComputeCoef[3] * vec.X;

			//	3次
			if (sh_band > 2) {
				// Y{2, -2}, Y{2,-1}, Y{2,0}, Y{2,1}, Y{2,2} 
				o_coef[4] = c_shPreComputeCoef[4] * vec.X * vec.Y;
				o_coef[5] = c_shPreComputeCoef[5] * vec.Y * vec.Z;
				o_coef[6] = c_shPreComputeCoef[6] * (3.0f * vec.Z * vec.Z - 1.0f);
				o_coef[7] = c_shPreComputeCoef[7] * vec.X * vec.Z;
				o_coef[8] = c_shPreComputeCoef[8] * (vec.X * vec.X - vec.Y * vec.Y);

				//	4次
				if (sh_band > 3) {
					// Y{3, -3} = A * sqrt(5/8) * (3 * x^2 * y - y^3)
					o_coef[9] = c_shPreComputeCoef[9] * (3.0f * vec.X * vec.X * vec.Y - vec.Y * vec.Y * vec.Y);

					// Y{3, -2} = A * sqrt(15) * x * y * z 
					o_coef[10] = c_shPreComputeCoef[10] * vec.X * vec.Y * vec.Z;

					// Y{3, -1} = A * sqrt(3/8) * y * (5 * z^2 - 1)
					o_coef[11] = c_shPreComputeCoef[11] * vec.Y * (5.0f * vec.Z * vec.Z - 1.0f);

					// Y{3,  0} = A * (1/2) * (5 * z^3 - 3 *z)	
					o_coef[12] = c_shPreComputeCoef[12] * (5.0f * vec.Z * vec.Z * vec.Z - 3.0f * vec.Z);

					// Y{3,  1} = A * sqrt(3/8) * x * (5 * z^2 - 1)
					o_coef[13] = c_shPreComputeCoef[13] * vec.X * (5.0f * vec.Z * vec.Z - 1.0f);

					// Y{3,  2} = A * sqrt(15/4) * z *(x^2 - y^2)
					o_coef[14] = c_shPreComputeCoef[14] * vec.Z * (vec.X * vec.X - vec.Y * vec.Y);

					// Y{3,  3} = A * sqrt(5/8) * (x^3 - 3 * x * y^2)
					o_coef[15] = c_shPreComputeCoef[15] * (vec.X * vec.X * vec.X - 3.0f * vec.X * vec.Y * vec.Y);

					//	5次
					if (sh_band > 4) {
						float x2 = vec.X * vec.X;
						float y2 = vec.Y * vec.Y;
						float z2 = vec.Z * vec.Z;
						float x4 = x2 * x2;
						float y4 = y2 * y2;
						float z4 = z2 * z2;
						o_coef[16] = c_shPreComputeCoef[16] * vec.Y * vec.X * (x2 - y2);               // 4, -4
						o_coef[17] = c_shPreComputeCoef[17] * vec.Y * (3.0f * x2 - y2) * vec.Z;        // 4, -3
						o_coef[18] = c_shPreComputeCoef[18] * vec.Y * vec.X * (-1.0f + 7.0f * z2);     // 4, -2
						o_coef[19] = c_shPreComputeCoef[19] * vec.Y * vec.Z * (-3.0f + 7.0f * z2);     // 4, -1
						o_coef[20] = c_shPreComputeCoef[20] * (35.0f * z4 - 30.0f * z2 + 3.0f);        // 4, 0
						o_coef[21] = c_shPreComputeCoef[21] * vec.X * vec.Z * (-3.0f + 7.0f * z2);     // 4, 1
						o_coef[22] = c_shPreComputeCoef[22] * (x2 - y2) * (-1.0f + 7.0f * z2);       // 4, 2
						o_coef[23] = c_shPreComputeCoef[23] * vec.X * (x2 - 3.0f * y2) * vec.Z;        // 4, 3
						o_coef[24] = c_shPreComputeCoef[24] * (x4 - 6.0f * y2 * x2 + y4);              // 4, 4

						//	6次
						if (sh_band > 5) {
							o_coef[25] = c_shPreComputeCoef[25] * vec.Y * (5.0f * x4 - 10.0f * y2 * x2 + y4);          // 5, -5
							o_coef[26] = c_shPreComputeCoef[26] * vec.Y * vec.X * (x2 - y2) * vec.Z;                   // 5, -4
							o_coef[27] = c_shPreComputeCoef[27] * vec.Y * (3.0f * x2 - y2) * (-1.0f + 9.0f * z2);    // 5, -3
							o_coef[28] = c_shPreComputeCoef[28] * vec.Y * vec.X * vec.Z * (-1.0f + 3.0f * z2);         // 5, -2
							o_coef[29] = c_shPreComputeCoef[29] * vec.Y * (-14.0f * z2 + 21.0f * z4 + 1.0f);           // 5, -1
							o_coef[30] = c_shPreComputeCoef[30] * vec.Z * (63.0f * z4 - 70.0f * z2 + 15.0f);           // 5, 0
							o_coef[31] = c_shPreComputeCoef[31] * vec.X * (-14.0f * z2 + 21.0f * z4 + 1.0f);           // 5, 1
							o_coef[32] = c_shPreComputeCoef[32] * (x2 - y2) * vec.Z * (-1.0f + 3.0f * z2);           // 5, 2
							o_coef[33] = c_shPreComputeCoef[33] * vec.X * (x2 - 3.0f * y2) * (-1.0f + 9.0f * z2);    // 5, 3
							o_coef[34] = c_shPreComputeCoef[34] * (x4 - 6.0f * y2 * x2 + y4) * vec.Z;                  // 5, 4
							o_coef[35] = c_shPreComputeCoef[35] * vec.X * (x4 - 10.0f * y2 * x2 + 5.0f * y4);          // 5, 5
						}
					}
				}
			}
		}


		/// <summary>
		/// 球面調和関数を用いて圧縮する(Rのみ)
		/// </summary>
		/// <param name="coef_list"></param>
		/// <param name="pCube"></param>
		public void CompressR(float[] coef_list, SHCalcData pCube)
		{
			//	SH計算
			ComputeSHTable();
			//	立体角計算
			ComputeDeltaFormFactor();

			//	イメージデータを圧縮
			for (int l = 0; l < s_SHCoefNum[m_shBandNum]; ++l) {
				double r = 0.0;
				for (uint f = 0; f < (int)CubeMapFace.Max; ++f) {
					Color3[] face_color = pCube.GetCubeColorData(f);
					for (uint p = 0; p < m_cubeMapSize * m_cubeMapSize; ++p) {
						double value = ((double)m_pDeltaFormFactorTable[f, p] * (double)m_pCubeSHTable[f, p, l]);
						r += ((double)face_color[p].Red * value);
					}
				}

				coef_list[l] = (float)(r);
			}
		}



		/// <summary>
		/// 球面調和関数を用いて圧縮する(RGBのみ)
		/// </summary>
		/// <param name="coef_list"></param>
		/// <param name="pCube"></param>
		public Vector3[] CompressRGB(SHCalcData pCube)
		{
			//	SH計算
			ComputeSHTable();
			//	立体角計算
			ComputeDeltaFormFactor();

			//	イメージデータを圧縮
			Vector3[] coefList = new Vector3[s_SHCoefNum[m_shBandNum]];
			for (int l = 0; l < s_SHCoefNum[m_shBandNum]; ++l) {
				double r = 0.0;
				double g = 0.0;
				double b = 0.0;

				for (uint f = 0; f < (int)CubeMapFace.Max; ++f) {
					Color3[] face_color = pCube.GetCubeColorData(f);
					for (uint p = 0; p < m_cubeMapSize * m_cubeMapSize; ++p) {
						double value = ((double)m_pDeltaFormFactorTable[f, p] * (double)m_pCubeSHTable[f, p, l]);
						r += (double)(face_color[p].Red * value);
						g += (double)(face_color[p].Green * value);
						b += (double)(face_color[p].Blue * value);
					}
				}

				coefList[l].X = (float)(r);
				coefList[l].Y = (float)(g);
				coefList[l].Z = (float)(b);
			}
			return coefList;
		}


		/// <summary>
		/// 球面調和関数展開
		/// </summary>
		/// <param name="coef_list"></param>
		/// <param name="pTextureList"></param>
		public void DecompressToTexture(Vector3[] coef_list, Texture[] pTextureList)
		{
			////	SH計算
			////ComputeSHTable();

			//u8* data = new u8[sizeof(ksTexHeader) + sizeof(uint) * (m_cubeMapSize * m_cubeMapSize)];
			//ksTexHeader* pHeader = (ksTexHeader*)data;
			//uint* texData = (uint*)(data + sizeof(ksTexHeader));

			////	キューブマップの各面のテクスチャを作成する
			//for(int f = 0; f < (int)CubeMapFace.Max; f++) {
			//	pHeader->width = pHeader->height = m_cubeMapSize;
			//	pHeader->type = 1;

			//	float r, g, b;
			//	for( uint p = 0; p < m_cubeMapSize * m_cubeMapSize; p++ ) {
			//		r = g = b = 0;
			//		for( int l = 0; l < s_SHCoefNum[m_shBandNum]; l++ ) {
			//			r += ( coef_list[l].r * GetSHTablePointerByIndex(f, p)[l] );
			//			g += ( coef_list[l].g * GetSHTablePointerByIndex(f, p)[l] );
			//			b += ( coef_list[l].b * GetSHTablePointerByIndex(f, p)[l] );
			//		}

			//		u8 br = (u8)ksUtilMath::Clamp((int)(r * 255), 0, 255); 
			//		u8 bg = (u8)ksUtilMath::Clamp((int)(g * 255), 0, 255); 
			//		u8 bb = (u8)ksUtilMath::Clamp((int)(b * 255), 0, 255);
			//		texData[p] = (br << 0) | (bg << 8) | (bb << 16) | 0xff000000;
			//	}
			//	pTextureList[f].Initialize(data);
			//}

			//delete [] data;
		}


		/// <summary>
		/// スケール係数を渡して球面調和関数の基底関数の係数を取得
		/// </summary>
		/// <param name="out_value"></param>
		/// <param name="scale_coef"></param>
		/// <param name="index"></param>
		static public void GetSphericalHarmonics(out Vector3 out_value, ref Vector3 scale_coef, uint index)
		{
			out_value = scale_coef * c_shPreComputeCoef[index];
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="out_value"></param>
		/// <param name="scale_coef"></param>
		/// <param name="index"></param>
		static public void GetSphericalHarmonics(out Vector4 out_value, ref Vector3 scale_coef, uint index)
		{
			out_value = new Vector4(scale_coef * c_shPreComputeCoef[index], 1);
		}


		/// <summary>
		/// キューブマップからSH係数を取得
		/// </summary>
		static public Vector4[] GetSphericalHarmonics(Texture cubemap, uint band)
		{
			SHCalcData shData = new SHCalcData(cubemap);
			SHHelper shHelper = new SHHelper(band, shData.MapSize);
			Vector3[] shCoef = shHelper.CompressRGB(shData);
			int coefNum = s_SHCoefNum[band];
			Vector4[] shScalingCoef = new Vector4[coefNum];
			for (uint i = 0; i < coefNum; i++) {
				SHHelper.GetSphericalHarmonics(out shScalingCoef[i], ref shCoef[i], i);
			}
			return shScalingCoef;
		}
	}
}
